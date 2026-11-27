namespace CodeExplorer.Core.Parser;

public sealed class WorkspaceScope
{
    public string AbsoluteWorkspacePath { get; }
    public string HostWorkspacePath { get; }
    public string ScanPath { get; }
    public bool IsSubtreeScan => !string.Equals(ScanPath, AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase);
    public string WorkspaceId { get; set; } = string.Empty;
    public WorkspaceFileFilter FileFilter { get; set; } = WorkspaceFileFilter.Default;
    public WorkspaceDetectorCatalog DetectorCatalog { get; init; } = WorkspaceDetectorCatalog.Empty;
    public CancellationToken CancellationToken { get; init; }
    public bool LightweightAstScanMode { get; set; }
    public bool EnableIntentAnalysis { get; set; }

    public WorkspaceScope(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        string? scanPath = null,
        CancellationToken cancellationToken = default)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        HostWorkspacePath = hostWorkspacePath;
        ScanPath = string.IsNullOrWhiteSpace(scanPath) ? AbsoluteWorkspacePath : scanPath.Replace('\\', '/');
        CancellationToken = cancellationToken;
    }

    public string NormalizeRelativePath(string? relativeOrFullPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrFullPath)) return string.Empty;
        var clean = relativeOrFullPath.Replace('\\', '/');
        if (!string.IsNullOrEmpty(AbsoluteWorkspacePath) && Path.IsPathRooted(clean))
        {
            try
            {
                var rel = Path.GetRelativePath(AbsoluteWorkspacePath, clean).Replace('\\', '/');
                return rel == "." ? string.Empty : rel;
            }
            catch
            {
                return clean;
            }
        }
        return clean == "." ? string.Empty : clean.TrimStart('/');
    }
}
