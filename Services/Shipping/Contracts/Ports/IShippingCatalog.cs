namespace CompanyHQ.Services.Shipping.Contracts.Ports;

/// <summary>
/// What Shipping may ask about the products in a basket. Optional: without it every product id is
/// taken as given and ships only from the origins linked to a method.
/// </summary>
public interface IShippingCatalog
{
    /// <summary>The products that exist. One missing from the answer fails the quote as not found.</summary>
    Task<IReadOnlyList<ShippingCatalogProduct>> GetProductsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default);
}

/// <param name="Origins">Places this product ships from on its own account, such as its suppliers.</param>
public sealed record ShippingCatalogProduct(Guid ProductId, string Name, IReadOnlyList<ShippingCatalogOrigin> Origins);

public sealed record ShippingCatalogOrigin(Guid OriginId, string Name, double? Latitude, double? Longitude);
