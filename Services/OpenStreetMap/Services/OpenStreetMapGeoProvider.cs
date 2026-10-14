using CompanyHQ.Geo.Contracts.Ports;
using CompanyHQ.OpenStreetMap.Contracts.Dtos;

namespace CompanyHQ.OpenStreetMap.Services;

/// <summary>
/// Geo's <see cref="IGeoProvider"/> over the public OpenStreetMap services: Nominatim names and
/// searches areas, Overpass finds places. It turns their response shapes and tags into Geo's
/// provider results and decides nothing about rounding, caching or which results are kept.
/// </summary>
public sealed class OpenStreetMapGeoProvider(NominatimClient nominatim, OverpassClient overpass) : IGeoProvider
{
    public async Task<GeoProviderArea?> FindAreaAsync(GeoAreaLookup lookup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var place = await nominatim.ReverseAsync(lookup.Latitude, lookup.Longitude, lookup.Zoom, lookup.Language, cancellationToken);

        return place is null ? null : MapArea(place);
    }

    public async Task<IReadOnlyList<GeoProviderArea>> SearchAreasAsync(GeoAreaSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var places = await nominatim.SearchAsync(search.Query, search.Limit, search.Language, cancellationToken);

        return places.Select(MapArea).ToArray();
    }

    public async Task<IReadOnlyList<GeoProviderPlace>?> FindPlacesAsync(GeoPlaceSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        var elements = await overpass.SearchBoundingBoxAsync(search.Bounds, search.Kinds, cancellationToken);

        // Null stays null: the query failed, which is not the same answer as an empty box.
        return elements?.Select(MapPlace).OfType<GeoProviderPlace>().ToArray();
    }

    private static GeoProviderArea MapArea(NominatimResponse place) =>
        new(
            NominatimResponse.ParseCoordinate(place.Latitude),
            NominatimResponse.ParseCoordinate(place.Longitude),
            place.DisplayName,
            place.Address?.Locality,
            place.Address?.State,
            place.Address?.CountryCode);

    /// <summary>
    /// A node carries its own position; a way or relation carries the representative
    /// <c>center</c> Overpass computed. An element with neither, or with no <c>amenity</c> tag, is
    /// not a place this adapter can describe.
    /// </summary>
    private static GeoProviderPlace? MapPlace(OverpassElement element)
    {
        var latitude = element.Latitude ?? element.Center?.Latitude;
        var longitude = element.Longitude ?? element.Center?.Longitude;

        if (latitude is null || longitude is null
            || element.Tags is not { } tags
            || !tags.TryGetValue("amenity", out var amenity))
        {
            return null;
        }

        return new GeoProviderPlace(
            latitude.Value,
            longitude.Value,
            amenity,
            tags.GetValueOrDefault("name"),
            MapAddress(tags),
            MapPhone(tags));
    }

    /// <summary>
    /// Composes the OpenStreetMap <c>addr:*</c> parts into one readable line.
    /// </summary>
    /// <remarks>
    /// These are published business addresses, not a person's. The privacy rule Geo exists to
    /// protect is about the *user's* position, which is still rounded and still never stored.
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
