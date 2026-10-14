using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Forms;
namespace CompanyHQ.Sales.Contracts.Ports;
/// <summary>Optional shipping capability. Physical checkout with an address fails when it is absent.</summary>
public interface ISalesShipping
{
    Task<SalesShippingQuote> QuoteAsync(SalesAddressDto address, IReadOnlyList<SalesLineRequest> lines,
        IReadOnlyList<string> couponCodes, CancellationToken cancellationToken = default);
}
/// <summary>Server quotation, persisted in full as an immutable order snapshot.</summary>
public sealed record SalesShippingQuote(bool IsAvailable, string? Message, Guid? MethodId, string? MethodName,
    string? MethodKind, int? EstimatedDeliveryDays, decimal BaseCost, decimal Discount, decimal TotalCost,
    IReadOnlyList<string> CouponCodes, IReadOnlyList<SalesRouteSnapshot> Routes, double? DestinationLatitude, double? DestinationLongitude);
public sealed record SalesRouteSnapshot(Guid MethodId, string MethodName, Guid OriginId, string OriginName, decimal Cost,
    double? DistanceKm, double? Latitude, double? Longitude, IReadOnlyList<Guid> ProductIds);
