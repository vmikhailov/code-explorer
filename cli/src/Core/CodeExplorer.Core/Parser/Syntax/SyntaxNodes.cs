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
    private const int MaxPoolCapacity = 32768;
    private static readonly ConcurrentDictionary<string, string> Pool = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> SpanLookup =
        Pool.GetAlternateLookup<ReadOnlySpan<char>>();

    public static string Intern(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return string.Empty;
        if (span.Length > MaxPooledLength) return span.ToString();
        if (SpanLookup.TryGetValue(span, out var existing)) return existing;
        var str = span.ToString();
        if (Pool.Count < MaxPoolCapacity)
        {
            SpanLookup.TryAdd(span, str);
        }
        return str;
    }

    public static string Intern(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length > MaxPooledLength) return value;
        if (Pool.TryGetValue(value, out var existing)) return existing;
        if (Pool.Count < MaxPoolCapacity)
        {
            Pool.TryAdd(value, value);
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
    Channel
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
    Throw
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
    public string RawGrammarKind { get; set; } = string.Empty;
    public string Type => RawGrammarKind;
    public string? FieldRole { get; set; }
    public bool IsNamed => true;

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
            SyntaxFields.Value when RawGrammarKind == "arrow_expression_clause" && Children.Count > 0 => Children[^1],
            SyntaxFields.Right when this is AssignmentExprSyntax { Right: { } assignRight } => assignRight,
            SyntaxFields.Left when this is AssignmentExprSyntax { Left: { } assignLeft } => assignLeft,
            SyntaxFields.Right when this is BinaryExprSyntax { Right: { } binRight } => binRight,
            SyntaxFields.Left when this is BinaryExprSyntax { Left: { } binLeft } => binLeft,
            SyntaxFields.Condition when this is ControlFlowStmtSyntax { Condition: { } cfCond } => cfCond,
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
        get => _explicitName ?? (NameSlice.Length > 0 && Document != null ? Document.GetText(NameSlice) : string.Empty);
        set => _explicitName = value;
    }

    public SyntaxModifiers Modifiers { get; set; }
}

public sealed class NamespaceDeclSyntax : DeclarationSyntax;

public sealed class ImportDeclSyntax : DeclarationSyntax
{
    public string ModulePath { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public bool IsWildcard { get; set; }
    public bool IsReExport { get; set; }
    public bool IsStatic { get; set; }
}

public sealed class TypeDeclSyntax : DeclarationSyntax
{
    public TypeSyntaxKind Kind { get; set; } = TypeSyntaxKind.Class;
}

public sealed class ImplDeclSyntax : DeclarationSyntax
{
    public TypeRefSyntax? TargetType { get; set; }
    public TypeRefSyntax? ImplementedTrait { get; set; }
}

public sealed class FunctionDeclSyntax : DeclarationSyntax
{
    public FunctionSyntaxKind Kind { get; set; } = FunctionSyntaxKind.Method;
    public ParameterDeclSyntax? Receiver { get; set; }
    public IReadOnlyList<ParameterDeclSyntax> Parameters { get; set; } = [];
    public BlockStmtSyntax? Body { get; set; }
    public ExprSyntax? ExpressionBody { get; set; }
    public CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowSummary? DataflowSummary { get; set; }
    public bool IsAsync => Modifiers.HasFlag(SyntaxModifiers.Async);
}

public sealed class ParameterDeclSyntax : DeclarationSyntax
{
    public TypeRefSyntax? ParameterType { get; set; }
    public ExprSyntax? DefaultValue { get; set; }
    public ParameterPassingMode PassingMode { get; set; } = ParameterPassingMode.Value;
}

public sealed class MemberDeclSyntax : DeclarationSyntax
{
    public MemberSyntaxKind Kind { get; set; } = MemberSyntaxKind.Field;
    public ExprSyntax? Initializer { get; set; }
    public string? ResolvedValue { get; set; }
    public bool IsDecoded { get; set; }
}

public sealed class VariableDeclSyntax : DeclarationSyntax
{
    public TypeRefSyntax? VariableType { get; set; }
    public ExprSyntax? Initializer { get; set; }
    public bool IsConstant { get; set; }
    public string Scope { get; set; } = "local";
    public string? ResolvedValue { get; set; }
    public bool IsDecoded { get; set; }
}

public sealed class AnnotationSyntax : DeclarationSyntax
{
    public IReadOnlyList<ArgumentSyntax> Arguments { get; set; } = [];
}

// ============================================================================
// TYPE REFERENCES (1 Canonical Node Type)
// ============================================================================

public sealed class TypeRefSyntax : SyntaxNode
{
    private string? _explicitName;
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
}

// ============================================================================
// STATEMENTS (5 Canonical Node Types)
// ============================================================================

public abstract class StatementSyntax : SyntaxNode;

public sealed class BlockStmtSyntax : StatementSyntax
{
    public bool IsUnsafe { get; set; }
}

public sealed class ControlFlowStmtSyntax : StatementSyntax
{
    public ControlFlowKind Kind { get; set; } = ControlFlowKind.Expression;
    public ExprSyntax? Condition { get; set; }
    public SyntaxNode? Body { get; set; }
    public SyntaxNode? Alternative { get; set; }
}

public sealed class CatchClauseSyntax : StatementSyntax
{
    public TypeRefSyntax? ExceptionType { get; set; }
    public BlockStmtSyntax? Body { get; set; }
}

public sealed class ReturnStmtSyntax : StatementSyntax
{
    public ExprSyntax? Value { get; set; }
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
// EXPRESSIONS (12 Canonical Node Types)
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
}

public sealed class InterpolatedStringExprSyntax : ExprSyntax;

public sealed class IdentifierExprSyntax : ExprSyntax
{
    private string? _explicitName;

    public ReadOnlySpan<char> NameSpan =>
        _explicitName != null ? _explicitName.AsSpan() : AsSpan();

    public string Name
    {
        get => _explicitName ?? Text;
        set => _explicitName = value;
    }
}

public sealed class MemberAccessExprSyntax : ExprSyntax
{
    private string? _explicitMemberName;
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
}

public sealed class IndexExprSyntax : ExprSyntax
{
    public ExprSyntax? Target { get; set; }
    public ExprSyntax? Index { get; set; }
}

public sealed class ArgumentSyntax : ExprSyntax
{
    private string? _explicitName;
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
}

public sealed class CallExprSyntax : ExprSyntax
{
    private string? _explicitMethodName;
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
        get => _explicitMethodName ?? (MethodNameSlice.Length > 0 && Document != null ? Document.GetText(MethodNameSlice) : string.Empty);
        set => _explicitMethodName = value;
    }

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
    public bool IsAwait => Operator == "await" || RawGrammarKind is "await_expression" or "await";
    public bool IsTryPropagate => Operator == "?" || RawGrammarKind == "try_expression";
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
}

public sealed class LambdaExprSyntax : ExprSyntax
{
    public IReadOnlyList<ParameterDeclSyntax> Parameters { get; set; } = [];
    public SyntaxNode? Body { get; set; }
    public bool IsAsync { get; set; }
}

// ============================================================================
// UNMAPPED NODES (1 Fallback Node Type)
// ============================================================================

/// <summary>
/// Zero-loss fallback for any named grammar node not matched by a more specific *Syntax class.
/// Preserves RawGrammarKind, FieldRole, Span, and all Children.
/// </summary>
public sealed class UnmappedSyntax : SyntaxNode;
