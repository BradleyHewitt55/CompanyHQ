using CompanyHQ.Services.Shipping.Contracts.Forms;

namespace CompanyHQ.Services.Shipping.Contracts.Ports;

/// <summary>
/// Whoever owns promotions decides what comes off a shipping cost. Optional: without it a quote is
/// the plain cost of its legs.
/// </summary>
public interface IShippingDiscountPolicy
{
    Task<ShippingDiscountResult> EvaluateAsync(ShippingDiscountContext context, CancellationToken cancellationToken = default);
}

/// <param name="Lines">The basket, so the policy prices it itself rather than trusting a caller's subtotal.</param>
/// <param name="Origins">Where each chosen leg leaves from. An entry is null when that origin has no coordinates.</param>
public sealed record ShippingDiscountContext(
    decimal BaseShippingCost,
    IReadOnlyList<ShippingQuoteLineRequest> Lines,
    IReadOnlyList<string> CouponCodes,
    double? DestinationLatitude,
    double? DestinationLongitude,
    IReadOnlyList<ShippingCoordinates?> Origins);

/// <param name="DiscountAmount">Clamped by Shipping to between nothing and the whole cost.</param>
/// <param name="AppliedCouponCodes">Only the codes that produced the discount.</param>
public sealed record ShippingDiscountResult(decimal DiscountAmount, IReadOnlyList<string> AppliedCouponCodes)
{
    public static ShippingDiscountResult None { get; } = new(0m, []);
}
