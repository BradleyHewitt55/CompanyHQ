using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Users.Controllers;

[ApiController]
[Route("api/access-roles")]
[Authorize(Policy = AccessPermissions.ManageRoles)]
public sealed class AccessRolesController(IAccessRoleService roles) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AdminAccessRoleDto>> List(CancellationToken cancellationToken) => roles.ListAsync(cancellationToken);

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
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation("ix_access_role_name"))
        { return Conflict("A role with that name already exists."); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminAccessRoleDto>> Update(Guid id, SaveAccessRoleRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();
        try { return await roles.UpdateAsync(actorId, id, request, cancellationToken) is { } role ? role : NotFound(); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation("ix_access_role_name"))
        { return Conflict("A role with that name already exists."); }
    }
}
