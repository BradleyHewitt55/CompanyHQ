using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using CompanyHQ.Services.Rss.Contracts.Dtos;
using CompanyHQ.Services.Rss.Contracts.Enums;
using CompanyHQ.Services.Rss.Contracts.Models;

namespace CompanyHQ.Services.Rss.Services;

/// <summary>What one guarded download produced: an error, a "not modified", or a bounded body.</summary>
internal sealed record FetchedDocument(
    FeedErrorDto? Error,
    Uri? FinalUri,
    bool MovedPermanently,
    bool NotModified,
    byte[] Body,
    bool Truncated,
    string? MediaType,
    string? Charset,
    FeedValidatorsDto Validators);

/// <summary>
/// The only outbound HTTP in this service. It treats every address as hostile input: it connects
/// only to public addresses, follows redirects itself so each hop is checked the same way, and
/// bounds the time and the bytes a publisher can cost.
/// </summary>
/// <remarks>
/// The address check happens inside the connection callback, on the very addresses the socket is
/// about to be opened to. Checking a name first and letting the HTTP stack resolve it again would
/// leave a gap in which the name can be re-pointed at an internal address.
/// </remarks>
internal sealed class SafeHttpFetcher : IDisposable
{
    private const int MaxUrlLength = 2048;
    private const int MaxValidatorLength = 1024;

    private readonly RssOptions options;
    private readonly IRssNetwork network;
    private readonly HttpMessageInvoker http;

    public SafeHttpFetcher(RssOptions options, IRssNetwork network)
    {
        this.options = options;
        this.network = network;

        http = new HttpMessageInvoker(new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync,
            ConnectTimeout = options.ConnectTimeout,
            // Each of these would otherwise act on the publisher's word before this class has
            // looked at it: a redirect followed unchecked, a proxy that does the connecting, a
            // decompressor with no ceiling, a cookie carried to the next publisher.
            AllowAutoRedirect = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            MaxResponseHeadersLength = 64,
            MaxConnectionsPerServer = 2,
            // A pooled connection stays pinned to the address it was approved for; a short life
            // keeps a pin from outliving the publisher's own DNS.
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30)
        }, disposeHandler: true);
    }

    /// <summary>
    /// Downloads one document. With <paramref name="truncate"/> a body longer than
    /// <paramref name="maxBytes"/> is cut there instead of being refused, for a web page whose
    /// useful part is its head.
    /// </summary>
    public async Task<FetchedDocument> GetAsync(
        string url,
        FeedValidatorsDto? validators,
        string accept,
        int maxBytes,
        bool truncate,
        CancellationToken cancellationToken)
    {
        var sent = Usable(validators);

        var (checkedUri, urlError) = Check(url);
        if (checkedUri is null)
        {
            return Failed(urlError!, null, sent);
        }

        var uri = checkedUri;

        // One budget for the whole exchange, so a publisher cannot spend it once per redirect.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);

        var permanent = true;

        try
        {
            for (var redirects = 0; ; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri)
                {
                    // HTTP/3 would bypass the connection callback, and with it the address check.
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                };
                request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);
                request.Headers.TryAddWithoutValidation("Accept", accept);
                request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
                if (sent.ETag is not null) request.Headers.TryAddWithoutValidation("If-None-Match", sent.ETag);
                if (sent.LastModified is not null) request.Headers.TryAddWithoutValidation("If-Modified-Since", sent.LastModified);

                using var response = await http.SendAsync(request, deadline.Token);
                var status = (int)response.StatusCode;

                if (status == 304)
                {
                    // "Not modified" answers a question; without validators none was asked.
                    if (sent.IsEmpty)
                    {
                        return Failed(Error(FeedErrorCode.InvalidResponse), uri, sent);
                    }

                    var confirmed = ReadValidators(response);

                    return new FetchedDocument(null, uri, permanent && redirects > 0, true, [], false, null, null,
                        new FeedValidatorsDto(confirmed.ETag ?? sent.ETag, confirmed.LastModified ?? sent.LastModified));
                }

                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    if (redirects >= options.MaxRedirects)
                    {
                        return Failed(Error(FeedErrorCode.TooManyRedirects), uri, sent);
                    }

                    if (response.Headers.Location is not { } location || !Uri.TryCreate(uri, location, out var next))
                    {
                        return Failed(Error(FeedErrorCode.InvalidResponse), uri, sent);
                    }

                    if (next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps
                        || (uri.Scheme == Uri.UriSchemeHttps && next.Scheme == Uri.UriSchemeHttp))
                    {
                        return Failed(Error(FeedErrorCode.UnsafeRedirect), uri, sent);
                    }

                    // The redirect target is checked exactly as the first address was.
                    var checkedNext = Check(next.AbsoluteUri);
                    if (checkedNext.Uri is null)
                    {
                        var error = checkedNext.Error!;

                        return Failed(error.Code == FeedErrorCode.UnsafeAddress ? error : Error(FeedErrorCode.UnsafeRedirect), uri, sent);
                    }

                    permanent &= status is 301 or 308;
                    uri = checkedNext.Uri;

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Failed(
                        new FeedErrorDto(FeedErrorCode.HttpStatus, Messages[FeedErrorCode.HttpStatus], status,
                            Transient: status is 408 or 425 or 429 or >= 500),
                        uri,
                        sent);
                }

                if (!truncate && response.Content.Headers.ContentLength > maxBytes)
                {
                    return Failed(Error(FeedErrorCode.ResponseTooLarge), uri, sent);
                }

                var (body, truncated, bodyError) = await ReadBodyAsync(response, maxBytes, truncate, deadline.Token);
                if (bodyError is not null)
                {
                    return Failed(bodyError, uri, sent);
                }

                return new FetchedDocument(
                    null,
                    uri,
                    permanent && redirects > 0,
                    false,
                    body,
                    truncated,
                    response.Content.Headers.ContentType?.MediaType?.Trim().ToLowerInvariant(),
                    response.Content.Headers.ContentType?.CharSet?.Trim().Trim('"', '\''),
                    ReadValidators(response));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(Error(FeedErrorCode.Timeout, transient: true), uri, sent);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or SocketException)
        {
            return Failed(
                Find<UnsafeAddressException>(exception) is not null
                    ? Error(FeedErrorCode.UnsafeAddress)
                    : Error(FeedErrorCode.Unreachable, transient: true),
                uri,
                sent);
        }
    }

    public void Dispose() => http.Dispose();

    /// <summary>
    /// Opens the connection for one request. The host name is resolved here, every address it
    /// resolves to must be public, and the socket is opened to one of those same addresses, so
    /// what was checked and what is connected to can never differ.
    /// </summary>
    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await network.ResolveAsync(host, cancellationToken);

        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        // One internal address among public ones is still a name somebody pointed inwards.
        if (addresses.Any(address => !PublicAddress.IsPublic(address)))
        {
            throw new UnsafeAddressException();
        }

        Exception? last = null;
        foreach (var address in addresses)
        {
            try
            {
                return await network.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                last = exception;
            }
        }

        throw last!;
    }

    /// <summary>Everything that can be decided about an address without touching the network.</summary>
    private (Uri? Uri, FeedErrorDto? Error) Check(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length > 0
            || uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6)
            || !options.AllowsPort(uri.Port))
        {
            return (null, Error(FeedErrorCode.InvalidUrl));
        }

        var host = uri.IdnHost.TrimEnd('.');
        if (host.Length == 0)
        {
            return (null, Error(FeedErrorCode.InvalidUrl));
        }

        // A literal address, in whatever notation Uri normalised it from, is judged before any
        // socket exists. Names are judged when they are resolved, in ConnectAsync.
        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? !PublicAddress.IsPublic(literal)
            : host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return (null, Error(FeedErrorCode.UnsafeAddress));
        }

        // The fragment never reaches a server, so it is not part of the address that is fetched.
        return (new UriBuilder(uri) { Fragment = string.Empty }.Uri, null);
    }

    private static async Task<(byte[] Body, bool Truncated, FeedErrorDto? Error)> ReadBodyAsync(
        HttpResponseMessage response,
        int maxBytes,
        bool truncate,
        CancellationToken cancellationToken)
    {
        var encodings = response.Content.Headers.ContentEncoding
            .Select(encoding => encoding.Trim().ToLowerInvariant())
            .Where(encoding => encoding is not ("" or "identity"))
            .ToArray();

        if (encodings.Length > 2)
        {
            return ([], false, Error(FeedErrorCode.InvalidResponse));
        }

        await using var wire = await response.Content.ReadAsStreamAsync(cancellationToken);
        // The first ceiling: bytes off the wire, whatever they later expand to.
        Stream decoded = new CappedStream(wire, maxBytes, throwAtLimit: !truncate);

        try
        {
            // Encodings are listed in the order they were applied, so they are undone in reverse.
            foreach (var encoding in encodings.Reverse())
            {
                switch (encoding)
                {
                    case "gzip" or "x-gzip":
                        decoded = new GZipStream(decoded, CompressionMode.Decompress);
                        break;
                    case "deflate":
                        // The name means zlib, but some servers send the bare stream.
                        var probe = new byte[2];
                        var probed = await decoded.ReadAtLeastAsync(probe, 2, throwOnEndOfStream: false, cancellationToken);
                        var rewound = new PrefixedStream(probe.AsMemory(0, probed), decoded);
                        decoded = probed == 2 && (probe[0] & 0x0f) == 8 && ((probe[0] << 8) | probe[1]) % 31 == 0
                            ? new ZLibStream(rewound, CompressionMode.Decompress)
                            : new DeflateStream(rewound, CompressionMode.Decompress);
                        break;
                    case "br":
                        decoded = new BrotliStream(decoded, CompressionMode.Decompress);
                        break;
                    default:
                        return ([], false, Error(FeedErrorCode.InvalidResponse));
                }
            }

            // The second ceiling: bytes after decompression. One byte past it proves there was more.
            var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            try
            {
                while (buffer.Length <= maxBytes)
                {
                    var read = await decoded.ReadAsync(chunk, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    buffer.Write(chunk, 0, read);
                }
            }
            catch (Exception exception) when (truncate && buffer.Length > 0 && exception is InvalidDataException or IOException)
            {
                // A page cut off mid-stream still has its head; what was read is what is used.
            }

            if (buffer.Length <= maxBytes)
            {
                return (buffer.ToArray(), false, null);
            }

            return truncate
                ? (buffer.GetBuffer().AsSpan(0, maxBytes).ToArray(), true, null)
                : ([], false, Error(FeedErrorCode.ResponseTooLarge));
        }
        catch (ResponseTooLargeException)
        {
            return ([], false, Error(FeedErrorCode.ResponseTooLarge));
        }
        catch (InvalidDataException)
        {
            return ([], false, Error(FeedErrorCode.InvalidResponse));
        }
        finally
        {
            await decoded.DisposeAsync();
        }
    }

    private static FeedValidatorsDto ReadValidators(HttpResponseMessage response) => Usable(new FeedValidatorsDto(
        response.Headers.TryGetValues("ETag", out var etags) ? etags.FirstOrDefault() : null,
        response.Content.Headers.TryGetValues("Last-Modified", out var modified) ? modified.FirstOrDefault()
            : response.Headers.TryGetValues("Last-Modified", out modified) ? modified.FirstOrDefault() : null));

    /// <summary>
    /// A validator is echoed into a request header, so one that could not be a single header value
    /// is dropped rather than sent: it came from a publisher, or from wherever the caller stored it.
    /// </summary>
    private static FeedValidatorsDto Usable(FeedValidatorsDto? validators)
    {
        static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) || value.Length > MaxValidatorLength || value.Any(character => character is < ' ' or > '~')
                ? null
                : value.Trim();

        return validators is null ? FeedValidatorsDto.None : new FeedValidatorsDto(Clean(validators.ETag), Clean(validators.LastModified));
    }

    private static FetchedDocument Failed(FeedErrorDto error, Uri? uri, FeedValidatorsDto sent) =>
        new(error, uri, false, false, [], false, null, null, sent);

    private static T? Find<T>(Exception? exception) where T : Exception
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is T found)
            {
                return found;
            }
        }

        return null;
    }

    internal static FeedErrorDto Error(FeedErrorCode code, bool transient = false) => new(code, Messages[code], Transient: transient);

    /// <summary>One fixed sentence per code. Nothing a publisher sent is ever interpolated into an error.</summary>
    private static readonly Dictionary<FeedErrorCode, string> Messages = new()
    {
        [FeedErrorCode.InvalidUrl] = "The address must be an absolute http or https URL without credentials, on an allowed port.",
        [FeedErrorCode.UnsafeAddress] = "The address is not on the public internet.",
        [FeedErrorCode.UnsafeRedirect] = "The publisher redirected to an address that is not followed.",
        [FeedErrorCode.TooManyRedirects] = "The publisher redirected too many times.",
        [FeedErrorCode.Unreachable] = "The publisher could not be reached.",
        [FeedErrorCode.Timeout] = "The publisher did not answer in time.",
        [FeedErrorCode.HttpStatus] = "The publisher answered with an error status.",
        [FeedErrorCode.ResponseTooLarge] = "The response is larger than the configured maximum.",
        [FeedErrorCode.InvalidResponse] = "The response could not be decoded.",
        [FeedErrorCode.NotAFeed] = "The document is not an RSS or Atom feed.",
        [FeedErrorCode.MalformedXml] = "The document is not well-formed XML.",
        [FeedErrorCode.ProhibitedXml] = "The document declares a DTD or entities, which are not processed."
    };

    private sealed class UnsafeAddressException() : IOException("The address is not on the public internet.");

    private sealed class ResponseTooLargeException() : IOException("The response is larger than the configured maximum.");

    /// <summary>Stops a read at a byte ceiling: by failing, or by reporting the end of the stream.</summary>
    private sealed class CappedStream(Stream inner, long limit, bool throwAtLimit) : Stream
    {
        private long read;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => read; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (read > limit)
            {
                return throwAtLimit ? throw new ResponseTooLargeException() : 0;
            }

            // One byte past the limit is allowed through, so "exactly at the limit" and "more" differ.
            var allowed = (int)Math.Min(buffer.Length, limit + 1 - read);
            var count = await inner.ReadAsync(buffer[..allowed], cancellationToken);
            read += count;

            return read > limit && throwAtLimit ? throw new ResponseTooLargeException() : count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Replays bytes that were read ahead to identify a stream, then continues with the stream.</summary>
    private sealed class PrefixedStream(ReadOnlyMemory<byte> prefix, Stream inner) : Stream
    {
        private ReadOnlyMemory<byte> pending = prefix;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (pending.IsEmpty)
            {
                return inner.ReadAsync(buffer, cancellationToken);
            }

            var count = Math.Min(buffer.Length, pending.Length);
            pending[..count].CopyTo(buffer);
            pending = pending[count..];

            return ValueTask.FromResult(count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
