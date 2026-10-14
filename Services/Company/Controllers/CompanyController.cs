using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CompanyHQ.Company.Contracts.Dtos;
using CompanyHQ.Company.Contracts.Forms;
using CompanyHQ.Company.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Company.Controllers;

/// <summary>The operating company profile: public to read, <c>company.manage</c> to change.</summary>
[ApiController]
[Route("api/company")]
[Produces("application/json")]
public sealed class CompanyController(ICompanyProfileService profiles) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ClientCompanyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientCompanyProfileDto>> Get(CancellationToken cancellationToken = default) =>
        await profiles.GetAsync(cancellationToken) is { } profile ? profile : NotFound();

    [HttpPut]
    [Authorize(Policy = AccessPermissions.ManageCompany)]
    [ProducesResponseType<ClientCompanyProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientCompanyProfileDto>> Update(
        UpdateCompanyProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await profiles.UpdateAsync(request, User.GetUserId(), cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid company profile.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
