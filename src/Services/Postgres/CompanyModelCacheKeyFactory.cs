using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Services.Postgres;

public sealed class CompanyModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is CompanyDbContext company
        ? (context.GetType(), company.ModelKey, context.Database.ProviderName, designTime)
        : (object)(context.GetType(), designTime);
}
