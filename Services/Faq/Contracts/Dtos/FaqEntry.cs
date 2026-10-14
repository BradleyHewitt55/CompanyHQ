using System.Text.Json.Serialization;

namespace CompanyHQ.Faqs.Contracts.Dtos;

/// <summary>Published FAQ content safe for anonymous readers.</summary>
[JsonDerivedType(typeof(AdminFaqEntryDto))]
public record ClientFaqEntryDto(
    Guid Id,
    string Key,
    string Question,
    string Answer,
    string Category,
    string SeoDescription,
    int SortOrder,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt);

/// <summary>Published FAQ content plus draft and audit fields for administrators.</summary>
public sealed record AdminFaqEntryDto(
    Guid Id,
    string Key,
    string Question,
    string Answer,
    string Category,
    string SeoDescription,
    int SortOrder,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    bool IsDraft,
    DateTimeOffset UpdatedAt)
    : ClientFaqEntryDto(
        Id,
        Key,
        Question,
        Answer,
        Category,
        SeoDescription,
        SortOrder,
        PublishedAt,
        CreatedAt);
