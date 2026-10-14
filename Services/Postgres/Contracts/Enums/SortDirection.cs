using System.Text.Json.Serialization;

namespace CompanyHQ.Postgres.Contracts.Enums;

/// <summary>Direction for a typed list sort.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    Ascending,
    Descending
}
