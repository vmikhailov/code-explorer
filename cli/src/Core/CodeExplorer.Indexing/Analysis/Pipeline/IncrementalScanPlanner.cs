using System.Diagnostics;
using CodeExplorer.Core.Parser.Incremental;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Parser;

internal sealed class IncrementalScanPlanner(
    IGraphClient dbClient,
    FileRegistry fileRegistry,
    ILogger logger)
{
    private const string SkippedContentHash = "__SKIPPED_CONTENT__";

    public async Task<IndexScanPlan> PlanAsync(
        string absoluteWorkspacePath,
        string absoluteTargetPath,
        string? scanPath,
        bool forceClear,
        WorkspaceDetectorCatalog detectorCatalog,
        WorkspaceFileFilter filter,
        IReadOnlyCollection<string>? knownChangedFiles,
        Stopwatch sw,
        CancellationToken ct)
    {
        if (forceClear)
        {
            return new IndexScanPlan(IndexExecutionMode.Full, ClearDatabase: true);
        }

        if (fileRegistry.Count == 0)
        {
            await fileRegistry.LoadAsync(dbClient, ct);
        }

        if (fileRegistry.Count == 0)
        {
            logger.LogInformation("[Incremental:Decision] Full rescan triggered: File registry in database is empty (workspace not indexed yet).");
            return new IndexScanPlan(IndexExecutionMode.Full, ClearDatabase: false);
        }

        if (dbClient.DbPath != null)
        {
            var recordedDetectorsHash = await dbClient.GetMetadataAsync(WorkspaceIndexer.DetectorsHashMetadataKey, ct) ?? "none";
            if (!string.Equals(recordedDetectorsHash, detectorCatalog.CombinedHash, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "[Incremental:Decision] Full rescan triggered: Custom YAML detectors changed ({RecordedHash} -> {CurrentHash}).",
                    recordedDetectorsHash,
                    detectorCatalog.CombinedHash);
                return new IndexScanPlan(IndexExecutionMode.Full, ClearDatabase: true);
            }
        }

        if (dbClient.DbPath != null && knownChangedFiles == null)
        {
            var recordedVersion = await dbClient.GetMetadataAsync(WorkspaceIndexer.IndexerVersionMetadataKey, ct);
            if (!string.Equals(recordedVersion, WorkspaceIndexer.CurrentIndexerVersion, StringComparison.Ordinal))
            {
                logger.LogInformation(
                    "[Incremental:Decision] Full rescan triggered: Indexer version changed ({RecordedVersion} -> {CurrentVersion}).",
                    recordedVersion ?? "legacy",
                    WorkspaceIndexer.CurrentIndexerVersion);
                return new IndexScanPlan(IndexExecutionMode.Full, ClearDatabase: true);
            }
        }

        var gitTrie = new GitIgnoreTrieMatcher(absoluteWorkspacePath, filter);
        var targetSubPath = scanPath == null
            ? null
            : Path.GetRelativePath(absoluteWorkspacePath, absoluteTargetPath).Replace('\\', '/');

        FileChangeset changeset;
        if (knownChangedFiles is { Count: > 0 })
        {
            var filteredPaths = knownChangedFiles
                .Select(p => p.Replace('\\', '/').TrimStart('/'))
                .Where(p => !string.IsNullOrWhiteSpace(p) && !gitTrie.IsIgnored(p, false))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            changeset = fileRegistry.ComputeChangesetForKnownPaths(absoluteWorkspacePath, filteredPaths);
        }
        else
        {
            var allFiles = WorkspaceCandidateEnumerator.EnumerateWithTrie(
                filter,
                absoluteTargetPath,
                absoluteWorkspacePath,
                gitTrie,
                msg => logger.LogInformation("{Msg}", msg));
            changeset = fileRegistry.ComputeChangeset(absoluteWorkspacePath, allFiles, targetSubPath);
        }

        if (changeset.IsEmpty)
        {
            logger.LogInformation("[Incremental] Workspace is up to date (0 changes detected) in {ElapsedMs:N0}ms.", sw.ElapsedMilliseconds);
            return new IndexScanPlan(IndexExecutionMode.UpToDate, ClearDatabase: false);
        }

        logger.LogInformation(
            "[Incremental] Changes detected: {AddedCount} added, {ModifiedCount} modified, {DeletedCount} deleted.",
            changeset.Added.Count,
            changeset.Modified.Count,
            changeset.Deleted.Count);

        var changedManifests = changeset.Added.Concat(changeset.Modified).Concat(changeset.Deleted)
            .Where(f => filter.IsManifestFile(Path.GetFileName(f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (changedManifests.Count > 0)
        {
            var manifestDesc = changedManifests.Count <= 3
                ? string.Join(", ", changedManifests)
                : $"{string.Join(", ", changedManifests.Take(3))} (+{changedManifests.Count - 3} more)";
            var hasDeletedManifests = changeset.Deleted.Any(f => filter.IsManifestFile(Path.GetFileName(f)));
            var shouldClear = hasDeletedManifests && targetSubPath == null;
            logger.LogInformation(
                "[Incremental:Decision] Full rescan triggered: Project manifest modified ({Manifests}). Reindexing workspace structure (clear={Clear})...",
                manifestDesc,
                shouldClear);
            return new IndexScanPlan(IndexExecutionMode.Full, ClearDatabase: shouldClear);
        }

        if (changeset.Deleted.Count > 0)
        {
            var filesDesc = changeset.Deleted.Count <= 3
                ? string.Join(", ", changeset.Deleted)
                : $"{string.Join(", ", changeset.Deleted.Take(3))} (+{changeset.Deleted.Count - 3} more)";
            logger.LogInformation(
                "[Incremental] Pruning {Count} deleted file(s) from graph and registry: {Files}",
                changeset.Deleted.Count,
                filesDesc);
            await dbClient.DeleteFileRegistryEntriesAsync(changeset.Deleted, ct);
            foreach (var del in changeset.Deleted)
            {
                await dbClient.ClearWorkspaceAsync(del);
                fileRegistry.RemoveEntry(del);
            }
        }

        var workspaceId = await dbClient.GetOrCreateWorkspaceIdAsync(absoluteWorkspacePath);
        var filesToUpdateRegistry = new List<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath, string? SnapshotJson)>();
        var structuralReasons = new List<string>();
        var modifiedWithStructuralChanges = new List<string>();
        var effectiveAdded = new List<string>();

        foreach (var relPath in changeset.Added)
        {
            var fileInfo = new FileInfo(Path.Combine(absoluteWorkspacePath, relPath));
            if (!fileInfo.Exists) continue;
            var normalizedFullPath = fileInfo.FullName.NormalizeSlashes();
            var streamed = await FileStreamReader.StreamAndAnalyzeFileAsync(
                normalizedFullPath,
                relPath,
                fileInfo.Name,
                fileInfo.Length,
                isManifest: false,
                isSupportedSourceOrConfig: true,
                filter,
                ct,
                computeHash: false);
            if (streamed.SkipByContent)
            {
                fileRegistry.UpdateEntry(relPath, SkippedContentHash, fileInfo.LastWriteTimeUtc, relPath, sizeBytes: fileInfo.Length);
                filesToUpdateRegistry.Add((relPath, SkippedContentHash, fileInfo.LastWriteTimeUtc, fileInfo.Length, relPath, null));
            }
            else
            {
                effectiveAdded.Add(relPath);
            }
        }

        if (effectiveAdded.Count > 0)
        {
            var addedDesc = effectiveAdded.Count <= 3
                ? string.Join(", ", effectiveAdded)
                : $"{string.Join(", ", effectiveAdded.Take(3))} (+{effectiveAdded.Count - 3} more)";
            var addedReason = $"New file(s) added ({effectiveAdded.Count}): [{addedDesc}]";
            logger.LogInformation("[Incremental:Decision] Structural change detected: {Reason}", addedReason);
            structuralReasons.Add(addedReason);
        }

        foreach (var relPath in changeset.Modified)
        {
            await EvaluateModifiedFileAsync(
                relPath,
                absoluteWorkspacePath,
                workspaceId,
                filter,
                filesToUpdateRegistry,
                structuralReasons,
                modifiedWithStructuralChanges,
                ct);
        }

        if (filesToUpdateRegistry.Count > 0)
        {
            await dbClient.SaveFileRegistryEntriesAsync(filesToUpdateRegistry, ct);
        }

        if (structuralReasons.Count == 0)
        {
            if (changeset.Deleted.Count == 0 && filesToUpdateRegistry.Count == 0)
            {
                logger.LogInformation("[Incremental] Workspace is up to date (0 effective changes detected) in {ElapsedMs:N0}ms.", sw.ElapsedMilliseconds);
                return new IndexScanPlan(IndexExecutionMode.UpToDate, ClearDatabase: false);
            }

            logger.LogInformation("[Incremental:Decision] All modifications were logic-only; graph remains unchanged ({ElapsedMs:N0}ms).", sw.ElapsedMilliseconds);
            return new IndexScanPlan(IndexExecutionMode.LogicOnly, ClearDatabase: false);
        }

        if (modifiedWithStructuralChanges.Count > 0)
        {
            logger.LogInformation(
                "[Incremental] Removing stale graph nodes for {Count} updated file(s)...",
                modifiedWithStructuralChanges.Count);

            foreach (var mod in modifiedWithStructuralChanges)
            {
                await dbClient.ClearWorkspaceAsync(mod);
            }
        }

        logger.LogInformation(
            "[Incremental:Decision] Executing graph update due to {Count} structural trigger(s)...",
            structuralReasons.Count);
        logger.LogDebug(
            "[Incremental:Decision] Structural triggers:\n  * {Reasons}",
            string.Join("\n  * ", structuralReasons));

        var structuralFiles = new HashSet<string>(
            effectiveAdded.Concat(modifiedWithStructuralChanges).Select(p => p.NormalizeSlashes()),
            StringComparer.OrdinalIgnoreCase);

        var relevantDirs = BuildRelevantDirectories(structuralFiles);
        return new IndexScanPlan(
            IndexExecutionMode.Incremental,
            ClearDatabase: false,
            IncrementalChangedFiles: structuralFiles,
            IncrementalRelevantDirectories: relevantDirs);
    }

    private async Task EvaluateModifiedFileAsync(
        string relPath,
        string absoluteWorkspacePath,
        string workspaceId,
        WorkspaceFileFilter filter,
        List<(string RelativePath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath, string? SnapshotJson)> filesToUpdateRegistry,
        List<string> structuralReasons,
        List<string> modifiedWithStructuralChanges,
        CancellationToken ct)
    {
        var fullPath = Path.Combine(absoluteWorkspacePath, relPath);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists) return;

        var normalizedFullPath = fileInfo.FullName.Replace('\\', '/');
        var lastMod = fileInfo.LastWriteTimeUtc;
        var sizeBytes = fileInfo.Length;

        var streamed = await FileStreamReader.StreamAndAnalyzeFileAsync(
            normalizedFullPath,
            relPath,
            fileInfo.Name,
            sizeBytes,
            isManifest: false,
            isSupportedSourceOrConfig: true,
            filter,
            ct);
        if (streamed.SkipByContent)
        {
            fileRegistry.UpdateEntry(relPath, SkippedContentHash, lastMod, relPath, sizeBytes: sizeBytes);
            filesToUpdateRegistry.Add((relPath, SkippedContentHash, lastMod, sizeBytes, relPath, null));
            return;
        }

        var newHash = streamed.ContentHash ?? await HashUtility.ComputeSha256FromFileAsync(fullPath, ct);

        if (fileRegistry.TryGetEntry(relPath, out var existingEntry) &&
            string.Equals(existingEntry.ContentHash, newHash, StringComparison.OrdinalIgnoreCase))
        {
            fileRegistry.UpdateEntry(relPath, newHash, lastMod, existingEntry.ProjectPath, sizeBytes: sizeBytes);
            filesToUpdateRegistry.Add((relPath, newHash, lastMod, sizeBytes, existingEntry.ProjectPath, null));
            return;
        }

        if (!fileRegistry.TryGetSnapshot(relPath, out var oldSnapshot))
        {
            var initialSnapshot = await WorkspaceIndexer.TryBuildSnapshotAsync(
                normalizedFullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, ct, streamed.DecodedText);
            if (initialSnapshot != null)
            {
                fileRegistry.UpdateEntry(relPath, newHash, lastMod, "", initialSnapshot, sizeBytes);
                var snapJson = initialSnapshot.ComputeStructuralHash();
                filesToUpdateRegistry.Add((relPath, newHash, lastMod, sizeBytes, "", snapJson));
                logger.LogInformation("[Incremental:Decision] Seeded initial AST snapshot for '{File}' without full rescan.", relPath);
                return;
            }

            var reason = $"No prior AST snapshot in database for '{relPath}' and AST parsing failed.";
            logger.LogInformation("[Incremental:Decision] Structural change in '{File}'.", relPath);
            logger.LogDebug("[Incremental:Decision] Structural details for '{File}': {Reason}", relPath, reason);
            structuralReasons.Add(reason);
            modifiedWithStructuralChanges.Add(relPath);
            return;
        }

        var newSnapshot = await WorkspaceIndexer.TryBuildSnapshotAsync(
            normalizedFullPath, relPath, workspaceId, absoluteWorkspacePath, newHash, lastMod, ct, streamed.DecodedText);
        if (newSnapshot == null)
        {
            var reason = $"No AST parser available or syntax parsing failed for '{relPath}'.";
            logger.LogInformation("[Incremental:Decision] Structural change in '{File}'.", relPath);
            logger.LogDebug("[Incremental:Decision] Structural details for '{File}': {Reason}", relPath, reason);
            structuralReasons.Add(reason);
            modifiedWithStructuralChanges.Add(relPath);
            return;
        }

        var patch = SemanticGraphDiffer.ComputeDiff(oldSnapshot, newSnapshot);
        if (!patch.HasGraphStructuralChanges)
        {
            logger.LogInformation(
                "[Incremental:Decision] Logic-only change in '{File}' ({LogicCount} logic edits, {ShiftCount} line shifts, 0 graph modifications). Preserving graph.",
                relPath,
                patch.LogicOnlyChangedSymbols.Count,
                patch.LineShiftedSymbols.Count);
            fileRegistry.UpdateEntry(relPath, newHash, lastMod, "", newSnapshot, sizeBytes);
            var snapJson = newSnapshot.ComputeStructuralHash();
            filesToUpdateRegistry.Add((relPath, newHash, lastMod, sizeBytes, "", snapJson));
            return;
        }

        var fullReason = FormatStructuralPatchReason(relPath, patch);
        logger.LogInformation("[Incremental:Decision] Structural change in '{File}'.", relPath);
        logger.LogDebug("[Incremental:Decision] Structural details for '{File}': {Reason}", relPath, fullReason);
        structuralReasons.Add(fullReason);
        modifiedWithStructuralChanges.Add(relPath);
    }

    private static string FormatStructuralPatchReason(string relPath, GraphPatch patch)
    {
        var reasons = new List<string>();
        if (patch.AddedSymbols.Count > 0)
        {
            reasons.Add($"Added symbols: [{string.Join(", ", patch.AddedSymbols.Select(s => $"{s.Kind} '{s.QualifiedName}'"))}]");
        }
        if (patch.RemovedSymbols.Count > 0)
        {
            reasons.Add($"Removed symbols: [{string.Join(", ", patch.RemovedSymbols.Select(s => $"{s.Kind} '{s.QualifiedName}'"))}]");
        }
        if (patch.OutgoingCallDiffs.Count > 0)
        {
            var callDesc = string.Join("; ", patch.OutgoingCallDiffs.Select(kv =>
            {
                var addedStr = kv.Value.Added.Count > 0 ? $"+[{string.Join(", ", kv.Value.Added)}]" : "";
                var remStr = kv.Value.Removed.Count > 0 ? $"-[{string.Join(", ", kv.Value.Removed)}]" : "";
                return $"{kv.Key} ({string.Join(" ", new[] { addedStr, remStr }.Where(s => !string.IsNullOrEmpty(s)))})";
            }));
            reasons.Add($"Call changes: [{callDesc}]");
        }
        if (patch.OutgoingTypeDiffs.Count > 0)
        {
            var typeDesc = string.Join("; ", patch.OutgoingTypeDiffs.Select(kv =>
            {
                var addedStr = kv.Value.Added.Count > 0 ? $"+[{string.Join(", ", kv.Value.Added)}]" : "";
                var remStr = kv.Value.Removed.Count > 0 ? $"-[{string.Join(", ", kv.Value.Removed)}]" : "";
                return $"{kv.Key} ({string.Join(" ", new[] { addedStr, remStr }.Where(s => !string.IsNullOrEmpty(s)))})";
            }));
            reasons.Add($"Type changes: [{typeDesc}]");
        }
        return $"Structural changes in '{relPath}': {string.Join(" | ", reasons)}";
    }

    private static HashSet<string> BuildRelevantDirectories(HashSet<string> structuralFiles)
    {
        var relevantDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty, "." };
        foreach (var relFilePath in structuralFiles)
        {
            var dir = Path.GetDirectoryName(relFilePath)?.NormalizeSlashes();
            while (!string.IsNullOrEmpty(dir) && dir != ".")
            {
                if (!relevantDirs.Add(dir)) break;
                dir = Path.GetDirectoryName(dir)?.NormalizeSlashes();
            }
        }
        return relevantDirs;
    }
}
