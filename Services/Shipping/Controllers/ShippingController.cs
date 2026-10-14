using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Services.Auth.Contracts.Identity;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Shipping.Contracts.Dtos;
using CompanyHQ.Services.Shipping.Contracts.Forms;
using CompanyHQ.Services.Shipping.Services;
using CompanyHQ.Services.Users.Extensions;

namespace CompanyHQ.Services.Shipping.Controllers;

/// <summary>
/// Shipping methods and quotes for anyone; methods, zones and origins to manage for a role with
/// <c>shipping.manage</c>.
/// </summary>
[ApiController]
[Route("api/shipping")]
[Produces("application/json")]
public sealed class ShippingController(IShippingService shipping, ShippingAddressService addresses) : ControllerBase
{
    /// <summary>Lists active methods. A manager sees every method in the staff shape.</summary>
    [HttpGet("methods")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ClientShippingMethodDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ClientShippingMethodDto>> ListMethods(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken = default) =>
        shipping.ListMethodsAsync(paging, search, User.Audience(AccessPermissions.ManageShipping), User.GetUserId(), cancellationToken);

    /// <summary>Returns one method. An inactive one is a 404 to anyone but a manager.</summary>
    [HttpGet("methods/{methodId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<ClientShippingMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientShippingMethodDto>> GetMethod(Guid methodId, CancellationToken cancellationToken = default) =>
        await shipping.GetMethodAsync(methodId, User.Audience(AccessPermissions.ManageShipping), User.GetUserId(), cancellationToken) is { } method
            ? method
            : NotFound();

    /// <summary>Creates a method with its zones, product allow-list and origin links.</summary>
    [HttpPost("methods")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType<AdminShippingMethodDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<AdminShippingMethodDto>> CreateMethod(
        SaveShippingMethodRequest request,
        CancellationToken cancellationToken = default) =>
        Guarded<AdminShippingMethodDto>(async () =>
        {
            var method = await shipping.CreateMethodAsync(request, User.GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetMethod), new { methodId = method.Id }, method);
        });

    /// <summary>Replaces a method and everything that decides where it delivers.</summary>
    [HttpPut("methods/{methodId:guid}")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType<AdminShippingMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AdminShippingMethodDto>> UpdateMethod(
        Guid methodId,
        SaveShippingMethodRequest request,
        CancellationToken cancellationToken = default) =>
        Guarded<AdminShippingMethodDto>(async () =>
            await shipping.UpdateMethodAsync(methodId, request, User.GetUserId(), cancellationToken) is { } method ? method : NotFound());

    /// <summary>Removes a method with its zones and origin links.</summary>
    [HttpDelete("methods/{methodId:guid}")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMethod(Guid methodId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await shipping.DeleteMethodAsync(methodId, User.GetUserId(), cancellationToken) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Lists the places goods leave from.</summary>
    [HttpGet("origins")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType<PagedResult<AdminFulfillmentOriginDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<PagedResult<AdminFulfillmentOriginDto>>> ListOrigins(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken = default) =>
        Guarded<PagedResult<AdminFulfillmentOriginDto>>(async () =>
            await shipping.ListOriginsAsync(paging, search, User.GetUserId(), cancellationToken));

    [HttpGet("origins/{originId:guid}")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType<AdminFulfillmentOriginDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AdminFulfillmentOriginDto>> GetOrigin(Guid originId, CancellationToken cancellationToken = default) =>
        Guarded<AdminFulfillmentOriginDto>(async () =>
            await shipping.GetOriginAsync(originId, User.GetUserId(), cancellationToken) is { } origin ? origin : NotFound());

    /// <summary>Adds an origin. Its address is placed on the map when the host can geocode.</summary>
    [HttpPost("origins")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType<AdminFulfillmentOriginDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<ActionResult<AdminFulfillmentOriginDto>> CreateOrigin(
        SaveFulfillmentOriginRequest request,
        CancellationToken cancellationToken = default) =>
        Guarded<AdminFulfillmentOriginDto>(async () =>
        {
            var origin = await shipping.CreateOriginAsync(request, User.GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetOrigin), new { originId = origin.Id }, origin);
        });

    [HttpPut("origins/{originId:guid}")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType<AdminFulfillmentOriginDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<AdminFulfillmentOriginDto>> UpdateOrigin(
        Guid originId,
        SaveFulfillmentOriginRequest request,
        CancellationToken cancellationToken = default) =>
        Guarded<AdminFulfillmentOriginDto>(async () =>
            await shipping.UpdateOriginAsync(originId, request, User.GetUserId(), cancellationToken) is { } origin ? origin : NotFound());

    /// <summary>Removes an origin and unlinks it from every method.</summary>
    [HttpDelete("origins/{originId:guid}")]
    [Authorize(Policy = AccessPermissions.ManageShipping)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrigin(Guid originId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await shipping.DeleteOriginAsync(originId, User.GetUserId(), cancellationToken) ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Prices the cheapest way to ship a basket to an address. A basket that cannot be shipped is a
    /// 200 with <c>isAvailable</c> false and the reasons, not an error.
    /// </summary>
    [HttpPost("quote")]
    [AllowAnonymous]
    [EnableRateLimiting(ShippingRateLimit.QuotePolicyName)]
    [ProducesResponseType<ShippingQuoteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public Task<ActionResult<ShippingQuoteDto>> Quote(ShippingQuoteRequest request, CancellationToken cancellationToken = default) =>
        Guarded<ShippingQuoteDto>(async () => await shipping.QuoteAsync(request, cancellationToken));

    /// <summary>Provider-backed address candidates. HQ's default adapter returns area precision.</summary>
    [HttpGet("addresses/search"),AllowAnonymous,EnableRateLimiting(ShippingRateLimit.QuotePolicyName)]
    public Task<ActionResult<IReadOnlyList<ShippingAddressCandidateDto>>> SearchAddresses([FromQuery] string query,[FromQuery] int limit=10,CancellationToken cancellationToken=default)=>
        Guarded<IReadOnlyList<ShippingAddressCandidateDto>>(async()=>Ok(await addresses.SearchAsync(query,limit,cancellationToken)));
    [HttpGet("addresses/hydrate"),AllowAnonymous,EnableRateLimiting(ShippingRateLimit.QuotePolicyName)]
    public Task<ActionResult<ShippingAddressCandidateDto>> HydrateAddress([FromQuery] string reference,CancellationToken cancellationToken=default)=>
        Guarded<ShippingAddressCandidateDto>(async()=>await addresses.HydrateAsync(reference,cancellationToken) is { } candidate?Ok(candidate):NotFound());

    private async Task<ActionResult<TResult>> Guarded<TResult>(Func<Task<ActionResult<TResult>>> action)
    {
        try
        {
            return await action();
        }
        catch (ShippingCapabilityUnavailableException exception)
        {
            return StatusCode(503,new ProblemDetails{Title=exception.Message,Status=503});
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid shipping request.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
