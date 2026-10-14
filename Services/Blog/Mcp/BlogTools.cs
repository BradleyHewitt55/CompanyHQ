using System.ComponentModel;

using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using CompanyHQ.Blogs.Contracts.Dtos;
using CompanyHQ.Blogs.Contracts.Forms;
using CompanyHQ.Blogs.Services;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Users.Extensions;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Blogs.Mcp;

[McpServerToolType]
public sealed class BlogTools(IBlogService blogs, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "list_blog_posts")]
    public Task<PagedResult<ClientBlogPostDto>> ListBlogPosts(
        [Description("Keyword to match, without case. Omit to browse everything.")] string? keyword = null,
        [Description("The one field to match the keyword against, such as title or category. Omit to match every text field.")] string? field = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, capped at 100. Defaults to 10.")] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return blogs.ListAsync(
            new PageRequest { Page = page, PageSize = pageSize },
            SearchRequest.For(keyword, field),
            ResponseAudience.Admin,
            CurrentUserId(),
            cancellationToken);
    }

    [McpServerTool(Name = "get_blog_post")]
    public Task<ClientBlogPostDto?> GetBlogPost(
        [Description("The stable blog post slug.")] string slug,
        CancellationToken cancellationToken = default)
    {
        return blogs.GetAsync(slug, ResponseAudience.Admin, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "create_blog_post")]
    public Task<ClientBlogPostDto> CreateBlogPost(
        [Description("The blog post fields to create.")] CreateBlogPostRequest request,
        CancellationToken cancellationToken = default)
    {
        return blogs.CreateAsync(request, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "update_blog_post")]
    public Task<ClientBlogPostDto?> UpdateBlogPost(
        [Description("The stable blog post slug.")] string slug,
        [Description("The replacement blog post fields.")] UpdateBlogPostRequest request,
        CancellationToken cancellationToken = default)
    {
        return blogs.UpdateAsync(slug, request, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "delete_blog_post")]
    public Task<bool> DeleteBlogPost(
        [Description("The stable blog post slug.")] string slug,
        CancellationToken cancellationToken = default)
    {
        return blogs.DeleteAsync(slug, CurrentUserId(), cancellationToken);
    }

    // The MCP call filter has already checked this tool's capability and the key's write access
    // before the call reaches here, so an adapter only needs to know who is calling.
    private Guid? CurrentUserId() => httpContextAccessor.HttpContext?.User.GetUserId();
}
