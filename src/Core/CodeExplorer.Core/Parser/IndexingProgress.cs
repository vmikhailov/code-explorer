using System.Collections.Generic;

namespace CodeExplorer.Core.Parser;

public record IndexingProgress(
    int NodesPersisted,
    int RelationshipsPersisted,
    int NodesCount,
    int RelationshipsCount,
    IReadOnlyDictionary<string, int> NodesByKind
);
