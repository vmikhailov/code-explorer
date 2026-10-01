using System.Collections;
using System.Text.Json;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Linq.Entities;
using LinqExpression = System.Linq.Expressions.Expression;

namespace CodeExplorer.Cypher.Linq;

public class CypherQueryProvider : IQueryProvider
{
    private readonly ICypherQueryExecutor _executor;

    public CypherQueryProvider(ICypherQueryExecutor executor)
    {
        _executor = executor;
    }

    public ICypherQueryExecutor Executor => _executor;

    public IQueryable CreateQuery(LinqExpression expression)
    {
        var elementType = TypeHelper.GetElementType(expression.Type);
        try
        {
            return (IQueryable)Activator.CreateInstance(
                typeof(CypherQueryable<>).MakeGenericType(elementType),
                this, expression)!;
        }
        catch (System.Reflection.TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }
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

    public async Task<int> ExecuteCountAsync(LinqExpression expression, Type entityType, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, entityType);
        model.IsCountQuery = true;
        var ast = model.BuildAst();

        var json = await _executor.ExecuteQueryAsync(ast, model.Parameters, ct);
        if (string.IsNullOrWhiteSpace(json)) return 0;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("count", out var cProp))
            {
                if (cProp.TryGetInt32(out var count)) return count;
                if (cProp.TryGetInt64(out var count64)) return (int)count64;
            }
        }

        return 0;
    }

    public async Task<bool> ExecuteAnyAsync(LinqExpression expression, Type entityType, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, entityType);
        model.IsAnyQuery = true;
        var ast = model.BuildAst();

        var json = await _executor.ExecuteQueryAsync(ast, model.Parameters, ct);
        if (string.IsNullOrWhiteSpace(json)) return false;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("any", out var aProp))
            {
                if (aProp.ValueKind == JsonValueKind.True) return true;
                if (aProp.ValueKind == JsonValueKind.False) return false;
                if (aProp.TryGetInt32(out var anyInt)) return anyInt > 0;
            }
        }

        return false;
    }

    public async Task<List<TElement>> ExecuteListAsync<TElement>(LinqExpression expression, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, typeof(TElement));
        var ast = model.BuildAst();

        var json = await _executor.ExecuteQueryAsync(ast, model.Parameters, ct);
        if (string.IsNullOrWhiteSpace(json)) return [];

        var results = new List<TElement>();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var entity = DeserializeEntity<TElement>(elem);
                if (entity != null)
                {
                    results.Add(entity);
                }
            }
        }

        return results;
    }

    public async Task<TElement?> ExecuteFirstOrDefaultAsync<TElement>(LinqExpression expression, CancellationToken ct = default)
    {
        var model = CypherExpressionVisitor.Translate(expression, typeof(TElement));
        model.LimitCount = 1;
        var ast = model.BuildAst();

        var json = await _executor.ExecuteQueryAsync(ast, model.Parameters, ct);
        if (string.IsNullOrWhiteSpace(json)) return default;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined && first.ValueKind != JsonValueKind.Null)
            {
                return DeserializeEntity<TElement>(first);
            }
        }

        return default;
    }

    private static TElement? DeserializeEntity<TElement>(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Object) return default;

        // If the row wraps the entity under a column name (e.g. {"n": {"id": ...}}), unwrap it
        var targetElem = elem;
        if (!targetElem.TryGetProperty("id", out _))
        {
            foreach (var prop in elem.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("id", out _))
                {
                    targetElem = prop.Value;
                    break;
                }
            }
        }

        if (typeof(GraphEntity).IsAssignableFrom(typeof(TElement)))
        {
            var entity = (GraphEntity)Activator.CreateInstance(typeof(TElement))!;

            if (targetElem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                entity.Id = idProp.GetString() ?? "";
            }

            if (targetElem.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                entity.Name = nameProp.GetString() ?? "";
            }

            if (targetElem.TryGetProperty("display_name", out var dispProp) && dispProp.ValueKind == JsonValueKind.String)
            {
                entity.DisplayName = dispProp.GetString();
            }

            if (targetElem.TryGetProperty("file_path", out var fpProp) && fpProp.ValueKind == JsonValueKind.String)
            {
                entity.FilePath = fpProp.GetString();
            }

            if (targetElem.TryGetProperty("line_start", out var lsProp) && lsProp.TryGetInt32(out var ls))
            {
                entity.LineStart = ls;
            }

            if (targetElem.TryGetProperty("line_end", out var leProp) && leProp.TryGetInt32(out var le))
            {
                entity.LineEnd = le;
            }

            // Properties dictionary
            if (targetElem.TryGetProperty("properties", out var propsElem))
            {
                if (propsElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in propsElem.EnumerateObject())
                    {
                        entity.Properties[p.Name] = ConvertJsonElement(p.Value);
                    }
                }
                else if (propsElem.ValueKind == JsonValueKind.String)
                {
                    try
                    {
                        using var pDoc = JsonDocument.Parse(propsElem.GetString()!);
                        foreach (var p in pDoc.RootElement.EnumerateObject())
                        {
                            entity.Properties[p.Name] = ConvertJsonElement(p.Value);
                        }
                    }
                    catch { }
                }
            }

            // Fallback for name / display_name from properties if not on top-level
            if (string.IsNullOrEmpty(entity.Name) && entity.Properties.TryGetValue("name", out var pName) && pName is string sName)
            {
                entity.Name = sName;
            }
            if (string.IsNullOrEmpty(entity.DisplayName) && entity.Properties.TryGetValue("display_name", out var pDisp) && pDisp is string sDisp)
            {
                entity.DisplayName = sDisp;
            }

            // Copy top-level properties if present
            foreach (var prop in targetElem.EnumerateObject())
            {
                if (prop.Name is not ("id" or "name" or "display_name" or "file_path" or "line_start" or "line_end" or "properties"))
                {
                    if (!entity.Properties.ContainsKey(prop.Name))
                    {
                        entity.Properties[prop.Name] = ConvertJsonElement(prop.Value);
                    }
                }
            }

            return (TElement)(object)entity;
        }

        return JsonSerializer.Deserialize<TElement>(targetElem.GetRawText());
    }

    private static object? ConvertJsonElement(JsonElement elem) => elem.ValueKind switch
    {
        JsonValueKind.String => elem.GetString(),
        JsonValueKind.Number => elem.TryGetInt32(out var ni) ? (object)ni : elem.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => elem.ToString()
    };
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
    public static Type GetElementType(Type type)
    {
        var ienum = FindIEnumerable(type);
        if (ienum == null) return type;
        return ienum.GetGenericArguments()[0];
    }

    private static Type? FindIEnumerable(Type? type)
    {
        if (type == null || type == typeof(string)) return null;
        if (type.IsArray) return typeof(IEnumerable<>).MakeGenericType(type.GetElementType()!);

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                var ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                if (ienum.IsAssignableFrom(type)) return ienum;
            }
        }

        var ifaces = type.GetInterfaces();
        if (ifaces.Length > 0)
        {
            foreach (var iface in ifaces)
            {
                var ienum = FindIEnumerable(iface);
                if (ienum != null) return ienum;
            }
        }

        if (type.BaseType != null && type.BaseType != typeof(object))
        {
            return FindIEnumerable(type.BaseType);
        }

        return null;
    }
}
