using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Models;
using CompanyHQ.Sales.Contracts.Ports;
namespace CompanyHQ.Sales.Services;
/// <summary>Bounded hypothetical profit combinations. Radius, shipping eligibility and minimum purchases still need a real checkout quote.</summary>
public sealed class SalesProfitScenarioService(IServiceAvailability? availability, IPersistence db,SalesCatalogService catalog,SalesOptions policy,ISalesProfitShipping? shipping=null)
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public SalesProfitScenarioService(IPersistence db,SalesCatalogService catalog,SalesOptions policy,ISalesProfitShipping? shipping=null) : this(null, db, catalog, policy, shipping) { }

    public async Task<IReadOnlyList<AdminProductProfitScenarioDto>> CalculateAsync(Guid productId,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        await catalog.RequireManager(actor,ct);
        var parents=await db.Query<BundleLink>().Where(x=>x.ComponentProductId==productId).Select(x=>x.ProductId).Distinct().Take(101).ToArrayAsync(ct);
        if(parents.Length>100)throw new ArgumentException("Profit scenario bundle limit exceeded.");
        var graph=await catalog.Graph(parents.Append(productId),ct);var product=graph[productId];
        var coupons=(await catalog.Coupons(ct)).Where(c=>SalesRules.Valid(c.Definition,catalog.Today) && SalesRules.Targets(c.Definition,productId,product.CategoryIds)).ToArray();
        if(coupons.Length>12)throw new ArgumentException("Exact profit scenarios support at most 12 applicable coupons.");
        var methods=shipping is null?[]:await shipping.GetMethodsAsync(ct);
        var preferred=string.IsNullOrWhiteSpace(policy.PreferredProfitShippingMethodCode)?null:methods.FirstOrDefault(m=>string.Equals(m.Code,policy.PreferredProfitShippingMethodCode,StringComparison.OrdinalIgnoreCase));
        var shippingBasis=preferred?.Cost ?? methods.Select(m=>m.Cost).DefaultIfEmpty(0).Max();
        var stackable=coupons.Where(c=>c.Definition.AllowsCombination).ToArray();var sets=coupons.Select(c=>new[]{c}).ToList();
        for(var mask=1;mask<(1<<stackable.Length);mask++)
        {var combination=stackable.Where((_,i)=>(mask&(1<<i))!=0).ToArray();if(combination.Length>=2)sets.Add(combination);}
        var pricing=new CatalogPricing(graph,[],catalog.Today,policy);var result=new List<AdminProductProfitScenarioDto>();
        foreach(var variant in product.Variants.OrderBy(x=>x.Sku,StringComparer.Ordinal).ThenBy(x=>x.Id))
        {
            var price=pricing.Price(productId,variant,false);
            result.Add(Scenario(variant,"Retail",1,price.Selling,price.Effective,0,price.Cost));
            foreach(var set in sets)
            {
                var discount=set.Where(c=>!c.Definition.AppliesToShipping).Sum(c=>SalesRules.Discount(price.Selling,c.Definition.DiscountType,c.Definition.DiscountValue));
                var impact=Math.Min(shippingBasis,set.Where(c=>c.Definition.AppliesToShipping).Sum(c=>SalesRules.Discount(shippingBasis,c.Definition.DiscountType,c.Definition.DiscountValue)));
                result.Add(Scenario(variant,string.Join(" + ",set.Select(c=>c.Definition.Code)),1,price.Selling,Math.Max(0,price.Effective-discount),impact,price.Cost));
                if(result.Count>10000)throw new ArgumentException("Exact profit scenario output limit exceeded.");
            }
        }
        foreach(var parentId in parents)
        foreach(var component in graph[parentId].BundleComponents!.Where(c=>c.ProductId==productId))
        foreach(var parentVariant in graph[parentId].Variants)
        {
            var variant=CatalogPricing.ResolveComponent(product,parentVariant,component);var price=pricing.Price(productId,variant,false);
            var effective=Math.Max(0,price.Effective-SalesRules.Discount(price.Effective,component.DiscountType,component.DiscountValue));
            result.Add(Scenario(variant,graph[parentId].Name+" Bundle",component.Quantity,price.Selling*component.Quantity,effective*component.Quantity,0,price.Cost*component.Quantity));
            if(result.Count>10000)throw new ArgumentException("Exact profit scenario output limit exceeded.");
        }
        return result.DistinctBy(x=>(x.VariantId,x.Scenario,x.Quantity)).OrderBy(x=>x.VariantSku,StringComparer.Ordinal).ThenBy(x=>x.Scenario,StringComparer.Ordinal).ToArray();
    }
    private static AdminProductProfitScenarioDto Scenario(SaveVariantRequest v,string label,int qty,decimal selling,decimal effective,decimal shippingImpact,decimal cost)
    {var profit=SalesRules.Money(effective-cost-shippingImpact);return new(v.Id,v.Sku,label,qty,selling>0?SalesRules.Money((selling-effective)/selling*100):0,SalesRules.Money(effective),SalesRules.Money(shippingImpact),profit,cost>0?SalesRules.Money(profit/cost*100):0);}
}
