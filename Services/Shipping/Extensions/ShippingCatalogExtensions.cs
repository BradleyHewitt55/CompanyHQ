using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Shipping.Extensions;

public static class ShippingCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddShippingServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null)
    {
        var availablePorts = new Dictionary<string, bool>
        {
            ["catalog"] = services.Any(item => item.ServiceType == typeof(CompanyHQ.Shipping.Contracts.Ports.IShippingCatalog)),
            ["discounts"] = services.Any(item => item.ServiceType == typeof(CompanyHQ.Shipping.Contracts.Ports.IShippingDiscountPolicy)),
            ["addresses"] = services.Any(item => item.ServiceType == typeof(CompanyHQ.Shipping.Contracts.Ports.IShippingAddressDirectory)),
        };
        return
        services.AddServiceDescriptor(new(new(
            "shipping", "Shipping", "Delivery methods and shipping quotes.",
            "Set up delivery methods, postal zones, distance limits and fulfilment origins, and quote the cheapest way to deliver an order. Uses Sales for product origins. It does not book couriers or track parcels.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/shipping", Capabilities: [new("catalog", "Product and fulfillment facts through Sales.", ["sales"]), new("discounts", "Shipping promotion evaluation through Sales.", ["sales"]), new("addresses", "Area search and geocoding through Geo.", ["geo"])], ReadPermission: "shipping.manage"),
            typeof(ShippingCatalogExtensions).Assembly, loadedProvider,
            isConfigured, (_, capability, _) => Task.FromResult(availablePorts.GetValueOrDefault(capability))));
    }
}
