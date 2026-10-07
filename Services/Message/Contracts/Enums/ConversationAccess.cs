namespace CompanyHQ.Services.Messages.Contracts.Enums;

/// <summary>Who a deployment lets close or delete a conversation.</summary>
public enum ConversationAccess
{
    /// <summary>Participants and conversation managers. The default.</summary>
    Both,

    /// <summary>Only participants who do not hold <c>conversations.manage</c>.</summary>
    Users,

    /// <summary>Only accounts holding <c>conversations.manage</c>, participant or not.</summary>
    Admins
}
