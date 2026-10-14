using System.Linq.Expressions;
using CompanyHQ.Faqs.Contracts.Models;
using CompanyHQ.Postgres.Contracts.Enums;

namespace CompanyHQ.Faqs.Contracts.Dtos;

/// <summary>SQL projections and write-result mapping for FAQ content.</summary>
public static class FaqEntryProjections
{
    public static Expression<Func<FaqEntry, ClientFaqEntryDto>> ToDto { get; } = entry => new(
        entry.Id,
        entry.Key,
        entry.Question,
        entry.Answer,
        entry.Category,
        entry.SeoDescription,
        entry.SortOrder,
        entry.PublishedAt,
        entry.CreatedAt);

    private static Expression<Func<FaqEntry, ClientFaqEntryDto>> ToAdminDto { get; } =
        entry => (ClientFaqEntryDto)new AdminFaqEntryDto(
            entry.Id,
            entry.Key,
            entry.Question,
            entry.Answer,
            entry.Category,
            entry.SeoDescription,
            entry.SortOrder,
            entry.PublishedAt,
            entry.CreatedAt,
            entry.IsDraft,
            entry.UpdatedAt);

    public static Expression<Func<FaqEntry, ClientFaqEntryDto>> For(ResponseAudience audience) =>
        audience is ResponseAudience.Admin ? ToAdminDto : ToDto;

    public static ClientFaqEntryDto AsDto(this FaqEntry entry, ResponseAudience audience) =>
        audience is ResponseAudience.Admin
            ? new AdminFaqEntryDto(
                entry.Id,
                entry.Key,
                entry.Question,
                entry.Answer,
                entry.Category,
                entry.SeoDescription,
                entry.SortOrder,
                entry.PublishedAt,
                entry.CreatedAt,
                entry.IsDraft,
                entry.UpdatedAt)
            : new ClientFaqEntryDto(
                entry.Id,
                entry.Key,
                entry.Question,
                entry.Answer,
                entry.Category,
                entry.SeoDescription,
                entry.SortOrder,
                entry.PublishedAt,
                entry.CreatedAt);
}
