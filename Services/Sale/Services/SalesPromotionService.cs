using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Enums;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Models;
namespace CompanyHQ.Sales.Services;

/// <summary>Persisted coupon, relationship and checkout policy management.</summary>
public sealed class SalesPromotionService(IServiceAvailability? availability, IPersistence db,SalesCatalogService catalog) : ISalesPromotionService
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public SalesPromotionService(IPersistence db,SalesCatalogService catalog) : this(null, db, catalog) { }

    internal static readonly Guid SettingsId=Guid.Parse("7147ed7e-2a68-4c22-8a2c-c68bad4a370a");
    public async Task<PagedResult<AdminCouponDto>> CouponsAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);var result=await db.Query<Coupon>().Search(search.Field,search.Keyword,typeof(AdminCouponDto)).OrderBy(x=>x.Code).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);return new(result.Items.Select(CouponDto).ToArray(),page.Page,page.PageSize,result.Total);}
    public async Task<AdminCouponDto> SaveCouponAsync(Guid? id,SaveCouponRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        await catalog.RequireManager(actor,ct);SalesRules.Validate(request);
        if(!Enum.IsDefined(request.DiscountType) || request.DiscountType==DiscountType.FreeShipping && !request.AppliesToShipping ||
            request.DiscountType==DiscountType.Percentage && request.DiscountValue>100 || request.StartDate>request.EndDate ||
            request.ShippingOriginLatitude.HasValue!=request.ShippingOriginLongitude.HasValue ||
            !request.AppliesToShipping && (request.ShippingMaxDistanceKm.HasValue || request.ShippingOriginLatitude.HasValue) ||
            (request.ProductIds?.Count ?? 0)>100 || (request.CategoryIds?.Count ?? 0)>100)throw new ArgumentException("Invalid coupon definition.");
        var products=(request.ProductIds ?? []).Distinct().ToArray();var categories=(request.CategoryIds ?? []).Distinct().ToArray();
        if(await db.Query<Product>().CountAsync(x=>products.Contains(x.Id),ct)!=products.Length || await db.Query<Category>().CountAsync(x=>categories.Contains(x.Id),ct)!=categories.Length) throw new ArgumentException("Coupon target not found.");
        await catalog.EnsurePriceStateAsync(ct);
        return await db.InTransactionAsync(async token=>
        {
        var row=id.HasValue?await db.FindAsync<Coupon>(id.Value,ct):new Coupon();if(row is null)throw new KeyNotFoundException();
        request=request with {Code=SalesRules.Normalize(request.Code)};
        row.Code=request.Code;row.IsAutomatic=request.IsAutomatic;row.IsActive=request.IsActive;row.AppliesToShipping=request.AppliesToShipping;row.DefinitionJson=SalesRules.Write(request);
        if(!id.HasValue)db.Add(row);await catalog.MarkPricesDirtyAsync(token);await db.SaveChangesAsync(token);return CouponDto(row);
        },cancellationToken:ct);
    }
    public async Task<bool> DeleteCouponAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);await catalog.EnsurePriceStateAsync(ct);return await db.InTransactionAsync(async token=>{await catalog.MarkPricesDirtyAsync(token);var deleted=await db.Query<Coupon>().Where(x=>x.Id==id).DeleteAsync(token)>0;await db.SaveChangesAsync(token);return deleted;},cancellationToken:ct);}
    public async Task<AdminCouponDto?> GetCouponAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);var row=await db.Query<Coupon>().FirstOrDefaultAsync(x=>x.Id==id,ct);return row is null?null:CouponDto(row);}
    public async Task<AdminUpsellOfferDto?> GetUpsellAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);var row=await db.Query<UpsellOffer>().FirstOrDefaultAsync(x=>x.Id==id,ct);if(row is null)return null;var ids=new[]{row.ParentProductId,row.ProductId};var names=await db.Query<Product>().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);return UpsellDto(row,names);}
    public async Task<ClientPaymentMethodDto?> GetPaymentMethodAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);var manager=await catalog.Manager(actor,ct);var row=await db.Query<PaymentMethod>().FirstOrDefaultAsync(x=>x.Id==id && (manager || x.IsActive),ct);return row is null?null:PaymentDto(row,manager);}
    private AdminCouponDto CouponDto(Coupon row)
    {var c=SalesRules.Read<SaveCouponRequest>(row.DefinitionJson);return new(row.Id,c.Code,c.Description,c.DiscountType,c.DiscountValue,c.IsAutomatic,c.MinimumPurchase,c.StartDate,c.EndDate,c.IsActive,c.AllowsCombination,c.AppliesToShipping,c.ShippingOriginLatitude,c.ShippingOriginLongitude,c.ShippingMaxDistanceKm,c.ProductIds ?? [],c.CategoryIds ?? [],SalesRules.Valid(c,catalog.Today),row.CreatedAt,row.UpdatedAt);}
    public async Task<PagedResult<AdminUpsellOfferDto>> UpsellsAsync(PageRequest page,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);var result=await db.Query<UpsellOffer>().OrderBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);var ids=result.Items.SelectMany(x=>new[]{x.ProductId,x.ParentProductId}).Distinct().ToArray();var names=await db.Query<Product>().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);return new(result.Items.Select(x=>UpsellDto(x,names)).ToArray(),page.Page,page.PageSize,result.Total);}
    public async Task<AdminUpsellOfferDto> SaveUpsellAsync(Guid? id,SaveUpsellRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        await catalog.RequireManager(actor,ct);SalesRules.Validate(request);
        if(request.ParentProductId==request.ProductId || request.ValueType==DiscountType.FreeShipping || !Enum.IsDefined(request.ValueType) || request.ValueType==DiscountType.Percentage && request.Value>100)throw new ArgumentException("Invalid upsell relationship.");
        var ids=new[]{request.ParentProductId,request.ProductId};var names=await db.Query<Product>().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);
        if(names.Count!=2)throw new ArgumentException("Upsell products not found.");
        var row=id.HasValue?await db.FindAsync<UpsellOffer>(id.Value,ct):new UpsellOffer();if(row is null)throw new KeyNotFoundException();
        row.ParentProductId=request.ParentProductId;row.ProductId=request.ProductId;row.DefinitionJson=SalesRules.Write(request);if(!id.HasValue)db.Add(row);await db.SaveChangesAsync(ct);return UpsellDto(row,names);
    }
    private static AdminUpsellOfferDto UpsellDto(UpsellOffer row,Dictionary<Guid,string> names)
    {var u=SalesRules.Read<SaveUpsellRequest>(row.DefinitionJson);return new(row.Id,row.ParentProductId,names.GetValueOrDefault(row.ParentProductId),row.ProductId,names.GetValueOrDefault(row.ProductId),u.Quantity,u.Value,u.ValueType,u.SyncVariants,row.CreatedAt,row.UpdatedAt);}
    public async Task<bool> DeleteUpsellAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);return await db.Query<UpsellOffer>().Where(x=>x.Id==id).DeleteAsync(ct)>0;}
    public async Task<AdminSalesSettingsDto> SettingsAsync(Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);var row=await db.Query<SalesSettings>().FirstOrDefaultAsync(x=>x.Id==SettingsId,ct);return new(row?.UpsellExtraDiscountPercent ?? 0,row?.UpdatedAt);}
    public async Task<AdminSalesSettingsDto> SaveSettingsAsync(SaveSalesSettingsRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);SalesRules.Validate(request);var row=await db.FindAsync<SalesSettings>(SettingsId,ct);if(row is null){row=new(){Id=SettingsId};db.Add(row);}row.UpsellExtraDiscountPercent=request.UpsellExtraDiscountPercent;await db.SaveChangesAsync(ct);return new(row.UpsellExtraDiscountPercent,row.UpdatedAt);}
    public async Task<PagedResult<ClientPaymentMethodDto>> PaymentMethodsAsync(PageRequest page,SearchRequest search,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);var manager=await catalog.Manager(actor,ct);var result=await db.Query<PaymentMethod>().Where(x=>manager || x.IsActive).Search(search.Field,search.Keyword,typeof(ClientPaymentMethodDto)).OrderBy(x=>x.Name).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);return new(result.Items.Select(x=>PaymentDto(x,manager)).ToArray(),page.Page,page.PageSize,result.Total);}
    public async Task<AdminPaymentMethodDto> SavePaymentMethodAsync(Guid? id,SavePaymentMethodRequest request,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);SalesRules.Validate(request);if(request.MinOrderTotal>request.MaxOrderTotal)throw new ArgumentException("Payment method minimum exceeds maximum.");var row=id.HasValue?await db.FindAsync<PaymentMethod>(id.Value,ct):new PaymentMethod();if(row is null)throw new KeyNotFoundException();row.Name=request.Name.Trim();row.IsActive=request.IsActive;row.DefinitionJson=SalesRules.Write(request);if(!id.HasValue)db.Add(row);await db.SaveChangesAsync(ct);return (AdminPaymentMethodDto)PaymentDto(row,true);}
    public async Task<bool> DeletePaymentMethodAsync(Guid id,Guid? actor,CancellationToken ct=default)
    {
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);await catalog.RequireManager(actor,ct);return await db.Query<PaymentMethod>().Where(x=>x.Id==id).DeleteAsync(ct)>0;}
    internal static ClientPaymentMethodDto PaymentDto(PaymentMethod row,bool admin)
    {var p=SalesRules.Read<SavePaymentMethodRequest>(row.DefinitionJson);return admin?new AdminPaymentMethodDto(row.Id,p.Name,p.MaxOrderTotal,p.MaxDistanceKm,p.CheckoutNote,p.IsActive,row.CreatedAt,row.UpdatedAt,p.MinOrderTotal):new ClientPaymentMethodDto(row.Id,p.Name,p.MaxOrderTotal,p.MaxDistanceKm,p.CheckoutNote,p.MinOrderTotal);}
}

