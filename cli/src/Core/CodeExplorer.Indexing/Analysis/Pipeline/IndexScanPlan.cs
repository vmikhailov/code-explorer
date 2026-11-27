namespace CodeExplorer.Core.Parser;

internal sealed record IndexScanPlan(
    IndexExecutionMode Mode,
    bool ClearDatabase,
    IReadOnlySet<string>? IncrementalChangedFiles = null,
    IReadOnlySet<string>? IncrementalRelevantDirectories = null)
{
    public bool GraphChanged => Mode is IndexExecutionMode.Incremental or IndexExecutionMode.Full;
}
