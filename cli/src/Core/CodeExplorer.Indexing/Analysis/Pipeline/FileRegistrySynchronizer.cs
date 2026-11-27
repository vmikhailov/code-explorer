using System.Collections.Concurrent;
using System.Diagnostics;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Parser.Incremental;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

internal sealed class FileRegistrySynchronizer(IGraphClient dbClient, FileRegistry fileRegistry)
{
    private const string SkippedContentHash = "__SKIPPED_CONTENT__";

    public static Dictionary<string, FileGraphSnapshot> PrecomputeSnapshots(IReadOnlyList<SyntaxTree> syntaxTrees)
    {
        var snapshots = new Dictionary<string, FileGraphSnapshot>(syntaxTrees.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < syntaxTrees.Count; i++)
        {
            var st = syntaxTrees[i];
            if (st.FileNode == null) continue;
            var normPath = st.FilePath.NormalizeSlashes();
            if (snapshots.ContainsKey(normPath)) continue;

            var relPath = !string.IsNullOrEmpty(st.FileNode.Path) ? st.FileNode.Path : st.RelativePath;
            var hash = st.FileNode.ContentHash ?? string.Empty;
            var lastMod = st.FileNode.LastModifiedUtc ?? DateTime.MinValue;
            snapshots[normPath] = FileSnapshotBuilder.BuildFromSyntaxTree(
                st,
                relPath,
                hash,
                lastMod,
                includeSemanticFootprint: false);
        }
        return snapshots;
    }

    public static void AppendProjectStructuralHashes(
        IReadOnlyList<SyntaxTree> projectTrees,
        Dictionary<string, string> structuralHashesByNormFullPath)
    {
        for (var i = 0; i < projectTrees.Count; i++)
        {
            var st = projectTrees[i];
            if (st.FileNode == null) continue;
            var normPath = st.FilePath.NormalizeSlashes();
            if (structuralHashesByNormFullPath.ContainsKey(normPath)) continue;

            var relPath = !string.IsNullOrEmpty(st.FileNode.Path) ? st.FileNode.Path : st.RelativePath;
            var hash = st.FileNode.ContentHash ?? string.Empty;
            var lastMod = st.FileNode.LastModifiedUtc ?? DateTime.MinValue;
            var snapshot = FileSnapshotBuilder.BuildFromSyntaxTree(
                st,
                relPath,
                hash,
                lastMod,
                includeSemanticFootprint: false);
            structuralHashesByNormFullPath[normPath] = snapshot.ComputeStructuralHash();
        }
    }

    public Task SyncWithHashesAsync(
        List<FileNode> files,
        Dictionary<string, string> precomputedStructuralHashes,
        WorkspaceScope scope,
        GraphWriteSink sink,
        Layer1Result l1,
        ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? skippedFiles,
        bool isIncremental) =>
        SyncCoreAsync(files, precomputedSnapshots: null, precomputedStructuralHashes, scope, sink, l1, skippedFiles, isIncremental);

    public Task SyncAsync(
        List<FileNode> files,
        Dictionary<string, FileGraphSnapshot> precomputedSnapshots,
        WorkspaceScope scope,
        GraphWriteSink sink,
        Layer1Result l1,
        ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? skippedFiles,
        bool isIncremental) =>
        SyncCoreAsync(files, precomputedSnapshots, precomputedStructuralHashes: null, scope, sink, l1, skippedFiles, isIncremental);

    private async Task SyncCoreAsync(
        List<FileNode> files,
        Dictionary<string, FileGraphSnapshot>? precomputedSnapshots,
        Dictionary<string, string>? precomputedStructuralHashes,
        WorkspaceScope scope,
        GraphWriteSink sink,
        Layer1Result l1,
        ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? skippedFiles,
        bool isIncremental)
    {
        var regSw = Stopwatch.StartNew();
        sink.PipelineLog.SyncingFileRegistry(files.Count);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = scope.CancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        var entries = new ConcurrentBag<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath, string? SnapshotJson)>();
        var seen = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            string hash;
            DateTime lastMod;
            var sizeBytes = file.SizeBytes ?? -1L;
            if (!string.IsNullOrEmpty(file.ContentHash) && file.LastModifiedUtc.HasValue)
            {
                hash = file.ContentHash;
                lastMod = file.LastModifiedUtc.Value;
            }
            else
            {
                try
                {
                    var fi = new FileInfo(file.FullPath);
                    if (!fi.Exists) return;
                    lastMod = fi.LastWriteTimeUtc;
                    sizeBytes = fi.Length;
                    hash = await HashUtility.ComputeSha256FromFileAsync(file.FullPath, ct);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return;
                }
            }
            seen.TryAdd(file.Path, 0);

            string? snapshotJson = null;
            FileGraphSnapshot? snapshot = null;

            var normalizedFullPath = file.FullPath.Replace('\\', '/');
            if (precomputedStructuralHashes != null &&
                precomputedStructuralHashes.TryGetValue(normalizedFullPath, out var precomputedHash))
            {
                snapshotJson = precomputedHash;
                snapshot = FileGraphSnapshot.FromStructuralHash(file.Path, hash, lastMod, precomputedHash);
            }
            else if (precomputedSnapshots != null &&
                     precomputedSnapshots.TryGetValue(normalizedFullPath, out var precomputed))
            {
                snapshot = (precomputed.ContentHash == hash && precomputed.LastModifiedUtc == lastMod)
                    ? precomputed
                    : precomputed with { ContentHash = hash, LastModifiedUtc = lastMod };
                snapshotJson = snapshot.ComputeStructuralHash();
            }

            fileRegistry.UpdateEntry(file.Path, hash, lastMod, file.Path, snapshot, sizeBytes);
            entries.Add((file.Path, hash, lastMod, sizeBytes, file.Path, snapshotJson));
        });

        if (!isIncremental)
        {
            var fileByRelPath = files
                .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var manifestFiles = l1.DiscoveredManifestFiles;
            var manifestPaths = l1.DiscoveredManifestPaths;
            var hasDiscoveredManifests = (manifestFiles is { Count: > 0 }) || (manifestPaths is { Count: > 0 });

            var candidateManifests = hasDiscoveredManifests
                ? (manifestFiles?.Keys ?? Enumerable.Empty<string>())
                    .Concat(manifestPaths?.Keys ?? Enumerable.Empty<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(f => scope.FileFilter.IsManifestFile(Path.GetFileName(f)))
                : WorkspaceCandidateEnumerator.EnumerateWithTrie(
                    scope.FileFilter,
                    scope.AbsoluteWorkspacePath,
                    scope.AbsoluteWorkspacePath,
                    new GitIgnoreTrieMatcher(scope.AbsoluteWorkspacePath, scope.FileFilter))
                    .Where(f => scope.FileFilter.IsManifestFile(Path.GetFileName(f)));

            foreach (var relPath in candidateManifests)
            {
                if (seen.ContainsKey(relPath)) continue;
                seen.TryAdd(relPath, 0);

                if (manifestFiles != null && manifestFiles.TryGetValue(relPath, out var discManifest))
                {
                    fileRegistry.UpdateEntry(relPath, discManifest.ContentHash, discManifest.LastModifiedUtc, relPath, sizeBytes: discManifest.SizeBytes);
                    entries.Add((relPath, discManifest.ContentHash, discManifest.LastModifiedUtc, discManifest.SizeBytes, relPath, null));
                    continue;
                }

                if (fileByRelPath.TryGetValue(relPath, out var existingFileNode) &&
                    !string.IsNullOrEmpty(existingFileNode.ContentHash) &&
                    existingFileNode.LastModifiedUtc.HasValue)
                {
                    var sz = existingFileNode.SizeBytes ?? -1L;
                    fileRegistry.UpdateEntry(relPath, existingFileNode.ContentHash, existingFileNode.LastModifiedUtc.Value, relPath, sizeBytes: sz);
                    entries.Add((relPath, existingFileNode.ContentHash, existingFileNode.LastModifiedUtc.Value, sz, relPath, null));
                    continue;
                }

                var manifestPath = Path.Combine(scope.AbsoluteWorkspacePath, relPath);
                var manifestInfo = new FileInfo(manifestPath);
                if (!manifestInfo.Exists) continue;

                var lastMod = manifestInfo.LastWriteTimeUtc;
                var manifestSize = manifestInfo.Length;
                var hash = await HashUtility.ComputeSha256FromFileAsync(manifestPath, scope.CancellationToken);
                fileRegistry.UpdateEntry(relPath, hash, lastMod, relPath, sizeBytes: manifestSize);
                entries.Add((relPath, hash, lastMod, manifestSize, relPath, null));
            }
        }

        if (skippedFiles != null)
        {
            foreach (var (relPath, skippedInfo) in skippedFiles)
            {
                if (seen.ContainsKey(relPath)) continue;
                seen.TryAdd(relPath, 0);
                fileRegistry.UpdateEntry(relPath, SkippedContentHash, skippedInfo.LastModifiedUtc, relPath, sizeBytes: skippedInfo.SizeBytes);
                entries.Add((relPath, SkippedContentHash, skippedInfo.LastModifiedUtc, skippedInfo.SizeBytes, relPath, null));
            }
        }

        sink.PipelineLog.PersistingFileRegistry(entries.Count);
        await dbClient.SaveFileRegistryEntriesAsync(entries, scope.CancellationToken);

        if (!scope.IsSubtreeScan && !isIncremental)
        {
            var staleKeys = fileRegistry.Entries.Select(e => e.Key).Where(k => !seen.ContainsKey(k)).ToList();
            if (staleKeys.Count > 0)
            {
                sink.PipelineLog.PruningFileRegistry(staleKeys.Count);
                foreach (var k in staleKeys)
                {
                    fileRegistry.RemoveEntry(k);
                }
                await dbClient.DeleteFileRegistryEntriesAsync(staleKeys, scope.CancellationToken);
            }
        }

        sink.PipelineLog.FileRegistrySynced(entries.Count, regSw.ElapsedMilliseconds);
    }
}
