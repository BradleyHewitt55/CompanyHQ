using CompanyHQ.Blogs.Contracts.Dtos;
using CompanyHQ.Blogs.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Enums;

namespace CompanyHQ.Blogs.Services;

public interface IBlogService
{
    Task<PagedResult<ClientBlogPostDto>> ListAsync(
        PageRequest page,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientBlogPostDto?> GetAsync(
        string slug,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientBlogPostDto> CreateAsync(
        CreateBlogPostRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<ClientBlogPostDto?> UpdateAsync(
        string slug,
        UpdateBlogPostRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string slug,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);
}
