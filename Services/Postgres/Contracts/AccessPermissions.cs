namespace CompanyHQ.Services.Postgres.Contracts;

/// <summary>Stable permission names stored on role rows and evaluated on every request.</summary>
public static class AccessPermissions
{
    public const string ManageRoles = "roles.manage";
    public const string ManageUsers = "users.manage";
    public const string ReadPrivateUsers = "users.read_private";
    public const string ManageConversations = "conversations.manage";
    public const string ManageCompany = "company.manage";
    public const string ManageModeration = "moderation.manage";
    public const string ManageMedia = "media.manage";
    public const string ManageInbox = "inbox.manage";
    public const string ManageGitHub = "github.manage";
    public const string ManageAppointments = "appointments.manage";
    public const string ManageSales = "sales.manage";
    public const string ManageShipping = "shipping.manage";

    public static IReadOnlyList<string> Client { get; } =
        [McpCapabilityCatalog.SearchArea, McpCapabilityCatalog.GetMyNotificationPreferences];

    public static IReadOnlyList<string> All { get; } =
    [
        .. Client,
        McpCapabilityCatalog.ListBlogPosts,
        McpCapabilityCatalog.GetBlogPost,
        McpCapabilityCatalog.CreateBlogPost,
        McpCapabilityCatalog.UpdateBlogPost,
        McpCapabilityCatalog.DeleteBlogPost,
        McpCapabilityCatalog.ListFaqEntries,
        McpCapabilityCatalog.GetFaqEntry,
        McpCapabilityCatalog.CreateFaqEntry,
        McpCapabilityCatalog.UpdateFaqEntry,
        McpCapabilityCatalog.DeleteFaqEntry,
        McpCapabilityCatalog.ListEmailTemplates,
        McpCapabilityCatalog.GetEmailTemplate,
        McpCapabilityCatalog.UpdateEmailTemplate,
        McpCapabilityCatalog.PreviewEmailTemplate,
        McpCapabilityCatalog.ResetEmailTemplate,
        ManageRoles, ManageUsers, ReadPrivateUsers, ManageConversations,
        ManageCompany, ManageModeration, ManageMedia, ManageInbox, ManageGitHub, ManageAppointments,
        ManageSales, ManageShipping
    ];

    public static string[] Normalize(IEnumerable<string>? permissions) =>
        (permissions ?? [])
            .Where(permission => All.Contains(permission, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
}
