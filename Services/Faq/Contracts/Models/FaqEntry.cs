using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Faqs.Contracts.Models;

/// <summary>A stable, categorised answer to one frequently asked question.</summary>
public sealed class FaqEntry : BaseModel
{
    /// <summary>Stable route and seed identity. It is unique and immutable.</summary>
    public required string Key { get; set; }

    public required string Question { get; set; }

    /// <summary>Plain-text answer paragraphs rendered into the visible page and FAQ JSON-LD.</summary>
    public required string Answer { get; set; }

    public required string Category { get; set; }

    /// <summary>The concise description used when this entry is referenced in search content.</summary>
    public required string SeoDescription { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsDraft { get; set; } = true;
}
