using System.Data;
using System.Security.Cryptography;
using System.Text;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Models;
using CompanyHQ.Sales.Contracts.Ports;
namespace CompanyHQ.Sales.Services;

/// <summary>Atomic unpaid checkout, immutable pricing history, ownership and replay-protected upsells.</summary>
public sealed partial class SalesOrderService(IPersistence db,SalesCatalogService catalog,SalesCheckoutService checkout,
    TimeProvider clock,SalesOptions options,SalesCustomerResolution? customers=null,ISalesShipping? shipping=null) : ISalesOrderService
{
    private static readonly Guid CounterId=Guid.Parse("7147ed7e-2a68-4c22-8a2c-c68bad4a370b");
    public async Task<OrderReceiptDto> PlaceAsync(PlaceOrderRequest request,Guid? actor,CancellationToken ct=default)
    {
        var prepared=await PrepareAsync(request,actor,ct);
        return await PersistAsync(prepared,actor,null,ct);
    }
    internal async Task<PreparedOrder> PrepareAsync(PlaceOrderRequest request,Guid? actor,CancellationToken ct,bool staff=false)
    {
        SalesRules.Validate(request);if(request.BillingAddress is not null)SalesRules.Validate(request.BillingAddress);
        if(actor.HasValue)
        {
            if(!options.EnableAuthenticatedCheckout)throw new UnauthorizedAccessException("Authenticated checkout is disabled.");
            if(customers is null)throw new InvalidOperationException("Authenticated customer resolution is not configured.");
            var profile=await customers.ResolveAsync(actor.Value,ct);
            // No UserId, saved-address id, or arbitrary customer's details enter self-service checkout.
            request=request with {CustomerFirstName=profile.FirstName,CustomerLastName=profile.LastName,CustomerEmail=profile.Email,CustomerPhone=profile.Phone};
        }
        var priced=await checkout.CalculateAsync(request.Sale,true,ct,staff);
        request=request with {Sale=request.Sale with {Lines=priced.Cart.Lines}};
        PaymentMethod? method=null;
        if(request.PaymentMethodId.HasValue)
        {
            method=await db.Query<PaymentMethod>().FirstOrDefaultAsync(x=>x.Id==request.PaymentMethodId && x.IsActive,ct);
            if(method is null)throw new ArgumentException("Payment method is unavailable.");
            ValidatePaymentLimits(SalesRules.Read<SavePaymentMethodRequest>(method.DefinitionJson),priced.Price.TotalAmount,priced.Shipping,!staff);
        }
        await EnsureCounterAsync(ct);
        return new(request,priced,method?.Name);
    }
    private async Task EnsureCounterAsync(CancellationToken ct)
    {
        try { await db.InTransactionAsync(async token=>
        {
            if(!await db.Query<SalesCounter>().AnyAsync(x=>x.Id==CounterId,token))
            {db.Add(new SalesCounter{Id=CounterId});await db.SaveChangesAsync(token);}
        },cancellationToken:ct); }
        catch(UniqueConstraintException) {db.Clear();}
    }
    internal async Task<OrderReceiptDto> PersistAsync(PreparedOrder prepared,Guid? actor,Guid? sourceQuote,CancellationToken ct,int? invoiceOverride=null)
    {
        var request=prepared.Request;var priced=prepared.Priced;
        var guest=actor.HasValue?null:Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        // The counter row serializes numbering. Its unique primary key also protects first use on
        // a fresh consumer database, without relying on host seed order.
        for(var attempt=0;;attempt++)
        {
            try
            {
                return await db.InTransactionAsync(async token=>
                {
                    var counter=await db.Locked<SalesCounter>().FirstOrDefaultAsync(x=>x.Id==CounterId,token);
                    if(counter is null){counter=new(){Id=CounterId};db.Add(counter);await db.SaveChangesAsync(token);}
                    var number=checked(++counter.Number);
                    var invoice=invoiceOverride ?? checked(++counter.InvoiceNumber);
                    while(!invoiceOverride.HasValue && await db.Query<SalesOrder>().AnyAsync(x=>x.InvoiceNumber==invoice,token))invoice=checked(++counter.InvoiceNumber);
                    if(invoice<=0 || await db.Query<SalesOrder>().AnyAsync(x=>x.InvoiceNumber==invoice,token))throw new SalesConflictException("Invoice number is unavailable.");
                    var reference=$"{options.InvoicePrefix}-{invoice:0000}";
                    var order=new SalesOrder {Number=number,InvoiceNumber=invoice,InvoiceReference=reference,ManualDiscountType=prepared.Adjustments?.ManualDiscountType,ManualDiscountValue=prepared.Adjustments?.ManualDiscountValue ?? 0,CustomerFirstName=request.CustomerFirstName,CustomerLastName=request.CustomerLastName,CustomerEmail=request.CustomerEmail,UserId=actor,GuestTokenHash=guest is null?null:Hash(guest),
                        GuestTokenExpiresAt=guest is null?null:clock.GetUtcNow().AddDays(options.GuestTokenLifetimeDays),
                        Status=OrderStatus.Pending,PaymentStatus=PaymentStatus.Unpaid,RequestJson=SalesRules.Write(request),
                        SnapshotJson=SalesRules.Write(priced),ShippingJson=priced.Shipping is null?null:SalesRules.Write(priced.Shipping),
                        PaymentMethodName=prepared.PaymentMethodName,SourceQuoteId=sourceQuote,TotalAmount=priced.Price.TotalAmount,Currency=priced.Price.Currency,ItemCount=priced.Price.ItemCount};
                    db.Add(order);await UpdateUnitsAsync([],priced.AdminLines,token);await db.SaveChangesAsync(token);
                    return new OrderReceiptDto(order.Id,number,invoice,reference,order.Currency,order.TotalAmount,guest,order.GuestTokenExpiresAt);
                },cancellationToken:ct);
            }
            catch(UniqueConstraintException) when(attempt<2) {db.Clear();}
        }
    }
    private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private async Task<bool> Access(SalesOrder order,Guid? actor,string? token,CancellationToken ct)
    {
        if(actor.HasValue && (order.UserId==actor || await catalog.Manager(actor,ct)))return true;
        if(order.UserId.HasValue || string.IsNullOrEmpty(token) || token.Length>200 || order.GuestTokenHash is null || order.GuestTokenExpiresAt<=clock.GetUtcNow())return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(order.GuestTokenHash),Convert.FromHexString(Hash(token)));
    }
    private async Task<SalesOrder> WithRevision(SalesOrder row,CancellationToken ct)
    {
        if(row.RevisionNumber==0)return row;
        var revision=await db.Query<SalesOrderRevision>().Where(x=>x.OrderId==row.Id && x.Number==row.RevisionNumber).FirstAsync(ct);
        var prepared=SalesRules.Read<PreparedOrder>(revision.PreparedJson);
        row=System.Text.Json.JsonSerializer.Deserialize<SalesOrder>(System.Text.Json.JsonSerializer.Serialize(row,SalesRules.Json),SalesRules.Json)!;
        row.RequestJson=SalesRules.Write(prepared.Request);row.SnapshotJson=SalesRules.Write(prepared.Priced);
        row.ShippingJson=prepared.Priced.Shipping is null?null:SalesRules.Write(prepared.Priced.Shipping);
        row.PaymentMethodName=prepared.PaymentMethodName;
        return row;
    }
    private async Task<SalesOrder?> ReadAccessible(Guid id,Guid? actor,string? token,CancellationToken ct)
    {var row=await db.Query<SalesOrder>().FirstOrDefaultAsync(x=>x.Id==id,ct);return row is not null && await Access(row,actor,token,ct)?await WithRevision(row,ct):null;}
    private async Task<List<OrderUpsellAcceptance>> Additions(Guid id,CancellationToken ct,int? revision=null)=>await db.Query<OrderUpsellAcceptance>().Where(x=>x.OrderId==id && (!revision.HasValue || x.RevisionNumber==revision.Value)).OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).ToListAsync(ct);
    public async Task<ClientOrderDto?> GetAsync(Guid id,Guid? actor,string? token,CancellationToken ct=default)
    {var row=await ReadAccessible(id,actor,token,ct);return row is null?null:ToDto(row,await Additions(id,ct,row.RevisionNumber),await catalog.Manager(actor,ct));}
    public async Task<PagedResult<ClientOrderSummaryDto>> ListAsync(PageRequest page,Guid actor,SearchRequest? search=null,CancellationToken ct=default)
    {
        var manager=await catalog.Manager(actor,ct);var result=await db.Query<SalesOrder>().Where(x=>manager || x.UserId==actor).Search(search?.Field,search?.Keyword,manager?typeof(AdminOrderSummaryDto):typeof(ClientOrderSummaryDto)).OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);
        return new(result.Items.Select(row=> {return manager?(ClientOrderSummaryDto)new AdminOrderSummaryDto(row.Id,row.InvoiceNumber,row.CreatedAt,row.Status,row.PaymentStatus,row.Currency,row.TotalAmount,row.ItemCount,row.Number,row.UserId,row.CustomerFirstName,row.CustomerLastName,row.CustomerEmail):new ClientOrderSummaryDto(row.Id,row.InvoiceNumber,row.CreatedAt,row.Status,row.PaymentStatus,row.Currency,row.TotalAmount,row.ItemCount);}).ToArray(),page.Page,page.PageSize,result.Total);
    }
    public async Task<OrderUpsellContextDto?> ContextAsync(Guid id,Guid? actor,string? token,CancellationToken ct=default)
    {
        var order=await ReadAccessible(id,actor,token,ct);if(order is null)return null;
        var additions=await Additions(id,ct,order.RevisionNumber);var offers=await Offers(order,additions,ct);
        return new(ToDto(order,additions,await catalog.Manager(actor,ct)),offers,order.FinalizedAt.HasValue);
    }
    private async Task<IReadOnlyList<OrderUpsellOfferDto>> Offers(SalesOrder order,IReadOnlyList<OrderUpsellAcceptance> additions,CancellationToken ct)
    {
        if(order.FinalizedAt.HasValue || order.Status!=OrderStatus.Pending || order.PaymentStatus!=PaymentStatus.Unpaid)return [];
        var stored=SalesRules.Read<PricedBasket>(order.SnapshotJson);
        var parents=stored.Cart.Lines.Where(x=>!x.UpsellParentLineId.HasValue).ToArray();
        var parentIds=parents.Select(x=>x.ProductId).Distinct().ToArray();
        var acceptedOffers=db.Query<OrderUpsellAcceptance>().Where(x=>x.OrderId==order.Id);
        var added=stored.Cart.Lines.Where(x=>x.UpsellParentLineId.HasValue).Select(x=>x.ProductId).Concat(additions.Select(x=>SalesRules.Read<SalesLineRequest>(x.LineJson).ProductId)).ToHashSet();
        var rows=await db.Query<UpsellOffer>().Where(x=>parentIds.Contains(x.ParentProductId) && !acceptedOffers.Any(a=>a.OfferId==x.Id)).OrderBy(x=>x.Id).Take(1001).ToListAsync(ct);
        if(rows.Count>1000)throw new ArgumentException("Upsell relationship limit exceeded.");
        var targetIds=rows.Select(x=>x.ProductId).Distinct().ToArray();
        var visible=await db.Query<Product>().Where(x=>targetIds.Contains(x.Id) && x.IsVisible).ToArrayAsync(ct);
        var graph=await catalog.Graph(visible.Select(x=>x.Id),ct);var coupons=await catalog.Coupons(ct);var pricing=new CatalogPricing(graph,coupons,catalog.Today,catalog.PricingOptions);
        var extra=(await db.Query<SalesSettings>().FirstOrDefaultAsync(x=>x.Id==SalesPromotionService.SettingsId,ct))?.UpsellExtraDiscountPercent ?? 0;
        var result=new List<OrderUpsellOfferDto>();
        foreach(var row in rows.Where(x=>!added.Contains(x.ProductId)))
        {
            if(!graph.TryGetValue(row.ProductId,out var product))continue;
            var definition=SalesRules.Read<SaveUpsellRequest>(row.DefinitionJson);
            foreach(var parent in parents.Where(x=>x.ProductId==row.ParentProductId))
            {
                // Saved options are used: a later parent catalog edit cannot rewrite the order.
                var parentVariant=new SaveVariantRequest(parent.VariantId ?? Guid.Empty,"snapshot",0,Options:parent.Options);
                var variant=CatalogPricing.ResolveUpsell(product,parentVariant,definition.SyncVariants);
                if(variant is null || product.Currency!=order.Currency)continue;
                var price=pricing.Price(row.ProductId,variant);var final=SalesRules.Money(Math.Max(0,price.Effective-SalesRules.Discount(price.Selling,definition.ValueType,definition.Value)-SalesRules.Discount(price.Selling,DiscountType.Percentage,extra)));
                result.Add(new(parent.Id,new(row.Id,row.ParentProductId,row.ProductId,variant.Id,product.Name,string.Join(" / ",(variant.Options ?? new Dictionary<string,string>()).Values),product.ImageUrls?.FirstOrDefault(),definition.Quantity,product.Currency,price.Selling,price.Effective,final,price.Selling*definition.Quantity,final*definition.Quantity,price.Selling>0?SalesRules.Money((price.Selling-final)/price.Selling*100):0,string.Join(", ",price.Discounts.Select(x=>x.Label).Append("Relationship and company upsell")))));
            }
        }
        return result;
    }
    public async Task<UpsellAcceptanceDto?> AcceptUnpaidAsync(Guid id,AcceptUpsellRequest request,Guid? actor,string? token,CancellationToken ct=default)
    {
        if(request.RequestId==Guid.Empty || request.OfferId==Guid.Empty || request.ParentLineId==Guid.Empty)throw new ArgumentException("Stable request, offer and parent ids are required.");
        var order=await ReadAccessible(id,actor,token,ct);if(order is null)return null;
        var history=await Additions(id,ct);var additions=history.Where(x=>x.RevisionNumber==order.RevisionNumber).ToList();var replay=history.FirstOrDefault(x=>x.RequestId==request.RequestId);
        if(replay is not null)
        {if(replay.OfferId!=request.OfferId || SalesRules.Read<SalesLineRequest>(replay.LineJson).UpsellParentLineId!=request.ParentLineId)throw new ArgumentException("Request id was used for another acceptance.");return SalesRules.Read<UpsellAcceptanceDto>(replay.ResultJson);}
        var offer=(await Offers(order,additions,ct)).SingleOrDefault(x=>x.ParentLineId==request.ParentLineId && x.Offer.UpsellOfferId==request.OfferId)?.Offer;
        if(offer is null)throw new ArgumentException("Offer is unavailable, already added, or order is closed.");
        var graph=await catalog.Graph([offer.ProductId],ct);var variant=graph[offer.ProductId].Variants.Single(x=>x.Id==offer.VariantId);
        var couponRules=await catalog.Coupons(ct);
        var price=new CatalogPricing(graph,couponRules,catalog.Today,catalog.PricingOptions).Price(offer.ProductId,variant);
        var line=new SalesLineRequest(Guid.CreateVersion7(),offer.ProductId,offer.VariantId,offer.Quantity,variant.Options,UpsellParentLineId:request.ParentLineId);
        var discounts=price.Discounts.Select(x=>x with {DiscountAmount=x.DiscountAmount*offer.Quantity}).ToList();
        var relationship=SalesRules.Money((price.Effective-offer.FinalUnitPrice)*offer.Quantity);
        if(relationship>0)discounts.Add(new(SalesDiscountSource.Upsell,"Relationship and company upsell",null,relationship,null,0));
        var total=offer.FinalLineTotal;var cost=SalesRules.Money(price.Cost*offer.Quantity);
        var pricedLine=new AdminSalesLineDto(line.Id,line.ProductId,line.VariantId,offer.ParentProductId,offer.ProductName,offer.VariantName,variant.Sku,offer.ImageUrl,line.Quantity,
            offer.BaseUnitPrice,offer.OriginalLineTotal,offer.FinalUnitPrice,total,offer.OriginalLineTotal-total,offer.DiscountSource,discounts,price.Cost,cost,total-cost,total>0?SalesRules.Money((total-cost)/total*100):0);
        var stored=SalesRules.Read<PricedBasket>(order.SnapshotJson);var original=SalesRules.Read<PlaceOrderRequest>(order.RequestJson);
        var all=stored.Cart.Lines.Concat(additions.Select(x=>SalesRules.Read<SalesLineRequest>(x.LineJson))).Append(line).ToArray();
        var previous=LatestShipping(order,additions);SalesShippingQuote? quote=previous;
        if(original.Sale.ShippingAddress is not null)
        {
            if(shipping is null)throw new ArgumentException("Shipping is not configured.");
            quote=await shipping.QuoteAsync(original.Sale.ShippingAddress,all,original.Sale.CouponCodes ?? [],ct);
            if(!quote.IsAvailable)throw new ArgumentException(quote.Message ?? "Upsell cannot be shipped.");
        }
        var couponSnapshots=discounts.Where(x=>x.CouponCode is not null).GroupBy(x=>x.CouponCode!).Select(g=>
        {var c=couponRules.Single(x=>x.Definition.Code==g.Key);return new SalesAppliedCouponDto(c.Id,c.Definition.Code,c.Definition.DiscountType,c.Definition.DiscountValue,c.Definition.AllowsCombination,false,g.Sum(x=>x.DiscountAmount));})
            .Concat(couponRules.Where(c=>quote?.CouponCodes.Contains(c.Definition.Code)==true).Select(c=>new SalesAppliedCouponDto(c.Id,c.Definition.Code,c.Definition.DiscountType,c.Definition.DiscountValue,c.Definition.AllowsCombination,true,quote!.Discount))).ToArray();
        var extraAmount=SalesRules.Money(total+(quote?.TotalCost ?? 0)-(previous?.TotalCost ?? 0));
        return await db.InTransactionAsync(async transactionToken=>
        {
            var locked=await db.Locked<SalesOrder>().Where(x=>x.Id==id).FirstAsync(transactionToken);
            if(!await Access(locked,actor,token,transactionToken))throw new UnauthorizedAccessException();
            var allCurrent=await Additions(id,transactionToken);var current=allCurrent.Where(x=>x.RevisionNumber==locked.RevisionNumber).ToList();var duplicate=allCurrent.FirstOrDefault(x=>x.RequestId==request.RequestId);
            if(duplicate is not null)
            {if(duplicate.OfferId!=request.OfferId || SalesRules.Read<SalesLineRequest>(duplicate.LineJson).UpsellParentLineId!=request.ParentLineId)throw new ArgumentException("Request id was used for another acceptance.");return SalesRules.Read<UpsellAcceptanceDto>(duplicate.ResultJson);}
            if(locked.FinalizedAt.HasValue || locked.Status!=OrderStatus.Pending || locked.PaymentStatus!=PaymentStatus.Unpaid || locked.RevisionNumber!=order.RevisionNumber || current.Count!=additions.Count || allCurrent.Any(x=>x.OfferId==request.OfferId))throw new SalesConflictException("Order changed; reload the server offer.");
            if(original.PaymentMethodId.HasValue)
            {
                var method=await db.Query<PaymentMethod>().FirstOrDefaultAsync(x=>x.Id==original.PaymentMethodId && x.IsActive,transactionToken)
                    ?? throw new ArgumentException("Payment method is unavailable.");
                ValidatePaymentLimits(SalesRules.Read<SavePaymentMethodRequest>(method.DefinitionJson),
                    SalesRules.Money(locked.TotalAmount+extraAmount),quote);
            }
            locked.TotalAmount=SalesRules.Money(locked.TotalAmount+extraAmount);locked.ItemCount+=line.Quantity;
            var result=new UpsellAcceptanceDto(request.RequestId,line.Id,extraAmount,locked.TotalAmount,false);
            await UpdateUnitsAsync([],[pricedLine],transactionToken);
            db.Add(new OrderUpsellAcceptance {OrderId=id,RevisionNumber=locked.RevisionNumber,RequestId=request.RequestId,OfferId=request.OfferId,ResultJson=SalesRules.Write(result),LineJson=SalesRules.Write(line),PricedLineJson=SalesRules.Write(pricedLine),CouponsJson=SalesRules.Write(couponSnapshots),ShippingJson=quote is null?null:SalesRules.Write(quote)});
            await db.SaveChangesAsync(transactionToken);return result;
        },cancellationToken:ct);
    }
    public async Task<ClientOrderDto?> FinalizeAsync(Guid id,Guid? actor,string? token,CancellationToken ct=default)
    {
        return await db.InTransactionAsync(async t=>
        {
            var row=await db.Locked<SalesOrder>().FirstOrDefaultAsync(x=>x.Id==id,t);
            if(row is null || !await Access(row,actor,token,t))return null;
            if(row.Status!=OrderStatus.Pending)throw new SalesConflictException("Only pending orders can finalize offers.");
            row.FinalizedAt ??=clock.GetUtcNow();await db.SaveChangesAsync(t);
            return ToDto(await WithRevision(row,t),await Additions(id,t,row.RevisionNumber),await catalog.Manager(actor,t));
        },cancellationToken:ct);
    }
    public async Task<ClientOrderDto?> ChangeStatusAsync(Guid id,ChangeOrderStatusRequest request,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);if(!Enum.IsDefined(request.Status))throw new ArgumentException("Invalid status.");
        return await db.InTransactionAsync(async t=>
        {
            var row=await db.Locked<SalesOrder>().FirstOrDefaultAsync(x=>x.Id==id,t);if(row is null)return null;
            var allowed=row.Status==request.Status || (row.Status,request.Status) is (OrderStatus.Pending,OrderStatus.Cancelled) or (OrderStatus.Pending,OrderStatus.Processing) or (OrderStatus.Processing,OrderStatus.Shipped) or (OrderStatus.Processing,OrderStatus.Cancelled) or (OrderStatus.Shipped,OrderStatus.Delivered);
            if(!allowed)throw new SalesConflictException("Invalid order status transition.");
            row.Status=request.Status;if(request.Status==OrderStatus.Shipped)row.ShippedAt ??=clock.GetUtcNow();if(request.Status==OrderStatus.Delivered)row.DeliveredAt ??=clock.GetUtcNow();
            if(request.Status!=OrderStatus.Pending)row.FinalizedAt ??=clock.GetUtcNow();await db.SaveChangesAsync(t);return ToDto(await WithRevision(row,t),await Additions(id,t,row.RevisionNumber),true);
        },cancellationToken:ct);
    }
    private static void ValidatePaymentLimits(SavePaymentMethodRequest rules,decimal total,SalesShippingQuote? quote,bool checkTotal=true)
    {
        if(checkTotal && (rules.MaxOrderTotal.HasValue && total>rules.MaxOrderTotal || rules.MinOrderTotal.HasValue && total<rules.MinOrderTotal))
            throw new ArgumentException("Payment method total limits exceeded.");
        if(rules.MaxDistanceKm.HasValue && (quote is null || quote.Routes.Count==0 ||
            quote.Routes.Any(r=>r.DistanceKm is not { } distance || !double.IsFinite(distance) || distance<0 || distance>rules.MaxDistanceKm)))
            throw new ArgumentException("Payment method distance limit exceeded.");
    }
    private static SalesShippingQuote? LatestShipping(SalesOrder row,IReadOnlyList<OrderUpsellAcceptance> additions)
    {var json=additions.LastOrDefault()?.ShippingJson ?? row.ShippingJson;return json is null?null:SalesRules.Read<SalesShippingQuote>(json);}
    private static ClientOrderDto ToDto(SalesOrder row,IReadOnlyList<OrderUpsellAcceptance> additions,bool admin)
    {
        var request=SalesRules.Read<PlaceOrderRequest>(row.RequestJson);var stored=SalesRules.Read<PricedBasket>(row.SnapshotJson);var quote=LatestShipping(row,additions);
        var lines=stored.AdminLines.Concat(additions.Select(x=>SalesRules.Read<AdminSalesLineDto>(x.PricedLineJson))).ToArray();
        IReadOnlyList<ClientSalesLineDto> output=admin?lines:lines.Select(SalesBasketPricing.ClientLine).ToArray();
        var snapshots=stored.Price.Coupons.Where(c=>!c.AppliesToShipping).Concat(additions.SelectMany(x=>SalesRules.Read<SalesAppliedCouponDto[]>(x.CouponsJson)).Where(c=>!c.AppliesToShipping));
        var coupons=snapshots.GroupBy(c=>c.CouponCode).Select(g=>g.First() with {AppliedDiscountAmount=g.Sum(x=>x.AppliedDiscountAmount)})
            .Concat(additions.Count>0?SalesRules.Read<SalesAppliedCouponDto[]>(additions.Last().CouponsJson).Where(c=>c.AppliesToShipping):stored.Price.Coupons.Where(c=>c.AppliesToShipping)).ToArray();
        var subtotal=lines.Sum(x=>x.OriginalTotalPrice);var discountAmount=lines.Sum(x=>x.LineDiscountAmount)+stored.Price.ManualDiscountAmount+(quote?.Discount ?? 0);
        return admin?new AdminOrderDto(row.Id,row.Number,row.InvoiceNumber,row.InvoiceReference,row.CreatedAt,row.Status,row.PaymentStatus,request.CustomerFirstName,request.CustomerLastName,request.CustomerEmail,request.CustomerPhone,request.CompanyName,request.VatNumber,request.Sale.ShippingAddress,request.BillingSameAsShipping,request.BillingSameAsShipping?request.Sale.ShippingAddress:request.BillingAddress,quote?.MethodName,quote?.EstimatedDeliveryDays,row.PaymentMethodName,row.Currency,output,coupons,row.ItemCount,subtotal,discountAmount,quote?.BaseCost ?? 0,stored.Price.TaxAmount,row.TotalAmount,request.Notes,row.ShippedAt,row.DeliveredAt,row.UserId,quote?.MethodId,quote?.MethodKind,request.PaymentMethodId,null,row.ManualDiscountType,row.ManualDiscountValue,lines.Sum(x=>x.ProfitAmount)-stored.Price.ManualDiscountAmount,row.SourceQuoteId,row.FinalizedAt,row.CreatedAt,row.UpdatedAt):
            new ClientOrderDto(row.Id,row.Number,row.InvoiceNumber,row.InvoiceReference,row.CreatedAt,row.Status,row.PaymentStatus,request.CustomerFirstName,request.CustomerLastName,request.CustomerEmail,request.CustomerPhone,request.CompanyName,request.VatNumber,request.Sale.ShippingAddress,request.BillingSameAsShipping,request.BillingSameAsShipping?request.Sale.ShippingAddress:request.BillingAddress,quote?.MethodName,quote?.EstimatedDeliveryDays,row.PaymentMethodName,row.Currency,output,coupons,row.ItemCount,subtotal,discountAmount,quote?.BaseCost ?? 0,stored.Price.TaxAmount,row.TotalAmount,request.Notes,row.ShippedAt,row.DeliveredAt);
    }
}
public sealed class SalesConflictException(string message) : Exception(message);

internal sealed record PreparedOrder(PlaceOrderRequest Request,PricedBasket Priced,string? PaymentMethodName,StaffPricingRequest? Adjustments=null);
