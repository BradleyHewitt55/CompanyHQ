using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using CompanyHQ.Services.Geo.Contracts.Models;

namespace CompanyHQ.Services.Geo.Services;

/// <summary>Refreshes cached tiles that are past their soft TTL, out of band.</summary>
/// <remarks>
/// This exists for outage resilience, not freshness. A place rarely moves, so a
/// six-hour-old pin is still correct — what matters is that the map keeps working while Overpass is
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
                // Its own scope, and CancellationToken.None: the request that noticed the staleness
                // has already been answered from cache, so its token is about to be cancelled and
                // would abort every refresh before it started.
                using var scope = scopes.CreateScope();

                await scope.ServiceProvider
                    .GetRequiredService<NearbyTileStore>()
                    .FetchAsync(claimed, CancellationToken.None);
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
