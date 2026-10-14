using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Enums;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Models;
namespace CompanyHQ.Services.Sales.Services;
/// <summary>Staff quotation lifecycle and atomic conversion of the immutable accepted snapshot.</summary>
public sealed class SalesQuoteService(IPersistence db,SalesCatalogService catalog,SalesOrderService orders,TimeProvider clock,SalesOptions options) : ISalesQuoteService
{
    private static readonly Guid CounterId=Guid.Parse("7147ed7e-2a68-4c22-8a2c-c68bad4a370c");
    public async Task<PagedResult<AdminQuoteSummaryDto>> ListAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);var result=await db.Query<SalesQuote>().Search(search.Field,search.Keyword,typeof(AdminQuoteSummaryDto)).OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);
        return new(result.Items.Select(x=>new AdminQuoteSummaryDto(x.Id,x.Number,x.CreatedAt,x.ValidUntil,EffectiveStatus(x),x.CustomerFirstName,x.CustomerLastName,x.CustomerEmail,x.Currency,x.TotalAmount,x.ItemCount)).ToArray(),page.Page,page.PageSize,result.Total);
    }
    private QuoteStatus EffectiveStatus(SalesQuote q) =>
        q.Status is QuoteStatus.Draft or QuoteStatus.Sent or QuoteStatus.Issuing && q.ValidUntil < clock.GetUtcNow()
            ? QuoteStatus.Expired : q.Status;

    internal async Task<(AdminQuoteDto Quote, Guid RequestId)?> ReserveDeliveryAsync(
        Guid id, Guid requestId, Guid? actor, CancellationToken ct)
    {
        await catalog.RequireManager(actor, ct);
        return await db.InTransactionAsync<(AdminQuoteDto, Guid)?>(async token =>
        {
            var quote = await db.Locked<SalesQuote>().FirstOrDefaultAsync(x => x.Id == id, token);
            if (quote is null) return null;
            if (EffectiveStatus(quote) is not (QuoteStatus.Draft or QuoteStatus.Issuing or QuoteStatus.Sent or QuoteStatus.Accepted))
                throw new SalesConflictException("Expired or converted quotes cannot be issued.");

            // Commit the immutable revision before provider work. Failures retain the reservation:
            // retries always deliver the same content with the same provider idempotency key.
            quote.DeliveryRequestId ??= requestId;
            if (quote.Status == QuoteStatus.Draft) quote.Status = QuoteStatus.Issuing;
            await db.SaveChangesAsync(token);
            return (ToDto(quote), quote.DeliveryRequestId.Value);
        }, cancellationToken: ct);
    }

    internal async Task CompleteDeliveryAsync(Guid id, Guid requestId, CancellationToken ct)
    {
        await db.InTransactionAsync(async token =>
        {
            var quote = await db.Locked<SalesQuote>().Where(x => x.Id == id).FirstAsync(token);
            if (quote.DeliveryRequestId != requestId)
                throw new SalesConflictException("Issued quote reservation changed.");
            if (quote.Status == QuoteStatus.Issuing) quote.Status = QuoteStatus.Sent;
            await db.SaveChangesAsync(token);
        }, cancellationToken: ct);
    }
    public async Task<AdminQuoteDto?> GetAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {await catalog.RequireManager(actor,ct);var q=await db.Query<SalesQuote>().FirstOrDefaultAsync(x=>x.Id==id,ct);return q is null?null:ToDto(q);}
    public async Task<AdminQuoteDto> SaveAsync(Guid? id,SaveQuoteRequest request,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);SalesRules.Validate(request);
        if(request.ValidUntil<=clock.GetUtcNow() || request.ValidUntil>clock.GetUtcNow().AddDays(365))throw new ArgumentException("Quote validity must be within the next year.");
        var prepared=await orders.PrepareStaffAsync(request.Order,request.UserId,request.Pricing,ct);
        for(var attempt=0;;attempt++)
        {
            try{return await db.InTransactionAsync(async token=>
            {
                var q=id.HasValue?await db.Locked<SalesQuote>().FirstOrDefaultAsync(x=>x.Id==id.Value,token):new SalesQuote();
                if(q is null)throw new KeyNotFoundException();if(id.HasValue && q.Status!=QuoteStatus.Draft)throw new SalesConflictException("Only draft quotes can be replaced.");
                if(!id.HasValue)
                {var counter=await db.Locked<SalesCounter>().FirstOrDefaultAsync(x=>x.Id==CounterId,token);if(counter is null){counter=new(){Id=CounterId};db.Add(counter);await db.SaveChangesAsync(token);}q.Number=checked(++counter.Number);q.QuoteReference=$"{options.QuotePrefix}-{q.Number:0000}";db.Add(q);}
                q.UserId=request.UserId;q.ValidUntil=request.ValidUntil.ToUniversalTime();q.PreparedJson=SalesRules.Write(prepared);q.CustomerFirstName=prepared.Request.CustomerFirstName;q.CustomerLastName=prepared.Request.CustomerLastName;q.CustomerEmail=prepared.Request.CustomerEmail;q.Currency=prepared.Priced.Price.Currency;q.TotalAmount=prepared.Priced.Price.TotalAmount;q.ItemCount=prepared.Priced.Price.ItemCount;
                await db.SaveChangesAsync(token);return ToDto(q);
            },cancellationToken:ct);}catch(UniqueConstraintException)when(attempt<2){db.Clear();}
        }
    }
    public async Task<bool> DeleteAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);return await db.InTransactionAsync(async token=>
        {var q=await db.Locked<SalesQuote>().FirstOrDefaultAsync(x=>x.Id==id,token);if(q is null)return false;if(q.Status!=QuoteStatus.Draft)throw new SalesConflictException("Issued quotes are retained.");db.Remove(q);await db.SaveChangesAsync(token);return true;},cancellationToken:ct);
    }
    public async Task<AdminQuoteDto?> ChangeStatusAsync(Guid id,ChangeQuoteStatusRequest request,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);return await db.InTransactionAsync(async token=>
        {
            var q=await db.Locked<SalesQuote>().FirstOrDefaultAsync(x=>x.Id==id,token);if(q is null)return null;
            var status=EffectiveStatus(q);if((status,request.Status) is not ((QuoteStatus.Draft,QuoteStatus.Sent) or (QuoteStatus.Sent,QuoteStatus.Accepted)) && status!=request.Status)throw new SalesConflictException("Invalid or expired quote transition.");
            q.Status=request.Status;if(q.Status==QuoteStatus.Accepted)q.AcceptedAt ??=clock.GetUtcNow();await db.SaveChangesAsync(token);return ToDto(q);
        },cancellationToken:ct);
    }
    public async Task<OrderReceiptDto?> ConvertAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await catalog.RequireManager(actor,ct);var quote=await db.Query<SalesQuote>().FirstOrDefaultAsync(x=>x.Id==id,ct);if(quote is null)return null;
        if(quote.Status!=QuoteStatus.Accepted || quote.ValidUntil<clock.GetUtcNow())throw new SalesConflictException("Only valid accepted quotes can convert once.");
        var prepared=SalesRules.Read<PreparedOrder>(quote.PreparedJson);
        return await db.InTransactionAsync(async token=>
        {
            var locked=await db.Locked<SalesQuote>().Where(x=>x.Id==id).FirstAsync(token);
            if(locked.Status!=QuoteStatus.Accepted || locked.ConvertedToOrderId.HasValue || locked.ValidUntil<clock.GetUtcNow() || locked.UpdatedAt!=quote.UpdatedAt)throw new SalesConflictException("Quote changed or was already converted.");
            var receipt=await orders.PersistAsync(prepared,locked.UserId,id,token);
            locked.Status=QuoteStatus.Converted;locked.ConvertedToOrderId=receipt.OrderId;locked.ConvertedOrderNumber=receipt.OrderNumber;await db.SaveChangesAsync(token);return receipt;
        },cancellationToken:ct);
    }
    private AdminQuoteDto ToDto(SalesQuote q)
    {
        var p=SalesRules.Read<PreparedOrder>(q.PreparedJson);var r=p.Request;var s=p.Priced.Price;var shipping=p.Priced.Shipping;
        return new(q.Id,q.Number,q.QuoteReference,q.CreatedAt,q.ValidUntil,EffectiveStatus(q),q.UserId,r.CustomerFirstName,r.CustomerLastName,r.CustomerEmail,r.CustomerPhone,r.CompanyName,r.VatNumber,r.Sale.ShippingAddress,r.BillingSameAsShipping,r.BillingSameAsShipping?r.Sale.ShippingAddress:r.BillingAddress,shipping?.MethodId,shipping?.MethodName,shipping?.MethodKind,shipping?.EstimatedDeliveryDays,r.PaymentMethodId,p.PaymentMethodName,s.Currency,p.Priced.AdminLines,s.Coupons,s.ItemCount,s.Subtotal,s.DiscountAmount,p.Adjustments?.ManualDiscountType,p.Adjustments?.ManualDiscountValue ?? 0,s.ShippingCost,s.TaxAmount,s.TotalAmount,p.Priced.AdminLines.Sum(x=>x.ProfitAmount)-s.ManualDiscountAmount,r.Notes,null,q.AcceptedAt,q.ConvertedToOrderId,q.ConvertedOrderNumber,q.CreatedAt,q.UpdatedAt);
    }
}

