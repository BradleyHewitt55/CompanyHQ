using System.Text.Json.Serialization;

namespace CompanyHQ.ServiceCatalog.Contracts;

/// <summary>Portable catalog wire enums serialize as text and reject integer input.</summary>
public sealed class CatalogStringEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false)
    where TEnum : struct, Enum;
