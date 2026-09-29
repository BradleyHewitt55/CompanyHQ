using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Models;
using CompanyHQ.Services.Postgres.Data;
using CompanyHQ.Services.Postgres.Extensions;

namespace CompanyHQ.Services.Postgres.Services;

/// <inheritdoc cref="IPostgresService{TModel}"/>
public sealed class PostgresService<TModel>(PostgresDbContext context)
    : IPostgresService<TModel> where TModel : BaseModel
{
    private DbSet<TModel> Set => context.Set<TModel>();

    public IQueryable<TModel> Query() => Set.AsNoTracking();

    public Task<TModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(model => model.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TModel>> ListAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        await Filtered(predicate).ToListAsync(cancellationToken);

    public Task<PagedResult<TModel>> PageAsync(
        PageRequest page,
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        Filtered(predicate).ToPagedResultAsync(page, cancellationToken: cancellationToken);

    public Task<PagedResult<TItem>> PageAsync<TItem>(
        PageRequest page,
        Expression<Func<TModel, TItem>> selector,
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        Filtered(predicate).ToPagedResultAsync(page, selector, cancellationToken: cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().AnyAsync(model => model.Id == id, cancellationToken);

    public Task<int> CountAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        predicate is null
            ? Set.AsNoTracking().CountAsync(cancellationToken)
            : Set.AsNoTracking().CountAsync(predicate, cancellationToken);

    public async Task<TModel> AddAsync(TModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            await Set.AddAsync(model, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return model;
        }
        catch (DbUpdateException)
        {
            // A caller may deliberately retry a unique-key race. Do not leave the failed Added
            // entry in this scoped context, or the next SaveChanges would replay it as well.
            context.Entry(model).State = EntityState.Detached;
            throw;
        }
    }

    public async Task<IReadOnlyList<TModel>> AddRangeAsync(
        IEnumerable<TModel> models,
        CancellationToken cancellationToken = default)
    {
        var materialised = models as IReadOnlyList<TModel> ?? [.. models];

        await Set.AddRangeAsync(materialised, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return materialised;
    }

    public async Task<TModel> UpdateAsync(TModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Id == Guid.Empty)
        {
            throw new ArgumentException("Cannot update a model with no id.", nameof(model));
        }

        // Reads hand back untracked instances, so an update usually arrives detached.
        if (context.Entry(model).State is EntityState.Detached)
        {
            Set.Update(model);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return model;
        }
        catch (DbUpdateException)
        {
            context.Entry(model).State = EntityState.Detached;
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await Set.Where(model => model.Id == id).ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public Task<int> DeleteAsync(
        Expression<Func<TModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return Set.Where(predicate).ExecuteDeleteAsync(cancellationToken);
    }

    private IQueryable<TModel> Filtered(Expression<Func<TModel, bool>>? predicate)
    {
        var query = Set.AsNoTracking();

        return predicate is null ? query : query.Where(predicate);
    }
}
