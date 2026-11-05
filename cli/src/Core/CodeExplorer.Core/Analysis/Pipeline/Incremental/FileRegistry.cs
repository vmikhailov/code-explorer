using System.Collections.Concurrent;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Incremental;

public record FileChangeset(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Modified,
    IReadOnlyList<string> Deleted
)
{
    public bool IsEmpty => Added.Count == 0 && Modified.Count == 0 && Deleted.Count == 0;
}

public class FileRegistry
{
    private readonly ConcurrentDictionary<string, (string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath)> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, FileGraphSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;
    public int SnapshotCount => _snapshots.Count;

    public IEnumerable<KeyValuePair<string, (string ContentHash, DateTime LastModifiedUtc, string ProjectPath)>> Entries =>
        _entries.Select(kv => new KeyValuePair<string, (string ContentHash, DateTime LastModifiedUtc, string ProjectPath)>(
            kv.Key, (kv.Value.ContentHash, kv.Value.LastModifiedUtc, kv.Value.ProjectPath)));


    public async Task LoadAsync(IGraphClient dbClient, CancellationToken cancellationToken = default)
    {
        var dbEntries = await dbClient.LoadFileRegistryAsync(cancellationToken);
        _entries.Clear();
        _snapshots.Clear();
        foreach (var (path, entry) in dbEntries)
        {
            _entries[path] = (entry.ContentHash, entry.LastModifiedUtc, entry.SizeBytes, entry.ProjectPath);
            if (!string.IsNullOrEmpty(entry.SnapshotJson))
            {
                if (!entry.SnapshotJson.StartsWith('{'))
                {
                    _snapshots[path] = FileGraphSnapshot.FromStructuralHash(
                        path,
                        entry.ContentHash,
                        entry.LastModifiedUtc,
                        entry.SnapshotJson);
                }
                else
                {
                    try
                    {
                        var snapshot = System.Text.Json.JsonSerializer.Deserialize<FileGraphSnapshot>(entry.SnapshotJson);
                        if (snapshot != null)
                        {
                            _snapshots[path] = snapshot;
                        }
                    }
                    catch
                    {
                        // Ignore corrupted snapshots
                    }
                }
            }
        }
    }

    public FileChangeset ComputeChangeset(string workspaceRoot, IEnumerable<string> currentRelativeFiles, string? targetSubPath = null)
    {
        var added = new List<string>();
        var modified = new List<string>();
        var seenOnDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relPath in currentRelativeFiles)
        {
            seenOnDisk.Add(relPath);
            var fullPath = Path.Combine(workspaceRoot, relPath);
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists) continue;

            var lastMod = fileInfo.LastWriteTimeUtc;
            var sizeBytes = fileInfo.Length;

            if (_entries.TryGetValue(relPath, out var existing))
            {
                // Fast path 1: Both LastWriteTimeUtc and SizeBytes match -> file did not change (0 disk reads)
                if (existing.LastModifiedUtc == lastMod && (existing.SizeBytes < 0 || existing.SizeBytes == sizeBytes))
                {
                    continue;
                }

                // Fast path 2: SizeBytes differs -> file is guaranteed modified without reading/hashing here
                if (existing.SizeBytes >= 0 && existing.SizeBytes != sizeBytes)
                {
                    modified.Add(relPath);
                    continue;
                }

                // Path 3: Timestamp shifted while SizeBytes matches (or legacy entry without SizeBytes) ->
                // stream-read file and compute SHA-256 to check if content actually changed
                var hash = HashUtility.ComputeSha256FromFile(fullPath);

                if (string.Equals(existing.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    // Only timestamp touched, content is identical
                    _entries[relPath] = (hash, lastMod, sizeBytes, existing.ProjectPath);
                    continue;
                }

                modified.Add(relPath);
            }
            else
            {
                added.Add(relPath);
            }
        }

        var normalizedSubPath = targetSubPath?.Replace('\\', '/').Trim('/');
        var targetPrefix = string.IsNullOrEmpty(normalizedSubPath) ? null : normalizedSubPath + "/";

        var deleted = _entries.Keys
            .Where(path =>
            {
                if (targetPrefix != null && !path.StartWithIC(targetPrefix))
                {
                    return false;
                }
                return !seenOnDisk.Contains(path);
            })
            .ToList();

        return new FileChangeset(added, modified, deleted);
    }

    public bool TryGetEntry(string relativePath, out (string ContentHash, DateTime LastModifiedUtc, string ProjectPath) entry)
    {
        if (_entries.TryGetValue(relativePath, out var raw))
        {
            entry = (raw.ContentHash, raw.LastModifiedUtc, raw.ProjectPath);
            return true;
        }

        entry = default;
        return false;
    }

    public bool TryGetEntryWithSize(string relativePath, out (string ContentHash, DateTime LastModifiedUtc, long SizeBytes, string ProjectPath) entry) =>
        _entries.TryGetValue(relativePath, out entry);

    public bool TryGetSnapshot(string relativePath, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FileGraphSnapshot? snapshot) =>
        _snapshots.TryGetValue(relativePath, out snapshot);

    public void SetSnapshot(string relativePath, FileGraphSnapshot snapshot)
    {
        _snapshots[relativePath] = snapshot;
    }

    public void UpdateEntry(string relativePath, string contentHash, DateTime lastModifiedUtc, string projectPath, FileGraphSnapshot? snapshot = null, long sizeBytes = -1)
    {
        if (sizeBytes < 0 && _entries.TryGetValue(relativePath, out var prev) &&
            string.Equals(prev.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
        {
            sizeBytes = prev.SizeBytes;
        }

        _entries[relativePath] = (contentHash, lastModifiedUtc, sizeBytes, projectPath);
        if (snapshot != null)
        {
            _snapshots[relativePath] = snapshot;
        }
    }

    public void RemoveEntry(string relativePath)
    {
        _entries.TryRemove(relativePath, out _);
        _snapshots.TryRemove(relativePath, out _);
    }

    public async Task FlushAsync(IGraphClient dbClient, CancellationToken cancellationToken = default)
    {
        var toSave = _entries.Select(kv =>
        {
            string? snapshotJson = null;
            if (_snapshots.TryGetValue(kv.Key, out var snapshot))
            {
                snapshotJson = snapshot.ComputeStructuralHash();
            }

            return (
                RelativePath: kv.Key,
                ContentHash: kv.Value.ContentHash,
                LastModifiedUtc: kv.Value.LastModifiedUtc,
                SizeBytes: kv.Value.SizeBytes,
                ProjectPath: kv.Value.ProjectPath,
                SnapshotJson: snapshotJson
            );
        }).ToList();

        await dbClient.SaveFileRegistryEntriesAsync(toSave, cancellationToken);
    }
}
