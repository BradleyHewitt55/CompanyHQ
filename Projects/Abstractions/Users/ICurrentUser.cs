namespace Abstractions.Users;

public interface ICurrentUser
{
    User? User { get; set; }
    bool IsInRole(string role);
}
