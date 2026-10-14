using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;

using CompanyHQ.Mcp.Contracts.Dtos;
using CompanyHQ.Mcp.Contracts.Enums;
using CompanyHQ.Mcp.Contracts.Forms;
using CompanyHQ.Mcp.Contracts.Models;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Mcp.Services;

/// <inheritdoc cref="IAgentKeyService"/>
public sealed class AgentKeyService(
    IRepository<AgentKey> keys,
    IRepository<User> users,
    IRepository<AccessRole> roles,
    TimeProvider clock) : IAgentKeyService
{
    private const int KeyBytes = 32;
    private const string KeyPrefix = "pgk_";

    public async Task<AgentKeyOutcome> IssueAsync(
        Guid userId,
        IssueAgentKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var owner = await users.Query()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Role, user.AccessRoleId, Permissions = user.AccessRole == null ? null : user.AccessRole.Permissions })
            .FirstOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            return AgentKeyOutcome.Failure(AgentKeyError.UserNotFound);
        }

        if (request.AccessRoleId is { } assignedRoleId)
        {
            var assigned = await roles.Query()
                .Where(role => role.Id == assignedRoleId)
                .Select(role => role.Permissions)
                .FirstOrDefaultAsync(cancellationToken);
            var ownerPermissions = owner.AccessRoleId is null
                ? owner.Role == CompanyHQ.Users.Contracts.Enums.UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client
                : owner.Permissions ?? [];
            if (assigned is null) throw new ArgumentException("The connection role does not exist.");
            if (assigned.Except(ownerPermissions, StringComparer.Ordinal).Any())
                throw new UnauthorizedAccessException("The connection role must be within the owner's permissions.");
        }

        var secret = KeyPrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(KeyBytes));

        var key = new AgentKey
        {
            UserId = userId,
            Name = request.Name.Trim(),
            KeyHash = Hash(secret),
            Prefix = secret[..8],
            Access = request.Access,
            AccessRoleId = request.AccessRoleId,
            ExpiresAt = request.ExpiresAt,
            McpDisabledTools = [.. McpCapabilityCatalog.NormalizeDisabled(request.McpDisabledTools)]
        };

        await keys.AddAsync(key, cancellationToken);

        return AgentKeyOutcome.Success(
            new IssuedAgentKeyDto(key.Id, key.Name, secret, key.Access, key.CreatedAt, key.ExpiresAt, key.McpDisabledTools, key.AccessRoleId));
    }

    public async Task<PagedResult<ClientAgentKeyDto>> ListMineAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var page = await keys.Query()
            .Where(key => key.UserId == userId && key.RevokedAt == null)
            .OrderByDescending(key => key.CreatedAt).ThenBy(key => key.Id)
            .ToPageAsync(
                paging.Skip,
                paging.PageSize,
                key => new ClientAgentKeyDto(
                    key.Id,
                    key.Name,
                    key.Prefix,
                    key.Access,
                    key.CreatedAt,
                    key.LastUsedAt,
                    key.ExpiresAt,
                    key.RevokedAt,
                    key.McpDisabledTools,
                    key.AccessRoleId),
                cancellationToken);

        return new PagedResult<ClientAgentKeyDto>(page.Items, paging.Page, paging.PageSize, page.Total);
    }

    public async Task<PagedResult<AdminAgentKeyDto>> ListForUserAsync(
        Guid targetUserId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var query = keys.Query()
            .Where(key => key.UserId == targetUserId && key.RevokedAt == null)
            .Join(
                users.Query(),
                key => key.UserId,
                user => user.Id,
                (key, user) => new { Key = key, User = user });
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(pair => pair.Key.CreatedAt)
            .ThenBy(pair => pair.Key.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(pair => new AdminAgentKeyDto(
                pair.Key.Id,
                pair.Key.Name,
                pair.Key.Prefix,
                pair.Key.Access,
                pair.Key.CreatedAt,
                pair.Key.LastUsedAt,
                pair.Key.ExpiresAt,
                pair.Key.RevokedAt,
                pair.Key.UserId,
                pair.User.FirstName,
                pair.User.LastName,
                pair.Key.RevokedByUserId,
                pair.Key.McpDisabledTools,
                pair.Key.AccessRoleId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminAgentKeyDto>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<bool> RevokeAsync(
        Guid callerId,
        bool callerIsAdmin,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var key = await keys.GetAsync(keyId, cancellationToken);

        if (key is null || key.RevokedAt is not null)
        {
            return false;
        }

        if (key.UserId != callerId && !callerIsAdmin)
        {
            return false;
        }

        key.RevokedAt = clock.GetUtcNow();
        if (key.UserId != callerId)
        {
            key.RevokedByUserId = callerId;
        }

        await keys.UpdateAsync(key, cancellationToken);
        return true;
    }

    public async Task<bool> AssignRoleAsync(Guid callerId, bool canManageUsers, Guid keyId,
        Guid? accessRoleId, CancellationToken cancellationToken = default)
    {
        var key = await keys.Query().FirstOrDefaultAsync(candidate => candidate.Id == keyId
            && candidate.RevokedAt == null, cancellationToken);
        if (key is null || key.UserId != callerId && !canManageUsers) return false;
        if (accessRoleId is { } roleId)
        {
            var owner = await users.Query().Where(user => user.Id == key.UserId)
                .Select(user => new { user.Role, user.AccessRoleId,
                    Permissions = user.AccessRole == null ? null : user.AccessRole.Permissions })
                .SingleAsync(cancellationToken);
            var permissions = await roles.Query()
                .Where(role => role.Id == roleId).Select(role => role.Permissions)
                .FirstOrDefaultAsync(cancellationToken);
            var ceiling = owner.AccessRoleId is null
                ? owner.Role == CompanyHQ.Users.Contracts.Enums.UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client
                : owner.Permissions ?? [];
            if (permissions is null) throw new ArgumentException("The connection role does not exist.");
            if (permissions.Except(ceiling, StringComparer.Ordinal).Any())
                throw new UnauthorizedAccessException("The connection role must be within the owner's permissions.");
        }
        key.AccessRoleId = accessRoleId;
        await keys.UpdateAsync(key, cancellationToken);
        return true;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
