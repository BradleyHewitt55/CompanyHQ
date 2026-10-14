using System.ComponentModel;

using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using CompanyHQ.Services.Auth.Contracts.Identity;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Services;
using CompanyHQ.Services.Users.Extensions;

namespace CompanyHQ.Services.Sales.Mcp;

[McpServerToolType]
public sealed class SalesTools(SalesCatalogService catalog, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "list_products")]
    public Task<PagedResult<ClientProductDto>> ListProducts(
        [Description("Keyword to match, without case. Omit to browse everything.")] string? keyword = null,
        [Description("The one field to match the keyword against, such as name or brand. Omit to match every text field.")] string? field = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, capped at 100. Defaults to 10.")] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        catalog.ListAsync(new PageRequest { Page = page, PageSize = pageSize }, SearchRequest.For(keyword, field),
            new ProductFilterRequest(), Audience(), CurrentUserId(), cancellationToken);

    [McpServerTool(Name = "get_product")]
    public Task<ClientProductDto?> GetProduct(
        [Description("The stable product slug.")] string slug,
        CancellationToken cancellationToken = default) =>
        catalog.GetAsync(slug, Audience(), CurrentUserId(), cancellationToken);

    // The connection's permission claims are its owner's role intersected with any role assigned to
    // the key, so a staff member's key restricted to a client role gets the client shape. The
    // catalog then rechecks sales.manage against the owner's current role before adding costs.
    private ResponseAudience Audience() =>
        httpContextAccessor.HttpContext?.User.Audience(AccessPermissions.ManageSales) ?? ResponseAudience.Client;

    private Guid? CurrentUserId() => httpContextAccessor.HttpContext?.User.GetUserId();
}
