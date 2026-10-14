namespace CompanyHQ.Messages.Contracts.Enums;

/// <summary>Operations a host may further restrict. Policies never grant participant or staff access.</summary>
public enum ConversationAction
{
    Read,
    Send,
    Close,
    Delete
}
