using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.Company.Contracts.Dtos;
using CompanyHQ.Company.Contracts.Forms;
using CompanyHQ.Company.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompanyHQ.Company.Controllers;

[ApiController]
[NonController]
[Route("api/company/services")]
[Authorize(Policy = AccessPermissions.ManageServices)]
public sealed class CompanyServicesController(ICompanyServiceCatalog catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceCatalogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ServiceCatalogDto>>> List(CancellationToken cancellationToken)
    {
        try { return Ok(await catalog.ListAsync(User.GetUserId(), cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("{key}")]
    [ProducesResponseType<ServiceCatalogDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ServiceCatalogDto>> Get(string key, CancellationToken cancellationToken)
    {
        try { return await catalog.GetAsync(key, User.GetUserId(), cancellationToken) is { } service ? service : NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{key}")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ServiceChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ServiceChangeResult>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ServiceChangeResult>> Change(string key, ChangeServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalog.ChangeAsync(key, request, User.GetUserId(), cancellationToken);
            return result.Error switch { ServiceChangeError.None => Ok(result), ServiceChangeError.NotFound => NotFound(), _ => Conflict(result) };
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
