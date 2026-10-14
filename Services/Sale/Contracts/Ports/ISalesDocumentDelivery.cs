using CompanyHQ.Sales.Contracts.Dtos;
namespace CompanyHQ.Sales.Contracts.Ports;
/// <summary>Company-controlled rendering/delivery. The adapter must honor the stable idempotency key and return the actual provider outcome.</summary>
public interface ISalesDocumentDelivery
{
    Task<SalesDocumentDeliveryResult> DeliverAsync(SalesDocument document,CancellationToken cancellationToken=default);
}
/// <summary>Customer-safe invoice/quotation snapshot; no cost or payment token is exposed.</summary>
public sealed record SalesDocument(string IdempotencyKey,string Kind,string Reference,string Recipient,string Currency,
    IReadOnlyList<ClientSalesLineDto> Lines,decimal Subtotal,decimal DiscountAmount,decimal ShippingCost,decimal TaxAmount,decimal TotalAmount,string Status);
/// <summary>Accepted means actual provider acceptance, never just enqueueing. Queued means awaiting the provider; retry the same request to refresh its outcome. Neither proves inbox delivery or payment.</summary>
public sealed record SalesDocumentDeliveryResult(bool Accepted,string? ProviderReference,string? Error,bool Queued=false);
