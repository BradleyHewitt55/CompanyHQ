using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Services.Media.Contracts.Dtos;
using CompanyHQ.Services.Media.Contracts.Forms;
using CompanyHQ.Services.Media.Services;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Media.Controllers;

/// <summary>Issues direct object-storage upload tickets for image and video media.</summary>
[ApiController]
[Route("api/media")]
[Authorize]
[Produces("application/json")]
public sealed class MediaController(IMediaService media) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MediaUploadTicketDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MediaUploadTicketDto>> Create(
        CreateMediaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } ownerUserId) return Unauthorized();

        try
        {
            var ticket = await media.CreateUploadAsync(request, ownerUserId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = ticket.MediaObjectId }, ticket);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem(exception.Message));
        }
    }

    /// <summary>
    /// Uploads the bytes of a pending ticket through this API instead of straight to object storage. A browser
    /// PUT to the presigned URL needs a bucket CORS rule naming every origin the app is served from,
    /// and a missing rule surfaces as an opaque network error, not a status code. Native clients may
    /// still use <c>MediaUploadTicketDto.UploadUrl</c> directly.
    /// </summary>
    [HttpPut("{id:guid}/upload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Upload(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } uploaderUserId) return Unauthorized();

        var outcome = await media.UploadAsync(
            id,
            uploaderUserId,
            Request.Body,
            Request.ContentType,
            Request.ContentLength,
            cancellationToken);
        return outcome.Error switch
        {
            Contracts.Enums.MediaOperationError.None => NoContent(),
            Contracts.Enums.MediaOperationError.NotFound => NotFound(),
            Contracts.Enums.MediaOperationError.Forbidden => Forbid(),
            Contracts.Enums.MediaOperationError.NotPending => Conflict(Problem("This media upload has already been completed or deleted.")),
            Contracts.Enums.MediaOperationError.MetadataMismatch => UnprocessableEntity(Problem("The body does not match the content type and size this ticket was issued for.")),
            Contracts.Enums.MediaOperationError.UploadFailed => StatusCode(StatusCodes.Status502BadGateway, Problem("The media store rejected the upload. Try again.")),
            _ => throw new InvalidOperationException("Unknown media upload result.")
        };
    }

    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } uploaderUserId) return Unauthorized();

        var outcome = await media.CompleteAsync(id, uploaderUserId, cancellationToken);
        return outcome.Error switch
        {
            Contracts.Enums.MediaOperationError.None => NoContent(),
            Contracts.Enums.MediaOperationError.NotFound => NotFound(),
            Contracts.Enums.MediaOperationError.Forbidden => Forbid(),
            Contracts.Enums.MediaOperationError.NotPending => Conflict(Problem("This media upload has already been completed or deleted.")),
            Contracts.Enums.MediaOperationError.ObjectMissing => UnprocessableEntity(Problem("The media object was not found. Upload it before completing the ticket.")),
            Contracts.Enums.MediaOperationError.MetadataMismatch => UnprocessableEntity(Problem("The stored media metadata does not match the declared upload.")),
            _ => throw new InvalidOperationException("Unknown media completion result.")
        };
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClientMediaObjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AdminMediaObjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientMediaObjectDto>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } viewerUserId) return Unauthorized();
        var result = await media.GetAsync(id, viewerUserId, User.Audience(AccessPermissions.ManageMedia), cancellationToken);
        return result is null ? NotFound() : result;
    }

    /// <summary>
    /// The durable address of a public profile image. Anonymous because these objects are
    /// public by definition — this is the same audience a custom domain bound to the bucket would
    /// serve, and Message objects are never reachable here.
    /// </summary>
    /// <remarks>
    /// A redirect, not a proxy: the response carries no bytes, and the client fetches the object
    /// straight from object storage with the signature attached. Caching is short because the target expires.
    /// </remarks>
    [HttpGet("{id:guid}/content")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Content(Guid id, CancellationToken cancellationToken = default)
    {
        var target = await media.GetPublicContentUrlAsync(id, cancellationToken);
        if (target is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=300";
        return Redirect(target.AbsoluteUri);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId) return Unauthorized();
        var outcome = await media.DeleteAsync(id, actingUserId, User.HasPermission(AccessPermissions.ManageMedia), cancellationToken);
        return outcome.Error switch
        {
            Contracts.Enums.MediaOperationError.None => NoContent(),
            Contracts.Enums.MediaOperationError.NotFound => NotFound(),
            Contracts.Enums.MediaOperationError.Forbidden => Forbid(),
            Contracts.Enums.MediaOperationError.NotPending => Conflict(Problem("The media changed while it was being deleted; retry the request.")),
            _ => throw new InvalidOperationException("Unknown media deletion result.")
        };
    }

    private static ProblemDetails Problem(string detail) => new()
    {
        Title = "Invalid media upload.",
        Detail = detail,
        Status = StatusCodes.Status400BadRequest
    };
}
