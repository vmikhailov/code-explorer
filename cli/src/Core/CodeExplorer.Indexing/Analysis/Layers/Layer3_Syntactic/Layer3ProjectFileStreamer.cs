using System.Collections.Concurrent;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;

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
        Layer2Result l2Result,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        Dictionary<string, FileNode> fileNodeByPath,
        List<Relationship> projectRels)
    {
        var hasNonScriptProject = l2Result.Projects.Any(static pr => pr.EntityKind != ProjectEntityKind.ScriptProject);
        var bestMatches = new List<ProjectNode>();

        foreach (var file in l2Result.Prev.Files)
        {
            if (!string.IsNullOrEmpty(file.Path))
            {
                fileNodeByPath[file.Path.NormalizeSlashes().Trim(SlashChar)] = file;
            }

            bestMatches.Clear();
            var bestMatchLength = -1;
            var isManagedSourceExt = IsManagedSourceExtension(Path.GetExtension(file.Name));

            foreach (var p in l2Result.Projects)
            {
                if (hasNonScriptProject && isManagedSourceExt && p.EntityKind == ProjectEntityKind.ScriptProject)
                {
                    continue;
                }

                var matchLen = ComputeProjectMatchLength(file.Path, p.Path);
                if (matchLen < 0) continue;

                if (matchLen > bestMatchLength)
                {
                    bestMatchLength = matchLen;
                    bestMatches.Clear();
                    bestMatches.Add(p);
                }
                else if (matchLen == bestMatchLength)
                {
                    bestMatches.Add(p);
                }
            }

            if (bestMatches.Count > 0)
            {
                AssignFileToMatchedProjects(file, bestMatches, filesByProjectId, primaryOwnerByFilePath, projectRels);
            }
            else
            {
                file.IsTest = WorkspaceConventions.IsTestFilePath(file.Path);
            }
        }
    }

    private static bool IsManagedSourceExtension(string ext) =>
        ext.ToLowerInvariant() is
            ".ts" or ".tsx" or ".mts" or ".cts" or ".js" or ".jsx" or ".mjs" or ".cjs" or
            ".cs" or ".java" or ".kt" or ".go" or ".rs" or ".php" or ".phtml" or ".inc" or ".pm" or ".pl";

    private static int ComputeProjectMatchLength(string filePath, string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath)) return 0;

        var normProjPath = projectPath.NormalizeSlashes().Trim(SlashChar);
        if (filePath.Equals(normProjPath, StringComparison.OrdinalIgnoreCase))
        {
            return normProjPath.Length + 1;
        }

        var prefix = normProjPath + SlashChar;
        return filePath.StartWithIC(prefix) ? prefix.Length : -1;
    }

    private static void AssignFileToMatchedProjects(
        FileNode file,
        List<ProjectNode> bestMatches,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        List<Relationship> projectRels)
    {
        var primaryMatch = bestMatches[0];
        file.IsTest = primaryMatch.IsTest || WorkspaceConventions.IsTestFilePath(file.Path);
        file.ProjectKind = primaryMatch.EntityKind != ProjectEntityKind.Unknown ? primaryMatch.EntityKind : null;
        file.ProjectName = primaryMatch.Name;
        if (!string.IsNullOrEmpty(file.Path))
        {
            primaryOwnerByFilePath[file.Path.NormalizeSlashes().Trim(SlashChar)] = primaryMatch;
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
        ParsingContext ctx,
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
            var streamed = await PhysicalFileStore.StreamAndAnalyzeFileAsync(
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
                PhysicalFileStore.EvictFileText(file.FullPath);
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
            PhysicalFileStore.RemoveCachedFile(file.FullPath);
            prunedFileIds.TryAdd(file.Id, 0);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Layer1Log.FileReadFailed(file.Path, ex);
            PhysicalFileStore.RemoveCachedFile(file.FullPath);
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
                isFileParent: false,
                logToConsole: false);
            return false;
        }
    }

    public static void PruneSkippedFilesAndEnqueueContains(
        Layer2Result l2Result,
        Dictionary<string, IOntologyNode> parentByChildId,
        Dictionary<string, FileNode> fileNodeByPath,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        List<Relationship> projectRels,
        ConcurrentDictionary<string, byte> prunedFileIds)
    {
        if (prunedFileIds.IsEmpty) return;

        var mutableFolders = l2Result.Prev.Folders as List<FolderNode>;
        foreach (var fileId in prunedFileIds.Keys)
        {
            if (!parentByChildId.TryGetValue(fileId, out var parentNode)) continue;
            parentNode.Children.RemoveAll(c => c.Id == fileId);
            PruneEmptyAncestorFolders(parentNode, parentByChildId, mutableFolders);
        }

        if (l2Result.Prev.Files is List<FileNode> mutableFiles)
        {
            for (var i = mutableFiles.Count - 1; i >= 0; i--)
            {
                var f = mutableFiles[i];
                if (!prunedFileIds.ContainsKey(f.Id)) continue;
                var normPath = f.Path.NormalizeSlashes().Trim(SlashChar);
                fileNodeByPath.Remove(normPath);
                primaryOwnerByFilePath.Remove(normPath);
                mutableFiles.RemoveAt(i);
            }
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
