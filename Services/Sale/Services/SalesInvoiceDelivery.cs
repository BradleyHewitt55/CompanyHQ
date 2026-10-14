using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Sales.Contracts.Models;
using CompanyHQ.Services.Sales.Contracts.Ports;

namespace CompanyHQ.Services.Sales.Services;

public sealed partial class SalesOrderService
{
    internal async Task<SalesDocument?> ReserveInvoiceDeliveryAsync(Guid id,Guid requestId,Guid? actor,CancellationToken ct)
    {
        await catalog.RequireManager(actor,ct);
        return await db.InTransactionAsync<SalesDocument?>(async token=>
        {
            // Share the order mutation lock: the snapshot contains exactly one revision and its
            // current additions, reference and recipient. Commit before any provider work.
            var order=await db.Locked<SalesOrder>().FirstOrDefaultAsync(x=>x.Id==id,token);
            if(order is null)return null;
            var existing=await db.Query<InvoiceDeliveryRequest>().FirstOrDefaultAsync(x=>x.OrderId==id && x.RequestId==requestId,token);
            if(existing is not null)return SalesRules.Read<SalesDocument>(existing.DocumentJson);
            var dto=ToDto(await WithRevision(order,token),await Additions(id,token,order.RevisionNumber),false);
            var document=new SalesDocument($"sales:invoice:{id}:{requestId}","Invoice",dto.InvoiceReference,
                dto.CustomerEmail!,dto.Currency,dto.Lines,dto.Subtotal,dto.DiscountAmount,dto.ShippingCost,
                dto.TaxAmount,dto.TotalAmount,dto.PaymentStatus.ToString());
            db.Add(new InvoiceDeliveryRequest {OrderId=id,RequestId=requestId,RevisionNumber=order.RevisionNumber,DocumentJson=SalesRules.Write(document)});
            await db.SaveChangesAsync(token);
            return document;
        },cancellationToken:ct);
    }

    internal async Task RecordInvoiceDeliveryAsync(Guid id,Guid requestId,SalesDocumentDeliveryResult result,CancellationToken ct)
    {
        await db.InTransactionAsync(async token=>
        {
            var row=await db.Locked<InvoiceDeliveryRequest>().Where(x=>x.OrderId==id && x.RequestId==requestId).FirstAsync(token);
            if(result.Queued)row.QueuedAt ??=clock.GetUtcNow();
            if(result.Accepted)row.AcceptedAt ??=clock.GetUtcNow();
            await db.SaveChangesAsync(token);
        },cancellationToken:ct);
    }
}
