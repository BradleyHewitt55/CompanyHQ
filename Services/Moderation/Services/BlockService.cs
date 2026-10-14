
using CompanyHQ.Cache.Services;
using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Forms;
using CompanyHQ.Moderation.Contracts.Models;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Moderation.Services;

/// <inheritdoc cref="IBlockService" />
public sealed class BlockService(
    IRepository<UserBlock> blocks,
    IRepository<User> users,
    ICacheService cache,
    IContentHider hider) : IBlockService
{
    private static readonly TimeSpan BlockCacheTtl = TimeSpan.FromMinutes(5);

    public async Task<bool> IsBlockedBetweenAsync(Guid a, Guid b, CancellationToken ct = default)
    {
        if (a == Guid.Empty || b == Guid.Empty || a == b)
        {
            return false;
        }

        return (await BlockedForAsync(a, ct)).Contains(b);
    }

    public async Task<IReadOnlySet<Guid>> BlockedForAsync(Guid userId, CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));

        return await cache.GetOrSetAsync<HashSet<Guid>>(
            CacheKey(userId),
            async cancellationToken =>
            {
                var ids = await blocks.Query()
                    .Where(block => block.BlockerUserId == userId || block.BlockedUserId == userId)
                    .Select(block => block.BlockerUserId == userId
                        ? block.BlockedUserId
                        : block.BlockerUserId)
                    .ToListAsync(cancellationToken);

                return ids.ToHashSet();
            },
            BlockCacheTtl,
            ct);
    }

    public async Task<BlockResult> BlockAsync(
        Guid blockerUserId,
        BlockUserRequest request,
        CancellationToken ct = default)
    {
        EnsureUserId(blockerUserId, nameof(blockerUserId));
        ArgumentNullException.ThrowIfNull(request);

        if (request.BlockedUserId == Guid.Empty)
        {
            return BlockResult.Fail(BlockFailure.NotFound);
        }

        if (request.BlockedUserId == blockerUserId)
        {
            return BlockResult.Fail(BlockFailure.Self);
        }

        if (!await users.ExistsAsync(request.BlockedUserId, ct))
        {
            return BlockResult.Fail(BlockFailure.NotFound);
        }

        var existing = await blocks.Query()
            .Where(block => block.BlockerUserId == blockerUserId
                && block.BlockedUserId == request.BlockedUserId)
            .Select(block => new { block.Id, block.CreatedAt })
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return BlockResult.Ok(await ToDtoAsync(request.BlockedUserId, existing.CreatedAt, ct));
        }

        var block = await blocks.AddAsync(new UserBlock
        {
            BlockerUserId = blockerUserId,
            BlockedUserId = request.BlockedUserId,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        }, ct);

        await hider.HideAsync(
            blockerUserId,
            new HideContentRequest("user", request.BlockedUserId),
            HiddenContentSource.Block,
            block.Id,
            ct);
        await InvalidateAsync(blockerUserId, request.BlockedUserId, CancellationToken.None);

        return BlockResult.Ok(await ToDtoAsync(block.BlockedUserId, block.CreatedAt, ct));
    }

    public async Task<bool> UnblockAsync(
        Guid blockerUserId,
        Guid blockedUserId,
        CancellationToken ct = default)
    {
        EnsureUserId(blockerUserId, nameof(blockerUserId));
        EnsureUserId(blockedUserId, nameof(blockedUserId));

        var block = await blocks.Query()
            .Where(block => block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId)
            .Select(block => new { block.Id })
            .FirstOrDefaultAsync(ct);

        if (block is null || !await blocks.DeleteAsync(block.Id, ct))
        {
            return false;
        }

        await hider.RemoveSourceAsync(
            blockerUserId,
            "user",
            blockedUserId,
            HiddenContentSource.Block,
            block.Id,
            ct);

        await InvalidateAsync(blockerUserId, blockedUserId, CancellationToken.None);
        return true;
    }

    public async Task<PagedResult<ClientUserBlockDto>> ListOutboundAsync(
        Guid blockerUserId,
        PageRequest paging,
        CancellationToken ct = default)
    {
        EnsureUserId(blockerUserId, nameof(blockerUserId));

        var query = blocks.Query()
            .Where(block => block.BlockerUserId == blockerUserId)
            .Join(
                users.Query(),
                block => block.BlockedUserId,
                user => user.Id,
                (block, user) => new { Block = block, User = user });
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(pair => pair.Block.CreatedAt)
            .ThenBy(pair => pair.Block.BlockedUserId)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(pair => new ClientUserBlockDto(
                pair.Block.BlockedUserId,
                pair.User.FirstName,
                pair.User.LastName,
                pair.Block.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ClientUserBlockDto>(items, paging.Page, paging.PageSize, total);
    }

    private Task<ClientUserBlockDto> ToDtoAsync(Guid blockedUserId, DateTimeOffset createdAt, CancellationToken ct) =>
        users.Query()
            .Where(user => user.Id == blockedUserId)
            .Select(user => new ClientUserBlockDto(blockedUserId, user.FirstName, user.LastName, createdAt))
            .FirstAsync(ct);

    private async Task InvalidateAsync(Guid firstUserId, Guid secondUserId, CancellationToken ct)
    {
        await cache.RemoveAsync(CacheKey(firstUserId), ct);
        await cache.RemoveAsync(CacheKey(secondUserId), ct);
    }

    private static string CacheKey(Guid userId) => $"moderation:v1:blocks:{userId}";

    private static void EnsureUserId(Guid userId, string parameterName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", parameterName);
        }
    }
}
