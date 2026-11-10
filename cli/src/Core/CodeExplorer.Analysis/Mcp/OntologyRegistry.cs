using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Mcp;

public record NodeMetadataInfo(NodeKind Kind, Type NodeType, OntologyNodeAttribute Attribute)
{
    public NodeMetadataInfo(string Kind, Type NodeType, OntologyNodeAttribute Attribute)
        : this(EnumExtensions.ParseNodeKind(Kind), NodeType, Attribute)
    {
    }
}

[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Node types in CodeExplorer.Core.Common.Nodes are preserved via TrimmerRoots")]
[UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "Node types in CodeExplorer.Core.Common.Nodes are preserved via TrimmerRoots")]
public static class OntologyRegistry
{
    public static readonly IReadOnlyList<NodeMetadataInfo> AllNodes =
    [
        .. typeof(IOntologyNode).Assembly.GetTypes()
            .Where(t => typeof(IOntologyNode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract).Select(t =>
            {
                var attr = t.GetCustomAttribute<OntologyNodeAttribute>();
                if (attr == null) return null;

                var instance = (IOntologyNode)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);
                return new NodeMetadataInfo(instance.Kind, t, attr);
            }).OfType<NodeMetadataInfo>()
    ];

    private const string LayerKeyPhysical = "L1_physical";
    private const string LayerKeyBoundaries = "L2_boundaries";
    private const string LayerKeySyntactic = "L3_syntactic";
    private const string LayerKeySemantic = "L4_semantic";
    private const string LayerKeyLogical = "L5_logical";

    public static readonly HashSet<string> SystemKinds = AllNodes
        .Where(x => x.Attribute.IsSystemNode)
        .Select(x => x.Kind.ToCypherLabel())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSystemNode(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && SystemKinds.Contains(kind);

    public static readonly IReadOnlyDictionary<string, (int Order, string LayerKey)> TaxonomyLayerByKind =
        AllNodes.ToDictionary(
            x => x.Kind.ToCypherLabel(),
            x => ResolveTaxonomyLayer(x.Attribute.Layer),
            StringComparer.OrdinalIgnoreCase);

    public static (int Order, string LayerKey) GetTaxonomyLayer(string? kind)
    {
        if (!string.IsNullOrWhiteSpace(kind) && TaxonomyLayerByKind.TryGetValue(kind, out var layer))
        {
            return layer;
        }
        return (4, LayerKeySemantic);
    }

    private static (int Order, string LayerKey) ResolveTaxonomyLayer(string? layerName) => layerName switch
    {
        OntologyConstants.Layers.Workspace or OntologyConstants.Layers.Physical => (1, LayerKeyPhysical),
        OntologyConstants.Layers.ProjectBoundary => (2, LayerKeyBoundaries),
        OntologyConstants.Layers.Syntactic => (3, LayerKeySyntactic),
        OntologyConstants.Layers.Semantic => (4, LayerKeySemantic),
        OntologyConstants.Layers.Logical => (5, LayerKeyLogical),
        _ => (4, LayerKeySemantic)
    };

    public static readonly IReadOnlySet<string> SystemPropertyKeys = BuildSystemPropertyKeys();

    public static bool IsSystemProperty(string? propKey) =>
        !string.IsNullOrWhiteSpace(propKey) && SystemPropertyKeys.Contains(propKey);

    public static readonly IReadOnlyList<string> CommonPropertyKeys = BuildCommonPropertyKeys();

    public static readonly IReadOnlySet<string> CommonPropertySet =
        new HashSet<string>(CommonPropertyKeys, StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> BuildSystemPropertyKeys()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = AllNodes.Select(x => x.NodeType).Append(typeof(CompositeNode));
        foreach (var type in types)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var ontProp = prop.GetCustomAttribute<OntologyPropertyAttribute>(inherit: true);
                if (ontProp is not { IsSystem: true }) continue;

                var jsonProp = prop.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true);
                if (jsonProp != null && !string.IsNullOrEmpty(jsonProp.Name))
                {
                    set.Add(jsonProp.Name);
                }
                set.Add(ToSnakeCase(prop.Name));
                if (prop.Name.Length > 0)
                {
                    set.Add(char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..]);
                }
            }
        }
        return set;
    }

    private static string[] BuildCommonPropertyKeys()
    {
        var common = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in typeof(CompositeNode).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var ontProp = prop.GetCustomAttribute<OntologyPropertyAttribute>(inherit: true);
            if (ontProp is not { IsSystem: false }) continue;

            var jsonProp = prop.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true);
            common.Add(jsonProp != null && !string.IsNullOrEmpty(jsonProp.Name) ? jsonProp.Name : ToSnakeCase(prop.Name));
        }

        HashSet<string>? intersection = null;
        foreach (var node in AllNodes.Where(n => !n.Attribute.IsSystemNode))
        {
            var keys = CodeExplorer.Core.Database.NodeFactory.GetSchemaPropertyKeys(node.NodeType);
            if (intersection == null)
            {
                intersection = new HashSet<string>(keys, StringComparer.Ordinal);
            }
            else
            {
                intersection.IntersectWith(keys);
            }
        }
        if (intersection != null)
        {
            common.UnionWith(intersection);
        }
        return [.. common.OrderBy(k => k, StringComparer.Ordinal)];
    }

    public static readonly Dictionary<string, (string CapitalizedKind, Type NodeType)> KindMapping = 
        AllNodes.ToDictionary(
            x => x.Kind.ToCypherLabel(), 
            x => (x.Kind.ToCypherLabel(), x.NodeType), 
            StringComparer.OrdinalIgnoreCase
        );

    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (input == "Id") return "id";
        
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static string GetNodeDefinition(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return "Invalid node kind.";
        var kindLower = kind.Trim().ToLowerInvariant();

        if (!KindMapping.TryGetValue(kindLower, out var map))
        {
            var activeKinds = string.Join(", ", KindMapping.Values
                .Select(v => $"'{v.CapitalizedKind}'")
                .OrderBy(k => k));
            return $"Unknown node kind: '{kind}'. Active ontological kinds in CodeExplorer are: {activeKinds}.";
        }

        var (capitalizedKind, nodeType) = map;
        
        var purpose = "Node of kind " + capitalizedKind + ".";
        var nodeAttr = nodeType.GetCustomAttribute<OntologyNodeAttribute>();
        if (nodeAttr != null)
        {
            purpose = nodeAttr.Purpose;
        }

        var outboundList = new List<string>();
        foreach (var attr in nodeType.GetCustomAttributes(true))
        {
            if (TryGetOntologyEdge(attr, out var relVal, out var toVal))
            {
                var targetNodeAttr = toVal.GetCustomAttribute<OntologyNodeAttribute>();
                var targetLabel = targetNodeAttr != null ? targetNodeAttr.Label : toVal.Name.Replace("Node", "");
                outboundList.Add($"  - {relVal} -> {targetLabel}");
            }
        }

        var inboundList = new List<string>();
        foreach (var mapEntry in KindMapping.Values)
        {
            var otherType = mapEntry.NodeType;
            if (otherType == nodeType) continue;

            foreach (var attr in otherType.GetCustomAttributes(true))
            {
                if (TryGetOntologyEdge(attr, out var relVal, out var toVal))
                {
                    if (toVal != nodeType)
                    {
                        continue;
                    }

                    var otherNodeAttr = otherType.GetCustomAttribute<OntologyNodeAttribute>();
                    var otherLabel = otherNodeAttr != null ? otherNodeAttr.Label : otherType.Name.Replace("Node", "");
                    inboundList.Add($"  - {otherLabel} -{relVal}-> {capitalizedKind}");
                }
            }
        }

        var propertiesYamlList = new List<string>();
        var properties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var propAttr = prop.GetCustomAttribute<OntologyPropertyAttribute>(inherit: true);
            if (propAttr == null || propAttr.IsSystem)
            {
                continue;
            }

            var propType = prop.PropertyType;
            var isNullable = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>);
            var underlyingType = isNullable ? (Nullable.GetUnderlyingType(propType) ?? propType) : propType;

            var typeStr = underlyingType == typeof(string) ? "string" :
                          underlyingType == typeof(int) || underlyingType == typeof(long) ? "integer" :
                          underlyingType == typeof(bool) ? "boolean" :
                          underlyingType.Name.ToLowerInvariant();

            var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var snakeName = jsonPropAttr != null ? jsonPropAttr.Name : ToSnakeCase(prop.Name);
            var description = propAttr.Description;
            
            var formattedDesc = string.IsNullOrEmpty(description) ? typeStr : $"{typeStr} - {description}";
            propertiesYamlList.Add($"  {snakeName}: {formattedDesc}");
        }

        propertiesYamlList =
        [
            .. propertiesYamlList
                .OrderBy(p => p.StartsWith("  name:") ? 0 : p.StartsWith("  path:") ? 1 : 2)
                .ThenBy(p => p)
        ];

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"kind: {capitalizedKind}");
        sb.AppendLine($"purpose: {purpose}");
        if (propertiesYamlList.Count > 0)
        {
            sb.AppendLine("properties:");
            sb.AppendLine(string.Join("\n", propertiesYamlList));
        }
        if (outboundList.Count > 0)
        {
            sb.AppendLine("outbound_relationships:");
            sb.AppendLine(string.Join("\n", outboundList));
        }
        if (inboundList.Count > 0)
        {
            sb.AppendLine("inbound_relationships:");
            sb.AppendLine(string.Join("\n", inboundList));
        }
        return sb.ToString().TrimEnd();
    }

    private static bool TryGetOntologyEdge(object attr, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? rel, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? toType)
    {
        rel = null;
        toType = null;
        var attrType = attr.GetType();

        if (!attrType.IsGenericType || attrType.GetGenericTypeDefinition() != typeof(OntologyEdgeAttribute<>))
        {
            return false;
        }

        var relProp = attrType.GetProperty("Rel");
        var toProp = attrType.GetProperty("To");

        if (relProp == null || toProp == null)
        {
            return false;
        }

        rel = relProp.GetValue(attr) as string;
        toType = toProp.GetValue(attr) as Type;

        return rel != null && toType != null;
    }
}
