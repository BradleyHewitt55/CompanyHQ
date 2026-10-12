using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Users.Controllers;

[ApiController]
[Route("api/access-roles")]
[Authorize(Policy = AccessPermissions.ManageRoles)]
public sealed class AccessRolesController(IAccessRoleService roles) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminAccessRoleDto>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken) => roles.ListAsync(paging, search, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminAccessRoleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminAccessRoleDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await roles.GetAsync(id, cancellationToken) is { } role ? role : NotFound();

    [HttpPost]
    public async Task<ActionResult<AdminAccessRoleDto>> Create(SaveAccessRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (User.GetUserId() is not { } actorId) return Unauthorized();
            var role = await roles.CreateAsync(actorId, request, cancellationToken);
            return Created($"/api/access-roles/{role.Id}", role);
        }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (UniqueConstraintException exception) when (exception.Is("ix_access_role_name"))
        { return Conflict("A role with that name already exists."); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminAccessRoleDto>> Update(Guid id, SaveAccessRoleRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();
        try { return await roles.UpdateAsync(actorId, id, request, cancellationToken) is { } role ? role : NotFound(); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (UniqueConstraintException exception) when (exception.Is("ix_access_role_name"))
        { return Conflict("A role with that name already exists."); }
    }

    /// <summary>409 while an account or an agent connection still uses the role.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();
        try
        {
            return await roles.DeleteAsync(actorId, id, cancellationToken) switch
            {
                null => NotFound(),
                false => Conflict("The role is still assigned. Move its accounts and agent connections to another role first."),
                _ => NoContent()
            };
        }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
