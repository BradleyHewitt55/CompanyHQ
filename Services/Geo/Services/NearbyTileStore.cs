using System.Globalization;
using CompanyHQ.Services.Cache.Services;
using CompanyHQ.Services.Geo.Contracts.Dtos;
using CompanyHQ.Services.Geo.Contracts.Enums;
using CompanyHQ.Services.Geo.Contracts.Models;

namespace CompanyHQ.Services.Geo.Services;

/// <summary>
/// Reads and writes the per-tile nearby-place cache, and owns the one Overpass call that fills it.
/// </summary>
/// <remarks>
/// It is a separate class from <see cref="GeoService"/> because the background refresh runs in its
/// own DI scope after the request has finished, and needs exactly this and nothing else.
/// </remarks>
public sealed class NearbyTileStore(OverpassClient overpass, ICacheService cache, GeoOptions options)
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

        var elements = await overpass.SearchBoundingBoxAsync(bounds, cancellationToken);

        // Null, not empty. Caching an outage as "this area has no vets" would be a lie with a
        // six-hour shelf life that no caller could tell apart from the truth.
        if (elements is null)
        {
            return null;
        }

        var fetchedAt = DateTimeOffset.UtcNow;

        // The query scanned the whole rectangle, so a tile inside it with no hits is a real "none
        // here" answer and is cached as one. That is the invariant the map's messaging rests on.
        var buckets = covered.ToDictionary(tile => tile, _ => new List<NearbyPlaceDto>());

        foreach (var place in elements.Select(MapNearbyPlace).OfType<NearbyPlaceDto>())
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

    private NearbyPlaceDto? MapNearbyPlace(OverpassElement element)
    {
        var latitude = element.Latitude ?? element.Center?.Latitude;
        var longitude = element.Longitude ?? element.Center?.Longitude;

        if (latitude is null or < -90 or > 90 || longitude is null or < -180 or > 180
            || element.Tags is not { } tags
            || !tags.TryGetValue("amenity", out var amenity))
        {
            return null;
        }

        if (!options.NearbyPlaceKinds.Contains(amenity, StringComparer.Ordinal))
        {
            return null;
        }

        // An unnamed place still gets a readable label: "fire_station" reads as "Fire station".
        var name = tags.GetValueOrDefault("name")
            ?? char.ToUpperInvariant(amenity[0]) + amenity[1..].Replace('_', ' ');

        return new NearbyPlaceDto(
            latitude.Value,
            longitude.Value,
            name,
            amenity,
            MapAddress(tags),
            MapPhone(tags));
    }

    /// <summary>
    /// Composes the OpenStreetMap <c>addr:*</c> parts into one readable line.
    /// </summary>
    /// <remarks>
    /// These are published business addresses, not a person's. The privacy rule this service exists
    /// to protect is about the *user's* position, which is still rounded and still never stored.
    /// </remarks>
    private static string? MapAddress(IReadOnlyDictionary<string, string> tags)
    {
        // A mapper who filled in addr:full has already made the editorial call about what belongs on
        // one line; second-guessing it by recomposing the parts reads worse.
        if (Clean(tags.GetValueOrDefault("addr:full")) is { } full)
        {
            return full;
        }

        var street = Clean(tags.GetValueOrDefault("addr:street"));
        var houseNumber = Clean(tags.GetValueOrDefault("addr:housenumber"));

        var parts = new[]
        {
            street is null ? null : houseNumber is null ? street : $"{houseNumber} {street}",
            Clean(tags.GetValueOrDefault("addr:suburb")),
            Clean(tags.GetValueOrDefault("addr:city")),
            Clean(tags.GetValueOrDefault("addr:postcode"))
        }.OfType<string>().ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Prefers the plain <c>phone</c> tag; <c>contact:phone</c> is the older equivalent.</summary>
    private static string? MapPhone(IReadOnlyDictionary<string, string> tags)
    {
        var phone = Clean(tags.GetValueOrDefault("phone"))
            ?? Clean(tags.GetValueOrDefault("contact:phone"));

        // Multi-value tags are semicolon separated. One number is what a "Call" button can act on.
        return phone?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
