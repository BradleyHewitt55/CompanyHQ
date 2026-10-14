using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Ports;
using CompanyHQ.Services.Sales.Services;
using CompanyHQ.Services.Shipping.Contracts.Dtos;
using CompanyHQ.Services.Shipping.Contracts.Forms;
using CompanyHQ.Services.Shipping.Contracts.Ports;
using CompanyHQ.Services.Shipping.Services;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Geo.Services;
namespace Api.Integrations;

internal sealed class SalesShippingBridge(IShippingService shipping) : ISalesShipping
{
    public async Task<SalesShippingQuote> QuoteAsync(SalesAddressDto address,IReadOnlyList<SalesLineRequest> lines,IReadOnlyList<string> couponCodes,CancellationToken cancellationToken=default)
    {
        var q=await shipping.QuoteAsync(new(ToShipping(address),lines.Select(x=>new ShippingQuoteLineRequest(
            x.ProductId,x.VariantId,x.Quantity,x.Id,x.Options,x.BundleInstanceId,x.UpsellParentLineId)).ToArray(),couponCodes),cancellationToken);
        return new(q.IsAvailable,q.Message,q.ShippingMethodId,q.ShippingMethodName,q.ShippingMethodType?.ToString(),q.EstimatedDeliveryDays,q.BaseShippingCost,q.ShippingDiscount,q.TotalShippingCost,q.AppliedShippingCouponCodes,
            q.RouteLegs.Select(x=>new SalesRouteSnapshot(x.ShippingMethodId,x.ShippingMethodName,x.OriginId,x.OriginName,x.Cost,x.DistanceKm,x.OriginLatitude,x.OriginLongitude,x.ProductIds)).ToArray(),q.DestinationLatitude,q.DestinationLongitude);
    }
    internal static ShippingAddressDto ToShipping(SalesAddressDto a)=>new(a.StreetAddress,a.ApartmentUnit,a.Suburb,a.City,a.Province,a.PostalCode,a.Country,a.Latitude,a.Longitude);
}
internal sealed class ShippingCatalogBridge(SalesCatalogService catalog) : IShippingCatalog
{
    public async Task<IReadOnlyList<ShippingCatalogProduct>> GetProductsAsync(IReadOnlyCollection<Guid> productIds,CancellationToken cancellationToken=default)=>
        (await catalog.ShippingProductsAsync(productIds,cancellationToken)).Select(x=>new ShippingCatalogProduct(x.Id,x.Name,x.Origins.Select(o=>new ShippingCatalogOrigin(o.Id,o.Name,o.Address?.Latitude,o.Address?.Longitude)).ToArray())).ToArray();
}
internal sealed class ShippingDiscountBridge(SalesBasketPricing pricing) : IShippingDiscountPolicy
{
    public async Task<ShippingDiscountResult> EvaluateAsync(ShippingDiscountContext context,CancellationToken cancellationToken=default)
    {
        var d=await pricing.ShippingDiscountAsync(new(context.BaseShippingCost,context.Lines.Select(x=>new SalesLineRequest(
            x.Id is null || x.Id==Guid.Empty?Guid.CreateVersion7():x.Id.Value,x.ProductId,x.VariantId,x.Quantity,x.Options,x.BundleInstanceId,x.UpsellParentLineId)).ToArray(),context.CouponCodes,context.DestinationLatitude,context.DestinationLongitude,
            context.Origins.Select(x=>new SalesDiscountOrigin(x?.Latitude,x?.Longitude)).ToArray()),cancellationToken);
        return new(d.Amount,d.CouponCodes);
    }
}
internal sealed class SalesCustomerBridge(IPersistence db) : ISalesCustomer
{
    public async Task<SalesCustomer?> GetAsync(Guid userId,CancellationToken cancellationToken=default)=>
        await db.Query<User>().Where(x=>x.Id==userId).Select(x=>new SalesCustomer(x.FirstName ?? "",x.LastName ?? "",x.Email,null)).FirstOrDefaultAsync(cancellationToken);
}
internal sealed class ShippingGeocoderBridge(IGeoService geo) : IShippingGeocoder
{
    public async Task<ShippingCoordinates?> LocateAsync(ShippingAddressDto address,CancellationToken cancellationToken=default)
    {
        var query=string.Join(", ",new[]{address.StreetAddress,address.Suburb,address.City,address.Province,address.PostalCode,address.Country}.Where(x=>!string.IsNullOrWhiteSpace(x)));
        if(query.Length==0)return null;
        var place=(await geo.SearchAreasAsync(query,1,cancellationToken)).FirstOrDefault();
        return place is null?null:new(place.Latitude,place.Longitude);
    }
}

internal sealed class ShippingAddressDirectoryBridge(IGeoService geo) : IShippingAddressDirectory
{
    public async Task<IReadOnlyList<ShippingAddressCandidateDto>> SearchAsync(string query,int limit,CancellationToken cancellationToken=default)=>
        (await geo.SearchAreasAsync(query,limit,cancellationToken)).Select(Map).ToArray();
    public async Task<ShippingAddressCandidateDto?> HydrateAsync(string reference,CancellationToken cancellationToken=default)
    {
        var parts=reference.Split(':');
        if(parts.Length!=3 || parts[0]!="area" || !double.TryParse(parts[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var lat) || !double.TryParse(parts[2],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var lon) || !double.IsFinite(lat) || !double.IsFinite(lon) || lat is <-90 or >90 || lon is <-180 or >180)throw new ArgumentException("Invalid area reference.");
        var area=await geo.DescribeAreaAsync(lat,lon,cancellationToken);return area is null?null:Map(area);
    }
    private static ShippingAddressCandidateDto Map(CompanyHQ.Services.Geo.Contracts.Dtos.AreaDto area)=>
        new(FormattableString.Invariant($"area:{area.Latitude:R}:{area.Longitude:R}"),area.DisplayName,area.Precision.ToString(),new(City:area.Locality,Province:area.Region,Country:area.CountryCode,Latitude:area.Latitude,Longitude:area.Longitude));
}

internal sealed class SalesProfitShippingBridge(IShippingService shipping) : ISalesProfitShipping
{
    public async Task<IReadOnlyList<SalesProfitShippingMethod>> GetMethodsAsync(CancellationToken cancellationToken=default)
    {
        var first=await shipping.ListMethodsAsync(new(){PageSize=100},cancellationToken:cancellationToken);
        var methods=first.Items.ToList();
        if(first.Total>100)methods.AddRange((await shipping.ListMethodsAsync(new(){Page=2,PageSize=100},cancellationToken:cancellationToken)).Items);
        if(first.Total>200)throw new ArgumentException("Too many shipping methods for exact profit scenarios.");
        return methods.Select(x=>new SalesProfitShippingMethod(x.Id,x.Code,x.Cost)).ToArray();
    }
}
