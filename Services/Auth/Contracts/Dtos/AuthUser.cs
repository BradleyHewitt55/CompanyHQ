using CompanyHQ.Users.Contracts.Enums;

namespace CompanyHQ.Auth.Contracts.Dtos;

/// <summary>
/// The signed-in account, as it is handed back to its own owner.
/// </summary>
/// <remarks>
/// It carries <c>Email</c>, which <see cref="Users.Contracts.Dtos.ClientUserDto"/> deliberately omits:
/// this DTO only ever describes the caller, who already knows their own address.
/// <c>RoleName</c> and <c>Permissions</c> are the caller's current access role, so a client can show
/// only what the API would let this account do; the API still checks every call itself.
/// </remarks>
public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    UserRole Role,
    bool EmailVerified,
    string RoleName,
    IReadOnlyList<string> Permissions);
