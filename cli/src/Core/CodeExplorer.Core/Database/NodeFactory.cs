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

    private static readonly ConcurrentDictionary<Type, Action<IOntologyNode, Dictionary<string, object>>> Converters = new();
    private static readonly ConcurrentDictionary<Type, Action<IOntologyNode, Utf8JsonWriter>> JsonWriters = new();

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

        ApplyExtensions(node, properties);

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

            WriteJsonExtensions(ont, writer);
            writer.WriteEndObject();
            return;
        }

        var props = dbNode.Properties;
        props[IdPropName] = dbNode.Id;
        JsonSerializer.Serialize(writer, props);
    }

    private static void WriteJsonExtensions(IOntologyNode node, Utf8JsonWriter writer)
    {
        if (node.Extensions == null || node.Extensions.Count == 0) return;

        foreach (var kvp in node.Extensions)
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
        Dictionary<string, string>? ext = null;
        if (props != null)
        {
            foreach (var (k, v) in props)
            {
                if (k is OntologyConstants.PropertyNames.Id or OntologyConstants.PropertyNames.Name or OntologyConstants.PropertyNames.Path or OntologyConstants.PropertyNames.Kind)
                    continue;
                if (v != null)
                {
                    ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    ext[k] = v.ToString() ?? string.Empty;
                }
            }
        }
        return new WorkspaceNode(node.Id, name, path, ext);
    }
}

