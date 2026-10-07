using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Users.Services;

/// <summary>Reads the role row on every call; never treats a cookie or JWT claim as current authority.</summary>
public sealed class UserAuthorizationService(IPostgresService<User> users) : IUserAuthorizationService
{
    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !AccessPermissions.All.Contains(permission, StringComparer.Ordinal)) return false;
        var assignment = await users.Query().Where(user => user.Id == userId)
            .Select(user => new { user.Role, user.AccessRoleId,
                Permissions = user.AccessRole == null ? null : user.AccessRole.Permissions })
            .FirstOrDefaultAsync(cancellationToken);
        return assignment is not null && (assignment.AccessRoleId is null
            ? assignment.Role == UserRole.Admin || AccessPermissions.Client.Contains(permission, StringComparer.Ordinal)
            : assignment.Permissions?.Contains(permission, StringComparer.Ordinal) == true);
    }

    public async Task<IReadOnlyList<Guid>> ListUserIdsAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (!AccessPermissions.All.Contains(permission, StringComparer.Ordinal)) return [];
        var grantedToClients = AccessPermissions.Client.Contains(permission, StringComparer.Ordinal);
        return await users.Query()
            .Where(user => user.AccessRole != null
                ? user.AccessRole.Permissions.Contains(permission)
                : user.Role == UserRole.Admin || grantedToClients)
            .OrderBy(user => user.CreatedAt)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }
}
