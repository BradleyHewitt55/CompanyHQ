using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Models;
using CompanyHQ.Sales.Contracts.Ports;
namespace CompanyHQ.Sales.Services;

/// <summary>Prices browser cart selections and promotions without invoking shipping or payment.</summary>
public sealed class SalesBasketPricing(IServiceAvailability? availability, IPersistence db,SalesCatalogService catalog) : ISalesBasketPricing
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public SalesBasketPricing(IPersistence db,SalesCatalogService catalog) : this(null, db, catalog) { }

    public CartDto NormalizeCart(IReadOnlyList<SalesLineRequest> lines)=>SalesRules.NormalizeCart(lines);
    internal async Task<PricedBasket> CalculateAsync(PriceSaleRequest request,CancellationToken ct,bool staff=false)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        SalesRules.Validate(request);if(request.ShippingAddress is not null) SalesRules.Validate(request.ShippingAddress);
        var initial=SalesRules.NormalizeCart(request.Lines);
        var graph=await catalog.Graph(initial.Lines.Select(x=>x.ProductId),ct);
        var resolved=initial.Lines.Select(line=>line with {VariantId=Resolve(graph[line.ProductId],line).Id,Options=SalesRules.Selections(Resolve(graph[line.ProductId],line).Options)}).ToArray();
        var cart=SalesRules.NormalizeCart(resolved);
        cart=cart with {RemovedLineIds=initial.RemovedLineIds.Concat(cart.RemovedLineIds).Distinct().ToArray()};
        var currencies=cart.Lines.Select(x=>graph[x.ProductId].Currency).Distinct().ToArray();
        if(currencies.Length!=1)throw new ArgumentException("Checkout requires one currency and at least one parent line.");
        if(!staff && cart.Lines.Any(x=>!graph[x.ProductId].IsVisible))throw new ArgumentException("A product is not available.");
        var coupons=await catalog.Coupons(ct);var pricing=new CatalogPricing(graph,coupons,catalog.Today,catalog.PricingOptions);
        var rawSubtotal=cart.Lines.Sum(x=>pricing.Price(x.ProductId,Resolve(graph[x.ProductId],x),false).Selling*x.Quantity);
        var parents=cart.Lines.Where(x=>!x.UpsellParentLineId.HasValue).Select(x=>x.ProductId).Distinct().ToArray();
        var relationships=await db.Query<UpsellOffer>().Where(x=>parents.Contains(x.ParentProductId)).Take(1001).ToListAsync(ct);
        if(relationships.Count>1000)throw new ArgumentException("Upsell relationship limit exceeded.");
        var extra=(await db.Query<SalesSettings>().FirstOrDefaultAsync(x=>x.Id==SalesPromotionService.SettingsId,ct))?.UpsellExtraDiscountPercent ?? 0;
        var priced=new List<AdminSalesLineDto>();
        foreach(var line in cart.Lines)
        {
            var p=graph[line.ProductId];var variant=Resolve(p,line);var price=pricing.Price(line.ProductId,variant,true,rawSubtotal);
            var entries=price.Discounts.ToList();var final=price.Effective;Guid? parentProduct=null;
            if(line.UpsellParentLineId.HasValue)
            {
                var parent=cart.Lines.Single(x=>x.Id==line.UpsellParentLineId);
                if(parent.UpsellParentLineId.HasValue)throw new ArgumentException("An upsell cannot offer further upsells.");
                parentProduct=parent.ProductId;
                var row=relationships.SingleOrDefault(x=>x.ParentProductId==parent.ProductId && x.ProductId==line.ProductId);
                if(row is null)throw new ArgumentException("Upsell relationship not found.");
                var offer=SalesRules.Read<SaveUpsellRequest>(row.DefinitionJson);
                var matching=CatalogPricing.ResolveUpsell(p,Resolve(graph[parent.ProductId],parent),offer.SyncVariants);
                if(matching is null || matching.Id!=variant.Id)throw new ArgumentException("Upsell variant is incompatible with its parent.");
                if(cart.Lines.Where(x=>x.ProductId==line.ProductId && x.UpsellParentLineId==parent.Id).Sum(x=>x.Quantity)>offer.Quantity)throw new ArgumentException("Upsell quantity exceeds the relationship quantity.");
                var discount=Math.Min(final,SalesRules.Discount(price.Selling,offer.ValueType,offer.Value)+SalesRules.Discount(price.Selling,DiscountType.Percentage,extra));
                final=SalesRules.Money(final-discount);
                if(discount>0)entries.Add(new(SalesDiscountSource.Upsell,"Relationship and company upsell",null,discount,offer.ValueType,offer.Value));
            }
            var name=string.Join(" / ",(p.Options ?? []).Select(o=>variant.Options!.First(x=>SalesRules.Normalize(x.Key)==SalesRules.Normalize(o.Name)).Value));
            var total=SalesRules.Money(final*line.Quantity);var cost=SalesRules.Money(price.Cost*line.Quantity);
            var discounts=entries.Select(e=>e with {DiscountAmount=SalesRules.Money(e.DiscountAmount*line.Quantity)}).ToArray();
            priced.Add(new(line.Id,line.ProductId,variant.Id,parentProduct,p.Name,name,variant.Sku,p.ImageUrls?.FirstOrDefault(),line.Quantity,
                price.Selling,SalesRules.Money(price.Selling*line.Quantity),final,total,SalesRules.Money(price.Selling*line.Quantity-total),
                string.Join(", ",discounts.Select(x=>x.Label)),discounts,price.Cost,cost,SalesRules.Money(total-cost),total>0?SalesRules.Money((total-cost)/total*100):0));
        }
        var codes=(request.CouponCodes ?? []).Select(SalesRules.Normalize).Distinct().ToArray();
        if(codes.Any(x=>x.Length is <1 or >100))throw new ArgumentException("Invalid coupon code.");
        foreach(var code in codes)
        {
            var rule=coupons.SingleOrDefault(c=>c.Definition.Code==code);
            if(rule is null || !SalesRules.Valid(rule.Definition,catalog.Today) || (rule.Definition.MinimumPurchase ?? 0)>rawSubtotal ||
                !cart.Lines.Any(l=>SalesRules.Targets(rule.Definition,l.ProductId,graph[l.ProductId].CategoryIds)))
                throw new ArgumentException($"Coupon '{code}' is invalid or ineligible.");
        }
        var selected=coupons.Where(c=>codes.Contains(c.Definition.Code) && !c.Definition.IsAutomatic && !c.Definition.AppliesToShipping && SalesRules.Valid(c.Definition,catalog.Today) && (c.Definition.MinimumPurchase ?? 0)<=rawSubtotal).ToArray();
        var allocations=selected.Select(c=>new {Coupon=c,Indexes=priced.Select((p,i)=>(p,i)).Where(x=>SalesRules.Targets(c.Definition,x.p.ProductId,graph[x.p.ProductId].CategoryIds)).Select(x=>x.i).ToArray()})
            .Select(x=>new CouponAllocation(x.Coupon,x.Indexes,SalesRules.Discount(x.Indexes.Sum(i=>priced[i].FinalTotalPrice),x.Coupon.Definition.DiscountType,x.Coupon.Definition.DiscountValue))).Where(x=>x.Amount>0).ToArray();
        var stack=allocations.Where(x=>x.Coupon.Definition.AllowsCombination).OrderBy(x=>x.Coupon.Definition.Code,StringComparer.Ordinal).ToArray();
        var single=allocations.OrderByDescending(x=>x.Amount).ThenBy(x=>x.Coupon.Definition.Code,StringComparer.Ordinal).FirstOrDefault();
        var chosen=SimulatedAmount(stack,priced)>=(single?.Amount ?? 0)?stack:single is null?[]:[single];
        decimal requestedDiscount=0;
        foreach(var allocation in chosen)
        {
            var basis=allocation.Indexes.Sum(i=>priced[i].FinalTotalPrice);var remaining=Math.Min(basis,allocation.Amount);var remainingBasis=basis;
            foreach(var index in allocation.Indexes)
            {
                var line=priced[index];var amount=remainingBasis==line.FinalTotalPrice?remaining:Math.Min(remaining,SalesRules.Money(remaining*line.FinalTotalPrice/remainingBasis));
                amount=Math.Min(amount,line.FinalTotalPrice);remaining-=amount;remainingBasis-=line.FinalTotalPrice;requestedDiscount+=amount;
                var total=SalesRules.Money(line.FinalTotalPrice-amount);var c=allocation.Coupon.Definition;
                var discounts=line.Discounts.Append(new SalesLineDiscountDto(SalesDiscountSource.Coupon,c.Code,c.Code,amount,c.DiscountType,c.DiscountValue)).Where(x=>x.DiscountAmount>0).ToArray();
                priced[index]=line with {FinalTotalPrice=total,FinalUnitPrice=SalesRules.Money(total/line.Quantity),LineDiscountAmount=line.LineDiscountAmount+amount,
                    Discounts=discounts,DiscountSource=string.Join(", ",discounts.Select(x=>x.Label)),ProfitAmount=total-line.CostTotalUsed,ProfitMargin=total>0?SalesRules.Money((total-line.CostTotalUsed)/total*100):0};
            }
        }
        var applied=priced.SelectMany(x=>x.Discounts).Where(x=>x.CouponCode is not null).GroupBy(x=>x.CouponCode!)
            .Select(g=> {var c=coupons.Single(x=>x.Definition.Code==g.Key);return new SalesAppliedCouponDto(c.Id,g.Key,c.Definition.DiscountType,c.Definition.DiscountValue,c.Definition.AllowsCombination,false,g.Sum(x=>x.DiscountAmount));}).ToArray();
        var subtotal=priced.Sum(x=>x.OriginalTotalPrice);var discountAmount=priced.Sum(x=>x.LineDiscountAmount);
        var priceDto=new PricedSaleDto(currencies[0],priced.Select(ClientLine).ToArray(),applied,priced.Sum(x=>x.Quantity),subtotal,discountAmount,requestedDiscount,0,0,0,0,SalesRules.Money(subtotal-discountAmount),null,cart.RemovedLineIds);
        return new(cart,priceDto,priced,null);
    }
    internal static SaveVariantRequest Resolve(SaveProductRequest p,SalesLineRequest line)
    {
        var selections=SalesRules.Selections(line.Options);
        var variant=line.VariantId.HasValue?p.Variants.FirstOrDefault(x=>x.Id==line.VariantId):p.Variants.FirstOrDefault(v=>SalesRules.Matches(v,selections));
        if(variant is null || !SalesRules.Matches(variant,selections))throw new ArgumentException("Product variant or selections not found.");
        return variant;
    }
    internal static ClientSalesLineDto ClientLine(AdminSalesLineDto l)=>new(l.Id,l.ProductId,l.VariantId,l.UpsellParentProductId,l.ProductName,l.VariantName,l.Sku,l.ImageUrl,l.Quantity,l.OriginalUnitPrice,l.OriginalTotalPrice,l.FinalUnitPrice,l.FinalTotalPrice,l.LineDiscountAmount,l.DiscountSource,l.Discounts);
    /// <summary>Server-evaluated shipping coupon selection, used by a narrow host adapter.</summary>
    public async Task<SalesShippingDiscount> ShippingDiscountAsync(SalesShippingDiscountRequest request,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        if(request.BaseCost<=0)return new(0,[]);
        var basket=await CalculateAsync(new(request.Lines),ct);var graph=await catalog.Graph(basket.Cart.Lines.Select(x=>x.ProductId),ct);
        var codes=request.CouponCodes.Select(SalesRules.Normalize).ToHashSet();
        var allCoupons=await catalog.Coupons(ct);
        foreach(var code in codes)
        {
            var c=allCoupons.SingleOrDefault(x=>x.Definition.Code==code);
            if(c is null || !SalesRules.Valid(c.Definition,catalog.Today))throw new ArgumentException($"Coupon '{code}' is invalid.");
            if(c.Definition.AppliesToShipping && ((c.Definition.MinimumPurchase ?? 0)>basket.Price.Lines.Sum(x=>x.FinalTotalPrice) ||
                !basket.Cart.Lines.Any(l=>SalesRules.Targets(c.Definition,l.ProductId,graph[l.ProductId].CategoryIds)) || !InRadius(c.Definition,request)))
                throw new ArgumentException($"Shipping coupon '{code}' is ineligible for this route.");
        }
        var candidates=allCoupons.Where(c=>c.Definition.AppliesToShipping && (c.Definition.IsAutomatic || codes.Contains(c.Definition.Code)) &&
            SalesRules.Valid(c.Definition,catalog.Today) && (c.Definition.MinimumPurchase ?? 0)<=basket.Price.Lines.Sum(x=>x.FinalTotalPrice) &&
            basket.Cart.Lines.Any(l=>SalesRules.Targets(c.Definition,l.ProductId,graph[l.ProductId].CategoryIds)) && InRadius(c.Definition,request))
            .OrderBy(c=>c.Definition.DiscountType==DiscountType.FreeShipping?0:c.Definition.DiscountType==DiscountType.Percentage?1:2)
            .ThenByDescending(c=>c.Definition.DiscountValue).ThenBy(c=>c.Definition.Code,StringComparer.Ordinal).FirstOrDefault();
        if(candidates is null)return new(0,[]);
        var amount=SalesRules.Discount(request.BaseCost,candidates.Definition.DiscountType,candidates.Definition.DiscountValue);
        return amount>0?new(amount,[candidates.Definition.Code]):new(0,[]);
    }
    private static bool InRadius(SaveCouponRequest c,SalesShippingDiscountRequest r)
    {
        if(!c.ShippingMaxDistanceKm.HasValue)return true;
        if(!r.DestinationLatitude.HasValue || !r.DestinationLongitude.HasValue)return false;
        var origins=c.ShippingOriginLatitude.HasValue?[new SalesDiscountOrigin(c.ShippingOriginLatitude,c.ShippingOriginLongitude)]:r.Origins;
        return origins.Any(o=>o.Latitude.HasValue && o.Longitude.HasValue && Distance(o.Latitude.Value,o.Longitude.Value,r.DestinationLatitude.Value,r.DestinationLongitude.Value)<=(double)c.ShippingMaxDistanceKm.Value);
    }
    private static double Distance(double a,double b,double c,double d)
    {var lat=(c-a)*Math.PI/180;var lon=(d-b)*Math.PI/180;var x=Math.Sin(lat/2)*Math.Sin(lat/2)+Math.Cos(a*Math.PI/180)*Math.Cos(c*Math.PI/180)*Math.Sin(lon/2)*Math.Sin(lon/2);return 6371*2*Math.Asin(Math.Sqrt(Math.Clamp(x,0,1)));}
    private static decimal SimulatedAmount(IReadOnlyList<CouponAllocation> allocations,IReadOnlyList<AdminSalesLineDto> lines)
    {
        var bases=lines.Select(x=>x.FinalTotalPrice).ToArray();decimal total=0;
        foreach(var a in allocations)
        {
            var basis=a.Indexes.Sum(i=>bases[i]);var remaining=Math.Min(basis,a.Amount);var remainingBasis=basis;
            foreach(var i in a.Indexes)
            {
                var amount=remainingBasis==bases[i]?remaining:Math.Min(remaining,SalesRules.Money(remaining*bases[i]/remainingBasis));
                amount=Math.Min(amount,bases[i]);remaining-=amount;remainingBasis-=bases[i];bases[i]-=amount;total+=amount;
            }
        }
        return total;
    }
    private sealed record CouponAllocation(CouponRule Coupon,int[] Indexes,decimal Amount);
}
internal sealed record PricedBasket(CartDto Cart, PricedSaleDto Price, IReadOnlyList<AdminSalesLineDto> AdminLines, CompanyHQ.Sales.Contracts.Ports.SalesShippingQuote? Shipping);
