using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Enums;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Models;
using CompanyHQ.Services.Persistence.Services;
namespace CompanyHQ.Services.Sales.Services;
public sealed partial class SalesOrderService
{
    internal async Task<PreparedOrder> PrepareStaffAsync(PlaceOrderRequest request,Guid? userId,StaffPricingRequest? adjustment,CancellationToken ct)
    {
        var prepared=await PrepareAsync(request,userId,ct,true);var a=adjustment ?? new StaffPricingRequest();SalesRules.Validate(a);
        if(a.ManualDiscountType.HasValue && (!Enum.IsDefined(a.ManualDiscountType.Value) || a.ManualDiscountType==DiscountType.FreeShipping) ||
            a.ManualDiscountType==DiscountType.Percentage && a.ManualDiscountValue>100 || !a.ManualDiscountType.HasValue && a.ManualDiscountValue!=0 || (a.LineDiscounts?.Count ?? 0)>200)
            throw new ArgumentException("Invalid staff order discount.");
        var lines=prepared.Priced.AdminLines.ToList();
        if((a.LineDiscounts ?? []).Select(x=>x.LineId).Distinct().Count()!=(a.LineDiscounts?.Count ?? 0))throw new ArgumentException("Duplicate staff line adjustment.");
        foreach(var entry in a.LineDiscounts ?? [])
        {
            SalesRules.Validate(entry);if(!Enum.IsDefined(entry.Type) || entry.Type==DiscountType.FreeShipping || entry.Type==DiscountType.Percentage && entry.Value>100)throw new ArgumentException("Invalid staff line discount.");
            var index=lines.FindIndex(x=>x.Id==entry.LineId);if(index<0)throw new ArgumentException("Staff line adjustment refers to a missing line.");
            var line=lines[index];var amount=SalesRules.Discount(line.FinalTotalPrice,entry.Type,entry.Value);var total=SalesRules.Money(line.FinalTotalPrice-amount);
            var discounts=line.Discounts.Append(new SalesLineDiscountDto(SalesDiscountSource.Manual,"Staff line discount",null,amount,entry.Type,entry.Value)).Where(x=>x.DiscountAmount>0).ToArray();
            lines[index]=line with {FinalTotalPrice=total,FinalUnitPrice=SalesRules.Money(total/line.Quantity),LineDiscountAmount=line.LineDiscountAmount+amount,
                Discounts=discounts,DiscountSource=string.Join(", ",discounts.Select(x=>x.Label)),ProfitAmount=total-line.CostTotalUsed,ProfitMargin=total>0?SalesRules.Money((total-line.CostTotalUsed)/total*100):0};
        }
        var manual=SalesRules.Discount(lines.Sum(x=>x.FinalTotalPrice),a.ManualDiscountType ?? DiscountType.Fixed,a.ManualDiscountValue);
        var quote=prepared.Priced.Shipping;var coupons=prepared.Priced.Price.Coupons;
        if(a.NegotiatedShippingCost.HasValue)
        {
            if(quote is null)throw new ArgumentException("Negotiated shipping needs a server-validated shipping route.");
            quote=quote with {BaseCost=SalesRules.Money(a.NegotiatedShippingCost.Value),Discount=0,TotalCost=SalesRules.Money(a.NegotiatedShippingCost.Value),CouponCodes=[]};
            coupons=coupons.Where(x=>!x.AppliesToShipping).ToArray();
        }
        var price=prepared.Priced.Price with {Lines=lines.Select(SalesBasketPricing.ClientLine).ToArray(),Coupons=coupons,
            ManualDiscountAmount=manual,ShippingCost=quote?.BaseCost ?? 0,ShippingDiscount=quote?.Discount ?? 0,TaxAmount=SalesRules.Money(a.TaxAmount),
            DiscountAmount=SalesRules.Money(lines.Sum(x=>x.LineDiscountAmount)+manual+(quote?.Discount ?? 0)),
            TotalAmount=SalesRules.Money(lines.Sum(x=>x.FinalTotalPrice)-manual+(quote?.TotalCost ?? 0)+a.TaxAmount)};
        if(request.PaymentMethodId.HasValue)
        {
            var method=await db.Query<PaymentMethod>().FirstOrDefaultAsync(x=>x.Id==request.PaymentMethodId && x.IsActive,ct) ?? throw new ArgumentException("Payment method is unavailable.");
            ValidatePaymentLimits(SalesRules.Read<SavePaymentMethodRequest>(method.DefinitionJson),price.TotalAmount,quote);
        }
        return prepared with {Priced=prepared.Priced with {Price=price,AdminLines=lines,Shipping=quote},Adjustments=a};
    }
    public async Task<PricedSaleDto> PriceStaffAsync(SaveStaffOrderRequest request,Guid? actor,CancellationToken ct=default)
    {await catalog.RequireManager(actor,ct);SalesRules.Validate(request);return (await PrepareStaffAsync(request.Order,request.UserId,request.Pricing,ct)).Priced.Price;}
    public async Task<OrderReceiptDto> PlaceStaffAsync(SaveStaffOrderRequest request,Guid? actor,CancellationToken ct=default)
    {await catalog.RequireManager(actor,ct);SalesRules.Validate(request);return await PersistAsync(await PrepareStaffAsync(request.Order,request.UserId,request.Pricing,ct),request.UserId,null,ct,request.InvoiceNumber);}
    public async Task<ClientOrderDto?> UpdateStaffAsync(Guid id,SaveStaffOrderRequest request,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);SalesRules.Validate(request);var prepared=await PrepareStaffAsync(request.Order,request.UserId,request.Pricing,ct);
        return await db.InTransactionAsync(async token=>
        {
            var row=await db.Locked<SalesOrder>().FirstOrDefaultAsync(x=>x.Id==id,token);if(row is null)return null;
            if(row.PaymentStatus!=PaymentStatus.Unpaid || row.Status!=OrderStatus.Pending)throw new SalesConflictException("Only pending unpaid orders can be repriced.");
            // Lock the same counter used by creation so concurrent staff invoice assignments cannot
            // race with an automatic invoice allocation. The unique index is the final boundary.
            await db.Locked<SalesCounter>().Where(x=>x.Id==CounterId).FirstAsync(token);
            var invoice=request.InvoiceNumber ?? row.InvoiceNumber;
            if(await db.Query<SalesOrder>().AnyAsync(x=>x.Id!=id && x.InvoiceNumber==invoice,token))throw new SalesConflictException("Invoice number is unavailable.");
            var oldView=await WithRevision(row,token);
            var oldLines=SalesRules.Read<PricedBasket>(oldView.SnapshotJson).AdminLines.Concat((await Additions(id,token,row.RevisionNumber)).Select(x=>SalesRules.Read<AdminSalesLineDto>(x.PricedLineJson))).ToArray();
            await UpdateUnitsAsync(oldLines,prepared.Priced.AdminLines,token);
            row.CustomerFirstName=prepared.Request.CustomerFirstName;row.CustomerLastName=prepared.Request.CustomerLastName;row.CustomerEmail=prepared.Request.CustomerEmail;
            row.InvoiceNumber=invoice;row.InvoiceReference=$"{options.InvoicePrefix}-{invoice:0000}";
            if(row.UserId!=request.UserId) {row.GuestTokenHash=null;row.GuestTokenExpiresAt=null;}
            row.UserId=request.UserId;row.RevisionNumber=checked(row.RevisionNumber+1);row.TotalAmount=prepared.Priced.Price.TotalAmount;row.ItemCount=prepared.Priced.Price.ItemCount;row.Currency=prepared.Priced.Price.Currency;
            row.ManualDiscountType=prepared.Adjustments?.ManualDiscountType;row.ManualDiscountValue=prepared.Adjustments?.ManualDiscountValue ?? 0;
            db.Add(new SalesOrderRevision {OrderId=id,Number=row.RevisionNumber,ActingUserId=actor!.Value,PreparedJson=SalesRules.Write(prepared)});
            await db.SaveChangesAsync(token);return ToDto(await WithRevision(row,token),[],true);
        },cancellationToken:ct);
    }
    public async Task<NumberAvailabilityDto> InvoiceAvailabilityAsync(int number,Guid? excludeOrder,Guid? actor,CancellationToken ct=default)
    {await catalog.RequireManager(actor,ct);if(number<=0)throw new ArgumentException("Invoice number must be positive.");return new(!await db.Query<SalesOrder>().AnyAsync(x=>x.InvoiceNumber==number && x.Id!=excludeOrder,ct));}    private async Task UpdateUnitsAsync(IReadOnlyList<AdminSalesLineDto> before,IReadOnlyList<AdminSalesLineDto> after,CancellationToken ct)
    {
        var prior=before.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        var next=after.GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
        foreach(var productId in prior.Keys.Concat(next.Keys).Distinct().Order())
        {var delta=next.GetValueOrDefault(productId)-prior.GetValueOrDefault(productId);if(delta!=0)await db.Query<Product>().Where(x=>x.Id==productId).UpdateAsync(s=>s.Set(x=>x.UnitsSold,x=>x.UnitsSold+delta).Set(x=>x.UpdatedAt,clock.GetUtcNow()),ct);}
    }

}
