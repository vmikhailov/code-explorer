using System.Text;
using CodeExplorer.Core.Analysis.Subgraph;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private const int InlineClusterMemberPreviewCap = 24;

    public async Task<string> GetWorkspaceClustersAsync(
        string? clusterName = null,
        string[]? seeds = null,
        double resolution = 1.0,
        double hubThreshold = 0.15,
        int maxUnits = 10,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var effectiveWsRoot = await ResolveWorkspacePathAsync(workspacePath);
        var service = new WorkspaceSubgraphService();

        if (seeds is { Length: > 0 })
        {
            var cleanSeeds = seeds.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
            if (cleanSeeds.Count > 0)
            {
                var slice = await service.SliceSubgraphAsync(client, cleanSeeds, Math.Clamp(maxUnits, 1, 100), 0.85, cancellationToken);
                return ToonYamlSerializer.SerializeObject(slice);
            }
        }

        var report = await service.PartitionClustersAsync(client, resolution, hubThreshold, effectiveWsRoot, cancellationToken);
        if (!string.IsNullOrWhiteSpace(clusterName))
        {
            var drillDown = BuildClusterDrillDownPayload(report, clusterName.Trim());
            if (drillDown == null)
            {
                return ToonYamlSerializer.SerializeObject(new
                {
                    status = "not_found",
                    requested_cluster = clusterName.Trim(),
                    available_clusters = BuildAvailableClusterIds(report)
                });
            }
            return ToonYamlSerializer.SerializeObject(drillDown);
        }

        var summaryPayload = new
        {
            raw_project_count = report.RawProjectCount,
            coarsened_unit_count = report.CoarsenedUnitCount,
            resolution = report.Resolution,
            hub_threshold = report.HubThreshold,
            elapsed_ms = report.ElapsedMs,
            shared_libraries = report.SharedLibraries == null ? null : FormatCompactClusterSummary(report.SharedLibraries),
            clusters = report.Clusters.Select(FormatCompactClusterSummary).ToList(),
            boundary_edges = report.BoundaryEdges.Take(50).Select(b => new
            {
                from_cluster = b.FromCluster,
                to_cluster = b.ToCluster,
                weight = b.Weight,
                cross_edge_count = b.CrossEdgeCount,
                sample_reasons = b.SampleReasons
            }).ToList(),
            standalone_units = report.StandaloneUnits.Take(40).Select(u => new
            {
                name = u.Name,
                language = u.PrimaryLanguage,
                project_count = u.MemberProjectCount
            }).ToList(),
            standalone_total_count = report.StandaloneUnits.Count
        };

        return ToonYamlSerializer.SerializeObject(summaryPayload);
    }

    public async Task<string> ExportClusterDiagramAsync(
        string format = "mermaid",
        string? clusterFilter = null,
        bool includeLibraries = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var effectiveWsRoot = await ResolveWorkspacePathAsync(workspacePath);
        var service = new WorkspaceSubgraphService();
        var report = await service.PartitionClustersAsync(client, 1.0, 0.15, effectiveWsRoot, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        static string SafeMermaidId(string id)
        {
            var chars = id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            return "c_" + new string(chars);
        }

        var allowed = string.IsNullOrWhiteSpace(clusterFilter)
            ? null
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { clusterFilter.Trim() };

        foreach (var c in report.Clusters)
        {
            if (allowed != null && !allowed.Contains(c.ClusterId) && !c.DisplayName.Contains(clusterFilter!, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var nodeId = SafeMermaidId(c.ClusterId);
            sb.AppendLine($"  {nodeId}[\"{c.DisplayName}\\n({c.UnitCount} units / {c.ProjectCount} proj)\"]");
        }

        if (includeLibraries && report.SharedLibraries != null)
        {
            var libId = SafeMermaidId(report.SharedLibraries.ClusterId);
            sb.AppendLine($"  {libId}[[\"{report.SharedLibraries.DisplayName}\\n({report.SharedLibraries.UnitCount} libs)\"]]");
        }

        foreach (var edge in report.BoundaryEdges)
        {
            if (!includeLibraries &&
                (string.Equals(edge.FromCluster, "shared-libraries", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(edge.ToCluster, "shared-libraries", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            if (allowed != null && !allowed.Contains(edge.FromCluster) && !allowed.Contains(edge.ToCluster))
            {
                continue;
            }
            sb.AppendLine($"  {SafeMermaidId(edge.FromCluster)} -->|\"{edge.CrossEdgeCount} links\"| {SafeMermaidId(edge.ToCluster)}");
        }

        return sb.ToString().TrimEnd();
    }

    public static object? BuildClusterDrillDownPayload(WorkspaceClusterPartitionReport report, string clusterQuery)
    {
        var allClusters = new List<WorkspaceClusterDto>(report.Clusters.Count + 1);
        if (report.SharedLibraries != null) allClusters.Add(report.SharedLibraries);
        allClusters.AddRange(report.Clusters);

        var matched = allClusters.FirstOrDefault(c =>
            string.Equals(c.ClusterId, clusterQuery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Name, clusterQuery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.DisplayName, clusterQuery, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
        {
            var inbound = report.BoundaryEdges
                .Where(e => string.Equals(e.ToCluster, matched.ClusterId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var outbound = report.BoundaryEdges
                .Where(e => string.Equals(e.FromCluster, matched.ClusterId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new
            {
                cluster_id = matched.ClusterId,
                display_name = matched.DisplayName,
                kind = matched.Kind.ToString(),
                unit_count = matched.UnitCount,
                project_count = matched.ProjectCount,
                internal_weight = matched.InternalWeight,
                languages = matched.Languages,
                sub_clusters = matched.SubClusters?.Select(sc => new
                {
                    sub_cluster_id = sc.SubClusterId,
                    display_name = sc.DisplayName,
                    unit_count = sc.UnitCount,
                    project_count = sc.ProjectCount
                }).ToList(),
                members = matched.Members.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    root_path = m.RootPath,
                    language = m.PrimaryLanguage,
                    is_library_only = m.IsLibraryOnly,
                    project_count = m.MemberProjectCount,
                    member_projects = m.MemberProjects
                }).ToList(),
                inbound_boundary_edges = inbound,
                outbound_boundary_edges = outbound
            };
        }

        foreach (var parent in allClusters)
        {
            if (parent.SubClusters == null) continue;
            var sub = parent.SubClusters.FirstOrDefault(sc =>
                string.Equals(sc.SubClusterId, clusterQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sc.Name, clusterQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sc.DisplayName, clusterQuery, StringComparison.OrdinalIgnoreCase));
            if (sub != null)
            {
                return new
                {
                    sub_cluster_id = sub.SubClusterId,
                    parent_cluster_id = parent.ClusterId,
                    display_name = sub.DisplayName,
                    unit_count = sub.UnitCount,
                    project_count = sub.ProjectCount,
                    members = sub.Members.Select(m => new
                    {
                        id = m.Id,
                        name = m.Name,
                        root_path = m.RootPath,
                        language = m.PrimaryLanguage,
                        is_library_only = m.IsLibraryOnly,
                        project_count = m.MemberProjectCount,
                        member_projects = m.MemberProjects
                    }).ToList()
                };
            }
        }

        return null;
    }

    private static List<string> BuildAvailableClusterIds(WorkspaceClusterPartitionReport report)
    {
        var ids = new List<string>();
        if (report.SharedLibraries != null) ids.Add(report.SharedLibraries.ClusterId);
        ids.AddRange(report.Clusters.Select(c => c.ClusterId));
        return ids;
    }

    private static object FormatCompactClusterSummary(WorkspaceClusterDto c)
    {
        var hasSubClusters = c.SubClusters is { Count: > 0 };
        return new
        {
            cluster_id = c.ClusterId,
            display_name = c.DisplayName,
            kind = c.Kind.ToString(),
            unit_count = c.UnitCount,
            project_count = c.ProjectCount,
            internal_weight = c.InternalWeight,
            languages = c.Languages,
            sub_clusters = hasSubClusters
                ? c.SubClusters!.Select(sc => new
                {
                    sub_cluster_id = sc.SubClusterId,
                    display_name = sc.DisplayName,
                    unit_count = sc.UnitCount,
                    project_count = sc.ProjectCount
                }).ToList()
                : null,
            sample_members = c.Members.Take(InlineClusterMemberPreviewCap).Select(m => m.Name).ToList(),
            truncated_members = Math.Max(0, c.Members.Count - InlineClusterMemberPreviewCap)
        };
    }
}
