using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public static class DbRowMaterializer
{
    private const string PropId = "id";
    private const string PropKind = "kind";
    private const string PropType = "type";
    private const string PropFrom = "from";
    private const string PropFromId = "from_id";
    private const string PropTo = "to";
    private const string PropToId = "to_id";
    private const string PropProperties = "properties";

    private static readonly ConcurrentDictionary<Type, CachedTypeDescriptor> TypeCache = new();

    public static async Task<CypherQueryResult> ReadDynamicResultAsync(DbDataReader reader, CancellationToken ct = default)
    {
        var fieldCount = reader.FieldCount;
        var columns = new string[fieldCount];
        for (var i = 0; i < fieldCount; i++)
        {
            columns[i] = reader.GetName(i);
        }

        var rows = new List<CypherRow>();
        while (await reader.ReadAsync(ct))
        {
            var values = new CypherValue[fieldCount];
            for (var i = 0; i < fieldCount; i++)
            {
                values[i] = reader.IsDBNull(i)
                    ? CypherValue.Null
                    : CypherValue.FromSqliteObject(reader.GetValue(i));
            }
            rows.Add(new CypherRow(columns, values));
        }

        return new CypherQueryResult(columns, rows);
    }

    public static async Task<List<T>> ReadListAsync<T>(DbDataReader reader, CancellationToken ct = default)
    {
        var dynamicResult = await ReadDynamicResultAsync(reader, ct);
        return MaterializeList<T>(dynamicResult);
    }

    public static async Task<T?> ReadFirstOrDefaultAsync<T>(DbDataReader reader, CancellationToken ct = default)
    {
        var fieldCount = reader.FieldCount;
        var columns = new string[fieldCount];
        for (var i = 0; i < fieldCount; i++)
        {
            columns[i] = reader.GetName(i);
        }

        if (!await reader.ReadAsync(ct))
        {
            return default;
        }

        var values = new CypherValue[fieldCount];
        for (var i = 0; i < fieldCount; i++)
        {
            values[i] = reader.IsDBNull(i)
                ? CypherValue.Null
                : CypherValue.FromSqliteObject(reader.GetValue(i));
        }

        var row = new CypherRow(columns, values);
        return MaterializeRow<T>(row);
    }

    public static async Task<T?> ReadScalarAsync<T>(DbDataReader reader, CancellationToken ct = default)
    {
        if (!await reader.ReadAsync(ct) || reader.FieldCount == 0 || reader.IsDBNull(0))
        {
            return default;
        }

        var val = CypherValue.FromSqliteObject(reader.GetValue(0));
        return (T?)ConvertCypherValue(val, typeof(T));
    }

    public static List<T> MaterializeList<T>(CypherQueryResult result)
    {
        var list = new List<T>(result.Rows.Count);
        for (var i = 0; i < result.Rows.Count; i++)
        {
            list.Add(MaterializeRow<T>(result.Rows[i]));
        }
        return list;
    }

    public static T MaterializeRow<T>(CypherRow row)
    {
        var targetType = typeof(T);
        if (targetType == typeof(CypherRow))
        {
            return (T)(object)row;
        }

        if (targetType == typeof(CypherValue))
        {
            var val = row.FieldCount > 0 ? row[0] : CypherValue.Null;
            return (T)(object)val;
        }

        if (targetType == typeof(Node))
        {
            return (T)(object)MaterializeNode(row);
        }

        if (targetType == typeof(Relationship))
        {
            return (T)(object)MaterializeRelationship(row);
        }

        if (IsScalarOrEnumType(targetType))
        {
            var firstVal = row.FieldCount > 0 ? row[0] : CypherValue.Null;
            return (T)ConvertCypherValue(firstVal, targetType)!;
        }

        if (typeof(ITuple).IsAssignableFrom(targetType) && targetType.IsValueType)
        {
            return (T)MaterializeValueTuple(row, targetType);
        }

        return (T)MaterializeComplexObject(row, targetType);
    }

    public static Node MaterializeNode(CypherRow row)
    {
        if (row.FieldCount == 1 && row[0].Kind == CypherValueKind.Object)
        {
            return MaterializeNodeFromValue(row[0]);
        }

        var id = row.GetStringProp(PropId);
        var kind = row.GetStringProp(PropKind);
        var props = row.TryGetProperty(PropProperties, out var propVal)
            ? ConvertObjectToLegacyDictionary(propVal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        return new Node(id, kind, props);
    }

    public static Node MaterializeNodeFromValue(CypherValue val)
    {
        var id = val.GetStringProp(PropId);
        var kind = val.GetStringProp(PropKind);
        var props = val.TryGetProperty(PropProperties, out var propVal)
            ? ConvertObjectToLegacyDictionary(propVal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        return new Node(id, kind, props);
    }

    public static Relationship MaterializeRelationship(CypherRow row)
    {
        if (row.FieldCount == 1 && row[0].Kind == CypherValueKind.Object)
        {
            return MaterializeRelationshipFromValue(row[0]);
        }

        var from = row.GetStringProp(PropFromId, row.GetStringProp(PropFrom));
        var to = row.GetStringProp(PropToId, row.GetStringProp(PropTo));
        var kind = row.GetStringProp(PropKind, row.GetStringProp(PropType));
        var props = row.TryGetProperty(PropProperties, out var propVal)
            ? ConvertObjectToLegacyDictionary(propVal)
            : null;

        return new Relationship(from, to, kind, props);
    }

    public static Relationship MaterializeRelationshipFromValue(CypherValue val)
    {
        var from = val.GetStringProp(PropFrom, val.GetStringProp(PropFromId));
        var to = val.GetStringProp(PropTo, val.GetStringProp(PropToId));
        var kind = val.GetStringProp(PropType, val.GetStringProp(PropKind));
        var props = val.TryGetProperty(PropProperties, out var propVal)
            ? ConvertObjectToLegacyDictionary(propVal)
            : null;

        return new Relationship(from, to, kind, props);
    }

    private static Dictionary<string, object> ConvertObjectToLegacyDictionary(CypherValue val)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        if (val.Kind == CypherValueKind.String)
        {
            val = CypherValue.FromPotentialJsonString(val.AsNullableString());
        }

        if (val.Kind != CypherValueKind.Object) return dict;

        var obj = val.AsObject();
        for (var i = 0; i < obj.Count; i++)
        {
            var k = obj[i].Key;
            var v = ConvertCypherValueToClrPrimitive(obj[i].Value);
            if (v is not null)
            {
                dict[k] = v;
            }
        }
        return dict;
    }

    private static object? ConvertCypherValueToClrPrimitive(CypherValue val)
    {
        return val.Kind switch
        {
            CypherValueKind.Null => null,
            CypherValueKind.Boolean => val.AsBool(),
            CypherValueKind.Int64 => val.AsInt64(),
            CypherValueKind.Float64 => val.AsDouble(),
            CypherValueKind.String => val.AsNullableString(),
            CypherValueKind.Array => val.AsStringList().ToList(),
            CypherValueKind.Object => ConvertObjectToLegacyDictionary(val),
            _ => val.AsNullableString()
        };
    }

    private static object MaterializeValueTuple(CypherRow row, Type tupleType)
    {
        var genericArgs = tupleType.GetGenericArguments();
        var ctorArgs = new object?[genericArgs.Length];
        for (var i = 0; i < genericArgs.Length; i++)
        {
            var cell = i < row.FieldCount ? row[i] : CypherValue.Null;
            ctorArgs[i] = ConvertCypherValue(cell, genericArgs[i]);
        }
        return Activator.CreateInstance(tupleType, ctorArgs)!;
    }

    private static object MaterializeComplexObject(CypherRow row, Type targetType)
    {
        var descriptor = TypeCache.GetOrAdd(targetType, static t => new CachedTypeDescriptor(t));
        var ctorParams = descriptor.CtorParameters;
        var args = new object?[ctorParams.Length];

        for (var i = 0; i < ctorParams.Length; i++)
        {
            var param = ctorParams[i];
            if (TryFindColumnValue(row, param.Name, i, ctorParams.Length, out var colVal))
            {
                args[i] = ConvertCypherValue(colVal, param.ParameterType, param.HasDefaultValue ? param.DefaultValue : null);
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else
            {
                args[i] = GetDefaultForType(param.ParameterType);
            }
        }

        var instance = descriptor.Constructor.Invoke(args);

        if (descriptor.WritableProperties.Length > 0 && ctorParams.Length == 0)
        {
            foreach (var prop in descriptor.WritableProperties)
            {
                if (TryFindColumnValue(row, prop.Name, -1, -1, out var colVal) && !colVal.IsNull)
                {
                    prop.SetValue(instance, ConvertCypherValue(colVal, prop.PropertyType));
                }
            }
        }

        return instance;
    }

    private static bool TryFindColumnValue(CypherRow row, string? memberName, int paramIndex, int totalParams, out CypherValue value)
    {
        if (!string.IsNullOrEmpty(memberName))
        {
            if (row.TryGetProperty(memberName, out value))
            {
                return true;
            }

            var normTarget = NormalizeIdentifier(memberName);
            for (var i = 0; i < row.Columns.Count; i++)
            {
                if (string.Equals(NormalizeIdentifier(row.Columns[i]), normTarget, StringComparison.OrdinalIgnoreCase))
                {
                    value = row[i];
                    return true;
                }
            }
        }

        if (paramIndex >= 0 && totalParams == row.FieldCount && paramIndex < row.FieldCount)
        {
            value = row[paramIndex];
            return true;
        }

        value = CypherValue.Null;
        return false;
    }

    private static string NormalizeIdentifier(string name) =>
        name.Contains('_') ? name.Replace("_", string.Empty, StringComparison.Ordinal) : name;

    public static object? ConvertCypherValue(CypherValue val, Type targetType, object? fallbackDefault = null)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        var effectiveType = underlying ?? targetType;

        if (val.IsNull)
        {
            if (fallbackDefault is not null) return fallbackDefault;
            if (underlying is not null) return null;
            return GetDefaultForType(targetType);
        }

        if (effectiveType == typeof(CypherValue)) return val;
        if (effectiveType == typeof(string)) return val.AsString();
        if (effectiveType == typeof(int)) return val.AsInt32();
        if (effectiveType == typeof(long)) return val.AsInt64();
        if (effectiveType == typeof(double)) return val.AsDouble();
        if (effectiveType == typeof(float)) return (float)val.AsDouble();
        if (effectiveType == typeof(bool)) return val.AsBool();
        if (effectiveType == typeof(Node)) return MaterializeNodeFromValue(val);
        if (effectiveType == typeof(Relationship)) return MaterializeRelationshipFromValue(val);

        if (effectiveType.IsEnum)
        {
            return ParseEnumType(effectiveType, val.AsString());
        }

        if (effectiveType == typeof(List<string>) || effectiveType == typeof(IReadOnlyList<string>) || effectiveType == typeof(IEnumerable<string>))
        {
            return val.AsStringList().ToList();
        }

        if (effectiveType == typeof(string[]))
        {
            return val.AsStringList().ToArray();
        }

        if (effectiveType == typeof(Dictionary<string, string>) || effectiveType == typeof(IReadOnlyDictionary<string, string>))
        {
            return val.ExtractStringMap();
        }

        return Convert.ChangeType(val.AsString(), effectiveType, CultureInfo.InvariantCulture);
    }

    private static object ParseEnumType(Type enumType, string raw)
    {
        if (enumType == typeof(NodeKind)) return EnumExtensions.ParseNodeKind(raw);
        if (enumType == typeof(RelationshipKind)) return EnumExtensions.ParseRelationshipKind(raw);
        if (enumType == typeof(GraphEdgeCategory)) return TaxonomyViewEnumExtensions.ParseGraphEdgeCategory(raw);
        if (enumType == typeof(EvidenceKind)) return TaxonomyViewEnumExtensions.ParseEvidenceKind(raw);
        if (enumType == typeof(TypeSubKind)) return TaxonomyEnumExtensions.ParseTypeSubKind(raw);
        if (enumType == typeof(FunctionSubKind)) return TaxonomyEnumExtensions.ParseFunctionSubKind(raw);
        if (enumType == typeof(OperationType)) return TaxonomyEnumExtendedExtensions.ParseOperationType(raw);
        if (enumType == typeof(DatabaseType)) return EnumExtensions.ParseDatabaseType(raw);
        if (enumType == typeof(BrokerType)) return TaxonomyEnumExtensions.ParseBrokerType(raw);

        if (Enum.TryParse(enumType, raw, ignoreCase: true, out var parsed) && parsed is not null)
        {
            return parsed;
        }

        return Activator.CreateInstance(enumType)!;
    }

    private static bool IsScalarOrEnumType(Type t)
    {
        var effective = Nullable.GetUnderlyingType(t) ?? t;
        return effective.IsPrimitive || effective.IsEnum || effective == typeof(string) || effective == typeof(decimal);
    }

    private static object? GetDefaultForType(Type t)
    {
        if (t == typeof(string)) return string.Empty;
        if (t == typeof(List<string>) || t == typeof(IReadOnlyList<string>)) return new List<string>();
        if (t == typeof(string[])) return Array.Empty<string>();
        return t.IsValueType ? Activator.CreateInstance(t) : null;
    }

    private sealed class CachedTypeDescriptor
    {
        public ConstructorInfo Constructor { get; }
        public ParameterInfo[] CtorParameters { get; }
        public PropertyInfo[] WritableProperties { get; }

        public CachedTypeDescriptor(Type type)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Constructor = ctors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault()
                ?? throw new InvalidOperationException($"Type '{type.FullName}' has no public constructors.");
            CtorParameters = Constructor.GetParameters();
            WritableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToArray();
        }
    }
}
