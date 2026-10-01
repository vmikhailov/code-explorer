namespace CodeExplorer.Core.Analysis.Testing;

public record TestImpactRequest(
    IReadOnlyList<string>? ChangedFiles = null,
    string? GitDiff = null,
    string? GitBase = null,
    IReadOnlyList<string>? SymbolNames = null,
    int MaxDepth = 15,
    string? WorkspaceRoot = null
);

public record AffectedTestMethod(
    string TestMethodName,
    string TestSymbol,
    string? TestClassName,
    string TestFilePath,
    int StartLine,
    string? TestFramework,
    string ImpactReason,
    int Depth,
    IReadOnlyList<string> CallChain,
    string? TargetSymbol,
    string? TargetFilePath
);

public record ChangedHunk(string File, int StartLine, int EndLine);

public record ChangedSymbolInfo(
    string Id,
    string Name,
    string Symbol,
    string Kind,
    string FilePath,
    int StartLine,
    int EndLine
);

public record TestImpactReport(
    IReadOnlyList<AffectedTestMethod> AffectedTestMethods,
    IReadOnlyList<ChangedSymbolInfo> ChangedSymbols,
    IReadOnlyDictionary<string, string> RunnerCommands
);
