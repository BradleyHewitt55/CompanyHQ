using CompanyHQ.Sales.Contracts.Enums;

namespace CompanyHQ.Sales.Contracts.Dtos;

/// <summary>One row of the staff quote list. Quotes are staff documents, so there is no customer shape.</summary>
public sealed record AdminQuoteSummaryDto(
    Guid Id,
    int QuoteNumber,
    DateTimeOffset QuoteDate,
    DateTimeOffset ValidUntil,
    QuoteStatus Status,
    string? CustomerFirstName,
    string? CustomerLastName,
    string? CustomerEmail,
    string Currency,
    decimal TotalAmount,
    int ItemCount);

public sealed record AdminQuoteDto(
    Guid Id,
    int QuoteNumber,
    /// <summary>The quote number as it is printed, such as <c>QUO-0007</c>.</summary>
    string QuoteReference,
    DateTimeOffset QuoteDate,
    DateTimeOffset ValidUntil,
    QuoteStatus Status,
    Guid? UserId,
    string? CustomerFirstName,
    string? CustomerLastName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CompanyName,
    string? VatNumber,
    SalesAddressDto? ShippingAddress,
    bool BillingSameAsShipping,
    SalesAddressDto? BillingAddress,
    Guid? ShippingMethodId,
    string? ShippingMethodName,
    string? ShippingMethodKind,
    int? EstimatedDeliveryDays,
    Guid? PaymentMethodId,
    string? PaymentMethodName,
    string Currency,
    IReadOnlyList<ClientSalesLineDto> Lines,
    IReadOnlyList<SalesAppliedCouponDto> Coupons,
    int ItemCount,
    decimal Subtotal,
    decimal DiscountAmount,
    DiscountType? ManualDiscountType,
    decimal ManualDiscountValue,
    decimal ShippingCost,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal ProfitAmount,
    string? Notes,
    string? InternalNotes,
    DateTimeOffset? AcceptedAt,
    Guid? ConvertedToOrderId,
    int? ConvertedOrderNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record QuoteNumberDto(int QuoteNumber);
