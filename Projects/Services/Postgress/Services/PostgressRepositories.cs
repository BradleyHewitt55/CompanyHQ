using Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PostgressDbContext = CompanyHQ.Services.Postgress.DbContext;

namespace CompanyHQ.Services.Postgress.Services;

public sealed class PostgressRepositories<TEntity>(PostgressDbContext dbContext) : IRepository<TEntity>
    where TEntity : BaseModel
{
    private readonly PostgressDbContext _dbContext = dbContext;

    public async Task<TEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().FindAsync([id], cancellationToken);

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Add(entity);
        await SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Update(entity);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.Set<TEntity>().Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintException("A unique constraint was violated.", exception);
        }
    }
}
