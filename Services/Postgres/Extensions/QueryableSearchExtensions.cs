using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Postgres.Extensions;

/// <summary>Thrown when a caller names a search field their role's shape does not carry.</summary>
public sealed class SearchFieldException(string field, IReadOnlyList<string> allowed)
    : ArgumentException($"'{field}' is not a searchable field. Use one of: {string.Join(", ", allowed)}.")
{
    public string Field { get; } = field;

    public IReadOnlyList<string> Allowed { get; } = allowed;
}

/// <summary>
/// One keyword filter for every list endpoint. The searchable fields are not a hand-kept list: they
/// are the fields of the DTO the caller's role receives, so a caller can search exactly what they are
/// allowed to read and nothing else.
/// </summary>
public static class QueryableSearchExtensions
{
    private static readonly MethodInfo ILike = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string)])!;

    private static readonly ConcurrentDictionary<(Type Model, Type Shape), IReadOnlyDictionary<string, string>> Shared = new();

    /// <summary>
    /// Narrows the query to rows whose <see cref="SearchRequest.Field"/> contains the keyword, without
    /// case. <typeparamref name="TShape"/> is the DTO this caller is about to be handed.
    /// </summary>
    /// <param name="aliases">
    /// Shape field to model path, for a field the model spells differently or reaches through a
    /// navigation (<c>["RoleName"] = "AccessRole.Name"</c>).
    /// </param>
    /// <exception cref="SearchFieldException">The field is not in the shape, or cannot be searched.</exception>
    public static IQueryable<TModel> Search<TModel, TShape>(
        this IQueryable<TModel> query,
        SearchRequest? search,
        IReadOnlyDictionary<string, string>? aliases = null) =>
        query.Search(search, typeof(TShape), aliases);

    /// <inheritdoc cref="Search{TModel,TShape}"/>
    public static IQueryable<TModel> Search<TModel>(
        this IQueryable<TModel> query,
        SearchRequest? search,
        Type shape,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(shape);

        if (string.IsNullOrWhiteSpace(search?.Keyword))
        {
            return query;
        }

        var fields = aliases is null
            ? Shared.GetOrAdd((typeof(TModel), shape), static key => Fields(key.Model, key.Shape, null))
            : Fields(typeof(TModel), shape, aliases);
        var row = Expression.Parameter(typeof(TModel), "row");
        // Read through a closure so the keyword reaches Postgres as a parameter, never as SQL text.
        var pattern = Expression.Property(Expression.Constant(new Pattern(search.Keyword)), nameof(Pattern.Value));

        Expression predicate;
        if (string.IsNullOrWhiteSpace(search.Field))
        {
            // No field named: any text field of the shape may match.
            predicate = fields.Values
                .Select(path => Member(row, path))
                .Where(member => member.Type == typeof(string))
                .Select(member => Matches(member, pattern))
                .DefaultIfEmpty(Expression.Constant(false))
                .Aggregate(Expression.OrElse);
        }
        else if (fields.TryGetValue(search.Field.Trim(), out var path))
        {
            predicate = Matches(Member(row, path), pattern);
        }
        else
        {
            throw new SearchFieldException(search.Field.Trim(), Names(fields));
        }

        return query.Where(Expression.Lambda<Func<TModel, bool>>(predicate, row));
    }

    private static IReadOnlyList<string> Names(IReadOnlyDictionary<string, string> fields) =>
        fields.Keys.Select(JsonName).Order(StringComparer.Ordinal).ToArray();

    private static string JsonName(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    /// <summary>Shape field name (matched without case) to the model path that backs it.</summary>
    private static IReadOnlyDictionary<string, string> Fields(
        Type model,
        Type shape,
        IReadOnlyDictionary<string, string>? aliases)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in shape.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var path = aliases is not null && aliases.TryGetValue(property.Name, out var alias) ? alias : property.Name;
            if (Resolve(model, path) is { } type && IsSearchable(type))
            {
                fields[property.Name] = path;
            }
        }

        return fields;
    }

    private static Type? Resolve(Type model, string path)
    {
        var type = model;
        foreach (var segment in path.Split('.'))
        {
            var property = type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                return null;
            }

            type = property.PropertyType;
        }

        return type;
    }

    private static bool IsSearchable(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string) || type == typeof(Guid) || type == typeof(bool)
            || type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double)
            || type == typeof(DateTimeOffset) || type.IsEnum;
    }

    private static Expression Member(Expression row, string path) =>
        path.Split('.').Aggregate(row, (current, segment) => Expression.Property(
            current,
            current.Type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!));

    // A column that is not text is compared as Postgres prints it: CAST(column AS text) ILIKE pattern.
    private static Expression Matches(Expression member, Expression pattern) =>
        Expression.Call(
            ILike,
            Expression.Constant(EF.Functions),
            member.Type == typeof(string)
                ? member
                : Expression.Convert(Expression.Convert(member, typeof(object)), typeof(string)),
            pattern);

    private sealed class Pattern(string keyword)
    {
        // The keyword is a literal: its own %, _ and \ must not act as wildcards.
        public string Value { get; } =
            $"%{keyword.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
    }
}
