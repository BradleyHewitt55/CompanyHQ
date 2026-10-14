using System.Text.Json.Serialization;

using CompanyHQ.Postgres.Contracts.Serialization;

namespace CompanyHQ.Media.Contracts.Enums;

/// <summary>The upload lifecycle state stored as text so enum insertion cannot change old rows.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<MediaStatus>))]
public enum MediaStatus
{
    Pending,
    Ready,
    Failed,
    Deleted
}
