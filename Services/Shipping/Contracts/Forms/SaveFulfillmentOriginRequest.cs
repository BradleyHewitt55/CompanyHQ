using System.ComponentModel.DataAnnotations;

using CompanyHQ.Shipping.Contracts.Dtos;

namespace CompanyHQ.Shipping.Contracts.Forms;

/// <summary>Creates or replaces a place goods leave from.</summary>
public sealed record SaveFulfillmentOriginRequest(
    [Required, MaxLength(255), RegularExpression(@".*\S.*")] string Name,
    [Required] ShippingAddressDto Address,
    bool IsActive = true,
    Guid? SupplierId = null);
