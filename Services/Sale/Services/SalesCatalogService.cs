using CompanyHQ.ServiceCatalog.Contracts;
using System.Data;
using System.Text.RegularExpressions;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Models;
using CompanyHQ.Sales.Contracts.Ports;
using CompanyHQ.Users.Services;
namespace CompanyHQ.Sales.Services;

/// <summary>Owns the reusable persisted catalog and calculated product prices.</summary>
public sealed partial class SalesCatalogService(IServiceAvailability? availability, IPersistence db, IUserAuthorizationService auth, TimeProvider clock, SalesOptions options) : ISalesCatalogService
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public SalesCatalogService(IPersistence db, IUserAuthorizationService auth, TimeProvider clock, SalesOptions options) : this(null, db, auth, clock, options) { }

    internal SalesOptions PricingOptions => options;
    internal DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZone)).Date);
    internal Task<bool> Manager(Guid? actor,CancellationToken ct) {
        availability?.RequireAsync("sales", false, ct).GetAwaiter().GetResult();
        return actor.HasValue?auth.HasPermissionAsync(actor.Value,AccessPermissions.ManageSales,ct):Task.FromResult(false);
    }
    internal async Task RequireManager(Guid? actor,CancellationToken ct) {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask); if(!await Manager(actor,ct)) throw new UnauthorizedAccessException(); }
    internal async Task<IReadOnlyList<CouponRule>> Coupons(CancellationToken ct) {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        return (await db.Query<Coupon>().Where(x=>x.IsActive).OrderBy(x=>x.Code).Take(1001).ToListAsync(ct)) is var rows && rows.Count<=1000
        ? rows.Select(x=>new CouponRule(x.Id,SalesRules.Read<SaveCouponRequest>(x.DefinitionJson))).ToArray() : throw new ArgumentException("Promotion limit exceeded.");
    }
    internal async Task<Dictionary<Guid,SaveProductRequest>> Graph(IEnumerable<Guid> ids,CancellationToken ct)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        var graph=new Dictionary<Guid,SaveProductRequest>(); var pending=ids.Distinct().ToArray();
        for(var depth=0;pending.Length>0;depth++)
        {
            if(depth>20 || graph.Count+pending.Length>1000) throw new ArgumentException("Catalog dependency limit exceeded.");
            var rows=await db.Query<Product>().Where(p=>pending.Contains(p.Id)).ToListAsync(ct);
            foreach(var row in rows) graph[row.Id]=SalesRules.Read<SaveProductRequest>(row.DefinitionJson);
            if(rows.Count!=pending.Length) throw new ArgumentException("Product or bundle component not found.");
            pending=rows.SelectMany(p=>graph[p.Id].BundleComponents ?? []).Select(c=>c.ProductId).Distinct().Where(id=>!graph.ContainsKey(id)).ToArray();
        }
        return graph;
    }
    public async Task<PagedResult<ClientProductDto>> ListAsync(PageRequest paging, SearchRequest search, ProductFilterRequest filter,
        ResponseAudience audience=ResponseAudience.Client,Guid? actor=null,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        var usesPrice=filter.MinPrice.HasValue || filter.MaxPrice.HasValue || filter.Sale==true || filter.Sort.StartsWith("price-",StringComparison.Ordinal);
        if(!usesPrice)return await ListCoreAsync(paging,search,filter,audience,actor,false,ct);
        var result=await db.InTransactionAsync(async token=>
        {
            try { return await ListCoreAsync(paging,search,filter,audience,actor,true,token); }
            catch(SalesCatalogPricesRefreshingException) { return null; }
        },cancellationToken:ct);
        // Commit progress before reporting pending; the next request or worker resumes it.
        return result ?? throw new SalesCapabilityUnavailableException("Catalog prices are refreshing. Retry this price query shortly.");
    }
    private async Task<PagedResult<ClientProductDto>> ListCoreAsync(PageRequest paging,SearchRequest search,ProductFilterRequest filter,
        ResponseAudience audience,Guid? actor,bool usesPrice,CancellationToken ct)
    {
        var manager=audience==ResponseAudience.Admin && await Manager(actor,ct);
        var query=db.Query<Product>().Where(p=>manager || p.IsVisible);
        if(filter.CategoryId.HasValue) query=query.Where(p=>p.CategoryIds.Contains(filter.CategoryId.Value));
        if(filter.SupplierId.HasValue) query=query.Where(p=>p.SupplierIds.Contains(filter.SupplierId.Value));
        if(filter.Type.HasValue && !Enum.IsDefined(filter.Type.Value))throw new ArgumentException("Invalid product type.");
        if(filter.Type.HasValue) query=query.Where(p=>p.Type==filter.Type.Value);
        var categoryIds=(filter.CategoryIds ?? []).Distinct().ToArray();
        if(categoryIds.Length>100)throw new ArgumentException("Too many category filters.");
        if(categoryIds.Length>0)query=query.Where(p=>p.CategoryIds.Any(id=>categoryIds.Contains(id)));
        if(filter.MinRating.HasValue){if(filter.MinRating is <1 or >5)throw new ArgumentException("Rating must be 1 to 5.");query=query.Where(p=>p.ReviewCount>0 && p.AverageRating>=filter.MinRating.Value);}
        if(filter.MinPrice<0 || filter.MaxPrice<0 || filter.MinPrice>filter.MaxPrice)throw new ArgumentException("Invalid price range.");
        if((filter.Options?.Count ?? 0)>20) throw new ArgumentException("Too many option filters.");
        var variants=db.Query<CatalogVariant>();
        foreach(var (key,val) in SalesRules.Selections(filter.Options))
        {
            var name=key;var value=val;
            var selections=db.Query<VariantSelection>().Where(x=>x.Name==name && x.Value==value);
            variants=variants.Where(v=>selections.Any(s=>s.VariantId==v.Id));
        }
        if((filter.Options?.Count ?? 0)>0) query=query.Where(p=>variants.Any(v=>v.ProductId==p.Id));
        query=query.Search(search.Field,search.Keyword,typeof(ClientProductDto));
        if(usesPrice)await EnsurePricesAsync(query,ct);
        // Price bounds describe the displayed minimum product price. Matching selections only
        // affect variant eligibility and ordering, not which product price the range describes.
        if(filter.MinPrice.HasValue)query=query.Where(p=>p.EffectivePrice>=filter.MinPrice.Value);
        if(filter.MaxPrice.HasValue)query=query.Where(p=>p.EffectivePrice<=filter.MaxPrice.Value);
        if(filter.MinPrice.HasValue || filter.MaxPrice.HasValue)query=query.Where(p=>p.EffectivePrice>0);
        if(filter.Sale==true)query=query.Where(p=>p.IsOnSale);
        var ordered=filter.Sort switch
        {
            "name-asc"=>query.OrderBy(p=>p.Name).ThenBy(p=>p.Id),
            "name-desc"=>query.OrderByDescending(p=>p.Name).ThenBy(p=>p.Id),
            "price-asc"=>query.OrderBy(p=>variants.Where(v=>v.ProductId==p.Id).OrderBy(v=>v.EffectivePrice).Select(v=>v.EffectivePrice).FirstOrDefault()).ThenBy(p=>p.Name).ThenBy(p=>p.Id),
            "price-desc"=>query.OrderByDescending(p=>variants.Where(v=>v.ProductId==p.Id).OrderBy(v=>v.EffectivePrice).Select(v=>v.EffectivePrice).FirstOrDefault()).ThenBy(p=>p.Name).ThenBy(p=>p.Id),
            _=>throw new ArgumentException("Unsupported catalog sort.")
        };
        var page=await ordered.ToPageAsync(paging.Skip,paging.PageSize,ct);
        var graph=await Graph(page.Items.Select(x=>x.Id),ct); var coupons=await Coupons(ct);
        var related=await RelatedAsync(page.Items,ct);
        return new(page.Items.Select(p=>ToDto(p,graph,coupons,manager,related.GetValueOrDefault(p.Id,[]))).ToArray(),paging.Page,paging.PageSize,page.Total);
    }
    public async Task<ClientProductDto?> GetAsync(string slug,ResponseAudience audience=ResponseAudience.Client,Guid? actor=null,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        var manager=audience==ResponseAudience.Admin && await Manager(actor,ct);
        var p=await db.Query<Product>().FirstOrDefaultAsync(p=>p.Slug==slug && (manager || p.IsVisible),ct);
        if(p is null)return null;
        var related=await RelatedAsync([p],ct);
        return ToDto(p,await Graph([p.Id],ct),await Coupons(ct),manager,related.GetValueOrDefault(p.Id,[]));
    }
    public async Task<AdminProductDto> SaveAsync(Guid? id,SaveProductRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        await RequireManager(actor,ct);
        request=request with {Currency=string.IsNullOrWhiteSpace(request.Currency)?options.DefaultCurrency:request.Currency};
        await EnsurePriceStateAsync(ct);
        return await db.InTransactionAsync(async token=>
        {
            var product=id.HasValue?await db.Tracked<Product>().FirstOrDefaultAsync(p=>p.Id==id.Value,token):new Product {Id=Guid.CreateVersion7()};
            if(product is null) throw new KeyNotFoundException("Product not found.");
            var existingOptions=id.HasValue?SalesRules.Read<SaveProductRequest>(product.DefinitionJson).Options ?? []:[];
            var optionIds=existingOptions.ToDictionary(x=>SalesRules.Normalize(x.Name),x=>x.Id);
            request=request with {Options=request.Options?.Select(x=>x.Id!=Guid.Empty?x:x with
                {Id=optionIds.TryGetValue(SalesRules.Normalize(x.Name),out var optionId) && optionId!=Guid.Empty?optionId:Guid.CreateVersion7()}).ToArray()};
            ValidateProduct(request);
            var categories=(request.CategoryIds ?? []).Distinct().ToArray();var suppliers=(request.SupplierIds ?? []).Distinct().ToArray();
            var related=(request.RelatedProductIds ?? []).Distinct().ToArray();
            if(related.Contains(product.Id) || await db.Query<Product>().CountAsync(x=>related.Contains(x.Id),token)!=related.Length)throw new ArgumentException("Invalid related products.");
            if(await db.Query<Category>().CountAsync(x=>categories.Contains(x.Id),token)!=categories.Length ||
                await db.Query<Supplier>().CountAsync(x=>suppliers.Contains(x.Id),token)!=suppliers.Length) throw new ArgumentException("Category or supplier not found.");
            // Load and validate the replacement graph inside a serializable transaction: concurrent
            // opposing bundle edits cannot both pass cycle detection against an old snapshot.
            var children=(request.BundleComponents ?? []).Select(x=>x.ProductId).Where(x=>x!=product.Id);
            var ancestors=new HashSet<Guid>();var pending=new[]{product.Id};
            while(pending.Length>0)
            {
                var parents=await db.Query<BundleLink>().Where(x=>pending.Contains(x.ComponentProductId))
                    .Select(x=>x.ProductId).Distinct().Take(1001).ToArrayAsync(token);
                pending=parents.Where(x=>x!=product.Id && ancestors.Add(x)).ToArray();
                if(ancestors.Count>1000)throw new ArgumentException("Catalog dependency limit exceeded.");
            }
            var graph=await Graph(children.Concat(ancestors),token); graph[product.Id]=request;
            var pricing=new CatalogPricing(graph,[],Today,options);
            foreach(var root in ancestors.Append(product.Id))
                foreach(var v in graph[root].Variants) pricing.Price(root,v,false);
            foreach(var component in request.BundleComponents ?? [])
            {
                var child=graph[component.ProductId];
                if(child.Currency!=request.Currency) throw new ArgumentException("A bundle cannot mix currencies.");
                if(component.FallbackVariantId.HasValue && !child.Variants.Any(v=>v.Id==component.FallbackVariantId)) throw new ArgumentException("Bundle fallback variant not found.");
            }
            product.Name=request.Name.Trim();product.Slug=request.Slug;product.Type=request.Type;product.IsVisible=request.IsVisible;
            product.ShortDescription=request.ShortDescription;product.Description=request.Description;product.CategoryIds=categories;product.SupplierIds=suppliers;
            product.DefinitionJson=SalesRules.Write(request);
            if(!id.HasValue) db.Add(product);
            else
            {
                await db.Query<CatalogVariant>().Where(v=>v.ProductId==product.Id).DeleteAsync(token);
                await db.Query<BundleLink>().Where(v=>v.ProductId==product.Id).DeleteAsync(token);
            }
            foreach(var v in request.Variants)
            {
                db.Add(new CatalogVariant {Id=v.Id,ProductId=product.Id,Sku=v.Sku});
                foreach(var (name,value) in SalesRules.Selections(v.Options)) db.Add(new VariantSelection {VariantId=v.Id,Name=name,Value=value});
            }
            foreach(var c in request.BundleComponents ?? []) db.Add(new BundleLink {ProductId=product.Id,ComponentProductId=c.ProductId});
            await MarkPricesDirtyAsync(token);
            await db.SaveChangesAsync(token);
            var relatedProducts=await RelatedAsync([product],token);
            return (AdminProductDto)ToDto(product,graph,await Coupons(token),true,relatedProducts.GetValueOrDefault(product.Id,[]));
        },IsolationLevel.Serializable,ct);
    }
    public async Task<bool> DeleteAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        await RequireManager(actor,ct);await EnsurePriceStateAsync(ct);
        return await db.InTransactionAsync(async token=>
        {
            if(await db.Query<BundleLink>().AnyAsync(x=>x.ComponentProductId==id,token)) throw new ArgumentException("A bundle still uses this product.");
            await MarkPricesDirtyAsync(token);var deleted=await db.Query<Product>().Where(x=>x.Id==id).DeleteAsync(token)>0;await db.SaveChangesAsync(token);return deleted;
        },IsolationLevel.Serializable,ct);
    }
    internal static void ValidateProduct(SaveProductRequest p)
    {
        SalesRules.Validate(p);
        if(!Regex.IsMatch(p.Slug,"^[a-z0-9]+(?:-[a-z0-9]+)*$",RegexOptions.CultureInvariant) || p.Slug.Length>255) throw new ArgumentException("Use a lowercase hyphenated slug.");
        if(!Regex.IsMatch(p.Currency,"^[A-Z]{3}$") || !Enum.IsDefined(p.Type) || !Enum.IsDefined(p.RoundingDirection)) throw new ArgumentException("Invalid currency or product type.");
        if(p.Variants.Count is <1 or >500 || p.Variants.Any(v=>v.Id==Guid.Empty) || p.Variants.Select(v=>v.Id).Distinct().Count()!=p.Variants.Count) throw new ArgumentException("Supply 1 to 500 variants with stable unique ids.");
        if(p.Variants.Select(v=>SalesRules.Normalize(v.Sku)).Distinct().Count()!=p.Variants.Count) throw new ArgumentException("Variant SKUs must be unique.");
        if((p.Tags?.Count ?? 0)>100 || (p.RelatedProductIds?.Count ?? 0)>100 || (p.Tags ?? []).Any(t=>string.IsNullOrWhiteSpace(t) || t.Length>100))throw new ArgumentException("Invalid product tags or relations.");
        if((p.Options?.Count ?? 0)>20 || (p.ImageUrls?.Count ?? 0)>50 || (p.CategoryIds?.Count ?? 0)>100 || (p.SupplierIds?.Count ?? 0)>100 || (p.BundleComponents?.Count ?? 0)>100) throw new ArgumentException("Catalog input limit exceeded.");
        var options=(p.Options ?? []).ToDictionary(o=>SalesRules.Normalize(o.Name),o=>o.Values.Select(SalesRules.Normalize).ToHashSet());
        foreach(var o in p.Options ?? []) if(string.IsNullOrWhiteSpace(o.Name) || o.Name.Length>100 || o.Values.Count is <1 or >100 || o.Values.Any(v=>string.IsNullOrWhiteSpace(v) || v.Length>255) || o.Values.Select(SalesRules.Normalize).Distinct().Count()!=o.Values.Count) throw new ArgumentException("Invalid product options.");
        var orderedOptions=p.Options ?? [];
        var indexes=new Dictionary<Guid,int>();
        for(var index=0;index<orderedOptions.Count;index++)
            if(orderedOptions[index].Id==Guid.Empty || !indexes.TryAdd(orderedOptions[index].Id,index))
                throw new ArgumentException("Product options require unique stable ids.");
        for(var index=0;index<orderedOptions.Count;index++)
        {
            var dependencies=orderedOptions[index].DependencyOptionIds ?? [];
            if(dependencies.Count>20 || dependencies.Distinct().Count()!=dependencies.Count ||
                dependencies.Any(id=>!indexes.TryGetValue(id,out var parentIndex) || parentIndex>=index))
                throw new ArgumentException("Options can depend only on distinct earlier options in the same product.");
        }
        var identities=new HashSet<string>();
        foreach(var v in p.Variants)
        {
            SalesRules.Validate(v);if(!Enum.IsDefined(v.RoundingDirection)) throw new ArgumentException("Invalid rounding direction.");
            var values=SalesRules.Selections(v.Options);
            if(values.Count!=options.Count || values.Any(x=>!options.TryGetValue(x.Key,out var allowed) || !allowed.Contains(x.Value))) throw new ArgumentException("Variant options must exactly match the product definition.");
            if(!identities.Add(string.Join('|',values.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value)))) throw new ArgumentException("Duplicate variant selections.");
        }
        if(p.Type==ProductType.Bundle && (p.BundleComponents?.Count ?? 0)==0 || p.Type!=ProductType.Bundle && (p.BundleComponents?.Count ?? 0)>0) throw new ArgumentException("Only bundles have required components.");
        if((p.BundleComponents ?? []).Select(c=>c.ProductId).Distinct().Count()!=(p.BundleComponents?.Count ?? 0)) throw new ArgumentException("Duplicate bundle component.");
        foreach(var c in p.BundleComponents ?? [])
        {
            SalesRules.Validate(c); if(c.ProductId==Guid.Empty || c.DiscountType==DiscountType.FreeShipping || !Enum.IsDefined(c.DiscountType) || c.DiscountType==DiscountType.Percentage && c.DiscountValue>100 || (c.OptionMappings?.Count ?? 0)>20) throw new ArgumentException("Invalid bundle component.");
        }
    }
    private async Task<Dictionary<Guid,IReadOnlyList<ClientRelatedProductDto>>> RelatedAsync(IReadOnlyList<Product> products,CancellationToken ct)
    {
        var links=products.ToDictionary(x=>x.Id,x=>SalesRules.Read<SaveProductRequest>(x.DefinitionJson).RelatedProductIds ?? []);
        var ids=links.Values.SelectMany(x=>x).Distinct().ToArray();
        var rows=await db.Query<Product>().Where(x=>ids.Contains(x.Id) && x.IsVisible).ToListAsync(ct);
        var summaries=new Dictionary<Guid,ClientRelatedProductDto>();
        var coupons=await Coupons(ct);
        foreach(var row in rows)
        {
            var dto=ToDto(row,await Graph([row.Id],ct),coupons,false);
            summaries[row.Id]=new(dto.Id,dto.Name,dto.Slug,dto.ImageUrls.FirstOrDefault(),dto.Currency,dto.Price);
        }
        return links.ToDictionary(x=>x.Key,x=>(IReadOnlyList<ClientRelatedProductDto>)x.Value
            .Where(summaries.ContainsKey).Select(id=>summaries[id]).ToArray());
    }
    internal ClientProductDto ToDto(Product row,Dictionary<Guid,SaveProductRequest> graph,IReadOnlyList<CouponRule> coupons,bool admin,
        IReadOnlyList<ClientRelatedProductDto>? related=null)
    {
        var p=graph[row.Id];var pricing=new CatalogPricing(graph,coupons,Today,options);
        var variants=p.Variants.Select(v=>
        {
            var price=pricing.Price(row.Id,v).Dto;var name=string.Join(" / ",(p.Options ?? []).Select(o=>v.Options!.First(x=>SalesRules.Normalize(x.Key)==SalesRules.Normalize(o.Name)).Value));
            return admin ? (ClientProductVariantDto)new AdminProductVariantDto(v.Id,v.Sku,name,v.Options ?? new Dictionary<string,string>(),price,v.StockQty,v.Cost,v.ProfitPercentage,v.UseCustomRounding,v.RoundingTarget,v.RoundingDirection,v.Specifications)
                :new ClientProductVariantDto(v.Id,v.Sku,name,v.Options ?? new Dictionary<string,string>(),price,v.StockQty,v.Specifications);
        }).ToArray();
        var components=(p.BundleComponents ?? []).Select(c=>admin?(ClientBundleComponentDto)new AdminBundleComponentDto(c.ProductId,graph[c.ProductId].Name,c.Quantity,c.DiscountValue,c.DiscountType,c.SyncOptions,c.FallbackVariantId,c.OptionMappings ?? new Dictionary<string,string>()):new ClientBundleComponentDto(c.ProductId,graph[c.ProductId].Name,c.Quantity)).ToArray();
        var cheapest=variants.OrderBy(v=>v.Price.EffectivePrice).ThenBy(v=>v.Price.SellingPrice).ThenBy(v=>v.Id).First().Price;
        return admin ? new AdminProductDto(row.Id,p.Name,p.Slug,p.ShortDescription,p.Description,p.Type,p.ImageUrls ?? [],p.CategoryIds ?? [],p.Currency,cheapest,p.Options ?? [],variants,components,p.IsVisible,p.EnableRounding,p.RoundingTarget,p.RoundingDirection,row.CreatedAt,row.UpdatedAt,p.Brand,p.Tags,row.AverageRating,row.ReviewCount,row.UnitsSold,null,p.SupplierIds,p.RelatedProductIds,related ?? []):
            new ClientProductDto(row.Id,p.Name,p.Slug,p.ShortDescription,p.Description,p.Type,p.ImageUrls ?? [],p.CategoryIds ?? [],p.Currency,cheapest,p.Options ?? [],variants,components,p.Brand,p.Tags,row.AverageRating,row.ReviewCount,row.UnitsSold,RelatedProducts:related ?? []);
    }
    public async Task<PagedResult<ClientCategoryDto>> CategoriesAsync(PageRequest page,SearchRequest search,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask); var result=await db.Query<Category>().Search(search.Field,search.Keyword,typeof(ClientCategoryDto)).OrderBy(x=>x.Name).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);return new(result.Items.Select(x=>new ClientCategoryDto(x.Id,x.Name,x.Slug,x.ParentId,x.Position,x.Sku,x.ImageUrl)).ToArray(),page.Page,page.PageSize,result.Total); }
    public async Task<ClientCategoryDto> SaveCategoryAsync(Guid? id,SaveCategoryRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask); await RequireManager(actor,ct);SalesRules.Validate(request);if(!Regex.IsMatch(request.Slug,"^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new ArgumentException("Invalid slug.");var row=id.HasValue?await db.FindAsync<Category>(id.Value,ct):new Category();if(row is null) throw new KeyNotFoundException();if(request.ParentId==id && id.HasValue)throw new ArgumentException("A category cannot parent itself.");
        var parent=request.ParentId;var visited=new HashSet<Guid>();
        while(parent.HasValue){if(parent==id || !visited.Add(parent.Value) || visited.Count>50)throw new ArgumentException("Category cycle or depth limit.");var ancestor=await db.Query<Category>().FirstOrDefaultAsync(x=>x.Id==parent,ct) ?? throw new ArgumentException("Parent category not found.");parent=ancestor.ParentId;}
        row.ParentId=request.ParentId;row.Position=request.Position;row.Sku=request.Sku;row.ImageUrl=request.ImageUrl;row.Name=request.Name.Trim();row.Slug=request.Slug;if(!id.HasValue)db.Add(row);await db.SaveChangesAsync(ct);return new(row.Id,row.Name,row.Slug,row.ParentId,row.Position,row.Sku,row.ImageUrl); }
    public async Task<bool> DeleteCategoryAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask); await RequireManager(actor,ct);return await db.InTransactionAsync(async token=> {if(await db.Query<Category>().AnyAsync(x=>x.ParentId==id,token) || await db.Query<Product>().AnyAsync(x=>x.CategoryIds.Contains(id),token))throw new ArgumentException("Category is in use.");return await db.Query<Category>().Where(x=>x.Id==id).DeleteAsync(token)>0;},IsolationLevel.Serializable,ct); }
    public async Task<PagedResult<AdminSupplierDto>> SuppliersAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await RequireManager(actor,ct);var result=await db.Query<Supplier>().Search(search.Field,search.Keyword,typeof(AdminSupplierDto)).OrderBy(x=>x.Name).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);return new(result.Items.Select(SupplierDto).ToArray(),page.Page,page.PageSize,result.Total);}
    public async Task<AdminSupplierDto> SaveSupplierAsync(Guid? id,SaveSupplierRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await RequireManager(actor,ct);SalesRules.Validate(request);if(request.Origin is not null)SalesRules.Validate(request.Origin);var row=id.HasValue?await db.FindAsync<Supplier>(id.Value,ct):new Supplier();if(row is null)throw new KeyNotFoundException();row.Name=request.Name.Trim();row.IsActive=request.IsActive;row.OriginJson=request.Origin is null?null:SalesRules.Write(request.Origin);if(!id.HasValue)db.Add(row);await db.SaveChangesAsync(ct);return SupplierDto(row);}
    public async Task<bool> DeleteSupplierAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await RequireManager(actor,ct);return await db.InTransactionAsync(async token=>{if(await db.Query<Product>().AnyAsync(x=>x.SupplierIds.Contains(id),token))throw new ArgumentException("Supplier is in use.");return await db.Query<Supplier>().Where(x=>x.Id==id).DeleteAsync(token)>0;},IsolationLevel.Serializable,ct);}
    public async Task<ClientCategoryDto?> GetCategoryAsync(Guid id,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);var x=await db.Query<Category>().FirstOrDefaultAsync(x=>x.Id==id,ct);return x is null?null:new(x.Id,x.Name,x.Slug,x.ParentId,x.Position,x.Sku,x.ImageUrl);}
    public async Task<AdminSupplierDto?> GetSupplierAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await RequireManager(actor,ct);var x=await db.Query<Supplier>().FirstOrDefaultAsync(x=>x.Id==id,ct);return x is null?null:SupplierDto(x);}
    private static AdminSupplierDto SupplierDto(Supplier s)=>new(s.Id,s.Name,s.OriginJson is null?null:SalesRules.Read<SalesAddressDto>(s.OriginJson),s.IsActive);
    /// <summary>Narrow integration facts for shipping adapters, including active supplier origins.</summary>
    public async Task<IReadOnlyList<SalesCatalogShippingProduct>> ShippingProductsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        var rows=await db.Query<Product>().Where(x=>ids.Contains(x.Id) && x.IsVisible).ToListAsync(ct);
        var supplierIds=rows.SelectMany(x=>x.SupplierIds).Distinct().ToArray();var suppliers=await db.Query<Supplier>().Where(x=>supplierIds.Contains(x.Id) && x.IsActive).ToListAsync(ct);
        return rows.Select(x=>new SalesCatalogShippingProduct(x.Id,x.Name,suppliers.Where(s=>x.SupplierIds.Contains(s.Id)).Select(s=>new SalesSupplierOrigin(s.Id,s.Name,s.OriginJson is null?null:SalesRules.Read<SalesAddressDto>(s.OriginJson))).ToArray())).ToArray();
    }
}
