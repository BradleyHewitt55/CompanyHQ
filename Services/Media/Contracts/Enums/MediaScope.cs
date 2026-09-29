using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Media.Contracts.Enums;

/// <summary>The consuming feature that owns a media object's access policy.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<MediaScope>))]
public enum MediaScope
{
    /// <summary>A message attachment, readable only through the Message service's access policy.</summary>
    Message,

    /// <summary>A profile image, public once ready.</summary>
    User
}
