namespace CompanyHQ.Inbox.Contracts.Enums;

/// <summary>Why an inbox operation did not happen.</summary>
public enum InboxError
{
    None,

    /// <summary>No such channel or thread, or the caller may not know it exists.</summary>
    NotFound,

    /// <summary>The caller's role lacks <c>inbox.manage</c> or the channel's own permission.</summary>
    Forbidden,

    /// <summary>The thread is resolved and takes no further messages.</summary>
    Resolved,

    /// <summary>The message is blank once trimmed.</summary>
    Invalid,

    /// <summary>The channel cannot do this to a thread at all.</summary>
    Unsupported,

    /// <summary>The channel's own backend could not be reached or is not connected.</summary>
    Unavailable
}
