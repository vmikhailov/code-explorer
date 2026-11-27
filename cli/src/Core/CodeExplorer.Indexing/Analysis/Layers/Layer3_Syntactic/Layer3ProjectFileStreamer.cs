using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser.Layers;

/// <summary>
/// Groups candidate files by enclosing project, streams/hashes source files on demand per project during Layer 3,
/// and prunes content-skipped minified bundles or broken symlinks without upward pipeline layer dependencies.
/// </summary>
internal static class Layer3ProjectFileStreamer
{
    private const string Layer1Stage = "Layer1_Physical";
    private const string IssueCategoryIoError = "io_error";
    private const string IssueFileReadError = "FileReadError";
    private const string SeverityWarning = "warning";
    private const char SlashChar = '/';

    public static void GroupFilesByEnclosingProject(
        Layer1Result l1Result,
        Layer2Result l2Result,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        Dictionary<string, FileNode> fileNodeByPath,
        List<Relationship> projectRels)
    {
        var hasNonScriptProject = false;
        var projectsByDir = new Dictionary<string, List<ProjectNode>>(l2Result.Projects.Count, StringComparer.OrdinalIgnoreCase);
        List<ProjectNode>? rootProjects = null;

        for (var i = 0; i < l2Result.Projects.Count; i++)
        {
            var proj = l2Result.Projects[i];
            if (proj.EntityKind != ProjectEntityKind.ScriptProject)
            {
                hasNonScriptProject = true;
            }

            if (string.IsNullOrEmpty(proj.Path))
            {
                rootProjects ??= [];
                rootProjects.Add(proj);
                continue;
            }

            var normProjPath = proj.Path.NormalizeSlashes().Trim(SlashChar);
            if (normProjPath.Length == 0 || normProjPath == ".")
            {
                rootProjects ??= [];
                rootProjects.Add(proj);
                continue;
            }

            if (!projectsByDir.TryGetValue(normProjPath, out var dirList))
            {
                dirList = [];
                projectsByDir[normProjPath] = dirList;
            }
            dirList.Add(proj);
        }

        var dirLookup = projectsByDir.GetAlternateLookup<ReadOnlySpan<char>>();
        var bestMatches = new List<ProjectNode>(4);

        foreach (var file in l1Result.Files)
        {
            var normFilePath = string.IsNullOrEmpty(file.Path)
                ? string.Empty
                : file.Path.NormalizeSlashes().Trim(SlashChar);
            if (normFilePath.Length > 0)
            {
                fileNodeByPath[normFilePath] = file;
            }

            bestMatches.Clear();
            var isManagedSourceExt = IsManagedSourceExtension(Path.GetExtension(file.Name));

            // 1. Exact single-file project match (matchLen = normProjPath.Length + 1)
            if (normFilePath.Length > 0 && dirLookup.TryGetValue(normFilePath.AsSpan(), out var exactDirProjs))
            {
                AppendEligibleProjects(exactDirProjs, hasNonScriptProject, isManagedSourceExt, bestMatches);
            }

            // 2. Deepest enclosing directory walk (right-to-left along '/', zero allocations)
            var probeSpan = normFilePath.AsSpan();
            while (bestMatches.Count == 0 && probeSpan.Length > 0)
            {
                var lastSlash = probeSpan.LastIndexOf(SlashChar);
                if (lastSlash <= 0)
                {
                    break;
                }
                probeSpan = probeSpan[..lastSlash];
                if (dirLookup.TryGetValue(probeSpan, out var parentDirProjs))
                {
                    AppendEligibleProjects(parentDirProjs, hasNonScriptProject, isManagedSourceExt, bestMatches);
                }
            }

            // 3. Workspace-root project fallback (matchLen = 0)
            if (bestMatches.Count == 0 && rootProjects != null)
            {
                AppendEligibleProjects(rootProjects, hasNonScriptProject, isManagedSourceExt, bestMatches);
            }

            if (bestMatches.Count > 0)
            {
                AssignFileToMatchedProjects(file, normFilePath, bestMatches, filesByProjectId, primaryOwnerByFilePath, projectRels);
            }
            else
            {
                file.IsTest = WorkspaceConventions.IsTestFilePath(file.Path);
            }
        }
    }

    private static void AppendEligibleProjects(
        List<ProjectNode> candidates,
        bool hasNonScriptProject,
        bool isManagedSourceExt,
        List<ProjectNode> destination)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var p = candidates[i];
            if (hasNonScriptProject && isManagedSourceExt && p.EntityKind == ProjectEntityKind.ScriptProject)
            {
                continue;
            }
            destination.Add(p);
        }
    }

    private static bool IsManagedSourceExtension(string ext) =>
        ext.ToLowerInvariant() is
            ".ts" or ".tsx" or ".mts" or ".cts" or ".js" or ".jsx" or ".mjs" or ".cjs" or
            ".cs" or ".java" or ".kt" or ".go" or ".rs" or ".php" or ".phtml" or ".inc" or ".pm" or ".pl";

    private static void AssignFileToMatchedProjects(
        FileNode file,
        string normFilePath,
        List<ProjectNode> bestMatches,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        List<Relationship> projectRels)
    {
        var primaryMatch = bestMatches[0];
        file.IsTest = primaryMatch.IsTest || WorkspaceConventions.IsTestFilePath(file.Path);
        file.ProjectKind = primaryMatch.EntityKind != ProjectEntityKind.Unknown ? primaryMatch.EntityKind : null;
        file.ProjectName = primaryMatch.Name;
        if (normFilePath.Length > 0)
        {
            primaryOwnerByFilePath[normFilePath] = primaryMatch;
        }

        if (!filesByProjectId.TryGetValue(primaryMatch.Id, out var primaryList))
        {
            primaryList = [];
            filesByProjectId[primaryMatch.Id] = primaryList;
        }
        primaryList.Add(file);

        for (var i = 0; i < bestMatches.Count; i++)
        {
            projectRels.Add(new Relationship(bestMatches[i].Id, file.Id, RelationshipKind.Contains));
        }
    }

    public static async ValueTask<bool> EnsureSourceFileStreamedAsync(
        FileNode file,
        Layer3Context ctx,
        ConcurrentDictionary<string, byte> prunedFileIds,
        CancellationToken cancellationToken)
    {
        if (file.LineCount.HasValue || file.PreloadedContent != null)
        {
            return true;
        }

        try
        {
            var sizeBytes = file.SizeBytes ?? 0L;
            var streamed = await FileStreamReader.StreamAndAnalyzeFileAsync(
                file.FullPath,
                file.Path,
                file.Name,
                sizeBytes,
                isManifest: false,
                isSupportedSourceOrConfig: true,
                ctx.FileFilter,
                cancellationToken,
                computeHash: ctx.PersistToGraphDb);

            if (streamed.SkipByContent)
            {
                if (file.LastModifiedUtc.HasValue)
                {
                    ctx.DiscoveredContentSkippedFiles[file.Path] = (file.FullPath, file.LastModifiedUtc.Value, sizeBytes);
                }
                prunedFileIds.TryAdd(file.Id, 0);
                return false;
            }

            file.LineCount = streamed.LineCount;
            if (streamed.ContentHash != null) file.ContentHash = streamed.ContentHash;
            if (streamed.ArtifactRole != null) file.ArtifactRole = streamed.ArtifactRole;
            file.PreloadedContent = streamed.DecodedText;
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            ctx.Layer1Log.SkippingBrokenSymlink(file.Path);
            ctx.DirectoryIndex.RemoveDirectoryFileEntry(file.FullPath);
            prunedFileIds.TryAdd(file.Id, 0);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(file.Path, ex);
            ctx.DirectoryIndex.RemoveDirectoryFileEntry(file.FullPath);
            prunedFileIds.TryAdd(file.Id, 0);
            ctx.RecordScanIssue(
                Layer1Stage,
                IssueCategoryIoError,
                file.Path,
                ex.Message,
                name: IssueFileReadError,
                severity: SeverityWarning,
                exception: ex,
                parentNodeId: null,
                isFileParent: false);
            return false;
        }
    }

    public static void PruneSkippedFilesAndEnqueueContains(
        Layer1Result l1Result,
        Dictionary<string, IOntologyNode> parentByChildId,
        Dictionary<string, FileNode> fileNodeByPath,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        List<Relationship> projectRels,
        ConcurrentDictionary<string, byte> prunedFileIds)
    {
        if (prunedFileIds.IsEmpty) return;

        var mutableFolders = l1Result.Folders;
        foreach (var fileId in prunedFileIds.Keys)
        {
            if (!parentByChildId.TryGetValue(fileId, out var parentNode)) continue;
            parentNode.Children.RemoveAll(c => c.Id == fileId);
            PruneEmptyAncestorFolders(parentNode, parentByChildId, mutableFolders);
        }

        var mutableFiles = l1Result.Files;
        for (var i = mutableFiles.Count - 1; i >= 0; i--)
        {
            var f = mutableFiles[i];
            if (!prunedFileIds.ContainsKey(f.Id)) continue;
            var normPath = f.Path.NormalizeSlashes().Trim(SlashChar);
            fileNodeByPath.Remove(normPath);
            primaryOwnerByFilePath.Remove(normPath);
            mutableFiles.RemoveAt(i);
        }

        projectRels.RemoveAll(r => prunedFileIds.ContainsKey(r.To));
    }

    private static void PruneEmptyAncestorFolders(
        IOntologyNode current,
        Dictionary<string, IOntologyNode> parentByChildId,
        List<FolderNode>? mutableFolders)
    {
        while (current is FolderNode folder &&
               folder.Children.Count == 0 &&
               parentByChildId.TryGetValue(folder.Id, out var grandParent))
        {
            grandParent.Children.Remove(folder);
            mutableFolders?.Remove(folder);
            current = grandParent;
        }
    }
}
