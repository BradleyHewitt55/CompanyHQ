using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Ports;

namespace CompanyHQ.Services.Sales.Services;

/// <summary>Authorizes customer-safe delivery; provider calls run outside database transactions.</summary>
public sealed class SalesDocumentService(SalesCatalogService catalog, SalesOrderService orders,
    SalesQuoteService quotes, ISalesDocumentDelivery? delivery = null)
{
    public async Task<SalesDocumentDeliveryResult?> SendInvoiceAsync(Guid id, SendSalesDocumentRequest request,
        Guid? actor, CancellationToken ct = default)
    {
        await catalog.RequireManager(actor, ct);
        Validate(request);
        if (delivery is null) throw new SalesCapabilityUnavailableException("Invoice delivery is not configured.");
        var document = await orders.ReserveInvoiceDeliveryAsync(id, request.RequestId, actor, ct);
        if (document is null) return null;
        var result = await delivery.DeliverAsync(document, ct);
        await orders.RecordInvoiceDeliveryAsync(id, request.RequestId, result, ct);
        return result;
    }

    public async Task<SalesDocumentDeliveryResult?> SendQuoteAsync(Guid id, SendSalesDocumentRequest request,
        Guid? actor, CancellationToken ct = default)
    {
        await catalog.RequireManager(actor, ct);
        Validate(request);
        if (delivery is null) throw new SalesCapabilityUnavailableException("Quote delivery is not configured.");
        var reservation = await quotes.ReserveDeliveryAsync(id, request.RequestId, actor, ct);
        if (reservation is null) return null;
        var (quote, requestId) = reservation.Value;
        var result = await delivery.DeliverAsync(new($"sales:quote:{id}:{requestId}", "Quotation",
            quote.QuoteReference, quote.CustomerEmail!, quote.Currency, PublicLines(quote.Lines),
            quote.Subtotal, quote.DiscountAmount, quote.ShippingCost, quote.TaxAmount, quote.TotalAmount,
            "Awaiting customer acceptance"), ct);
        if (result.Accepted) await quotes.CompleteDeliveryAsync(id, requestId, ct);
        return result;
    }

    private static IReadOnlyList<ClientSalesLineDto> PublicLines(IReadOnlyList<ClientSalesLineDto> lines) =>
        lines.Select(x => x is AdminSalesLineDto admin ? SalesBasketPricing.ClientLine(admin) : x).ToArray();

    private static void Validate(SendSalesDocumentRequest request)
    {
        if (request.RequestId == Guid.Empty) throw new ArgumentException("A stable delivery request id is required.");
    }
}

public sealed class SalesCapabilityUnavailableException(string message) : Exception(message);
