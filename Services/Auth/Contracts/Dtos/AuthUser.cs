using CompanyHQ.Services.Users.Contracts.Enums;

namespace CompanyHQ.Services.Auth.Contracts.Dtos;

/// <summary>
/// The signed-in account, as it is handed back to its own owner.
/// </summary>
/// <remarks>
/// It carries <c>Email</c>, which <see cref="Users.Contracts.Dtos.ClientUserDto"/> deliberately omits:
/// this DTO only ever describes the caller, who already knows their own address.
/// </remarks>
public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    UserRole Role,
    bool EmailVerified);
