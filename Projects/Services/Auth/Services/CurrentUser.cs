using Abstractions.Users;

namespace CompanyHQ.Services.Auth.Services;

public sealed class CurrentUser : ICurrentUser
{
    public User? User { get; set; }

    public bool IsInRole(string role)
        => User?.Role.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
}
