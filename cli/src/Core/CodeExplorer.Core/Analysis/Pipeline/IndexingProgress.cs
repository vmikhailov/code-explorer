namespace CodeExplorer.Core.Parser;

public record IndexingProgress(
    int NodesPersisted,
    int RelationshipsPersisted,
    int NodesCount,
    int RelationshipsCount,
    IReadOnlyDictionary<string, int> NodesByKind,
    int Percentage = 0,
    string Phase = "Indexing",
    string? Message = null,
    int TotalFiles = 0,
    int ProcessedFiles = 0
);
