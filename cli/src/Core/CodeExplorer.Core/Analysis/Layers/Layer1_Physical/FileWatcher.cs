using System.Collections.Concurrent;

namespace CodeExplorer.Core.Parser;

public class FileWatcher : IDisposable
{
    private readonly string _workspacePath;
    private readonly Func<IReadOnlyList<string>, Task> _onBatchChanged;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounceTimer;
    private readonly ConcurrentDictionary<string, byte> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _isDisposed;

    public FileWatcher(
        string workspacePath,
        Func<IReadOnlyList<string>, Task> onBatchChanged,
        double debounceMs = 300)
    {
        _workspacePath = Path.GetFullPath(workspacePath);
        _onBatchChanged = onBatchChanged;

        _debounceTimer = new System.Timers.Timer(debounceMs)
        {
            AutoReset = false
        };
        _debounceTimer.Elapsed += async (_, _) => await OnDebounceTimerFiredAsync();

        _watcher = new FileSystemWatcher(_workspacePath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.CreationTime
        };

        _watcher.Changed += OnFileSystemEvent;
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemRenamed;
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _debounceTimer.Stop();
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath)) return;

        var relPath = Path.GetRelativePath(_workspacePath, e.FullPath).Replace('\\', '/');
        _pendingChanges[relPath] = 0;
        RestartDebounceTimer();
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (!ShouldIgnore(e.OldFullPath))
        {
            var oldRel = Path.GetRelativePath(_workspacePath, e.OldFullPath).Replace('\\', '/');
            _pendingChanges[oldRel] = 0;
        }

        if (!ShouldIgnore(e.FullPath))
        {
            var newRel = Path.GetRelativePath(_workspacePath, e.FullPath).Replace('\\', '/');
            _pendingChanges[newRel] = 0;
        }

        RestartDebounceTimer();
    }

    private void RestartDebounceTimer()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    private async Task OnDebounceTimerFiredAsync()
    {
        List<string> batch;
        lock (_lock)
        {
            if (_pendingChanges.IsEmpty) return;
            batch = [.. _pendingChanges.Keys];
            _pendingChanges.Clear();
        }

        try
        {
            await _onBatchChanged(batch);
        }
        catch
        {
            // Logging or error handling hook
        }
    }

    private static bool ShouldIgnore(string fullPath)
    {
        var normalized = fullPath.Replace('\\', '/');
        var parts = normalized.Split('/');
        foreach (var part in parts)
        {
            if (WorkspaceFileFilter.IsExcludedDirectory(part))
                return true;
        }

        var fileName = Path.GetFileName(normalized);
        if (WorkspaceFileFilter.ShouldSkipFileName(fileName))
            return true;

        if (Directory.Exists(fullPath))
            return false;

        return !WorkspaceFileFilter.IsSupportedFileType(fileName);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _watcher.Dispose();
            _debounceTimer.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
