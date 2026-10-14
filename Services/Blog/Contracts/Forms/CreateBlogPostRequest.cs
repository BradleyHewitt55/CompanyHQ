using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Blogs.Contracts.Forms;

/// <summary>Administrator body for creating a blog post.</summary>
public sealed record CreateBlogPostRequest(
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), StringLength(160)] string Slug,
    [Required, StringLength(240, MinimumLength = 3)] string Title,
    [Required, StringLength(320, MinimumLength = 20)] string Description,
    [Required, StringLength(100_000, MinimumLength = 20)] string Body,
    [Required, StringLength(120, MinimumLength = 2)] string Author,
    [Required, StringLength(100, MinimumLength = 2)] string Category,
    [Url, StringLength(2048)] string? CoverImageUrl = null,
    DateTimeOffset? PublishedAt = null,
    [Range(0, 1_000_000)] int SortOrder = 0,
    bool IsDraft = true,
    Guid? CoverMediaObjectId = null);
