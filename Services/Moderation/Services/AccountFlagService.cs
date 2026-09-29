using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Cache.Services;
using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Moderation.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Moderation.Services;

/// <inheritdoc cref="IAccountFlagService" />
public sealed class AccountFlagService(
    IPostgresService<AccountFlag> flags,
    IUserAuthorizationService authorization,
    ICacheService cache) : IAccountFlagService
{
    private static readonly TimeSpan FlagCacheTtl = TimeSpan.FromMinutes(5);

    public async Task RaiseAsync(
        Guid userId,
        string kind,
        string evidence,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));
        var normalizedKind = NormalizeKind(kind);
        var normalizedEvidence = NormalizeEvidence(evidence);

        var active = await flags.Query()
            .Where(flag => flag.UserId == userId
                && flag.Kind == normalizedKind
                && flag.Status == AccountFlagStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (active is null)
        {
            try
            {
                await flags.AddAsync(new AccountFlag
                {
                    UserId = userId,
                    Kind = normalizedKind,
                    Status = AccountFlagStatus.Active,
                    RaisedBy = FlagOrigin.System,
                    Evidence = normalizedEvidence
                }, ct);
            }
            catch (DbUpdateException exception) when (
                exception.IsUniqueConstraintViolation("ix_account_flag_user_id_kind"))
            {
                // A detector race is the same idempotent outcome as observing the live row first.
            }
        }

        await cache.RemoveAsync(CacheKey(normalizedKind, userId), ct);
    }

    public async Task<bool> IsFlaggedAsync(
        Guid userId,
        string kind,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));
        var normalizedKind = NormalizeKind(kind);

        return await cache.GetOrSetAsync(
            CacheKey(normalizedKind, userId),
            cancellationToken => flags.Query().AnyAsync(
                flag => flag.UserId == userId
                    && flag.Kind == normalizedKind
                    && flag.Status == AccountFlagStatus.Active,
                cancellationToken),
            FlagCacheTtl,
            ct);
    }

    public async Task<PagedResult<ClientAccountFlagDto>> ListAdminAsync(
        PageRequest paging,
        AccountFlagStatus? status = null,
        string? kind = null,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default)
    {
        var query = flags.Query().Where(flag => flag.Status == (status ?? AccountFlagStatus.Active));

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var normalizedKind = NormalizeKind(kind);
            query = query.Where(flag => flag.Kind == normalizedKind);
        }

        return await query.ToPagedResultAsync(
            paging,
            FlagProjection(audience),
            flags => flags.OrderBy(candidate => candidate.CreatedAt).ThenBy(candidate => candidate.Id),
            ct);
    }

    public async Task<bool> DecideAsync(
        Guid actingAdministratorId,
        Guid flagId,
        FlagDecisionRequest request,
        CancellationToken ct = default)
    {
        await EnsureAdministratorAsync(actingAdministratorId, ct);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status is not (AccountFlagStatus.Cleared or AccountFlagStatus.Upheld))
        {
            throw new ArgumentException("A flag decision must clear or uphold the flag.", nameof(request));
        }

        var flag = await flags.GetAsync(flagId, ct);
        if (flag is null)
        {
            return false;
        }

        flag.Status = request.Status;
        if (request.Status is AccountFlagStatus.Cleared)
        {
            flag.ClearedByUserId = actingAdministratorId;
            flag.ClearedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            // The plan names these columns for a clear; an upheld flag must not look cleared.
            flag.ClearedByUserId = null;
            flag.ClearedAt = null;
        }
        flag.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await flags.UpdateAsync(flag, ct);
        await cache.RemoveAsync(CacheKey(flag.Kind, flag.UserId), ct);
        return true;
    }

    public async Task<PagedResult<ClientAccountFlagDto>> ListForUserAsync(
        Guid userId,
        PageRequest paging,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));

        return await flags.Query()
            .Where(flag => flag.UserId == userId)
            .ToPagedResultAsync(
                paging,
                FlagProjection(audience),
                query => query.OrderByDescending(flag => flag.CreatedAt).ThenBy(flag => flag.Id),
                ct);
    }

    private async Task EnsureAdministratorAsync(Guid userId, CancellationToken ct)
    {
        if (!await authorization.IsAdministratorAsync(userId, ct))
        {
            throw new UnauthorizedAccessException();
        }
    }

    private static System.Linq.Expressions.Expression<Func<AccountFlag, ClientAccountFlagDto>> FlagProjection(
        ResponseAudience audience) =>
        audience is ResponseAudience.Admin
            ? flag => (ClientAccountFlagDto)new AdminAccountFlagDto(
                flag.Id,
                flag.UserId,
                flag.Kind,
                flag.Status,
                flag.CreatedAt,
                flag.RaisedBy,
                flag.RaisedByUserId,
                flag.Evidence,
                flag.ClearedByUserId,
                flag.ClearedAt,
                flag.ReviewNote)
            : flag => new ClientAccountFlagDto(
                flag.Id,
                flag.UserId,
                flag.Kind,
                flag.Status,
                flag.CreatedAt);

    private static string NormalizeKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        var normalized = kind.Trim().ToLowerInvariant();
        if (normalized.Length > 128
            || normalized.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character != '.'
                && character != '-'
                && character != '_'))
        {
            throw new ArgumentException("The flag kind is invalid.", nameof(kind));
        }

        return normalized;
    }

    private static string NormalizeEvidence(string evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        var normalized = evidence.Trim();
        return normalized.Length <= 4000
            ? normalized
            : normalized[..4000];
    }

    private static string CacheKey(string kind, Guid userId) =>
        $"moderation:v1:flag:{kind}:{userId}";

    private static void EnsureUserId(Guid userId, string parameterName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", parameterName);
        }
    }
}
