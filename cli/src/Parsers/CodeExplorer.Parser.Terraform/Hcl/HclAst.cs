namespace CodeExplorer.Parser.Terraform.Hcl;

/// <summary>
/// Lightweight HCL AST records produced by <see cref="HclParser"/>.
/// Only what the mapper/enricher need — not a full HCL implementation.
/// </summary>
public sealed record HclFile(
    IReadOnlyList<HclBlock> Blocks,
    IReadOnlyList<HclAttribute> TopLevelAttributes,
    IReadOnlyList<HclReference> References);

/// <summary>A block: type keyword + quoted labels + body. e.g. resource "aws_s3_bucket" "this" { }</summary>
public sealed record HclBlock(
    string Type,                      // resource | data | module | variable | output | provider | locals | terraform | dependency | include | ...
    IReadOnlyList<string> Labels,     // the quoted labels after the type
    IReadOnlyList<HclAttribute> Attributes,
    IReadOnlyList<HclBlock> NestedBlocks,
    int Line);

/// <summary>An attribute: name = value-expression.</summary>
public sealed record HclAttribute(string Name, HclExpr Value, int Line);

/// <summary>A value expression. Kind discriminates literal vs traversal (ref) vs template vs call.</summary>
public sealed record HclExpr(
    HclExprKind Kind,
    string? Literal,                      // for Literal
    IReadOnlyList<string>? Traversal,     // for Traversal: ["var","X"] / ["module","X","Y"] / ["data","T","N"]
    IReadOnlyList<HclExpr>? Parts,        // for Template/Tuple
    string? FunctionName,                 // for Call
    int Line,
    IReadOnlyList<string>? ObjectKeys = null,    // for ObjectExpr: top-level keys, e.g. inputs = { k = v }
    IReadOnlyList<string>? TupleItems = null);   // for Tuple: top-level element strings, e.g. paths = ["../a","../b"]

public enum HclExprKind { Literal, Traversal, Template, Tuple, ObjectExpr, Call, Unknown }

/// <summary>
/// A flat, line-numbered traversal reference discovered anywhere in the file (attribute values,
/// nested blocks, interpolations). Collected during parsing and resolved later by the enricher,
/// since cross-file resolution cannot happen per-file. Head is the leading symbol
/// (var/local/module/data/&lt;resource_type&gt;/dependency); Path is the full dotted traversal.
/// EnclosingKey is the ref-key of the top-level block the reference appears inside (e.g.
/// "aws_security_group.internal"), or null if the reference is at file top level.
/// </summary>
public sealed record HclReference(
    string Head,                      // "var" | "local" | "module" | "data" | "dependency" | "<resource_type>"
    IReadOnlyList<string> Path,       // full traversal, e.g. ["module","vpc","vpc_id"]
    int Line,
    string? EnclosingKey = null,
    bool Explicit = false);           // true when harvested from a `depends_on` meta-argument
