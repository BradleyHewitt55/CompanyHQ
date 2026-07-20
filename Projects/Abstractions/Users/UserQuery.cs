namespace Abstractions.Users;

public sealed record UserQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Role = null);
