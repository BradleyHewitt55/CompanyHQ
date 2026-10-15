using CompanyHQ.ServiceCatalog.Contracts;
using System.Globalization;

using CompanyHQ.Cache.Services;
using CompanyHQ.Geo.Contracts.Dtos;
using CompanyHQ.Geo.Contracts.Enums;
using CompanyHQ.Geo.Contracts.Models;
using CompanyHQ.Geo.Contracts.Ports;

namespace CompanyHQ.Geo.Services;

/// <summary>
/// Rounding, caching, and mapping around the composed <see cref="IGeoProvider"/>.
/// </summary>
public sealed class GeoService(IServiceAvailability? availability,
    IGeoProvider provider,
    NearbyTileStore tiles,
    NearbyTileRefresher refresher,
    ICacheService cache,
    GeoOptions options) : IGeoService
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public GeoService(
    IGeoProvider provider,
    NearbyTileStore tiles,
    NearbyTileRefresher refresher,
    ICacheService cache,
    GeoOptions options) : this(null, provider, tiles, refresher, cache, options) { }

    /// <summary>
    /// Three decimals is roughly 110 m. Coarse enough that a coordinate no longer identifies a
    /// home, fine enough to name the right suburb — and it is applied here as well as on the
    /// client, because a caller's rounding is not something the server can verify.
    /// </summary>
    public const int CoordinateDecimals = 3;

    public async Task<AreaDto?> DescribeAreaAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("geo", true, cancellationToken) ?? Task.CompletedTask);
        var roundedLatitude = Round(latitude);
        var roundedLongitude = Round(longitude);

        // Keyed on the rounded pair, so the entry is shared by everyone in the area and holds no
        // position precise enough to identify one of them. v2 because AreaPrecision now travels by
        // name; a v1 entry wrote it as an integer and no longer parses.
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"geo:area:v2:{options.AreaZoom}:{options.Language}:{roundedLatitude}:{roundedLongitude}");

        if (await cache.GetAsync<AreaDto>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        var place = await provider.FindAreaAsync(
            new GeoAreaLookup(roundedLatitude, roundedLongitude, options.AreaZoom, options.Language),
            cancellationToken);
        if (place is null)
        {
            return null;
        }

        var area = Map(place, roundedLatitude, roundedLongitude);

        // Only successes are written. Caching a provider outage for a day would keep the app
        // area-less long after the outage ended.
        await cache.SetAsync(key, area, options.CacheTtl, cancellationToken);

        return area;
    }

    public async Task<IReadOnlyList<AreaDto>> SearchAreasAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("geo", true, cancellationToken) ?? Task.CompletedTask);
        var normalised = query?.Trim() ?? string.Empty;
        if (normalised.Length == 0)
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 20);

        var key = $"geo:search:v2:{options.Language}:{limit}:{normalised.ToLowerInvariant()}";

        if (await cache.GetAsync<IReadOnlyList<AreaDto>>(key, cancellationToken) is { Count: > 0 } cached)
        {
            return cached;
        }

        var places = await provider.SearchAreasAsync(
            new GeoAreaSearch(normalised, limit, options.Language),
            cancellationToken);
        if (places.Count == 0)
        {
            return [];
        }

        // A place with no usable coordinates is dropped rather than defaulted: a zero pair is a real
        // position off the coast of Africa, and a picker that silently offers it is worse than one
        // result short.
        var areas = places
            .Where(place => place is { Latitude: not null, Longitude: not null })
            .Select(place => Map(place, Round(place.Latitude!.Value), Round(place.Longitude!.Value)))
            .ToArray();

        if (areas.Length == 0)
        {
            return [];
        }

        await cache.SetAsync(key, areas, options.CacheTtl, cancellationToken);

        return areas;
    }

    public async Task<NearbyPlacesResultDto> ListNearbyPlacesAsync(
        GeoViewport viewport,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("geo", true, cancellationToken) ?? Task.CompletedTask);
        // No configured kinds means the deployment has not switched the search on.
        if (options.NearbyPlaceKinds.Length == 0)
        {
            return new NearbyPlacesResultDto([], [], false);
        }

        var covering = MapTile.Cover(viewport.Rounded());

        var places = new List<NearbyPlaceDto>();
        var resolved = new List<string>(covering.Count);
        var stale = new List<MapTile>();
        var missing = new List<MapTile>();
        var now = DateTimeOffset.UtcNow;

        foreach (var tile in covering)
        {
            if (await tiles.ReadAsync(tile, cancellationToken) is not { } cached)
            {
                missing.Add(tile);

                continue;
            }

            places.AddRange(cached.Places);
            resolved.Add(tile.Id);

            if (now - cached.FetchedAt > options.NearbyPlaceCacheTtl)
            {
                stale.Add(tile);
            }
        }

        // Serve the cached tiles now, catch them up afterwards. Refreshing on the request path is
        // what turned a rate-limited provider into an error banner in the first place.
        if (stale.Count > 0)
        {
            refresher.Queue(stale);
        }

        var incomplete = false;

        if (missing.Count > 0)
        {
            var fetched = await tiles.FetchAsync(missing, cancellationToken);

            foreach (var tile in missing)
            {
                if (fetched is null || !fetched.TryGetValue(tile, out var entry))
                {
                    // Flagged, not thrown. Whatever came out of the cache is still worth showing;
                    // only a result that is both empty and incomplete is an outage the caller sees.
                    incomplete = true;

                    continue;
                }

                places.AddRange(entry.Places);
                resolved.Add(tile.Id);
            }
        }

        return new NearbyPlacesResultDto(
            // Tiles are disjoint, so this only ever collapses the duplicate records a provider
            // sometimes carries for one real place.
            places.DistinctBy(place => (place.Latitude, place.Longitude, place.Kind, place.Name)).ToArray(),
            resolved,
            incomplete);
    }

    private static AreaDto Map(GeoProviderArea place, double latitude, double longitude) =>
        new(
            latitude,
            longitude,
            place.DisplayName ?? place.Locality ?? place.Region ?? "Unknown area",
            place.Locality,
            place.Region,
            place.CountryCode?.ToUpperInvariant(),
            AreaPrecision.Area);

    private static double Round(double value) => Math.Round(value, CoordinateDecimals, MidpointRounding.AwayFromZero);
}
