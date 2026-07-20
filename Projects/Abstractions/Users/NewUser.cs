namespace Abstractions.Users;

public sealed record NewUser(
    string Name,
    string Surname,
    string Email,
    string ContactNumber,
    string Password);
