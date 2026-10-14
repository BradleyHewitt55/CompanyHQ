namespace CompanyHQ.Users.Contracts.Models;

/// <summary>Joins a person's stored first and last name into the single line some surfaces show.</summary>
public static class UserNames
{
    public static string? Join(string? firstName, string? lastName)
    {
        var joined = $"{firstName?.Trim()} {lastName?.Trim()}".Trim();
        return joined.Length == 0 ? null : joined;
    }
}
