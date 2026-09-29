using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Blogs.Contracts.Dtos;
using CompanyHQ.Services.Blogs.Contracts.Forms;
using CompanyHQ.Services.Blogs.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Blogs.Services;

public sealed class BlogService(
    IPostgresService<BlogPost> posts,
    IUserAuthorizationService authorization) : IBlogService
{
    public async Task<PagedResult<ClientBlogPostDto>> ListAsync(
        PageRequest page,
        string? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveAudience = await EffectiveAudienceAsync(audience, actingUserId, McpCapabilityCatalog.ListBlogPosts, cancellationToken);
        var query = posts.Query();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(post => EF.Functions.ILike(post.Title, pattern)
                                        || EF.Functions.ILike(post.Description, pattern)
                                        || EF.Functions.ILike(post.Author, pattern)
                                        || EF.Functions.ILike(post.Category, pattern));
        }

        if (effectiveAudience is not ResponseAudience.Admin)
        {
            query = query.Where(post => !post.IsDraft &&
                                        post.PublishedAt != null &&
                                        post.PublishedAt <= DateTimeOffset.UtcNow);
        }

        return await query.ToPagedResultAsync(
            page,
            BlogPostProjections.For(effectiveAudience),
            ordered => ordered
                .OrderByDescending(post => post.PublishedAt)
                .ThenBy(post => post.SortOrder)
                .ThenBy(post => post.Id),
            cancellationToken);
    }

    public async Task<ClientBlogPostDto?> GetAsync(
        string slug,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveAudience = await EffectiveAudienceAsync(audience, actingUserId, McpCapabilityCatalog.GetBlogPost, cancellationToken);
        var normalised = NormaliseSlug(slug);
        var query = posts.Query().Where(post => post.Slug == normalised);
        if (effectiveAudience is not ResponseAudience.Admin)
        {
            query = query.Where(post => !post.IsDraft &&
                                        post.PublishedAt != null &&
                                        post.PublishedAt <= DateTimeOffset.UtcNow);
        }

        return await query
            .Select(BlogPostProjections.For(effectiveAudience))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ClientBlogPostDto> CreateAsync(
        CreateBlogPostRequest request,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(actingUserId, McpCapabilityCatalog.CreateBlogPost, cancellationToken);
        var slug = NormaliseSlug(request.Slug);
        if (await posts.Query().AnyAsync(post => post.Slug == slug, cancellationToken))
        {
            throw new BlogSlugConflictException();
        }

        var post = new BlogPost
        {
            Slug = slug,
            Title = CleanRequired(request.Title, nameof(request.Title)),
            Description = CleanRequired(request.Description, nameof(request.Description)),
            Body = CleanRequired(request.Body, nameof(request.Body)),
            Author = CleanRequired(request.Author, nameof(request.Author)),
            Category = CleanRequired(request.Category, nameof(request.Category)),
            CoverImageUrl = CleanOptional(request.CoverImageUrl),
            PublishedAt = request.PublishedAt,
            SortOrder = request.SortOrder,
            IsDraft = request.IsDraft
        };
        ValidatePublication(post);

        try
        {
            await posts.AddAsync(post, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation("ix_blog_post_slug"))
        {
            throw new BlogSlugConflictException();
        }

        return post.AsDto(ResponseAudience.Admin);
    }

    public async Task<ClientBlogPostDto?> UpdateAsync(
        string slug,
        UpdateBlogPostRequest request,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(actingUserId, McpCapabilityCatalog.UpdateBlogPost, cancellationToken);
        var post = await posts.Query().FirstOrDefaultAsync(
            candidate => candidate.Slug == NormaliseSlug(slug),
            cancellationToken);
        if (post is null)
        {
            return null;
        }

        post.Title = CleanRequired(request.Title, nameof(request.Title));
        post.Description = CleanRequired(request.Description, nameof(request.Description));
        post.Body = CleanRequired(request.Body, nameof(request.Body));
        post.Author = CleanRequired(request.Author, nameof(request.Author));
        post.Category = CleanRequired(request.Category, nameof(request.Category));
        post.CoverImageUrl = CleanOptional(request.CoverImageUrl);
        post.PublishedAt = request.PublishedAt;
        post.SortOrder = request.SortOrder;
        post.IsDraft = request.IsDraft;
        ValidatePublication(post);

        await posts.UpdateAsync(post, cancellationToken);
        return post.AsDto(ResponseAudience.Admin);
    }

    public async Task<bool> DeleteAsync(
        string slug,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(actingUserId, McpCapabilityCatalog.DeleteBlogPost, cancellationToken);
        var post = await posts.Query().FirstOrDefaultAsync(
            candidate => candidate.Slug == NormaliseSlug(slug),
            cancellationToken);
        return post is not null && await posts.DeleteAsync(post.Id, cancellationToken);
    }

    private async Task EnsurePermissionAsync(Guid? actingUserId, string permission, CancellationToken cancellationToken)
    {
        if (actingUserId is not { } userId ||
            !await authorization.HasPermissionAsync(userId, permission, cancellationToken))
        {
            throw new UnauthorizedAccessException();
        }
    }

    private async Task<ResponseAudience> EffectiveAudienceAsync(
        ResponseAudience requested,
        Guid? actingUserId,
        string permission,
        CancellationToken cancellationToken) =>
        requested is ResponseAudience.Admin &&
        actingUserId is { } userId &&
        await authorization.HasPermissionAsync(userId, permission, cancellationToken)
            ? ResponseAudience.Admin
            : ResponseAudience.Client;

    private static void ValidatePublication(BlogPost post)
    {
        if (!post.IsDraft && post.PublishedAt is null)
        {
            throw new ArgumentException("A published blog post needs a publication date.", nameof(post.PublishedAt));
        }
    }

    private static string NormaliseSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        if (slug.Length == 0 || slug.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '-')) ||
            slug.StartsWith('-') || slug.EndsWith('-') || slug.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Slug must contain lowercase letters, numbers, and single hyphens only.", nameof(value));
        }

        return slug;
    }

    private static string CleanRequired(string value, string parameterName)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length == 0)
        {
            throw new ArgumentException("The value cannot be blank.", parameterName);
        }

        return cleaned;
    }

    private static string? CleanOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("CoverImageUrl must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        return cleaned;
    }
}
