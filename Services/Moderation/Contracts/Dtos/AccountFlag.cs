using System.Text.Json.Serialization;

using CompanyHQ.Services.Moderation.Contracts.Enums;

namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>The narrow account-flag identity used by non-operator consumers.</summary>
[JsonDerivedType(typeof(AdminAccountFlagDto))]
public record ClientAccountFlagDto(
    Guid Id,
    Guid UserId,
    string Kind,
    AccountFlagStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>The operator's account-flag queue shape, including detector evidence and audit fields.</summary>
public sealed record AdminAccountFlagDto(
    Guid Id,
    Guid UserId,
    string Kind,
    AccountFlagStatus Status,
    DateTimeOffset CreatedAt,
    FlagOrigin RaisedBy,
    Guid? RaisedByUserId,
    string Evidence,
    Guid? ClearedByUserId,
    DateTimeOffset? ClearedAt,
    string? ReviewNote)
    : ClientAccountFlagDto(Id, UserId, Kind, Status, CreatedAt);
