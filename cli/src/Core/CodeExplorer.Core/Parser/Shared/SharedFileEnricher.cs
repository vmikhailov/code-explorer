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
    private const string Layer2Stage = "Layer2_Boundaries";
    private const string ConfigParseErrorCode = "config_parse_error";
    private const string SharedFileEnrichErrorName = "SharedFileEnrichError";

    public static async Task EnrichAsync(IReadOnlyList<FileNode> files, IReadOnlyList<ProjectNode> projects, Layer2Context ctx)
    {
        ctx.WorkspaceEnrichments.Clear();
        foreach (var project in projects) project.Enrichments.Clear();

        var candidates = files.Where(f => SharedFileParserRegistry.CanParse(f.Name)).ToList();
        if (candidates.Count == 0) return;

        var parsed = new ConcurrentBag<(FileNode File, IReadOnlyList<IProjectEnrichment> Results)>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ctx.CancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        async ValueTask body(FileNode file, CancellationToken ct)
        {
            try
            {
                if (file.SizeBytes <= 0 || file.SizeBytes > SharedFileParserRegistry.MaxFileSizeBytes) return;
                var fileText = file.PreloadedContent;
                if (fileText == null)
                {
                    var streamed = await FileStreamReader.StreamAndAnalyzeFileAsync(
                        file.FullPath,
                        file.Path,
                        file.Name,
                        file.SizeBytes ?? 0L,
                        isManifest: false,
                        isSupportedSourceOrConfig: true,
                        ctx.FileFilter,
                        ct,
                        computeHash: ctx.PersistToGraphDb);
                    if (streamed.SkipByContent)
                    {
                        return;
                    }
                    file.LineCount = streamed.LineCount;
                    if (streamed.ContentHash != null) file.ContentHash = streamed.ContentHash;
                    if (streamed.ArtifactRole != null) file.ArtifactRole = streamed.ArtifactRole;
                    file.PreloadedContent = streamed.DecodedText;
                    fileText = streamed.DecodedText;
                }
                if (string.IsNullOrEmpty(fileText)) return;

                var results = SharedFileParserRegistry.Parse(file.FullPath, fileText);
                if (results.Count > 0) parsed.Add((file, results));
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                ctx.Layer1Log.SkippingBrokenSymlink(file.Path);
                ctx.DirectoryIndex.RemoveDirectoryFileEntry(file.FullPath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.Layer2Log.SharedFileEnrichFailed(file.Path, ex);
                ctx.RecordScanIssue(Layer2Stage, ConfigParseErrorCode, file.Path, ex.Message, name: SharedFileEnrichErrorName, exception: ex, parentNodeId: file.Id, isFileParent: true);
            }
        }
        await Parallel.ForEachAsync(candidates, parallelOptions, body);

        var normalizedProjects = projects
            .Select(p => (Project: p, Prefix: string.IsNullOrEmpty(p.Path) ? "" : p.Path.Replace('\\', '/').TrimEnd('/') + "/"))
            .ToArray();

        foreach (var (file, results) in parsed.OrderBy(p => p.File.Path, StringComparer.Ordinal))
        {
            var owner = FindOwner(file, normalizedProjects);
            if (owner != null) owner.Enrichments.AddRange(results);
            else ctx.WorkspaceEnrichments.AddRange(results);
        }
    }

    private static ProjectNode? FindOwner(
        FileNode file,
        (ProjectNode Project, string Prefix)[] normalizedProjects)
    {
        ProjectNode? best = null;
        var bestLength = -1;
        foreach (var (p, prefix) in normalizedProjects)
        {
            if (prefix.Length == 0)
            {
                var isRootScriptOrOrchestrator =
                    p.IsBuildOrchestrator == true ||
                    p.EntityKind == ProjectEntityKind.ScriptProject ||
                    p.ProjectType is ProjectLanguage.Script or ProjectLanguage.Batch or ProjectLanguage.PowerShell or ProjectLanguage.Shell;
                if (isRootScriptOrOrchestrator && file.Path.Contains('/'))
                {
                    continue;
                }
                if (bestLength < 0)
                {
                    best = p;
                    bestLength = 0;
                }
                continue;
            }

            if (file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > bestLength)
            {
                best = p;
                bestLength = prefix.Length;
            }
        }

        return best;
    }
}
