namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>A parsed HCL file: its top-level body plus any syntax errors recovered from.</summary>
public sealed record HclFile(
    IReadOnlyList<HclAttribute> Attributes,
    IReadOnlyList<HclBlock> Blocks,
    IReadOnlyList<HclDiagnostic> Diagnostics);

/// <summary>
/// A block: type keyword, labels, and body, e.g. <c>resource "aws_s3_bucket" "this" { }</c>.
/// <see cref="Kind"/> classifies the Terraform/Terragrunt top-level keywords; any other block type
/// (nested blocks such as <c>lifecycle</c> or <c>ingress</c>) is <see cref="HclBlockKind.Other"/>.
/// <see cref="Keyword"/> is the block type as written.
/// </summary>
public sealed record HclBlock(
    HclBlockKind Kind,
    string Keyword,
    IReadOnlyList<string> Labels,
    IReadOnlyList<HclAttribute> Attributes,
    IReadOnlyList<HclBlock> Blocks,
    int Line);

public enum HclBlockKind
{
    Other,
    Resource,
    Data,
    Module,
    Variable,
    Output,
    Provider,
    Locals,
    Terraform,
    Dependency,
    Dependencies,
    Include
}

/// <summary>An attribute <c>name = value</c>. <see cref="ValueText"/> is the value's source text.</summary>
public sealed record HclAttribute(string Name, HclExpr Value, string ValueText, int Line);

/// <summary>A syntax error the parser skipped past; the rest of the file is still parsed.</summary>
public sealed record HclDiagnostic(int Line, string Message);

/// <summary>An HCL expression. <see cref="Line"/> is the 1-based line of its first token.</summary>
public abstract record HclExpr(int Line);

public sealed record HclLiteral(HclLiteralKind Kind, string Text, int Line) : HclExpr(Line);

public enum HclLiteralKind { Number, Bool, Null }

/// <summary>A quoted string or heredoc. Literal-only templates have only <see cref="HclTemplateText"/> parts.</summary>
public sealed record HclTemplate(IReadOnlyList<HclTemplatePart> Parts, int Line) : HclExpr(Line)
{
    /// <summary>The string value when the template has no interpolations or directives; otherwise null.</summary>
    public string? LiteralValue =>
        Parts.All(p => p is HclTemplateText)
            ? string.Concat(Parts.Cast<HclTemplateText>().Select(p => p.Text))
            : null;
}

public abstract record HclTemplatePart;

public sealed record HclTemplateText(string Text) : HclTemplatePart;

/// <summary>A <c>${ expr }</c> interpolation.</summary>
public sealed record HclTemplateInterpolation(HclExpr Expression) : HclTemplatePart;

/// <summary>
/// A <c>%{ ... }</c> directive. <c>if</c> carries its condition; <c>for</c> carries its collection
/// and the iteration variables it binds until the matching <c>endfor</c>.
/// </summary>
public sealed record HclTemplateDirective(
    HclDirectiveKind Kind,
    HclExpr? Expression,
    IReadOnlyList<string> BoundVariables) : HclTemplatePart;

public enum HclDirectiveKind { If, Else, EndIf, For, EndFor }

/// <summary>A bare identifier at the root of a traversal, e.g. <c>var</c> in <c>var.env</c>.</summary>
public sealed record HclVariable(string Name, int Line) : HclExpr(Line);

/// <summary>Attribute access <c>target.name</c> (legacy numeric index <c>target.0</c> included).</summary>
public sealed record HclGetAttr(HclExpr Target, string Name, int Line) : HclExpr(Line);

/// <summary>Index access <c>target[key]</c>.</summary>
public sealed record HclIndex(HclExpr Target, HclExpr Key, int Line) : HclExpr(Line);

/// <summary>Splat <c>target[*]</c> or <c>target.*</c>.</summary>
public sealed record HclSplat(HclExpr Target, int Line) : HclExpr(Line);

public sealed record HclCall(string Function, IReadOnlyList<HclExpr> Arguments, int Line) : HclExpr(Line);

public sealed record HclTuple(IReadOnlyList<HclExpr> Items, int Line) : HclExpr(Line);

/// <summary>
/// An object constructor. A bare identifier key (<c>{ name = 1 }</c>) is a literal name, not a
/// variable reference, so it is carried as <see cref="HclObjectItem.KeyName"/>.
/// </summary>
public sealed record HclObject(IReadOnlyList<HclObjectItem> Items, int Line) : HclExpr(Line);

public sealed record HclObjectItem(string? KeyName, HclExpr? KeyExpression, HclExpr Value);

/// <summary>
/// A <c>for</c> expression: <c>[for k, v in coll : value if cond]</c> or
/// <c>{for k, v in coll : key =&gt; value if cond}</c>. <see cref="KeyVariable"/> is null when only
/// one iteration variable is declared; <see cref="KeyExpression"/> is null for the tuple form.
/// </summary>
public sealed record HclFor(
    string? KeyVariable,
    string ValueVariable,
    HclExpr Collection,
    HclExpr? KeyExpression,
    HclExpr Value,
    HclExpr? Condition,
    int Line) : HclExpr(Line);

public sealed record HclUnary(string Operator, HclExpr Operand, int Line) : HclExpr(Line);

public sealed record HclBinary(string Operator, HclExpr Left, HclExpr Right, int Line) : HclExpr(Line);

public sealed record HclConditional(HclExpr Condition, HclExpr WhenTrue, HclExpr WhenFalse, int Line) : HclExpr(Line);
