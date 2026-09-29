using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Faqs.Contracts.Dtos;
using CompanyHQ.Services.Faqs.Contracts.Forms;
using CompanyHQ.Services.Faqs.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Faqs.Services;

public sealed class FaqService(
    IPostgresService<FaqEntry> entries,
    IUserAuthorizationService authorization) : IFaqService
{
    public async Task<PagedResult<ClientFaqEntryDto>> ListAsync(
        PageRequest page,
        string? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveAudience = await EffectiveAudienceAsync(audience, actingUserId, McpCapabilityCatalog.ListFaqEntries, cancellationToken);
        var query = entries.Query();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(entry => EF.Functions.ILike(entry.Question, pattern)
                                         || EF.Functions.ILike(entry.Answer, pattern)
                                         || EF.Functions.ILike(entry.Category, pattern)
                                         || EF.Functions.ILike(entry.Key, pattern));
        }

        if (effectiveAudience is not ResponseAudience.Admin)
        {
            query = query.Where(entry => !entry.IsDraft &&
                                         entry.PublishedAt != null &&
                                         entry.PublishedAt <= DateTimeOffset.UtcNow);
        }

        return await query.ToPagedResultAsync(
            page,
            FaqEntryProjections.For(effectiveAudience),
            ordered => ordered
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Category)
                .ThenBy(entry => entry.Key),
            cancellationToken);
    }

    public async Task<ClientFaqEntryDto?> GetAsync(
        string key,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveAudience = await EffectiveAudienceAsync(audience, actingUserId, McpCapabilityCatalog.GetFaqEntry, cancellationToken);
        var normalised = NormaliseKey(key);
        var query = entries.Query().Where(entry => entry.Key == normalised);
        if (effectiveAudience is not ResponseAudience.Admin)
        {
            query = query.Where(entry => !entry.IsDraft &&
                                         entry.PublishedAt != null &&
                                         entry.PublishedAt <= DateTimeOffset.UtcNow);
        }

        return await query
            .Select(FaqEntryProjections.For(effectiveAudience))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ClientFaqEntryDto> CreateAsync(
        CreateFaqEntryRequest request,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(actingUserId, McpCapabilityCatalog.CreateFaqEntry, cancellationToken);
        var key = NormaliseKey(request.Key);
        if (await entries.Query().AnyAsync(entry => entry.Key == key, cancellationToken))
        {
            throw new FaqKeyConflictException();
        }

        var entry = new FaqEntry
        {
            Key = key,
            Question = CleanRequired(request.Question, nameof(request.Question)),
            Answer = CleanRequired(request.Answer, nameof(request.Answer)),
            Category = CleanRequired(request.Category, nameof(request.Category)),
            SeoDescription = CleanRequired(request.SeoDescription, nameof(request.SeoDescription)),
            SortOrder = request.SortOrder,
            PublishedAt = request.PublishedAt,
            IsDraft = request.IsDraft
        };
        ValidatePublication(entry);

        try
        {
            await entries.AddAsync(entry, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation("ix_faq_entry_key"))
        {
            throw new FaqKeyConflictException();
        }

        return entry.AsDto(ResponseAudience.Admin);
    }

    public async Task<ClientFaqEntryDto?> UpdateAsync(
        string key,
        UpdateFaqEntryRequest request,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(actingUserId, McpCapabilityCatalog.UpdateFaqEntry, cancellationToken);
        var entry = await entries.Query().FirstOrDefaultAsync(
            candidate => candidate.Key == NormaliseKey(key),
            cancellationToken);
        if (entry is null)
        {
            return null;
        }

        entry.Question = CleanRequired(request.Question, nameof(request.Question));
        entry.Answer = CleanRequired(request.Answer, nameof(request.Answer));
        entry.Category = CleanRequired(request.Category, nameof(request.Category));
        entry.SeoDescription = CleanRequired(request.SeoDescription, nameof(request.SeoDescription));
        entry.SortOrder = request.SortOrder;
        entry.PublishedAt = request.PublishedAt;
        entry.IsDraft = request.IsDraft;
        ValidatePublication(entry);

        await entries.UpdateAsync(entry, cancellationToken);
        return entry.AsDto(ResponseAudience.Admin);
    }

    public async Task<bool> DeleteAsync(
        string key,
        ResponseAudience audience = ResponseAudience.Client,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync(actingUserId, McpCapabilityCatalog.DeleteFaqEntry, cancellationToken);
        var entry = await entries.Query().FirstOrDefaultAsync(
            candidate => candidate.Key == NormaliseKey(key),
            cancellationToken);
        return entry is not null && await entries.DeleteAsync(entry.Id, cancellationToken);
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

    private static void ValidatePublication(FaqEntry entry)
    {
        if (!entry.IsDraft && entry.PublishedAt is null)
        {
            throw new ArgumentException("A published FAQ entry needs a publication date.", nameof(entry.PublishedAt));
        }
    }

    private static string NormaliseKey(string value)
    {
        var key = value.Trim().ToLowerInvariant();
        if (key.Length == 0 || key.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '-')) ||
            key.StartsWith('-') || key.EndsWith('-') || key.Contains("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Key must contain lowercase letters, numbers, and single hyphens only.", nameof(value));
        }

        return key;
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
}
