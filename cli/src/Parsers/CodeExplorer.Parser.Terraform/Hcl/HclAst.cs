namespace CodeExplorer.Parser.Terraform.Hcl;

public enum HclRefKind : byte
{
    Var,
    Local,
    Module,
    Data,
    Resource,
    Dependency,
    Each,
    Self,
    Count,
    Path,
    Terraform
}

public enum HclLiteralKind : byte
{
    String,
    Number,
    Bool,
    Null,
    Heredoc
}

/// <summary>
/// Parsed HCL file containing top-level blocks and attributes.
/// </summary>
public sealed record HclFile(
    string FilePath,
    IReadOnlyList<HclBlock> Blocks,
    IReadOnlyList<HclAttribute> Attributes)
{
    public HclAttribute? FindAttribute(string name) =>
        Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// An HCL block such as <c>resource "aws_s3_bucket" "assets" { ... }</c>.
/// </summary>
public sealed record HclBlock(
    string BlockHeader,
    IReadOnlyList<string> Labels,
    IReadOnlyList<HclAttribute> Attributes,
    IReadOnlyList<HclBlock> NestedBlocks,
    int StartLine,
    int EndLine)
{
    public HclAttribute? FindAttribute(string name) =>
        Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
}

/// <summary>
/// A <c>key = value</c> attribute inside an HCL file, block, or object expression.
/// </summary>
public sealed record HclAttribute(
    string Name,
    HclExpr Value,
    int Line);

/// <summary>
/// Base type for HCL expressions parsed by <see cref="HclParser"/>.
/// </summary>
public abstract record HclExpr(int Line)
{
    public sealed record Literal(HclLiteralKind Kind, string Value, int Line) : HclExpr(Line);

    public sealed record Traversal(IReadOnlyList<string> Parts, int Line) : HclExpr(Line)
    {
        public string Joined => string.Join('.', Parts);
    }

    public sealed record Call(string FunctionName, IReadOnlyList<HclExpr> Arguments, int Line) : HclExpr(Line);

    public sealed record Tuple(IReadOnlyList<HclExpr> Elements, int Line) : HclExpr(Line);

    public sealed record ObjectExpr(IReadOnlyList<HclAttribute> Entries, int Line) : HclExpr(Line);

    public sealed record Template(
        string RawValue,
        IReadOnlyList<HclExpr> Interpolations,
        IReadOnlyList<HclReference> ExtractedReferences,
        int Line) : HclExpr(Line);

    public sealed record Complex(
        string RawText,
        IReadOnlyList<HclExpr> SubExpressions,
        IReadOnlyList<HclReference> ExtractedReferences,
        int Line) : HclExpr(Line);

    public string? TryGetStringLiteral() => this switch
    {
        Literal { Kind: HclLiteralKind.String or HclLiteralKind.Heredoc } lit => lit.Value,
        Template { Interpolations.Count: 0 } tpl => tpl.RawValue,
        _ => null
    };

    public IEnumerable<HclCallInfo> EnumerateCalls()
    {
        switch (this)
        {
            case Call call:
                yield return new HclCallInfo(call.FunctionName, call.Arguments, call.Line);
                foreach (var arg in call.Arguments)
                {
                    foreach (var nested in arg.EnumerateCalls())
                    {
                        yield return nested;
                    }
                }
                break;

            case Tuple tuple:
                foreach (var el in tuple.Elements)
                {
                    foreach (var nested in el.EnumerateCalls())
                    {
                        yield return nested;
                    }
                }
                break;

            case ObjectExpr obj:
                foreach (var entry in obj.Entries)
                {
                    foreach (var nested in entry.Value.EnumerateCalls())
                    {
                        yield return nested;
                    }
                }
                break;

            case Template tpl:
                foreach (var interp in tpl.Interpolations)
                {
                    foreach (var nested in interp.EnumerateCalls())
                    {
                        yield return nested;
                    }
                }
                break;

            case Complex cx:
                foreach (var sub in cx.SubExpressions)
                {
                    foreach (var nested in sub.EnumerateCalls())
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }
}

public sealed record HclCallInfo(
    string FunctionName,
    IReadOnlyList<HclExpr> Arguments,
    int Line);

/// <summary>
/// A semantic reference extracted from an HCL expression (e.g. <c>var.env</c>, <c>aws_vpc.main.id</c>).
/// </summary>
public sealed record HclReference(
    HclRefKind Kind,
    string TargetScope,
    string TargetName,
    string? Attribute,
    int Line,
    string Via)
{
    private const string VarPrefix = "var";
    private const string LocalPrefix = "local";
    private const string ModulePrefix = "module";
    private const string DataPrefix = "data";
    private const string DependencyPrefix = "dependency";
    private const string EachPrefix = "each";
    private const string SelfPrefix = "self";
    private const string CountPrefix = "count";
    private const string PathPrefix = "path";
    private const string TerraformPrefix = "terraform";

    public string CanonicalTarget => Kind switch
    {
        HclRefKind.Var => $"{VarPrefix}.{TargetName}",
        HclRefKind.Local => $"{LocalPrefix}.{TargetName}",
        HclRefKind.Module => $"{ModulePrefix}.{TargetName}",
        HclRefKind.Data => $"{DataPrefix}.{TargetScope}.{TargetName}",
        HclRefKind.Resource => $"{TargetScope}.{TargetName}",
        HclRefKind.Dependency => $"{DependencyPrefix}.{TargetName}",
        HclRefKind.Each => string.IsNullOrEmpty(TargetName) ? EachPrefix : $"{EachPrefix}.{TargetName}",
        HclRefKind.Self => string.IsNullOrEmpty(TargetName) ? SelfPrefix : $"{SelfPrefix}.{TargetName}",
        HclRefKind.Count => string.IsNullOrEmpty(TargetName) ? CountPrefix : $"{CountPrefix}.{TargetName}",
        HclRefKind.Path => string.IsNullOrEmpty(TargetName) ? PathPrefix : $"{PathPrefix}.{TargetName}",
        HclRefKind.Terraform => string.IsNullOrEmpty(TargetName) ? TerraformPrefix : $"{TerraformPrefix}.{TargetName}",
        _ => TargetName
    };
}
