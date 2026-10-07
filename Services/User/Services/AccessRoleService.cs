using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Data;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CompanyHQ.Services.Users.Services;

/// <inheritdoc cref="IAccessRoleService" />
/// <remarks>
/// Takes <see cref="PostgresDbContext"/> because every edit locks the acting user's row and rechecks
/// their permissions in the same serializable transaction as the role write.
/// </remarks>
public sealed class AccessRoleService(PostgresDbContext context) : IAccessRoleService
{
    public Task<PagedResult<AdminAccessRoleDto>> ListAsync(PageRequest paging, SearchRequest? search, CancellationToken cancellationToken) =>
        context.Set<AccessRole>().AsNoTracking()
            .Search<AccessRole, AdminAccessRoleDto>(search)
            .ToPagedResultAsync(
                paging,
                role => new AdminAccessRoleDto(role.Id, role.Name, role.Permissions),
                roles => roles.OrderBy(role => role.Name).ThenBy(role => role.Id),
                cancellationToken);

    public Task<AdminAccessRoleDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<AccessRole>().AsNoTracking().Where(role => role.Id == id)
            .Select(role => new AdminAccessRoleDto(role.Id, role.Name, role.Permissions))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AdminAccessRoleDto> CreateAsync(Guid actorId, SaveAccessRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        ValidateName(name);
        ValidatePermissions(request.Permissions);
        return InEditTransactionAsync(actorId, async permissions =>
        {
            var grants = AccessPermissions.Normalize(AccessPermissions.Client.Concat(request.Permissions ?? []));
            RequireSubset(grants, permissions);
            var role = new AccessRole { Name = name, Permissions = grants };
            context.Set<AccessRole>().Add(role);
            await context.SaveChangesAsync(cancellationToken);
            return new AdminAccessRoleDto(role.Id, role.Name, role.Permissions);
        }, cancellationToken);
    }

    public Task<AdminAccessRoleDto?> UpdateAsync(Guid actorId, Guid id, SaveAccessRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        ValidateName(name);
        ValidatePermissions(request.Permissions);
        return InEditTransactionAsync(actorId, async permissions =>
        {
            var role = await context.Set<AccessRole>().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (role is null) return null;
            if (role.Name is "Client" or "Admin")
                throw new ArgumentException("Built-in roles cannot be edited.");
            RequireSubset(role.Permissions, permissions);
            var grants = request.Permissions is null ? role.Permissions : AccessPermissions.Normalize(request.Permissions);
            RequireSubset(grants, permissions);
            role.Name = name;
            role.Permissions = grants;
            await context.SaveChangesAsync(cancellationToken);
            return new AdminAccessRoleDto(role.Id, role.Name, role.Permissions);
        }, cancellationToken);
    }

    public Task<bool?> DeleteAsync(Guid actorId, Guid id, CancellationToken cancellationToken) =>
        InEditTransactionAsync<bool?>(actorId, async permissions =>
        {
            var role = await context.Set<AccessRole>().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (role is null) return null;
            if (role.Name is "Client" or "Admin")
                throw new ArgumentException("Built-in roles cannot be deleted.");
            RequireSubset(role.Permissions, permissions);
            if (await context.Set<User>().AnyAsync(user => user.AccessRoleId == id, cancellationToken))
                return false;
            context.Set<AccessRole>().Remove(role);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another service's row (an agent connection) still points at the role.
                return false;
            }

            return true;
        }, cancellationToken);

    private async Task<T> InEditTransactionAsync<T>(Guid actorId,
        Func<IReadOnlyList<string>, Task<T>> edit, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var actor = await context.Set<User>()
                .FromSqlInterpolated($"SELECT * FROM \"user\" WHERE id = {actorId} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken);
            var permissions = actor?.AccessRoleId is { } roleId
                ? await context.Set<AccessRole>().Where(role => role.Id == roleId)
                    .Select(role => role.Permissions).FirstOrDefaultAsync(cancellationToken) ?? []
                : [];
            if (!permissions.Contains(AccessPermissions.ManageRoles, StringComparer.Ordinal))
                throw new UnauthorizedAccessException("The current role cannot manage roles.");
            var result = await edit(permissions);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static void RequireSubset(IEnumerable<string> grants, IReadOnlyList<string> editorPermissions)
    {
        if (grants.Except(editorPermissions, StringComparer.Ordinal).Any())
            throw new UnauthorizedAccessException("Cannot assign permissions beyond the current role.");
    }

    private static void ValidateName(string name)
    {
        if (name.Length is 0 or > 80) throw new ArgumentException("Role name must contain 1 to 80 characters.");
    }

    private static void ValidatePermissions(IReadOnlyList<string>? permissions)
    {
        if (permissions?.Any(permission => !AccessPermissions.All.Contains(permission, StringComparer.Ordinal)) == true)
            throw new ArgumentException("The role contains an unknown permission.");
    }
}
