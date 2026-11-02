using System.Globalization;
using System.Text.Json;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis.ProjectMap;

/// <summary>
/// Maps a <see cref="ProjectMapResult"/> to the generic graph DTO consumed by the webview, CLI and MCP serializers.
/// Module nodes reuse the existing <c>Folder</c> / <c>File</c> node kinds; no new kinds are introduced.
/// </summary>
public static class ProjectMapGraphMapper
{
    private const int MaxListedFiles = 25;

    public static readonly IReadOnlyList<(ProjectMapTier Tier, string Label, string Description)> TierDefinitions =
    [
        (ProjectMapTier.Ingress, "EntryPoints & Endpoints", "CLI commands, HTTP/gRPC/MCP endpoints, and external triggers (Orbit 0)"),
        (ProjectMapTier.Application, "Classes — Orbit 1", "Entry handlers and direct orchestrators (BFS Depth 1)"),
        (ProjectMapTier.Core, "Classes — Orbit 2", "Application and domain services (BFS Depth 2)"),
        (ProjectMapTier.Data, "Classes — Orbit 3", "Engine, repositories, and lower-level services (BFS Depth 3)"),
        (ProjectMapTier.Foundation, "Classes — Orbit 4+", "Foundation classes, helpers, and deep dependencies (BFS Depth 4+)"),
        (ProjectMapTier.Resource, "Transports, Buses & Libraries", "Message buses, topics, external services, and workspace libraries (Orbit 5)"),
        (ProjectMapTier.Database, "Databases & Storage", "Databases, tables, procedures, and datasets (Orbit 6)")
    ];

    public static void Populate(GraphDataDto graph, ProjectMapResult result)
    {
        var defaultModuleKind = result.Granularity == ProjectMapGranularity.File ? NodeKind.File : NodeKind.Folder;

        foreach (var m in result.Modules)
        {
            var nodeKind = !string.IsNullOrEmpty(m.NodeKindOverride)
                ? EnumExtensions.ParseNodeKind(m.NodeKindOverride)
                : defaultModuleKind;
            var filePath = !string.IsNullOrEmpty(m.NodeKindOverride)
                ? (m.Files.FirstOrDefault() ?? m.Path)
                : (result.Granularity == ProjectMapGranularity.File ? m.Path : null);

            graph.Nodes.Add(new GraphNodeDto
            {
                Id = m.Id,
                Name = m.Name,
                Kind = nodeKind,
                FilePath = filePath,
                Properties = BuildModuleProperties(m)
            });
        }

        foreach (var r in result.Resources)
        {
            graph.Nodes.Add(new GraphNodeDto
            {
                Id = r.Id,
                Name = r.Name,
                Kind = EnumExtensions.ParseNodeKind(r.Kind),
                Properties = new Dictionary<string, string>
                {
                    ["pm_type"] = "resource",
                    ["resource_kind"] = r.Kind,
                    ["tier"] = ((int)r.Tier).ToString(CultureInfo.InvariantCulture),
                    ["tier_label"] = TierLabel(r.Tier)
                }
            });
        }

        foreach (var e in result.Edges)
        {
            graph.Edges.Add(BuildEdge(e));
        }

        var meta = graph.Metadata ??= new Dictionary<string, string>();
        meta["project_id"] = result.Input.ProjectId;
        meta["project_name"] = result.Input.ProjectName;
        meta["project_path"] = result.Input.ProjectPath;
        meta["source_root"] = result.SourceRoot;
        meta["granularity"] = result.Granularity.ToString().ToLowerInvariant();
        meta["module_count"] = result.Modules.Count.ToString(CultureInfo.InvariantCulture);
        meta["resource_count"] = result.Resources.Count.ToString(CultureInfo.InvariantCulture);
        meta["edge_count"] = result.Edges.Count(e => !e.IsResourceLink).ToString(CultureInfo.InvariantCulture);
        meta["cycle_count"] = result.Cycles.Count.ToString(CultureInfo.InvariantCulture);
        meta["cycles"] = JsonSerializer.Serialize(result.Cycles);
        meta["test_files_excluded"] = result.TestFilesExcluded.ToString(CultureInfo.InvariantCulture);
        meta["tiers"] = JsonSerializer.Serialize(TierDefinitions.Select(t => new { tier = (int)t.Tier, label = t.Label, description = t.Description }));
    }

    public static string TierLabel(ProjectMapTier tier) =>
        TierDefinitions.First(t => t.Tier == tier).Label;

    private static Dictionary<string, string> BuildModuleProperties(ProjectMapModule m)
    {
        var props = new Dictionary<string, string>
        {
            ["pm_type"] = m.PmType ?? "module",
            ["path"] = m.Path,
            ["tier"] = ((int)m.Tier).ToString(CultureInfo.InvariantCulture),
            ["tier_label"] = TierLabel(m.Tier),
            ["tier_reason"] = m.TierReason,
            ["file_count"] = m.Files.Count.ToString(CultureInfo.InvariantCulture),
            ["line_count"] = m.LineCount.ToString(CultureInfo.InvariantCulture),
            ["fan_in"] = m.FanIn.ToString(CultureInfo.InvariantCulture),
            ["fan_out"] = m.FanOut.ToString(CultureInfo.InvariantCulture),
            ["instability"] = m.Instability.ToString("0.00", CultureInfo.InvariantCulture),
            ["entry_points"] = m.EntryPointCount.ToString(CultureInfo.InvariantCulture),
            ["touches_data"] = m.TouchesData ? "true" : "false",
            ["data_file_count"] = m.DataFileCount.ToString(CultureInfo.InvariantCulture),
            ["files"] = JsonSerializer.Serialize(m.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Take(MaxListedFiles))
        };

        if (!string.IsNullOrEmpty(m.SubKind)) props["sub_kind"] = m.SubKind;
        if (m.StartLine.HasValue) props["start_line"] = m.StartLine.Value.ToString(CultureInfo.InvariantCulture);
        if (m.BfsDepth.HasValue) props["bfs_depth"] = m.BfsDepth.Value.ToString(CultureInfo.InvariantCulture);
        if (m.Methods.Count > 0)
        {
            props["method_count"] = m.Methods.Count.ToString(CultureInfo.InvariantCulture);
            props["methods"] = JsonSerializer.Serialize(m.Methods.Take(MaxListedFiles).Select(x => new
            {
                name = x.Name,
                path = x.FilePath,
                line = x.Line
            }));
        }
        if (m.ImplementedInterfaces.Count > 0)
        {
            props["interfaces"] = JsonSerializer.Serialize(m.ImplementedInterfaces);
        }
        if (m.IsRootFiles) props["is_root_files"] = "true";
        if (m.IsMerged) props["is_merged"] = "true";
        if (m.IsSubProject)
        {
            props["pm_type"] = "subproject";
            if (!string.IsNullOrEmpty(m.SubProjectId)) props["subproject_id"] = m.SubProjectId;
            if (!string.IsNullOrEmpty(m.SubProjectKind)) props["subproject_kind"] = m.SubProjectKind;
        }
        if (m.CycleGroup.HasValue) props["cycle_group"] = m.CycleGroup.Value.ToString(CultureInfo.InvariantCulture);
        return props;
    }

    private static GraphEdgeDto BuildEdge(ProjectMapEdge e)
    {
        var dominant = e.Kinds.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key).FirstOrDefault() ?? "RELATED";
        var props = new Dictionary<string, string>
        {
            ["call_count"] = e.Count.ToString(CultureInfo.InvariantCulture),
            ["distinct_files"] = e.Files.Count.ToString(CultureInfo.InvariantCulture),
            ["kinds"] = JsonSerializer.Serialize(e.Kinds),
            ["samples"] = JsonSerializer.Serialize(e.Samples.Select(s => new
            {
                from = s.FromSymbol,
                to = s.ToSymbol,
                file = s.File,
                line = s.Line,
                kind = s.Kind
            })),
            ["is_circular"] = e.IsCircular ? "true" : "false",
            ["is_resource_link"] = e.IsResourceLink ? "true" : "false"
        };

        return new GraphEdgeDto
        {
            Id = $"pm:e:{e.SourceId}->{e.TargetId}",
            Source = e.SourceId,
            Target = e.TargetId,
            Kind = dominant,
            Category = e.IsResourceLink ? "resource" : "dependency",
            Properties = props
        };
    }
}
