using CompanyHQ.Geo.Contracts.Models;

namespace CompanyHQ.Geo.Contracts.Ports;

/// <summary>
/// One reverse lookup. <see cref="Zoom"/> is the map zoom whose granularity the area should have
/// (0 a continent, 10 a town, 18 a building); <see cref="Language"/> is the language to name it in.
/// </summary>
public sealed record GeoAreaLookup(double Latitude, double Longitude, int Zoom, string Language);

/// <summary>One free-text area search, already trimmed and limited by Geo.</summary>
public sealed record GeoAreaSearch(string Query, int Limit, string Language);

/// <summary>
/// One place search over a tile-aligned box. <see cref="Kinds"/> are the configured place kinds in
/// the provider's own vocabulary, validated at startup to lower-case letters, digits and underscores.
/// </summary>
public sealed record GeoPlaceSearch(GeoViewport Bounds, IReadOnlyList<string> Kinds);
