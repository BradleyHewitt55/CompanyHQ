using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Services.Messages.Contracts;
using CompanyHQ.Services.Messages.Contracts.Dtos;
using CompanyHQ.Services.Messages.Contracts.Forms;
using CompanyHQ.Services.Messages.Extensions;
using CompanyHQ.Services.Messages.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Messages.Controllers;

[ApiController]
[Authorize]
[Route("api/conversations")]
[Produces("application/json")]
public sealed class ConversationsController(IMessageService messages) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientConversationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientConversationDto>>> List(
        [FromQuery] PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await messages.ListConversationsAsync(userId, paging, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<ClientConversationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClientConversationDto>> Start(
        [FromBody] StartConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.StartConversationAsync(userId, request, cancellationToken);
        return result.Status switch
        {
            MessageOperationStatus.Success => CreatedAtAction(
                nameof(GetMessages),
                new { id = result.Value!.Id },
                result.Value),
            MessageOperationStatus.Invalid => BadRequest(),
            _ => NotFound()
        };
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType<MessagePageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagePageDto>> GetMessages(
        Guid id,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.GetMessagesAsync(userId, id, cursor, search, cancellationToken);
        return result.Status switch
        {
            MessageOperationStatus.Success => Ok(result.Value),
            MessageOperationStatus.Invalid => BadRequest(),
            _ => NotFound()
        };
    }

    [HttpPost("{id:guid}/messages")]
    [Authorize(Policy = AuthPolicies.RequireVerifiedEmail)]
    [EnableRateLimiting(MessageServiceCollectionExtensions.SendRateLimitPolicyName)]
    [ProducesResponseType<ClientMessageDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClientMessageDto>> Send(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.SendMessageAsync(userId, id, request, cancellationToken);
        return result.Status switch
        {
            MessageOperationStatus.Success => CreatedAtAction(
                nameof(GetMessages),
                new { id },
                result.Value!.Message),
            MessageOperationStatus.Invalid => BadRequest(),
            MessageOperationStatus.Conflict => Conflict(),
            _ => NotFound()
        };
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.MarkReadAsync(userId, id, cancellationToken);
        return result.Status is MessageOperationStatus.Success
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:guid}/mute")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Mute(Guid id, CancellationToken cancellationToken = default) =>
        SetMuteAsync(id, muted: true, cancellationToken);

    [HttpPost("{id:guid}/unmute")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Unmute(Guid id, CancellationToken cancellationToken = default) =>
        SetMuteAsync(id, muted: false, cancellationToken);

    [HttpPost("{id:guid}/clear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.ClearAsync(userId, id, cancellationToken);
        return result.Status is MessageOperationStatus.Success
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.CloseAsync(userId, id, cancellationToken);
        return result.Status switch
        {
            MessageOperationStatus.Success => NoContent(),
            MessageOperationStatus.Invalid => BadRequest(),
            _ => NotFound()
        };
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(new UnreadCountDto(await messages.GetUnreadCountAsync(userId, cancellationToken)));
    }

    [HttpGet("admin/closed")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<PagedResult<AdminConversationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AdminConversationDto>>> ListClosed(
        [FromQuery] PageRequest paging,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var administratorId))
        {
            return Unauthorized();
        }

        return Ok(await messages.ListClosedConversationsAsync(
            administratorId,
            paging,
            search,
            cancellationToken));
    }

    [HttpGet("admin/closed/{id:guid}/messages")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<MessagePageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagePageDto>> GetClosedMessages(
        Guid id,
        [FromQuery] string? cursor = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var administratorId))
        {
            return Unauthorized();
        }

        var result = await messages.GetClosedMessagesAsync(
            administratorId,
            id,
            cursor,
            search,
            cancellationToken);
        return result.Status switch
        {
            MessageOperationStatus.Success => Ok(result.Value),
            MessageOperationStatus.Invalid => BadRequest(),
            _ => NotFound()
        };
    }

    [HttpPost("with/{userId:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<ClientConversationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClientConversationDto>> StartOperatorConversation(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var administratorId))
        {
            return Unauthorized();
        }

        var result = await messages.StartOperatorConversationAsync(
            administratorId,
            userId,
            cancellationToken);
        return result.Status switch
        {
            MessageOperationStatus.Success => CreatedAtAction(
                nameof(GetMessages),
                new { id = result.Value!.Id },
                result.Value),
            MessageOperationStatus.Invalid => BadRequest(),
            _ => NotFound()
        };
    }

    private bool TryGetUserId(out Guid userId)
    {
        var id = User.GetUserId();
        if (id is not { } value || value == Guid.Empty)
        {
            userId = Guid.Empty;
            return false;
        }

        userId = value;
        return true;
    }

    private async Task<IActionResult> SetMuteAsync(
        Guid id,
        bool muted,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await messages.SetMuteAsync(userId, id, muted, cancellationToken);
        return result.Status is MessageOperationStatus.Success
            ? NoContent()
            : NotFound();
    }
}
