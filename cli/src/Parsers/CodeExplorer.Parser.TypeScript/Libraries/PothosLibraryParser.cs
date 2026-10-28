using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

/// <summary>
/// Semantic extension for Pothos GraphQL (@pothos/core) code-first schema builders.
/// Supports multi-builder setups (e.g. clientBuilder, adminBuilder, monitoringBuilder)
/// without relying on variable-name heuristics.
/// Recognizes:
/// - &lt;builder&gt;.queryField('name', (t) =&gt; t.field({ resolve: ... }))
/// - &lt;builder&gt;.mutationField('name', (t) =&gt; t.field({ resolve: ... }))
/// - &lt;builder&gt;.subscriptionField('name', (t) =&gt; t.field({ resolve: ... }))
/// - Batch definitions via queryFields / mutationFields / subscriptionFields / queryType / mutationType / subscriptionType
/// </summary>
public class PothosLibraryParser : ISemanticExtension
{
    public string Id => "pothos-graphql";
    public string Name => "Pothos GraphQL";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.WebService;
    public IReadOnlyList<string> SupportedPatterns => ["@pothos/core"];
    public bool IsBuiltIn => true;
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            TryExtractSingleRootField(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (node.Is(TreeSitterSyntax.TypeScript.Pair) &&
            TryExtractBatchRootField(node, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            TryExtractSingleRootField(node, out _, out var opPrefix, out var fieldName))
        {
            return $"{opPrefix}:{fieldName}";
        }

        if (node.Is(TreeSitterSyntax.TypeScript.Pair) &&
            TryExtractBatchRootField(node, out _, out var batchOpPrefix, out var batchFieldName))
        {
            return $"{batchOpPrefix}:{batchFieldName}";
        }

        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (symbol.Kind != OntologyConstants.NodeLabels.EntryPoint) return;

        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            TryExtractSingleRootField(node, out var builderName, out var opPrefix, out var fieldName))
        {
            PopulateGraphQLMetadata(node, symbol, builderName, opPrefix, fieldName);
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.Pair) &&
                 TryExtractBatchRootField(node, out var batchBuilder, out var batchOpPrefix, out var batchFieldName))
        {
            PopulateGraphQLMetadata(node, symbol, batchBuilder, batchOpPrefix, batchFieldName);
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.IsValid() || !node.IsAny(TreeSitterSyntax.TypeScript.CallExpression, TreeSitterSyntax.TypeScript.Pair))
        {
            return;
        }
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression) &&
            !TryExtractSingleRootField(node, out _, out _, out _))
        {
            return;
        }
        if (node.Is(TreeSitterSyntax.TypeScript.Pair) &&
            !TryExtractBatchRootField(node, out _, out _, out _))
        {
            return;
        }

        // If resolve is a named function reference rather than an inline arrow/function expression
        // (e.g., `resolve: resolveAuditLogs`), link the endpoint to that function.
        foreach (var pair in EnumeratePairs(node))
        {
            var key = pair.GetChildFieldText(TreeSitterSyntax.Fields.Key)?.Trim('\'', '"', '`');
            if (key is "resolve" or "subscribe")
            {
                var val = pair.GetField(TreeSitterSyntax.Fields.Value);
                if (val.IsValid() &&
                    !val.IsAny(TreeSitterSyntax.TypeScript.ArrowFunction, TreeSitterSyntax.TypeScript.FunctionExpression))
                {
                    var targetName = val.Text.Trim();
                    if (!string.IsNullOrEmpty(targetName) && !targetName.Contains('('))
                    {
                        references.Add(new Reference(scopeSymbolId, targetName, OntologyConstants.Relationships.Triggers));
                    }
                }
            }
        }
    }

    private static void PopulateGraphQLMetadata(
        Node astNode,
        SyntacticSymbol symbol,
        string builderName,
        string opPrefix,
        string fieldName)
    {
        symbol.Protocol = "GraphQL";
        symbol.OperationType = opPrefix switch
        {
            "MUTATION" => "Mutation",
            "SUBSCRIPTION" => "Subscription",
            _ => "Query"
        };
        symbol.Properties["graphql_field"] = fieldName;
        if (!string.IsNullOrEmpty(builderName))
        {
            symbol.Properties["graphql_schema"] = builderName;
        }

        // Extract response type from `t.field({ type: SomeTypeRef, ... })` if present
        foreach (var pair in EnumeratePairs(astNode))
        {
            var key = pair.GetChildFieldText(TreeSitterSyntax.Fields.Key)?.Trim('\'', '"', '`');
            if (key == "type")
            {
                var val = pair.GetField(TreeSitterSyntax.Fields.Value);
                if (val.IsValid())
                {
                    var typeText = val.Text.Trim().Trim('\'', '"', '`', '[', ']');
                    if (!string.IsNullOrEmpty(typeText) && typeText.Length <= 120 && !typeText.Contains('{'))
                    {
                        symbol.ResponseType = typeText;
                        break;
                    }
                }
            }
        }
    }

    private static IEnumerable<Node> EnumeratePairs(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (!curr.IsValid()) continue;
            if (curr.Is(TreeSitterSyntax.TypeScript.Pair))
            {
                yield return curr;
            }
            foreach (var child in curr.Children)
            {
                stack.Push(child);
            }
        }
    }

    private static bool TryExtractSingleRootField(
        Node callNode,
        out string builderName,
        out string opPrefix,
        out string fieldName)
    {
        builderName = string.Empty;
        opPrefix = string.Empty;
        fieldName = string.Empty;

        var fnNode = callNode.GetFunctionNode();
        if (!fnNode.IsValid() || !fnNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            return false;
        }

        var propNode = fnNode.GetField(TreeSitterSyntax.Fields.Property);
        var objNode = fnNode.GetField(TreeSitterSyntax.Fields.Object);
        if (!propNode.IsValid() || !objNode.IsValid())
        {
            return false;
        }

        opPrefix = propNode.Text switch
        {
            "queryField" => "QUERY",
            "mutationField" => "MUTATION",
            "subscriptionField" => "SUBSCRIPTION",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(opPrefix))
        {
            return false;
        }

        var argsNode = callNode.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!argsNode.IsValid())
        {
            return false;
        }

        var firstString = argsNode.Children.FirstOrDefault(c =>
            c.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString));
        if (!firstString.IsValid())
        {
            return false;
        }

        fieldName = firstString.Text.Trim('\'', '"', '`').Trim();
        if (string.IsNullOrEmpty(fieldName))
        {
            return false;
        }

        builderName = objNode.Text.Trim();
        return true;
    }

    private static bool TryExtractBatchRootField(
        Node pairNode,
        out string builderName,
        out string opPrefix,
        out string fieldName)
    {
        builderName = string.Empty;
        opPrefix = string.Empty;
        fieldName = string.Empty;

        var key = pairNode.GetChildFieldText(TreeSitterSyntax.Fields.Key)?.Trim('\'', '"', '`');
        if (string.IsNullOrEmpty(key) || key is "fields" or "type" or "args" or "resolve" or "subscribe" or "description" or "nullable" or "authScopes")
        {
            return false;
        }

        var valueNode = pairNode.GetField(TreeSitterSyntax.Fields.Value);
        if (!valueNode.IsValid() || !valueNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            return false;
        }

        // Value must be a field builder call like `t.field(...)`, `t.prismaField(...)`, `t.string(...)`, `t.int(...)`, `t.expose(...)`
        var valFn = valueNode.GetFunctionNode();
        if (!valFn.IsValid() || !valFn.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            return false;
        }

        // Walk up to check if this Pair is inside a Pothos batch root block:
        // <builder>.queryFields((t) => ({ <pair> }))
        // or <builder>.queryType({ fields: (t) => ({ <pair> }) })
        var curr = pairNode.Parent;
        for (var hops = 0; curr.IsValid() && hops < 8; hops++, curr = curr.Parent)
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.Pair))
            {
                var outerKey = curr.GetChildFieldText(TreeSitterSyntax.Fields.Key)?.Trim('\'', '"', '`');
                if (outerKey != "fields")
                {
                    return false; // Nested inside another field's options (like args: { ... })
                }
            }

            if (curr.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                var fn = curr.GetFunctionNode();
                if (fn.IsValid() && fn.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var prop = fn.GetField(TreeSitterSyntax.Fields.Property);
                    var obj = fn.GetField(TreeSitterSyntax.Fields.Object);
                    if (prop.IsValid() && obj.IsValid())
                    {
                        opPrefix = prop.Text switch
                        {
                            "queryFields" or "queryType" => "QUERY",
                            "mutationFields" or "mutationType" => "MUTATION",
                            "subscriptionFields" or "subscriptionType" => "SUBSCRIPTION",
                            _ => string.Empty
                        };

                        if (!string.IsNullOrEmpty(opPrefix))
                        {
                            builderName = obj.Text.Trim();
                            fieldName = key;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
