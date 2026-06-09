using System.Reflection;
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
        if (string.IsNullOrEmpty(kind)) return "Invalid node kind.";
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
        var relationshipsMarkdown = "  - None defined.";

        var nodeAttr = nodeType.GetCustomAttribute<OntologyNodeAttribute>();
        if (nodeAttr != null)
        {
            purpose = nodeAttr.Purpose;
            if (nodeAttr.Relationships.Length > 0)
            {
                relationshipsMarkdown = string.Join("\n", nodeAttr.Relationships.Select(r => $"  - {r}"));
            }
        }

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

            var snakeName = ToSnakeCase(prop.Name);
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
}
