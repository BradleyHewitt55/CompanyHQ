using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Postgres.Extensions;

namespace CompanyHQ.Postgres.Data;

/// <summary>The description behind a neutral query, as this provider holds it.</summary>
internal interface IPostgresQuery
{
    Expression Expression { get; }
}

/// <summary>
/// <see cref="IQuery{T}"/> over EF Core. Every operator adds to an <see cref="IQueryable{T}"/> that
/// never leaves this class, so the SQL a query produces is exactly what the same LINQ produced
/// when services held the queryable themselves.
/// </summary>
internal sealed class PostgresQuery<T>(IQueryable<T> source) : IOrderedQuery<T>, IPostgresQuery
{
    private const string OutsideExpression =
        "This form is a correlated subquery: it is only valid inside the expression of another query. Await the Async form to run a query.";

    Expression IPostgresQuery.Expression => source.Expression;

    public IQuery<T> Where(Expression<Func<T, bool>> predicate) =>
        new PostgresQuery<T>(source.Where(NeutralExpression.Translate(predicate)));

    public IQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector) =>
        new PostgresQuery<TResult>(source.Select(NeutralExpression.Translate(selector)));

    public IQuery<TResult> Join<TInner, TKey, TResult>(
        IQuery<TInner> inner,
        Expression<Func<T, TKey>> outerKey,
        Expression<Func<TInner, TKey>> innerKey,
        Expression<Func<T, TInner, TResult>> result) =>
        new PostgresQuery<TResult>(source.Join(
            Unwrap(inner),
            NeutralExpression.Translate(outerKey),
            NeutralExpression.Translate(innerKey),
            NeutralExpression.Translate(result)));

    public IOrderedQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key) =>
        new PostgresQuery<T>(source.OrderBy(NeutralExpression.Translate(key)));

    public IOrderedQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key) =>
        new PostgresQuery<T>(source.OrderByDescending(NeutralExpression.Translate(key)));

    public IOrderedQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> key) =>
        new PostgresQuery<T>(Ordered.ThenBy(NeutralExpression.Translate(key)));

    public IOrderedQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key) =>
        new PostgresQuery<T>(Ordered.ThenByDescending(NeutralExpression.Translate(key)));

    public IQuery<T> Skip(int count) => new PostgresQuery<T>(source.Skip(count));

    public IQuery<T> Take(int count) => new PostgresQuery<T>(source.Take(count));

    public IQuery<T> Distinct() => new PostgresQuery<T>(source.Distinct());

    public IQuery<T> Search(string? field, string? keyword, Type shape, IReadOnlyDictionary<string, string>? aliases = null) =>
        new PostgresQuery<T>(source.Search(SearchRequest.For(keyword, field), shape, aliases));

    public Task<List<T>> ToListAsync(CancellationToken cancellationToken = default) => source.ToListAsync(cancellationToken);

    public Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default) => source.ToArrayAsync(cancellationToken);

    public Task<HashSet<T>> ToHashSetAsync(CancellationToken cancellationToken = default) => source.ToHashSetAsync(cancellationToken);

    public Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(
        Func<T, TKey> key,
        Func<T, TValue> value,
        CancellationToken cancellationToken = default)
        where TKey : notnull =>
        source.ToDictionaryAsync(key, value, cancellationToken);

    public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default) => source.FirstOrDefaultAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        source.FirstOrDefaultAsync(NeutralExpression.Translate(predicate), cancellationToken);

    public Task<T> FirstAsync(CancellationToken cancellationToken = default) => source.FirstAsync(cancellationToken);

    public Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default) => source.SingleOrDefaultAsync(cancellationToken);

    public Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        source.SingleOrDefaultAsync(NeutralExpression.Translate(predicate), cancellationToken);

    public Task<T> SingleAsync(CancellationToken cancellationToken = default) => source.SingleAsync(cancellationToken);

    public Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        source.SingleAsync(NeutralExpression.Translate(predicate), cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => source.AnyAsync(cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        source.AnyAsync(NeutralExpression.Translate(predicate), cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => source.CountAsync(cancellationToken);

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        source.CountAsync(NeutralExpression.Translate(predicate), cancellationToken);

    public Task<Dictionary<TKey, int>> CountByAsync<TKey>(Expression<Func<T, TKey>> key, CancellationToken cancellationToken = default)
        where TKey : notnull =>
        source.GroupBy(NeutralExpression.Translate(key))
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

    public async Task<Page<T>> ToPageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var total = await source.CountAsync(cancellationToken);

        return new Page<T>(await source.Skip(skip).Take(take).ToListAsync(cancellationToken), total);
    }

    public async Task<Page<TItem>> ToPageAsync<TItem>(
        int skip,
        int take,
        Expression<Func<T, TItem>> selector,
        CancellationToken cancellationToken = default)
    {
        // Counted and windowed over the model, projected last: a filter or count applied to the
        // projected shape is something the provider cannot translate.
        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip(skip).Take(take).Select(NeutralExpression.Translate(selector)).ToListAsync(cancellationToken);

        return new Page<TItem>(items, total);
    }

    public Task<int> DeleteAsync(CancellationToken cancellationToken = default) =>
        PostgresFailure.GuardAsync(() => source.ExecuteDeleteAsync(cancellationToken));

    public Task<int> UpdateAsync(Action<IUpdateSetters<T>> set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);

        var setters = new Setters();
        set(setters);

        return PostgresFailure.GuardAsync(() => source.ExecuteUpdateAsync(setters.Apply, cancellationToken));
    }

    public bool Any() => throw new InvalidOperationException(OutsideExpression);

    public bool Any(Expression<Func<T, bool>> predicate) => throw new InvalidOperationException(OutsideExpression);

    public int Count() => throw new InvalidOperationException(OutsideExpression);

    public int Count(Expression<Func<T, bool>> predicate) => throw new InvalidOperationException(OutsideExpression);

    public T? FirstOrDefault() => throw new InvalidOperationException(OutsideExpression);

    public bool Contains(T value) => throw new InvalidOperationException(OutsideExpression);

    private IOrderedQueryable<T> Ordered => source as IOrderedQueryable<T>
        ?? throw new InvalidOperationException("A tie-breaker needs a primary order first.");

    private static IQueryable<TOther> Unwrap<TOther>(IQuery<TOther> query) =>
        query is PostgresQuery<TOther> postgres
            ? postgres.Source
            : throw new NotSupportedException("A query can only be combined with another query from the same provider.");

    private IQueryable<T> Source => source;

    private sealed class Setters : IUpdateSetters<T>
    {
        private readonly List<Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<T>>> calls = [];

        public IUpdateSetters<T> Set<TProperty>(Expression<Func<T, TProperty>> property, TProperty value)
        {
            calls.Add(builder => builder.SetProperty(property, value));

            return this;
        }

        public IUpdateSetters<T> Set<TProperty>(Expression<Func<T, TProperty>> property, Expression<Func<T, TProperty>> value)
        {
            var translated = NeutralExpression.Translate(value);
            calls.Add(builder => builder.SetProperty(property, translated));

            return this;
        }

        public void Apply(Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<T> builder)
        {
            foreach (var call in calls)
            {
                call(builder);
            }
        }
    }
}

/// <summary>
/// Turns the expression of a neutral query into one EF Core can translate: a neutral query used
/// inside it becomes the subquery it describes, and a neutral function becomes this provider's own.
/// </summary>
/// <remarks>
/// It also keeps the contract honest in the other direction. An expression that names an EF Core
/// or Npgsql API is refused here, so a service cannot quietly depend on this provider through a
/// query that looks neutral.
/// </remarks>
internal sealed class NeutralExpression : ExpressionVisitor
{
    private static readonly MethodInfo ILike = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    public static Expression<TDelegate> Translate<TDelegate>(Expression<TDelegate> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return (Expression<TDelegate>)new NeutralExpression().Visit(expression)!;
    }

    public override Expression? Visit(Expression? node)
    {
        // A neutral query that depends on nothing in this expression was built outside it — a
        // captured variable, or a call such as Query<T>() — so it is evaluated and its
        // description inlined as a subquery.
        if (node is not null && IsQuery(node.Type) && !FreeParameters.In(node))
        {
            return Evaluate(node) is IPostgresQuery query
                ? query.Expression
                : throw new NotSupportedException("A query can only be combined with another query from the same provider.");
        }

        return base.Visit(node);
    }

    protected override Expression VisitUnary(UnaryExpression node) =>
        // The compiler inserts a conversion where an ordered query is passed as a plain one.
        node.NodeType is ExpressionType.Convert or ExpressionType.TypeAs && IsQuery(node.Type)
            ? Visit(node.Operand)!
            : base.VisitUnary(node);

    protected override Expression VisitMember(MemberExpression node)
    {
        Refuse(node.Member);

        return base.VisitMember(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;

        if (method.DeclaringType == typeof(QueryFunctions) && method.Name == nameof(QueryFunctions.LikeIgnoreCase))
        {
            return Expression.Call(ILike, Expression.Constant(EF.Functions), Visit(node.Arguments[0])!, Visit(node.Arguments[1])!, Expression.Constant(@"\"));
        }

        if (node.Object is not null && method.DeclaringType is { IsGenericType: true } declaring && IsQuery(declaring))
        {
            var element = declaring.GetGenericArguments()[0];
            var source = Visit(node.Object)!;
            var arguments = node.Arguments.Select(argument => Visit(argument)!).ToArray();
            var generic = method.IsGenericMethod ? method.GetGenericArguments() : [];

            return method.Name switch
            {
                nameof(IQuery<object>.Where) or nameof(IQuery<object>.Skip) or nameof(IQuery<object>.Take)
                    or nameof(IQuery<object>.Distinct) or nameof(IQuery<object>.Any) or nameof(IQuery<object>.Count)
                    or nameof(IQuery<object>.FirstOrDefault) or nameof(IQuery<object>.Contains) =>
                    Expression.Call(typeof(Queryable), method.Name, [element], [source, .. arguments]),
                nameof(IQuery<object>.Select) or nameof(IQuery<object>.OrderBy) or nameof(IQuery<object>.OrderByDescending)
                    or nameof(IOrderedQuery<object>.ThenBy) or nameof(IOrderedQuery<object>.ThenByDescending) =>
                    Expression.Call(typeof(Queryable), method.Name, [element, generic[0]], source, arguments[0]),
                nameof(IQuery<object>.Join) =>
                    Expression.Call(typeof(Queryable), method.Name, [element, generic[0], generic[1], generic[2]], [source, .. arguments]),
                _ => throw new NotSupportedException(
                    $"{method.Name} runs a query; it cannot be used inside the expression of another one.")
            };
        }

        Refuse(method);

        return base.VisitMethodCall(node);
    }

    private static bool IsQuery(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(IQuery<>) || definition == typeof(IOrderedQuery<>) || definition == typeof(PostgresQuery<>));

    private static void Refuse(MemberInfo member)
    {
        var assembly = member.DeclaringType?.Assembly.GetName().Name;

        if (assembly is not null
            && (assembly.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || assembly.StartsWith("Npgsql", StringComparison.Ordinal)))
        {
            throw new NotSupportedException(
                $"{member.DeclaringType!.Name}.{member.Name} belongs to the database provider and cannot be used in a query. " +
                $"Use {nameof(QueryFunctions)}, or the provider's own surface where a provider feature is genuinely required.");
        }
    }

    private static object? Evaluate(Expression node) => node switch
    {
        ConstantExpression constant => constant.Value,
        MemberExpression { Expression: ConstantExpression closure, Member: FieldInfo field } => field.GetValue(closure.Value),
        MemberExpression { Expression: ConstantExpression closure, Member: PropertyInfo property } => property.GetValue(closure.Value),
        _ => Expression.Lambda<Func<object?>>(Expression.Convert(node, typeof(object))).Compile(preferInterpretation: true)()
    };

    /// <summary>Whether an expression uses a parameter it does not declare itself, i.e. depends on the row around it.</summary>
    private sealed class FreeParameters : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> declared = [];
        private bool found;

        public static bool In(Expression node)
        {
            var finder = new FreeParameters();
            finder.Visit(node);

            return finder.found;
        }

        protected override Expression VisitLambda<TDelegate>(Expression<TDelegate> node)
        {
            foreach (var parameter in node.Parameters)
            {
                declared.Add(parameter);
            }

            return base.VisitLambda(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            found |= !declared.Contains(node);

            return node;
        }
    }
}
