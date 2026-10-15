using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using CompanyHQ.Geo.Contracts.Models;

namespace CompanyHQ.Geo.Services;

/// <summary>Refreshes cached tiles that are past their soft TTL, out of band.</summary>
/// <remarks>
/// This exists for outage resilience, not freshness. A place rarely moves, so a
/// six-hour-old pin is still correct — what matters is that the map keeps working while the provider is
/// rate-limiting, which it only can if a stale entry is served rather than discarded and refetched
/// on the request path.
/// </remarks>
public sealed class NearbyTileRefresher(IServiceScopeFactory scopes, ILogger<NearbyTileRefresher> logger)
{
    /// <summary>Stops a busy area queueing the same tile once per viewport request.</summary>
    private readonly ConcurrentDictionary<MapTile, byte> inFlight = new();

    /// <summary>Starts a refresh and returns immediately. It never throws and never reports back.</summary>
    public void Queue(IReadOnlyCollection<MapTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var claimed = tiles.Where(tile => inFlight.TryAdd(tile, 0)).ToArray();
        if (claimed.Length == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var stopping = scope.ServiceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? default;
                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                lifetime.CancelAfter(TimeSpan.FromSeconds(60));
                var availability = scope.ServiceProvider.GetService<IServiceAvailability>();
                if (availability is not null) await availability.RequireAsync("geo", true, lifetime.Token);
                await scope.ServiceProvider.GetRequiredService<NearbyTileStore>().FetchAsync(claimed, lifetime.Token);
            }
            catch (Exception exception)
            {
                // Nobody is waiting on this. A failure just means the stale entry keeps being served.
                logger.LogWarning(exception, "Background nearby-tile refresh failed.");
            }
            finally
            {
                foreach (var tile in claimed)
                {
                    inFlight.TryRemove(tile, out _);
                }
            }
        });
    }
}
