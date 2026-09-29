using CompanyHQ.Services.Users.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Forms;

namespace CompanyHQ.Services.Users.Services;

/// <summary>Owns reusable role rows. All edits take effect on the next authenticated request.</summary>
public interface IAccessRoleService
{
    Task<IReadOnlyList<AdminAccessRoleDto>> ListAsync(CancellationToken cancellationToken);

    Task<AdminAccessRoleDto> CreateAsync(Guid actorId, SaveAccessRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Null when the role does not exist.</summary>
    Task<AdminAccessRoleDto?> UpdateAsync(Guid actorId, Guid id, SaveAccessRoleRequest request, CancellationToken cancellationToken);
}
