using CodeExplorer.Core.Semantics;

namespace CodeExplorer.Core.Parser;

public sealed record FunctionFlowSummary(
    string Id,
    string Name,
    string FilePath,
    string? EnclosingTypeId,
    bool IsTest,
    ProjectEntityKind? ProjectKind,
    string? ProjectName,
    IReadOnlyList<SemanticParamSignal>? SemanticParams,
    IReadOnlyList<SemanticArgumentSlice>? OutgoingCallSlices);
