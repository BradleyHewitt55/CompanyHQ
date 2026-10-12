using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Models;
using System.Data;

namespace CompanyHQ.Services.Users.Services;

/// <inheritdoc cref="IAccessRoleService" />
/// <remarks>
/// Takes <see cref="IPersistence"/> because every edit locks the acting user's row and rechecks
/// their permissions in the same serializable transaction as the role write.
/// </remarks>
public sealed class AccessRoleService(IPersistence context) : IAccessRoleService
{
    public async Task<PagedResult<AdminAccessRoleDto>> ListAsync(PageRequest paging, SearchRequest? search, CancellationToken cancellationToken)
    {
        var page = await context.Query<AccessRole>()
            .Search(search?.Field, search?.Keyword, typeof(AdminAccessRoleDto))
            .OrderBy(role => role.Name).ThenBy(role => role.Id)
            .ToPageAsync(
                paging.Skip,
                paging.PageSize,
                role => new AdminAccessRoleDto(role.Id, role.Name, role.Permissions),
                cancellationToken);

        return new PagedResult<AdminAccessRoleDto>(page.Items, paging.Page, paging.PageSize, page.Total);
    }

    public Task<AdminAccessRoleDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Query<AccessRole>().Where(role => role.Id == id)
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
            context.Add(role);
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
            var role = await context.Tracked<AccessRole>().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
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
            var role = await context.Tracked<AccessRole>().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (role is null) return null;
            if (role.Name is "Client" or "Admin")
                throw new ArgumentException("Built-in roles cannot be deleted.");
            RequireSubset(role.Permissions, permissions);
            if (await context.Tracked<User>().AnyAsync(user => user.AccessRoleId == id, cancellationToken))
                return false;
            context.Remove(role);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (PersistenceException)
            {
                // Another service's row (an agent connection) still points at the role.
                return false;
            }

            return true;
        }, cancellationToken);

    private Task<T> InEditTransactionAsync<T>(Guid actorId,
        Func<IReadOnlyList<string>, Task<T>> edit, CancellationToken cancellationToken) =>
        context.InTransactionAsync(async _ =>
        {
            // The row lock is the gate: a concurrent edit by the same actor waits here, then
            // rechecks the permissions this transaction may have changed.
            var actor = await context.Locked<User>()
                .FirstOrDefaultAsync(user => user.Id == actorId, cancellationToken);
            var permissions = actor?.AccessRoleId is { } roleId
                ? await context.Tracked<AccessRole>().Where(role => role.Id == roleId)
                    .Select(role => role.Permissions).FirstOrDefaultAsync(cancellationToken) ?? []
                : [];
            if (!permissions.Contains(AccessPermissions.ManageRoles, StringComparer.Ordinal))
                throw new UnauthorizedAccessException("The current role cannot manage roles.");
            return await edit(permissions);
        }, IsolationLevel.Serializable, cancellationToken);

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
