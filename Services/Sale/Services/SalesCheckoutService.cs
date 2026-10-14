using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Models;
using CompanyHQ.Services.Sales.Contracts.Ports;
namespace CompanyHQ.Services.Sales.Services;
/// <summary>Coordinates fresh basket pricing with server shipping quotes.</summary>
public sealed class SalesCheckoutService(SalesBasketPricing basket,SalesCatalogService catalog,SalesOptions options,ISalesShipping? shipping=null) : ISalesCheckoutService
{
    public async Task<PricedSaleDto> PriceAsync(PriceSaleRequest request,CancellationToken ct=default)=>(await CalculateAsync(request,false,ct)).Price;
    internal async Task<PricedBasket> CalculateAsync(PriceSaleRequest request,bool placing,CancellationToken ct,bool staff=false)
    {
        var priced=await basket.CalculateAsync(request,ct,staff);
        if(request.ShippingAddress is null)
        {if((await catalog.Coupons(ct)).Any(c=>c.Definition.AppliesToShipping && (request.CouponCodes ?? []).Any(code=>SalesRules.Normalize(code)==c.Definition.Code)))throw new ArgumentException("Entered shipping coupons require a shipping address.");
            if(placing && options.RequireShippingAddress)throw new ArgumentException("A shipping address is required.");return priced;}
        if(shipping is null)throw new ArgumentException("Shipping is not configured.");
        var quote=await shipping.QuoteAsync(request.ShippingAddress,priced.Cart.Lines,request.CouponCodes ?? [],ct);
        if(placing && !quote.IsAvailable)throw new ArgumentException(quote.Message ?? "Shipping is unavailable.");
        if(quote.BaseCost<0 || quote.Discount<0 || quote.Discount>quote.BaseCost || quote.TotalCost!=quote.BaseCost-quote.Discount)throw new InvalidOperationException("Invalid shipping adapter quote.");
        var shippingCoupons=(await catalog.Coupons(ct)).Where(c=>quote.CouponCodes.Contains(c.Definition.Code))
            .Select(c=>new SalesAppliedCouponDto(c.Id,c.Definition.Code,c.Definition.DiscountType,c.Definition.DiscountValue,c.Definition.AllowsCombination,true,quote.Discount)).ToArray();
        return priced with {Shipping=quote,Price=priced.Price with {Coupons=priced.Price.Coupons.Concat(shippingCoupons).ToArray(),ShippingCost=quote.BaseCost,ShippingDiscount=quote.Discount,
            DiscountAmount=priced.Price.DiscountAmount+quote.Discount,TotalAmount=SalesRules.Money(priced.Price.TotalAmount+quote.TotalCost),
            Shipping=new(quote.IsAvailable,quote.Message,quote.MethodName,quote.EstimatedDeliveryDays)}};
    }
}
