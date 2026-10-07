using System.ComponentModel;

using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using CompanyHQ.Services.Faqs.Contracts.Dtos;
using CompanyHQ.Services.Faqs.Contracts.Forms;
using CompanyHQ.Services.Faqs.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Faqs.Mcp;

[McpServerToolType]
public sealed class FaqTools(IFaqService faqs, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "list_faq_entries")]
    public Task<PagedResult<ClientFaqEntryDto>> ListFaqEntries(
        [Description("Keyword to match, without case. Omit to browse everything.")] string? keyword = null,
        [Description("The one field to match the keyword against, such as title or category. Omit to match every text field.")] string? field = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, capped at 100. Defaults to 10.")] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return faqs.ListAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            SearchRequest.For(keyword, field),
            ResponseAudience.Admin,
            CurrentUserId(),
            cancellationToken);
    }

    [McpServerTool(Name = "get_faq_entry")]
    public Task<ClientFaqEntryDto?> GetFaqEntry(
        [Description("The stable FAQ key.")] string key,
        CancellationToken cancellationToken = default)
    {
        return faqs.GetAsync(key, ResponseAudience.Admin, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "create_faq_entry")]
    public Task<ClientFaqEntryDto> CreateFaqEntry(
        [Description("The FAQ fields to create.")] CreateFaqEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        return faqs.CreateAsync(request, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "update_faq_entry")]
    public Task<ClientFaqEntryDto?> UpdateFaqEntry(
        [Description("The stable FAQ key.")] string key,
        [Description("The replacement FAQ fields.")] UpdateFaqEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        return faqs.UpdateAsync(key, request, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "delete_faq_entry")]
    public Task<bool> DeleteFaqEntry(
        [Description("The stable FAQ key.")] string key,
        CancellationToken cancellationToken = default)
    {
        return faqs.DeleteAsync(key, CurrentUserId(), cancellationToken);
    }

    // The MCP call filter has already checked this tool's capability and the key's write access
    // before the call reaches here, so an adapter only needs to know who is calling.
    private Guid? CurrentUserId() => httpContextAccessor.HttpContext?.User.GetUserId();
}
