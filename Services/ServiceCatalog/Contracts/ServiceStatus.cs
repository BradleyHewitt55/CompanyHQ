using System.Text.Json.Serialization;

namespace CompanyHQ.ServiceCatalog.Contracts;

[JsonConverter(typeof(CatalogStringEnumConverter<ServiceStatus>))]
public enum ServiceStatus { Disabled, EnabledNotConfigured, EnabledReady }
