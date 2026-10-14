namespace CompanyHQ.Services.Shipping.Contracts.Dtos;

/// <summary>A place goods leave from. Staff only: it is a warehouse or supplier address.</summary>
public sealed record AdminFulfillmentOriginDto(
    Guid Id,
    string Name,
    bool IsActive,
    /// <summary>The supplier or warehouse record in the company's own system, when there is one.</summary>
    Guid? SupplierId,
    ShippingAddressDto Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
