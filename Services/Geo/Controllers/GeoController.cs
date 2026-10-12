using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Services.Geo.Contracts.Dtos;
using CompanyHQ.Services.Geo.Contracts.Models;
using CompanyHQ.Services.Geo.Services;

namespace CompanyHQ.Services.Geo.Controllers;

/// <summary>
/// Area naming and area search on <c>/api/geo</c>. Every endpoint is anonymous: a signed-out user
/// still gets a map, and nothing here is tied to an account.
/// </summary>
[ApiController]
[Route("api/geo")]
[EnableRateLimiting(GeoOptions.InboundRateLimitPolicyName)]
[Produces("application/json")]
public sealed class GeoController(IGeoService geo) : ControllerBase
{
    /// <summary>Names the area around a coordinate. The coordinate is rounded before it is used.</summary>
    [HttpGet("area")]
    [ProducesResponseType<AreaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AreaDto>> Area(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            return BadRequest("latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            return BadRequest("longitude must be between -180 and 180.");
        }

        var area = await geo.DescribeAreaAsync(latitude, longitude, cancellationToken);

        return area is null ? NotFound() : area;
    }

    /// <summary>Finds places of the configured kinds inside a bounding box.</summary>
    /// <remarks>
    /// The viewport is capped at <see cref="GeoViewport.MaxTiles"/> tiles. That cap is what replaces
    /// the old server-owned 15 km radius: a caller choosing the area must not be able to enlarge an
    /// expensive public-provider query without limit.
    /// </remarks>
    [HttpGet("nearby-places")]
    [ProducesResponseType<NearbyPlacesResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<NearbyPlacesResultDto>> NearbyPlaces(
        [FromQuery] double south,
        [FromQuery] double west,
        [FromQuery] double north,
        [FromQuery] double east,
        CancellationToken cancellationToken = default)
    {
        if (!GeoViewport.TryCreate(south, west, north, east, out var viewport, out var error))
        {
            return BadRequest(error);
        }

        var result = await geo.ListNearbyPlacesAsync(viewport, cancellationToken);

        // 503 only when there is nothing to show *and* a tile genuinely failed. A partial cache
        // beats an error banner, and "this area has no vets" has to stay distinguishable from an
        // provider outage — that distinction is the whole point of the feature.
        return result is { Places.Count: 0, Incomplete: true }
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, "Nearby places are unavailable right now.")
            : Ok(result);
    }

    /// <summary>Free-text area search, for the picker a user gets when they decline location.</summary>
    [HttpGet("areas")]
    [CompanyHQ.Services.Postgres.Contracts.McpAction(
        CompanyHQ.Services.Postgres.Contracts.McpCapabilityCatalog.SearchArea,
        CompanyHQ.Services.Postgres.Contracts.McpCapabilityCatalog.SearchArea,
        "Search area names or partial addresses for a location picker. Results are bounded; repeat a new query to refine the location.")]
    [ProducesResponseType<IReadOnlyList<AreaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AreaDto>>> Areas(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return BadRequest("search is required.");
        }

        var areas = await geo.SearchAreasAsync(search, limit, cancellationToken);

        return Ok(areas);
    }
}
