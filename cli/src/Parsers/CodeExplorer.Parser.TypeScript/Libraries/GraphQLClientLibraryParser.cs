using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GraphQLClientLibraryParser : ISemanticExtension
{
    public string Type => "api";

    public string Name => "GraphQL Client";

    public string Id => "graphql-client";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@apollo/client",
        "graphql-request",
        "urql",
        "@urql/core",
        "graphql-tag",
        "@graphql-typed-document-node/core"
    ];

    public bool IsBuiltIn => true;

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (!node.IsValid()) return null;

        // 1. VariableDeclarator assigned a gql`...` template, graphql(`...`) call, or TypedDocumentNode AST object:
        //    e.g., `export const PublishedItemsDocument = gql`...`` or `{ kind: 'Document', definitions: [...] }`
        if (node.Is(TreeSitterSyntax.TypeScript.VariableDeclarator) &&
            TryExtractDocumentOperationsFromDeclarator(node, out var ops) &&
            ops.Count > 0)
        {
            return OntologyConstants.NodeLabels.Function;
        }

        // 2. Inline gql`...` or graphql(`...`) not directly owned by a VariableDeclarator
        if (!IsOwnedByVariableDeclarator(node) &&
            TryExtractInlineGraphQLOperations(node, out var inlineOps) &&
            inlineOps.Count > 0 &&
            inlineOps[0].RootFields.Count > 0)
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        // 3. ApolloClient / HttpLink / createClient / request(url, query)
        if (IsGraphQLTarget(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (!node.IsValid()) return null;

        if (node.Is(TreeSitterSyntax.TypeScript.VariableDeclarator) &&
            TryExtractDocumentOperationsFromDeclarator(node, out var ops) &&
            ops.Count > 0)
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier));
            return nameNode?.Text;
        }

        if (!IsOwnedByVariableDeclarator(node) &&
            TryExtractInlineGraphQLOperations(node, out var inlineOps) &&
            inlineOps.Count > 0 &&
            inlineOps[0].RootFields.Count > 0)
        {
            var op = inlineOps[0];
            return $"graphql:{op.OperationType.ToUpperInvariant()}:{op.RootFields[0]}";
        }

        if (IsGraphQLTarget(node))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].IsValid())
            {
                if (args[0].Is(TreeSitterSyntax.TypeScript.Object))
                {
                    if (AstHelper.TryGetObjectProperty(args[0], "uri", out var uriNode) && uriNode.IsValid())
                    {
                        var resolved = AstHelper.ResolveStringOrTemplate(uriNode);
                        if (!string.IsNullOrWhiteSpace(resolved)) return FormatTarget(resolved);
                    }
                    if (AstHelper.TryGetObjectProperty(args[0], "url", out var urlNode) && urlNode.IsValid())
                    {
                        var resolved = AstHelper.ResolveStringOrTemplate(urlNode);
                        if (!string.IsNullOrWhiteSpace(resolved)) return FormatTarget(resolved);
                    }
                }

                var directResolved = AstHelper.ResolveStringOrTemplate(args[0]);
                if (!string.IsNullOrWhiteSpace(directResolved))
                {
                    return FormatTarget(directResolved);
                }
            }

            return "graphql";
        }

        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (symbol.Kind == OntologyConstants.NodeLabels.Function &&
            node.Is(TreeSitterSyntax.TypeScript.VariableDeclarator) &&
            TryExtractDocumentOperationsFromDeclarator(node, out var ops) &&
            ops.Count > 0)
        {
            var allRootFields = new List<string>();
            var aliases = new List<string>();

            foreach (var op in ops)
            {
                if (!string.IsNullOrEmpty(op.OperationName))
                {
                    symbol.Properties.TryAdd("graphql_operation", op.OperationName);
                    aliases.AddRange(GraphQLFileParser.BuildCodegenAliases(op.OperationName, op.OperationType));
                }
                symbol.Properties.TryAdd("operation_type", op.OperationType);

                foreach (var rf in op.RootFields)
                {
                    if (!allRootFields.Contains(rf, StringComparer.Ordinal))
                    {
                        allRootFields.Add(rf);
                    }

                    var extIdentifier = $"graphql:{op.OperationType.ToUpperInvariant()}:{rf}";
                    symbol.Children.Add(new SyntacticSymbol(
                        OntologyConstants.NodeLabels.ExternalService,
                        extIdentifier,
                        node)
                    {
                        Text = node.Text
                    });
                }
            }

            if (allRootFields.Count > 0)
            {
                symbol.Properties["graphql_fields"] = string.Join(",", allRootFields);
                if (allRootFields.Count == 1)
                {
                    symbol.Properties["graphql_field"] = allRootFields[0];
                }
            }

            if (aliases.Count > 0)
            {
                symbol.Properties["graphql_aliases"] = string.Join(",", aliases.Distinct(StringComparer.Ordinal));
            }
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.IsValid()) return;

        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var fnNode = node.GetFunctionNode();
            if (!fnNode.IsValid()) return;

            var fnText = fnNode.Text;
            var shortFn = fnText.Contains('.') ? fnText[(fnText.LastIndexOf('.') + 1)..] : fnText;

            // 1. Hook calls passing a DocumentNode: useQuery(PublishedItemsDocument), Apollo.useQuery(PublishedItemsDocument, options)
            if (shortFn is "useQuery" or "useLazyQuery" or "useSuspenseQuery" or "useMutation" or "useSubscription")
            {
                var args = AstHelper.GetCallArguments(node);
                if (args.Count > 0 && args[0].IsValid())
                {
                    var arg0 = args[0];
                    if (arg0.IsAny(TreeSitterSyntax.TypeScript.Identifier, TreeSitterSyntax.TypeScript.MemberExpression))
                    {
                        var docName = arg0.Text.Trim();
                        if (!string.IsNullOrEmpty(docName))
                        {
                            references.Add(new Reference(scopeSymbolId, docName, OntologyConstants.Relationships.Calls));
                        }
                    }
                    else if (arg0.Is(TreeSitterSyntax.TypeScript.Object) &&
                             AstHelper.TryGetObjectProperty(arg0, "query", out var qNode) &&
                             qNode.IsValid() &&
                             qNode.IsAny(TreeSitterSyntax.TypeScript.Identifier, TreeSitterSyntax.TypeScript.MemberExpression))
                    {
                        references.Add(new Reference(scopeSymbolId, qNode.Text.Trim(), OntologyConstants.Relationships.Calls));
                    }
                }
            }
            // 2. Imperative ApolloClient calls: client.query({ query: PublishedItemsDocument }), client.mutate({ mutation: ... })
            else if (shortFn is "query" or "mutate" or "watchQuery" or "subscribe")
            {
                var args = AstHelper.GetCallArguments(node);
                if (args.Count > 0 && args[0].IsValid() && args[0].Is(TreeSitterSyntax.TypeScript.Object))
                {
                    foreach (var propKey in new[] { "query", "mutation", "subscription", "document" })
                    {
                        if (AstHelper.TryGetObjectProperty(args[0], propKey, out var docProp) &&
                            docProp.IsValid() &&
                            docProp.IsAny(TreeSitterSyntax.TypeScript.Identifier, TreeSitterSyntax.TypeScript.MemberExpression))
                        {
                            references.Add(new Reference(scopeSymbolId, docProp.Text.Trim(), OntologyConstants.Relationships.Calls));
                        }
                    }
                }
            }
        }
    }

    private static bool TryExtractDocumentOperationsFromDeclarator(
        Node declaratorNode,
        out List<GraphQLParsedOperation> operations)
    {
        operations = [];
        var valNode = declaratorNode.GetField(TreeSitterSyntax.Fields.Value);
        if (!valNode.IsValid()) return false;

        // Unwrap TypeScript `as unknown as DocumentNode<...>` or `({...}) as const`
        var curr = valNode;
        for (var i = 0; i < 5 && curr.IsValid(); i++)
        {
            if (curr.IsAny("as_expression", "satisfies_expression", "parenthesized_expression", "type_assertion"))
            {
                var inner = curr.Children.FirstOrDefault(c =>
                    c.IsNamed &&
                    !c.Is("type_identifier") &&
                    !c.Is("generic_type") &&
                    !c.Is("predefined_type"));
                if (inner.IsValid())
                {
                    curr = inner;
                    continue;
                }
            }
            break;
        }

        if (TryExtractInlineGraphQLOperations(curr, out operations) && operations.Count > 0)
        {
            return true;
        }

        if (curr.Is(TreeSitterSyntax.TypeScript.Object))
        {
            var typedDocOp = GraphQLDocumentExtractor.TryParseTypedDocumentAst(
                curr.Text,
                declaratorNode.StartPosition.Row + 1,
                declaratorNode.EndPosition.Row + 1);
            if (typedDocOp != null)
            {
                operations = [typedDocOp];
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractInlineGraphQLOperations(
        Node node,
        out List<GraphQLParsedOperation> operations)
    {
        operations = [];
        if (!node.IsValid()) return false;

        // 1. TaggedTemplateExpression: gql`...` or graphql`...`
        if (node.Is("tagged_template_expression"))
        {
            var tagNode = node.GetField("function") ?? node.Children.FirstOrDefault();
            var tplNode = node.GetField("arguments") ?? node.FindChildOfType(TreeSitterSyntax.TypeScript.TemplateString);
            if (tagNode.IsValid() && tplNode.IsValid())
            {
                var tagText = tagNode.Text.Trim();
                if (tagText is "gql" or "graphql" || tagText.EndsWith(".gql", StringComparison.Ordinal))
                {
                    var raw = tplNode.Text.Trim('`', '\'', '"');
                    var (ops, _) = GraphQLDocumentExtractor.ParseDocument(raw);
                    if (ops.Count > 0)
                    {
                        operations = ops;
                        return true;
                    }
                }
            }
        }

        // 2. CallExpression: gql(`...`) or graphql(`...`)
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var fnNode = node.GetFunctionNode();
            if (fnNode.IsValid())
            {
                var fnText = fnNode.Text.Trim();
                if (fnText is "gql" or "graphql")
                {
                    var args = AstHelper.GetCallArguments(node);
                    if (args.Count > 0 && args[0].IsValid() &&
                        args[0].IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString))
                    {
                        var raw = args[0].Text.Trim('`', '\'', '"');
                        var (ops, _) = GraphQLDocumentExtractor.ParseDocument(raw);
                        if (ops.Count > 0)
                        {
                            operations = ops;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool IsOwnedByVariableDeclarator(Node node)
    {
        var curr = node.Parent;
        for (var i = 0; i < 4 && curr.IsValid(); i++, curr = curr.Parent)
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
            {
                return true;
            }
            if (curr.Is(TreeSitterSyntax.TypeScript.CallExpression))
            {
                return false;
            }
        }
        return false;
    }

    private static bool IsGraphQLTarget(Node node)
    {
        if (!node.IsValid()) return false;

        // 1. new ApolloClient({ uri: '...' }) or new HttpLink({ uri: '...' }) or createClient({ url: '...' })
        if (node.Is(TreeSitterSyntax.TypeScript.NewExpression) || node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetField(TreeSitterSyntax.Fields.Constructor) ?? node.GetFunctionNode();
            if (!func.IsValid() || func.Text == "new")
            {
                func = node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.TypeScript.MemberExpression, TreeSitterSyntax.Common.TypeIdentifier));
            }

            if (func.IsValid())
            {
                var name = func.Text;
                if (name is "ApolloClient" or "HttpLink" or "createClient" ||
                    name.EndsWith(".ApolloClient", StringComparison.Ordinal) ||
                    name.EndsWith(".HttpLink", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        // 2. Direct call: request(url, query)
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid())
            {
                var name = func.Text.ToLowerInvariant();
                if (name is "request" or "graphqlclient.request" || name.EndsWith(".request"))
                {
                    var args = AstHelper.GetCallArguments(node);
                    if (args.Count >= 2)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string FormatTarget(string raw)
    {
        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var path = uri.AbsolutePath.TrimStart('/');
            return string.IsNullOrEmpty(path) ? uri.Host : $"{uri.Host}/{path}";
        }
        return raw.StartsWith('/') ? raw : $"/{raw}";
    }
}
