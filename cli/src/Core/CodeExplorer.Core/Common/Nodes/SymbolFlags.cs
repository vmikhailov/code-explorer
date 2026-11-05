namespace CodeExplorer.Core.Common.Nodes;

/// <summary>
/// Compact bitmask representing boolean flags on syntactic symbols and ontology nodes,
/// replacing string-typed "true"/"false" dictionary entries.
/// </summary>
[Flags]
public enum SymbolFlags : ushort
{
    None = 0,
    IsTest = 1 << 0,
    IsLibrary = 1 << 1,
    IsEmpty = 1 << 2,
    HasComment = 1 << 3,
    IsDecorator = 1 << 4,
    IsInternal = 1 << 5,
    IsUnsafe = 1 << 6,
    IsAsync = 1 << 7,
    IsExported = 1 << 8,
    IsModuleInit = 1 << 9,
    HasParseIssues = 1 << 10,
    IsImplicitSchema = 1 << 11,
    IsConstant = 1 << 12,
    IsStatic = 1 << 13,
    IsReadOnly = 1 << 14,
    HasEntityOrTable = 1 << 15
}
