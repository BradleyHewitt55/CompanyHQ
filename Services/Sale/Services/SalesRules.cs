using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Enums;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Models;
namespace CompanyHQ.Services.Sales.Services;

internal static class SalesRules
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => CompanyHQ.Services.Sales.Storage.SalesStorage.Write(value);
    public static T Read<T>(string json) => CompanyHQ.Services.Sales.Storage.SalesStorage.Read<T>(json);
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
    public static Dictionary<string,string> Selections(IReadOnlyDictionary<string,string>? source) =>
        (source ?? new Dictionary<string,string>()).ToDictionary(x=>Normalize(x.Key),x=>Normalize(x.Value),StringComparer.Ordinal);
    public static void Validate(object? value)
    {
        if (value is null) throw new ArgumentException("A request is required.");
        var type = value.GetType();
        foreach(var parameter in type.GetConstructors().OrderByDescending(x=>x.GetParameters().Length).First().GetParameters())
        {
            var property = type.GetProperty(parameter.Name!, BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase);
            var item = property?.GetValue(value);
            foreach(var attribute in parameter.GetCustomAttributes<ValidationAttribute>())
                if (!attribute.IsValid(item)) throw new ArgumentException(attribute.FormatErrorMessage(parameter.Name!));
            if (item is string text && parameter.GetCustomAttribute<RequiredAttribute>() is not null && string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Required text must not be blank.");
            if (item is double d && !double.IsFinite(d)) throw new ArgumentException("Coordinates must be finite.");
        }
        if (value is SalesAddressDto address && address.Latitude.HasValue != address.Longitude.HasValue)
            throw new ArgumentException("Latitude and longitude must be given together.");
    }
    public static decimal Discount(decimal basis, DiscountType type, decimal value) => Money(Math.Clamp(type switch
    { DiscountType.Percentage=>basis * Math.Clamp(value,0,100)/100, DiscountType.Fixed=>value, DiscountType.FreeShipping=>basis, _=>0 },0,basis));
    public static bool Valid(SaveCouponRequest coupon, DateOnly today) => coupon.IsActive &&
        (!coupon.StartDate.HasValue || today>=coupon.StartDate) && (!coupon.EndDate.HasValue || today<=coupon.EndDate);
    public static bool Targets(SaveCouponRequest coupon, Guid productId, IReadOnlyList<Guid>? categoryIds) =>
        (coupon.ProductIds?.Count ?? 0)==0 && (coupon.CategoryIds?.Count ?? 0)==0 ||
        (coupon.ProductIds?.Contains(productId) ?? false) || (coupon.CategoryIds?.Any(x=>categoryIds?.Contains(x)==true) ?? false);
    public static decimal Retail(SaveProductRequest p, SaveVariantRequest v, decimal amount)
    {
        if (!v.UseCustomRounding && !p.EnableRounding) return Money(amount);
        var target=v.UseCustomRounding?v.RoundingTarget:p.RoundingTarget;
        var direction=v.UseCustomRounding?v.RoundingDirection:p.RoundingDirection;
        var digits=decimal.Truncate(target).ToString(CultureInfo.InvariantCulture).Length;
        var divisor=(decimal)Math.Pow(10,digits);
        var candidate=decimal.Floor(amount/divisor)*divisor+target;
        if(direction==RoundingDirection.RoundUp && candidate<amount) candidate+=divisor;
        if(direction==RoundingDirection.RoundDown && candidate>amount) candidate-=divisor;
        return Money(Math.Max(0,candidate));
    }
    public static bool Matches(SaveVariantRequest v, IReadOnlyDictionary<string,string> selections)
    {
        var options=Selections(v.Options);
        return selections.All(x=>options.TryGetValue(x.Key,out var val) && val==x.Value);
    }
    public static CartDto NormalizeCart(IReadOnlyList<SalesLineRequest> input)
    {
        if (input.Count is <1 or >200) throw new ArgumentException("A cart must contain 1 to 200 lines.");
        if(input.Any(x=>x.Id==Guid.Empty || x.ProductId==Guid.Empty) || input.Select(x=>x.Id).Distinct().Count()!=input.Count)
            throw new ArgumentException("Cart lines require distinct nonempty ids and product ids.");
        foreach(var line in input) { Validate(line); if((line.Options?.Count ?? 0)>20) throw new ArgumentException("Too many selections."); Selections(line.Options); }
        var removed=new HashSet<Guid>();
        var remaining=input.ToList();
        while(true)
        {
            var ids=remaining.Select(x=>x.Id).ToHashSet();
            var orphans=remaining.Where(x=>x.UpsellParentLineId.HasValue && !ids.Contains(x.UpsellParentLineId.Value)).ToList();
            if(orphans.Count==0) break;
            foreach(var orphan in orphans) { removed.Add(orphan.Id); remaining.Remove(orphan); }
        }
        foreach(var line in remaining)
        {
            var visited=new HashSet<Guid>{line.Id}; var parent=line.UpsellParentLineId;
            while(parent.HasValue)
            {
                if(!visited.Add(parent.Value)) throw new ArgumentException("Cart upsell identities cannot form cycles.");
                parent=remaining.Single(x=>x.Id==parent).UpsellParentLineId;
            }
        }
        var canonical=new List<SalesLineRequest>(); var aliases=new Dictionary<Guid,Guid>();
        // Process parents first, then rewrite child ids after merges.
        var pending=remaining.ToList();
        while(pending.Count>0)
        {
            foreach(var line in pending.ToArray())
            {
                if(line.UpsellParentLineId.HasValue && !aliases.ContainsKey(line.UpsellParentLineId.Value)) continue;
                var parent=line.UpsellParentLineId.HasValue?aliases[line.UpsellParentLineId.Value]:(Guid?)null;
                var normalized=line with {Options=Selections(line.Options),UpsellParentLineId=parent};
                var index=canonical.FindIndex(x=>x.ProductId==line.ProductId && x.VariantId==line.VariantId &&
                    x.BundleInstanceId==line.BundleInstanceId && x.UpsellParentLineId==parent &&
                    Selections(x.Options).OrderBy(x=>x.Key).SequenceEqual(Selections(line.Options).OrderBy(x=>x.Key)));
                if(index<0) { canonical.Add(normalized); aliases[line.Id]=line.Id; }
                else
                {
                    var existing=canonical[index]; var qty=checked(existing.Quantity+line.Quantity);
                    if(qty>1000) throw new ArgumentException("Merged quantity exceeds 1000.");
                    canonical[index]=existing with {Quantity=qty}; aliases[line.Id]=existing.Id; removed.Add(line.Id);
                }
                pending.Remove(line);
            }
        }
        return new(canonical,removed.ToArray());
    }
}

internal sealed record PriceResult(decimal Selling, decimal Effective, decimal Cost, IReadOnlyList<SalesLineDiscountDto> Discounts)
{
    public ProductPriceDto Dto => new(Selling,Effective<Selling?Effective:null,Effective,string.Join(", ",Discounts.Select(x=>x.Label)));
}
internal sealed record CouponRule(Guid Id, SaveCouponRequest Definition);

internal sealed class CatalogPricing(IReadOnlyDictionary<Guid,SaveProductRequest> products, IReadOnlyList<CouponRule> coupons, DateOnly today, SalesOptions? policy=null)
{
    private readonly Dictionary<(Guid Product,Guid Variant,bool Automatic,decimal? Subtotal),PriceResult> cache=[];
    private int budget=100000;
    public PriceResult Price(Guid id, SaveVariantRequest variant, bool automatic=true, decimal? subtotal=null) => Price(id,variant,new HashSet<Guid>(),automatic,subtotal);
    private PriceResult Price(Guid id, SaveVariantRequest variant, HashSet<Guid> path, bool automatic, decimal? subtotal)
    {
        if(path.Contains(id))throw new ArgumentException("Bundle cycle.");
        var key=(id,variant.Id,automatic,subtotal);
        if(cache.TryGetValue(key,out var cached))return cached;
        if(--budget<0)throw new ArgumentException("Pricing complexity limit exceeded.");
        if(!path.Add(id) || path.Count>20) throw new ArgumentException("Bundle cycle or depth limit exceeded.");
        var p=products[id]; decimal selling,cost,bundleEffective;
        if(p.Type==ProductType.Bundle)
        {
            selling=cost=bundleEffective=0;
            foreach(var component in p.BundleComponents ?? [])
            {
                if(!products.TryGetValue(component.ProductId,out var child)) throw new ArgumentException("Bundle component missing.");
                if(child.Currency!=p.Currency)throw new ArgumentException("A bundle cannot mix currencies.");
                var selected=ResolveComponent(child,variant,component);
                var price=Price(component.ProductId,selected,path,false,subtotal);
                selling+=price.Selling*component.Quantity; cost+=price.Cost*component.Quantity;
                bundleEffective+=Math.Max(0,price.Effective-SalesRules.Discount(price.Effective,component.DiscountType,component.DiscountValue))*component.Quantity;
            }
            selling=SalesRules.Money(selling); bundleEffective=SalesRules.Money(bundleEffective); cost=SalesRules.Money(cost);
        }
        else
        {
            cost=variant.Cost;
            var selections=SalesRules.Selections(variant.Options);
            var optionName=SalesRules.Normalize(policy?.ExtraLengthOptionName ?? "");
            var extraValue=SalesRules.Normalize(policy?.ExtraLengthValueContains ?? "");
            var standardValue=SalesRules.Normalize(policy?.StandardLengthValueContains ?? "");
            var standard=policy?.EnableExtraLengthPricing==true && selections.TryGetValue(optionName,out var length) && length.Contains(extraValue,StringComparison.Ordinal)
                ? p.Variants.FirstOrDefault(v=>SalesRules.Selections(v.Options).TryGetValue(optionName,out var l) && l.Contains(standardValue,StringComparison.Ordinal) &&
                    SalesRules.Matches(v,selections.Where(x=>x.Key!=optionName).ToDictionary(x=>x.Key,x=>x.Value))) : null;
            selling=standard is null ? SalesRules.Retail(p,variant,variant.Cost*(1+variant.ProfitPercentage/100)) :
                SalesRules.Retail(p,variant,SalesRules.Retail(p,standard,standard.Cost*(1+standard.ProfitPercentage/100))*policy!.ExtraLengthMultiplier);
            bundleEffective=selling;
        }
        if(selling>1_000_000_000m || cost>1_000_000_000m)throw new ArgumentException("Product price exceeds the supported monetary limit.");
        path.Remove(id);
        var bundleDiscount=SalesRules.Money(selling-bundleEffective);
        var eligible=automatic ? coupons.Where(c=>c.Definition.IsAutomatic && !c.Definition.AppliesToShipping && SalesRules.Valid(c.Definition,today) &&
            SalesRules.Targets(c.Definition,id,p.CategoryIds) && (c.Definition.MinimumPurchase ?? 0)<=(subtotal ?? selling)).ToArray() : [];
        var stack=eligible.Where(c=>c.Definition.AllowsCombination).OrderBy(c=>c.Definition.Code,StringComparer.Ordinal).ToArray();
        var stacked=bundleDiscount+stack.Sum(c=>SalesRules.Discount(selling,c.Definition.DiscountType,c.Definition.DiscountValue));
        var single=eligible.OrderByDescending(c=>SalesRules.Discount(selling,c.Definition.DiscountType,c.Definition.DiscountValue)).ThenBy(c=>c.Definition.Code,StringComparer.Ordinal).FirstOrDefault();
        var singleAmount=single is null?0:SalesRules.Discount(selling,single.Definition.DiscountType,single.Definition.DiscountValue);
        var entries=new List<SalesLineDiscountDto>();
        if(stacked>=singleAmount)
        {
            if(bundleDiscount>0) entries.Add(new(SalesDiscountSource.Bundle,"Bundle",null,bundleDiscount,null,0));
            foreach(var c in stack) entries.Add(new(SalesDiscountSource.AutomaticCoupon,c.Definition.Code,c.Definition.Code,
                SalesRules.Discount(selling,c.Definition.DiscountType,c.Definition.DiscountValue),c.Definition.DiscountType,c.Definition.DiscountValue));
        }
        else if(single is not null) entries.Add(new(SalesDiscountSource.AutomaticCoupon,single.Definition.Code,single.Definition.Code,singleAmount,single.Definition.DiscountType,single.Definition.DiscountValue));
        decimal remaining=selling;
        var capped=entries.Select(e=> { var amount=Math.Min(remaining,e.DiscountAmount);remaining-=amount;return e with {DiscountAmount=amount}; }).Where(e=>e.DiscountAmount>0).ToArray();
        var result=new PriceResult(selling,SalesRules.Money(remaining),cost,capped);cache[key]=result;return result;
    }
    public static SaveVariantRequest ResolveComponent(SaveProductRequest child,SaveVariantRequest parent,SaveBundleComponentRequest component)
    {
        var selections=SalesRules.Selections(parent.Options);
        var mapped=new Dictionary<string,string>();
        if(component.SyncOptions)
        {
            foreach(var mapping in component.OptionMappings ?? new Dictionary<string,string>())
                if(selections.TryGetValue(SalesRules.Normalize(mapping.Key),out var val)) mapped[SalesRules.Normalize(mapping.Value)]=val;
            if((component.OptionMappings?.Count ?? 0)==0)
                foreach(var o in child.Options ?? []) if(selections.TryGetValue(SalesRules.Normalize(o.Name),out var val)) mapped[SalesRules.Normalize(o.Name)]=val;
        }
        return (mapped.Count>0?child.Variants.FirstOrDefault(v=>SalesRules.Matches(v,mapped)):null)
            ?? child.Variants.FirstOrDefault(v=>v.Id==component.FallbackVariantId) ?? child.Variants[0];
    }
    public static SaveVariantRequest? ResolveUpsell(SaveProductRequest target,SaveVariantRequest parent,bool sync)
    {
        if(!sync) return target.Variants[0];
        var selections=SalesRules.Selections(parent.Options);
        var names=(target.Options ?? []).Select(x=>SalesRules.Normalize(x.Name)).ToHashSet();
        var shared=selections.Where(x=>names.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value);
        return shared.Count==0 ? target.Variants[0] : target.Variants.FirstOrDefault(v=>SalesRules.Matches(v,shared));
    }
}
