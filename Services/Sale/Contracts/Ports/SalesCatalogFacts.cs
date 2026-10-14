using CompanyHQ.Services.Sales.Contracts.Dtos;
namespace CompanyHQ.Services.Sales.Contracts.Ports;
/// <summary>Catalog facts translated by host bridges, with no Shipping dependency.</summary>
public sealed record SalesCatalogShippingProduct(Guid Id,string Name,IReadOnlyList<SalesSupplierOrigin> Origins);
public sealed record SalesSupplierOrigin(Guid Id,string Name,SalesAddressDto? Address);
