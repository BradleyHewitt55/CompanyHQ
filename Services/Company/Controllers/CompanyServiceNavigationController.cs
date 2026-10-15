using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.Company.Contracts.Ports;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CompanyHQ.Company.Controllers;

[ApiController]
[NonController]
[Authorize]
[Route("api/company/navigation")]
public sealed class CompanyServiceNavigationController(ICompanyServiceNavigation navigation) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServiceNavigationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ServiceNavigationDto>>> List(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } actor) return Unauthorized();
        return Ok(await navigation.ListAsync(actor, cancellationToken));
    }
}
