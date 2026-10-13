using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using CompanyHQ.Services.Cache.Extensions;
using CompanyHQ.Services.Geo.Contracts.Dtos;
using CompanyHQ.Services.Geo.Contracts.Enums;
using CompanyHQ.Services.Geo.Contracts.Models;
using CompanyHQ.Services.Geo.Contracts.Ports;
using CompanyHQ.Services.Geo.Controllers;
using CompanyHQ.Services.Geo.Extensions;
using CompanyHQ.Services.Geo.Services;
using CompanyHQ.Services.OpenStreetMap.Extensions;
using StackExchange.Redis;
using Xunit;

namespace CompanyHQ.Tests;

/// <summary>
/// Geo end to end — the service, its provider, and a real Redis — with the public OpenStreetMap
/// endpoints replaced by canned responses. Pins what clients and already-cached entries rely on:
/// rounded coordinates, cache keys and payloads, tile semantics, and how an outage is reported.
/// </summary>
public sealed class GeoTests(GeoFixture fixture) : IClassFixture<GeoFixture>
{
    // Two neighbouring zoom-12 tiles: x 2369 and 2370 on row 2355.
    private static readonly GeoViewport TwoTiles = new(-26.10, 28.23, -26.06, 28.35);
    private const string LeftTile = "12/2369/2355";
    private const string RightTile = "12/2370/2355";

    private const string ReversePlace = """
        {"place_id":1,"lat":"-26.0884911","lon":"28.2275133","display_name":"Kempton Park, Ekurhuleni, Gauteng, South Africa",
         "address":{"town":"Kempton Park","suburb":"Aston Manor","state":"Gauteng","country":"South Africa","country_code":"za"},"boundingbox":["-26.1","-26.0","28.2","28.3"]}
        """;

    private const string SearchPlaces = """
        [{"lat":"-26.10045","lon":"28.23449","display_name":"Kempton Park, Gauteng, South Africa","address":{"city":"Kempton Park","state":"Gauteng","country_code":"za"}},
         {"lat":"not-a-number","lon":"28.1","display_name":"Broken","address":{"village":"Nowhere"}},
         {"lat":"51.5074","lon":"-0.1278","address":{"suburb":"Kempton","state":"England","country_code":"gb"}},
         {"lat":"1.0006","lon":"2.0006"}]
        """;

    private const string NearbyElements = """
        {"version":0.6,"elements":[
          {"type":"node","id":1,"lat":-26.09,"lon":28.25,"tags":{"amenity":"veterinary","name":"Aston Vet","addr:housenumber":"12","addr:street":"Monument Road","addr:suburb":"Aston Manor","addr:city":"Kempton Park","addr:postcode":"1619","phone":"+27 11 000 0000; +27 11 111 1111"}},
          {"type":"node","id":2,"lat":-26.09,"lon":28.25,"tags":{"amenity":"veterinary","name":"Aston Vet"}},
          {"type":"way","id":3,"center":{"lat":-26.08,"lon":28.34},"tags":{"amenity":"animal_shelter","addr:full":" 4 Kennel Lane, Bredell ","contact:phone":"011 222 3333"}},
          {"type":"node","id":4,"lat":-26.085,"lon":28.26,"tags":{"amenity":"pharmacy","name":"Not asked for"}},
          {"type":"node","id":5,"lat":-26.085,"lon":28.27},
          {"type":"relation","id":6,"tags":{"amenity":"veterinary","name":"No position"}},
          {"type":"node","id":7,"lat":-25.5,"lon":28.26,"tags":{"amenity":"veterinary","name":"Outside every requested tile"}}
        ]}
        """;

    [Fact]
    public async Task Area_RoundsBeforeTheProviderAndTheCache_AndKeepsItsKeyAndPayload()
    {
        var osm = new OpenStreetMapStub(_ => OpenStreetMapStub.Json(ReversePlace));
        await using var host = fixture.Host(osm);
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();

        var area = await geo.DescribeAreaAsync(-26.0884911, 28.2275133);

        Assert.Equal(new AreaDto(-26.088, 28.228, "Kempton Park, Ekurhuleni, Gauteng, South Africa", "Kempton Park", "Gauteng", "ZA", AreaPrecision.Area), area);
        var request = Assert.Single(osm.Requests);
        Assert.Equal("GET https://nominatim.test/reverse?format=jsonv2&addressdetails=1&zoom=10&lat=-26.088&lon=28.228", request.Line);
        Assert.Equal("CompanyHQ-Tests/1.0 (tests@example.test)", request.UserAgent);
        Assert.Equal("en", request.AcceptLanguage);

        // The entry an earlier release wrote: same key, same JSON, the area TTL.
        const string key = "companyhq:geo:area:v2:10:en:-26.088:28.228";
        Assert.Equal(
            """{"latitude":-26.088,"longitude":28.228,"displayName":"Kempton Park, Ekurhuleni, Gauteng, South Africa","locality":"Kempton Park","region":"Gauteng","countryCode":"ZA","precision":"Area"}""",
            (string?)await fixture.Raw.StringGetAsync(key));
        Assert.InRange((await fixture.Raw.KeyTimeToLiveAsync(key))!.Value.TotalHours, 23.9, 24);

        // Anyone else in the same ~110 m cell is answered from that entry without a provider call.
        Assert.Equal(area, await geo.DescribeAreaAsync(-26.08751, 28.22849));
        Assert.Single(osm.Requests);
        Assert.DoesNotContain(osm.Requests, sent => sent.Line.Contains("0884911") || sent.Line.Contains("2275133"));
    }

    [Fact]
    public async Task Area_ReturnsNullAndCachesNothing_WhenTheProviderRefusesOrAnswersGarbage()
    {
        var responses = new Queue<Func<HttpResponseMessage>>([
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            () => OpenStreetMapStub.Json("<html>rate limited</html>"),
            () => throw new HttpRequestException("connection reset"),
            () => OpenStreetMapStub.Json("""{"address":{"village":"Little Place","state":"Free State"}}"""),
        ]);
        var osm = new OpenStreetMapStub(_ => responses.Dequeue()());
        await using var host = fixture.Host(osm);
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();

        Assert.Null(await geo.DescribeAreaAsync(10.0004, 20.0004));
        Assert.Null(await geo.DescribeAreaAsync(10.0004, 20.0004));
        Assert.Null(await geo.DescribeAreaAsync(10.0004, 20.0004));
        Assert.False(await fixture.Raw.KeyExistsAsync("companyhq:geo:area:v2:10:en:10:20"));

        // No retry of a failed request, and a place with no display name falls back to its locality.
        Assert.Equal(new AreaDto(10, 20, "Little Place", "Little Place", "Free State", null, AreaPrecision.Area), await geo.DescribeAreaAsync(10.0004, 20.0004));
        Assert.Equal(4, osm.Requests.Count);
    }

    [Fact]
    public async Task AreaSearch_ClampsTheLimit_DropsUnusablePlaces_AndCachesOnlyRealResults()
    {
        var osm = new OpenStreetMapStub(request => OpenStreetMapStub.Json(request.Line.Contains("q=Atlantis") ? "[]" : SearchPlaces));
        await using var host = fixture.Host(osm);
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();

        var areas = await geo.SearchAreasAsync("  Kempton Park ", 50);

        Assert.Equal(
        [
            new AreaDto(-26.1, 28.234, "Kempton Park, Gauteng, South Africa", "Kempton Park", "Gauteng", "ZA", AreaPrecision.Area),
            new AreaDto(51.507, -0.128, "Kempton", "Kempton", "England", "GB", AreaPrecision.Area),
            new AreaDto(1.001, 2.001, "Unknown area", null, null, null, AreaPrecision.Area),
        ], areas);
        Assert.Equal("GET https://nominatim.test/search?format=jsonv2&addressdetails=1&limit=20&q=Kempton%20Park", Assert.Single(osm.Requests).Line);
        Assert.StartsWith("""[{"latitude":-26.1,"longitude":28.234,"displayName":"Kempton Park, Gauteng, South Africa",""",
            (string?)await fixture.Raw.StringGetAsync("companyhq:geo:search:v2:en:20:kempton park"));

        // Case and padding share the entry; a different limit is a different one.
        Assert.Equal(areas, await geo.SearchAreasAsync("KEMPTON PARK", 20));
        Assert.Single(osm.Requests);
        await geo.SearchAreasAsync("Kempton Park", 0);
        Assert.EndsWith("limit=1&q=Kempton%20Park", osm.Requests.Last().Line);

        // Nothing found is not cached, and blank text never reaches the provider.
        Assert.Empty(await geo.SearchAreasAsync("Atlantis"));
        Assert.Empty(await geo.SearchAreasAsync("Atlantis"));
        Assert.Equal(4, osm.Requests.Count);
        Assert.Empty(await geo.SearchAreasAsync("   "));
        Assert.Equal(4, osm.Requests.Count);
        Assert.False(await fixture.Raw.KeyExistsAsync("companyhq:geo:search:v2:en:10:atlantis"));
    }

    [Fact]
    public async Task NearbyPlaces_FillMissingTilesWithOneQuery_AndCacheEveryTileIncludingEmptyOnes()
    {
        var osm = new OpenStreetMapStub(_ => OpenStreetMapStub.Json(NearbyElements));
        await using var host = fixture.Host(osm, kinds: ["veterinary", "animal_shelter"]);
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();

        var result = await geo.ListNearbyPlacesAsync(TwoTiles);

        Assert.False(result.Incomplete);
        Assert.Equal([LeftTile, RightTile], result.Tiles);
        Assert.Equal(
        [
            // The node and its duplicate collapse to one pin; one phone number, the first.
            new NearbyPlaceDto(-26.09, 28.25, "Aston Vet", "veterinary", "12 Monument Road, Aston Manor, Kempton Park, 1619", "+27 11 000 0000"),
            // A way is placed at its centre, an unnamed place is labelled by its kind, addr:full wins.
            new NearbyPlaceDto(-26.08, 28.34, "Animal shelter", "animal_shelter", "4 Kennel Lane, Bredell", "011 222 3333"),
        ], result.Places);

        var request = Assert.Single(osm.Requests);
        var bounds = MapTile.Enclose(MapTile.Cover(TwoTiles.Rounded())).Bounds;
        Assert.Equal("POST https://overpass.test/api/interpreter", request.Line);
        Assert.Equal("CompanyHQ-Tests/1.0 (tests@example.test)", request.UserAgent);
        Assert.StartsWith("data=", request.Body);
        Assert.Equal(
            string.Create(CultureInfo.InvariantCulture,
                $"[out:json][timeout:25];nwr[\"amenity\"~\"^(veterinary|animal_shelter)$\"]({bounds.South},{bounds.West},{bounds.North},{bounds.East});out center;"),
            WebUtility.UrlDecode(request.Body!["data=".Length..]));

        // One entry per tile under the versioned, kind-qualified key, stored for the hard TTL.
        const string left = "companyhq:geo:nearby-tile:v3:animal_shelter+veterinary:12:2369:2355";
        const string right = "companyhq:geo:nearby-tile:v3:animal_shelter+veterinary:12:2370:2355";
        var stored = (string?)await fixture.Raw.StringGetAsync(left);
        Assert.StartsWith("""{"fetchedAt":"20""", stored);
        Assert.EndsWith(""","places":[{"latitude":-26.09,"longitude":28.25,"name":"Aston Vet","kind":"veterinary","address":"12 Monument Road, Aston Manor, Kempton Park, 1619","phone":"\u002B27 11 000 0000"}]}""", stored);
        Assert.InRange((await fixture.Raw.KeyTimeToLiveAsync(right))!.Value.TotalHours, 71.9, 72);

        // Warm: the same viewport, and a pan inside the same tiles, cost no provider call.
        Assert.Equal(result.Places, (await geo.ListNearbyPlacesAsync(TwoTiles)).Places);
        Assert.Equal(result.Places, (await geo.ListNearbyPlacesAsync(new(-26.11, 28.22, -26.05, 28.38))).Places);
        Assert.Single(osm.Requests);
    }

    [Fact]
    public async Task NearbyPlaces_TellAnEmptyAreaFromAnOutage_AndServeWhatIsCachedDuringOne()
    {
        var available = true;
        var osm = new OpenStreetMapStub(_ => available
            ? OpenStreetMapStub.Json("""{"elements":[]}""")
            : new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await using var host = fixture.Host(osm, kinds: ["fuel"]);
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();
        var controller = new GeoController(geo);
        var leftOnly = new GeoViewport(-26.10, 28.23, -26.06, 28.29);

        // An area with nothing in it is a real, cached answer.
        Assert.Equal(new NearbyPlacesResultDto([], [LeftTile], false), await geo.ListNearbyPlacesAsync(leftOnly), NearbyComparer.Instance);
        Assert.EndsWith(""","places":[]}""", (string?)await fixture.Raw.StringGetAsync("companyhq:geo:nearby-tile:v3:fuel:12:2369:2355"));
        Assert.IsType<OkObjectResult>((await controller.NearbyPlaces(-26.10, 28.23, -26.06, 28.29)).Result);

        // The query scanned the whole enclosing rectangle, whose edges touch the next column and
        // row, so those tiles were answered by it too and are cached as empty.
        Assert.True(await fixture.Raw.KeyExistsAsync("companyhq:geo:nearby-tile:v3:fuel:12:2370:2355"));

        // An outage is never written, and what is already held is still served, flagged incomplete.
        available = false;
        var partial = await geo.ListNearbyPlacesAsync(new(-26.10, 28.23, -26.06, 28.55));
        Assert.Equal(new NearbyPlacesResultDto([], [LeftTile, RightTile], true), partial, NearbyComparer.Instance);
        Assert.False(await fixture.Raw.KeyExistsAsync("companyhq:geo:nearby-tile:v3:fuel:12:2372:2355"));

        // Empty and incomplete is the one case worth a 503; the failed tile is asked for again.
        var unavailable = Assert.IsType<ObjectResult>((await controller.NearbyPlaces(-26.10, 28.50, -26.06, 28.55)).Result);
        Assert.Equal((503, "Nearby places are unavailable right now."), (unavailable.StatusCode, unavailable.Value));
        available = true;
        Assert.Equal(["12/2372/2355"], (await geo.ListNearbyPlacesAsync(new(-26.10, 28.50, -26.06, 28.55))).Tiles);
        Assert.Equal(4, osm.Requests.Count);
    }

    [Fact]
    public async Task NearbyPlaces_ServeAStaleTileImmediately_AndRefreshItBehindTheResponse()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var osm = new OpenStreetMapStub(_ =>
        {
            release.Task.Wait(TimeSpan.FromSeconds(10));
            return OpenStreetMapStub.Json("""{"elements":[{"type":"node","lat":-26.09,"lon":28.25,"tags":{"amenity":"bank","name":"Fresh Bank"}}]}""");
        });
        await using var host = fixture.Host(osm, kinds: ["bank"]);
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();
        const string key = "companyhq:geo:nearby-tile:v3:bank:12:2369:2355";
        var fetchedAt = DateTimeOffset.UtcNow.AddHours(-7).ToString("O", CultureInfo.InvariantCulture);
        // Past the 6 h soft TTL, well inside the 72 h hard one: exactly what an older release left behind.
        await fixture.Raw.StringSetAsync(key,
            $$"""{"fetchedAt":"{{fetchedAt}}","places":[{"latitude":-26.09,"longitude":28.25,"name":"Old Bank","kind":"bank"}]}""",
            TimeSpan.FromHours(65));

        var served = await geo.ListNearbyPlacesAsync(new(-26.10, 28.23, -26.06, 28.29));

        Assert.Equal("Old Bank", Assert.Single(served.Places).Name);
        Assert.False(served.Incomplete);
        release.SetResult();
        await Eventually(async () => ((string?)await fixture.Raw.StringGetAsync(key))!.Contains("Fresh Bank"));
        Assert.Single(osm.Requests);
        Assert.Equal("Fresh Bank", Assert.Single((await geo.ListNearbyPlacesAsync(new(-26.10, 28.23, -26.06, 28.29))).Places).Name);
        Assert.Single(osm.Requests);
    }

    [Fact]
    public async Task Controller_KeepsItsValidationAndStatusCodes_AndNoKindsMeansNoProviderCall()
    {
        var osm = new OpenStreetMapStub(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        await using var host = fixture.Host(osm);
        await using var scope = host.CreateAsyncScope();
        var controller = new GeoController(scope.ServiceProvider.GetRequiredService<IGeoService>());

        Assert.Equal("latitude must be between -90 and 90.", Assert.IsType<BadRequestObjectResult>((await controller.Area(95, 28)).Result).Value);
        Assert.Equal("longitude must be between -180 and 180.", Assert.IsType<BadRequestObjectResult>((await controller.Area(-26, double.NaN)).Result).Value);
        Assert.IsType<NotFoundResult>((await controller.Area(-33.9, 18.4)).Result);
        Assert.Equal("search is required.", Assert.IsType<BadRequestObjectResult>((await controller.Areas(" ")).Result).Value);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<AreaDto>>(Assert.IsType<OkObjectResult>((await controller.Areas("Cape Town")).Result).Value));
        Assert.Equal(2, osm.Requests.Count);

        Assert.Equal("south must be less than north.", Assert.IsType<BadRequestObjectResult>((await controller.NearbyPlaces(-26.06, 28.18, -26.15, 28.29)).Result).Value);
        Assert.StartsWith("The viewport covers 30 map tiles; the maximum is 24.",
            (string)Assert.IsType<BadRequestObjectResult>((await controller.NearbyPlaces(-26.35, 28.10, -25.90, 28.45)).Result).Value!);
        // With no configured kinds the search is off: complete, empty, and silent.
        var off = Assert.IsType<NearbyPlacesResultDto>(Assert.IsType<OkObjectResult>((await controller.NearbyPlaces(-26.10, 28.23, -26.06, 28.35)).Result).Value);
        Assert.Equal(new NearbyPlacesResultDto([], [], false), off, NearbyComparer.Instance);
        Assert.Equal(2, osm.Requests.Count);
    }

    [Fact]
    public void Registration_RejectsAnUnidentifiedDeploymentAndUnsafeKinds_AtStartup()
    {
        var osm = new OpenStreetMapStub(_ => new HttpResponseMessage(HttpStatusCode.OK));
        Assert.Contains("Geo:UserAgent", Assert.Throws<InvalidOperationException>(() => fixture.Host(osm, settings: ("Geo:UserAgent", "changeme"))).Message);
        Assert.Contains("Geo:BaseAddress", Assert.Throws<InvalidOperationException>(() => fixture.Host(osm, settings: ("Geo:BaseAddress", "nominatim"))).Message);
        Assert.Contains("Geo:NearbyPlaceKinds", Assert.Throws<InvalidOperationException>(() => fixture.Host(osm, kinds: ["vet)|(.*"])).Message);
        Assert.Contains("Geo:NearbyTileHardTtlHours", Assert.Throws<InvalidOperationException>(() => fixture.Host(osm, settings: ("Geo:NearbyTileHardTtlHours", "1"))).Message);
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }

        Assert.Fail("The background refresh did not complete.");
    }

    private sealed class NearbyComparer : IEqualityComparer<NearbyPlacesResultDto>
    {
        public static readonly NearbyComparer Instance = new();
        public bool Equals(NearbyPlacesResultDto? x, NearbyPlacesResultDto? y) =>
            x!.Incomplete == y!.Incomplete && x.Places.SequenceEqual(y.Places) && x.Tiles.SequenceEqual(y.Tiles);
        public int GetHashCode(NearbyPlacesResultDto value) => value.Tiles.Count;
    }
}

/// <summary>
/// Geo's own policy over a scripted provider, with no HTTP at all: what Geo asks a provider for,
/// and what it refuses to keep from one that answers with more than the question.
/// </summary>
public sealed class GeoProviderSeamTests(GeoFixture fixture) : IClassFixture<GeoFixture>
{
    private sealed class ScriptedProvider : IGeoProvider
    {
        public List<object> Asked { get; } = [];
        public GeoProviderArea? Area { get; set; }
        public IReadOnlyList<GeoProviderArea> Areas { get; set; } = [];
        public IReadOnlyList<GeoProviderPlace>? Places { get; set; } = [];

        public Task<GeoProviderArea?> FindAreaAsync(GeoAreaLookup lookup, CancellationToken cancellationToken)
        { Asked.Add(lookup); return Task.FromResult(Area); }

        public Task<IReadOnlyList<GeoProviderArea>> SearchAreasAsync(GeoAreaSearch search, CancellationToken cancellationToken)
        { Asked.Add(search); return Task.FromResult(Areas); }

        public Task<IReadOnlyList<GeoProviderPlace>?> FindPlacesAsync(GeoPlaceSearch search, CancellationToken cancellationToken)
        { Asked.Add(search); return Task.FromResult(Places); }
    }

    [Fact]
    public async Task Geo_AsksTheProviderOnlyForRoundedConfiguredQuestions()
    {
        var provider = new ScriptedProvider
        {
            Area = new(-33.9249123, 18.4241987, null, null, "Western Cape", "za"),
            Areas = [new(48.85661, 2.35222, "Paris", "Paris", "Île-de-France", "fr"), new(null, 2.1, "No latitude", null, null, null)],
        };
        await using var host = fixture.Host(provider, ["cafe", "atm"], ("Geo:AreaZoom", "12"), ("Geo:Language", "fr"), ("Cache:KeyPrefix", "seam-a:"));
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();

        // The answer echoes the rounded pair that was asked about, never the provider's own position.
        Assert.Equal(new AreaDto(-33.925, 18.424, "Western Cape", null, "Western Cape", "ZA", AreaPrecision.Area), await geo.DescribeAreaAsync(-33.9249123, 18.4241987));
        Assert.Equal([new AreaDto(48.857, 2.352, "Paris", "Paris", "Île-de-France", "FR", AreaPrecision.Area)], await geo.SearchAreasAsync(" Paris ", 99));
        await geo.ListNearbyPlacesAsync(new(48.85, 2.34, 48.86, 2.36));

        Assert.Equal(new GeoAreaLookup(-33.925, 18.424, 12, "fr"), provider.Asked[0]);
        Assert.Equal(new GeoAreaSearch("Paris", 20, "fr"), provider.Asked[1]);
        var places = Assert.IsType<GeoPlaceSearch>(provider.Asked[2]);
        Assert.Equal(["cafe", "atm"], places.Kinds);
        // A tile-aligned box: Geo, not the caller, chooses what a provider scans.
        Assert.Equal(MapTile.Enclose(MapTile.Cover(new GeoViewport(48.85, 2.34, 48.86, 2.36))).Bounds, places.Bounds);
        Assert.True(await fixture.Raw.KeyExistsAsync("seam-a:geo:area:v2:12:fr:-33.925:18.424"));
        Assert.True(await fixture.Raw.KeyExistsAsync("seam-a:geo:search:v2:fr:20:paris"));
    }

    [Fact]
    public async Task Geo_KeepsOnlyPlacesItAskedFor_AndNeverCachesAProviderFailure()
    {
        var provider = new ScriptedProvider
        {
            Places =
            [
                new(48.855, 2.35, "cafe", null, null, null),
                new(48.855, 2.35, "cafe", null, "duplicate", null),
                new(48.856, 2.351, "casino", "Not asked for", null, null),
                new(91, 2.35, "cafe", "Off the map", null, null),
                new(double.NaN, 2.35, "cafe", "Nowhere", null, null),
                new(10, 10, "cafe", "Outside the scanned box", null, null),
                new(48.857, 2.352, "atm", "Guichet", "1 Rue de Rivoli", "+33 1 00 00 00 00"),
            ],
        };
        await using var host = fixture.Host(provider, ["cafe", "atm"], ("Cache:KeyPrefix", "seam-b:"));
        await using var scope = host.CreateAsyncScope();
        var geo = scope.ServiceProvider.GetRequiredService<IGeoService>();
        var viewport = new GeoViewport(48.85, 2.34, 48.86, 2.36);

        var result = await geo.ListNearbyPlacesAsync(viewport);

        Assert.False(result.Incomplete);
        Assert.Equal(
        [
            new NearbyPlaceDto(48.855, 2.35, "Cafe", "cafe"),
            new NearbyPlaceDto(48.857, 2.352, "Guichet", "atm", "1 Rue de Rivoli", "+33 1 00 00 00 00"),
        ], result.Places);

        // A failed search is reported, not stored: the very next request asks again and succeeds.
        await using var failing = fixture.Host(provider, ["cafe", "atm"], ("Cache:KeyPrefix", "seam-c:"));
        await using var failingScope = failing.CreateAsyncScope();
        var uncached = failingScope.ServiceProvider.GetRequiredService<IGeoService>();
        provider.Places = null;
        Assert.Equal((0, true), ((await uncached.ListNearbyPlacesAsync(viewport)).Places.Count, (await uncached.ListNearbyPlacesAsync(viewport)).Incomplete));
        provider.Places = [];
        var recovered = await uncached.ListNearbyPlacesAsync(viewport);
        Assert.Equal((false, 1), (recovered.Incomplete, recovered.Tiles.Count));
        Assert.Equal(4, provider.Asked.Count);
    }

    [Fact]
    public async Task Geo_WithoutAProviderAdapter_CannotBeResolved()
    {
        await using var host = fixture.Host(provider: null);
        await using var scope = host.CreateAsyncScope();
        Assert.Contains(nameof(IGeoProvider), Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IGeoService>()).Message);
    }
}

/// <summary>
/// The OpenStreetMap adapter on its own, behind Geo's port: pacing, cancellation, and what it will
/// not send to a public endpoint. No request leaves the process.
/// </summary>
public sealed class OpenStreetMapProviderTests
{
    private static ServiceProvider Compose(OpenStreetMapStub osm, params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>
        {
            ["Geo:BaseAddress"] = "https://nominatim.test/",
            ["Geo:OverpassBaseAddress"] = "https://overpass.test/",
            ["Geo:UserAgent"] = "CompanyHQ-Tests/1.0 (tests@example.test)",
            ["Geo:MinimumRequestInterval"] = "00:00:00",
            ["Geo:OverpassMinimumRequestInterval"] = "00:00:00",
        };
        foreach (var (key, value) in settings) values[key] = value;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddOpenStreetMap(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = osm));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public async Task Requests_AreSerialisedAndSpacedPerEndpoint_SoABurstNeverReachesThePublicInstance()
    {
        var inFlight = 0;
        var overlapped = false;
        var starts = new ConcurrentQueue<long>();
        var osm = new OpenStreetMapStub(_ =>
        {
            starts.Enqueue(Environment.TickCount64);
            if (Interlocked.Increment(ref inFlight) > 1) overlapped = true;
            Thread.Sleep(20);
            Interlocked.Decrement(ref inFlight);
            return OpenStreetMapStub.Json("[]");
        });
        await using var host = Compose(osm, ("Geo:MinimumRequestInterval", "00:00:00.200"));
        await using var scope = host.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IGeoProvider>();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(index => provider.SearchAreasAsync(new($"place {index}", 5, "en"), default)));

        Assert.False(overlapped);
        var ordered = starts.Order().ToArray();
        Assert.Equal(4, ordered.Length);
        // Timer granularity allows a few milliseconds either way; a burst would show as near zero.
        Assert.All(ordered.Zip(ordered.Skip(1), (previous, next) => next - previous), gap => Assert.InRange(gap, 170, 5_000));
    }

    [Fact]
    public async Task ACallersCancellationPropagates_ButATransportTimeoutIsAnOrdinaryFailure()
    {
        var osm = new OpenStreetMapStub(_ => throw new TaskCanceledException("the HTTP client timed out"));
        await using var host = Compose(osm);
        await using var scope = host.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IGeoProvider>();
        var box = new GeoViewport(-26.1, 28.2, -26.0, 28.3);

        Assert.Null(await provider.FindAreaAsync(new(-26.088, 28.228, 10, "en"), default));
        Assert.Empty(await provider.SearchAreasAsync(new("Kempton Park", 5, "en"), default));
        Assert.Null(await provider.FindPlacesAsync(new(box, ["veterinary"]), default));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.FindAreaAsync(new(-26.088, 28.228, 10, "en"), cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.FindPlacesAsync(new(box, ["veterinary"]), cancelled.Token));
    }

    [Fact]
    public async Task PlaceKindsThatAreNotBareTagValues_NeverReachTheQuery()
    {
        var osm = new OpenStreetMapStub(_ => OpenStreetMapStub.Json("""{"elements":[]}"""));
        await using var host = Compose(osm);
        await using var scope = host.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<IGeoProvider>();
        var box = new GeoViewport(-26.1, 28.2, -26.0, 28.3);

        foreach (var kinds in new string[][] { [], [""], ["Veterinary"], ["vet)$\"];node(1"], ["veterinary", "a|b"] })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => provider.FindPlacesAsync(new(box, kinds), default));
        }

        Assert.Empty(osm.Requests);
        Assert.Empty((await provider.FindPlacesAsync(new(box, ["fire_station", "atm2"]), default))!);
        Assert.Contains("^(fire_station|atm2)$", WebUtility.UrlDecode(Assert.Single(osm.Requests).Body));
    }
}

/// <summary>Answers every outbound OpenStreetMap request from a script and records what was sent.</summary>
public sealed class OpenStreetMapStub(Func<OpenStreetMapStub.Sent, HttpResponseMessage> respond) : HttpMessageHandler
{
    public sealed record Sent(string Line, string? UserAgent, string? AcceptLanguage, string? Body);

    public ConcurrentQueue<Sent> Requests { get; } = new();

    public static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sent = new Sent(
            $"{request.Method} {request.RequestUri!.AbsoluteUri}",
            request.Headers.UserAgent.ToString(),
            request.Headers.AcceptLanguage.ToString(),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        Requests.Enqueue(sent);
        await Task.Yield();
        return respond(sent);
    }
}

public sealed class GeoFixture : IAsyncLifetime
{
    private readonly IContainer redis = new ContainerBuilder().WithImage("redis:7.4-alpine").WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
    private ConnectionMultiplexer? connection;

    public IDatabase Raw => connection!.GetDatabase();

    public async Task InitializeAsync()
    {
        await redis.StartAsync();
        connection = await ConnectionMultiplexer.ConnectAsync($"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}");
    }

    /// <summary>Composes Geo, its provider and Cache as a host does, with outbound HTTP scripted.</summary>
    public ServiceProvider Host(OpenStreetMapStub osm, string[]? kinds = null, params (string Key, string Value)[] settings) =>
        Compose(kinds, settings, (services, configuration) =>
        {
            services.AddOpenStreetMap(configuration);
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
                options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = osm));
        });

    /// <summary>Composes Geo over a given provider, or over none, with no HTTP client at all.</summary>
    public ServiceProvider Host(IGeoProvider? provider, string[]? kinds = null, params (string Key, string Value)[] settings) =>
        Compose(kinds, settings, (services, _) =>
        {
            if (provider is not null) services.AddSingleton(provider);
        });

    private ServiceProvider Compose(string[]? kinds, (string Key, string Value)[] settings, Action<IServiceCollection, IConfiguration> addProvider)
    {
        var values = new Dictionary<string, string?>
        {
            ["Cache:ConnectionString"] = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}",
            ["Geo:BaseAddress"] = "https://nominatim.test/",
            ["Geo:OverpassBaseAddress"] = "https://overpass.test/",
            ["Geo:UserAgent"] = "CompanyHQ-Tests/1.0 (tests@example.test)",
            // The public instances' pacing is covered on its own; here it would only slow the suite.
            ["Geo:MinimumRequestInterval"] = "00:00:00",
            ["Geo:OverpassMinimumRequestInterval"] = "00:00:00",
        };
        foreach (var (kind, index) in (kinds ?? []).Select((kind, index) => (kind, index))) values[$"Geo:NearbyPlaceKinds:{index}"] = kind;
        foreach (var (key, value) in settings) values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddRedisConnection(configuration);
        services.AddCache(configuration);
        services.AddGeo(configuration);
        addProvider(services, configuration);
        // MVC needs a web host to validate on build; scope validation still applies.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        await redis.DisposeAsync();
    }
}
