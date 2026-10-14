using CompanyHQ.Rss.Contracts.Enums;

namespace CompanyHQ.Rss.Contracts.Dtos;

/// <summary>Why a fetch or a discovery produced nothing.</summary>
/// <remarks>
/// <see cref="Message"/> is fixed per code and safe to log or show an operator: it never carries a
/// response body, a resolved address, or anything else a publisher controls.
/// </remarks>
public sealed record FeedErrorDto(
    FeedErrorCode Code,
    string Message,
    /// <summary>The publisher's status, for <see cref="FeedErrorCode.HttpStatus"/> only.</summary>
    int? HttpStatus = null,
    /// <summary>True when the same request may well succeed later; false when repeating it is pointless.</summary>
    bool Transient = false);
