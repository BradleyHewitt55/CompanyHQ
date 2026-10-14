using System.Security.Cryptography;
using System.Text;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Dtos;
using CompanyHQ.Services.Sales.Contracts.Forms;
using CompanyHQ.Services.Sales.Contracts.Models;
namespace CompanyHQ.Services.Sales.Services;
internal sealed class SalesCatalogPricesRefreshingException : Exception;
public sealed partial class SalesCatalogService
{
    private static readonly Guid ProjectionId = Guid.Parse("7147ed7e-2a68-4c22-8a2c-c68bad4a370d");
    private const int PriceBatchSize = 32;

    private string PricePolicyKey => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(options))));

    internal async Task EnsurePriceStateAsync(CancellationToken ct)
    {
        if (await db.Query<CatalogPriceProjection>().AnyAsync(x => x.Id == ProjectionId, ct)) return;
        try
        {
            await db.InTransactionAsync(async token =>
            {
                if (await db.Query<CatalogPriceProjection>().AnyAsync(x => x.Id == ProjectionId, token)) return;
                db.Add(new CatalogPriceProjection { Id = ProjectionId, Dirty = true });
                await db.SaveChangesAsync(token);
            }, cancellationToken: ct);
        }
        catch (UniqueConstraintException) { db.Clear(); }
    }

    internal async Task MarkPricesDirtyAsync(CancellationToken ct)
    {
        var state = await db.Locked<CatalogPriceProjection>().Where(x => x.Id == ProjectionId).FirstAsync(ct);
        state.Dirty = true;
        state.LastProductId = null;
        state.Generation = checked(state.Generation + 1);
    }

    internal async Task EnsurePricesAsync(IQuery<Product> candidates, CancellationToken ct)
    {
        await EnsurePriceStateAsync(ct);
        // A request warms one batch of eligible products. The worker handles larger invalidations;
        // incomplete price queries fail explicitly instead of filtering/sorting on stale values.
        await RefreshPriceBatchAsync(ct, candidates);
        var state = await db.Locked<CatalogPriceProjection>().Where(x => x.Id == ProjectionId).FirstAsync(ct);
        var key = PricePolicyKey;
        var date = Today;
        if (await candidates.AnyAsync(p => p.PriceGeneration != state.Generation || p.PriceDate != date || p.PricePolicyKey != key, ct))
            throw new SalesCatalogPricesRefreshingException();
    }

    // Durable generation stamps resume after a restart. Each transaction holds the invalidation
    // lock over at most one batch, with no catalog-wide materialization or row-count ceiling.
    internal async Task<bool> RefreshPriceBatchAsync(CancellationToken ct, IQuery<Product>? candidates = null)
    {
        var key = PricePolicyKey;
        var date = Today;
        var state = await db.Query<CatalogPriceProjection>().FirstOrDefaultAsync(x => x.Id == ProjectionId, ct);
        if (state is { Dirty: false } && state.PriceDate == date && state.PolicyKey == key) return false;

        return await db.InTransactionAsync(async token =>
        {
            var locked = await db.Locked<CatalogPriceProjection>().Where(x => x.Id == ProjectionId).FirstAsync(token);
            if (!locked.Dirty && locked.PriceDate == date && locked.PolicyKey == key) return false;
            if (locked.PriceDate != date || locked.PolicyKey != key)
            {
                locked.Dirty = true;
                locked.LastProductId = null;
                locked.PriceDate = date;
                locked.PolicyKey = key;
                locked.Generation = checked(locked.Generation + 1);
            }
            var generation = locked.Generation;
            var query = db.Query<Product>().Where(p => p.PriceGeneration != generation || p.PriceDate != date || p.PricePolicyKey != key);
            if (candidates is not null) query = query.Where(p => candidates.Any(x => x.Id == p.Id));
            var ids = await query.OrderBy(x => x.Id).Take(PriceBatchSize).Select(x => x.Id).ToArrayAsync(token);

            // Keep snapshot subqueries out of the tracked write query: a no-tracking modifier
            // inside a correlated candidate query can otherwise detach these roots.
            var products = await db.Tracked<Product>().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(token);
            var variants = await db.Tracked<CatalogVariant>().Where(x => ids.Contains(x.ProductId)).ToListAsync(token);
            var byId = variants.ToDictionary(x => x.Id);
            var coupons = await Coupons(token);
            // Load per root so unrelated bundles do not consume one shared graph budget.
            foreach (var product in products)
            {
                var graph = await Graph([product.Id], token);
                var pricing = new CatalogPricing(graph, coupons, date, options);
                var prices = graph[product.Id].Variants.Select(v => (Variant: v, Price: pricing.Price(product.Id, v))).ToArray();
                product.EffectivePrice = prices.Min(x => x.Price.Effective);
                product.IsOnSale = prices.Any(x => x.Price.Effective < x.Price.Selling);
                product.PriceGeneration = generation;
                product.PriceDate = date;
                product.PricePolicyKey = key;
                foreach (var (variant, price) in prices)
                {
                    byId[variant.Id].EffectivePrice = price.Effective;
                    byId[variant.Id].IsOnSale = price.Effective < price.Selling;
                }
            }
            if (products.Count > 0) locked.LastProductId = products[^1].Id;
            await db.SaveChangesAsync(token);
            locked.Dirty = await db.Query<Product>().AnyAsync(p => p.PriceGeneration != generation || p.PriceDate != date || p.PricePolicyKey != key, token);
            await db.SaveChangesAsync(token);
            return locked.Dirty;
        }, cancellationToken: ct);
    }

    public async Task<PagedResult<ClientProductReviewDto>> ReviewsAsync(Guid productId,PageRequest page,CancellationToken ct=default)
    {
        if(!await db.Query<Product>().AnyAsync(x=>x.Id==productId && x.IsVisible,ct))throw new KeyNotFoundException();
        var result=await db.Query<ProductReview>().Where(x=>x.ProductId==productId).OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).ToPageAsync(page.Skip,page.PageSize,ct);
        return new(result.Items.Select(ReviewDto).ToArray(),page.Page,page.PageSize,result.Total);
    }
    public async Task<ClientProductReviewDto> SaveReviewAsync(Guid productId,Guid? reviewId,SaveProductReviewRequest request,Guid? actor,CancellationToken ct=default)
    {
        await RequireManager(actor,ct);SalesRules.Validate(request);return await db.InTransactionAsync(async token=>
        {
            var product=await db.Locked<Product>().FirstOrDefaultAsync(x=>x.Id==productId,token) ?? throw new KeyNotFoundException();
            var row=reviewId.HasValue?await db.Tracked<ProductReview>().FirstOrDefaultAsync(x=>x.Id==reviewId && x.ProductId==productId,token):new ProductReview{ProductId=productId};
            if(row is null)throw new KeyNotFoundException();product.RatingTotal-=row.Rating;if(!reviewId.HasValue){product.ReviewCount++;db.Add(row);}
            row.Title=request.Title.Trim();row.Comment=request.Comment;row.Rating=request.Rating;row.AuthorName=request.AuthorName.Trim();
            product.RatingTotal+=row.Rating;product.AverageRating=SalesRules.Money((decimal)product.RatingTotal/product.ReviewCount);
            await db.SaveChangesAsync(token);return ReviewDto(row);
        },cancellationToken:ct);
    }
    public async Task<bool> DeleteReviewAsync(Guid productId,Guid reviewId,Guid? actor,CancellationToken ct=default)
    {
        await RequireManager(actor,ct);return await db.InTransactionAsync(async token=>
        {var product=await db.Locked<Product>().FirstOrDefaultAsync(x=>x.Id==productId,token);if(product is null)return false;var row=await db.Tracked<ProductReview>().FirstOrDefaultAsync(x=>x.Id==reviewId && x.ProductId==productId,token);if(row is null)return false;product.ReviewCount--;product.RatingTotal-=row.Rating;product.AverageRating=product.ReviewCount>0?SalesRules.Money((decimal)product.RatingTotal/product.ReviewCount):0;db.Remove(row);await db.SaveChangesAsync(token);return true;},cancellationToken:ct);
    }
    private static ClientProductReviewDto ReviewDto(ProductReview r)=>new(r.Id,r.Title,r.Comment,r.Rating,r.AuthorName,r.CreatedAt);
}
