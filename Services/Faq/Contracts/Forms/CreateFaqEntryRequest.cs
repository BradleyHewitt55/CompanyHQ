using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Faqs.Contracts.Forms;

/// <summary>Administrator body for creating a FAQ entry.</summary>
public sealed record CreateFaqEntryRequest(
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), StringLength(160)] string Key,
    [Required, StringLength(300, MinimumLength = 5)] string Question,
    [Required, StringLength(20_000, MinimumLength = 10)] string Answer,
    [Required, StringLength(100, MinimumLength = 2)] string Category,
    [Required, StringLength(320, MinimumLength = 20)] string SeoDescription,
    [Range(0, 1_000_000)] int SortOrder = 0,
    DateTimeOffset? PublishedAt = null,
    bool IsDraft = true);
