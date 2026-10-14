using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.Inbox.Contracts.Dtos;
using CompanyHQ.Inbox.Contracts.Enums;
using CompanyHQ.Inbox.Contracts.Forms;
using CompanyHQ.Inbox.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Inbox.Controllers;

/// <summary>
/// The staff inbox. Every route needs <c>inbox.manage</c>; a channel's own permission is checked by
/// the service, which answers 403 for a channel the caller's role cannot open.
/// </summary>
[ApiController]
[Route("api/inbox")]
[Authorize(Policy = AccessPermissions.ManageInbox)]
[Produces("application/json")]
public sealed class InboxController(IInboxService inbox) : ControllerBase
{
    /// <summary>The channels the caller's role may open.</summary>
    [HttpGet("channels")]
    [ProducesResponseType<IReadOnlyList<AdminInboxChannelDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminInboxChannelDto>>> Channels(
        CancellationToken cancellationToken = default) =>
        User.GetUserId() is { } actingUserId
            ? Ok(await inbox.ChannelsAsync(actingUserId, cancellationToken))
            : Unauthorized();

    /// <summary>One channel's threads, open ones first.</summary>
    [HttpGet("{channel}")]
    [ProducesResponseType<PagedResult<AdminInboxItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> List(
        string channel,
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var (error, items) = await inbox.ListAsync(actingUserId, channel, paging, search, cancellationToken);
        return Result(error, Ok(items));
    }

    [HttpGet("{channel}/{id}")]
    [ProducesResponseType<AdminInboxThreadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(string channel, string id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var (error, thread) = await inbox.GetAsync(actingUserId, channel, id, cancellationToken);
        return Result(error, Ok(thread));
    }

    [HttpPost("{channel}/{id}/replies")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reply(
        string channel,
        string id,
        ReplyInboxRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return Result(
            await inbox.ReplyAsync(actingUserId, channel, id, request.Body, cancellationToken),
            StatusCode(StatusCodes.Status201Created));
    }

    [HttpPost("{channel}/{id}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(string channel, string id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return Result(await inbox.ResolveAsync(actingUserId, channel, id, cancellationToken), NoContent());
    }

    [HttpDelete("{channel}/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string channel, string id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return Result(await inbox.DeleteAsync(actingUserId, channel, id, cancellationToken), NoContent());
    }

    private IActionResult Result(InboxError error, IActionResult success) => error switch
    {
        InboxError.None => success,
        InboxError.Forbidden => Forbid(),
        InboxError.Resolved => Conflict("This thread is resolved."),
        InboxError.Invalid => BadRequest("Message text is required."),
        InboxError.Unsupported => Problem(
            title: "This channel does not support that.",
            statusCode: StatusCodes.Status400BadRequest),
        InboxError.Unavailable => Problem(
            title: "This channel is unavailable right now.",
            statusCode: StatusCodes.Status502BadGateway),
        _ => NotFound()
    };
}
