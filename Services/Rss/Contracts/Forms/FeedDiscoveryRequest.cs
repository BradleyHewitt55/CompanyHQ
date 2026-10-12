namespace CompanyHQ.Services.Rss.Contracts.Forms;

/// <summary>One discovery: the address of a publisher's website, or of a page on it.</summary>
public sealed record FeedDiscoveryRequest(string Url);
