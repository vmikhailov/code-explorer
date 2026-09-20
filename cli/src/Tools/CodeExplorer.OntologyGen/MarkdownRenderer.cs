using System.Text;

namespace OntologyGen;

/// <summary>
/// Renders extracted ontology metadata into a Markdown document.
/// </summary>
public static class MarkdownRenderer
{
    public static string Render(List<NodeInfo> nodes, List<RelInfo> relationships)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!-- AUTO-GENERATED — do not edit manually. Re-generated on every build by OntologyGen. -->");
        sb.AppendLine();
        sb.AppendLine("# CodeExplorer Ontology");
        sb.AppendLine();
        sb.AppendLine("> This document is generated from source annotations during the build.");
        sb.AppendLine("> Edit the `[OntologyNode]`, `[OntologyEdge<>]`, `[OntologyProperty]`, and `[OntologyRelationship]` attributes in the source files to update it.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        var classToLabel = nodes.ToDictionary(n => n.ClassName, n => n.Label);
        var nodesByLayer = nodes.GroupBy(n => n.Layer).OrderBy(g => g.Key).ToList();

        // ── 1. Mermaid Diagram ───────────────────────────────────────────────────
        sb.AppendLine("## 📊 Architectural Overview (Mermaid Diagram)");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph TD");

        int layerIndex = 1;
        foreach (var group in nodesByLayer)
        {
            if (string.IsNullOrEmpty(group.Key))
            {
                foreach (var node in group.OrderBy(n => n.Label))
                {
                    sb.AppendLine($"    {node.Label}[\"{node.Label}\"]");
                }
                sb.AppendLine();
                continue;
            }
            var layerId = $"Layer{layerIndex++}";
            sb.AppendLine($"    subgraph {layerId} [\"{group.Key}\"]");
            foreach (var node in group.OrderBy(n => n.Label))
            {
                sb.AppendLine($"        {node.Label}[\"{node.Label}\"]");
            }
            sb.AppendLine("    end");
            sb.AppendLine();
        }

        var renderedEdges = new HashSet<(string from, string rel, string to)>();
        foreach (var node in nodes.OrderBy(n => n.Label))
        {
            foreach (var edge in node.OutEdges)
            {
                if (classToLabel.TryGetValue(edge.ToTypeName, out var targetLabel))
                {
                    if (renderedEdges.Add((node.Label, edge.Rel, targetLabel)))
                    {
                        sb.AppendLine($"    {node.Label} -->|{edge.Rel}| {targetLabel}");
                    }
                }
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // ── 2. Layered Definitions ───────────────────────────────────────────────
        sb.AppendLine("## 📂 Layered Definitions");
        sb.AppendLine();

        // Build incoming-edge index by inverting all outbound declarations
        var incomingEdges = nodes
            .SelectMany(n => n.OutEdges.Select(e => (target: e.ToTypeName, from: n.Label, rel: e.Rel)))
            .ToLookup(x => x.target, x => (x.from, x.rel));

        foreach (var group in nodesByLayer)
        {
            if (string.IsNullOrEmpty(group.Key))
            {
                sb.AppendLine("### 🌐 Root System Umbrella");
            }
            else
            {
                sb.AppendLine($"### 📂 {group.Key}");
            }
            sb.AppendLine();

            foreach (var node in group.OrderBy(n => n.Label))
            {
                sb.AppendLine($"#### `{node.Label}`");
                sb.AppendLine();
                sb.AppendLine($"> {node.Purpose}");
                sb.AppendLine();

                // Outbound edges table
                if (node.OutEdges.Count > 0)
                {
                    sb.AppendLine("**Outbound edges:**");
                    sb.AppendLine();
                    sb.AppendLine("| Relationship | To |");
                    sb.AppendLine("| :--- | :--- |");
                    foreach (var e in node.OutEdges)
                    {
                        var targetLabel = classToLabel.TryGetValue(e.ToTypeName, out var l) ? l : StripNodeSuffix(e.ToTypeName);
                        sb.AppendLine($"| `{e.Rel}` | `{targetLabel}` |");
                    }
                    sb.AppendLine();
                }

                // Incoming edges (derived from other nodes' outbound declarations)
                var incoming = incomingEdges[node.ClassName].ToList();
                if (incoming.Count > 0)
                {
                    sb.AppendLine("**Incoming edges** *(derived from other nodes' declarations)*:");
                    sb.AppendLine();
                    sb.AppendLine("| From | Relationship |");
                    sb.AppendLine("| :--- | :--- |");
                    foreach (var (from, rel) in incoming.OrderBy(x => x.from))
                    {
                        sb.AppendLine($"| `{from}` | `{rel}` |");
                    }
                    sb.AppendLine();
                }

                // Properties table
                if (node.Properties.Count > 0)
                {
                    sb.AppendLine("**Properties:**");
                    sb.AppendLine();
                    sb.AppendLine("| Property | Type | Description |");
                    sb.AppendLine("| :--- | :--- | :--- |");
                    foreach (var p in node.Properties)
                    {
                        sb.AppendLine($"| `{p.Name}` | `{p.Type}` | {EscapePipe(p.Description)} |");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        // Layer 5: SystemBindings (Integration Links)
        sb.AppendLine("## 📂 Layer 5: SystemBindings (Integration Links)");
        sb.AppendLine();
        sb.AppendLine("> This layer contains the relationship edges that connect nodes across layers into a unified semantic map.");
        sb.AppendLine();
        if (relationships.Count > 0)
        {
            sb.AppendLine("| Relationship Label | Description |");
            sb.AppendLine("| :--- | :--- |");
            foreach (var rel in relationships.OrderBy(r => r.Label))
            {
                sb.AppendLine($"| `{rel.Label}` | {EscapePipe(rel.Description)} |");
            }
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine();

        // ── 3. URN & ID Schemes ──────────────────────────────────────────────────
        sb.AppendLine("## 🏷️ Uniform Resource Name (URN) & ID Schemes");
        sb.AppendLine();
        sb.AppendLine("> Every node in the CodeExplorer graph has a structured ID (URN) that guarantees uniqueness across projects and workspaces.");
        sb.AppendLine();
        sb.AppendLine("| Layer | Node Label | ID / URN Scheme |");
        sb.AppendLine("| :--- | :--- | :--- |");
        foreach (var node in nodes.OrderBy(n => n.Layer).ThenBy(n => n.Label))
        {
            var layerName = string.IsNullOrEmpty(node.Layer) ? "Root / Umbrella" : node.Layer;
            var scheme = string.IsNullOrEmpty(node.IdScheme) ? "*None*" : $"`{node.IdScheme}`";
            sb.AppendLine($"| {layerName} | `{node.Label}` | {scheme} |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    private static string StripNodeSuffix(string typeName) =>
        typeName.EndsWith("Node", StringComparison.Ordinal)
            ? typeName[..^4]
            : typeName;

    private static string EscapePipe(string text) =>
        text.Replace("|", "\\|");
}
