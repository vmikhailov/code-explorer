using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Common;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public sealed class SliceCommandHandler : BaseCommandHandler
{
    private const string FormatJson = "json";
    private const string FormatYaml = "yaml";
    private const string CommaSep = ", ";
    private const string MissingSeedError = "Error: Provide at least one seed (e.g. 'ce slice plat-lui-portal --max-repos 8').";
    private const string Separator = "------------------------------------------------------------------------";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static async Task<int> HandleAsync(SliceOptions opts)
    {
        var seeds = opts.Seeds?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? [];
        if (seeds.Count == 0)
        {
            PrintError(MissingSeedError);
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var service = new WorkspaceSubgraphService();
        var report = await service.SliceSubgraphAsync(client, seeds, opts.MaxRepos, opts.Alpha);

        if (report.ResolvedSeedUnits.Count == 0)
        {
            PrintError($"No workspace unit, endpoint, or table matched seed(s): {string.Join(CommaSep, seeds)}");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(opts.WriteWorkspacePath))
        {
            var wsFile = await WriteVsCodeWorkspaceAsync(ws.RootDirectory, opts.WriteWorkspacePath, report.SelectedUnits);
            report.WorkspaceFileWritten = wsFile;
        }

        var fmt = (opts.Format ?? string.Empty).Trim().ToLowerInvariant();
        var rendered = fmt switch
        {
            FormatJson => JsonSerializer.Serialize(report, JsonOpts),
            FormatYaml => ToonYamlSerializer.SerializeObject(report),
            _ => RenderText(report)
        };

        Console.WriteLine(rendered);
        return 0;
    }

    private static async Task<string> WriteVsCodeWorkspaceAsync(
        string workspaceRoot,
        string outputFile,
        IReadOnlyList<SlicedUnitEntry> selected)
    {
        var fullOut = Path.GetFullPath(outputFile);
        var outDir = Path.GetDirectoryName(fullOut) ?? workspaceRoot;
        Directory.CreateDirectory(outDir);

        var folders = selected.Select(entry =>
        {
            var absUnitPath = string.IsNullOrEmpty(entry.Unit.RootPath)
                ? workspaceRoot
                : Path.Combine(workspaceRoot, entry.Unit.RootPath);
            var rel = Path.GetRelativePath(outDir, absUnitPath).Replace('\\', '/');
            if (string.IsNullOrEmpty(rel)) rel = ".";
            return new Dictionary<string, string>
            {
                ["name"] = entry.Unit.Name,
                ["path"] = rel
            };
        }).ToList();

        var doc = new Dictionary<string, object>
        {
            ["folders"] = folders,
            ["settings"] = new Dictionary<string, object>()
        };

        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fullOut, json);
        return fullOut;
    }

    private static string RenderText(WorkspaceSliceReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Task-Focused Subgraph Slice (PPR alpha={r.Alpha:0.##}, max={r.MaxRepos}, {r.ElapsedMs:0.##} ms)");
        sb.AppendLine(Separator);
        sb.AppendLine($"Seeds:    {string.Join(CommaSep, r.RequestedSeeds)} -> [{string.Join(CommaSep, r.ResolvedSeedUnits)}]");
        sb.AppendLine($"Selected: {r.SelectedUnits.Count} unit(s)");
        sb.AppendLine();

        for (var i = 0; i < r.SelectedUnits.Count; i++)
        {
            var u = r.SelectedUnits[i];
            var tag = u.IsSeed ? "[SEED]" : $"[hop {u.HopDistance}]";
            sb.AppendLine($"  {i + 1,2}. {u.Unit.Name} ({u.Unit.PrimaryLanguage}, {u.Unit.MemberProjectCount} proj)  ppr={u.Score:0.0000}  {tag}");
            foreach (var reason in u.ConnectionReasons.Take(3))
            {
                sb.AppendLine($"        via {reason}");
            }
        }

        if (!string.IsNullOrEmpty(r.WorkspaceFileWritten))
        {
            sb.AppendLine();
            sb.AppendLine($"VS Code workspace written to: {r.WorkspaceFileWritten}");
        }

        return sb.ToString().TrimEnd();
    }
}
