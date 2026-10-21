using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Reads every file that a registered <see cref="ISharedFileParser"/> understands exactly once,
/// and attaches the parsed results to the owning project (or to the workspace for root files).
/// </summary>
public static class SharedFileEnricher
{
    public static void Enrich(IReadOnlyList<FileNode> files, IReadOnlyList<ProjectNode> projects, ParsingContext ctx)
    {
        ctx.WorkspaceEnrichments.Clear();
        foreach (var project in projects) project.Enrichments.Clear();

        var candidates = files.Where(f => SharedFileParserRegistry.CanParse(f.Name)).ToList();
        if (candidates.Count == 0) return;

        var parsed = new ConcurrentBag<(FileNode File, IReadOnlyList<IProjectEnrichment> Results)>();
        Parallel.ForEach(candidates, new ParallelOptions { CancellationToken = ctx.CancellationToken }, file =>
        {
            try
            {
                var info = new FileInfo(file.FullPath);
                if (!info.Exists || info.Length > SharedFileParserRegistry.MaxFileSizeBytes) return;

                var results = SharedFileParserRegistry.Parse(file.FullPath, File.ReadAllText(file.FullPath));
                if (results.Count > 0) parsed.Add((file, results));
            }
            catch (Exception ex)
            {
                ctx.LogWarning($"[SharedFileEnricher] Failed to read '{file.FullPath}': {ex.Message}");
            }
        });

        foreach (var (file, results) in parsed.OrderBy(p => p.File.Path, StringComparer.Ordinal))
        {
            var owner = FindOwner(file, projects);
            if (owner != null) owner.Enrichments.AddRange(results);
            else ctx.WorkspaceEnrichments.AddRange(results);
        }
    }

    private static ProjectNode? FindOwner(FileNode file, IReadOnlyList<ProjectNode> projects)
    {
        ProjectNode? best = null;
        var bestLength = -1;
        foreach (var p in projects)
        {
            if (string.IsNullOrEmpty(p.Path))
            {
                if (bestLength < 0)
                {
                    best = p;
                    bestLength = 0;
                }
                continue;
            }

            var prefix = p.Path.Replace('\\', '/').TrimEnd('/') + "/";
            if (file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > bestLength)
            {
                best = p;
                bestLength = prefix.Length;
            }
        }

        return best;
    }
}
