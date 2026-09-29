using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Postgres.Contracts.Serialization;

/// <summary>Serializes an enum as text and refuses integer JSON values.</summary>
public sealed class StrictStringEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;
