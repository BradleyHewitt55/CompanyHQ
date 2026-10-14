using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using CompanyHQ.Cache.Contracts.Models;
using CompanyHQ.Cache.Contracts.Ports;
using CompanyHQ.Cache.Extensions;
using CompanyHQ.Cache.Services;
using StackExchange.Redis;
using Xunit;

namespace CompanyHQ.Tests;

/// <summary>
/// What every Cache consumer relies on, pinned against a real Redis: the key namespace, the JSON
/// payload, TTLs, cached negatives, prefix removal and the fail-silently policy. Entries are read
/// and written raw as well, so a release can never require flushing Redis.
/// </summary>
public sealed class CacheTests(CacheFixture fixture) : IClassFixture<CacheFixture>
{
    private sealed record Sample(Guid Id, string Title, string? Subtitle = null, int Views = 0);

    [Fact]
    public async Task Entries_KeepPrefixJsonShapeAndTtl_AndReadWhatAnEarlierReleaseWrote()
    {
        await using var host = fixture.Host();
        var cache = host.GetRequiredService<ICacheService>();
        var raw = fixture.Raw;
        var id = Guid.Parse("0198c3a1-0000-7000-8000-000000000001");

        await cache.SetAsync($"sample:{id}:client", new Sample(id, "Launch", null, 3), TimeSpan.FromSeconds(90));
        // Web defaults: camelCase, and a null is left out rather than written.
        Assert.Equal($$"""{"id":"{{id}}","title":"Launch","views":3}""", (string?)await raw.StringGetAsync($"companyhq:sample:{id}:client"));
        Assert.InRange((await raw.KeyTimeToLiveAsync($"companyhq:sample:{id}:client"))!.Value.TotalSeconds, 80, 90);

        await cache.SetAsync("sample:default-ttl", new Sample(id, "Default"));
        Assert.InRange((await raw.KeyTimeToLiveAsync("companyhq:sample:default-ttl"))!.Value.TotalSeconds, 290, 300);

        // An entry persisted before an upgrade is still a hit, extra and missing fields included.
        await raw.StringSetAsync("companyhq:sample:legacy", $$"""{"id":"{{id}}","title":"Legacy","retired":true}""", TimeSpan.FromMinutes(5));
        Assert.Equal(new Sample(id, "Legacy"), await cache.GetAsync<Sample>("sample:legacy"));
        Assert.True(await cache.ExistsAsync("sample:legacy"));
        Assert.True(await cache.RemoveAsync("sample:legacy"));
        Assert.False(await cache.RemoveAsync("sample:legacy"));
        Assert.False(await cache.ExistsAsync("sample:legacy"));
        Assert.Null(await cache.GetAsync<Sample>("sample:legacy"));
    }

    [Fact]
    public async Task GetOrSet_CachesNegativesAndDefaults_WithoutRerunningTheFactory()
    {
        await using var host = fixture.Host();
        var cache = host.GetRequiredService<ICacheService>();
        var runs = 0;

        for (var read = 0; read < 3; read++)
        {
            Assert.Null(await cache.GetOrSetAsync<Sample?>("negative:missing", _ => { runs++; return Task.FromResult<Sample?>(null); }, TimeSpan.FromMinutes(1)));
            Assert.Equal(0, await cache.GetOrSetAsync("negative:zero", _ => { runs++; return Task.FromResult(0); }, TimeSpan.FromMinutes(1)));
            Assert.False(await cache.GetOrSetAsync("negative:false", _ => { runs++; return Task.FromResult(false); }, TimeSpan.FromMinutes(1)));
        }

        Assert.Equal(3, runs);
        Assert.Equal("null", (string?)await fixture.Raw.StringGetAsync("companyhq:negative:missing"));
        Assert.InRange((await fixture.Raw.KeyTimeToLiveAsync("companyhq:negative:missing"))!.Value.TotalSeconds, 50, 60);
    }

    [Fact]
    public async Task RemoveByPrefix_IsAGlobPrefixBoundedByTheKeyPrefix_AcrossScanPages()
    {
        // A page of two forces several SCAN round trips and delete batches for seven keys.
        await using var host = fixture.Host(("Cache:ScanPageSize", "2"));
        var cache = host.GetRequiredService<ICacheService>();
        var raw = fixture.Raw;

        foreach (var key in new[] { "post:1:client", "post:1:admin", "post:1:en:1", "post:1:en:2", "post:10:client", "post:2:client", "other:1" })
        {
            await cache.SetAsync(key, key, TimeSpan.FromMinutes(5));
        }

        // Web shares the instance; a key outside this service's prefix is never scanned or removed.
        await raw.StringSetAsync("elsewhere:post:1:client", "kept", TimeSpan.FromMinutes(5));

        // "post:1" is a glob prefix, not a path segment: it takes post:10 with it.
        Assert.Equal(5, await cache.RemoveByPrefixAsync("post:1"));
        Assert.False(await cache.ExistsAsync("post:1:client"));
        Assert.False(await cache.ExistsAsync("post:10:client"));
        Assert.True(await cache.ExistsAsync("post:2:client"));
        Assert.True(await cache.ExistsAsync("other:1"));
        Assert.True(await raw.KeyExistsAsync("elsewhere:post:1:client"));

        Assert.Equal(1, await cache.RemoveByPrefixAsync("post:2:"));
        Assert.Equal(0, await cache.RemoveByPrefixAsync("post:"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.RemoveByPrefixAsync(" "));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.RemoveAsync("other:1", new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Increment_CountsAtomicallyInsideOneExpiringWindow()
    {
        await using var host = fixture.Host();
        var cache = host.GetRequiredService<ICacheService>();
        var key = "counter:" + Guid.NewGuid().ToString("N");

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.IncrementAsync(key, TimeSpan.FromSeconds(30))));

        Assert.All(results, result => Assert.True(result.IsAvailable));
        Assert.Equal(Enumerable.Range(1, 20).Select(count => (long)count), results.Select(result => result.Count).Order());
        Assert.All(results, result => Assert.InRange(result.RetryAfter.TotalSeconds, 1, 30));
        // The raw value is the bare integer INCR keeps, and the window is set once, by the first hit.
        Assert.Equal("20", (string?)await fixture.Raw.StringGetAsync("companyhq:" + key));
        Assert.InRange((await fixture.Raw.KeyTimeToLiveAsync("companyhq:" + key))!.Value.TotalSeconds, 1, 30);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.IncrementAsync(key, TimeSpan.Zero));
    }

    [Fact]
    public async Task UnreachableRedis_DegradesToMisses_ButValidationAndCountersStillReport()
    {
        await using var host = fixture.Host(("Cache:ConnectionString", CacheFixture.Unreachable));
        var cache = host.GetRequiredService<ICacheService>();
        var runs = 0;

        Assert.Null(await cache.GetAsync<Sample>("down:1"));
        await cache.SetAsync("down:1", new Sample(Guid.NewGuid(), "Lost"));
        Assert.Equal(7, await cache.GetOrSetAsync("down:2", _ => { runs++; return Task.FromResult(7); }));
        Assert.Equal(7, await cache.GetOrSetAsync("down:2", _ => { runs++; return Task.FromResult(7); }));
        Assert.Equal(2, runs);
        Assert.False(await cache.RemoveAsync("down:1"));
        Assert.False(await cache.ExistsAsync("down:1"));
        Assert.Equal(0, await cache.RemoveByPrefixAsync("down:"));

        // A caller bug is not an outage, and a security counter must be able to fail closed.
        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.GetAsync<Sample>(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.SetAsync(null!, 1));
        Assert.Equal(CacheCounterResult.Unavailable, await cache.IncrementAsync("down:counter", TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task FailSilentlyOff_SurfacesTheRedisFailure_AndUnreadablePayloadsAreMissesOnlyWhenOn()
    {
        await using (var strict = fixture.Host(("Cache:ConnectionString", CacheFixture.Unreachable), ("Cache:FailSilently", "false")))
        {
            var cache = strict.GetRequiredService<ICacheService>();
            var failure = await Record.ExceptionAsync(() => cache.GetAsync<Sample>("down:1"));
            Assert.True(failure is RedisException or TimeoutException, failure?.GetType().FullName);
            Assert.Equal(CacheCounterResult.Unavailable, await cache.IncrementAsync("down:counter", TimeSpan.FromSeconds(5)));
        }

        await fixture.Raw.StringSetAsync("companyhq:broken:1", "{not json", TimeSpan.FromMinutes(5));
        await using var silent = fixture.Host();
        Assert.Null(await silent.GetRequiredService<ICacheService>().GetAsync<Sample>("broken:1"));
        await using var loud = fixture.Host(("Cache:FailSilently", "false"));
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => loud.GetRequiredService<ICacheService>().GetAsync<Sample>("broken:1"));
    }

    [Fact]
    public async Task HealthEntry_KeepsItsNameTagsAndDegradedStatus_OverRedis()
    {
        await using var host = fixture.Host();
        var registration = Assert.Single(host.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal(("cache", HealthStatus.Degraded), (registration.Name, registration.FailureStatus));
        Assert.Equal(["cache", "redis"], registration.Tags.Order());
        Assert.Equal(HealthStatus.Healthy, (await host.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Entries["cache"].Status);

        await using var down = fixture.Host(("Cache:ConnectionString", CacheFixture.Unreachable));
        Assert.Equal(HealthStatus.Degraded, (await down.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Entries["cache"].Status);
    }

    [Fact]
    public void InvalidConfiguration_FailsRegistration_NotTheFirstRead()
    {
        foreach (var (key, value) in new[] { ("Cache:ConnectionString", " "), ("Cache:DefaultTtlSeconds", "0"), ("Cache:ScanPageSize", "0") })
        {
            Assert.Throws<InvalidOperationException>(() => fixture.Host((key, value)));
        }
    }
}

public sealed class CacheFixture : IAsyncLifetime
{
    // Nothing listens here; short timeouts keep the outage tests fast.
    public const string Unreachable = "127.0.0.1:1,connectTimeout=300,syncTimeout=300,asyncTimeout=300,connectRetry=0";

    private readonly IContainer redis = new ContainerBuilder().WithImage("redis:7.4-alpine").WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
    private ConnectionMultiplexer? connection;

    public string ConnectionString => $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}";

    /// <summary>A second connection that bypasses Cache, to see exactly what is stored.</summary>
    public IDatabase Raw => connection!.GetDatabase();

    public async Task InitializeAsync()
    {
        await redis.StartAsync();
        connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    /// <summary>Composes Cache with its Redis connection the way a host does.</summary>
    public ServiceProvider Host(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?> { ["Cache:ConnectionString"] = ConnectionString };
        foreach (var (key, value) in settings) values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisConnection(configuration);
        services.AddCache(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public async Task DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        await redis.DisposeAsync();
    }
}

internal static class RedisConnection
{
    /// <summary>The multiplexer a host registers before <c>AddCache</c>, lazy and reconnecting.</summary>
    public static IServiceCollection AddRedisConnection(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redis = ConfigurationOptions.Parse(configuration["Cache:ConnectionString"]!);
            redis.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redis);
        });
}

/// <summary>
/// The Cache-owned policy on its own, over a scripted provider: what Cache hands a provider, and
/// which provider failures it is allowed to turn into a miss.
/// </summary>
public sealed class CacheProviderSeamTests
{
    private sealed class ProviderOutage(string message) : Exception(message);

    private sealed class ScriptedProvider : ICacheProvider
    {
        public List<string> Calls { get; } = [];
        public Dictionary<string, string> Entries { get; } = [];
        public Exception? Failure { get; set; }
        public long[] PrefixBatches { get; set; } = [];
        public int FailAfterBatches { get; set; } = int.MaxValue;
        public CacheProviderCounter Counter { get; set; } = new(1, TimeSpan.FromSeconds(30));

        public string Name => "scripted";

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
            Run($"get {key}", () => Entries.GetValueOrDefault(key));

        public Task SetAsync(string key, string payload, TimeSpan ttl, CancellationToken cancellationToken) =>
            Run($"set {key} {payload} {ttl.TotalSeconds}", () => Entries[key] = payload);

        public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken) => Run($"remove {key}", () => Entries.Remove(key));

        public async IAsyncEnumerable<long> RemoveByPrefixAsync(string prefix, int pageSize,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls.Add($"remove-prefix {prefix} {pageSize}");
            for (var batch = 0; batch < PrefixBatches.Length; batch++)
            {
                await Task.Yield();
                if (batch == FailAfterBatches) throw Failure!;
                yield return PrefixBatches[batch];
            }
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => Run($"exists {key}", () => Entries.ContainsKey(key));

        public Task<CacheProviderCounter> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken) =>
            Run($"increment {key} {window.TotalSeconds}", () => Counter);

        public Task<TimeSpan> PingAsync(CancellationToken cancellationToken) => Run("ping", () => TimeSpan.FromMilliseconds(2));

        public bool IsTransportFailure(Exception exception) => exception is ProviderOutage;

        private Task<T> Run<T>(string call, Func<T> result)
        {
            Calls.Add(call);
            return Failure is null ? Task.FromResult(result()) : Task.FromException<T>(Failure);
        }
    }

    private static (ICacheService Cache, ScriptedProvider Provider, ServiceProvider Services) Compose(params (string Key, string Value)[] settings)
    {
        var provider = new ScriptedProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCache(configuration);
        services.Replace(ServiceDescriptor.Singleton<ICacheProvider>(provider));
        var built = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        return (built.GetRequiredService<ICacheService>(), provider, built);
    }

    [Fact]
    public void WithoutTheHostsRedisConnection_HostValidationFails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCache(new ConfigurationBuilder().Build());
        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
    }

    [Fact]
    public async Task Cache_QualifiesKeysSerialisesAndAppliesTheTtl_BeforeTheProviderSeesThem()
    {
        var (cache, provider, services) = Compose(("Cache:KeyPrefix", "acme:"), ("Cache:DefaultTtlSeconds", "45"), ("Cache:ScanPageSize", "7"));
        await using var _ = services;
        provider.PrefixBatches = [7, 2];

        await cache.SetAsync("order:1:client", new { Total = 12.5m, Note = (string?)null });
        await cache.SetAsync("order:1:admin", true, TimeSpan.FromSeconds(9));
        Assert.Equal(12.5m, (await cache.GetAsync<Dictionary<string, decimal>>("order:1:client"))!["total"]);
        Assert.True(await cache.ExistsAsync("order:1:admin"));
        Assert.Equal(9, await cache.RemoveByPrefixAsync("order:1:"));
        Assert.True(await cache.RemoveAsync("order:1:client"));
        await cache.IncrementAsync("limit:ip", TimeSpan.FromSeconds(60));

        Assert.Equal(
        [
            """set acme:order:1:client {"total":12.5} 45""",
            "set acme:order:1:admin true 9",
            "get acme:order:1:client",
            "exists acme:order:1:admin",
            "remove-prefix acme:order:1: 7",
            "remove acme:order:1:client",
            "increment acme:limit:ip 60",
        ], provider.Calls);
    }

    [Fact]
    public async Task OnlyTheProvidersOwnTransportFailures_BecomeMisses_AndOnlyWhileFailSilentlyIsOn()
    {
        var (cache, provider, services) = Compose();
        await using var _ = services;

        provider.Failure = new ProviderOutage("connection lost");
        Assert.Null(await cache.GetAsync<string>("key"));
        await cache.SetAsync("key", "value");
        Assert.False(await cache.RemoveAsync("key"));
        Assert.False(await cache.ExistsAsync("key"));
        Assert.Equal("made", await cache.GetOrSetAsync("key", _ => Task.FromResult("made")));

        // Anything the provider does not claim is a bug, and stays loud.
        provider.Failure = new InvalidOperationException("not an outage");
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<string>("key"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SetAsync("key", "value"));
        // The counter reports rather than throws, whatever went wrong, so its caller can fail closed.
        Assert.Equal(CacheCounterResult.Unavailable, await cache.IncrementAsync("key", TimeSpan.FromSeconds(1)));
        provider.Failure = new TimeoutException();
        Assert.Null(await cache.GetAsync<string>("key"));

        var (strict, strictProvider, strictServices) = Compose(("Cache:FailSilently", "false"));
        await using var __ = strictServices;
        strictProvider.Failure = new ProviderOutage("connection lost");
        // Unwrapped: the caller sees the exception exactly as the provider threw it.
        Assert.Same(strictProvider.Failure, await Assert.ThrowsAsync<ProviderOutage>(() => strict.GetAsync<string>("key")));
        Assert.Equal(CacheCounterResult.Unavailable, await strict.IncrementAsync("key", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task PrefixRemoval_KeepsThePartialCount_AndTheCounterWindowNeverReportsLessThanASecond()
    {
        var (cache, provider, services) = Compose();
        await using var _ = services;
        provider.PrefixBatches = [250, 250, 40];
        provider.FailAfterBatches = 2;
        provider.Failure = new ProviderOutage("dropped mid-scan");
        Assert.Equal(500, await cache.RemoveByPrefixAsync("post:"));

        provider.Failure = null;
        // A window the provider reports as gone (-2) or unset (-1) still tells the caller to wait.
        provider.Counter = new(4, TimeSpan.FromSeconds(-2));
        Assert.Equal(new CacheCounterResult(true, 4, TimeSpan.FromSeconds(1)), await cache.IncrementAsync("key", TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task HealthEntry_IsNamedCache_TaggedByTheProvider_AndDegradedRatherThanUnhealthy()
    {
        var (_, provider, services) = Compose();
        await using var __ = services;
        var registration = Assert.Single(services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal("cache", registration.Name);
        Assert.Equal(["cache", "scripted"], registration.Tags.Order());
        var health = services.GetRequiredService<HealthCheckService>();

        var healthy = (await health.CheckHealthAsync()).Entries["cache"];
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
        Assert.Equal(2d, healthy.Data["latencyMs"]);

        provider.Failure = new ProviderOutage("down");
        Assert.Equal(HealthStatus.Degraded, (await health.CheckHealthAsync()).Entries["cache"].Status);
    }
}
