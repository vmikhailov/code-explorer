using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Database;

public static class NodeFactory
{
    private static readonly ConcurrentDictionary<Type, Action<IOntologyNode, Dictionary<string, object>>> Converters = new();

    public static Node Create(IOntologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var converter = Converters.GetOrAdd(node.GetType(), CreateConverter);
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        converter(node, properties);

        ApplyExtensions(node, properties);

        return new Node(node.Id, node.Kind, properties);
    }

    public static void ApplyExtensions(IOntologyNode node, Dictionary<string, object> properties)
    {
        if (node.Extensions == null || node.Extensions.Count == 0) return;

        foreach (var kvp in node.Extensions)
        {
            if (int.TryParse(kvp.Value, out var intVal) &&
                (kvp.Key.EndsWith("_line") || kvp.Key.EndsWith("_col") || kvp.Key is "line" or "column" or "arg_index" or "start_line" or "end_line" or "stage_order" or "loc" or "line_count" or "statement_count"))
            {
                properties[kvp.Key] = intVal;
            }
            else if (long.TryParse(kvp.Value, out var longVal) && kvp.Key is "size_bytes")
            {
                properties[kvp.Key] = longVal;
            }
            else if (bool.TryParse(kvp.Value, out var boolVal) && kvp.Key is "is_test" or "is_library" or "is_empty" or "has_comment" or "is_decorator" or "is_internal" or "is_unsafe" or "is_async")
            {
                properties[kvp.Key] = boolVal;
            }
            else
            {
                properties[kvp.Key] = kvp.Value;
            }
        }
    }

    private static Action<IOntologyNode, Dictionary<string, object>> CreateConverter(Type type)
    {
        var nodeParam = Expression.Parameter(typeof(IOntologyNode), "node");
        var dictParam = Expression.Parameter(typeof(Dictionary<string, object>), "dict");

        var typedVar = Expression.Variable(type, "typedNode");
        var assignTyped = Expression.Assign(typedVar, Expression.Convert(nodeParam, type));

        var dictIndexer = typeof(Dictionary<string, object>).GetProperty("Item")!;
        var statements = new List<Expression> { assignTyped };

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        foreach (var prop in props)
        {
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>(inherit: false) != null)
                continue;

            string key;
            var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (jsonPropAttr != null && !string.IsNullOrEmpty(jsonPropAttr.Name))
            {
                key = jsonPropAttr.Name;
            }
            else
            {
                key = JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name);
            }

            var keyConst = Expression.Constant(key);
            var propAccess = Expression.Property(typedVar, prop);
            var propType = prop.PropertyType;

            if (propType == typeof(string) || (!propType.IsValueType && Nullable.GetUnderlyingType(propType) == null))
            {
                var notNull = Expression.NotEqual(propAccess, Expression.Constant(null, propType));
                var assign = Expression.Assign(
                    Expression.Property(dictParam, dictIndexer, keyConst),
                    Expression.Convert(propAccess, typeof(object))
                );
                statements.Add(Expression.IfThen(notNull, assign));
            }
            else if (Nullable.GetUnderlyingType(propType) is { } underlyingType)
            {
                var hasValue = Expression.Property(propAccess, "HasValue");
                var valueAccess = Expression.Property(propAccess, "Value");

                Expression valueExpr;
                if (underlyingType.IsEnum)
                {
                    var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes)!;
                    valueExpr = Expression.Call(Expression.Convert(valueAccess, typeof(object)), toStringMethod);
                }
                else
                {
                    valueExpr = Expression.Convert(valueAccess, typeof(object));
                }

                var assign = Expression.Assign(
                    Expression.Property(dictParam, dictIndexer, keyConst),
                    valueExpr
                );
                statements.Add(Expression.IfThen(hasValue, assign));
            }
            else if (propType.IsEnum)
            {
                var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes)!;
                var valueExpr = Expression.Call(Expression.Convert(propAccess, typeof(object)), toStringMethod);
                var assign = Expression.Assign(
                    Expression.Property(dictParam, dictIndexer, keyConst),
                    valueExpr
                );
                statements.Add(assign);
            }
            else
            {
                var assign = Expression.Assign(
                    Expression.Property(dictParam, dictIndexer, keyConst),
                    Expression.Convert(propAccess, typeof(object))
                );
                statements.Add(assign);
            }
        }

        var body = Expression.Block(new[] { typedVar }, statements);
        return Expression.Lambda<Action<IOntologyNode, Dictionary<string, object>>>(body, nodeParam, dictParam).Compile();
    }
}

