namespace CompanyHQ.Services.Email.Contracts.Ports;

/// <summary>The controlled template catalog. A caller can only send one of these.</summary>
public static class EmailTemplateKeys
{
    public const string VerifyEmail = "auth.verify-email";
    public const string PasswordReset = "auth.password-reset";
    public const string EmailChangeConfirmation = "auth.email-change-confirmation";
    public const string PasswordChanged = "auth.password-changed";
    public const string EmailChanging = "auth.email-changing";
    public const string AccountDeletion = "auth.account-deletion";
    public const string ContactMessage = "contact.message";
    public const string NotificationMessage = "notification.message";
    public const string NotificationGeneric = "notification.generic";
    public const string InboxReply = "inbox.reply";
}
