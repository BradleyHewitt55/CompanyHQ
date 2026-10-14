using System.Text.Json.Serialization;
using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Sales.Contracts.Enums;

/// <summary>What a product is priced from: its own variants, or the products it bundles.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ProductType>))]
public enum ProductType
{
    Standard,
    Variant,
    Bundle
}
