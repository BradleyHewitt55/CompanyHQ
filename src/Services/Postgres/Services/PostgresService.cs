using System.Linq.Expressions;
using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;
using Microsoft.EntityFrameworkCore;

namespace Services.Postgres.Services;

public sealed class PostgresService<T>(IDbContextFactory<CompanyDbContext> contextFactory) : IEntityService<T> where T : class, IEntityRecord
{
    public async Task<IReadOnlyList<T>> Get(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<T?> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<T>().AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetBy(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<T>().AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<T> Save(T entity, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
            context.Set<T>().Add(entity);
        }
        else
        {
            var existing = await context.Set<T>().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken);
            if (existing is null) context.Set<T>().Add(entity);
            else context.Entry(existing).CurrentValues.SetValues(entity);
        }
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<T>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return false;
        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
