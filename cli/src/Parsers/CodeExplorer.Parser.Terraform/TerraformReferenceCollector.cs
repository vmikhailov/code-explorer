using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// A traversal reference found in a file, resolved later by the enricher across the module's files.
/// <see cref="Head"/> is the root symbol (var / local / module / data / dependency /
/// &lt;resource_type&gt;) and <see cref="Path"/> the root plus attribute names, e.g.
/// <c>["module","vpc","vpc_id"]</c>. <see cref="EnclosingKey"/> identifies the node the reference
/// is made from: the ref-key of the enclosing declaration (e.g. <c>aws_security_group.internal</c>
/// or <c>local.prefix</c>), or the <see cref="TerraformAnchor.Key"/> of an attribute anchor.
/// <see cref="Explicit"/> marks entries of a <c>depends_on</c> list.
/// </summary>
public sealed record TerraformReference(
    string Head,
    IReadOnlyList<string> Path,
    int Line,
    string? EnclosingKey = null,
    bool Explicit = false);

/// <summary>
/// An attribute that contains references but belongs to no declaration: a top-level attribute
/// (Terragrunt <c>inputs</c>) or an attribute of a block that declares no node (<c>terraform</c>,
/// <c>generate</c>, <c>remote_state</c>, …). The mapper declares a member node for it so its
/// references have a source. <see cref="Name"/> is the attribute's path, e.g. <c>inputs</c> or
/// <c>generate.provider.contents</c>; <see cref="Key"/> is unique within the file.
/// </summary>
public sealed record TerraformAnchor(string Key, string Name, int Line, string ValueText);

/// <summary>The references in a file and the attribute anchors they are made from.</summary>
public sealed record TerraformReferenceSet(
    IReadOnlyList<TerraformReference> References,
    IReadOnlyList<TerraformAnchor> Anchors);

/// <summary>
/// Collects <see cref="TerraformReference"/>s from a parsed file. Only traversal expressions
/// (<c>a.b</c>, <c>a.b[i]</c>, <c>a[*].b</c>) produce references; string literal text never does.
/// Index keys are visited as expressions of their own, and names bound by <c>for</c> expressions
/// and <c>%{ for }</c> directives are not references.
///
/// Each reference is attributed to the most specific node it is made from: the enclosing
/// declaration block, the local value it initializes, or otherwise an attribute anchor.
/// </summary>
public static class TerraformReferenceCollector
{
    private const string DependsOnAttribute = "depends_on";
    private const string AnchorKeyPrefix = "#";

    public static TerraformReferenceSet Collect(HclFile file, bool isTfvars = false)
    {
        var references = new List<TerraformReference>();
        var anchors = new List<TerraformAnchor>();

        // .tfvars assignments are literal values declared by the mapper; they make no references.
        if (!isTfvars)
        {
            foreach (var attribute in file.Attributes)
            {
                VisitAnchoredAttribute(attribute, attribute.Name, references, anchors);
            }
        }

        foreach (var block in file.Blocks)
        {
            if (block.Kind == HclBlockKind.Locals)
            {
                foreach (var attribute in block.Attributes)
                {
                    VisitAttribute(attribute, $"local.{attribute.Name}", references);
                }
            }
            else if (EnclosingKeyFor(block) is { } enclosingKey)
            {
                VisitBlock(block, enclosingKey, references);
            }
            else
            {
                VisitAnchoredBlock(block, PathOf(block, prefix: null), references, anchors);
            }
        }

        // Source order, so a consumer that keeps the first reference per target keeps the
        // earliest one in the file.
        return new TerraformReferenceSet(references.OrderBy(r => r.Line).ToList(), anchors);
    }

    /// <summary>
    /// The ref-key a top-level block is referenced by, matching <see cref="TerraformDeclarationMapper"/>:
    /// <c>resource "T" "N"</c> → <c>T.N</c>, <c>data "T" "N"</c> → <c>data.T.N</c>,
    /// <c>module / provider / dependency "N"</c> → <c>&lt;keyword&gt;.N</c>, <c>output "N"</c> →
    /// <c>output.N</c>, <c>variable "N"</c> → <c>var.N</c>. Blocks that declare no single
    /// addressable node (e.g. <c>locals</c>, <c>terraform</c>) return null.
    /// </summary>
    public static string? EnclosingKeyFor(HclBlock block)
    {
        var labels = block.Labels;
        return block.Kind switch
        {
            HclBlockKind.Resource when labels.Count >= 2 => $"{labels[0]}.{labels[1]}",
            HclBlockKind.Data when labels.Count >= 2 => $"data.{labels[0]}.{labels[1]}",
            HclBlockKind.Module when labels.Count >= 1 => $"module.{labels[0]}",
            HclBlockKind.Provider when labels.Count >= 1 => $"provider.{labels[0]}",
            HclBlockKind.Dependency when labels.Count >= 1 => $"dependency.{labels[0]}",
            HclBlockKind.Output when labels.Count >= 1 => $"output.{labels[0]}",
            HclBlockKind.Variable when labels.Count >= 1 => $"var.{labels[0]}",
            _ => null
        };
    }

    // Name of a block within an attribute path: keyword plus labels, e.g. "generate.provider".
    private static string PathOf(HclBlock block, string? prefix)
    {
        var own = string.Join('.', block.Labels.Prepend(block.Keyword));
        return prefix == null ? own : $"{prefix}.{own}";
    }

    // Attributes of a block that declares no node: each attribute with references gets an anchor.
    private static void VisitAnchoredBlock(HclBlock block, string path, List<TerraformReference> references, List<TerraformAnchor> anchors)
    {
        foreach (var attribute in block.Attributes)
        {
            VisitAnchoredAttribute(attribute, $"{path}.{attribute.Name}", references, anchors);
        }

        foreach (var nested in block.Blocks)
        {
            VisitAnchoredBlock(nested, PathOf(nested, path), references, anchors);
        }
    }

    private static void VisitAnchoredAttribute(HclAttribute attribute, string name, List<TerraformReference> references, List<TerraformAnchor> anchors)
    {
        var key = $"{AnchorKeyPrefix}{name}:{attribute.Line}";
        var before = references.Count;
        VisitAttribute(attribute, key, references);
        if (references.Count > before)
        {
            anchors.Add(new TerraformAnchor(key, name, attribute.Line, attribute.ValueText));
        }
    }

    private static void VisitBlock(HclBlock block, string? enclosingKey, List<TerraformReference> references)
    {
        foreach (var attribute in block.Attributes)
        {
            VisitAttribute(attribute, enclosingKey, references);
        }

        foreach (var nested in block.Blocks)
        {
            VisitBlock(nested, enclosingKey, references);
        }
    }

    private static void VisitAttribute(HclAttribute attribute, string? enclosingKey, List<TerraformReference> references)
    {
        var walker = new Walker(references, enclosingKey,
            string.Equals(attribute.Name, DependsOnAttribute, StringComparison.Ordinal));
        walker.Visit(attribute.Value, Scope.Empty);
    }

    // Names bound by enclosing for-expressions and %{ for } directives.
    private sealed class Scope
    {
        public static readonly Scope Empty = new(null, []);

        private readonly Scope? _parent;
        private readonly IReadOnlyList<string> _names;

        private Scope(Scope? parent, IReadOnlyList<string> names)
        {
            _parent = parent;
            _names = names;
        }

        public Scope With(IEnumerable<string?> names) => new(this, names.OfType<string>().ToList());

        public bool Binds(string name) =>
            _names.Contains(name, StringComparer.Ordinal) || (_parent?.Binds(name) ?? false);
    }

    private sealed class Walker(List<TerraformReference> references, string? enclosingKey, bool isExplicit)
    {
        public void Visit(HclExpr? expr, Scope scope)
        {
            switch (expr)
            {
                case null:
                case HclLiteral:
                    return;
                case HclVariable or HclGetAttr or HclIndex or HclSplat:
                    VisitTraversal(expr, scope);
                    return;
                case HclTemplate template:
                    VisitTemplate(template, scope);
                    return;
                case HclCall call:
                    foreach (var arg in call.Arguments) Visit(arg, scope);
                    return;
                case HclTuple tuple:
                    foreach (var item in tuple.Items) Visit(item, scope);
                    return;
                case HclObject obj:
                    foreach (var item in obj.Items)
                    {
                        Visit(item.KeyExpression, scope);
                        Visit(item.Value, scope);
                    }
                    return;
                case HclFor forExpr:
                    Visit(forExpr.Collection, scope);
                    var inner = scope.With([forExpr.KeyVariable, forExpr.ValueVariable]);
                    Visit(forExpr.KeyExpression, inner);
                    Visit(forExpr.Value, inner);
                    Visit(forExpr.Condition, inner);
                    return;
                case HclUnary unary:
                    Visit(unary.Operand, scope);
                    return;
                case HclBinary binary:
                    Visit(binary.Left, scope);
                    Visit(binary.Right, scope);
                    return;
                case HclConditional conditional:
                    Visit(conditional.Condition, scope);
                    Visit(conditional.WhenTrue, scope);
                    Visit(conditional.WhenFalse, scope);
                    return;
            }
        }

        // Unwinds a traversal to its root. A variable root with at least one attribute name yields
        // one reference; index keys and a non-variable root are visited on their own.
        private void VisitTraversal(HclExpr expr, Scope scope)
        {
            var names = new List<string>();
            var current = expr;
            while (true)
            {
                switch (current)
                {
                    case HclGetAttr getAttr:
                        names.Add(getAttr.Name);
                        current = getAttr.Target;
                        continue;
                    case HclIndex index:
                        Visit(index.Key, scope);
                        current = index.Target;
                        continue;
                    case HclSplat splat:
                        current = splat.Target;
                        continue;
                }
                break;
            }

            if (current is not HclVariable root)
            {
                Visit(current, scope);
                return;
            }

            if (names.Count == 0 || scope.Binds(root.Name)) return;

            names.Add(root.Name);
            names.Reverse();
            references.Add(new TerraformReference(root.Name, names, root.Line, enclosingKey, isExplicit));
        }

        private void VisitTemplate(HclTemplate template, Scope scope)
        {
            var scopes = new Stack<Scope>();
            var current = scope;
            foreach (var part in template.Parts)
            {
                switch (part)
                {
                    case HclTemplateInterpolation interpolation:
                        Visit(interpolation.Expression, current);
                        break;
                    case HclTemplateDirective { Kind: HclDirectiveKind.For } directive:
                        Visit(directive.Expression, current);
                        scopes.Push(current);
                        current = current.With(directive.BoundVariables);
                        break;
                    case HclTemplateDirective { Kind: HclDirectiveKind.EndFor }:
                        if (scopes.Count > 0) current = scopes.Pop();
                        break;
                    case HclTemplateDirective directive:
                        Visit(directive.Expression, current);
                        break;
                }
            }
        }
    }
}
