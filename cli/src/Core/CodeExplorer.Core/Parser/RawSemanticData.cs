namespace CodeExplorer.Core.Parser;

public enum ImportType
{
    External,
    Internal
}

public record ImportSpecifier(
    string LocalName,
    string? ImportedName = null,
    bool IsDefault = false,
    bool IsNamespace = false
);

public record RawImport(
    string Path,
    string FilePath,
    ImportType Type = ImportType.External,
    IReadOnlyList<ImportSpecifier>? Specifiers = null
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
    int EndCol,
    string? TypeName = null,
    string? MemberKind = null // "property", "field", "variable", "parameter"
);

public record RawTypeBinding(
    string VariableName,
    string TypeName,
    string FilePath,
    string ScopeId
);
