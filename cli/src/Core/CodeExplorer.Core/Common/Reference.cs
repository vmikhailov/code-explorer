namespace CodeExplorer.Common;

public record Reference(string ScopeSymbolId, string TargetName, string Kind, int Line = 0, int Column = 0);

