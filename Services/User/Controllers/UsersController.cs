using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Services;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Users.Controllers;

/// <summary>
/// CRUD for user accounts. HTTP only: every rule lives in the services it calls.
/// </summary>
/// <remarks>
/// Reads return <see cref="ClientUserDto"/>, or the derived <see cref="AdminUserDto"/> when the caller
/// holds <c>users.read_private</c> — the declared type stays the client shape, so an admin response is
/// the same JSON plus the address and last-modified stamp. Reads stay anonymous; every write that
/// names a user is permission-gated, because those endpoints can set a role. Self-service signup is
/// <c>POST /api/auth/register</c> and self-service profile editing is <c>PUT /api/users/me</c>.
/// </remarks>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(
    IUserService users,
    IAccountDeletionCoordinator accountDeletion,
    IUserAdministrationService administration,
    IUserFavoritePlaceService favoritePlaces) : ControllerBase
{
    [HttpGet("me/favorite-places")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<OwnerUserFavoritePlaceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OwnerUserFavoritePlaceDto>>> ListFavoritePlaces(
        CancellationToken cancellationToken = default) =>
        User.GetUserId() is { } id
            ? Ok(await favoritePlaces.ListAsync(id, cancellationToken))
            : Unauthorized();

    [HttpPut("me/favorite-places")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<OwnerUserFavoritePlaceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OwnerUserFavoritePlaceDto>>> ToggleFavoritePlace(
        ToggleFavoritePlaceRequest request,
        CancellationToken cancellationToken = default) =>
        User.GetUserId() is { } id
            ? Ok(await favoritePlaces.ToggleAsync(id, request, cancellationToken))
            : Unauthorized();

    /// <summary>Returns typed-sorted accounts, optionally narrowed by text or role.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ClientUserDto>>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] string? search = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] UserSort sort = UserSort.CreatedAt,
        [FromQuery] SortDirection direction = SortDirection.Descending,
        CancellationToken cancellationToken = default)
    {
        var page = await users.ListAsync(Audience(), paging, search, role, sort, direction, cancellationToken);
        return page is null ? Problem(statusCode: StatusCodes.Status403Forbidden) : page;
    }

    /// <summary>
    /// Protected identity lookup for picking another account, e.g. to start a conversation. It
    /// returns only an id and display name; the public list cannot enumerate account identifiers.
    /// </summary>
    [HttpGet("lookup")]
    [Authorize]
    [ProducesResponseType<PagedResult<ClientUserLookupDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ClientUserLookupDto>> Lookup(
        [FromQuery] PageRequest paging,
        [FromQuery] string? search = null,
        [FromQuery] UserSort sort = UserSort.CreatedAt,
        [FromQuery] SortDirection direction = SortDirection.Descending,
        CancellationToken cancellationToken = default) =>
        users.LookupAsync(paging, search, sort, direction, cancellationToken);

    /// <summary>
    /// Resolves one user by id, display name, or email — whichever the caller happens to hold.
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType<ClientUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientUserDto>> Get(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(slug, User.GetUserId(), Audience(), cancellationToken);
        return user is null ? NotFound() : user;
    }

    [HttpPost]
    [Authorize(Policy = AccessPermissions.ManageUsers)]
    [Authorize(Policy = AccessPermissions.ReadPrivateUsers)]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingAdministratorId)
        {
            return Unauthorized();
        }

        var outcome = await administration.CreateAsync(
            actingAdministratorId,
            request,
            cancellationToken);

        if (outcome.Error is UserAdministrationError.NotAdministrator)
        {
            return Forbid();
        }

        if (outcome.Error is UserAdministrationError.InvalidRole)
            return BadRequest("Role name must contain 1 to 80 characters.");

        if (outcome.Error is UserAdministrationError.EmailTaken)
        {
            return Problem(
                detail: $"Email {request.Email.Trim().ToLowerInvariant()} is already registered.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return outcome.User is { } user
            ? CreatedAtAction(nameof(Get), new { slug = user.Id }, user)
            : Problem(statusCode: StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// The one user write that is not permission-gated, because it can only ever reach the caller's
    /// own row: the id comes from the token, never from the route. It cannot set a role, an email or
    /// a password, so it is a separate endpoint rather than a relaxed <see cref="Update"/>.
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType<ClientUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ClientUserDto>> UpdateMe(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } id)
        {
            return Unauthorized();
        }

        // Null means the token is valid but the account is gone — deleted since it was issued.
        var user = await users.UpdateProfileAsync(id, request, cancellationToken);
        return user is null ? Unauthorized() : user;
    }

    /// <summary>Applies only the fields present in the body.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AccessPermissions.ManageUsers)]
    [Authorize(Policy = AccessPermissions.ReadPrivateUsers)]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingAdministratorId)
        {
            return Unauthorized();
        }

        var outcome = await administration.UpdateAsync(
            actingAdministratorId,
            id,
            request,
            cancellationToken);

        if (outcome.Error is UserAdministrationError.NotAdministrator)
        {
            return Forbid();
        }

        if (outcome.Error is UserAdministrationError.InvalidRole)
            return BadRequest("Role name must contain 1 to 80 characters.");

        if (outcome.Error is UserAdministrationError.UserNotFound)
        {
            return NotFound();
        }

        if (outcome.Error is UserAdministrationError.EmailTaken)
        {
            return Problem(
                detail: $"Email {request.Email!.Trim().ToLowerInvariant()} is already registered.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return outcome.User ?? throw new InvalidOperationException("A successful user update returned no user.");
    }

    /// <summary>
    /// Refused with 409 when a plugged-in service's deletion guard objects; the reason says what the
    /// account holder still has to resolve.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AccessPermissions.ManageUsers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingAdministratorId)
        {
            return Unauthorized();
        }

        var (outcome, reason) = await accountDeletion.DeleteAsync(
            id,
            actingAdministratorId,
            cancellationToken);

        return outcome switch
        {
            AccountDeletionOutcome.Deleted => NoContent(),
            AccountDeletionOutcome.Blocked =>
                Problem(statusCode: StatusCodes.Status409Conflict, detail: reason),
            AccountDeletionOutcome.NotAuthorized => Forbid(),
            _ => NotFound()
        };
    }

    private ResponseAudience Audience() =>
        User.HasClaim("permission", AccessPermissions.ReadPrivateUsers)
            ? ResponseAudience.Admin
            : ResponseAudience.Client;
}
