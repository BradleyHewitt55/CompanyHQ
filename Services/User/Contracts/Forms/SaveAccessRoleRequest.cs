using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Users.Contracts.Forms;

public sealed record SaveAccessRoleRequest(
    [Required, MaxLength(80)] string Name,
    IReadOnlyList<string>? Permissions = null);
