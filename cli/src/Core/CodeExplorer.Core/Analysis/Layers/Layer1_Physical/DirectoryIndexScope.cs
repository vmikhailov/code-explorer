namespace CodeExplorer.Core.Parser;

/// <summary>
/// Disposable ambient scope binding <see cref="WorkspaceDirectoryIndex.Current"/> for the duration of a workspace scan.
/// </summary>
public readonly struct DirectoryIndexScope(WorkspaceDirectoryIndex? previous) : IDisposable
{
    private readonly Action<WorkspaceDirectoryIndex?>? _restore;

    internal DirectoryIndexScope(WorkspaceDirectoryIndex? previous, Action<WorkspaceDirectoryIndex?> restore)
        : this(previous)
    {
        _restore = restore;
    }

    public void Dispose() => _restore?.Invoke(previous);
}
