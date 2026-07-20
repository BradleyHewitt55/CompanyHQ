namespace Abstractions.Users;

public sealed record UserSave(
    Guid? Id,
    string Name,
    string Surname,
    string Email,
    string ContactNumber,
    string Role,
    string? Password);
