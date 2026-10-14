using System.Text;
using Microsoft.Extensions.Logging;
using CompanyHQ.Rss.Contracts.Dtos;
using CompanyHQ.Rss.Contracts.Enums;
using CompanyHQ.Rss.Contracts.Forms;
using CompanyHQ.Rss.Contracts.Models;
using CompanyHQ.Rss.Contracts.Ports;

namespace CompanyHQ.Rss.Services;

/// <inheritdoc cref="IRssService"/>
public sealed class RssService : IRssService, IDisposable
{
    private const string FeedAccept = "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.8, */*;q=0.5";
    private const string PageAccept = "text/html, application/xhtml+xml, application/rss+xml;q=0.9, application/atom+xml;q=0.9, */*;q=0.5";

    private readonly RssOptions options;
    private readonly ILogger<RssService> logger;
    private readonly SafeHttpFetcher fetcher;

    static RssService()
    {
        // Feeds and pages still arrive in windows-1252 and friends; without this only the Unicode
        // encodings and Latin-1 are known and such a document cannot be decoded at all.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public RssService(RssOptions options, IRssNetwork network, ILogger<RssService> logger)
    {
        this.options = options;
        this.logger = logger;
        fetcher = new SafeHttpFetcher(options, network);
    }

    public async Task<FeedFetchResultDto> FetchAsync(FeedFetchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var document = await fetcher.GetAsync(request.Url, request.Validators, FeedAccept, options.MaxFeedBytes, truncate: false, cancellationToken);
        var finalUrl = document.FinalUri?.AbsoluteUri;

        if (document.Error is not null)
        {
            return Failed(document.Error, document);
        }

        if (document.NotModified)
        {
            return new FeedFetchResultDto(FeedFetchStatus.NotModified, finalUrl, document.MovedPermanently, document.Validators, null, null);
        }

        var (feed, error) = FeedParser.Parse(document.Body, document.Charset, document.FinalUri!, options.MaxItems);

        return feed is null
            // The publisher's new validators describe a document that could not be read, so they are
            // not handed back: storing them would make the next fetch skip it as "not modified".
            ? Failed(error!, document with { Validators = request.Validators ?? FeedValidatorsDto.None })
            : new FeedFetchResultDto(FeedFetchStatus.Changed, finalUrl, document.MovedPermanently, document.Validators, feed, null);
    }

    public async Task<FeedDiscoveryResultDto> DiscoverAsync(FeedDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var document = await fetcher.GetAsync(request.Url, null, PageAccept, options.MaxPageBytes, truncate: true, cancellationToken);
        var finalUrl = document.FinalUri?.AbsoluteUri;

        if (document.Error is not null)
        {
            Log(document.Error, document.FinalUri, "discovery");

            return new FeedDiscoveryResultDto(FeedDiscoveryStatus.Failed, finalUrl, null, [], document.Error);
        }

        var pageUri = document.FinalUri!;
        var declaresFeed = document.MediaType is "application/rss+xml" or "application/atom+xml" or "application/rdf+xml";

        // The address may itself be a feed. A cut document cannot be parsed, so only a whole one is tried.
        if (!document.Truncated && (declaresFeed || LooksLikeXml(document)))
        {
            var (feed, error) = FeedParser.Parse(document.Body, document.Charset, pageUri, maxItems: 1);

            if (feed is not null)
            {
                return new FeedDiscoveryResultDto(
                    FeedDiscoveryStatus.Found,
                    finalUrl,
                    new PublisherDto(feed.SiteUrl ?? pageUri.GetLeftPart(UriPartial.Authority) + "/", feed.Title, feed.Description, feed.ImageUrl, feed.Language),
                    [new DiscoveredFeedDto(pageUri.AbsoluteUri, feed.Format, feed.Title)],
                    null);
            }

            // Served as a feed and unreadable as one is a failure. An XHTML page merely looks like XML.
            if (declaresFeed)
            {
                Log(error!, pageUri, "discovery");

                return new FeedDiscoveryResultDto(FeedDiscoveryStatus.Failed, finalUrl, null, [], error);
            }
        }

        var (publisher, feeds) = HtmlHead.Read(document.Body, document.Charset, pageUri, options.MaxDiscoveredFeeds);

        return new FeedDiscoveryResultDto(
            feeds.Count > 0 ? FeedDiscoveryStatus.Found : FeedDiscoveryStatus.NoneFound,
            finalUrl,
            publisher,
            feeds,
            null);
    }

    public void Dispose() => fetcher.Dispose();

    private FeedFetchResultDto Failed(FeedErrorDto error, FetchedDocument document)
    {
        Log(error, document.FinalUri, "fetch");

        return new FeedFetchResultDto(FeedFetchStatus.Failed, document.FinalUri?.AbsoluteUri, false, document.Validators, null, error);
    }

    /// <summary>
    /// The host and the code only. A feed address can carry a subscriber's token in its path or
    /// query, so the address itself never reaches a log.
    /// </summary>
    private void Log(FeedErrorDto error, Uri? uri, string operation) =>
        logger.LogInformation(
            "RSS {Operation} of {Host} failed with {Code} (status {Status}).",
            operation,
            uri?.IdnHost ?? "an invalid address",
            error.Code,
            error.HttpStatus);

    private static bool LooksLikeXml(FetchedDocument document)
    {
        if (document.MediaType is "application/xml" or "text/xml")
        {
            return true;
        }

        var start = Encoding.Latin1.GetString(document.Body, 0, Math.Min(document.Body.Length, 512)).TrimStart((char)0xFEFF, (char)0xEF, (char)0xBB, (char)0xBF, ' ', '\t', '\r', '\n');

        return start.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || start.StartsWith("<rss", StringComparison.OrdinalIgnoreCase)
            || start.StartsWith("<feed", StringComparison.OrdinalIgnoreCase)
            || start.StartsWith("<rdf:RDF", StringComparison.OrdinalIgnoreCase);
    }
}
