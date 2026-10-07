using CompanyHQ.Services.Messages.Contracts.Enums;

namespace CompanyHQ.Services.Messages.Contracts.Models;

/// <summary>Per-deployment choice of who may close and who may delete a conversation.</summary>
public sealed class MessageAccessOptions
{
    public const string SectionName = "Message:Access";

    public ConversationAccess Close { get; set; } = ConversationAccess.Both;

    public ConversationAccess Delete { get; set; } = ConversationAccess.Both;

    /// <summary>Checks the current account in the service, including in-process callers.</summary>
    public bool RequireVerifiedEmail { get; set; } = true;

    public void Validate()
    {
        if (!Enum.IsDefined(Close) || !Enum.IsDefined(Delete))
        {
            throw new InvalidOperationException("Message:Access:Close and Delete must be Both, Users, or Admins.");
        }
    }

    /// <summary>Whether <paramref name="access"/> lets this caller act; a manager counts as an admin.</summary>
    public static bool Allows(ConversationAccess access, bool isManager) => access switch
    {
        ConversationAccess.Users => !isManager,
        ConversationAccess.Admins => isManager,
        _ => true
    };
}
