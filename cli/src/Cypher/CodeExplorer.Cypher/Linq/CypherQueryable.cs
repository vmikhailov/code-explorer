using System.Collections;
using System.Collections.Concurrent;
using CodeExplorer.Cypher.Ast;
using LinqExpression = System.Linq.Expressions.Expression;

namespace CodeExplorer.Cypher.Linq;

public class CypherQueryProvider : IQueryProvider
{
    private static readonly ConcurrentDictionary<Type, Func<CypherQueryProvider, LinqExpression, IQueryable>> _queryableFactories = new();

    private readonly ICypherQueryExecutor _executor;

    public CypherQueryProvider(ICypherQueryExecutor executor)
    {
        _executor = executor;
    }

    public ICypherQueryExecutor Executor => _executor;

    public IQueryable CreateQuery(LinqExpression expression)
    {
        var elementType = TypeHelper.GetElementType(expression.Type);
        var factory = _queryableFactories.GetOrAdd(elementType, static elemType =>
        {
            var queryableType = typeof(CypherQueryable<>).MakeGenericType(elemType);
            var ctor = queryableType.GetConstructor([typeof(CypherQueryProvider), typeof(LinqExpression)])!;
            var providerParam = LinqExpression.Parameter(typeof(CypherQueryProvider), "provider");
            var exprParam = LinqExpression.Parameter(typeof(LinqExpression), "expression");
            var newExpr = LinqExpression.New(ctor, providerParam, exprParam);
            var castExpr = LinqExpression.Convert(newExpr, typeof(IQueryable));
            return LinqExpression.Lambda<Func<CypherQueryProvider, LinqExpression, IQueryable>>(
                castExpr, providerParam, exprParam).Compile();
        });

        return factory(this, expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(LinqExpression expression)
    {
        return new CypherQueryable<TElement>(this, expression);
    }

    public object? Execute(LinqExpression expression)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use async execution methods (ToListAsync, CountAsync, FirstOrDefaultAsync).");
    }

    public TResult Execute<TResult>(LinqExpression expression)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use async execution methods (ToListAsync, CountAsync, FirstOrDefaultAsync).");
    }

    public Task<int> ExecuteCountAsync(LinqExpression expression, Type entityType, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, entityType);
        model.IsCountQuery = true;
        var ast = model.BuildAst();
        return _executor.ExecuteCountAsync(ast, model.Parameters, ct);
    }

    public Task<bool> ExecuteAnyAsync(LinqExpression expression, Type entityType, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, entityType);
        model.IsAnyQuery = true;
        var ast = model.BuildAst();
        return _executor.ExecuteAnyAsync(ast, model.Parameters, ct);
    }

    public Task<List<TElement>> ExecuteListAsync<TElement>(LinqExpression expression, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, typeof(TElement));
        var ast = model.BuildAst();
        return _executor.ExecuteListAsync<TElement>(ast, model.Parameters, ct);
    }

    public Task<TElement?> ExecuteFirstOrDefaultAsync<TElement>(LinqExpression expression, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, typeof(TElement));
        model.LimitCount = 1;
        var ast = model.BuildAst();
        return _executor.ExecuteFirstOrDefaultAsync<TElement>(ast, model.Parameters, ct);
    }
}

public class CypherQueryable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly CypherQueryProvider _provider;
    private readonly LinqExpression _expression;

    public CypherQueryable(CypherQueryProvider provider)
    {
        _provider = provider;
        _expression = LinqExpression.Constant(this);
    }

    public CypherQueryable(CypherQueryProvider provider, LinqExpression expression)
    {
        _provider = provider;
        _expression = expression;
    }

    public Type ElementType => typeof(T);

    public LinqExpression Expression => _expression;

    public IQueryProvider Provider => _provider;

    public CypherQuery ToCypherQuery()
    {
        var model = CypherExpressionVisitor.Translate(_expression, typeof(T));
        return model.BuildAst();
    }

    public CypherQueryModel GetQueryModel()
    {
        return CypherExpressionVisitor.Translate(_expression, typeof(T));
    }

    public IEnumerator<T> GetEnumerator()
    {
        throw new NotSupportedException("Synchronous enumeration is not supported. Use async methods: await query.ToListAsync(), etc.");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var items = await _provider.ExecuteListAsync<T>(_expression, cancellationToken);
        foreach (var item in items)
        {
            yield return item;
        }
    }
}

public static class CypherQueryableExtensions
{
    public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
    {
        if (source.Provider is CypherQueryProvider provider)
        {
            return provider.ExecuteCountAsync(source.Expression, typeof(T), ct);
        }
        throw new InvalidOperationException("Source is not a CypherQueryable.");
    }

    public static Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
    {
        if (source.Provider is CypherQueryProvider provider)
        {
            return provider.ExecuteAnyAsync(source.Expression, typeof(T), ct);
        }
        throw new InvalidOperationException("Source is not a CypherQueryable.");
    }

    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
    {
        if (source.Provider is CypherQueryProvider provider)
        {
            return provider.ExecuteListAsync<T>(source.Expression, ct);
        }
        throw new InvalidOperationException("Source is not a CypherQueryable.");
    }

    public static async Task<T[]> ToArrayAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
    {
        var list = await source.ToListAsync(ct);
        return [.. list];
    }

    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> source, CancellationToken ct = default)
    {
        if (source.Provider is CypherQueryProvider provider)
        {
            return provider.ExecuteFirstOrDefaultAsync<T>(source.Expression, ct);
        }
        throw new InvalidOperationException("Source is not a CypherQueryable.");
    }

    public static CypherQuery ToCypherAst<T>(this IQueryable<T> source)
    {
        if (source is CypherQueryable<T> cq)
        {
            return cq.ToCypherQuery();
        }
        throw new InvalidOperationException("Source is not a CypherQueryable.");
    }

    public static IReadOnlyDictionary<string, object?> GetParameters<T>(this IQueryable<T> source)
    {
        if (source is CypherQueryable<T> cq)
        {
            return cq.GetQueryModel().Parameters;
        }
        var model = CypherExpressionVisitor.Translate(source.Expression, typeof(T));
        return model.Parameters;
    }
}

internal static class TypeHelper
{
    private static readonly ConcurrentDictionary<Type, Type> _elementTypes = new();

    public static Type GetElementType(Type type) =>
        _elementTypes.GetOrAdd(type, static t => FindIEnumerable(t) is { } ienum ? ienum.GetGenericArguments()[0] : t);

    private static Type? FindIEnumerable(Type? type)
    {
        if (type == null || type == typeof(string)) return null;
        if (type.IsArray && type.GetElementType() is { } elemType) return typeof(IEnumerable<>).MakeGenericType(elemType);

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                var ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                if (ienum.IsAssignableFrom(type)) return ienum;
            }
        }

        foreach (var iface in type.GetInterfaces())
        {
            var ienum = FindIEnumerable(iface);
            if (ienum != null) return ienum;
        }

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            return FindIEnumerable(type.BaseType);
        }

        return null;
    }
}
