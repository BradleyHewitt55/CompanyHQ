using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Services.Companies.Contracts.Metadata;
using Services.Companies.Contracts.Services;

namespace Services.Postgres.Metadata;

public sealed class RelatedRecordsService(CompanyDbContext database, EntityTypeRegistry types) : IRelatedRecordsService
{
    public async Task<IReadOnlyList<RelatedRecord>> GetFor(string sourceEntity, Guid sourceId, CancellationToken cancellationToken = default)
    {
        var source = await database.EntityDefinitions.SingleOrDefaultAsync(entity => entity.Name == sourceEntity, cancellationToken);
        if (source is null) return [];
        var links = await database.LinkDefinitions.Where(link => link.SourceEntityDefinitionId == source.Id).ToListAsync(cancellationToken);
        var targets = await database.EntityDefinitions.Where(entity => links.Select(link => link.TargetEntityDefinitionId).Contains(entity.Id)).ToListAsync(cancellationToken);
        var results = new List<RelatedRecord>();
        foreach (var target in targets)
        {
            var clrType = types.Types.SingleOrDefault(type => type.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));
            var foreignKey = clrType?.GetProperty(sourceEntity + "Id");
            if (clrType is null || foreignKey is null) continue;
            var propertyType = foreignKey.PropertyType;
            if (propertyType != typeof(Guid) && propertyType != typeof(Guid?)) continue;
            var set = (IQueryable)typeof(DbContext).GetMethods().Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethod && method.GetParameters().Length == 0).MakeGenericMethod(clrType).Invoke(database, null)!;
            var parameter = Expression.Parameter(clrType, "record");
            Expression member = Expression.Property(parameter, foreignKey);
            Expression id = propertyType == typeof(Guid?) ? Expression.Convert(Expression.Constant(sourceId), typeof(Guid?)) : Expression.Constant(sourceId);
            var predicate = Expression.Lambda(Expression.Equal(member, id), parameter);
            var query = (IQueryable)typeof(Queryable).GetMethods().Single(method => method.Name == nameof(Queryable.Where) && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
                .MakeGenericMethod(clrType).Invoke(null, [set, predicate])!;
            foreach (var record in (IEnumerable)query)
            {
                var recordId = (Guid)(clrType.GetProperty("Id")?.GetValue(record) ?? Guid.Empty);
                var label = clrType.GetProperty("Slug")?.GetValue(record)?.ToString() ?? recordId.ToString();
                results.Add(new RelatedRecord(target.Name, recordId, label));
            }
        }
        return results;
    }
}
