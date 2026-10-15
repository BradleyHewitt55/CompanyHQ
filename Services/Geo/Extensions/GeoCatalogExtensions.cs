using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Geo.Extensions;

public static class GeoCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddGeoServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = "openstreetmap", Func<IServiceProvider, bool>? isConfigured = null)
    {
        var providerType = services.LastOrDefault(item => item.ServiceType == typeof(CompanyHQ.Geo.Contracts.Ports.IGeoProvider))?.ImplementationType;
        return
        services.AddServiceDescriptor(new(new(
            "geo", "Geography", "Address and area search.",
            "Looks up areas and addresses so customers can enter a location and delivery distances can be measured. Uses OpenStreetMap, which needs a contact address in its settings before it can be used.",
            Core: false, Dependencies: [],
            Providers: [
                new("openstreetmap", "OpenStreetMap", "Nominatim and Overpass geographic lookups.", [
                    new("Geo:UserAgent", "Deployment identity with a reachable contact.", Secret: false, Required: true, ReadOnly: false),
                    new("Geo:BaseAddress", "Nominatim endpoint.", Secret: false, Required: false, ReadOnly: false),
                    new("Geo:OverpassBaseAddress", "Overpass endpoint.", Secret: false, Required: false, ReadOnly: false)
                ])
            ],
            DefaultProvider: "openstreetmap", MenuPath: null, Capabilities: null,
            ConfigurationKeys: [
                new("Geo:AreaZoom", "Area Zoom.", Required: false),
                new("Geo:Language", "Language.", Required: false),
                new("Geo:CacheTtlHours", "Cache TTL Hours.", Required: false),
                new("Geo:MinimumRequestInterval", "Minimum Request Interval.", Required: false),
                new("Geo:OverpassMinimumRequestInterval", "Overpass Minimum Request Interval.", Required: false),
                new("Geo:NearbyPlaceCacheTtlHours", "Nearby Place Cache TTL Hours.", Required: false),
                new("Geo:NearbyTileHardTtlHours", "Nearby Tile Hard TTL Hours.", Required: false),
                new("Geo:NearbyPlaceKinds", "Nearby Place Kinds.", Required: false),
                new("Geo:InboundRateLimitPermitLimit", "Inbound Rate Limit Permit Limit.", Required: false),
                new("Geo:InboundRateLimitWindowSeconds", "Inbound Rate Limit Window Seconds.", Required: false)
            ]),
            typeof(GeoCatalogExtensions).Assembly, loadedProvider,
            isConfigured ?? (_ => providerType is { } geo
                && geo != typeof(CompanyHQ.Geo.Services.UnconfiguredGeoProvider))));
    }
}
