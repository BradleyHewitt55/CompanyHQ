using CompanyHQ.Services.Rss.Contracts.Dtos;
using CompanyHQ.Services.Rss.Contracts.Forms;

namespace CompanyHQ.Services.Rss.Contracts.Ports;

/// <summary>
/// Finds, downloads and reads RSS and Atom feeds from the public internet on a caller's behalf.
/// It holds no state between calls: the caller keeps the URL, the validators and the items, and
/// decides when to ask again.
/// </summary>
/// <remarks>
/// Neither method throws for anything a publisher or the network does. Every outcome, including a
/// refused address, a timeout and a broken document, comes back as a result with a
/// <see cref="FeedErrorDto"/>; only an argument that is null and the caller's own cancellation
/// surface as exceptions.
/// </remarks>
public interface IRssService
{
    /// <summary>Version of the shapes in this assembly. It changes only when a field is removed or changes meaning.</summary>
    public const int ContractVersion = 1;

    /// <summary>
    /// Reads a website and reports the feeds it advertises together with basic publisher metadata.
    /// A URL that is itself a feed is reported as its own single feed.
    /// </summary>
    Task<FeedDiscoveryResultDto> DiscoverAsync(FeedDiscoveryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads one feed. Send the validators from the previous successful fetch to let the
    /// publisher answer "not modified" instead of the whole document.
    /// </summary>
    Task<FeedFetchResultDto> FetchAsync(FeedFetchRequest request, CancellationToken cancellationToken = default);
}
