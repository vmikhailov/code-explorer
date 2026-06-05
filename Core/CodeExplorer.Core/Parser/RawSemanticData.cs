namespace CodeExplorer.Core.Parser;

public enum ImportType
{
    External,
    Internal
}

public record RawImport(
    string Path,
    string FilePath,
    ImportType Type = ImportType.External
);

public record RawVariable(
    string Name,
    string InitializerText,
    string Scope, // "global", "class", "local"
    bool IsConstant,
    string FilePath,
    int StartLine,
    int EndLine,
    int StartCol,
    int EndCol
);
