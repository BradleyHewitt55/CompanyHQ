using System.Text.Json.Serialization;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Company.Contracts.Dtos;

[JsonConverter(typeof(CatalogStringEnumConverter<ServiceChangeError>))]
public enum ServiceChangeError { None, NotFound, CoreService, DependencyUnavailable, RequiredByEnabledService, UnknownProvider, UnsafeProviderChange, ConcurrentChange }
