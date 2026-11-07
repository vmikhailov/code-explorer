using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Declarative rule for mapping annotations/decorators (e.g., Resilience4j @CircuitBreaker, GraphQL @QueryMapping) to references or EntryPoints.
/// </summary>
public sealed record AnnotationSinkRule
{
    public required FrozenSet<string> AnnotationNames { get; init; }
    public FrozenSet<string> AnnotationNodeTypes { get; init; } = new[] { "annotation", "marker_annotation", "attribute", "attribute_group", "decorator" }.ToFrozenSet();
    public string TargetRelation { get; init; } = OntologyConstants.Relationships.Calls;
    public string TargetPrefix { get; init; } = "";
    public bool UseAnnotationNameAsTarget { get; init; } = true;
    public string? StaticTargetName { get; init; }
}

/// <summary>
/// Declarative rule for mapping class inheritance/interface implementations (e.g., Netty ChannelInboundHandlerAdapter, Tortoise Model) to references or Table nodes.
/// </summary>
public sealed record ClassHierarchySinkRule
{
    public required FrozenSet<string> BaseTypeSubstrings { get; init; }
    public FrozenSet<string> ClassDeclarationNodeTypes { get; init; } = new[] { "class_declaration", "class_definition", "struct_item" }.ToFrozenSet();
    public string TargetRelation { get; init; } = OntologyConstants.Relationships.Exposes;
    public string TargetPrefix { get; init; } = "";
    public string TargetSuffix { get; init; } = "";
    public bool LowercaseClassName { get; init; }
    public bool EmitTableNode { get; init; }
}

/// <summary>
/// Declarative AST rule indexed by AST node type for specialized framework patterns within a fused DeclarativeSemanticExtension.
/// </summary>
public sealed record CustomAstRule
{
    public IReadOnlyCollection<string> NodeTypes { get; init; } = FrozenSet<string>.Empty;
    public IReadOnlyDictionary<string, NodeSelector>? Selectors { get; init; }
    public Func<Node, ParsingContext, string?>? MapNodeType { get; init; }
    public Func<Node, ParsingContext, string?, string?, string?>? ExtractIdentifier { get; init; }
    public Action<Node, string, List<Reference>, ParsingContext>? CollectReferences { get; init; }
    public Action<Node, SyntacticSymbol, ParsingContext>? EnrichSymbol { get; init; }
}

internal static class DeclarativeExtendedRuleEvaluator
{
    private const string SuperclassField = "superclass";
    private const string ArgumentListType = "argument_list";
    private const char AtChar = '@';

    internal static void EvaluateAnnotationSinkRule(
        Node node,
        string scopeSymbolId,
        List<Reference> references,
        AnnotationSinkRule rule)
    {
        if (node is not AnnotationSyntax && !rule.AnnotationNodeTypes.Contains(node.Type)) return;

        var shortName = DeclarativeSemanticExtension.GetShortAnnotationName(node);
        if (string.IsNullOrEmpty(shortName))
        {
            var nameNode = node.GetField(SyntaxFields.Name) ?? node.Children.FirstOrDefault();
            if (nameNode.IsValid())
            {
                shortName = nameNode.Text.TrimStart(AtChar);
            }
        }

        if (string.IsNullOrEmpty(shortName) || !rule.AnnotationNames.Contains(shortName)) return;

        var target = !string.IsNullOrEmpty(rule.StaticTargetName)
            ? rule.StaticTargetName
            : rule.UseAnnotationNameAsTarget
                ? rule.TargetPrefix + shortName
                : rule.TargetPrefix;

        if (!string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, target, rule.TargetRelation));
        }
    }

    internal static bool MatchesClassHierarchyRule(Node node, ClassHierarchySinkRule rule, out string targetName)
    {
        targetName = string.Empty;
        if (node is not TypeDeclSyntax && !rule.ClassDeclarationNodeTypes.Contains(node.Type)) return false;

        var superNode = node.GetField(SuperclassField);
        if (!superNode.IsValid())
        {
            superNode = node.FindChildOfType(ArgumentListType);
        }

        var searchSpan = superNode.IsValid() ? superNode.AsSpan() : node.AsSpan();
        if (!DeclarativeUniversalRuleEvaluator.SpanContainsAny(searchSpan, rule.BaseTypeSubstrings))
        {
            return false;
        }

        var nameNode = node.GetField(SyntaxFields.Name);
        if (!nameNode.IsValid() || string.IsNullOrEmpty(nameNode.Text)) return false;

        var className = rule.LowercaseClassName ? nameNode.Text.ToLowerInvariant() : nameNode.Text;
        targetName = $"{rule.TargetPrefix}{className}{rule.TargetSuffix}";
        return true;
    }

    internal static string? EvaluateCustomMapNodeType(
        Node node,
        ParsingContext ctx,
        FrozenDictionary<string, CustomAstRule[]> byNodeType,
        CustomAstRule[] unfiltered)
    {
        if (byNodeType.Count > 0 &&
            byNodeType.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(node.Type.AsSpan(), out var typedRules))
        {
            for (var i = 0; i < typedRules.Length; i++)
            {
                var r = typedRules[i];
                if (r.Selectors != null)
                {
                    foreach (var kvp in r.Selectors)
                    {
                        if (kvp.Value.Matches(node)) return kvp.Key;
                    }
                }
                var kind = r.MapNodeType?.Invoke(node, ctx);
                if (kind != null) return kind;
            }
        }

        for (var i = 0; i < unfiltered.Length; i++)
        {
            var r = unfiltered[i];
            if (r.Selectors != null)
            {
                foreach (var kvp in r.Selectors)
                {
                    if (kvp.Value.Matches(node)) return kvp.Key;
                }
            }
            var kind = r.MapNodeType?.Invoke(node, ctx);
            if (kind != null) return kind;
        }

        return null;
    }

    internal static string? EvaluateCustomExtractIdentifier(
        Node node,
        ParsingContext ctx,
        string? projectName,
        string? relativePath,
        FrozenDictionary<string, CustomAstRule[]> byNodeType,
        CustomAstRule[] unfiltered)
    {
        if (byNodeType.Count > 0 &&
            byNodeType.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(node.Type.AsSpan(), out var typedRules))
        {
            for (var i = 0; i < typedRules.Length; i++)
            {
                var r = typedRules[i];
                if (r.ExtractIdentifier == null) continue;
                var matched = MatchesRuleOrSelectors(node, ctx, r);
                if (matched)
                {
                    var id = r.ExtractIdentifier(node, ctx, projectName, relativePath);
                    if (id != null) return id;
                }
            }
        }

        for (var i = 0; i < unfiltered.Length; i++)
        {
            var r = unfiltered[i];
            if (r.ExtractIdentifier == null) continue;
            if (MatchesRuleOrSelectors(node, ctx, r))
            {
                var id = r.ExtractIdentifier(node, ctx, projectName, relativePath);
                if (id != null) return id;
            }
        }

        return null;
    }

    private static bool MatchesRuleOrSelectors(Node node, ParsingContext ctx, CustomAstRule rule)
    {
        if (rule.Selectors != null)
        {
            foreach (var kvp in rule.Selectors)
            {
                if (kvp.Value.Matches(node)) return true;
            }
        }
        return rule.MapNodeType == null || rule.MapNodeType(node, ctx) != null;
    }

    internal static void EvaluateCustomCollectReferences(
        Node node,
        string scopeSymbolId,
        List<Reference> references,
        ParsingContext ctx,
        FrozenDictionary<string, CustomAstRule[]> byNodeType,
        CustomAstRule[] unfiltered)
    {
        if (byNodeType.Count > 0 &&
            byNodeType.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(node.Type.AsSpan(), out var typedRules))
        {
            for (var i = 0; i < typedRules.Length; i++)
            {
                typedRules[i].CollectReferences?.Invoke(node, scopeSymbolId, references, ctx);
            }
        }

        for (var i = 0; i < unfiltered.Length; i++)
        {
            unfiltered[i].CollectReferences?.Invoke(node, scopeSymbolId, references, ctx);
        }
    }

    internal static void EvaluateCustomEnrichSymbol(
        Node node,
        SyntacticSymbol symbol,
        ParsingContext ctx,
        FrozenDictionary<string, CustomAstRule[]> byNodeType,
        CustomAstRule[] unfiltered)
    {
        if (byNodeType.Count > 0 &&
            byNodeType.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(node.Type.AsSpan(), out var typedRules))
        {
            for (var i = 0; i < typedRules.Length; i++)
            {
                typedRules[i].EnrichSymbol?.Invoke(node, symbol, ctx);
            }
        }

        for (var i = 0; i < unfiltered.Length; i++)
        {
            unfiltered[i].EnrichSymbol?.Invoke(node, symbol, ctx);
        }
    }
}
