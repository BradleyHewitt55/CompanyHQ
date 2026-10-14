using CompanyHQ.Sales.Contracts.Forms;
namespace CompanyHQ.Sales.Contracts.Ports;
/// <summary>Promotion evaluation facts, kept independent of Shipping contracts.</summary>
public sealed record SalesShippingDiscountRequest(decimal BaseCost,IReadOnlyList<SalesLineRequest> Lines,IReadOnlyList<string> CouponCodes,double? DestinationLatitude,double? DestinationLongitude,IReadOnlyList<SalesDiscountOrigin> Origins);
public sealed record SalesDiscountOrigin(double? Latitude,double? Longitude);
public sealed record SalesShippingDiscount(decimal Amount,IReadOnlyList<string> CouponCodes);
