using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public sealed class ClustersCommandHandler : BaseCommandHandler
{
    private const string FormatJson = "json";
    private const string FormatYaml = "yaml";
    private const string FormatMarkdown = "markdown";
    private const string FormatMd = "md";
    private const string CommaSep = ", ";
    private const string Separator = "------------------------------------------------------------------------";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<int> HandleAsync(ClustersOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var service = new WorkspaceSubgraphService();
        var report = await service.PartitionClustersAsync(client, opts.Resolution, opts.HubThreshold, ws.RootDirectory);

        var fmt = (opts.Format ?? string.Empty).Trim().ToLowerInvariant();
        string output;
        if (!string.IsNullOrWhiteSpace(opts.Cluster))
        {
            var expanded = CodeExplorerRepository.BuildClusterDrillDownPayload(report, opts.Cluster.Trim());
            if (expanded == null)
            {
                PrintError($"Cluster '{opts.Cluster}' not found.");
                return 1;
            }
            output = fmt switch
            {
                FormatJson => JsonSerializer.Serialize(expanded, JsonOpts),
                FormatYaml => ToonYamlSerializer.SerializeObject(expanded),
                _ => ToonYamlSerializer.SerializeObject(expanded)
            };
        }
        else
        {
            output = fmt switch
            {
                FormatJson => JsonSerializer.Serialize(report, JsonOpts),
                FormatYaml => ToonYamlSerializer.SerializeObject(report),
                FormatMarkdown or FormatMd => RenderMarkdown(report),
                _ => RenderText(report)
            };
        }

        if (!string.IsNullOrWhiteSpace(opts.OutputPath))
        {
            var fullPath = Path.GetFullPath(opts.OutputPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            await File.WriteAllTextAsync(fullPath, output);
            PrintSuccess($"Cluster partition report written to {fullPath}");
        }
        else
        {
            Console.WriteLine(output);
        }

        return 0;
    }

    private static string RenderText(WorkspaceClusterPartitionReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Workspace Cluster Partitioning (Leiden, gamma={r.Resolution:0.##}, {r.ElapsedMs:0.##} ms)");
        sb.AppendLine(Separator);
        sb.AppendLine($"Coarsened: {r.RawProjectCount} project(s) -> {r.CoarsenedUnitCount} root unit(s)");
        var sharedCount = r.SharedLibraries?.UnitCount ?? 0;
        sb.AppendLine($"Summary:   {r.Clusters.Count} cluster(s), {sharedCount} shared library unit(s), {r.StandaloneUnits.Count} standalone unit(s)");

        if (r.SharedLibraries != null && r.SharedLibraries.Members.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Shared Libraries [{r.SharedLibraries.ClusterId}] ({r.SharedLibraries.UnitCount} units, {r.SharedLibraries.ProjectCount} projects):");
            foreach (var m in r.SharedLibraries.Members.Take(20))
            {
                sb.AppendLine($"  - {m.Name} ({m.PrimaryLanguage}, {m.MemberProjectCount} proj)");
            }
            if (r.SharedLibraries.Members.Count > 20)
            {
                sb.AppendLine($"  ... +{r.SharedLibraries.Members.Count - 20} more (run 'ce clusters -c {r.SharedLibraries.ClusterId}' to expand)");
            }
        }

        if (r.Clusters.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Detected Clusters ({r.Clusters.Count}):");
            foreach (var c in r.Clusters)
            {
                var langs = string.Join(CommaSep, c.Languages);
                sb.AppendLine($"  [{c.ClusterId}] {c.DisplayName} [{c.Kind}] ({c.UnitCount} units, {c.ProjectCount} projects, langs: {langs}, cohesion: {c.InternalWeight:0.##})");
                if (c.SubClusters is { Count: > 0 })
                {
                    foreach (var sc in c.SubClusters)
                    {
                        sb.AppendLine($"    * sub-cluster [{sc.SubClusterId}] {sc.DisplayName} ({sc.UnitCount} units, {sc.ProjectCount} proj)");
                    }
                }
                else
                {
                    foreach (var m in c.Members)
                    {
                        sb.AppendLine($"    - {m.Name} ({m.PrimaryLanguage}, {m.MemberProjectCount} proj)");
                    }
                }
            }
        }

        if (r.BoundaryEdges.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Inter-Cluster Boundary Edges ({r.BoundaryEdges.Count}):");
            foreach (var b in r.BoundaryEdges.Take(25))
            {
                var sample = b.SampleReasons.Count > 0 ? $" [{b.SampleReasons[0]}]" : string.Empty;
                sb.AppendLine($"  - {b.FromCluster} -> {b.ToCluster} (weight={b.Weight:0.##}, edges={b.CrossEdgeCount}){sample}");
            }
        }

        if (r.StandaloneUnits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Standalone / Isolated Units ({r.StandaloneUnits.Count}):");
            sb.AppendLine($"  {string.Join(CommaSep, r.StandaloneUnits.Select(u => $"{u.Name} ({u.PrimaryLanguage})"))}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string RenderMarkdown(WorkspaceClusterPartitionReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Workspace Cluster Partitioning");
        sb.AppendLine();
        sb.AppendLine($"- **Coarsening**: `{r.RawProjectCount}` projects -> `{r.CoarsenedUnitCount}` root units (`{r.ElapsedMs:0.##} ms`)");
        sb.AppendLine($"- **Parameters**: Leiden `resolution = {r.Resolution:0.##}`, `hub_threshold = {r.HubThreshold:P0}`");
        sb.AppendLine();

        if (r.SharedLibraries != null)
        {
            sb.AppendLine($"## Shared Libraries (`{r.SharedLibraries.ClusterId}` — {r.SharedLibraries.UnitCount} units)");
            foreach (var m in r.SharedLibraries.Members)
            {
                sb.AppendLine($"- `{m.Name}` ({m.PrimaryLanguage}, `{m.MemberProjectCount}` projects)");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"## Clusters ({r.Clusters.Count})");
        foreach (var c in r.Clusters)
        {
            sb.AppendLine($"### `{c.ClusterId}` — {c.DisplayName} (`{c.Kind}`, {c.UnitCount} units, {c.ProjectCount} projects)");
            foreach (var m in c.Members.Take(30))
            {
                sb.AppendLine($"- `{m.Name}` ({m.PrimaryLanguage}, {m.MemberProjectCount} proj)");
            }
            if (c.Members.Count > 30)
            {
                sb.AppendLine($"- *+{c.Members.Count - 30} additional units*");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
