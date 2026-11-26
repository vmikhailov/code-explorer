using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Common;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public sealed class DomainsCommandHandler : BaseCommandHandler
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

    public static async Task<int> HandleAsync(DomainsOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var service = new WorkspaceSubgraphService();
        var report = await service.PartitionDomainsAsync(client, opts.Resolution, opts.HubThreshold);

        var fmt = (opts.Format ?? string.Empty).Trim().ToLowerInvariant();
        var output = fmt switch
        {
            FormatJson => JsonSerializer.Serialize(report, JsonOpts),
            FormatYaml => ToonYamlSerializer.SerializeObject(report),
            FormatMarkdown or FormatMd => RenderMarkdown(report),
            _ => RenderText(report)
        };

        if (!string.IsNullOrWhiteSpace(opts.OutputPath))
        {
            var fullPath = Path.GetFullPath(opts.OutputPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            await File.WriteAllTextAsync(fullPath, output);
            PrintSuccess($"Domain partition report written to {fullPath}");
        }
        else
        {
            Console.WriteLine(output);
        }

        return 0;
    }

    private static string RenderText(WorkspaceDomainPartitionReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Workspace Domain Partitioning (Leiden, gamma={r.Resolution:0.##}, {r.ElapsedMs:0.##} ms)");
        sb.AppendLine(Separator);
        sb.AppendLine($"Coarsened: {r.RawProjectCount} project(s) -> {r.CoarsenedUnitCount} root unit(s)");
        sb.AppendLine($"Summary:   {r.Domains.Count} domain(s), {r.PlatformHubs.Count} platform hub(s), {r.StandaloneUnits.Count} standalone unit(s)");

        if (r.PlatformHubs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Shared Platform / Commons Hubs ({r.PlatformHubs.Count}):");
            foreach (var h in r.PlatformHubs)
            {
                sb.AppendLine($"  - {h.Unit.Name} [{h.Unit.PrimaryLanguage}] (depended on by {h.InboundDependentUnits} units / {h.InboundRatio:P0})");
            }
        }

        if (r.Domains.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Detected Domains ({r.Domains.Count}):");
            foreach (var d in r.Domains)
            {
                var langs = string.Join(CommaSep, d.Languages);
                sb.AppendLine($"  [{d.Name}] ({d.UnitCount} units, {d.ProjectCount} projects, langs: {langs}, cohesion: {d.InternalWeight:0.##})");
                foreach (var m in d.Members)
                {
                    sb.AppendLine($"    - {m.Name} ({m.PrimaryLanguage}, {m.MemberProjectCount} proj)");
                }
            }
        }

        if (r.BoundaryEdges.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Inter-Domain Boundary Edges ({r.BoundaryEdges.Count}):");
            foreach (var b in r.BoundaryEdges.Take(20))
            {
                var sample = b.SampleReasons.Count > 0 ? $" [{b.SampleReasons[0]}]" : string.Empty;
                sb.AppendLine($"  - {b.FromDomain} -> {b.ToDomain} (weight={b.Weight:0.##}, edges={b.CrossEdgeCount}){sample}");
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

    private static string RenderMarkdown(WorkspaceDomainPartitionReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Workspace Domain Partitioning");
        sb.AppendLine();
        sb.AppendLine($"- **Coarsening**: `{r.RawProjectCount}` projects -> `{r.CoarsenedUnitCount}` root units (`{r.ElapsedMs:0.##} ms`)");
        sb.AppendLine($"- **Parameters**: Leiden `resolution = {r.Resolution:0.##}`, `hub_threshold = {r.HubThreshold:P0}`");
        sb.AppendLine();

        if (r.PlatformHubs.Count > 0)
        {
            sb.AppendLine("## Shared Platform / Commons Hubs");
            foreach (var h in r.PlatformHubs)
            {
                sb.AppendLine($"- **`{h.Unit.Name}`** (`{h.Unit.PrimaryLanguage}`) - inbound from `{h.InboundDependentUnits}` units (`{h.InboundRatio:P0}`)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Domains");
        foreach (var d in r.Domains)
        {
            sb.AppendLine($"### `{d.Name}` ({d.UnitCount} units, {d.ProjectCount} projects)");
            foreach (var m in d.Members)
            {
                sb.AppendLine($"- `{m.Name}` (`{m.PrimaryLanguage}`, {m.MemberProjectCount} project(s))");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
