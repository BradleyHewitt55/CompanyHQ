using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Blogs.Contracts.Models;

/// <summary>A CompanyHQ-owned article prepared for public search and answer engines.</summary>
public sealed class BlogPost : BaseModel
{
    /// <summary>Stable lowercase route identity. It is unique and never changed by an update.</summary>
    public required string Slug { get; set; }

    public required string Title { get; set; }

    /// <summary>The concise search description shown in metadata and cards.</summary>
    public required string Description { get; set; }

    /// <summary>Author-authored HTML. Consumers must sanitize it before rendering.</summary>
    public required string Body { get; set; }

    public Guid? CoverMediaObjectId { get; set; }

    public required string Author { get; set; }

    /// <summary>The filter a reader picks on the blog list, such as Safety or Health.</summary>
    public required string Category { get; set; }

    public string? CoverImageUrl { get; set; }

    /// <summary>Null while a draft is unpublished.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Lower values appear first when publication times are tied.</summary>
    public int SortOrder { get; set; }

    public bool IsDraft { get; set; } = true;
}
