namespace CompanyHQ.Services.Geo.Contracts.Models;

/// <summary>
/// Configuration for the OpenStreetMap/Nominatim lookups, bound from the <see cref="SectionName"/>
/// configuration section.
/// </summary>
public sealed class GeoOptions
{
    public const string SectionName = "Geo";

    /// <summary>
    /// Values that mean "nobody filled this in". Rejected so a deployment cannot hammer the public
    /// instance under an identity that reaches no one.
    /// </summary>
    private static readonly string[] Sentinels = ["changeme", "todo", "tbd", "placeholder", "your-app"];

    public string BaseAddress { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>The OpenStreetMap Overpass endpoint used only for nearby-place pins.</summary>
    public string OverpassBaseAddress { get; set; } = "https://overpass-api.de/";

    /// <summary>
    /// Nominatim's usage policy requires a User-Agent that identifies the application and gives a
    /// way to contact its operator; requests without one get blocked.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Reverse-geocoding zoom. 10 is city/town — coarse enough that two neighbours resolve to the
    /// same area, which is the point.
    /// </summary>
    public int AreaZoom { get; set; } = 10;

    /// <summary>Sent as <c>Accept-Language</c>, so place names come back in the app's language.</summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Administrative boundaries barely move; the real constraint is the rate limit, so cache long.
    /// </summary>
    public int CacheTtlHours { get; set; } = 24;

    /// <summary>The public instance's absolute ceiling is one request per second.</summary>
    public TimeSpan MinimumRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Keep the public Overpass endpoint from receiving a burst when maps open together.</summary>
    public TimeSpan OverpassMinimumRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The <b>soft</b> TTL for a nearby-place tile: past this it is still served, and a background
    /// refresh is started behind the response.
    /// </summary>
    public int NearbyPlaceCacheTtlHours { get; set; } = 6;

    /// <summary>
    /// The hard TTL a tile is actually stored with. Far longer than the soft one on purpose — it is
    /// the outage budget. A place rarely moves, so a three-day-old pin is still correct, and keeping
    /// it means the map still works through a long Overpass rate-limiting spell.
    /// </summary>
    public int NearbyTileHardTtlHours { get; set; } = 72;

    /// <summary>
    /// The OpenStreetMap <c>amenity</c> values the nearby-place search returns, e.g. <c>pharmacy</c>,
    /// <c>hospital</c>, <c>bank</c>. Empty (the default) switches the search off: it answers with no
    /// places and never calls Overpass. Each deployment chooses what "nearby" means for it.
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

    /// <summary>Throws when the bound configuration would produce blocked or nonsense requests.</summary>
    public void Validate()
    {
        if (!Uri.TryCreate(BaseAddress, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"{SectionName}:BaseAddress must be an absolute URI.");
        }

        if (!Uri.TryCreate(OverpassBaseAddress, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"{SectionName}:OverpassBaseAddress must be an absolute URI.");
        }

        // A contact is what the policy actually asks for, so require something reachable rather
        // than merely non-empty.
        var hasContact = UserAgent.Contains('@', StringComparison.Ordinal)
            || UserAgent.Contains("http", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(UserAgent)
            || !hasContact
            || Sentinels.Any(sentinel => UserAgent.Contains(sentinel, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{SectionName}:UserAgent must identify this deployment and include a reachable contact " +
                "address or URL. Nominatim's usage policy blocks unidentified clients.");
        }

        // Kinds are spliced into the Overpass query, so only bare tag values are accepted.
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

        if (MinimumRequestInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{SectionName}:MinimumRequestInterval cannot be negative.");
        }

        if (OverpassMinimumRequestInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{SectionName}:OverpassMinimumRequestInterval cannot be negative.");
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
