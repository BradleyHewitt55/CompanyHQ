namespace Services.Auth;

public static class AuthRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string Viewer = "Viewer";
    public static readonly string[] All = [Viewer, Member, Admin, Owner];
}
