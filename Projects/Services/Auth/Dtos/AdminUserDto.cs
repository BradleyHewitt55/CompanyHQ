using Abstractions.Users;

namespace CompanyHQ.Services.Auth.Dtos;

public sealed record AdminUserDto(
    Guid Id,
    string Name,
    string Surname,
    string Email,
    string ContactNumber,
    string Role)
{
    public static AdminUserDto From(User user) => new(
        user.Id,
        user.Name,
        user.Surname,
        user.Email,
        user.ContactNumber,
        user.Role);
}
