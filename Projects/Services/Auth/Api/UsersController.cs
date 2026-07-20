using Abstractions.Data;
using Abstractions.Users;
using CompanyHQ.Services.Auth.Dtos;
using CompanyHQ.Services.Auth.Models;
using CompanyHQ.Services.Auth.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyHQ.Services.Auth.Api;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthClaims.CompanyAdminPolicy)]
public sealed class UsersController(IUserService users, ICurrentUser currentUser) : ControllerBase
{
    private const string DuplicateEmailMessage = "A user with this email already exists.";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> List(
        CancellationToken cancellationToken)
    {
        var result = await users.ListAsync(cancellationToken);
        return Ok(result.Select(AdminUserDto.From).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(AdminUserDto.From(user));
    }

    [HttpGet("query")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Query(
        [FromQuery] UserQuery query,
        CancellationToken cancellationToken)
    {
        var result = await users.QueryAsync(query, cancellationToken);
        return Ok(new PagedResult<AdminUserDto>(
            result.Items.Select(AdminUserDto.From).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Save(
        [FromBody] AdminUserSaveModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await users.SaveAsync(model.ToUserSave(), cancellationToken);
            return Ok(AdminUserDto.From(user));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UniqueConstraintException)
        {
            return Conflict(new { error = DuplicateEmailMessage });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.User?.Id == id)
        {
            return BadRequest(new { error = "You cannot delete your own user." });
        }

        try
        {
            await users.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
