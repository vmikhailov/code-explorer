using System.Collections.Concurrent;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Thread-safe string pool using .NET 10 <see cref="ConcurrentDictionary{TKey,TValue}.GetAlternateLookup{TAlternateKey}"/>
/// to deduplicate identifier, grammar kind, and operator strings from <see cref="ReadOnlySpan{T}"/> without intermediate allocations.
/// </summary>
public static class SyntaxStringPool
{
    private const int MaxPooledLength = 64;
    private const int MaxPoolCapacity = 262144;
    private static int _count;
    private static readonly ConcurrentDictionary<string, string> Pool = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> SpanLookup =
        Pool.GetAlternateLookup<ReadOnlySpan<char>>();

    public static string Intern(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return string.Empty;
        if (span.Length > MaxPooledLength) return span.ToString();
        if (SpanLookup.TryGetValue(span, out var existing)) return existing;
        var str = span.ToString();
        if (Volatile.Read(ref _count) < MaxPoolCapacity && SpanLookup.TryAdd(span, str))
        {
            Interlocked.Increment(ref _count);
        }
        return str;
    }

    public static string Intern(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length > MaxPooledLength) return value;
        if (Pool.TryGetValue(value, out var existing)) return existing;
        if (Volatile.Read(ref _count) < MaxPoolCapacity && Pool.TryAdd(value, value))
        {
            Interlocked.Increment(ref _count);
        }
        return value;
    }
}

/// <summary>
/// Represents a 0-based line and column coordinate in source text.
/// </summary>
public readonly record struct SyntaxPoint(int Row, int Column);

public sealed record SyntaxDiagnostic(
    SyntaxDiagnosticKind Kind,
    string Message,
    TextSpan Span
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

/// <summary>
/// Zero-allocation character offset and length slice into <see cref="SyntaxDocument.SourceText"/> (8 bytes).
/// Eliminates duplicate string fragment allocations on AST nodes.
/// </summary>
public readonly record struct SourceSlice(int Start, int Length)
{
    public static readonly SourceSlice Empty = new(0, 0);
    public bool IsEmpty => Length <= 0;
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

public enum TypeSyntaxKind : byte
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

public enum FunctionSyntaxKind : byte
{
    Function,
    Method,
    Constructor,
    Destructor,
    Accessor,
    Operator,
    Macro
}

public enum MemberSyntaxKind : byte
{
    Field,
    Property,
    Event,
    Constant,
    EnumMember
}

public enum ParameterPassingMode : byte
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

public enum TypeRefKind : byte
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
    Channel,
    Annotation,
    Predefined
}

public enum IdentifierRole : byte
{
    Symbol,
    Type,
    Member,
    SelfOrSuper
}

public enum PatternKind : byte
{
    Binding,
    Object,
    Array,
    Tuple,
    Rest,
    As,
    Pair
}

public enum SyntaxListKind : byte
{
    General,
    Expressions,
    Arguments,
    Parameters,
    TypeArguments,
    TypeParameters,
    Attributes,
    Declarations,
    Imports,
    Exports,
    SwitchArms,
    Initializer,
    Clauses,
    TopLevelStatement
}

public enum SyntaxKind : byte
{
    Document,
    NamespaceDeclaration,
    ImportDeclaration,
    TypeDeclaration,
    ImplDeclaration,
    FunctionDeclaration,
    ParameterDeclaration,
    MemberDeclaration,
    VariableDeclaration,
    Annotation,
    BaseTypeClause,
    TypeReference,
    BlockStatement,
    ControlFlowStatement,
    CatchClause,
    ReturnStatement,
    SqlStatement,
    LiteralExpression,
    InterpolatedStringExpression,
    IdentifierExpression,
    MemberAccessExpression,
    IndexExpression,
    Argument,
    CallExpression,
    BinaryExpression,
    UnaryExpression,
    AssignmentExpression,
    ConditionalExpression,
    CollectionExpression,
    LambdaExpression,
    KeyValueExpression,
    TypeCastExpression,
    UiElement,
    Pattern,
    ParenthesizedExpression,
    SyntaxList
}

public enum LiteralSyntaxKind : byte
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

public enum CallSyntaxKind : byte
{
    Function,
    Method,
    Constructor,
    Macro,
    SuperConstructor,
    ThisConstructor
}

public enum ControlFlowKind : byte
{
    Expression,
    If,
    Loop,
    Switch,
    Try,
    Using,
    Lock,
    Defer,
    Goroutine,
    Branch,
    Throw,
    NoOp
}

public enum SqlOperationKind : byte
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
    /// <summary>
    /// Unique non-zero node identifier within the owning <see cref="SyntaxDocument"/>.
    /// </summary>
    public int NodeId { get; set; } = 1;

    /// <summary>
    /// Pointer-compatible handle derived from <see cref="NodeId"/> for fast identity sets and stacks.
    /// </summary>
    public IntPtr Id => (IntPtr)NodeId;

    public TextSpan Span { get; set; }
    public abstract SyntaxKind SyntaxKind { get; }
    public virtual string Type => GetCanonicalKindString();
    public string? FieldRole { get; set; }
    public bool IsNamed => true;

    protected virtual string GetCanonicalKindString() => "syntax_node";

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

    public IReadOnlyList<SyntaxNode> Children { get; set; } = [];
    public IReadOnlyList<SyntaxNode> NamedChildren => Children;

    public SyntaxNode? ResolveSyntheticField(string fieldName) =>
        fieldName switch
        {
            SyntaxFields.Value when this is VariableDeclSyntax { Initializer: { } varInit } => varInit,
            SyntaxFields.Value when this is MemberDeclSyntax { Initializer: { } memInit } => memInit,
            SyntaxFields.Value when this is ArgumentSyntax { Value: { } argVal } => argVal,
            SyntaxFields.Value when this is ParameterDeclSyntax { DefaultValue: { } paramDef } => paramDef,
            SyntaxFields.Value when this is KeyValueExprSyntax { Value: { } kvVal } => kvVal,
            SyntaxFields.Key when this is KeyValueExprSyntax { Key: { } kvKey } => kvKey,
            SyntaxFields.Value when this is SyntaxList { Kind: SyntaxListKind.Initializer } && Children.Count > 0 => Children[^1],
            SyntaxFields.Right when this is AssignmentExprSyntax { Right: { } assignRight } => assignRight,
            SyntaxFields.Left when this is AssignmentExprSyntax { Left: { } assignLeft } => assignLeft,
            SyntaxFields.Right when this is BinaryExprSyntax { Right: { } binRight } => binRight,
            SyntaxFields.Left when this is BinaryExprSyntax { Left: { } binLeft } => binLeft,
            SyntaxFields.Condition when this is ControlFlowStmtSyntax { Condition: { } cfCond } => cfCond,
            SyntaxFields.Condition when this is ConditionalExprSyntax { Condition: { } condExpr } => condExpr,
            SyntaxFields.Consequence when this is ConditionalExprSyntax { WhenTrue: { } whenTrue } => whenTrue,
            SyntaxFields.Alternative when this is ConditionalExprSyntax { WhenFalse: { } whenFalse } => whenFalse,
            SyntaxFields.Object when this is IndexExprSyntax { Target: { } idxTarget } => idxTarget,
            SyntaxFields.Index or SyntaxFields.Subscript when this is IndexExprSyntax { Index: { } idxVal } => idxVal,
            SyntaxFields.Type when this is ParameterDeclSyntax { ParameterType: { } paramType } => paramType,
            SyntaxFields.Type when this is VariableDeclSyntax { VariableType: { } varType } => varType,
            SyntaxFields.Type when this is MemberDeclSyntax { MemberType: { } memType } => memType,
            SyntaxFields.Type when this is CatchClauseSyntax { ExceptionType: { } excType } => excType,
            SyntaxFields.Receiver when this is FunctionDeclSyntax { Receiver: { } fnRecv } => fnRecv,
            "trait" when this is ImplDeclSyntax { ImplementedTrait: { } implTrait } => implTrait,
            SyntaxFields.Type when this is ImplDeclSyntax { TargetType: { } implTarget } => implTarget,
            _ => null
        };

    public SyntaxPoint StartPosition => new(Span.StartLine, Span.StartColumn);
    public SyntaxPoint EndPosition => new(Span.EndLine, Span.EndColumn);
    public int StartIndex => Span.StartByte;
    public int EndIndex => Span.EndByte;

    public IEnumerable<SyntaxNode> DescendantsAndSelf()
    {
        yield return this;
        for (int i = 0; i < Children.Count; i++)
        {
            foreach (var descendant in Children[i].DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    public IEnumerable<SyntaxNode> Descendants()
    {
        for (int i = 0; i < Children.Count; i++)
        {
            foreach (var descendant in Children[i].DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Gets the source code slice corresponding to this syntax node without caching duplicate fragments on the node.
    /// Prefer <see cref="AsSpan"/> or <see cref="TextSpan"/> for zero-allocation inspection.
    /// </summary>
    public string Text => Document != null ? Document.GetText(Span.StartByte, Span.EndByte) : string.Empty;

    /// <summary>
    /// Zero-allocation slice into the owning document's source text.
    /// </summary>
    public ReadOnlySpan<char> AsSpan() => Document != null ? Document.Slice(Span) : ReadOnlySpan<char>.Empty;

    public ReadOnlySpan<char> TextSpan => AsSpan();

    public bool SpanEquals(ReadOnlySpan<char> other, StringComparison comparison = StringComparison.Ordinal) =>
        AsSpan().Equals(other, comparison);

    public bool SpanStartsWith(ReadOnlySpan<char> prefix, StringComparison comparison = StringComparison.Ordinal) =>
        AsSpan().StartsWith(prefix, comparison);

    public bool SpanContains(ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal) =>
        AsSpan().Contains(value, comparison);

    /// <summary>
    /// Zero-allocation lookup for the first child of type <typeparamref name="T"/>.
    /// Replaces <c>Children.OfType&lt;T&gt;().FirstOrDefault()</c> to avoid LINQ iterator allocations.
    /// </summary>
    public T? FirstChildOfType<T>() where T : SyntaxNode
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is T match) return match;
        }
        return null;
    }

    /// <summary>
    /// Zero-allocation lookup for the last child of type <typeparamref name="T"/>.
    /// Replaces <c>Children.OfType&lt;T&gt;().LastOrDefault()</c> to avoid LINQ iterator allocations.
    /// </summary>
    public T? LastChildOfType<T>() where T : SyntaxNode
    {
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is T match) return match;
        }
        return null;
    }

    /// <summary>
    /// Zero-allocation lookup for the N-th (0-based) child of type <typeparamref name="T"/>.
    /// </summary>
    public T? NthChildOfType<T>(int index) where T : SyntaxNode
    {
        int seen = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is T match)
            {
                if (seen == index) return match;
                seen++;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the initializer text only when it is a bounded non-lambda expression (or SQL candidate),
    /// inspecting <see cref="AsSpan"/> first to avoid allocating large multi-line fragments.
    /// </summary>
    public string GetBoundedInitializerText(int maxLength = 512)
    {
        if (this is LambdaExprSyntax) return string.Empty;
        var span = AsSpan().Trim();
        if (span.IsEmpty) return string.Empty;
        if (span.Length <= maxLength) return span.ToString();
        if (span.Length <= 2048 && NestedSqlParser.FastCheckSqlCandidate(span)) return span.ToString();
        return string.Empty;
    }

    public string ToFullString() => Text;

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
        return ResolveSyntheticField(fieldName);
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

    public override string ToString() => $"{GetType().Name}({SyntaxKind}) [{Span.StartLine + 1}:{Span.StartColumn + 1}]";
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
    public const string Condition = "condition";
    public const string Consequence = "consequence";
    public const string Alternative = "alternative";
    public const string Initializer = "initializer";
    public const string Increment = "increment";
    public const string Update = "update";
    public const string Finalizer = "finalizer";
    public const string Argument = "argument";
    public const string Receiver = "receiver";
    public const string Trait = "trait";
}

/// <summary>
/// Root Universal Syntax Tree document representing a parsed source or schema file.
/// </summary>
public sealed class SyntaxDocument : SyntaxNode, IDisposable
{
    public override SyntaxKind SyntaxKind => SyntaxKind.Document;
    protected override string GetCanonicalKindString() => "document";

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
    internal sbyte HasParallelBatchCallsCache = -1;

    private Dictionary<string, FunctionDeclSyntax>? _functionsByName;

    private Dictionary<string, FunctionDeclSyntax> EnsureFunctionsByName()
    {
        if (_functionsByName != null) return _functionsByName;
        var dict = new Dictionary<string, FunctionDeclSyntax>(AllFunctions.Count, StringComparer.Ordinal);
        for (int i = 0; i < AllFunctions.Count; i++)
        {
            var fn = AllFunctions[i];
            var fnName = !string.IsNullOrEmpty(fn.Name)
                ? fn.Name
                : (fn.GetChildForField(SyntaxFields.Name) ?? fn.FirstChildOfType<IdentifierExprSyntax>())?.Text;
            if (!string.IsNullOrEmpty(fnName) && !dict.ContainsKey(fnName))
            {
                dict[fnName] = fn;
            }
        }
        _functionsByName = dict;
        return dict;
    }

    public FunctionDeclSyntax? FindFunctionByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return EnsureFunctionsByName().TryGetValue(name, out var found) ? found : null;
    }

    public FunctionDeclSyntax? FindFunctionByName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty) return null;
        var lookup = EnsureFunctionsByName().GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(name, out var found) ? found : null;
    }

    public ReadOnlySpan<char> Slice(TextSpan span) =>
        span.StartByte >= 0 && span.EndByte <= SourceText.Length && span.EndByte >= span.StartByte
            ? SourceText.AsSpan(span.StartByte, span.EndByte - span.StartByte)
            : ReadOnlySpan<char>.Empty;

    public ReadOnlySpan<char> AsSpan(SourceSlice slice) =>
        slice.Start >= 0 && slice.Length > 0 && slice.Start + slice.Length <= SourceText.Length
            ? SourceText.AsSpan(slice.Start, slice.Length)
            : ReadOnlySpan<char>.Empty;

    public string GetText(SourceSlice slice) =>
        slice.Start >= 0 && slice.Length > 0 && slice.Start + slice.Length <= SourceText.Length
            ? SyntaxStringPool.Intern(SourceText.AsSpan(slice.Start, slice.Length))
            : string.Empty;

    public string GetText(int startIndex, int endIndex) =>
        startIndex >= 0 && endIndex <= SourceText.Length && endIndex >= startIndex
            ? SourceText.Substring(startIndex, endIndex - startIndex)
            : string.Empty;

    public void Dispose()
    {
        Children = [];
        Imports = [];
        Declarations = [];
        TopLevelStatements = [];
        AllTypes = [];
        AllFunctions = [];
        AllMembers = [];
        AllVariables = [];
        AllCalls = [];
        AllSqlStatements = [];
        Diagnostics = [];
        _functionsByName = null;
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
// DECLARATIONS (9 Canonical Node Types)
// ============================================================================

public abstract class DeclarationSyntax : SyntaxNode
{
    private string? _explicitName;
    public SourceSlice NameSlice { get; set; }
    public bool HasName => _explicitName is { Length: > 0 } || NameSlice.Length > 0;

    public ReadOnlySpan<char> NameSpan =>
        _explicitName != null
            ? _explicitName.AsSpan()
            : (Document != null ? Document.AsSpan(NameSlice) : ReadOnlySpan<char>.Empty);

    public string Name
    {
        get => _explicitName ?? (NameSlice.Length > 0 && Document != null ? (_explicitName = Document.GetText(NameSlice)) : string.Empty);
        set => _explicitName = value;
    }

    public SyntaxModifiers Modifiers { get; set; }
}

public sealed class NamespaceDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.NamespaceDeclaration;
    protected override string GetCanonicalKindString() => "namespace_declaration";
}

public sealed class ImportDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ImportDeclaration;
    public string ModulePath { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public bool IsWildcard { get; set; }
    public bool IsReExport { get; set; }
    public bool IsStatic { get; set; }
    protected override string GetCanonicalKindString() => IsReExport ? "export_statement" : "import_statement";
}

public sealed class TypeDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.TypeDeclaration;
    public TypeSyntaxKind Kind { get; set; } = TypeSyntaxKind.Class;
    protected override string GetCanonicalKindString() => Kind switch
    {
        TypeSyntaxKind.Class => "class_declaration",
        TypeSyntaxKind.Interface => "interface_declaration",
        TypeSyntaxKind.Struct => "struct_declaration",
        TypeSyntaxKind.Enum => "enum_declaration",
        TypeSyntaxKind.Record => "record_declaration",
        TypeSyntaxKind.Trait => "trait_item",
        TypeSyntaxKind.Protocol => "protocol_declaration",
        TypeSyntaxKind.ObjectSingleton => "object_declaration",
        TypeSyntaxKind.CompanionObject => "companion_object",
        TypeSyntaxKind.TypeAlias => "type_alias_declaration",
        TypeSyntaxKind.Union => "union_item",
        TypeSyntaxKind.GraphQlType => "graphql_type_declaration",
        _ => "class_declaration"
    };
}

public sealed class ImplDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ImplDeclaration;
    public TypeRefSyntax? TargetType { get; set; }
    public TypeRefSyntax? ImplementedTrait { get; set; }
    protected override string GetCanonicalKindString() => "impl_item";
}

public sealed class FunctionDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.FunctionDeclaration;
    public FunctionSyntaxKind Kind { get; set; } = FunctionSyntaxKind.Method;
    public ParameterDeclSyntax? Receiver { get; set; }
    public IReadOnlyList<ParameterDeclSyntax> Parameters { get; set; } = [];
    public BlockStmtSyntax? Body { get; set; }
    public ExprSyntax? ExpressionBody { get; set; }
    public CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowSummary? DataflowSummary { get; set; }
    public bool IsAsync => Modifiers.HasFlag(SyntaxModifiers.Async);
    protected override string GetCanonicalKindString() => Kind switch
    {
        FunctionSyntaxKind.Method => "method_declaration",
        FunctionSyntaxKind.Function => "function_declaration",
        FunctionSyntaxKind.Constructor => "constructor_declaration",
        FunctionSyntaxKind.Destructor => "destructor_declaration",
        FunctionSyntaxKind.Accessor => "accessor_declaration",
        FunctionSyntaxKind.Operator => "operator_declaration",
        FunctionSyntaxKind.Macro => "macro_definition",
        _ => "method_declaration"
    };
}

public sealed class ParameterDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ParameterDeclaration;
    public TypeRefSyntax? ParameterType { get; set; }
    public ExprSyntax? DefaultValue { get; set; }
    public ParameterPassingMode PassingMode { get; set; } = ParameterPassingMode.Value;
    protected override string GetCanonicalKindString() => PassingMode switch
    {
        ParameterPassingMode.Receiver or ParameterPassingMode.MutBorrow => "self_parameter",
        ParameterPassingMode.VariadicArgs => "rest_pattern",
        ParameterPassingMode.VariadicKwargs => "dictionary_splat_pattern",
        _ => "parameter"
    };
}

public sealed class MemberDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.MemberDeclaration;
    public MemberSyntaxKind Kind { get; set; } = MemberSyntaxKind.Field;
    public TypeRefSyntax? MemberType { get; set; }
    public ExprSyntax? Initializer { get; set; }
    public string? ResolvedValue { get; set; }
    public bool IsDecoded { get; set; }
    protected override string GetCanonicalKindString() => Kind switch
    {
        MemberSyntaxKind.Field => "field_declaration",
        MemberSyntaxKind.Property => "property_declaration",
        MemberSyntaxKind.Event => "event_declaration",
        MemberSyntaxKind.Constant => "constant_declaration",
        MemberSyntaxKind.EnumMember => "enum_member_declaration",
        _ => "field_declaration"
    };
}

public sealed class VariableDeclSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.VariableDeclaration;
    public TypeRefSyntax? VariableType { get; set; }
    public ExprSyntax? Initializer { get; set; }
    public bool IsConstant { get; set; }
    public bool IsDeclarator { get; set; }
    public string Scope { get; set; } = "local";
    public string? ResolvedValue { get; set; }
    public bool IsDecoded { get; set; }
    protected override string GetCanonicalKindString() => IsDeclarator ? "variable_declarator" : "variable_declaration";
}

public sealed class AnnotationSyntax : DeclarationSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.Annotation;
    public IReadOnlyList<ArgumentSyntax> Arguments { get; set; } = [];
    protected override string GetCanonicalKindString() => "attribute";
}

public sealed class BaseTypeClauseSyntax : SyntaxNode
{
    public override SyntaxKind SyntaxKind => SyntaxKind.BaseTypeClause;
    public bool IsImplements { get; set; }
    protected override string GetCanonicalKindString() => IsImplements ? "implements_clause" : "base_list";
}

// ============================================================================
// TYPE REFERENCES (1 Canonical Node Type)
// ============================================================================

public sealed class TypeRefSyntax : SyntaxNode
{
    private string? _explicitName;
    public override SyntaxKind SyntaxKind => SyntaxKind.TypeReference;
    public TypeRefKind Kind { get; set; } = TypeRefKind.Named;
    public SourceSlice NameSlice { get; set; }

    public ReadOnlySpan<char> NameSpan =>
        _explicitName != null
            ? _explicitName.AsSpan()
            : (NameSlice.Length > 0 && Document != null ? Document.AsSpan(NameSlice) : AsSpan());

    public string Name
    {
        get => _explicitName ?? (NameSlice.Length > 0 && Document != null ? Document.GetText(NameSlice) : Text);
        set => _explicitName = value;
    }

    protected override string GetCanonicalKindString() => Kind switch
    {
        TypeRefKind.Generic => "generic_type",
        TypeRefKind.Array => "array_type",
        TypeRefKind.Slice => "slice_type",
        TypeRefKind.Tuple => "tuple_type",
        TypeRefKind.Pointer => "pointer_type",
        TypeRefKind.Reference => "reference_type",
        TypeRefKind.Nullable => "nullable_type",
        TypeRefKind.Union => "union_type",
        TypeRefKind.Intersection => "intersection_type",
        TypeRefKind.Function => "function_type",
        TypeRefKind.Channel => "channel_type",
        TypeRefKind.Annotation => "type_annotation",
        TypeRefKind.Predefined => "predefined_type",
        _ => "type_identifier"
    };
}

// ============================================================================
// STATEMENTS (5 Canonical Node Types)
// ============================================================================

public abstract class StatementSyntax : SyntaxNode;

public sealed class BlockStmtSyntax : StatementSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.BlockStatement;
    public bool IsUnsafe { get; set; }
    protected override string GetCanonicalKindString() => "block";
}

public sealed class ControlFlowStmtSyntax : StatementSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ControlFlowStatement;
    public ControlFlowKind Kind { get; set; } = ControlFlowKind.Expression;
    public ExprSyntax? Condition { get; set; }
    public SyntaxNode? Body { get; set; }
    public SyntaxNode? Alternative { get; set; }
    protected override string GetCanonicalKindString() => Kind switch
    {
        ControlFlowKind.Expression => "expression_statement",
        ControlFlowKind.If => "if_statement",
        ControlFlowKind.Loop => "for_statement",
        ControlFlowKind.Switch => "switch_statement",
        ControlFlowKind.Try => "try_statement",
        ControlFlowKind.Using => "using_statement",
        ControlFlowKind.Lock => "lock_statement",
        ControlFlowKind.Defer => "defer_statement",
        ControlFlowKind.Goroutine => "go_statement",
        ControlFlowKind.Branch => "break_statement",
        ControlFlowKind.Throw => "throw_statement",
        ControlFlowKind.NoOp => "pass_statement",
        _ => "expression_statement"
    };
}

public sealed class CatchClauseSyntax : StatementSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.CatchClause;
    public TypeRefSyntax? ExceptionType { get; set; }
    public BlockStmtSyntax? Body { get; set; }
    protected override string GetCanonicalKindString() => "catch_clause";
}

public sealed class ReturnStmtSyntax : StatementSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ReturnStatement;
    public ExprSyntax? Value { get; set; }
    protected override string GetCanonicalKindString() => "return_statement";
}

public sealed record SqlTableRefSyntax(
    string TableName,
    string? Schema = null,
    string? Alias = null,
    bool IsWriteTarget = false
);

public sealed class SqlStmtSyntax : StatementSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.SqlStatement;
    public SqlOperationKind Operation { get; set; } = SqlOperationKind.Unknown;
    public string OperationVerb { get; set; } = string.Empty;
    public IReadOnlyList<SqlTableRefSyntax> Tables { get; set; } = [];
    public IReadOnlyList<string> Columns { get; set; } = [];
    public IReadOnlyList<string> Procedures { get; set; } = [];
    public string RawSql { get; set; } = string.Empty;
    protected override string GetCanonicalKindString() => "sql_statement";
}

// ============================================================================
// EXPRESSIONS & STRUCTURAL LISTS
// ============================================================================

public abstract class ExprSyntax : SyntaxNode
{
    /// <summary>
    /// Pre-evaluated constant or intra-procedural dataflow value computed by <see cref="SyntaxValueEvaluator"/>.
    /// </summary>
    public string? ResolvedValue { get; set; }
    public bool IsDecoded { get; set; }

    /// <summary>
    /// Embedded SQL or GraphQL syntax node parsed after value evaluation in Layer 3.
    /// </summary>
    public SyntaxNode? EmbeddedSyntax { get; set; }
}

public sealed class LiteralExprSyntax : ExprSyntax
{
    private string? _explicitValueText;
    public override SyntaxKind SyntaxKind => SyntaxKind.LiteralExpression;
    public LiteralSyntaxKind LiteralKind { get; set; } = LiteralSyntaxKind.String;
    public SourceSlice ValueSlice { get; set; }
    public bool HasEscapeSequences { get; set; }

    public ReadOnlySpan<char> ValueSpan =>
        _explicitValueText != null
            ? _explicitValueText.AsSpan()
            : (ValueSlice.Length > 0 && Document != null ? Document.AsSpan(ValueSlice) : AsSpan());

    public string ValueText
    {
        get
        {
            if (_explicitValueText != null) return _explicitValueText;
            if (HasEscapeSequences)
            {
                return AstValueResolver.Unquote(AsSpan());
            }
            if (ValueSlice.Length > 0 && Document != null)
            {
                return Document.GetText(ValueSlice);
            }
            return LiteralKind is LiteralSyntaxKind.String or LiteralSyntaxKind.RawString or LiteralSyntaxKind.Char
                ? AstValueResolver.Unquote(AsSpan())
                : Text;
        }
        set => _explicitValueText = value;
    }

    protected override string GetCanonicalKindString() => LiteralKind switch
    {
        LiteralSyntaxKind.String => "string_literal",
        LiteralSyntaxKind.RawString => "raw_string_literal",
        LiteralSyntaxKind.Char => "character_literal",
        LiteralSyntaxKind.Integer => "integer_literal",
        LiteralSyntaxKind.Float => "float_literal",
        LiteralSyntaxKind.Boolean => "boolean_literal",
        LiteralSyntaxKind.Null => "null_literal",
        LiteralSyntaxKind.Regex => "regex",
        _ => "string_literal"
    };
}

public sealed class InterpolatedStringExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.InterpolatedStringExpression;
    protected override string GetCanonicalKindString() => "interpolated_string_expression";
}

public sealed class IdentifierExprSyntax : ExprSyntax
{
    private string? _explicitName;
    public override SyntaxKind SyntaxKind => SyntaxKind.IdentifierExpression;
    public IdentifierRole Role { get; set; } = IdentifierRole.Symbol;

    public ReadOnlySpan<char> NameSpan =>
        _explicitName != null ? _explicitName.AsSpan() : AsSpan();

    public string Name
    {
        get => _explicitName ?? Text;
        set => _explicitName = value;
    }

    protected override string GetCanonicalKindString() => Role switch
    {
        IdentifierRole.Type => "type_identifier",
        IdentifierRole.Member => "property_identifier",
        IdentifierRole.SelfOrSuper => "this",
        _ => "identifier"
    };
}

public sealed class MemberAccessExprSyntax : ExprSyntax
{
    private string? _explicitMemberName;
    public override SyntaxKind SyntaxKind => SyntaxKind.MemberAccessExpression;
    public ExprSyntax? Object { get; set; }
    public SourceSlice MemberNameSlice { get; set; }
    public bool HasExplicitMemberName => _explicitMemberName != null;

    public ReadOnlySpan<char> MemberNameSpan =>
        _explicitMemberName != null
            ? _explicitMemberName.AsSpan()
            : (Document != null ? Document.AsSpan(MemberNameSlice) : ReadOnlySpan<char>.Empty);

    public string MemberName
    {
        get => _explicitMemberName ?? (MemberNameSlice.Length > 0 && Document != null ? Document.GetText(MemberNameSlice) : string.Empty);
        set => _explicitMemberName = value;
    }

    public string Operator { get; set; } = ".";

    protected override string GetCanonicalKindString() => Operator == "::" ? "scoped_identifier" : "member_access_expression";
}

public sealed class IndexExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.IndexExpression;
    public ExprSyntax? Target { get; set; }
    public ExprSyntax? Index { get; set; }
    protected override string GetCanonicalKindString() => "index_expression";
}

public sealed class ArgumentSyntax : ExprSyntax
{
    private string? _explicitName;
    public override SyntaxKind SyntaxKind => SyntaxKind.Argument;
    public SourceSlice NameSlice { get; set; }

    public ReadOnlySpan<char> NameSpan =>
        _explicitName != null
            ? _explicitName.AsSpan()
            : (Document != null ? Document.AsSpan(NameSlice) : ReadOnlySpan<char>.Empty);

    public string? Name
    {
        get => _explicitName ?? (NameSlice.Length > 0 && Document != null ? Document.GetText(NameSlice) : null);
        set => _explicitName = value;
    }

    public ExprSyntax? Value { get; set; }
    public bool IsSpread { get; set; }
    protected override string GetCanonicalKindString() => IsSpread ? "spread_element" : "argument";
}

public sealed class CallExprSyntax : ExprSyntax
{
    private string? _explicitMethodName;
    public override SyntaxKind SyntaxKind => SyntaxKind.CallExpression;
    public CallSyntaxKind CallKind { get; set; } = CallSyntaxKind.Method;
    public ExprSyntax? Callee { get; set; }
    public ExprSyntax? Receiver { get; set; }
    public SourceSlice MethodNameSlice { get; set; }

    public ReadOnlySpan<char> MethodNameSpan =>
        _explicitMethodName != null
            ? _explicitMethodName.AsSpan()
            : (Document != null ? Document.AsSpan(MethodNameSlice) : ReadOnlySpan<char>.Empty);

    public string MethodName
    {
        get => _explicitMethodName ?? (MethodNameSlice.Length > 0 && Document != null ? (_explicitMethodName = Document.GetText(MethodNameSlice)) : string.Empty);
        set => _explicitMethodName = value;
    }

    public IReadOnlyList<ArgumentSyntax> Arguments { get; set; } = [];

    protected override string GetCanonicalKindString() => CallKind switch
    {
        CallSyntaxKind.Constructor when MethodNameSlice.Length == 0 && Callee == null && _explicitMethodName == null => "implicit_object_creation_expression",
        CallSyntaxKind.Constructor or CallSyntaxKind.SuperConstructor or CallSyntaxKind.ThisConstructor => "object_creation_expression",
        CallSyntaxKind.Macro => "macro_invocation",
        _ => "call_expression"
    };
}

public sealed class BinaryExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.BinaryExpression;
    public ExprSyntax? Left { get; set; }
    public string Operator { get; set; } = string.Empty;
    public ExprSyntax? Right { get; set; }
    protected override string GetCanonicalKindString() => "binary_expression";
}

public sealed class UnaryExprSyntax : ExprSyntax
{
    private bool _isAwait;
    private bool _isTryPropagate;
    public override SyntaxKind SyntaxKind => SyntaxKind.UnaryExpression;
    public string Operator { get; set; } = string.Empty;
    public ExprSyntax? Operand { get; set; }
    public bool IsPrefix { get; set; } = true;
    public bool IsAwait
    {
        get => _isAwait || Operator == "await";
        set => _isAwait = value;
    }
    public bool IsTryPropagate
    {
        get => _isTryPropagate || Operator == "?";
        set => _isTryPropagate = value;
    }
    protected override string GetCanonicalKindString() =>
        IsAwait ? "await_expression" : (IsTryPropagate ? "try_expression" : "unary_expression");
}

public sealed class AssignmentExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.AssignmentExpression;
    public SyntaxNode? Left { get; set; }
    public string Operator { get; set; } = "=";
    public ExprSyntax? Right { get; set; }
    protected override string GetCanonicalKindString() => "assignment_expression";
}

public enum CollectionSyntaxKind : byte
{
    Array,
    Object,
    Dictionary,
    Tuple,
    Set,
    Initializer
}

public sealed class ConditionalExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ConditionalExpression;
    public ExprSyntax? Condition { get; set; }
    public ExprSyntax? WhenTrue { get; set; }
    public ExprSyntax? WhenFalse { get; set; }
    protected override string GetCanonicalKindString() => "conditional_expression";
}

public sealed class CollectionExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.CollectionExpression;
    public CollectionSyntaxKind Kind { get; set; } = CollectionSyntaxKind.Array;
    public IReadOnlyList<ExprSyntax> Elements { get; set; } = [];
    protected override string GetCanonicalKindString() => Kind switch
    {
        CollectionSyntaxKind.Array => "array_expression",
        CollectionSyntaxKind.Object => "object",
        CollectionSyntaxKind.Dictionary => "dictionary",
        CollectionSyntaxKind.Tuple => "tuple_expression",
        CollectionSyntaxKind.Set => "set",
        CollectionSyntaxKind.Initializer => "composite_literal",
        _ => "array_expression"
    };
}

public sealed class LambdaExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.LambdaExpression;
    public IReadOnlyList<ParameterDeclSyntax> Parameters { get; set; } = [];
    public SyntaxNode? Body { get; set; }
    public bool IsAsync { get; set; }
    protected override string GetCanonicalKindString() => "lambda_expression";
}

public sealed class KeyValueExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.KeyValueExpression;
    public ExprSyntax? Key { get; set; }
    public SourceSlice KeySlice { get; set; }
    public ExprSyntax? Value { get; set; }

    public ReadOnlySpan<char> KeySpan =>
        Document != null && !KeySlice.IsEmpty ? Document.AsSpan(KeySlice) : (Key != null ? Key.AsSpan() : ReadOnlySpan<char>.Empty);

    public string KeyText =>
        Document != null && !KeySlice.IsEmpty ? Document.GetText(KeySlice) : (Key?.Text ?? string.Empty);

    protected override string GetCanonicalKindString() => "pair";
}

public sealed class TypeCastExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.TypeCastExpression;
    public ExprSyntax? Operand { get; set; }
    public TypeRefSyntax? TargetType { get; set; }
    public bool IsGenericInstantiation { get; set; }
    protected override string GetCanonicalKindString() => IsGenericInstantiation ? "generic_function" : "cast_expression";
}

public sealed class UiElementSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.UiElement;
    public SourceSlice TagNameSlice { get; set; }
    public bool IsSelfClosing { get; set; }
    public bool IsClosing { get; set; }
    public bool IsFragment { get; set; }
    protected override string GetCanonicalKindString() =>
        IsSelfClosing ? "jsx_self_closing_element" :
        IsClosing ? "jsx_closing_element" :
        IsFragment ? "jsx_fragment" : "jsx_opening_element";
}

public sealed class PatternSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.Pattern;
    public PatternKind Kind { get; set; } = PatternKind.Binding;
    protected override string GetCanonicalKindString() => Kind switch
    {
        PatternKind.Object => "object_pattern",
        PatternKind.Array => "array_pattern",
        PatternKind.Tuple => "tuple_pattern",
        PatternKind.Rest => "rest_pattern",
        PatternKind.As => "as_pattern",
        PatternKind.Pair => "pair_pattern",
        _ => "assignment_pattern"
    };
}

public sealed class ParenthesizedExprSyntax : ExprSyntax
{
    public override SyntaxKind SyntaxKind => SyntaxKind.ParenthesizedExpression;
    public ExprSyntax? Expression { get; set; }
    public bool IsInterpolation { get; set; }
    protected override string GetCanonicalKindString() => IsInterpolation ? "interpolation" : "parenthesized_expression";
}

public sealed class SyntaxList : SyntaxNode
{
    public override SyntaxKind SyntaxKind => SyntaxKind.SyntaxList;
    public SyntaxListKind Kind { get; set; } = SyntaxListKind.Expressions;
    protected override string GetCanonicalKindString() => Kind switch
    {
        SyntaxListKind.Arguments => "argument_list",
        SyntaxListKind.Parameters => "parameter_list",
        SyntaxListKind.TypeArguments => "type_argument_list",
        SyntaxListKind.TypeParameters => "type_parameter_list",
        SyntaxListKind.Attributes => "attribute_list",
        SyntaxListKind.Declarations => "declaration_list",
        SyntaxListKind.Imports => "import_clause",
        SyntaxListKind.Exports => "export_clause",
        SyntaxListKind.SwitchArms => "switch_section",
        SyntaxListKind.Initializer => "arrow_expression_clause",
        SyntaxListKind.Clauses => "finally_clause",
        SyntaxListKind.TopLevelStatement => "global_statement",
        SyntaxListKind.Expressions => "expression_list",
        _ => "syntax_list"
    };
}

/// <summary>
/// Resolves canonical UAST <see cref="SyntaxNode"/> instances against canonical or legacy kind aliases without storing language-specific grammar names on UAST nodes.
/// </summary>
public static class SyntaxKindResolver
{
    public static bool Matches(SyntaxNode? node, string expectedKind)
    {
        if (node == null || node.Id == IntPtr.Zero || string.IsNullOrEmpty(expectedKind))
            return false;
        if (string.Equals(node.Type, expectedKind, StringComparison.Ordinal))
            return true;

        return expectedKind switch
        {
            "call_expression" or "invocation_expression" or "method_invocation" or "call" or "tagged_template_expression" =>
                node is CallExprSyntax { CallKind: CallSyntaxKind.Method or CallSyntaxKind.Function },
            "new_expression" or "object_creation_expression" or "implicit_object_creation_expression" or "explicit_constructor_invocation" or "super_constructor_invocation" =>
                node is CallExprSyntax { CallKind: CallSyntaxKind.Constructor or CallSyntaxKind.SuperConstructor or CallSyntaxKind.ThisConstructor },
            "macro_invocation" =>
                node is CallExprSyntax { CallKind: CallSyntaxKind.Macro },
            "member_expression" or "member_access_expression" or "selector_expression" or "field_access" or "field_expression" =>
                node is MemberAccessExprSyntax { Operator: not "::" },
            "scoped_identifier" or "qualified_name" =>
                node is MemberAccessExprSyntax { Operator: "::" } or MemberAccessExprSyntax,
            "attribute" =>
                node is AnnotationSyntax or MemberAccessExprSyntax,
            "decorator" or "annotation" or "marker_annotation" or "attribute_item" or "inner_attribute_item" =>
                node is AnnotationSyntax,
            "class_declaration" or "class_definition" or "class_expression" or "abstract_class_declaration" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Class },
            "struct_declaration" or "struct_item" or "struct_type" or "type_spec" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Struct },
            "type_declaration" =>
                node is TypeDeclSyntax or SyntaxList { Kind: SyntaxListKind.Declarations },
            "record_declaration" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Record },
            "interface_declaration" or "interface_type" or "annotation_type_declaration" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface },
            "enum_declaration" or "enum_item" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Enum },
            "trait_item" or "trait_declaration" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Trait },
            "type_alias_declaration" or "type_item" or "delegate_declaration" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.TypeAlias },
            "union_item" or "union_declaration" =>
                node is TypeDeclSyntax { Kind: TypeSyntaxKind.Union },
            "impl_item" =>
                node is ImplDeclSyntax,
            "method_declaration" or "method_definition" or "method_signature" =>
                node is FunctionDeclSyntax { Kind: FunctionSyntaxKind.Method or FunctionSyntaxKind.Constructor },
            "function_declaration" or "function_definition" or "function_item" or "function_signature_item" or "local_function_statement" or "generator_function_declaration" =>
                node is FunctionDeclSyntax { Kind: FunctionSyntaxKind.Function or FunctionSyntaxKind.Method },
            "constructor_declaration" or "compact_constructor_declaration" or "constructor" =>
                node is FunctionDeclSyntax { Kind: FunctionSyntaxKind.Constructor } || (node is FunctionDeclSyntax fd && fd.NameSpan is "constructor"),
            "arrow_function" or "function_expression" or "lambda_expression" or "lambda" or "func_literal" or "closure_expression" or "function" or "generator_function" =>
                node is LambdaExprSyntax,
            "parameter" or "formal_parameter" or "required_parameter" or "optional_parameter" or "parameter_declaration" or "parameter_property" or "typed_parameter" or "default_parameter" or "typed_default_parameter" or "spread_parameter" or "variadic_parameter" or "variadic_parameter_declaration" or "self_parameter" or "receiver_parameter" =>
                node is ParameterDeclSyntax,
            "parameter_list" or "formal_parameters" or "parameters" =>
                node is SyntaxList { Kind: SyntaxListKind.Parameters },
            "argument_list" or "arguments" or "attribute_argument_list" or "annotation_argument_list" or "bracketed_argument_list" or "token_tree" =>
                node is SyntaxList { Kind: SyntaxListKind.Arguments },
            "argument" or "attribute_argument" or "keyword_argument" or "expression_element" or "spread_element" =>
                node is ArgumentSyntax,
            "variable_declaration" or "lexical_declaration" or "local_declaration_statement" or "local_variable_declaration" or "let_declaration" or "var_declaration" or "short_var_declaration" =>
                node is VariableDeclSyntax { IsDeclarator: false },
            "const_declaration" =>
                node is VariableDeclSyntax { IsConstant: true } or MemberDeclSyntax { Kind: MemberSyntaxKind.Constant },
            "variable_declarator" or "var_spec" =>
                node is VariableDeclSyntax { IsDeclarator: true },
            "field_declaration" or "public_field_definition" or "property_definition" or "static_item" =>
                node is MemberDeclSyntax { Kind: MemberSyntaxKind.Field },
            "property_declaration" or "property_signature" or "indexer_declaration" =>
                node is MemberDeclSyntax { Kind: MemberSyntaxKind.Property },
            "constant_declaration" or "const_spec" or "const_item" =>
                node is MemberDeclSyntax { Kind: MemberSyntaxKind.Constant } or VariableDeclSyntax { IsConstant: true },
            "enum_member_declaration" or "enum_assignment" or "enum_constant" or "enum_variant" =>
                node is MemberDeclSyntax { Kind: MemberSyntaxKind.EnumMember },
            "string" or "string_literal" or "interpreted_string_literal" or "text_block" or "string_content" or "string_fragment" or "escape_sequence" =>
                node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString },
            "raw_string_literal" or "verbatim_string_literal" =>
                node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.RawString },
            "template_string" or "interpolated_string_expression" or "format_string" or "concatenated_string" =>
                node is InterpolatedStringExprSyntax,
            "integer_literal" or "int_literal" or "integer" or "number" =>
                node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.Integer or LiteralSyntaxKind.Float },
            "boolean_literal" or "true" or "false" =>
                node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.Boolean },
            "null_literal" or "null" or "nil" or "none" or "undefined" =>
                node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.Null },
            "identifier" =>
                node is IdentifierExprSyntax,
            "type_identifier" =>
                node is IdentifierExprSyntax { Role: IdentifierRole.Type } or TypeRefSyntax { Kind: TypeRefKind.Named } or IdentifierExprSyntax,
            "property_identifier" or "field_identifier" or "shorthand_property_identifier" or "shorthand_property_identifier_pattern" =>
                node is IdentifierExprSyntax { Role: IdentifierRole.Member } or IdentifierExprSyntax,
            "pair" or "keyed_element" or "field_initializer" or "element_value_pair" or "jsx_attribute" or "import_specifier" or "export_specifier" or "import_require_clause" =>
                node is KeyValueExprSyntax,
            "object" or "struct_expression" =>
                node is CollectionExprSyntax { Kind: CollectionSyntaxKind.Object },
            "dictionary" or "dictionary_comprehension" =>
                node is CollectionExprSyntax { Kind: CollectionSyntaxKind.Dictionary },
            "array" or "array_expression" or "array_creation_expression" or "array_initializer" or "collection_expression" or "list" or "list_comprehension" or "element_value_array_initializer" =>
                node is CollectionExprSyntax { Kind: CollectionSyntaxKind.Array },
            "composite_literal" or "literal_value" or "initializer_expression" =>
                node is CollectionExprSyntax { Kind: CollectionSyntaxKind.Initializer },
            "tuple" or "tuple_expression" =>
                node is CollectionExprSyntax { Kind: CollectionSyntaxKind.Tuple },
            "subscript_expression" or "element_access_expression" or "index_expression" or "array_access" or "subscript" or "slice_expression" =>
                node is IndexExprSyntax,
            "assignment_expression" or "assignment_statement" or "assignment" or "augmented_assignment" or "augmented_assignment_expression" or "compound_assignment_expr" =>
                node is AssignmentExprSyntax,
            "binary_expression" or "binary_operator" or "boolean_operator" or "comparison_operator" =>
                node is BinaryExprSyntax,
            "unary_expression" or "unary_operator" or "prefix_unary_expression" or "postfix_unary_expression" or "update_expression" or "non_null_expression" =>
                node is UnaryExprSyntax,
            "cast_expression" or "as_expression" or "satisfies_expression" or "type_assertion" =>
                node is TypeCastExprSyntax { IsGenericInstantiation: false } or BinaryExprSyntax,
            "await_expression" or "await" =>
                node is UnaryExprSyntax { IsAwait: true },
            "try_expression" =>
                node is UnaryExprSyntax { IsTryPropagate: true },
            "parenthesized_expression" or "literal_element" =>
                node is ParenthesizedExprSyntax { IsInterpolation: false },
            "interpolation" or "template_substitution" or "formatted_expression" or "jsx_expression" =>
                node is ParenthesizedExprSyntax { IsInterpolation: true },
            "block" or "statement_block" or "statement_list" =>
                node is BlockStmtSyntax,
            "declaration_list" or "class_body" or "interface_body" or "enum_body" or "enum_variant_list" or "field_declaration_list" or "annotation_type_body" =>
                node is SyntaxList { Kind: SyntaxListKind.Declarations } or BlockStmtSyntax { Parent: TypeDeclSyntax or NamespaceDeclSyntax or ImplDeclSyntax },
            "decorated_definition" =>
                node is SyntaxList { Kind: SyntaxListKind.Declarations } or FunctionDeclSyntax or TypeDeclSyntax,
            "attribute_list" or "modifiers" or "decorator_list" =>
                node is SyntaxList { Kind: SyntaxListKind.Attributes },
            "type_argument_list" or "type_arguments" =>
                node is SyntaxList { Kind: SyntaxListKind.TypeArguments },
            "type_parameter_list" or "type_parameters" =>
                node is SyntaxList { Kind: SyntaxListKind.TypeParameters },
            "arrow_expression_clause" or "equals_value_clause" =>
                node is SyntaxList { Kind: SyntaxListKind.Initializer },
            "expression_list" =>
                node is SyntaxList { Kind: SyntaxListKind.Expressions },
            "import_clause" or "named_imports" or "import_spec_list" =>
                node is SyntaxList { Kind: SyntaxListKind.Imports },
            "export_clause" =>
                node is SyntaxList { Kind: SyntaxListKind.Exports },
            "global_statement" =>
                node is SyntaxList { Kind: SyntaxListKind.TopLevelStatement },
            "base_list" or "class_heritage" =>
                node is BaseTypeClauseSyntax,
            "extends_clause" or "extends_type_clause" or "superclass" =>
                node is BaseTypeClauseSyntax { IsImplements: false },
            "implements_clause" or "super_interfaces" or "extends_interfaces" or "interfaces" =>
                node is BaseTypeClauseSyntax { IsImplements: true },
            "import_statement" or "import_from_statement" or "import_declaration" or "import_spec" or "using_directive" or "use_declaration" or "extern_crate_declaration" =>
                node is ImportDeclSyntax { IsReExport: false },
            "export_statement" or "export_declaration" =>
                node is ImportDeclSyntax { IsReExport: true },
            "package_clause" or "package_declaration" or "namespace_declaration" or "file_scoped_namespace_declaration" or "mod_item" or "module" =>
                node is NamespaceDeclSyntax,
            "return_statement" or "return_expression" or "yield_statement" or "yield_expression" =>
                node is ReturnStmtSyntax,
            "expression_statement" =>
                node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Expression },
            "if_statement" or "if_expression" =>
                node is ControlFlowStmtSyntax { Kind: ControlFlowKind.If },
            "switch_statement" or "switch_expression" or "match_statement" or "match_expression" or "expression_switch_statement" or "type_switch_statement" or "select_statement" =>
                node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Switch },
            "for_statement" or "for_in_statement" or "foreach_statement" or "while_statement" or "do_statement" or "enhanced_for_statement" or "loop_expression" or "while_expression" or "for_expression" =>
                node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Loop },
            "try_statement" or "try_with_resources_statement" =>
                node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Try },
            "using_statement" or "with_statement" =>
                node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Using },
            "catch_clause" or "except_clause" =>
                node is CatchClauseSyntax,
            "type_annotation" or "type" =>
                node is TypeRefSyntax { Kind: TypeRefKind.Annotation } or TypeRefSyntax,
            "generic_type" or "generic_name" =>
                node is TypeRefSyntax { Kind: TypeRefKind.Generic },
            "generic_function" or "instantiation_expression" or "expression_with_type_arguments" =>
                node is TypeCastExprSyntax { IsGenericInstantiation: true } or TypeRefSyntax { Kind: TypeRefKind.Generic },
            "scoped_type_identifier" or "qualified_type" =>
                node is TypeRefSyntax,
            "pointer_type" =>
                node is TypeRefSyntax { Kind: TypeRefKind.Pointer },
            "nullable_type" =>
                node is TypeRefSyntax { Kind: TypeRefKind.Nullable },
            "predefined_type" or "primitive_type" or "integral_type" or "floating_point_type" or "boolean_type" =>
                node is TypeRefSyntax { Kind: TypeRefKind.Predefined },
            "jsx_opening_element" =>
                node is UiElementSyntax { IsSelfClosing: false, IsClosing: false, IsFragment: false },
            "jsx_self_closing_element" =>
                node is UiElementSyntax { IsSelfClosing: true },
            "jsx_closing_element" =>
                node is UiElementSyntax { IsClosing: true },
            "jsx_element" or "jsx_fragment" =>
                node is UiElementSyntax,
            "object_pattern" =>
                node is PatternSyntax { Kind: PatternKind.Object },
            "array_pattern" =>
                node is PatternSyntax { Kind: PatternKind.Array },
            "pair_pattern" =>
                node is PatternSyntax { Kind: PatternKind.Pair },
            "as_pattern" =>
                node is PatternSyntax { Kind: PatternKind.As },
            "rest_pattern" or "namespace_import" =>
                node is PatternSyntax { Kind: PatternKind.Rest } or ParameterDeclSyntax { PassingMode: ParameterPassingMode.VariadicArgs } or KeyValueExprSyntax,
            "compilation_unit" or "program" or "source_file" or "document" =>
                node is SyntaxDocument,
            _ => false
        };
    }

    public static string[] GetCanonicalNodeTypes(string expectedKind) =>
        expectedKind switch
        {
            "call_expression" or "invocation_expression" or "method_invocation" or "call" or "tagged_template_expression" =>
                ["call_expression"],
            "new_expression" or "object_creation_expression" or "implicit_object_creation_expression" or "explicit_constructor_invocation" or "super_constructor_invocation" =>
                ["object_creation_expression", "implicit_object_creation_expression"],
            "macro_invocation" =>
                ["macro_invocation"],
            "member_expression" or "member_access_expression" or "selector_expression" or "field_access" or "field_expression" =>
                ["member_access_expression"],
            "scoped_identifier" or "qualified_name" =>
                ["scoped_identifier", "member_access_expression"],
            "attribute" or "decorator" or "annotation" or "marker_annotation" or "attribute_item" or "inner_attribute_item" =>
                ["attribute", "member_access_expression"],
            "class_declaration" or "class_definition" or "class_expression" or "abstract_class_declaration" =>
                ["class_declaration"],
            "struct_declaration" or "struct_item" or "struct_type" or "type_spec" =>
                ["struct_declaration"],
            "type_declaration" =>
                ["class_declaration", "interface_declaration", "struct_declaration", "enum_declaration", "record_declaration", "trait_item", "protocol_declaration", "object_declaration", "companion_object", "type_alias_declaration", "union_item", "graphql_type_declaration", "declaration_list"],
            "record_declaration" =>
                ["record_declaration"],
            "interface_declaration" or "interface_type" or "annotation_type_declaration" =>
                ["interface_declaration"],
            "enum_declaration" or "enum_item" =>
                ["enum_declaration"],
            "trait_item" or "trait_declaration" =>
                ["trait_item"],
            "type_alias_declaration" or "type_item" or "delegate_declaration" =>
                ["type_alias_declaration"],
            "union_item" or "union_declaration" =>
                ["union_item"],
            "impl_item" =>
                ["impl_item"],
            "method_declaration" or "method_definition" or "method_signature" =>
                ["method_declaration", "constructor_declaration"],
            "function_declaration" or "function_definition" or "function_item" or "function_signature_item" or "local_function_statement" or "generator_function_declaration" =>
                ["function_declaration", "method_declaration"],
            "constructor_declaration" or "compact_constructor_declaration" or "constructor" =>
                ["constructor_declaration", "method_declaration"],
            "arrow_function" or "function_expression" or "lambda_expression" or "lambda" or "func_literal" or "closure_expression" or "function" or "generator_function" =>
                ["lambda_expression"],
            "parameter" or "formal_parameter" or "required_parameter" or "optional_parameter" or "parameter_declaration" or "parameter_property" or "typed_parameter" or "default_parameter" or "typed_default_parameter" or "spread_parameter" or "variadic_parameter" or "variadic_parameter_declaration" or "self_parameter" or "receiver_parameter" =>
                ["parameter", "self_parameter", "rest_pattern", "dictionary_splat_pattern"],
            "parameter_list" or "formal_parameters" or "parameters" =>
                ["parameter_list"],
            "argument_list" or "arguments" or "attribute_argument_list" or "annotation_argument_list" or "bracketed_argument_list" or "token_tree" =>
                ["argument_list"],
            "argument" or "attribute_argument" or "keyword_argument" or "expression_element" or "spread_element" =>
                ["argument", "spread_element"],
            "variable_declaration" or "lexical_declaration" or "local_declaration_statement" or "local_variable_declaration" or "let_declaration" or "var_declaration" or "short_var_declaration" =>
                ["variable_declaration"],
            "const_declaration" or "constant_declaration" or "const_spec" or "const_item" =>
                ["variable_declaration", "constant_declaration"],
            "variable_declarator" or "var_spec" =>
                ["variable_declarator"],
            "field_declaration" or "public_field_definition" or "property_definition" or "static_item" =>
                ["field_declaration"],
            "property_declaration" or "property_signature" or "indexer_declaration" =>
                ["property_declaration"],
            "enum_member_declaration" or "enum_assignment" or "enum_constant" or "enum_variant" =>
                ["enum_member_declaration"],
            "string" or "string_literal" or "interpreted_string_literal" or "text_block" or "string_content" or "string_fragment" or "escape_sequence" or "raw_string_literal" or "verbatim_string_literal" =>
                ["string_literal", "raw_string_literal"],
            "template_string" or "interpolated_string_expression" or "format_string" or "concatenated_string" =>
                ["interpolated_string_expression"],
            "integer_literal" or "int_literal" or "integer" or "number" =>
                ["integer_literal", "float_literal"],
            "boolean_literal" or "true" or "false" =>
                ["boolean_literal"],
            "null_literal" or "null" or "nil" or "none" or "undefined" =>
                ["null_literal"],
            "identifier" =>
                ["identifier", "type_identifier", "property_identifier", "this"],
            "type_identifier" or "scoped_type_identifier" or "qualified_type" or "type_annotation" or "type" =>
                ["type_identifier", "generic_type", "array_type", "slice_type", "tuple_type", "pointer_type", "reference_type", "nullable_type", "union_type", "intersection_type", "function_type", "channel_type", "type_annotation", "predefined_type", "identifier"],
            "property_identifier" or "field_identifier" or "shorthand_property_identifier" or "shorthand_property_identifier_pattern" =>
                ["property_identifier", "identifier"],
            "pair" or "keyed_element" or "field_initializer" or "element_value_pair" or "jsx_attribute" or "import_specifier" or "export_specifier" or "import_require_clause" =>
                ["pair"],
            "object" or "struct_expression" =>
                ["object"],
            "dictionary" or "dictionary_comprehension" =>
                ["dictionary"],
            "array" or "array_expression" or "array_creation_expression" or "array_initializer" or "collection_expression" or "list" or "list_comprehension" or "element_value_array_initializer" =>
                ["array_expression"],
            "composite_literal" or "literal_value" or "initializer_expression" =>
                ["composite_literal"],
            "tuple" or "tuple_expression" =>
                ["tuple_expression"],
            "subscript_expression" or "element_access_expression" or "index_expression" or "array_access" or "subscript" or "slice_expression" =>
                ["index_expression"],
            "assignment_expression" or "assignment_statement" or "assignment" or "augmented_assignment" or "augmented_assignment_expression" or "compound_assignment_expr" =>
                ["assignment_expression"],
            "binary_expression" or "binary_operator" or "boolean_operator" or "comparison_operator" =>
                ["binary_expression"],
            "unary_expression" or "unary_operator" or "prefix_unary_expression" or "postfix_unary_expression" or "update_expression" or "non_null_expression" =>
                ["unary_expression", "await_expression", "try_expression"],
            "cast_expression" or "as_expression" or "satisfies_expression" or "type_assertion" =>
                ["cast_expression", "binary_expression"],
            "await_expression" or "await" =>
                ["await_expression"],
            "try_expression" =>
                ["try_expression"],
            "parenthesized_expression" or "literal_element" =>
                ["parenthesized_expression"],
            "interpolation" or "template_substitution" or "formatted_expression" or "jsx_expression" =>
                ["interpolation"],
            "block" or "statement_block" or "statement_list" =>
                ["block"],
            "declaration_list" or "class_body" or "interface_body" or "enum_body" or "enum_variant_list" or "field_declaration_list" or "annotation_type_body" =>
                ["declaration_list", "block"],
            "decorated_definition" =>
                ["declaration_list", "function_declaration", "method_declaration", "class_declaration"],
            "attribute_list" or "modifiers" or "decorator_list" =>
                ["attribute_list"],
            "type_argument_list" or "type_arguments" =>
                ["type_argument_list"],
            "type_parameter_list" or "type_parameters" =>
                ["type_parameter_list"],
            "arrow_expression_clause" or "equals_value_clause" =>
                ["arrow_expression_clause"],
            "expression_list" =>
                ["expression_list"],
            "import_clause" or "named_imports" or "import_spec_list" =>
                ["import_clause"],
            "export_clause" =>
                ["export_clause"],
            "global_statement" =>
                ["global_statement"],
            "base_list" or "class_heritage" =>
                ["base_list", "implements_clause"],
            "extends_clause" or "extends_type_clause" or "superclass" =>
                ["base_list"],
            "implements_clause" or "super_interfaces" or "extends_interfaces" or "interfaces" =>
                ["implements_clause"],
            "import_statement" or "import_from_statement" or "import_declaration" or "import_spec" or "using_directive" or "use_declaration" or "extern_crate_declaration" =>
                ["import_statement"],
            "export_statement" or "export_declaration" =>
                ["export_statement"],
            "package_clause" or "package_declaration" or "namespace_declaration" or "file_scoped_namespace_declaration" or "mod_item" or "module" =>
                ["namespace_declaration"],
            "return_statement" or "return_expression" or "yield_statement" or "yield_expression" =>
                ["return_statement"],
            "expression_statement" =>
                ["expression_statement"],
            "if_statement" or "if_expression" =>
                ["if_statement"],
            "switch_statement" or "switch_expression" or "match_statement" or "match_expression" or "expression_switch_statement" or "type_switch_statement" or "select_statement" =>
                ["switch_statement"],
            "for_statement" or "for_in_statement" or "foreach_statement" or "while_statement" or "do_statement" or "enhanced_for_statement" or "loop_expression" or "while_expression" or "for_expression" =>
                ["for_statement"],
            "try_statement" or "try_with_resources_statement" =>
                ["try_statement"],
            "using_statement" or "with_statement" =>
                ["using_statement"],
            "catch_clause" or "except_clause" =>
                ["catch_clause"],
            "generic_type" or "generic_name" =>
                ["generic_type"],
            "generic_function" or "instantiation_expression" or "expression_with_type_arguments" =>
                ["generic_function", "generic_type"],
            "jsx_opening_element" =>
                ["jsx_opening_element"],
            "jsx_self_closing_element" =>
                ["jsx_self_closing_element"],
            "jsx_closing_element" =>
                ["jsx_closing_element"],
            "jsx_element" or "jsx_fragment" =>
                ["jsx_opening_element", "jsx_self_closing_element", "jsx_closing_element", "jsx_fragment"],
            "compilation_unit" or "program" or "source_file" or "document" =>
                ["document"],
            _ => [expectedKind]
        };
}
