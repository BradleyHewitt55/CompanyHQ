namespace CompanyHQ.Geo.Contracts.Models;

/// <summary>
/// Configuration for Geo's own policy — rounding granularity, cache lifetimes, which places count
/// as nearby, and the inbound limit — bound from the <see cref="SectionName"/> configuration section.
/// </summary>
/// <remarks>
/// The provider adapter the host composes binds its endpoints, identity and pacing from the same
/// section, so a deployment keeps one <c>Geo</c> block whichever adapter it uses.
/// </remarks>
public sealed class GeoOptions
{
    public const string SectionName = "Geo";

    private const string MovedToProvider =
        "Geo no longer reads this setting. The provider adapter binds the same Geo configuration key; "
        + "read it from the adapter's options (OpenStreetMapOptions).";

    [Obsolete(MovedToProvider)]
    public string BaseAddress { get; set; } = "https://nominatim.openstreetmap.org/";

    [Obsolete(MovedToProvider)]
    public string OverpassBaseAddress { get; set; } = "https://overpass-api.de/";

    [Obsolete(MovedToProvider)]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Reverse-geocoding zoom. 10 is city/town — coarse enough that two neighbours resolve to the
    /// same area, which is the point.
    /// </summary>
    public int AreaZoom { get; set; } = 10;

    /// <summary>The language place names come back in, so they match the app's.</summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Administrative boundaries barely move; the real constraint is the provider's rate limit, so
    /// cache long.
    /// </summary>
    public int CacheTtlHours { get; set; } = 24;

    [Obsolete(MovedToProvider)]
    public TimeSpan MinimumRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

    [Obsolete(MovedToProvider)]
    public TimeSpan OverpassMinimumRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The <b>soft</b> TTL for a nearby-place tile: past this it is still served, and a background
    /// refresh is started behind the response.
    /// </summary>
    public int NearbyPlaceCacheTtlHours { get; set; } = 6;

    /// <summary>
    /// The hard TTL a tile is actually stored with. Far longer than the soft one on purpose — it is
    /// the outage budget. A place rarely moves, so a three-day-old pin is still correct, and keeping
    /// it means the map still works through a long provider rate-limiting spell.
    /// </summary>
    public int NearbyTileHardTtlHours { get; set; } = 72;

    /// <summary>
    /// The place kinds the nearby-place search returns, in the composed provider's vocabulary — for
    /// OpenStreetMap its <c>amenity</c> values, e.g. <c>pharmacy</c>, <c>hospital</c>, <c>bank</c>.
    /// Empty (the default) switches the search off: it answers with no places and never calls the
    /// provider. Each deployment chooses what "nearby" means for it.
    /// </summary>
    public string[] NearbyPlaceKinds { get; set; } = [];

    /// <summary>
    /// The configured kinds in a stable order, part of every tile's cache key so changing the list
    /// never serves tiles resolved for the old one.
    /// </summary>
    public string NearbyPlaceKindsKey => string.Join('+', NearbyPlaceKinds.Order(StringComparer.Ordinal));

    /// <summary>Policy name used by the anonymous public Geo controllers.</summary>
    public const string InboundRateLimitPolicyName = "Geo";

    /// <summary>Maximum requests per client address in one inbound window.</summary>
    public int InboundRateLimitPermitLimit { get; set; } = 60;

    /// <summary>Length of the inbound abuse-protection window.</summary>
    public int InboundRateLimitWindowSeconds { get; set; } = 60;

    public TimeSpan CacheTtl => TimeSpan.FromHours(CacheTtlHours);

    public TimeSpan NearbyPlaceCacheTtl => TimeSpan.FromHours(NearbyPlaceCacheTtlHours);

    public TimeSpan NearbyTileHardTtl => TimeSpan.FromHours(NearbyTileHardTtlHours);

    /// <summary>Throws when the bound configuration would produce nonsense or unbounded requests.</summary>
    public void Validate()
    {
        // Kinds travel into the cache key and a provider's query, so only bare values are accepted.
        if (NearbyPlaceKinds.Any(kind => string.IsNullOrEmpty(kind)
            || kind.Any(character => !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'))))
        {
            throw new InvalidOperationException(
                $"{SectionName}:NearbyPlaceKinds must be lower-case OpenStreetMap amenity values (a-z, 0-9, _).");
        }

        if (AreaZoom is < 0 or > 18)
        {
            throw new InvalidOperationException($"{SectionName}:AreaZoom must be between 0 and 18.");
        }

        if (string.IsNullOrWhiteSpace(Language))
        {
            throw new InvalidOperationException($"{SectionName}:Language is required.");
        }

        if (CacheTtlHours <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:CacheTtlHours must be greater than zero.");
        }

        if (NearbyPlaceCacheTtlHours <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:NearbyPlaceCacheTtlHours must be greater than zero.");
        }

        // A hard TTL below the soft one would evict every tile before it was ever considered stale,
        // which quietly turns stale-while-revalidate back into fetch-on-every-request.
        if (NearbyTileHardTtlHours < NearbyPlaceCacheTtlHours)
        {
            throw new InvalidOperationException(
                $"{SectionName}:NearbyTileHardTtlHours must be at least {SectionName}:NearbyPlaceCacheTtlHours.");
        }

        if (InboundRateLimitPermitLimit <= 0 || InboundRateLimitWindowSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:InboundRateLimitPermitLimit and InboundRateLimitWindowSeconds must be greater than zero.");
        }
    }
}
