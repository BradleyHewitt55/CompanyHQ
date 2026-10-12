using CompanyHQ.Services.Postgres.Contracts.Models;
using CompanyHQ.Services.Persistence.Services;

namespace CompanyHQ.Services.Persistence.Extensions;

/// <summary>Shorthand built only from the operators <see cref="IQuery{T}"/> already has.</summary>
public static class QueryExtensions
{
    /// <summary>
    /// Newest first, tie-broken by id: the total order every list uses unless it has a reason for
    /// another. The id makes it total, so paging can neither repeat nor drop a row.
    /// </summary>
    public static IOrderedQuery<TModel> NewestFirst<TModel>(this IQuery<TModel> query) where TModel : BaseModel
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.OrderByDescending(model => model.CreatedAt).ThenBy(model => model.Id);
    }
}
