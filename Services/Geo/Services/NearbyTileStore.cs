using System.Globalization;
using CompanyHQ.Cache.Services;
using CompanyHQ.Geo.Contracts.Dtos;
using CompanyHQ.Geo.Contracts.Models;
using CompanyHQ.Geo.Contracts.Ports;

namespace CompanyHQ.Geo.Services;

/// <summary>
/// Reads and writes the per-tile nearby-place cache, and owns the one provider call that fills it.
/// </summary>
/// <remarks>
/// It is a separate class from <see cref="GeoService"/> because the background refresh runs in its
/// own DI scope after the request has finished, and needs exactly this and nothing else.
/// </remarks>
public sealed class NearbyTileStore(IGeoProvider provider, ICacheService cache, GeoOptions options)
{
    public Task<NearbyTileDto?> ReadAsync(MapTile tile, CancellationToken cancellationToken) =>
        cache.GetAsync<NearbyTileDto>(CacheKey(tile), cancellationToken);

    /// <summary>
    /// Fetches every given tile with a single bounding-box query and caches each tile separately.
    /// Null — never an empty dictionary — when the provider refused or could not be reached.
    /// </summary>
    public async Task<IReadOnlyDictionary<MapTile, NearbyTileDto>?> FetchAsync(
        IReadOnlyCollection<MapTile> tiles,
        CancellationToken cancellationToken)
    {
        if (tiles.Count == 0)
        {
            return new Dictionary<MapTile, NearbyTileDto>();
        }

        var (bounds, covered) = MapTile.Enclose(tiles);

        // A box rather than a radius so results line up exactly with the tile grid the cache is
        // keyed on; a radius would spill across tile edges and make an entry unusable for the tile
        // beside it.
        var found = await provider.FindPlacesAsync(new GeoPlaceSearch(bounds, options.NearbyPlaceKinds), cancellationToken);

        // Null, not empty. Caching an outage as "this area has no vets" would be a lie with a
        // six-hour shelf life that no caller could tell apart from the truth.
        if (found is null)
        {
            return null;
        }

        var fetchedAt = DateTimeOffset.UtcNow;

        // The query scanned the whole rectangle, so a tile inside it with no hits is a real "none
        // here" answer and is cached as one. That is the invariant the map's messaging rests on.
        var buckets = covered.ToDictionary(tile => tile, _ => new List<NearbyPlaceDto>());

        foreach (var place in found.Select(MapNearbyPlace).OfType<NearbyPlaceDto>())
        {
            // Assigned by the place's own centre, so a way straddling a boundary always lands in
            // exactly one tile and can never be stored — or returned — twice.
            if (buckets.TryGetValue(MapTile.From(place.Latitude, place.Longitude), out var bucket))
            {
                bucket.Add(place);
            }
        }

        var written = new Dictionary<MapTile, NearbyTileDto>(buckets.Count);

        foreach (var (tile, bucket) in buckets)
        {
            var entry = new NearbyTileDto(
                fetchedAt,
                bucket.DistinctBy(place => (place.Latitude, place.Longitude, place.Kind, place.Name)).ToArray());

            written[tile] = entry;

            // The hard TTL. The soft one lives in FetchedAt and only decides whether a background
            // refresh is worth starting.
            await cache.SetAsync(CacheKey(tile), entry, options.NearbyTileHardTtl, cancellationToken);
        }

        return written;
    }

    /// <summary>
    /// v3 is the cache-shape marker; bump it whenever <c>NearbyPlaceDto</c> or the tile entry gains
    /// or loses a field. The configured kinds are part of the key, so a changed list never serves
    /// tiles resolved for the old one.
    /// </summary>
    private string CacheKey(MapTile tile) =>
        string.Create(CultureInfo.InvariantCulture, $"geo:nearby-tile:v3:{options.NearbyPlaceKindsKey}:{tile.Zoom}:{tile.X}:{tile.Y}");

    /// <summary>
    /// Keeps a provider's place only when it is one that was asked for and sits on the map, so a
    /// provider answering with more than the question never widens what is cached or served.
    /// </summary>
    private NearbyPlaceDto? MapNearbyPlace(GeoProviderPlace place)
    {
        if (place.Latitude is < -90 or > 90 || place.Longitude is < -180 or > 180
            || double.IsNaN(place.Latitude) || double.IsNaN(place.Longitude)
            || !options.NearbyPlaceKinds.Contains(place.Kind, StringComparer.Ordinal))
        {
            return null;
        }

        // An unnamed place still gets a readable label: "fire_station" reads as "Fire station".
        var name = place.Name
            ?? char.ToUpperInvariant(place.Kind[0]) + place.Kind[1..].Replace('_', ' ');

        return new NearbyPlaceDto(place.Latitude, place.Longitude, name, place.Kind, place.Address, place.Phone);
    }
}
