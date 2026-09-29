using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Users.Contracts.Enums;

/// <summary>
/// Serialised as text on the wire as well as in the column — an ordinal on the wire silently
/// changes meaning the day a role is inserted into this list.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<UserRole>))]
public enum UserRole
{
    Client,
    Admin
}
