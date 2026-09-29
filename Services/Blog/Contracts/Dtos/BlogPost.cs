using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Blogs.Contracts.Dtos;

/// <summary>Published blog content safe for anonymous readers.</summary>
[JsonDerivedType(typeof(AdminBlogPostDto))]
public record ClientBlogPostDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Body,
    string Author,
    string Category,
    string? CoverImageUrl,
    DateTimeOffset? PublishedAt,
    int SortOrder,
    DateTimeOffset CreatedAt);

/// <summary>Published content plus draft and audit fields for administrators.</summary>
public sealed record AdminBlogPostDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Body,
    string Author,
    string Category,
    string? CoverImageUrl,
    DateTimeOffset? PublishedAt,
    int SortOrder,
    DateTimeOffset CreatedAt,
    bool IsDraft,
    DateTimeOffset UpdatedAt)
    : ClientBlogPostDto(
        Id,
        Slug,
        Title,
        Description,
        Body,
        Author,
        Category,
        CoverImageUrl,
        PublishedAt,
        SortOrder,
        CreatedAt);
