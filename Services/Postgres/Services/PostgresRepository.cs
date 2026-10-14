using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Postgres.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Data;

namespace CompanyHQ.Postgres.Services;

/// <inheritdoc cref="IRepository{TModel}"/>
internal sealed class PostgresRepository<TModel>(PostgresDbContext context)
    : IRepository<TModel> where TModel : BaseModel
{
    private DbSet<TModel> Set => context.Set<TModel>();

    public IQuery<TModel> Query() => new PostgresQuery<TModel>(Set.AsNoTracking());

    public Task<TModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(model => model.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TModel>> ListAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        await Filtered(predicate).ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().AnyAsync(model => model.Id == id, cancellationToken);

    public Task<int> CountAsync(
        Expression<Func<TModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default) =>
        Filtered(predicate).CountAsync(cancellationToken);

    public async Task<TModel> AddAsync(TModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            Set.Add(model);
            await context.SaveChangesAsync(cancellationToken);
            return model;
        }
        catch (DbUpdateException exception)
        {
            // A caller may deliberately retry a unique-key race. Do not leave the failed Added
            // entry in this scoped context, or the next save would replay it as well.
            context.Entry(model).State = EntityState.Detached;
            throw PostgresFailure.Translate(exception) ?? exception;
        }
    }

    public async Task<IReadOnlyList<TModel>> AddRangeAsync(
        IEnumerable<TModel> models,
        CancellationToken cancellationToken = default)
    {
        var materialised = models as IReadOnlyList<TModel> ?? [.. models];

        Set.AddRange(materialised);
        await PostgresFailure.GuardAsync(() => context.SaveChangesAsync(cancellationToken));

        return materialised;
    }

    public async Task<TModel> UpdateAsync(TModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Id == Guid.Empty)
        {
            throw new ArgumentException("Cannot update a model with no id.", nameof(model));
        }

        // Reads hand back snapshots, so an update usually arrives detached.
        if (context.Entry(model).State is EntityState.Detached)
        {
            Set.Update(model);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return model;
        }
        catch (DbUpdateException exception)
        {
            context.Entry(model).State = EntityState.Detached;
            throw PostgresFailure.Translate(exception) ?? exception;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await PostgresFailure.GuardAsync(() => Set.Where(model => model.Id == id).ExecuteDeleteAsync(cancellationToken)) > 0;

    public Task<int> DeleteAsync(
        Expression<Func<TModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return PostgresFailure.GuardAsync(() => Set.Where(NeutralExpression.Translate(predicate)).ExecuteDeleteAsync(cancellationToken));
    }

    private IQueryable<TModel> Filtered(Expression<Func<TModel, bool>>? predicate)
    {
        var query = Set.AsNoTracking();

        return predicate is null ? query : query.Where(NeutralExpression.Translate(predicate));
    }
}
