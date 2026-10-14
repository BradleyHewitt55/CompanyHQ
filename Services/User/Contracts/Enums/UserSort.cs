using System.Text.Json.Serialization;

namespace CompanyHQ.Users.Contracts.Enums;

/// <summary>Fields the user admin list may sort by.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserSort>))]
public enum UserSort
{
    CreatedAt,
    Email,
    Name,
    Role
}
