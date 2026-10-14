namespace CompanyHQ.Geo.Contracts.Ports;

/// <summary>
/// The geographic data source behind Geo: names an area, searches areas by text, and finds places
/// inside a box. Geo decides what is asked, how it is rounded, cached and limited; a provider
/// adapter owns how the answer is fetched.
/// </summary>
/// <remarks>
/// Every coordinate arriving here has already been rounded by Geo, so an adapter can never see a
/// precise position. An adapter reports a failure through the return value and never throws for
/// one: Geo must be able to tell "nothing there" from "could not find out". It never retries a
/// failed request and never logs a coordinate or a search text.
/// </remarks>
public interface IGeoProvider
{
    /// <summary>The area at a coordinate, or <c>null</c> when the provider has no answer or could not be reached.</summary>
    Task<GeoProviderArea?> FindAreaAsync(GeoAreaLookup lookup, CancellationToken cancellationToken);

    /// <summary>Areas matching free text, best first. Empty when nothing matched or the lookup failed.</summary>
    Task<IReadOnlyList<GeoProviderArea>> SearchAreasAsync(GeoAreaSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// Every place of the requested kinds inside the box. An empty list is a real answer — the box
    /// was searched and holds none — while <c>null</c> means the provider refused or could not be
    /// reached. The two are never interchangeable: Geo caches the first and must not cache the second.
    /// </summary>
    Task<IReadOnlyList<GeoProviderPlace>?> FindPlacesAsync(GeoPlaceSearch search, CancellationToken cancellationToken);
}
