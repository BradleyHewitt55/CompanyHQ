namespace CompanyHQ.Users.Contracts.Models;

/// <summary>
/// Credentials for the seeded administrator, from the <see cref="SectionName"/> configuration
/// section (<c>Seed__Admin__Email</c> / <c>Seed__Admin__Password</c> in the environment).
/// </summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "Seed:Admin";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string FirstName { get; set; } = "CompanyHQ";

    public string LastName { get; set; } = "Admin";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
