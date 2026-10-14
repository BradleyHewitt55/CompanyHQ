using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Widget.Contracts.Dtos;
using CompanyHQ.Widget.Contracts.Enums;
using CompanyHQ.Widget.Contracts.Forms;
using CompanyHQ.Widget.Extensions;
using CompanyHQ.Widget.Services;

namespace CompanyHQ.Widget.Controllers;

/// <summary>
/// Everything a contact widget calls. All of it is anonymous: a conversation is proved by its
/// token, and the rate limits are the only brake.
/// </summary>
[ApiController]
[Route("api/widget")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class WidgetController(IWidgetService widget) : ControllerBase
{
    /// <summary>What the widget can offer right now, and the sources it accepts.</summary>
    [HttpGet]
    [ProducesResponseType<ClientWidgetDto>(StatusCodes.Status200OK)]
    public Task<ClientWidgetDto> Get(CancellationToken cancellationToken = default) =>
        widget.GetAsync(cancellationToken);

    [HttpPost("conversations")]
    [EnableRateLimiting(WidgetServiceCollectionExtensions.SubmitRateLimitPolicyName)]
    [ProducesResponseType<ClientWidgetConversationAccessDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartConversation(
        StartWidgetConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var (error, access) = await widget.StartConversationAsync(request, cancellationToken);
        return Result(error, StatusCode(StatusCodes.Status201Created, access));
    }

    /// <summary>The visitor's own thread. A POST so the token never lands in a URL or an access log.</summary>
    [HttpPost("conversations/{id:guid}/session")]
    [EnableRateLimiting(WidgetServiceCollectionExtensions.ChatRateLimitPolicyName)]
    [ProducesResponseType<ClientWidgetConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Session(
        Guid id,
        WidgetConversationAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var (error, conversation) = await widget.ReadConversationAsync(id, request.Token, cancellationToken);
        return Result(error, Ok(conversation));
    }

    [HttpPost("conversations/{id:guid}/messages")]
    [EnableRateLimiting(WidgetServiceCollectionExtensions.ChatRateLimitPolicyName)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Send(
        Guid id,
        SendWidgetMessageRequest request,
        CancellationToken cancellationToken = default) =>
        Result(await widget.SendMessageAsync(id, request, cancellationToken), StatusCode(StatusCodes.Status201Created));

    [HttpPost("issues")]
    [EnableRateLimiting(WidgetServiceCollectionExtensions.SubmitRateLimitPolicyName)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitIssue(
        SubmitWidgetIssueRequest request,
        CancellationToken cancellationToken = default) =>
        Result(await widget.SubmitIssueAsync(request, cancellationToken), StatusCode(StatusCodes.Status201Created));

    private IActionResult Result(WidgetError error, IActionResult success) => error switch
    {
        WidgetError.None => success,
        WidgetError.NotFound => NotFound(),
        WidgetError.Closed => Conflict("This conversation is resolved."),
        WidgetError.Invalid => BadRequest("Message text is required."),
        WidgetError.UnknownSource => Problem(
            title: "That source is not allowed.",
            statusCode: StatusCodes.Status400BadRequest),
        WidgetError.Unavailable => Problem(
            title: "This is not available right now.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Problem(
            title: "The submission was not accepted.",
            statusCode: StatusCodes.Status502BadGateway)
    };
}
