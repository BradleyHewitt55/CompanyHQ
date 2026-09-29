using CompanyHQ.Services.Users.Contracts.Enums;

namespace CompanyHQ.Services.Users.Contracts.Dtos;

/// <summary>A user administration result with a safe, controller-mappable error.</summary>
public readonly record struct UserAdministrationOutcome(
    AdminUserDto? User,
    UserAdministrationError Error)
{
    public bool Succeeded => User is not null;

    public static UserAdministrationOutcome Success(AdminUserDto user) => new(user, UserAdministrationError.None);

    public static UserAdministrationOutcome Failure(UserAdministrationError error) => new(null, error);
}
