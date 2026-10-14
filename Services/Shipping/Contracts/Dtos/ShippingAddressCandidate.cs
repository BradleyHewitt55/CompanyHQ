namespace CompanyHQ.Services.Shipping.Contracts.Dtos;
/// <summary>Address or area candidate with explicit provider precision.</summary>
public sealed record ShippingAddressCandidateDto(string Reference,string DisplayName,string Precision,ShippingAddressDto Address);
