namespace CompanyHQ.Services.Postgres.Contracts;

/// <summary>Names of API actions explicitly exposed to MCP.</summary>
public static class McpCapabilityCatalog
{
    public const string SearchArea = "search_area";
    public const string ListBlogPosts = "list_blog_posts";
    public const string GetBlogPost = "get_blog_post";
    public const string CreateBlogPost = "create_blog_post";
    public const string UpdateBlogPost = "update_blog_post";
    public const string DeleteBlogPost = "delete_blog_post";
    public const string ListFaqEntries = "list_faq_entries";
    public const string GetFaqEntry = "get_faq_entry";
    public const string CreateFaqEntry = "create_faq_entry";
    public const string UpdateFaqEntry = "update_faq_entry";
    public const string DeleteFaqEntry = "delete_faq_entry";
    public const string ListEmailTemplates = "list_email_templates";
    public const string GetEmailTemplate = "get_email_template";
    public const string UpdateEmailTemplate = "update_email_template";
    public const string PreviewEmailTemplate = "preview_email_template";
    public const string ResetEmailTemplate = "reset_email_template";
    public const string GetMyNotificationPreferences = "get_my_notification_preferences";

    public static IReadOnlyList<string> All { get; } =
    [
        SearchArea, ListBlogPosts, GetBlogPost, CreateBlogPost, UpdateBlogPost, DeleteBlogPost,
        ListFaqEntries, GetFaqEntry, CreateFaqEntry, UpdateFaqEntry, DeleteFaqEntry,
        ListEmailTemplates, GetEmailTemplate, UpdateEmailTemplate, PreviewEmailTemplate,
        ResetEmailTemplate, GetMyNotificationPreferences
    ];

    public static IReadOnlySet<string> Write { get; } = new HashSet<string>(
    [
        CreateBlogPost, UpdateBlogPost, DeleteBlogPost, CreateFaqEntry, UpdateFaqEntry,
        DeleteFaqEntry, UpdateEmailTemplate, ResetEmailTemplate
    ], StringComparer.Ordinal);

    public static bool IsKnown(string? capability) =>
        capability is not null && All.Contains(capability, StringComparer.Ordinal);

    public static IReadOnlyList<string> NormalizeDisabled(IEnumerable<string>? capabilities) =>
        (capabilities ?? []).Where(IsKnown).Distinct(StringComparer.Ordinal)
            .OrderBy(capability => capability, StringComparer.Ordinal).ToArray();

    // Kept only for accounts created before an access role was assigned.
    public static IReadOnlyList<string> Effective(string? role, IEnumerable<string>? disabled) =>
        All.Where(capability =>
            (string.Equals(role, "Admin", StringComparison.Ordinal)
                || AccessPermissions.Client.Contains(capability, StringComparer.Ordinal))
            && !(disabled ?? []).Contains(capability, StringComparer.Ordinal)).ToArray();
}
