using System.Text.Json.Serialization;

using CompanyHQ.Sales.Contracts.Enums;

namespace CompanyHQ.Sales.Contracts.Dtos;

/// <summary>One row of an order list, for the customer whose order it is.</summary>
[JsonDerivedType(typeof(AdminOrderSummaryDto))]
public record ClientOrderSummaryDto(
    Guid Id,
    int InvoiceNumber,
    DateTimeOffset OrderDate,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string Currency,
    decimal TotalAmount,
    int ItemCount);

public sealed record AdminOrderSummaryDto(
    Guid Id,
    int InvoiceNumber,
    DateTimeOffset OrderDate,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string Currency,
    decimal TotalAmount,
    int ItemCount,
    int OrderNumber,
    Guid? UserId,
    string? CustomerFirstName,
    string? CustomerLastName,
    string? CustomerEmail)
    : ClientOrderSummaryDto(Id, InvoiceNumber, OrderDate, Status, PaymentStatus, Currency, TotalAmount, ItemCount);

/// <summary>An order as its customer sees it: the priced snapshot taken when it was placed.</summary>
[JsonDerivedType(typeof(AdminOrderDto))]
public record ClientOrderDto(
    Guid Id,
    int OrderNumber,
    int InvoiceNumber,
    /// <summary>The invoice number as it is printed, such as <c>INV-0042</c>.</summary>
    string InvoiceReference,
    DateTimeOffset OrderDate,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string? CustomerFirstName,
    string? CustomerLastName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CompanyName,
    string? VatNumber,
    SalesAddressDto? ShippingAddress,
    bool BillingSameAsShipping,
    SalesAddressDto? BillingAddress,
    string? ShippingMethodName,
    int? EstimatedDeliveryDays,
    string? PaymentMethodName,
    string Currency,
    IReadOnlyList<ClientSalesLineDto> Lines,
    IReadOnlyList<SalesAppliedCouponDto> Coupons,
    int ItemCount,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingCost,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes,
    DateTimeOffset? ShippedDate,
    DateTimeOffset? DeliveredDate);

public sealed record AdminOrderDto(
    Guid Id,
    int OrderNumber,
    int InvoiceNumber,
    string InvoiceReference,
    DateTimeOffset OrderDate,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    string? CustomerFirstName,
    string? CustomerLastName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? CompanyName,
    string? VatNumber,
    SalesAddressDto? ShippingAddress,
    bool BillingSameAsShipping,
    SalesAddressDto? BillingAddress,
    string? ShippingMethodName,
    int? EstimatedDeliveryDays,
    string? PaymentMethodName,
    string Currency,
    IReadOnlyList<ClientSalesLineDto> Lines,
    IReadOnlyList<SalesAppliedCouponDto> Coupons,
    int ItemCount,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingCost,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes,
    DateTimeOffset? ShippedDate,
    DateTimeOffset? DeliveredDate,
    Guid? UserId,
    Guid? ShippingMethodId,
    string? ShippingMethodKind,
    Guid? PaymentMethodId,
    string? PaymentTransactionId,
    DiscountType? ManualDiscountType,
    decimal ManualDiscountValue,
    decimal ProfitAmount,
    Guid? SourceQuoteId,
    DateTimeOffset? FinalizedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
    : ClientOrderDto(Id, OrderNumber, InvoiceNumber, InvoiceReference, OrderDate, Status, PaymentStatus, CustomerFirstName,
        CustomerLastName, CustomerEmail, CustomerPhone, CompanyName, VatNumber, ShippingAddress, BillingSameAsShipping,
        BillingAddress, ShippingMethodName, EstimatedDeliveryDays, PaymentMethodName, Currency, Lines, Coupons, ItemCount,
        Subtotal, DiscountAmount, ShippingCost, TaxAmount, TotalAmount, Notes, ShippedDate, DeliveredDate);

/// <summary>
/// What placing an order returns. Self-service: the same shape for every caller.
/// </summary>
/// <param name="GuestAccessToken">
/// Only for an order placed without signing in, and only here: the one credential that reads the
/// order, takes its upsell and finalizes it. Send it back in the <c>X-Order-Access-Token</c> header.
/// </param>
public sealed record OrderReceiptDto(
    Guid OrderId,
    int OrderNumber,
    int InvoiceNumber,
    string InvoiceReference,
    string Currency,
    decimal TotalAmount,
    string? GuestAccessToken,
    DateTimeOffset? GuestAccessTokenExpiresAt);

/// <summary>The numbers the next order would take.</summary>
public sealed record OrderNumbersDto(int OrderNumber, int InvoiceNumber);

public sealed record NumberAvailabilityDto(bool IsAvailable);
