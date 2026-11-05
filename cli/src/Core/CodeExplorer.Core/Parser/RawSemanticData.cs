using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public enum ImportType : byte
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
    MemberKind? MemberKind = null
)
{
    public RawVariable(
        string Name,
        string InitializerText,
        string Scope,
        bool IsConstant,
        string FilePath,
        int StartLine,
        int EndLine,
        int StartCol,
        int EndCol,
        string? TypeName,
        string? MemberKind)
        : this(Name, InitializerText, Scope, IsConstant, FilePath, StartLine, EndLine, StartCol, EndCol, TypeName, string.IsNullOrEmpty(MemberKind) ? null : TaxonomyEnumExtensions.ParseMemberKind(MemberKind))
    {
    }
}

public record RawTypeBinding(
    string VariableName,
    string TypeName,
    string FilePath,
    string ScopeId
);
