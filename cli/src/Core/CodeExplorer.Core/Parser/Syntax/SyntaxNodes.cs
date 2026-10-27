using System.Collections.Frozen;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Represents a 0-based line and column coordinate in source text.
/// </summary>
public readonly record struct SyntaxPoint(int Row, int Column);

public sealed record SyntaxDiagnostic(
    string Kind,
    string Message,
    TextSpan Span,
    string? ParentGrammarKind = null
);

/// <summary>
/// Represents an exact character/byte span and 0-based line/column range in a <see cref="SyntaxDocument"/>.
/// </summary>
public readonly record struct TextSpan(
    int StartByte,
    int EndByte,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn
)
{
    public static readonly TextSpan Empty = new(0, 0, 0, 0, 0, 0);
    public int Length => EndByte > StartByte ? EndByte - StartByte : 0;
    public bool IsEmpty => EndByte <= StartByte;
}

[Flags]
public enum SyntaxModifiers
{
    None = 0,
    Public = 1 << 0,
    Private = 1 << 1,
    Protected = 1 << 2,
    Internal = 1 << 3,
    Static = 1 << 4,
    Abstract = 1 << 5,
    Virtual = 1 << 6,
    Override = 1 << 7,
    Sealed = 1 << 8,
    ReadOnly = 1 << 9,
    Const = 1 << 10,
    Async = 1 << 11,
    Unsafe = 1 << 12,
    Extern = 1 << 13,
    Partial = 1 << 14,
    Mutable = 1 << 15,
    Export = 1 << 16,
    Default = 1 << 17,
    Data = 1 << 18
}

public enum TypeSyntaxKind
{
    Class,
    Interface,
    Struct,
    Enum,
    Record,
    Trait,
    Protocol,
    ObjectSingleton,
    CompanionObject,
    TypeAlias,
    Union,
    GraphQlType
}

public enum FunctionSyntaxKind
{
    Function,
    Method,
    Constructor,
    Destructor,
    Accessor,
    Operator,
    Macro
}

public enum MemberSyntaxKind
{
    Field,
    Property,
    Event,
    Constant,
    EnumMember
}

public enum ParameterPassingMode
{
    Value,
    Ref,
    Out,
    In,
    RefBorrow,
    MutBorrow,
    VariadicArgs,
    VariadicKwargs,
    Receiver
}

public enum TypeRefKind
{
    Named,
    Generic,
    Array,
    Slice,
    Tuple,
    Pointer,
    Reference,
    Nullable,
    Union,
    Intersection,
    Function,
    Channel
}

public enum LiteralSyntaxKind
{
    String,
    RawString,
    Char,
    Integer,
    Float,
    Boolean,
    Null,
    Regex
}

public enum CallSyntaxKind
{
    Function,
    Method,
    Constructor,
    Macro,
    SuperConstructor,
    ThisConstructor
}

public enum ArgumentModifier
{
    None,
    Ref,
    Out,
    In,
    Spread,
    KeywordSpread
}

public enum CollectionSyntaxKind
{
    Array,
    ObjectOrMap,
    Tuple,
    Set,
    StructInit
}

public enum SqlOperationKind
{
    Select,
    Insert,
    Update,
    Delete,
    CreateTable,
    AlterTable,
    DropTable,
    CreateProcedure,
    CreateFunction,
    CreateView,
    CreateTrigger,
    CreateDatabase,
    Exec,
    Merge,
    Unknown
}

/// <summary>
/// Universal Abstract Syntax Tree base node (Roslyn-style *Syntax model).
/// Represents the lossless logical union of all language AST constructs across CodeExplorer.
/// </summary>
public abstract class SyntaxNode
{
    private string? _cachedText;
    private IReadOnlyList<SyntaxNode>? _namedChildren;

    /// <summary>
    /// Unique non-zero node identifier within the owning <see cref="SyntaxDocument"/>.
    /// </summary>
    public int NodeId { get; set; } = 1;

    /// <summary>
    /// Pointer-compatible handle derived from <see cref="NodeId"/> for fast identity sets and stacks.
    /// </summary>
    public IntPtr Id => (IntPtr)NodeId;

    public TextSpan Span { get; set; }
    public string RawGrammarKind { get; set; } = string.Empty;
    public string Type => RawGrammarKind;
    public string? FieldRole { get; set; }
    public bool IsNamed { get; set; } = true;
    public string? LeadingDocComment { get; set; }

    public SyntaxDocument? Document { get; set; }
    public SyntaxDocument? Tree => Document;
    public SyntaxNode? Parent { get; set; }
    public int ChildIndex { get; set; } = -1;

    public SyntaxNode? NextSibling
    {
        get
        {
            var p = Parent;
            if (p == null) return null;
            var idx = ChildIndex >= 0 && ChildIndex < p.Children.Count && ReferenceEquals(p.Children[ChildIndex], this)
                ? ChildIndex
                : IndexInParent(p);
            return idx >= 0 && idx + 1 < p.Children.Count ? p.Children[idx + 1] : null;
        }
    }

    public SyntaxNode? PrevSibling
    {
        get
        {
            var p = Parent;
            if (p == null) return null;
            var idx = ChildIndex >= 0 && ChildIndex < p.Children.Count && ReferenceEquals(p.Children[ChildIndex], this)
                ? ChildIndex
                : IndexInParent(p);
            return idx > 0 && idx < p.Children.Count ? p.Children[idx - 1] : null;
        }
    }

    private int IndexInParent(SyntaxNode parent)
    {
        for (int i = 0; i < parent.Children.Count; i++)
        {
            if (ReferenceEquals(parent.Children[i], this)) return i;
        }
        return -1;
    }

    public IReadOnlyList<AnnotationSyntax> Annotations { get; set; } = [];
    public IReadOnlyList<SyntaxNode> Children { get; set; } = [];

    public IReadOnlyList<SyntaxNode> NamedChildren
    {
        get
        {
            if (_namedChildren != null) return _namedChildren;
            if (Children.Count == 0) return _namedChildren = [];
            var list = new List<SyntaxNode>(Children.Count);
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].IsNamed) list.Add(Children[i]);
            }
            return _namedChildren = list;
        }
    }

    public SyntaxPoint StartPosition => new(Span.StartLine, Span.StartColumn);
    public SyntaxPoint EndPosition => new(Span.EndLine, Span.EndColumn);
    public int StartIndex => Span.StartByte;
    public int EndIndex => Span.EndByte;

    public string? ExplicitText { get; init; }

    /// <summary>
    /// Gets the exact source code slice corresponding to this syntax node.
    /// </summary>
    public string Text
    {
        get
        {
            if (_cachedText != null) return _cachedText;
            if (ExplicitText != null) return _cachedText = ExplicitText;
            if (Document != null)
            {
                var src = Document.SourceText;
                if (Span.StartByte >= 0 && Span.EndByte <= src.Length && Span.EndByte >= Span.StartByte)
                {
                    return _cachedText = src.Substring(Span.StartByte, Span.EndByte - Span.StartByte);
                }
            }
            return _cachedText = string.Empty;
        }
    }

    /// <summary>
    /// Zero-allocation slice into the owning document's source text.
    /// </summary>
    public ReadOnlySpan<char> AsSpan()
    {
        if (ExplicitText != null) return ExplicitText.AsSpan();
        if (Document != null)
        {
            var src = Document.SourceText;
            if (Span.StartByte >= 0 && Span.EndByte <= src.Length && Span.EndByte >= Span.StartByte)
            {
                return src.AsSpan(Span.StartByte, Span.EndByte - Span.StartByte);
            }
        }
        return ReadOnlySpan<char>.Empty;
    }

    public string ToFullString() => Text;

    public string? OntologyNodeId { get; set; }
    public string? SemanticKind { get; set; }
    public string? SemanticIdentifier { get; set; }
    public Dictionary<string, string>? SemanticMetadata { get; set; }

    /// <summary>
    /// Retrieves the first child node assigned to the specified grammar field role (e.g. "name", "function", "arguments", "value").
    /// </summary>
    public SyntaxNode? GetChildForField(string fieldName)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            var c = Children[i];
            if (string.Equals(c.FieldRole, fieldName, StringComparison.Ordinal))
            {
                return c;
            }
        }
        return null;
    }

    /// <summary>
    /// Retrieves all child nodes assigned to the specified grammar field role.
    /// </summary>
    public IEnumerable<SyntaxNode> GetChildrenForField(string fieldName)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            var c = Children[i];
            if (string.Equals(c.FieldRole, fieldName, StringComparison.Ordinal))
            {
                yield return c;
            }
        }
    }

    public string? GetFieldNameForChild(int index) =>
        index >= 0 && index < Children.Count ? Children[index].FieldRole : null;

    public override string ToString() => $"{GetType().Name}({RawGrammarKind}) [{Span.StartLine + 1}:{Span.StartColumn + 1}]";
}

/// <summary>
/// Universal field role names for structural child access on <see cref="SyntaxNode"/>.
/// </summary>
public static class SyntaxFields
{
    public const string Name = "name";
    public const string Function = "function";
    public const string Type = "type";
    public const string Value = "value";
    public const string Pattern = "pattern";
    public const string Left = "left";
    public const string Right = "right";
    public const string Body = "body";
    public const string Arguments = "arguments";
    public const string Parameters = "parameters";
    public const string Return = "return";
    public const string Operator = "operator";
    public const string Expression = "expression";
    public const string Property = "property";
    public const string Object = "object";
    public const string Source = "source";
    public const string Constructor = "constructor";
    public const string Field = "field";
    public const string Path = "path";
    public const string Key = "key";
    public const string Operand = "operand";
    public const string Returns = "returns";
    public const string ReturnType = "return_type";
    public const string Index = "index";
    public const string Subscript = "subscript";
    public const string ModuleName = "module_name";
    public const string Tag = "tag";
}

/// <summary>
/// Root Universal Syntax Tree document representing a parsed source or schema file.
/// </summary>
public sealed class SyntaxDocument : SyntaxNode, IDisposable
{
    public required string FilePath { get; init; }
    public required string RelativePath { get; init; }
    public required string LanguageName { get; init; }
    public required string SourceText { get; init; }
    public string? PackageOrNamespace { get; set; }

    public SyntaxNode RootNode => this;
    public string Source => SourceText;

    public IReadOnlyList<ImportDeclSyntax> Imports { get; set; } = [];
    public IReadOnlyList<DeclarationSyntax> Declarations { get; set; } = [];
    public IReadOnlyList<StatementSyntax> TopLevelStatements { get; set; } = [];

    // Pre-indexed fast views built during SyntaxDocument construction
    public IReadOnlyList<TypeDeclSyntax> AllTypes { get; set; } = [];
    public IReadOnlyList<FunctionDeclSyntax> AllFunctions { get; set; } = [];
    public IReadOnlyList<MemberDeclSyntax> AllMembers { get; set; } = [];
    public IReadOnlyList<VariableDeclSyntax> AllVariables { get; set; } = [];
    public IReadOnlyList<CallExprSyntax> AllCalls { get; set; } = [];
    public IReadOnlyList<SqlStmtSyntax> AllSqlStatements { get; set; } = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics { get; set; } = [];

    private Dictionary<string, FunctionDeclSyntax>? _functionsByName;

    public FunctionDeclSyntax? FindFunctionByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_functionsByName == null)
        {
            var dict = new Dictionary<string, FunctionDeclSyntax>(AllFunctions.Count, StringComparer.Ordinal);
            for (int i = 0; i < AllFunctions.Count; i++)
            {
                var fn = AllFunctions[i];
                var fnName = !string.IsNullOrEmpty(fn.Name)
                    ? fn.Name
                    : (fn.GetChildForField(SyntaxFields.Name) ?? fn.Children.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text;
                if (!string.IsNullOrEmpty(fnName) && !dict.ContainsKey(fnName))
                {
                    dict[fnName] = fn;
                }
            }
            _functionsByName = dict;
        }
        return _functionsByName.TryGetValue(name, out var found) ? found : null;
    }

    public ReadOnlySpan<char> Slice(TextSpan span) =>
        span.StartByte >= 0 && span.EndByte <= SourceText.Length && span.EndByte >= span.StartByte
            ? SourceText.AsSpan(span.StartByte, span.EndByte - span.StartByte)
            : ReadOnlySpan<char>.Empty;

    public string GetText(int startIndex, int endIndex) =>
        startIndex >= 0 && endIndex <= SourceText.Length && endIndex >= startIndex
            ? SourceText.Substring(startIndex, endIndex - startIndex)
            : string.Empty;

    public void Dispose()
    {
        // Managed syntax tree is eligible for GC when references are released after Layer 4.
    }

    /// <summary>
    /// Creates a <see cref="SyntaxDocument"/> from source text and optional syntax children.
    /// </summary>
    public static SyntaxDocument Create(
        string sourceText,
        string languageName,
        string filePath = "",
        string relativePath = "",
        IReadOnlyList<SyntaxNode>? children = null)
    {
        sourceText ??= string.Empty;
        var span = ComputeTextSpan(sourceText);
        var doc = new SyntaxDocument
        {
            NodeId = 1,
            Span = span,
            RawGrammarKind = "document",
            FilePath = filePath,
            RelativePath = relativePath,
            LanguageName = languageName,
            SourceText = sourceText
        };
        doc.Document = doc;

        if (children is { Count: > 0 })
        {
            var nextId = 2;
            var allTypes = new List<TypeDeclSyntax>();
            var allFunctions = new List<FunctionDeclSyntax>();
            var allMembers = new List<MemberDeclSyntax>();
            var allVariables = new List<VariableDeclSyntax>();
            var allCalls = new List<CallExprSyntax>();
            var allSql = new List<SqlStmtSyntax>();
            var imports = new List<ImportDeclSyntax>();
            var declarations = new List<DeclarationSyntax>();

            void AttachRecursive(SyntaxNode node, SyntaxNode parent)
            {
                node.NodeId = nextId++;
                node.Document = doc;
                node.Parent = parent;
                switch (node)
                {
                    case TypeDeclSyntax td: allTypes.Add(td); break;
                    case FunctionDeclSyntax fd: allFunctions.Add(fd); break;
                    case MemberDeclSyntax md: allMembers.Add(md); break;
                    case VariableDeclSyntax vd: allVariables.Add(vd); break;
                    case CallExprSyntax ce: allCalls.Add(ce); break;
                    case SqlStmtSyntax sq: allSql.Add(sq); break;
                    case ImportDeclSyntax im: imports.Add(im); break;
                }
                for (int i = 0; i < node.Children.Count; i++)
                {
                    node.Children[i].ChildIndex = i;
                    AttachRecursive(node.Children[i], node);
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                c.ChildIndex = i;
                AttachRecursive(c, doc);
                if (c is DeclarationSyntax d && d is not ImportDeclSyntax)
                {
                    declarations.Add(d);
                }
            }

            doc.Children = children;
            doc.Imports = imports;
            doc.Declarations = declarations;
            doc.AllTypes = allTypes;
            doc.AllFunctions = allFunctions;
            doc.AllMembers = allMembers;
            doc.AllVariables = allVariables;
            doc.AllCalls = allCalls;
            doc.AllSqlStatements = allSql;
        }

        return doc;
    }

    private static TextSpan ComputeTextSpan(string sourceText)
    {
        int lines = 0;
        int lastLineStart = 0;
        for (int i = 0; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '\n')
            {
                lines++;
                lastLineStart = i + 1;
            }
        }
        return new TextSpan(0, sourceText.Length, 0, 0, lines, sourceText.Length - lastLineStart);
    }
}

// ============================================================================
// DECLARATIONS
// ============================================================================

public abstract class DeclarationSyntax : SyntaxNode
{
    public string Name { get; set; } = string.Empty;
    public SyntaxModifiers Modifiers { get; set; }
}

public sealed class NamespaceDeclSyntax : DeclarationSyntax
{
    public bool IsFileScoped { get; set; }
    public IReadOnlyList<DeclarationSyntax> Members { get; set; } = [];
    public IReadOnlyList<ImportDeclSyntax> Imports { get; set; } = [];
}

public sealed record ImportSpecifierSyntax(string Name, string? Alias = null);

public sealed class ImportDeclSyntax : DeclarationSyntax
{
    public string ModulePath { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public IReadOnlyList<ImportSpecifierSyntax> ImportedSymbols { get; set; } = [];
    public bool IsWildcard { get; set; }
    public bool IsReExport { get; set; }
    public bool IsStatic { get; set; }
    public int RelativeDotCount { get; set; }
    public ImportType ResolvedImportType { get; set; } = ImportType.External;
}

public sealed class TypeDeclSyntax : DeclarationSyntax
{
    public TypeSyntaxKind Kind { get; set; } = TypeSyntaxKind.Class;
    public IReadOnlyList<string> TypeParameters { get; set; } = [];
    public FunctionDeclSyntax? PrimaryConstructor { get; set; }
    public IReadOnlyList<TypeRefSyntax> BaseTypes { get; set; } = [];
    public IReadOnlyList<DeclarationSyntax> Members { get; set; } = [];
    public IReadOnlyList<MemberDeclSyntax> EnumVariants { get; set; } = [];
}

public sealed class ImplDeclSyntax : DeclarationSyntax
{
    public TypeRefSyntax? TargetType { get; set; }
    public TypeRefSyntax? ImplementedTrait { get; set; }
    public IReadOnlyList<string> TypeParameters { get; set; } = [];
    public IReadOnlyList<DeclarationSyntax> Members { get; set; } = [];
}

public sealed class FunctionDeclSyntax : DeclarationSyntax
{
    public FunctionSyntaxKind Kind { get; set; } = FunctionSyntaxKind.Method;
    public ParameterDeclSyntax? Receiver { get; set; }
    public IReadOnlyList<string> TypeParameters { get; set; } = [];
    public IReadOnlyList<ParameterDeclSyntax> Parameters { get; set; } = [];
    public IReadOnlyList<TypeRefSyntax> ReturnTypes { get; set; } = [];
    public IReadOnlyList<TypeRefSyntax> ThrowsTypes { get; set; } = [];
    public BlockStmtSyntax? Body { get; set; }
    public ExprSyntax? ExpressionBody { get; set; }
    public bool IsAsync => Modifiers.HasFlag(SyntaxModifiers.Async);
    public bool IsGenerator { get; set; }
}

public sealed class ParameterDeclSyntax : DeclarationSyntax
{
    public BindingPatternSyntax? Pattern { get; set; }
    public TypeRefSyntax? ParameterType { get; set; }
    public ExprSyntax? DefaultValue { get; set; }
    public ParameterPassingMode PassingMode { get; set; } = ParameterPassingMode.Value;
    public bool IsPropertyParameter { get; set; }
}

public sealed class MemberDeclSyntax : DeclarationSyntax
{
    public MemberSyntaxKind Kind { get; set; } = MemberSyntaxKind.Field;
    public TypeRefSyntax? MemberType { get; set; }
    public ExprSyntax? Initializer { get; set; }
    public FunctionDeclSyntax? Getter { get; set; }
    public FunctionDeclSyntax? Setter { get; set; }
    public string? StructTag { get; set; }
    public string? ResolvedValue { get; set; }
}

public sealed class VariableDeclSyntax : DeclarationSyntax
{
    public BindingPatternSyntax? Pattern { get; set; }
    public TypeRefSyntax? VariableType { get; set; }
    public ExprSyntax? Initializer { get; set; }
    public bool IsConstant { get; set; }
    public bool IsMutable { get; set; } = true;
    public string Scope { get; set; } = "local";
    public string? ResolvedValue { get; set; }
}

public sealed class AnnotationSyntax : DeclarationSyntax
{
    public string? Target { get; set; }
    public IReadOnlyList<ArgumentSyntax> Arguments { get; set; } = [];
}

// ============================================================================
// TYPE REFERENCES & BINDING PATTERNS
// ============================================================================

public sealed class TypeRefSyntax : SyntaxNode
{
    public string Name { get; set; } = string.Empty;
    public string? Qualifier { get; set; }
    public TypeRefKind RefKind { get; set; } = TypeRefKind.Named;
    public IReadOnlyList<TypeRefSyntax> TypeArguments { get; set; } = [];
    public bool IsNullable { get; set; }
    public bool IsMutableRef { get; set; }
    public string? Lifetime { get; set; }
}

public abstract class BindingPatternSyntax : SyntaxNode;

public sealed class IdentifierPatternSyntax : BindingPatternSyntax
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ObjectDestructuringPatternSyntax : BindingPatternSyntax
{
    public IReadOnlyList<(string Key, string? Alias, ExprSyntax? Default)> Properties { get; set; } = [];
}

public sealed class ArrayDestructuringPatternSyntax : BindingPatternSyntax
{
    public IReadOnlyList<BindingPatternSyntax> Elements { get; set; } = [];
}

public sealed class TuplePatternSyntax : BindingPatternSyntax
{
    public IReadOnlyList<BindingPatternSyntax> Elements { get; set; } = [];
}

// ============================================================================
// STATEMENTS
// ============================================================================

public abstract class StatementSyntax : SyntaxNode;

public sealed class BlockStmtSyntax : StatementSyntax
{
    public IReadOnlyList<StatementSyntax> Statements { get; set; } = [];
    public bool IsUnsafe { get; set; }
}

public sealed class ExprStmtSyntax : StatementSyntax
{
    public ExprSyntax? Expression { get; set; }
    public bool IsImplicitReturn { get; set; }
}

public sealed class DeclStmtSyntax : StatementSyntax
{
    public IReadOnlyList<DeclarationSyntax> Declarations { get; set; } = [];
}

public sealed class IfStmtSyntax : StatementSyntax
{
    public StatementSyntax? Init { get; set; }
    public BindingPatternSyntax? Pattern { get; set; }
    public ExprSyntax? Condition { get; set; }
    public StatementSyntax? ThenBranch { get; set; }
    public StatementSyntax? ElseBranch { get; set; }
}

public sealed class MatchArmSyntax : SyntaxNode
{
    public SyntaxNode? Pattern { get; set; }
    public ExprSyntax? Guard { get; set; }
    public SyntaxNode? Body { get; set; }
}

public sealed class SwitchOrMatchStmtSyntax : StatementSyntax
{
    public ExprSyntax? Subject { get; set; }
    public bool IsChannelSelect { get; set; }
    public IReadOnlyList<MatchArmSyntax> Arms { get; set; } = [];
}

public sealed class LoopStmtSyntax : StatementSyntax
{
    public BindingPatternSyntax? IteratorPattern { get; set; }
    public SyntaxNode? Init { get; set; }
    public ExprSyntax? Condition { get; set; }
    public ExprSyntax? Increment { get; set; }
    public ExprSyntax? Collection { get; set; }
    public StatementSyntax? Body { get; set; }
    public bool IsAsync { get; set; }
}

public sealed class CatchClauseSyntax : StatementSyntax
{
    public TypeRefSyntax? ExceptionType { get; set; }
    public string? ExceptionVariable { get; set; }
    public BlockStmtSyntax? Body { get; set; }
}

public sealed class TryStmtSyntax : StatementSyntax
{
    public BlockStmtSyntax? Body { get; set; }
    public IReadOnlyList<CatchClauseSyntax> Catches { get; set; } = [];
    public BlockStmtSyntax? FinallyBody { get; set; }
    public IReadOnlyList<SyntaxNode> Resources { get; set; } = [];
}

public sealed class UsingOrWithStmtSyntax : StatementSyntax
{
    public IReadOnlyList<SyntaxNode> Resources { get; set; } = [];
    public StatementSyntax? Body { get; set; }
    public bool IsAsync { get; set; }
}

public sealed class DeferOrGoStmtSyntax : StatementSyntax
{
    public bool IsGoroutine { get; set; }
    public ExprSyntax? Call { get; set; }
}

public sealed class ReturnOrYieldStmtSyntax : StatementSyntax
{
    public bool IsYield { get; set; }
    public ExprSyntax? Value { get; set; }
}

public sealed class ThrowStmtSyntax : StatementSyntax
{
    public ExprSyntax? Exception { get; set; }
}

public sealed class BranchStmtSyntax : StatementSyntax
{
    public string BranchKind { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public sealed class LockStmtSyntax : StatementSyntax
{
    public ExprSyntax? LockTarget { get; set; }
    public BlockStmtSyntax? Body { get; set; }
}

public sealed record SqlTableRefSyntax(
    string TableName,
    string? Schema = null,
    string? Alias = null,
    bool IsWriteTarget = false
);

public sealed class SqlStmtSyntax : StatementSyntax
{
    public SqlOperationKind Operation { get; set; } = SqlOperationKind.Unknown;
    public string OperationVerb { get; set; } = string.Empty;
    public IReadOnlyList<SqlTableRefSyntax> Tables { get; set; } = [];
    public IReadOnlyList<string> Columns { get; set; } = [];
    public IReadOnlyList<string> Procedures { get; set; } = [];
    public string RawSql { get; set; } = string.Empty;
}

// ============================================================================
// EXPRESSIONS
// ============================================================================

public abstract class ExprSyntax : SyntaxNode
{
    /// <summary>
    /// Pre-evaluated constant or intra-procedural dataflow value computed by <see cref="SyntaxValueEvaluator"/>.
    /// </summary>
    public string? ResolvedValue { get; set; }

    /// <summary>
    /// Declared or inferred type reference.
    /// </summary>
    public TypeRefSyntax? InferredType { get; set; }

    /// <summary>
    /// Embedded SQL or GraphQL syntax node parsed after value evaluation in Layer 3.
    /// </summary>
    public SyntaxNode? EmbeddedSyntax { get; set; }
}

public sealed class LiteralExprSyntax : ExprSyntax
{
    public LiteralSyntaxKind LiteralKind { get; set; } = LiteralSyntaxKind.String;
    public string ValueText { get; set; } = string.Empty;
}

public sealed record InterpolationPartSyntax(
    bool IsHole,
    string? LiteralText,
    ExprSyntax? Expression
);

public sealed class InterpolatedStringExprSyntax : ExprSyntax
{
    public ExprSyntax? Tag { get; set; }
    public IReadOnlyList<InterpolationPartSyntax> Parts { get; set; } = [];
}

public sealed class IdentifierExprSyntax : ExprSyntax
{
    public string Name { get; set; } = string.Empty;
    public DeclarationSyntax? TargetDeclaration { get; set; }
}

public sealed class MemberAccessExprSyntax : ExprSyntax
{
    public ExprSyntax? Object { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Operator { get; set; } = ".";
}

public sealed class IndexOrSliceExprSyntax : ExprSyntax
{
    public ExprSyntax? Target { get; set; }
    public ExprSyntax? Index { get; set; }
    public ExprSyntax? SliceStart { get; set; }
    public ExprSyntax? SliceEnd { get; set; }
    public ExprSyntax? SliceStep { get; set; }
}

public sealed class ArgumentSyntax : ExprSyntax
{
    public string? Name { get; set; }
    public ExprSyntax? Value { get; set; }
    public ArgumentModifier Modifier { get; set; } = ArgumentModifier.None;
}

public sealed class CallExprSyntax : ExprSyntax
{
    public CallSyntaxKind CallKind { get; set; } = CallSyntaxKind.Method;
    public ExprSyntax? Callee { get; set; }
    public ExprSyntax? Receiver { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public IReadOnlyList<TypeRefSyntax> TypeArguments { get; set; } = [];
    public IReadOnlyList<ArgumentSyntax> Arguments { get; set; } = [];
}

public sealed class BinaryExprSyntax : ExprSyntax
{
    public ExprSyntax? Left { get; set; }
    public string Operator { get; set; } = string.Empty;
    public ExprSyntax? Right { get; set; }
}

public sealed class UnaryExprSyntax : ExprSyntax
{
    public string Operator { get; set; } = string.Empty;
    public ExprSyntax? Operand { get; set; }
    public bool IsPrefix { get; set; } = true;
}

public sealed class AssignmentExprSyntax : ExprSyntax
{
    public SyntaxNode? Left { get; set; }
    public string Operator { get; set; } = "=";
    public ExprSyntax? Right { get; set; }
}

public sealed class ConditionalExprSyntax : ExprSyntax
{
    public ExprSyntax? Condition { get; set; }
    public ExprSyntax? WhenTrue { get; set; }
    public ExprSyntax? WhenFalse { get; set; }
}

public sealed class LambdaExprSyntax : ExprSyntax
{
    public IReadOnlyList<ParameterDeclSyntax> Parameters { get; set; } = [];
    public SyntaxNode? Body { get; set; }
    public bool IsAsync { get; set; }
    public bool IsMove { get; set; }
}

public sealed class AwaitOrTryExprSyntax : ExprSyntax
{
    public bool IsTryPropagate { get; set; }
    public ExprSyntax? Operand { get; set; }
}

public sealed class MatchExprSyntax : ExprSyntax
{
    public ExprSyntax? Subject { get; set; }
    public IReadOnlyList<MatchArmSyntax> Arms { get; set; } = [];
}

public sealed record CollectionElementSyntax(
    ExprSyntax? Key,
    ExprSyntax? Value,
    bool IsSpread = false
);

public sealed class CollectionExprSyntax : ExprSyntax
{
    public CollectionSyntaxKind CollectionKind { get; set; } = CollectionSyntaxKind.Array;
    public TypeRefSyntax? StructType { get; set; }
    public IReadOnlyList<CollectionElementSyntax> Elements { get; set; } = [];
}

public sealed class QueryOrComprehensionExprSyntax : ExprSyntax
{
    public bool IsLinq { get; set; }
    public ExprSyntax? Projection { get; set; }
}

public sealed class JsxElementExprSyntax : ExprSyntax
{
    public string TagName { get; set; } = string.Empty;
    public IReadOnlyList<ArgumentSyntax> Attributes { get; set; } = [];
    public IReadOnlyList<SyntaxNode> JsxChildren { get; set; } = [];
}

public sealed class ChannelExprSyntax : ExprSyntax
{
    public bool IsSend { get; set; }
    public ExprSyntax? Channel { get; set; }
    public ExprSyntax? Value { get; set; }
}

public sealed class RangeOrSpreadExprSyntax : ExprSyntax
{
    public ExprSyntax? Start { get; set; }
    public ExprSyntax? End { get; set; }
    public bool IsInclusive { get; set; }
    public bool IsSpread { get; set; }
}

// ============================================================================
// TOKENS & UNMAPPED NODES (100% LOSSLESS UNION FALLBACKS)
// ============================================================================

/// <summary>
/// Represents an unnamed token node (punctuation, operator, keyword, or trivia).
/// </summary>
public sealed class TokenSyntax : SyntaxNode;

/// <summary>
/// Zero-loss fallback for any named grammar node not matched by a more specific *Syntax class.
/// Preserves RawGrammarKind, FieldRole, Span, and all Children.
/// </summary>
public sealed class UnmappedSyntax : SyntaxNode;
