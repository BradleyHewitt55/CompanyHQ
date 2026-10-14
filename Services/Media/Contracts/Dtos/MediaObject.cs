using System.Text.Json.Serialization;

using CompanyHQ.Media.Contracts.Enums;

namespace CompanyHQ.Media.Contracts.Dtos;

/// <summary>The client-safe view of a ready media object.</summary>
[JsonDerivedType(typeof(AdminMediaObjectDto))]
public record ClientMediaObjectDto(
    Guid Id,
    MediaScope Scope,
    string ContentType,
    long SizeBytes,
    MediaStatus Status,
    string DownloadUrl);

/// <summary>The operator view, adding ownership, storage key, and audit information.</summary>
public sealed record AdminMediaObjectDto(
    Guid Id,
    MediaScope Scope,
    string ContentType,
    long SizeBytes,
    MediaStatus Status,
    string DownloadUrl,
    Guid OwnerUserId,
    string Key,
    DateTimeOffset UpdatedAt)
    : ClientMediaObjectDto(Id, Scope, ContentType, SizeBytes, Status, DownloadUrl);
