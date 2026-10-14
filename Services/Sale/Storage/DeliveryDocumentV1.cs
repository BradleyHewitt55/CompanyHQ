namespace CompanyHQ.Services.Sales.Storage;

// Frozen schema for the new delivery.document kind, version 1. Never persist HTTP records directly.
internal sealed record DeliveryDocumentV1(string IdempotencyKey,string Kind,string Reference,string Recipient,
    string Currency,IReadOnlyList<CustomerLineV1> Lines,decimal Subtotal,decimal DiscountAmount,
    decimal ShippingCost,decimal TaxAmount,decimal TotalAmount,string Status);
