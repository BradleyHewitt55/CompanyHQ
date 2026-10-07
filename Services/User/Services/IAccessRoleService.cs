using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;

namespace CompanyHQ.Services.Users.Services;

/// <summary>Owns reusable role rows. All edits take effect on the next authenticated request.</summary>
public interface IAccessRoleService
{
    Task<PagedResult<AdminAccessRoleDto>> ListAsync(PageRequest paging, SearchRequest? search, CancellationToken cancellationToken);

    /// <summary>Null when the role does not exist.</summary>
    Task<AdminAccessRoleDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<AdminAccessRoleDto> CreateAsync(Guid actorId, SaveAccessRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Null when the role does not exist.</summary>
    Task<AdminAccessRoleDto?> UpdateAsync(Guid actorId, Guid id, SaveAccessRoleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Null when the role does not exist, false when an account or an agent connection still uses
    /// it. The built-in roles are never deleted.
    /// </summary>
    Task<bool?> DeleteAsync(Guid actorId, Guid id, CancellationToken cancellationToken);
}
