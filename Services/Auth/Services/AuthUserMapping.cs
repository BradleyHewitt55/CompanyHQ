using CompanyHQ.Services.Auth.Contracts.Dtos;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Auth.Services;

/// <summary>
/// Turns the persisted account into the caller's own view of it. Lives beside the service rather
/// than on the DTO so <see cref="AuthUserDto"/> keeps no reference to the entity — the contract
/// ships to clients that have no database and no Npgsql to load one with.
/// </summary>
internal static class AuthUserMapping
{
    public static AuthUserDto ToAuthUserDto(this User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.EmailVerified);
}
