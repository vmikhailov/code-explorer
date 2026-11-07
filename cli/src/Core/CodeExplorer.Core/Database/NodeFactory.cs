using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public static class NodeFactory
{
    private const string IdPropName = "id";
    private const string NodeParamName = "node";
    private const string WriterParamName = "writer";
    private const string DictParamName = "dict";
    private const string TypedVarName = "typedNode";
    private const string HasValuePropName = "HasValue";
    private const string ValuePropName = "Value";
    private const string ItemPropName = "Item";
    private const string LineSuffix = "_line";
    private const string ColSuffix = "_col";
    private const string PropIndexedAt = "indexed_at";
    private const string PropActivationEvents = "activation_events";
    private const string BoolTrueStr = "true";

    private static readonly ConcurrentDictionary<Type, Action<IOntologyNode, Dictionary<string, object>>> Converters = new();
    private static readonly ConcurrentDictionary<Type, Action<IOntologyNode, Utf8JsonWriter>> JsonWriters = new();
    private static readonly ConcurrentDictionary<Type, string[]> SchemaPropertyKeys = new();
    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> DefaultPropertiesByKind = new(BuildDefaultPropertiesByKind);

    public static string[] GetSchemaPropertyKeys(Type type) =>
        SchemaPropertyKeys.GetOrAdd(type, ExtractSchemaPropertyKeys);

    public static IReadOnlyDictionary<string, string[]> GetDefaultPropertiesByKind() =>
        DefaultPropertiesByKind.Value;

    private static string[] ExtractSchemaPropertyKeys(Type t)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal) { IdPropName };
        var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        foreach (var prop in props)
        {
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>(inherit: false) != null)
                continue;

            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!IsSupportedJsonPropertyType(propType))
                continue;

            var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var key = jsonPropAttr != null && !string.IsNullOrEmpty(jsonPropAttr.Name)
                ? jsonPropAttr.Name
                : JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name);

            keys.Add(key);
        }

        return [.. keys.OrderBy(k => k, StringComparer.Ordinal)];
    }

    private static bool IsSupportedJsonPropertyType(Type propType) =>
        propType == typeof(string) ||
        propType == typeof(int) ||
        propType == typeof(long) ||
        propType == typeof(double) ||
        propType == typeof(bool) ||
        propType.IsEnum;

    private static IReadOnlyDictionary<string, string[]> BuildDefaultPropertiesByKind()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var nodeTypes = typeof(IOntologyNode).Assembly.GetTypes()
            .Where(t => typeof(IOntologyNode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in nodeTypes)
        {
            if (type.GetCustomAttribute<OntologyNodeAttribute>() == null) continue;
            var instance = (IOntologyNode)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            var kind = instance.Kind.ToCypherLabel();
            if (!map.TryGetValue(kind, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                map[kind] = set;
            }

            foreach (var key in GetSchemaPropertyKeys(type))
            {
                set.Add(key);
            }
        }

        return map.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static Node Create(IOntologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new Node(node);
    }

    public static Dictionary<string, object> MaterializeProperties(IOntologyNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var converter = Converters.GetOrAdd(node.GetType(), CreateConverter);
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        converter(node, properties);

        ApplyCustomProperties(node, properties);

        return properties;
    }

    public static void WriteJsonProperties(Node dbNode, Utf8JsonWriter writer)
    {
        if (dbNode.OntologyNode != null && !dbNode.HasMaterializedProperties)
        {
            var ont = dbNode.OntologyNode;
            writer.WriteStartObject();
            writer.WriteString(IdPropName, ont.Id);

            var jsonWriter = JsonWriters.GetOrAdd(ont.GetType(), CreateJsonWriter);
            jsonWriter(ont, writer);

            WriteJsonCustomProperties(ont, writer);
            writer.WriteEndObject();
            return;
        }

        var props = dbNode.Properties;
        props[IdPropName] = dbNode.Id;
        JsonSerializer.Serialize(writer, props);
    }

    private static void WriteJsonCustomProperties(IOntologyNode node, Utf8JsonWriter writer)
    {
        if (node.CustomProperties == null || node.CustomProperties.Count == 0) return;

        foreach (var kvp in node.CustomProperties)
        {
            if (int.TryParse(kvp.Value, out var intVal) &&
                (kvp.Key.EndsWith(LineSuffix, StringComparison.Ordinal) || kvp.Key.EndsWith(ColSuffix, StringComparison.Ordinal) || kvp.Key is "line" or "column" or "arg_index" or "start_line" or "end_line" or "stage_order" or "loc" or "line_count" or "statement_count"))
            {
                writer.WriteNumber(kvp.Key, intVal);
            }
            else if (long.TryParse(kvp.Value, out var longVal) && kvp.Key is "size_bytes")
            {
                writer.WriteNumber(kvp.Key, longVal);
            }
            else if (bool.TryParse(kvp.Value, out var boolVal) && kvp.Key is "is_test" or "is_library" or "is_empty" or "has_comment" or "is_decorator" or "is_internal" or "is_unsafe" or "is_async")
            {
                writer.WriteBoolean(kvp.Key, boolVal);
            }
            else
            {
                writer.WriteString(kvp.Key, kvp.Value);
            }
        }
    }

    private static Action<IOntologyNode, Utf8JsonWriter> CreateJsonWriter(Type type)
    {
        var nodeParam = Expression.Parameter(typeof(IOntologyNode), NodeParamName);
        var writerParam = Expression.Parameter(typeof(Utf8JsonWriter), WriterParamName);

        var typedVar = Expression.Variable(type, TypedVarName);
        var assignTyped = Expression.Assign(typedVar, Expression.Convert(nodeParam, type));
        var statements = new List<Expression> { assignTyped };

        var writeStringMethod = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteString), [typeof(string), typeof(string)])!;
        var writeIntMethod = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumber), [typeof(string), typeof(int)])!;
        var writeLongMethod = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumber), [typeof(string), typeof(long)])!;
        var writeDoubleMethod = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteNumber), [typeof(string), typeof(double)])!;
        var writeBoolMethod = typeof(Utf8JsonWriter).GetMethod(nameof(Utf8JsonWriter.WriteBoolean), [typeof(string), typeof(bool)])!;

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

            if (key == IdPropName) continue;

            var keyConst = Expression.Constant(key);
            var propAccess = Expression.Property(typedVar, prop);
            var propType = prop.PropertyType;

            if (propType == typeof(string))
            {
                var notNull = Expression.NotEqual(propAccess, Expression.Constant(null, typeof(string)));
                var callWrite = Expression.Call(writerParam, writeStringMethod, keyConst, propAccess);
                statements.Add(Expression.IfThen(notNull, callWrite));
            }
            else if (propType == typeof(int))
            {
                statements.Add(Expression.Call(writerParam, writeIntMethod, keyConst, propAccess));
            }
            else if (propType == typeof(long))
            {
                statements.Add(Expression.Call(writerParam, writeLongMethod, keyConst, propAccess));
            }
            else if (propType == typeof(double))
            {
                statements.Add(Expression.Call(writerParam, writeDoubleMethod, keyConst, propAccess));
            }
            else if (propType == typeof(bool))
            {
                statements.Add(Expression.Call(writerParam, writeBoolMethod, keyConst, propAccess));
            }
            else if (propType.IsEnum)
            {
                var formatMethod = typeof(TaxonomyViewEnumExtensions)
                    .GetMethod(nameof(TaxonomyViewEnumExtensions.FormatEnumValue))!
                    .MakeGenericMethod(propType);
                var formattedStr = Expression.Call(formatMethod, propAccess);
                statements.Add(Expression.Call(writerParam, writeStringMethod, keyConst, formattedStr));
            }
            else if (Nullable.GetUnderlyingType(propType) is { } underlyingType)
            {
                var hasValue = Expression.Property(propAccess, HasValuePropName);
                var valueAccess = Expression.Property(propAccess, ValuePropName);

                Expression? writeCall = null;
                if (underlyingType == typeof(int))
                    writeCall = Expression.Call(writerParam, writeIntMethod, keyConst, valueAccess);
                else if (underlyingType == typeof(long))
                    writeCall = Expression.Call(writerParam, writeLongMethod, keyConst, valueAccess);
                else if (underlyingType == typeof(double))
                    writeCall = Expression.Call(writerParam, writeDoubleMethod, keyConst, valueAccess);
                else if (underlyingType == typeof(bool))
                    writeCall = Expression.Call(writerParam, writeBoolMethod, keyConst, valueAccess);
                else if (underlyingType.IsEnum)
                {
                    var formatMethod = typeof(TaxonomyViewEnumExtensions)
                        .GetMethod(nameof(TaxonomyViewEnumExtensions.FormatEnumValue))!
                        .MakeGenericMethod(underlyingType);
                    var formattedStr = Expression.Call(formatMethod, valueAccess);
                    writeCall = Expression.Call(writerParam, writeStringMethod, keyConst, formattedStr);
                }

                if (writeCall != null)
                {
                    statements.Add(Expression.IfThen(hasValue, writeCall));
                }
            }
        }

        var body = Expression.Block([typedVar], statements);
        return Expression.Lambda<Action<IOntologyNode, Utf8JsonWriter>>(body, nodeParam, writerParam).Compile();
    }

    public static void ApplyCustomProperties(IOntologyNode node, Dictionary<string, object> properties)
    {
        if (node.CustomProperties == null || node.CustomProperties.Count == 0) return;

        foreach (var kvp in node.CustomProperties)
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
                    var formatMethod = typeof(TaxonomyViewEnumExtensions)
                        .GetMethod(nameof(TaxonomyViewEnumExtensions.FormatEnumValue))!
                        .MakeGenericMethod(underlyingType);
                    valueExpr = Expression.Convert(Expression.Call(formatMethod, valueAccess), typeof(object));
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
                var formatMethod = typeof(TaxonomyViewEnumExtensions)
                    .GetMethod(nameof(TaxonomyViewEnumExtensions.FormatEnumValue))!
                    .MakeGenericMethod(propType);
                var valueExpr = Expression.Convert(Expression.Call(formatMethod, propAccess), typeof(object));
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

    public static WorkspaceNode ToWorkspaceNode(Node node)
    {
        var props = node.Properties;
        var name = props != null && props.TryGetValue(OntologyConstants.PropertyNames.Name, out var n) ? n?.ToString() ?? string.Empty : string.Empty;
        var path = props != null && props.TryGetValue(OntologyConstants.PropertyNames.Path, out var p) ? p?.ToString() ?? string.Empty : string.Empty;
        var wsNode = new WorkspaceNode(node.Id, name, path);
        if (props != null)
        {
            if (props.TryGetValue(PropIndexedAt, out var idxAt) && idxAt != null)
                wsNode.IndexedAt = idxAt.ToString();
            if (props.TryGetValue(OntologyConstants.ExtensionKeys.PackageName, out var pkgName) && pkgName != null)
                wsNode.PackageName = pkgName.ToString();
            if (props.TryGetValue(OntologyConstants.ExtensionKeys.PackageVersion, out var pkgVer) && pkgVer != null)
                wsNode.PackageVersion = pkgVer.ToString();
            if (props.TryGetValue(OntologyConstants.ExtensionKeys.Publisher, out var pub) && pub != null)
                wsNode.Publisher = pub.ToString();
            if (props.TryGetValue(OntologyConstants.ExtensionKeys.HasVsCodeEngine, out var vsc) && vsc != null)
                wsNode.HasVsCodeEngine = string.Equals(vsc.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase);
            if (props.TryGetValue(OntologyConstants.ExtensionKeys.HasContributes, out var contrib) && contrib != null)
                wsNode.HasContributes = string.Equals(contrib.ToString(), BoolTrueStr, StringComparison.OrdinalIgnoreCase);
            if (props.TryGetValue(PropActivationEvents, out var act) && act != null)
                wsNode.ActivationEvents = act.ToString();
        }
        return wsNode;
    }
}

