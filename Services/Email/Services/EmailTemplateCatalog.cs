using CompanyHQ.Services.Email.Contracts.Ports;

namespace CompanyHQ.Services.Email.Services;

internal sealed record EmailTemplateDefinition(
    string Key,
    string DisplayName,
    string ResourceSuffix,
    string SubjectTemplate,
    string TextTemplate,
    IReadOnlyList<string> SupportedPlaceholders,
    IReadOnlyList<string> RequiredPlaceholders,
    bool IsPriority = false);

internal static class EmailTemplateCatalog
{
    private static readonly IReadOnlyList<EmailTemplateDefinition> definitions =
    [
        new(
            EmailTemplateKeys.VerifyEmail,
            "Verify email",
            "Templates.Auth.verify-email.html",
            "Verify your email",
            "Verify your email\n\nConfirm this address to finish setting up your account. Once it's verified you can message other members.\n\nVerify email: {{link}}\n\nThis link expires in 24 hours.\n\nIf you didn't create a CompanyHQ account, ignore this email and nothing will happen.",
            ["link"],
            ["link"]),
        new(
            EmailTemplateKeys.PasswordReset,
            "Password reset",
            "Templates.Auth.password-reset.html",
            "Reset your password",
            "Reset your password\n\nWe received a request to reset the password on your CompanyHQ account. If that was you, choose a new password here:\n\n{{link}}\n\nThis link expires in 1 hour and works once. If you didn't ask for a reset, ignore this email: your password stays the same.\n\nCompanyHQ will never ask for your password by email.",
            ["link"],
            ["link"],
            IsPriority: true),
        new(
            EmailTemplateKeys.EmailChangeConfirmation,
            "Confirm email change",
            "Templates.Auth.email-change-confirmation.html",
            "Confirm your new email",
            "Confirm your new email\n\nYour CompanyHQ account asked to use this address for sign-in. Confirm it to finish the change:\n\n{{link}}\n\nThis link expires in 1 hour. Confirming signs you out on every other device, so you'll sign in there again with this address.\n\nDidn't ask for this? Ignore this email and your CompanyHQ sign-in stays as it is.",
            ["link"],
            ["link"]),
        new(
            EmailTemplateKeys.PasswordChanged,
            "Password changed notice",
            "Templates.Auth.password-changed.html",
            "Your password was changed",
            "Your password was changed\n\nThe password on your CompanyHQ account was just changed, and every other device was signed out. If this was you, there's nothing else to do.\n\nWasn't you? Reset your password from the CompanyHQ sign-in page straight away to take back control of your account.",
            [],
            []),
        new(
            EmailTemplateKeys.EmailChanging,
            "Email change notice",
            "Templates.Auth.email-changing.html",
            "Your email address is being changed",
            "Your sign-in email is changing\n\nSomeone asked to move the sign-in email on your CompanyHQ account to: {{newEmail}}\n\nNothing changes until that address is confirmed. If this was you, there's nothing else to do.\n\nWasn't you? Reset your password from the CompanyHQ sign-in page straight away. That also cancels the pending email change.",
            ["newEmail"],
            ["newEmail"]),
        new(
            EmailTemplateKeys.AccountDeletion,
            "Confirm account deletion",
            "Templates.Auth.account-deletion.html",
            "Confirm deleting your account",
            "Confirm deleting your account\n\nYou asked to delete your CompanyHQ account. Confirm here to delete it and everything stored with it:\n\n{{link}}\n\nThis link expires in 1 hour and cannot be undone once used.\n\nDidn't ask for this? Ignore this email and your account stays as it is. Consider changing your password.",
            ["link"],
            ["link"],
            IsPriority: true),
        new(
            EmailTemplateKeys.ContactMessage,
            "Contact message",
            "Templates.Contact.message.html",
            "[{{topic}}] CompanyHQ contact from {{name}}",
            "New message from {{name}}\n\nTopic: {{topic}}\nFrom: {{name}} <{{email}}>\nReference: {{reference}}\n\nMessage:\n{{message}}\n\nSent from the CompanyHQ contact form.",
            ["topic", "name", "email", "reference", "message"],
            ["topic", "name", "email", "reference", "message"]),
        new(
            EmailTemplateKeys.NotificationMessage,
            "New message notification",
            "Templates.Notification.message.html",
            "New message on CompanyHQ",
            "{{title}}\n\nSomeone sent you a message on CompanyHQ. Open the app to read it and reply. For privacy, message content is never included in email.",
            ["title"],
            ["title"]),
        new(
            EmailTemplateKeys.NotificationGeneric,
            "Generic notification",
            "Templates.Notification.generic.html",
            "{{title}}",
            "{{title}}\n\n{{body}}\n\nOpen the CompanyHQ app for the full details.",
            ["title", "body"],
            ["title", "body"]),
        new(
            EmailTemplateKeys.InboxReply,
            "Inbox reply",
            "Templates.Inbox.reply.html",
            "A reply to your message",
            "A reply to your message\n\n{{body}}\n\nOpen the chat where you wrote to us to continue the conversation.",
            ["body"],
            ["body"])
    ];

    private static readonly IReadOnlyDictionary<string, EmailTemplateDefinition> byKey =
        definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    public static IReadOnlyList<EmailTemplateDefinition> All => definitions;

    public static bool TryGet(string key, out EmailTemplateDefinition? definition) =>
        byKey.TryGetValue(key, out definition);

    public static EmailTemplateDefinition Get(string key) =>
        TryGet(key, out var definition)
            ? definition!
            : throw new KeyNotFoundException($"Unknown email template key '{key}'.");

    public static string LoadDefaultHtml(EmailTemplateDefinition definition)
    {
        var assembly = typeof(EmailTemplateCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(definition.ResourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Packaged email template '{definition.Key}' is unavailable.");

        using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);
        return reader.ReadToEnd();
    }
}
