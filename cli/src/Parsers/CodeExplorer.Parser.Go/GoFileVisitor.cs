using CodeExplorer.Parser.Go.Libraries;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go;

public class GoFileVisitor : BaseParserVisitor
{
    private readonly GoParser _parser;

    public GoFileVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        GoParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
        : base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
        _parser = parser;
    }

    protected override string? MapNodeType(Node node)
    {
        if (IsGoHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (IsGoEntryPoint(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (node.Is(TreeSitterSyntax.Go.TypeSpec))
        {
            return node.Children.Any(c => c.Is(TreeSitterSyntax.Go.InterfaceType)) ? "Interface" : "Class";
        }

        if (node.IsAny(TreeSitterSyntax.Go.TypeDeclaration, TreeSitterSyntax.Go.StructType, TreeSitterSyntax.Go.InterfaceType))
        {
            return null;
        }

        return base.MapNodeType(node);
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (IsGoHttpClientCall(node))
        {
            return ExtractGoHttpClientTarget(node);
        }

        if (IsGoEntryPoint(node))
        {
            return ExtractGoEntryPointRoute(node, RelativePath);
        }

        return base.ExtractIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(Node node, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.Function &&
            symbolNode.Name == "main" &&
            !WorkspaceConventions.IsTestFilePath(RelativePath))
        {
            symbolNode.Children.Add(new SyntacticSymbol(CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint, "main-method:main", node) { Text = node.Text });
        }

        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint)
        {
            var routeVal = ExtractGoEntryPointRoute(node, RelativePath);
            if (!string.IsNullOrEmpty(routeVal))
            {
                var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
                if (args.IsValid() && args.Children.Count > 0)
                {
                    var handlerArg = args.Children.Skip(1).FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.SelectorExpression));
                    if (handlerArg.IsValid())
                    {
                        var handlerName = handlerArg.Text;
                        if (handlerName.Contains('.'))
                        {
                            handlerName = handlerName.Split('.').Last();
                        }
                        symbolNode.References.Add(new Reference(handlerName, routeVal.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                }
            }
        }
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        CollectVariable(node);
        VisitChildren(node, depth);
    }


    private void CollectVariable(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.Go.VarSpec, TreeSitterSyntax.Go.ConstSpec))
        {
            var identifiers = new List<Node>();
            var values = new List<Node>();
            var passedTypeOrEq = false;

            foreach (var child in node.Children)
            {
                if (child.Text == "=" || child.Type.Contains("type"))
                {
                    passedTypeOrEq = true;
                }
                else if (!passedTypeOrEq && child.Is(TreeSitterSyntax.Go.Identifier))
                {
                    identifiers.Add(child);
                }
                else if (passedTypeOrEq && !child.Is(TreeSitterSyntax.Symbols.Equals))
                {
                    values.Add(child);
                }
            }

            var isConstant = node.Is(TreeSitterSyntax.Go.ConstSpec);
            var scope = DetermineGoScope(node);

            for (var i = 0; i < identifiers.Count; i++)
            {
                var name = identifiers[i].Text;
                var initializerText = i < values.Count ? values[i].GetBoundedInitializerText() : "";

                RawVariables.Add(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    "",
                    node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1,
                    node.StartPosition.Column + 1,
                    node.EndPosition.Column + 1
                ));
            }
        }
        else if (node.Is(TreeSitterSyntax.Go.ShortVarDeclaration))
        {
            var leftNode = node.Children.FirstOrDefault();
            var rightNode = node.Children.LastOrDefault();

            if (leftNode.IsValid() && rightNode.IsValid() && leftNode.Id != rightNode.Id)
            {
                var names = new List<string>();
                if (leftNode.Is(TreeSitterSyntax.Go.ExpressionList))
                {
                    names.AddRange(leftNode.Children.Where(c => c.Is(TreeSitterSyntax.Go.Identifier)).Select(c => c.Text));
                }
                else if (leftNode.Is(TreeSitterSyntax.Go.Identifier))
                {
                    names.Add(leftNode.Text);
                }

                var values = new List<string>();
                if (rightNode.Is(TreeSitterSyntax.Go.ExpressionList))
                {
                    values.AddRange(rightNode.Children.Select(c => c.Text));
                }
                else
                {
                    values.Add(rightNode.Text);
                }

                var scope = DetermineGoScope(node);
                for (var i = 0; i < names.Count; i++)
                {
                    var name = names[i];
                    var initializerText = i < values.Count ? values[i] : "";

                    RawVariables.Add(new RawVariable(
                        name,
                        initializerText,
                        scope,
                        false,
                        "",
                        node.StartPosition.Row + 1,
                        node.EndPosition.Row + 1,
                        node.StartPosition.Column + 1,
                        node.EndPosition.Column + 1
                    ));
                }
            }
        }
    }

    private static string DetermineGoScope(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.Go.TypeSpec, TreeSitterSyntax.Go.StructType, TreeSitterSyntax.Go.InterfaceType))
                return "class";
            if (curr.IsAny(TreeSitterSyntax.Go.FunctionDeclaration, TreeSitterSyntax.Go.MethodDeclaration, TreeSitterSyntax.Go.Block))
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static bool IsGoEntryPoint(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (operand.IsValid())
            {
                var opText = operand.Text;
                if (opText is "slog" or "zap" || opText.Contains("log", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (opText.Contains("header", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("context", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("firestore", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("bulkWriter", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("doc", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("ref", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("repo", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("repository", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("store", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("cache", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("db", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (opText == "http" ||
                    opText.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains("resty", StringComparison.OrdinalIgnoreCase) ||
                    opText.Contains(".R()"))
                {
                    if (field.IsValid() && field.Text is not ("HandleFunc" or "Handle"))
                    {
                        return false;
                    }
                }
            }

            if (field.IsValid())
            {
                var methodName = field.Text;
                if (methodName == "Any")
                {
                    if (!operand.IsValid()) return false;
                    var op = operand.Text;
                    if (op is not ("r" or "router" or "app" or "e" or "mux" or "group" or "g" or "api" or "v1") &&
                        !op.EndsWith("Mux", StringComparison.OrdinalIgnoreCase) &&
                        !op.EndsWith("Router", StringComparison.OrdinalIgnoreCase) &&
                        !op.EndsWith("Group", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    return true;
                }

                if (methodName is "HandleFunc" or "Handle" or "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS"
                    or "Get" or "Post" or "Put" or "Delete" or "Patch" or "Options" or "Head" or "All")
                {
                    var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
                    if (!args.IsValid()) return false;
                    var exprArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();
                    if (exprArgs.Count < 2) return false;

                    var firstArg = exprArgs[0];
                    if (firstArg.Text is "ctx" || firstArg.Text.Contains("context", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var routeVal = GoAstHelper.ResolveStringOrVariable(firstArg);
                    if (string.IsNullOrEmpty(routeVal) ||
                        routeVal.StartWithIC("http://") ||
                        routeVal.StartWithIC("https://"))
                    {
                        return false;
                    }

                    if (methodName == "Handle" && exprArgs.Count >= 2 &&
                        (routeVal is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD"))
                    {
                        return false;
                    }

                    return true;
                }
            }
        }
        return false;
    }

    private static string? ExtractGoEntryPointRoute(Node callNode, string? relativePath = null)
    {
        var func = callNode.GetFunctionNode();
        if (!func.IsValid()) return null;

        var method = "GET";
        var prefix = "";
        if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid())
            {
                var methodName = field.Text;
                if (methodName != "HandleFunc" && methodName != "Handle" && !methodName.Equals("Any", StringComparison.OrdinalIgnoreCase) && !methodName.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    method = methodName.ToUpperInvariant();
                }
            }

            var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
            if (operand.IsValid())
            {
                prefix = GoAstHelper.ResolveRoutePrefix(callNode, operand);
            }
        }

        var args = callNode.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        string? routeVal = null;
        if (args.IsValid() && args.Children.Count > 0)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.IsAny(
                TreeSitterSyntax.Go.InterpretedStringLiteral,
                TreeSitterSyntax.Go.StringLiteral,
                TreeSitterSyntax.Go.RawStringLiteral,
                TreeSitterSyntax.Go.Identifier,
                TreeSitterSyntax.Common.BinaryExpression));
            if (firstArg.IsValid())
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(firstArg);
                if (!string.IsNullOrEmpty(resolved))
                {
                    routeVal = resolved.Trim('"', '`');
                }
            }
        }

        if (string.IsNullOrEmpty(routeVal)) return null;

        // Check if chained with .Methods(...) in gorilla/mux
        // e.g. r.HandleFunc(...).Methods(http.MethodGet, http.MethodHead)
        var outerMethod = FindChainedMethods(callNode);
        if (!string.IsNullOrEmpty(outerMethod))
        {
            method = outerMethod;
        }

        if (!string.IsNullOrEmpty(routeVal))
        {
            // Support Go 1.22+ ServeMux patterns, e.g. "GET /api/v1/bundles" or "POST /api/v1/bundles"
            var spaceIdx = routeVal.IndexOf(' ');
            if (spaceIdx > 0)
            {
                var candidateMethod = routeVal[..spaceIdx].ToUpperInvariant();
                if (candidateMethod is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD")
                {
                    method = candidateMethod;
                    routeVal = routeVal[(spaceIdx + 1)..].Trim();
                }
            }
            else
            {
                // Support pre-formatted or colon-separated routes like "GET:/api/v1/bundles"
                var colonIdx = routeVal.IndexOf(':');
                if (colonIdx > 0)
                {
                    var candidateMethod = routeVal[..colonIdx].ToUpperInvariant();
                    if (candidateMethod is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD")
                    {
                        method = candidateMethod;
                        routeVal = routeVal[(colonIdx + 1)..].Trim();
                    }
                }
            }
        }

        routeVal = GoAstHelper.CombineRoutes(prefix, routeVal);

        if (!routeVal.StartsWith("/v", StringComparison.OrdinalIgnoreCase))
        {
            if (RouteDictionaryRegistry.TryGetMountPrefix(relativePath, out var mountPrefix))
            {
                routeVal = GoAstHelper.CombineRoutes(mountPrefix, routeVal);
            }
            else if (!string.IsNullOrEmpty(relativePath))
            {
                var vPathMatch = System.Text.RegularExpressions.Regex.Match(relativePath.Replace('\\', '/'), @"/(v\d+)/");
                if (vPathMatch.Success)
                {
                    routeVal = GoAstHelper.CombineRoutes("/" + vPathMatch.Groups[1].Value, routeVal);
                }
            }
        }

        return $"{method}:{routeVal}";
    }

    private static string? FindChainedMethods(Node callNode)
    {
        var parent = callNode.Parent;
        while (parent.IsValid())
        {
            if (parent.Is(TreeSitterSyntax.Go.SelectorExpression))
            {
                var field = parent.GetChildForField(TreeSitterSyntax.Fields.Field);
                if (field.IsValid() && field.Text == "Methods")
                {
                    var outerCall = parent.Parent;
                    if (outerCall.IsValid() && outerCall.Is(TreeSitterSyntax.Go.CallExpression))
                    {
                        var mArgs = outerCall.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
                        if (mArgs.IsValid() && mArgs.Children.Count > 0)
                        {
                            var methods = new List<string>();
                            foreach (var mArg in mArgs.Children)
                            {
                                var txt = mArg.Text.Trim('"', '`');
                                if (txt.StartsWith("http.Method", StringComparison.OrdinalIgnoreCase))
                                {
                                    methods.Add(txt["http.Method".Length..].ToUpperInvariant());
                                }
                                else if (!string.IsNullOrEmpty(txt) && !txt.Contains('(') && !txt.Contains(','))
                                {
                                    methods.Add(txt.ToUpperInvariant());
                                }
                            }
                            if (methods.Count > 0)
                            {
                                return methods[0];
                            }
                        }
                    }
                }
            }
            if (parent.Is(TreeSitterSyntax.Go.ExpressionStatement) || parent.Is(TreeSitterSyntax.Go.Block))
            {
                break;
            }
            parent = parent.Parent;
        }
        return null;
    }

    private static bool IsGoHttpClientCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (operand.IsValid() && field.IsValid())
            {
                var objName = operand.Text;
                var methodName = field.Text;

                if (objName == "http")
                {
                    return methodName is "Get" or "Post" or "Head" or "PostForm" or "NewRequest" or "NewRequestWithContext";
                }
                if (objName.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                    objName.Contains("req", StringComparison.OrdinalIgnoreCase) ||
                    objName is "r" or "c")
                {
                    return methodName is "Get" or "Post" or "Put" or "Delete" or "Patch" or "Head" or "PostForm" or "Do";
                }
            }
        }
        return false;
    }

    private static string? ExtractGoHttpClientTarget(Node node)
    {
        var func = node.GetFunctionNode();
        var methodName = "";
        if (func.IsValid() && func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid()) methodName = field.Text;
        }

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            int targetArgIndex = 0;
            if (methodName == "NewRequest" && args.Count > 1)
            {
                targetArgIndex = 1;
            }
            else if (methodName == "NewRequestWithContext" && args.Count > 2)
            {
                targetArgIndex = 2;
            }

            if (targetArgIndex < args.Count)
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(args[targetArgIndex]);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            foreach (var arg in args)
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(arg);
                if (!string.IsNullOrEmpty(resolved) && (resolved.Contains('/') || resolved.StartsWith("http")))
                {
                    return resolved;
                }
            }
        }

        return "http:unknown-service";
    }
}
