namespace CodeExplorer.Core.Parser;

public readonly record struct IndexResult(
    int NodesCount,
    int RelationshipsCount,
    Dictionary<string, int> NodesByKind,
    IndexExecutionMode Mode,
    long ElapsedMs)
{
    public bool HasChanges => Mode != IndexExecutionMode.UpToDate;

    public bool GraphChanged => Mode is IndexExecutionMode.Incremental or IndexExecutionMode.Full;

    public void Deconstruct(
        out int nodesCount,
        out int relationshipsCount,
        out Dictionary<string, int> nodesByKind)
    {
        nodesCount = NodesCount;
        relationshipsCount = RelationshipsCount;
        nodesByKind = NodesByKind;
    }
}
