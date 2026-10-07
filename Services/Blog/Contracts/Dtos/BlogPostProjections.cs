using System.Linq.Expressions;
using CompanyHQ.Services.Blogs.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Enums;

namespace CompanyHQ.Services.Blogs.Contracts.Dtos;

/// <summary>SQL projections and write-result mapping for blog content.</summary>
public static class BlogPostProjections
{
    public static Expression<Func<BlogPost, ClientBlogPostDto>> ToDto { get; } = post => new(
        post.Id,
        post.Slug,
        post.Title,
        post.Description,
        post.Body,
        post.Author,
        post.Category,
        post.CoverImageUrl,
        post.PublishedAt,
        post.SortOrder,
        post.CreatedAt, post.CoverMediaObjectId);

    private static Expression<Func<BlogPost, ClientBlogPostDto>> ToAdminDto { get; } =
        post => (ClientBlogPostDto)new AdminBlogPostDto(
            post.Id,
            post.Slug,
            post.Title,
            post.Description,
            post.Body,
            post.Author,
            post.Category,
            post.CoverImageUrl,
            post.PublishedAt,
            post.SortOrder,
            post.CreatedAt,
            post.IsDraft,
            post.UpdatedAt, post.CoverMediaObjectId);

    public static Expression<Func<BlogPost, ClientBlogPostDto>> For(ResponseAudience audience) =>
        audience is ResponseAudience.Admin ? ToAdminDto : ToDto;

    public static ClientBlogPostDto AsDto(this BlogPost post, ResponseAudience audience) =>
        audience is ResponseAudience.Admin
            ? new AdminBlogPostDto(
                post.Id,
                post.Slug,
                post.Title,
                post.Description,
                post.Body,
                post.Author,
                post.Category,
                post.CoverImageUrl,
                post.PublishedAt,
                post.SortOrder,
                post.CreatedAt,
                post.IsDraft,
                post.UpdatedAt, post.CoverMediaObjectId)
            : new ClientBlogPostDto(
                post.Id,
                post.Slug,
                post.Title,
                post.Description,
                post.Body,
                post.Author,
                post.Category,
                post.CoverImageUrl,
                post.PublishedAt,
                post.SortOrder,
                post.CreatedAt, post.CoverMediaObjectId);
}
