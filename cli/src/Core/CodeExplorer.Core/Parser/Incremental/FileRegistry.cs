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
    private readonly ConcurrentDictionary<string, (string ContentHash, DateTime LastModifiedUtc, string ProjectPath)> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, FileGraphSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;
    public int SnapshotCount => _snapshots.Count;

    public IEnumerable<KeyValuePair<string, (string ContentHash, DateTime LastModifiedUtc, string ProjectPath)>> Entries => _entries;


    public async Task LoadAsync(IGraphClient dbClient, CancellationToken cancellationToken = default)
    {
        var dbEntries = await dbClient.LoadFileRegistryAsync(cancellationToken);
        _entries.Clear();
        _snapshots.Clear();
        foreach (var (path, entry) in dbEntries)
        {
            _entries[path] = (entry.ContentHash, entry.LastModifiedUtc, entry.ProjectPath);
            if (!string.IsNullOrEmpty(entry.SnapshotJson))
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

    public FileChangeset ComputeChangeset(string workspaceRoot, IEnumerable<string> currentRelativeFiles, string? targetSubPath = null)
    {
        var added = new List<string>();
        var modified = new List<string>();
        var seenOnDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relPath in currentRelativeFiles)
        {
            seenOnDisk.Add(relPath);
            var fullPath = Path.Combine(workspaceRoot, relPath);
            if (!File.Exists(fullPath)) continue;

            var lastMod = File.GetLastWriteTimeUtc(fullPath);

            if (_entries.TryGetValue(relPath, out var existing))
            {
                // Fast path 1: If LastWriteTimeUtc matches exactly, file did not change
                if (existing.LastModifiedUtc == lastMod)
                {
                    continue;
                }

                // Fast path 2: Timestamp shifted, check SHA-256
                var bytes = File.ReadAllBytes(fullPath);
                var hash = HashUtility.ComputeSha256(bytes);

                if (string.Equals(existing.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    // Only timestamp touched, content is identical
                    _entries[relPath] = (hash, lastMod, existing.ProjectPath);
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
                if (targetPrefix != null && !path.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return !seenOnDisk.Contains(path);
            })
            .ToList();

        return new FileChangeset(added, modified, deleted);
    }

    public bool TryGetEntry(string relativePath, out (string ContentHash, DateTime LastModifiedUtc, string ProjectPath) entry) =>
        _entries.TryGetValue(relativePath, out entry);

    public bool TryGetSnapshot(string relativePath, out FileGraphSnapshot snapshot) =>
        _snapshots.TryGetValue(relativePath, out snapshot!);

    public void SetSnapshot(string relativePath, FileGraphSnapshot snapshot)
    {
        _snapshots[relativePath] = snapshot;
    }

    public void UpdateEntry(string relativePath, string contentHash, DateTime lastModifiedUtc, string projectPath, FileGraphSnapshot? snapshot = null)
    {
        _entries[relativePath] = (contentHash, lastModifiedUtc, projectPath);
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
                try
                {
                    snapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot);
                }
                catch
                {
                }
            }

            return (
                RelativePath: kv.Key,
                ContentHash: kv.Value.ContentHash,
                LastModifiedUtc: kv.Value.LastModifiedUtc,
                ProjectPath: kv.Value.ProjectPath,
                SnapshotJson: snapshotJson
            );
        }).ToList();

        await dbClient.SaveFileRegistryEntriesAsync(toSave, cancellationToken);
    }
}
