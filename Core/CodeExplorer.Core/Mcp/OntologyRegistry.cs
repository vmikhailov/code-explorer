using System.Reflection;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Mcp;

public static class OntologyRegistry
{
    public static readonly Dictionary<string, (string CapitalizedKind, Type NodeType)> KindMapping = 
        typeof(IOntologyNode).Assembly.GetTypes()
            .Where(t => typeof(IOntologyNode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => 
            {
                var instance = (IOntologyNode)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);
                return (Kind: instance.Kind, Type: t);
            })
            .Where(x => x.Type.GetCustomAttribute<OntologyNodeAttribute>() != null)
            .ToDictionary(
                x => x.Kind, 
                x => (x.Kind, x.Type), 
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
                outboundList.Add($"  - `-{relVal}->` {targetLabel}");
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
                    inboundList.Add($"  - {otherLabel} `-{relVal}->` {capitalizedKind}");
                }
            }
        }

        var relationshipsMarkdownList = new List<string>();
        if (outboundList.Any())
        {
            relationshipsMarkdownList.Add("Outbound:\n" + string.Join("\n", outboundList.ToArray()));
        }
        else
        {
            relationshipsMarkdownList.Add("Outbound:\n  - None defined.");
        }

        if (inboundList.Any())
        {
            relationshipsMarkdownList.Add("Inbound:\n" + string.Join("\n", inboundList.ToArray()));
        }
        var relationshipsMarkdown = string.Join("\n", relationshipsMarkdownList);

        var propertiesMarkdownList = new List<string>();
        var properties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var propAttr = prop.GetCustomAttribute<OntologyPropertyAttribute>();
            if (propAttr == null)
            {
                continue;
            }

            var propType = prop.PropertyType;
            var isNullable = propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>);
            var underlyingType = isNullable ? Nullable.GetUnderlyingType(propType)! : propType;

            var typeStr = underlyingType == typeof(string) ? "string" :
                          underlyingType == typeof(int) || underlyingType == typeof(long) ? "integer" :
                          underlyingType == typeof(bool) ? "boolean" :
                          underlyingType.Name.ToLowerInvariant();

            var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var snakeName = jsonPropAttr != null ? jsonPropAttr.Name : ToSnakeCase(prop.Name);
            var description = propAttr.Description;
            
            var formattedDesc = string.IsNullOrEmpty(description) ? "" : $": {description}";
            propertiesMarkdownList.Add($"  - `{snakeName}` ({typeStr}){formattedDesc}");
        }

        propertiesMarkdownList = propertiesMarkdownList
            .OrderBy(p => p.Contains("`name`") ? 0 : p.Contains("`path`") ? 1 : 2)
            .ThenBy(p => p)
            .ToList();

        var propertiesMarkdown = string.Join("\n", propertiesMarkdownList);

        return $"### Kind: {capitalizedKind}\n" +
               $"**Purpose**: {purpose}\n" +
               $"**Key Properties**:\n{propertiesMarkdown}\n" +
               $"**Relationships**:\n{relationshipsMarkdown}";
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
