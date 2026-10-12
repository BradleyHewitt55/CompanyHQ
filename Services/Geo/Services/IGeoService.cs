using CompanyHQ.Services.Geo.Contracts.Dtos;
using CompanyHQ.Services.Geo.Contracts.Models;

namespace CompanyHQ.Services.Geo.Services;

/// <summary>
/// Turns coordinates or search text into a named area. It takes a position as a parameter and
/// returns an answer — nothing about a caller's whereabouts is stored, indexed, or logged.
/// </summary>
public interface IGeoService
{
    /// <summary>
    /// Names the area around a coordinate. The coordinate is rounded to
    /// <see cref="GeoService.CoordinateDecimals"/> decimals before it goes anywhere. Null when
    /// the provider has no answer or could not be reached.
    /// </summary>
    Task<AreaDto?> DescribeAreaAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Free-text area search for the manual picker a user gets when they decline location. Empty
    /// when nothing matched or the lookup failed.
    /// </summary>
    Task<IReadOnlyList<AreaDto>> SearchAreasAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds places of the configured <c>Geo:NearbyPlaceKinds</c> inside a viewport, tile by tile. Always
    /// returns a result: <c>Places</c> is everything that could be resolved and <c>Incomplete</c>
    /// says whether a tile was neither cached nor fetchable.
    /// </summary>
    /// <remarks>
    /// Empty and unavailable have to stay distinguishable — folding an outage into an empty list
    /// makes the map tell a person there is nothing near them, which is the worst thing this feature
    /// can get wrong. Anything cached is still served during an outage; only an empty <i>and</i>
    /// incomplete result is worth failing the request over.
    /// </remarks>
    Task<NearbyPlacesResultDto> ListNearbyPlacesAsync(
        GeoViewport viewport,
        CancellationToken cancellationToken = default);
}
