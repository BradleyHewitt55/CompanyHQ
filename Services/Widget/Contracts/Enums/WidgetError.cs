namespace CompanyHQ.Services.Widget.Contracts.Enums;

/// <summary>Why a widget submission did not go through.</summary>
public enum WidgetError
{
    None,

    /// <summary>No such conversation, or the token is not its own.</summary>
    NotFound,

    /// <summary>The conversation is resolved and takes no further messages.</summary>
    Closed,

    /// <summary>The message is blank once trimmed.</summary>
    Invalid,

    /// <summary>The submission names a source this deployment does not allow.</summary>
    UnknownSource,

    /// <summary>Nothing is plugged in or connected to take this kind of submission.</summary>
    Unavailable,

    /// <summary>The destination was reached and did not accept it.</summary>
    Failed
}
