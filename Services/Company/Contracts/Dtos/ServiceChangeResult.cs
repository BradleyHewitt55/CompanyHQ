using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Company.Contracts.Dtos;


public sealed record ServiceChangeResult(ServiceCatalogDto? Service, ServiceChangeError Error = ServiceChangeError.None,
    string? Detail = null);
