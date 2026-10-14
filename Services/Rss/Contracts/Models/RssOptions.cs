namespace CompanyHQ.Rss.Contracts.Models;

/// <summary>
/// Limits for every outbound feed request, bound from the <see cref="SectionName"/> configuration
/// section. Every default is deliberately small: the addresses come from outside, so each limit is
/// a bound on what an untrusted publisher can make this host do.
/// </summary>
public sealed class RssOptions
{
    public const string SectionName = "Rss";

    /// <summary>Sent with every request so a publisher can tell who is reading its feed.</summary>
    public string UserAgent { get; set; } = "CompanyHQ-Rss/1.0";

    /// <summary>One budget for a whole fetch or discovery: every redirect, the headers and the body.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>How long one connection attempt may take.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;

    /// <summary>Redirects followed before giving up. Zero follows none.</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>
    /// The largest feed read, in bytes. It bounds the bytes taken off the wire and, separately, the
    /// bytes they decompress to, so a small compressed response cannot expand without limit.
    /// </summary>
    public int MaxFeedBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>How much of a web page discovery reads. Feed links sit in the head, so the rest is not fetched.</summary>
    public int MaxPageBytes { get; set; } = 1024 * 1024;

    /// <summary>Entries kept from one feed. A longer document is cut and flagged as truncated.</summary>
    public int MaxItems { get; set; } = 500;

    /// <summary>Feeds reported for one website.</summary>
    public int MaxDiscoveredFeeds { get; set; } = 20;

    /// <summary>
    /// Ports a feed address may use. Empty, the default, means 80 and 443 only, so a caller-supplied
    /// address cannot be used to reach an arbitrary service on a public host.
    /// </summary>
    public int[] AllowedPorts { get; set; } = [];

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

    public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(ConnectTimeoutSeconds);

    public bool AllowsPort(int port) => AllowedPorts.Length == 0 ? port is 80 or 443 : AllowedPorts.Contains(port);

    /// <summary>Throws when the bound configuration would remove a limit or send an unidentified request.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(UserAgent) || UserAgent.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{SectionName}:UserAgent is required and must be a single line.");
        }

        if (TimeoutSeconds is <= 0 or > 120 || ConnectTimeoutSeconds is <= 0 or > 60)
        {
            throw new InvalidOperationException(
                $"{SectionName}:TimeoutSeconds must be 1 to 120 and {SectionName}:ConnectTimeoutSeconds 1 to 60.");
        }

        if (MaxRedirects is < 0 or > 10)
        {
            throw new InvalidOperationException($"{SectionName}:MaxRedirects must be 0 to 10.");
        }

        if (MaxFeedBytes is < 1024 or > 50 * 1024 * 1024 || MaxPageBytes is < 1024 or > 50 * 1024 * 1024)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaxFeedBytes and {SectionName}:MaxPageBytes must be between 1 KB and 50 MB.");
        }

        if (MaxItems is <= 0 or > 10_000 || MaxDiscoveredFeeds is <= 0 or > 100)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaxItems must be 1 to 10000 and {SectionName}:MaxDiscoveredFeeds 1 to 100.");
        }

        if (AllowedPorts.Any(port => port is <= 0 or > 65535))
        {
            throw new InvalidOperationException($"{SectionName}:AllowedPorts must be valid TCP ports.");
        }
    }
}
