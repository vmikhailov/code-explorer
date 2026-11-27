namespace CodeExplorer.Core.Parser;

public readonly record struct SymbolScopeInfo(
    string SymbolId,
    string FilePath,
    string? ProjectName,
    string? Namespace,
    string? EnclosingTypeName,
    SymbolVisibility Visibility,
    bool IsTest = false);
