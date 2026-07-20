namespace Abstractions.Users;

// One shared rule so every entry point (web form, CLI prompt, seeding) enforces
// the same policy. Length-only per NIST 800-63B; the cap bounds hashing cost.
public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static bool IsValid(string password)
        => password.Length is >= MinimumLength and <= MaximumLength;

    public static string Requirement
        => $"Password must be {MinimumLength}-{MaximumLength} characters.";
}
