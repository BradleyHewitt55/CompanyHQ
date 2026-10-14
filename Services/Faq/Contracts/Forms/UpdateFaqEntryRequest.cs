using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Faqs.Contracts.Forms;

/// <summary>Administrator body for changing an FAQ without changing its stable key.</summary>
public sealed record UpdateFaqEntryRequest(
    [Required, StringLength(300, MinimumLength = 5)] string Question,
    [Required, StringLength(20_000, MinimumLength = 10)] string Answer,
    [Required, StringLength(100, MinimumLength = 2)] string Category,
    [Required, StringLength(320, MinimumLength = 20)] string SeoDescription,
    [Range(0, 1_000_000)] int SortOrder = 0,
    DateTimeOffset? PublishedAt = null,
    bool IsDraft = true);
