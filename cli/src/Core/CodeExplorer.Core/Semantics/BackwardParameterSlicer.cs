using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Semantics;

/// <summary>
/// Result of tracing a parameter or argument backward to its origins.
/// </summary>
public sealed record ParameterSliceResult(
    ParameterNature Nature,
    string? ResolvedValue,
    IReadOnlyList<SyntaxNode> Origins,
    bool IsResolved
);

/// <summary>
/// Demand-driven backward provenance slicer that traces parameters and call arguments upward to their root origins.
/// </summary>
public static class BackwardParameterSlicer
{
    private const int MaxSlicingDepth = 12;

    /// <summary>
    /// Slices a call argument backward, determining its parameter nature and resolving its compile-time value.
    /// </summary>
    public static ParameterSliceResult SliceArgument(CallExprSyntax call, int argIndex, string? contextOrProject = null)
    {
        var nature = PlatformParameterCatalog.ResolveParameterNature(call.MethodName, argIndex);
        if (nature == ParameterNature.Unspecified && call.Receiver != null)
        {
            nature = PlatformParameterCatalog.ResolveParameterNature(call.ExplicitReceiverPath, argIndex);
        }

        if (argIndex < 0 || argIndex >= call.Arguments.Count)
        {
            return new ParameterSliceResult(nature, null, [], false);
        }

        var argNode = call.Arguments[argIndex].Value;
        if (argNode == null)
        {
            return new ParameterSliceResult(nature, null, [], false);
        }

        return SliceExpression(argNode, nature, contextOrProject);
    }

    /// <summary>
    /// Slices an arbitrary expression node backward to its origin definitions.
    /// </summary>
    public static ParameterSliceResult SliceExpression(
        SyntaxNode expr,
        ParameterNature inheritedNature = ParameterNature.Unspecified,
        string? contextOrProject = null)
    {
        var origins = new List<SyntaxNode>();
        var visited = new HashSet<SyntaxNode>(ReferenceEqualityComparer.Instance);

        var nature = inheritedNature;
        if (nature == ParameterNature.Unspecified && expr is IdentifierExprSyntax id)
        {
            nature = PlatformParameterCatalog.ClassifyByName(id.Name);
        }

        var isResolved = AstValueResolver.TryResolveString(expr, contextOrProject, out var resolvedStr);
        CollectOrigins(expr, origins, visited, 0);

        return new ParameterSliceResult(
            nature,
            isResolved ? resolvedStr : null,
            origins,
            isResolved);
    }

    private static void CollectOrigins(
        SyntaxNode? node,
        List<SyntaxNode> origins,
        HashSet<SyntaxNode> visited,
        int depth)
    {
        if (node == null || depth > MaxSlicingDepth || !visited.Add(node)) return;

        switch (node)
        {
            case LiteralExprSyntax lit:
                origins.Add(lit);
                break;

            case IdentifierExprSyntax ident:
                var def = FindReachingDefinition(ident);
                if (def != null)
                {
                    origins.Add(def);
                    CollectOrigins(def, origins, visited, depth + 1);
                }
                else
                {
                    origins.Add(ident);
                }
                break;

            case VariableDeclSyntax varDecl:
                if (varDecl.Initializer != null)
                {
                    CollectOrigins(varDecl.Initializer, origins, visited, depth + 1);
                }
                break;

            case AssignmentExprSyntax assign:
                if (assign.Right != null)
                {
                    CollectOrigins(assign.Right, origins, visited, depth + 1);
                }
                break;

            case BinaryExprSyntax bin:
                CollectOrigins(bin.Left, origins, visited, depth + 1);
                CollectOrigins(bin.Right, origins, visited, depth + 1);
                break;

            case InterpolatedStringExprSyntax interp:
                origins.Add(interp);
                foreach (var child in interp.Children)
                {
                    CollectOrigins(child, origins, visited, depth + 1);
                }
                break;

            case ParenthesizedExprSyntax paren:
                CollectOrigins(paren.Expression, origins, visited, depth + 1);
                break;

            default:
                origins.Add(node);
                break;
        }
    }

    private static SyntaxNode? FindReachingDefinition(IdentifierExprSyntax ident)
    {
        var targetName = ident.Name;
        if (string.IsNullOrEmpty(targetName)) return null;

        var current = ident.Parent;
        while (current != null)
        {
            // Search preceding siblings in blocks
            if (current is BlockStmtSyntax or FunctionDeclSyntax)
            {
                foreach (var child in current.Children)
                {
                    if (child.Span.StartByte >= ident.Span.StartByte) break;

                    if (child is VariableDeclSyntax v && v.Name.Equals(targetName, StringComparison.Ordinal))
                    {
                        return v;
                    }
                    if (child is AssignmentExprSyntax a && a.Left is IdentifierExprSyntax leftId && leftId.Name.Equals(targetName, StringComparison.Ordinal))
                    {
                        return a;
                    }
                }
            }

            current = current.Parent;
        }

        return null;
    }
}
