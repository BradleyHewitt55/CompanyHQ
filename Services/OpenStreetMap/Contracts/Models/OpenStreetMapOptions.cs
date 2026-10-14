namespace CompanyHQ.OpenStreetMap.Contracts.Models;

/// <summary>
/// Endpoints, identity and pacing for the public OpenStreetMap services, bound from the
/// <see cref="SectionName"/> configuration section.
/// </summary>
/// <remarks>
/// It binds Geo's own section on purpose: these keys lived on <c>Geo</c> before the provider moved
/// here, and a deployment must not need new configuration to upgrade.
/// </remarks>
public sealed class OpenStreetMapOptions
{
    public const string SectionName = "Geo";

    /// <summary>
    /// Values that mean "nobody filled this in". Rejected so a deployment cannot hammer the public
    /// instance under an identity that reaches no one.
    /// </summary>
    private static readonly string[] Sentinels = ["changeme", "todo", "tbd", "placeholder", "your-app"];

    /// <summary>The Nominatim endpoint, used for area naming and area search.</summary>
    public string BaseAddress { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>The Overpass endpoint, used only for the place search.</summary>
    public string OverpassBaseAddress { get; set; } = "https://overpass-api.de/";

    /// <summary>
    /// Nominatim's usage policy requires a User-Agent that identifies the application and gives a
    /// way to contact its operator; requests without one get blocked.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>The public instance's absolute ceiling is one request per second.</summary>
    public TimeSpan MinimumRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Keep the public Overpass endpoint from receiving a burst when maps open together.</summary>
    public TimeSpan OverpassMinimumRequestInterval { get; set; } = TimeSpan.FromSeconds(1);

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

        if (MinimumRequestInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{SectionName}:MinimumRequestInterval cannot be negative.");
        }

        if (OverpassMinimumRequestInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{SectionName}:OverpassMinimumRequestInterval cannot be negative.");
        }
    }
}
