using System.ComponentModel.DataAnnotations;

using CompanyHQ.Services.Shipping.Contracts.Dtos;

namespace CompanyHQ.Services.Shipping.Contracts.Forms;

/// <summary>A basket and where it is going. Nothing in it is a price: every amount is the server's.</summary>
public sealed record ShippingQuoteRequest(
    [Required] ShippingAddressDto Address,
    [Required, MinLength(1), MaxLength(ShippingQuoteRequest.MaxLines)] IReadOnlyList<ShippingQuoteLineRequest> Items,
    [MaxLength(20)] IReadOnlyList<string>? CouponCodes = null)
{
    public const int MaxLines = 200;
}

public sealed record ShippingQuoteLineRequest(
    Guid ProductId,
    Guid? VariantId = null,
    [Range(1, 100_000)] int Quantity = 1,
    Guid? Id = null,
    IReadOnlyDictionary<string, string>? Options = null,
    Guid? BundleInstanceId = null,
    Guid? UpsellParentLineId = null);
