using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using ArchitectureViewType = CodeExplorer.Core.Analysis.ArchitectureViewType;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    // =========================================================================
    // 8. Multi-Format LLM Serializers (Markdown, TOON, Mermaid, JSON)
    // =========================================================================

    public static string SerializeDomainArchitecture(DomainArchitectureDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaid(dto),
            "toon" => ToToon(dto),
            _ => ToMarkdown(dto)
        };
    }

    public static string ToMarkdown(DomainArchitectureDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Domain Microservices Architecture");
        sb.AppendLine();
        sb.AppendLine($"**Domains:** {dto.Stats.TotalDomains} | **Services:** {dto.Stats.Services} | **Apps:** {dto.Stats.Ingress} | **Workers:** {dto.Stats.Workers} | **Databases:** {dto.Stats.Databases} | **Topics:** {dto.Stats.Topics}");
        sb.AppendLine($"**Inter-Domain Calls:** {dto.Stats.ServiceCalls} | **Async Messages:** {dto.Stats.Messages}");
        sb.AppendLine();
        sb.AppendLine("## Bounded Contexts & Services");
        sb.AppendLine("| Domain | Kind | Projects | Inbound Calls | Outbound Calls | Databases | Topics |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var d in dto.Nodes.Where(n => n.Kind is NodeKind.Service or NodeKind.Ingress or NodeKind.Worker or NodeKind.Library or NodeKind.App or NodeKind.CliTool))
        {
            var pCount = d.Projects.Count > 0 ? string.Join(", ", d.Projects.Select(p => p.Name)) : d.Name;
            sb.AppendLine($"| {d.DisplayName} | `{d.Kind.ToCypherLabel()}` | {pCount} | {d.InboundCallsCount} | {d.OutboundCallsCount} | {d.DbCount} | {d.MessagingCount} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Inter-Domain Relationships");
        sb.AppendLine("| Source | Target | Category | Calls / Flow |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var e in dto.Edges)
        {
            sb.AppendLine($"| {e.Source} | {e.Target} | `{e.Category}` | {e.Count} |");
        }
        return sb.ToString();
    }

    public static string ToToon(DomainArchitectureDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("domain_architecture:");
        sb.AppendLine("  stats:");
        sb.AppendLine($"    domains: {dto.Stats.TotalDomains}");
        sb.AppendLine($"    services: {dto.Stats.Services}");
        sb.AppendLine($"    apps: {dto.Stats.Ingress}");
        sb.AppendLine($"    workers: {dto.Stats.Workers}");
        sb.AppendLine($"    databases: {dto.Stats.Databases}");
        sb.AppendLine($"    topics: {dto.Stats.Topics}");
        sb.AppendLine($"    calls: {dto.Stats.ServiceCalls}");
        sb.AppendLine($"    messages: {dto.Stats.Messages}");
        sb.AppendLine("  domains:");
        foreach (var d in dto.Nodes.Where(n => n.Kind is NodeKind.Service or NodeKind.Ingress or NodeKind.Worker or NodeKind.Library))
        {
            sb.AppendLine($"    - id: {d.Id}, name: {d.DisplayName}, kind: {d.Kind.ToCypherLabel()}, calls_in: {d.InboundCallsCount}, calls_out: {d.OutboundCallsCount}, dbs: {d.DbCount}, topics: {d.MessagingCount}");
        }
        sb.AppendLine("  macro_edges:");
        foreach (var e in dto.Edges)
        {
            sb.AppendLine($"    - from: {e.Source}, to: {e.Target}, category: {e.Category}, count: {e.Count}");
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> BuildShortMermaidIdMap(IEnumerable<DomainEntityDto> nodes)
    {
        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            var raw = node.Id ?? "";
            string prefix = node.Kind switch
            {
                NodeKind.Ingress => "app",
                NodeKind.Service or NodeKind.Worker => "svc",
                NodeKind.Database => "db",
                NodeKind.Topic => "topic",
                NodeKind.ExternalService => "ext",
                NodeKind.Library => "lib",
                _ => "node"
            };

            var baseName = !string.IsNullOrWhiteSpace(node.DisplayName) ? node.DisplayName : node.Name;
            baseName = Regex.Replace(baseName ?? "", @"[^\w\.\-]", "_");

            var slug = baseName.ToLowerInvariant()
                .Replace("postgresql.", "pg_")
                .Replace("mysql.", "mysql_")
                .Replace("clickhouse", "ch")
                .Replace("bigquery", "bq")
                .Replace("internal--", "")
                .Replace("integration--", "")
                .Replace("internal-service-", "")
                .Replace("internal_service_", "")
                .Replace("integration-service-", "")
                .Replace("integration_service_", "")
                .Replace("external-service-", "")
                .Replace("external_service_", "")
                .Replace("service-", "")
                .Replace("service_", "")
                .Replace("workspace:database:", "")
                .Replace("workspace:externalservice:", "")
                .Replace("workspace:topic:", "")
                .Replace("workspace:library:", "")
                .Replace("ws:db:", "")
                .Replace("ws:es:", "")
                .Replace("ws:top:", "")
                .Replace("ws:lib:", "")
                .Replace("domain:", "");

            slug = Regex.Replace(slug, @"[^a-z0-9_]", "_").Trim('_');
            if (string.IsNullOrWhiteSpace(slug)) slug = "item";

            var candidate = $"{prefix}_{slug}";
            if (candidate.Length > 40) candidate = candidate[..40].TrimEnd('_');

            var finalId = candidate;
            int counter = 2;
            while (usedIds.Contains(finalId))
            {
                finalId = $"{candidate}_{counter++}";
            }

            usedIds.Add(finalId);
            idMap[raw] = finalId;
            idMap[Regex.Replace(raw, @"[^a-zA-Z0-9_]", "_")] = finalId;
        }

        return idMap;
    }

    public static string ToMermaid(DomainArchitectureDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        var idMap = BuildShortMermaidIdMap(dto.Nodes);
        var getId = (string s) => idMap.GetValueOrDefault(s) ??
                                  idMap.GetValueOrDefault(Regex.Replace(s, @"[^a-zA-Z0-9_]", "_")) ??
                                  Regex.Replace(s, @"[^a-zA-Z0-9_]", "_");
        var esc = (string s) => s?.Replace("\"", "'") ?? "";

        var ingress = dto.Nodes.Where(n => n.Kind == NodeKind.Ingress).ToList();
        if (ingress.Count > 0)
        {
            sb.AppendLine("  subgraph Ingress [\"Apps & Ingress\"]");
            foreach (var node in ingress) sb.AppendLine($"    {getId(node.Id)}[\"{esc(node.DisplayName)}\"]");
            sb.AppendLine("  end");
        }

        var services = dto.Nodes.Where(n => n.Kind is NodeKind.Service or NodeKind.Worker).ToList();
        if (services.Count > 0)
        {
            sb.AppendLine("  subgraph Services [\"Core Services\"]");
            foreach (var node in services) sb.AppendLine($"    {getId(node.Id)}[\"{esc(node.DisplayName)}\"]");
            sb.AppendLine("  end");
        }

        var dbs = dto.Nodes.Where(n => n.Kind == NodeKind.Database).ToList();
        if (dbs.Count > 0)
        {
            sb.AppendLine("  subgraph Databases [\"Databases\"]");
            foreach (var node in dbs) sb.AppendLine($"    {getId(node.Id)}[(\"🗄️ {esc(node.DisplayName)}\")]");
            sb.AppendLine("  end");
        }

        var topics = dto.Nodes.Where(n => n.Kind == NodeKind.Topic).ToList();
        if (topics.Count > 0)
        {
            sb.AppendLine("  subgraph Topics [\"Message Queues\"]");
            foreach (var node in topics) sb.AppendLine($"    {getId(node.Id)}{{\"📬 {esc(node.DisplayName)}\"}}");
            sb.AppendLine("  end");
        }

        var external = dto.Nodes.Where(n => n.Kind == NodeKind.ExternalService).ToList();
        if (external.Count > 0)
        {
            sb.AppendLine("  subgraph External [\"External Systems\"]");
            foreach (var node in external) sb.AppendLine($"    {getId(node.Id)}[\"🌐 {esc(node.DisplayName)}\"]");
            sb.AppendLine("  end");
        }

        var libs = dto.Nodes.Where(n => n.Kind == NodeKind.Library).ToList();
        if (libs.Count > 0)
        {
            sb.AppendLine("  subgraph Libraries [\"Shared Libraries\"]");
            foreach (var node in libs) sb.AppendLine($"    {getId(node.Id)}[\"📦 {esc(node.DisplayName)}\"]");
            sb.AppendLine("  end");
        }

        foreach (var e in dto.Edges)
        {
            sb.AppendLine($"  {getId(e.Source)} -->|\"{esc(e.Label)} ({e.Count})\"| {getId(e.Target)}");
        }

        return sb.ToString();
    }

    public static string SerializeServiceContract(ServiceContractDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "toon" => ToToon(dto),
            _ => ToMarkdown(dto)
        };
    }

    public static string ToMarkdown(ServiceContractDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Service Contract: {dto.ServiceName}");
        sb.AppendLine();
        sb.AppendLine($"**Kind:** `{dto.Kind}` | **Framework:** {dto.Framework ?? "N/A"} | **Language:** {dto.Language ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("## Ingress (Inbound API & Messaging)");
        if (dto.IngressEndpoints.Count > 0)
        {
            sb.AppendLine("### HTTP Endpoints");
            foreach (var ep in dto.IngressEndpoints) sb.AppendLine($"- `{ep}`");
        }
        if (dto.SubscribedTopics.Count > 0)
        {
            sb.AppendLine("### Subscribed Message Topics");
            foreach (var t in dto.SubscribedTopics) sb.AppendLine($"- 📬 `{t}`");
        }
        sb.AppendLine();
        sb.AppendLine("## Egress (Outbound Dependencies)");
        if (dto.OutboundServiceCalls.Count > 0)
        {
            sb.AppendLine("### Service-to-Service Calls");
            foreach (var c in dto.OutboundServiceCalls) sb.AppendLine($"- ⚡ `{c}`");
        }
        if (dto.PublishedTopics.Count > 0)
        {
            sb.AppendLine("### Published Message Topics");
            foreach (var p in dto.PublishedTopics) sb.AppendLine($"- ✉️ `{p}`");
        }
        if (dto.Databases.Count > 0)
        {
            sb.AppendLine("### Databases & Storage");
            foreach (var db in dto.Databases) sb.AppendLine($"- 🗄️ `{db}`");
        }
        if (dto.ExternalServices.Count > 0)
        {
            sb.AppendLine("### External APIs");
            foreach (var ext in dto.ExternalServices) sb.AppendLine($"- ☁️ `{ext}`");
        }
        return sb.ToString();
    }

    public static string ToToon(ServiceContractDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("service_contract:");
        sb.AppendLine($"  service: {dto.ServiceName}");
        sb.AppendLine($"  kind: {dto.Kind}");
        sb.AppendLine($"  framework: {dto.Framework ?? "N/A"}");
        sb.AppendLine($"  language: {dto.Language ?? "N/A"}");
        sb.AppendLine($"  ingress_endpoints: [{string.Join(", ", dto.IngressEndpoints)}]");
        sb.AppendLine($"  subscribed_topics: [{string.Join(", ", dto.SubscribedTopics)}]");
        sb.AppendLine($"  outbound_service_calls: [{string.Join(", ", dto.OutboundServiceCalls)}]");
        sb.AppendLine($"  published_topics: [{string.Join(", ", dto.PublishedTopics)}]");
        sb.AppendLine($"  databases: [{string.Join(", ", dto.Databases)}]");
        sb.AppendLine($"  external_services: [{string.Join(", ", dto.ExternalServices)}]");
        return sb.ToString();
    }

    public static string SerializeCrossServiceFlow(CrossServiceFlowDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaid(dto),
            _ => ToMarkdown(dto)
        };
    }

    public static string ToMarkdown(CrossServiceFlowDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Cross-Service Execution Flow: {dto.StartService}");
        if (!string.IsNullOrEmpty(dto.EntryPoint)) sb.AppendLine($"**Entry Point:** `{dto.EntryPoint}`");
        sb.AppendLine($"**Max Depth:** {dto.MaxDepth} | **Visited Services:** {string.Join(" -> ", dto.VisitedServices)}");
        sb.AppendLine();
        sb.AppendLine("## Execution Trace Hops");
        sb.AppendLine("| Step | Source | Target | Relationship | Protocol / Details |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var hop in dto.Hops)
        {
            sb.AppendLine($"| {hop.Step} | **{hop.Source}** | **{hop.Target}** | `{hop.Kind}` | {hop.Protocol ?? "N/A"} {hop.Details} |");
        }
        return sb.ToString();
    }

    public static string ToMermaid(CrossServiceFlowDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        var sanitize = (string s) => s.Replace(":", "_").Replace("-", "_").Replace(".", "_").Replace("/", "_");
        foreach (var hop in dto.Hops)
        {
            sb.AppendLine($"  {sanitize(hop.Source)} -->|\"{hop.Kind}\"| {sanitize(hop.Target)}");
        }
        return sb.ToString();
    }

    public static string SerializeGraph(GraphDataDto graph, string format, string title = "Architecture View")
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(new { results = graph }, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaid(graph),
            "toon" or "yaml" => ToToon(graph),
            _ => ToMarkdown(graph, title)
        };
    }

    public static string ToMermaid(GraphDataDto graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        var sanitize = (string s) => s.Replace(":", "_").Replace("-", "_").Replace(".", "_").Replace("/", "_");

        foreach (var node in graph.Nodes)
        {
            var label = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            sb.AppendLine($"  {sanitize(node.Id)}[\"{label}\"]");
        }

        foreach (var edge in graph.Edges)
        {
            var kind = !string.IsNullOrWhiteSpace(edge.Kind) ? edge.Kind : "DEPENDS_ON";
            sb.AppendLine($"  {sanitize(edge.Source)} -->|\"{kind}\"| {sanitize(edge.Target)}");
        }

        return sb.ToString();
    }

    public static string ToToon(GraphDataDto graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph_view:");
        sb.AppendLine($"  nodes_count: {graph.Nodes.Count}");
        sb.AppendLine($"  edges_count: {graph.Edges.Count}");
        sb.AppendLine("  nodes:");
        foreach (var node in graph.Nodes)
        {
            var label = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            sb.AppendLine($"    - id: {node.Id}");
            sb.AppendLine($"      name: {label}");
            sb.AppendLine($"      kind: {node.Kind.ToCypherLabel()}");
        }
        sb.AppendLine("  edges:");
        foreach (var edge in graph.Edges)
        {
            sb.AppendLine($"    - {edge.Source} -> {edge.Target} [{edge.Kind}]");
        }
        return sb.ToString();
    }

    public static string ToMarkdown(GraphDataDto graph, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"**Total Nodes:** {graph.Nodes.Count} | **Total Edges:** {graph.Edges.Count}");
        sb.AppendLine();
        sb.AppendLine("## Nodes");
        sb.AppendLine("| Node | Kind | Subkind |");
        sb.AppendLine("|---|---|---|");
        foreach (var node in graph.Nodes.OrderBy(n => n.Kind).ThenBy(n => n.Name))
        {
            var label = !string.IsNullOrWhiteSpace(node.Name) ? node.Name : node.Id;
            var subkind = node.Properties != null && node.Properties.TryGetValue("subkind", out var sk) ? sk : "N/A";
            sb.AppendLine($"| **{label}** | `{node.Kind.ToCypherLabel()}` | {subkind} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Relationships");
        sb.AppendLine("| Source | Target | Relationship | Category |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var edge in graph.Edges.OrderBy(e => e.Kind).ThenBy(e => e.Source))
        {
            sb.AppendLine($"| **{edge.Source}** | **{edge.Target}** | `{edge.Kind}` | {edge.Category ?? "N/A"} |");
        }
        return sb.ToString();
    }

    public static string SerializeBoundedContextMap(BoundedContextMapDto dto, string format)
    {
        return (format.ToLowerInvariant()) switch
        {
            "json" => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = false }),
            "mermaid" => ToMermaidBoundedContexts(dto),
            _ => ToMarkdownBoundedContexts(dto)
        };
    }

    public static string ToMermaidBoundedContexts(BoundedContextMapDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        if (dto.Contexts.Count == 0)
        {
            sb.AppendLine("  NoContexts[\"No Bounded Contexts inferred (run 'ce intent')\"]");
            return sb.ToString();
        }

        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var idx = 1;
        foreach (var c in dto.Contexts)
        {
            var shortId = $"ctx_{idx++}";
            idMap[c.Id] = shortId;
            var entitiesStr = c.TargetEntities.Count > 0 ? string.Join(", ", c.TargetEntities.Take(3)) : "No entities";
            var label = $"{c.DisplayName}\\n({c.FileCount} files, {c.PurityPercentage}% pure)\\nEntities: {entitiesStr}";
            sb.AppendLine($"  {shortId}[\"{label}\"]");
        }

        foreach (var edge in dto.Interactions)
        {
            if (idMap.TryGetValue(edge.Source, out var src) && idMap.TryGetValue(edge.Target, out var tgt))
            {
                var arrow = edge.InteractionType == "messaging" ? "-.->|" : "-->|";
                sb.AppendLine($"  {src} {arrow}{edge.Label} ({edge.Count})| {tgt}");
            }
        }

        return sb.ToString();
    }

    public static string ToMarkdownBoundedContexts(BoundedContextMapDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AI-Distilled Bounded Contexts Map");
        sb.AppendLine();
        sb.AppendLine($"**Total Contexts:** {dto.Contexts.Count} | **Total Files Analyzed:** {dto.TotalIntents} | **High Purity Domains:** {dto.TotalPureDomains}");
        sb.AppendLine();
        sb.AppendLine("## Contexts & Ubiquitous Language");
        sb.AppendLine("| Bounded Context | Files | Purity | Entities | Emitted Events | Primary Layers |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var c in dto.Contexts)
        {
            var ents = c.TargetEntities.Count > 0 ? string.Join(", ", c.TargetEntities) : "-";
            var evts = c.EmittedEvents.Count > 0 ? string.Join(", ", c.EmittedEvents) : "-";
            var topLayers = string.Join(", ", c.Layers.OrderByDescending(kv => kv.Value).Take(2).Select(kv => $"{kv.Key} ({kv.Value})"));
            sb.AppendLine($"| **{c.DisplayName}** | {c.FileCount} | {c.PurityPercentage}% | {ents} | {evts} | {topLayers} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Inter-Context Interactions");
        sb.AppendLine("| Source Context | Target Context | Interaction Type | Label | Count |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var e in dto.Interactions)
        {
            var src = dto.Contexts.FirstOrDefault(c => c.Id == e.Source)?.DisplayName ?? e.Source;
            var tgt = dto.Contexts.FirstOrDefault(c => c.Id == e.Target)?.DisplayName ?? e.Target;
            sb.AppendLine($"| {src} | {tgt} | `{e.InteractionType}` | `{e.Label}` | {e.Count} |");
        }
        return sb.ToString();
    }
}
