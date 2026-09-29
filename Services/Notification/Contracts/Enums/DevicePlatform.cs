using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Notifications.Contracts.Enums;

/// <summary>The client platform that owns a registered push token.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<DevicePlatform>))]
public enum DevicePlatform
{
    Android,
    iOS,
    Windows,
    MacCatalyst
}
