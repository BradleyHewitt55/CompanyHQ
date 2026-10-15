using CompanyHQ.Geo.Contracts.Ports;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Geo.Services;

internal sealed class UnconfiguredGeoProvider : IGeoProvider
{
    private static ServiceUnavailableException Unavailable() => new("geo", ServiceStatus.EnabledNotConfigured);
    public Task<GeoProviderArea?> FindAreaAsync(GeoAreaLookup lookup, CancellationToken cancellationToken) => throw Unavailable();
    public Task<IReadOnlyList<GeoProviderArea>> SearchAreasAsync(GeoAreaSearch search, CancellationToken cancellationToken) => throw Unavailable();
    public Task<IReadOnlyList<GeoProviderPlace>?> FindPlacesAsync(GeoPlaceSearch search, CancellationToken cancellationToken) => throw Unavailable();
}
