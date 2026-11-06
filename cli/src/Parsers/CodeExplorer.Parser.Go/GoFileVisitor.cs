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

        if (IsGoEntryPoint(node) || IsGoEndpointLiteral(node, out _, out _, out _))
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

        if (IsGoEndpointLiteral(node, out var epMethods, out var epRoute, out _))
        {
            var primaryMethod = epMethods.Count > 0 ? epMethods[0] : "ANY";
            return $"{primaryMethod}:{epRoute}";
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
            var cleanName = "main";
            if (!string.IsNullOrEmpty(RelativePath))
            {
                var cleanRel = RelativePath.Replace('\\', '/').Trim('/');
                var dir = Path.GetDirectoryName(cleanRel)?.Replace('\\', '/').Trim('/');
                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName) && !dirName.Equals("src", StringComparison.OrdinalIgnoreCase) && !dirName.Equals("cmd", StringComparison.OrdinalIgnoreCase))
                {
                    cleanName = dirName;
                }
                else
                {
                    var fn = Path.GetFileNameWithoutExtension(cleanRel);
                    if (!string.IsNullOrEmpty(fn) && !fn.Equals("main", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanName = fn;
                    }
                }
            }
            symbolNode.Children.Add(new SyntacticSymbol(CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint, $"main-method:{cleanName}", node) { Text = node.Text });
        }

        if (symbolNode.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint)
        {
            if (IsGoEndpointLiteral(node, out var epMethods, out var epRoute, out var epHandler))
            {
                symbolNode.Protocol = "REST";
                symbolNode.Properties["protocol"] = "REST";
                var primaryMethod = epMethods.Count > 0 ? epMethods[0] : "ANY";
                symbolNode.Properties["httpMethod"] = primaryMethod;
                symbolNode.Properties["routeTemplate"] = epRoute;

                if (!string.IsNullOrEmpty(epHandler))
                {
                    symbolNode.References.Add(new Reference(epHandler, epRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
                }

                for (var i = 1; i < epMethods.Count; i++)
                {
                    var secMethod = epMethods[i];
                    var secSymbol = new SyntacticSymbol(CodeExplorer.Cypher.Common.SyntacticSymbolKind.EntryPoint, $"{secMethod}:{epRoute}", node)
                    {
                        Protocol = "REST"
                    };
                    secSymbol.Properties["protocol"] = "REST";
                    secSymbol.Properties["httpMethod"] = secMethod;
                    secSymbol.Properties["routeTemplate"] = epRoute;
                    if (!string.IsNullOrEmpty(epHandler))
                    {
                        secSymbol.References.Add(new Reference(epHandler, epRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                    symbolNode.Children.Add(secSymbol);
                }
            }
            else
            {
                var routeVal = ExtractGoEntryPointRoute(node, RelativePath);
                if (!string.IsNullOrEmpty(routeVal))
                {
                    var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
                    if (args.IsValid() && args.Children.Count > 0)
                    {
                        var nonStringArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",") &&
                            !c.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.RawStringLiteral, TreeSitterSyntax.Go.StringLiteral)).ToList();
                        var handlerArg = nonStringArgs.Count > 1 ? nonStringArgs[1] : nonStringArgs.FirstOrDefault();
                        if (handlerArg.IsValid())
                        {
                            var handlerName = handlerArg.Text;
                            if (handlerName.Contains('('))
                            {
                                handlerName = handlerName[..handlerName.IndexOf('(')].Trim();
                            }
                            if (handlerName.Contains('.'))
                            {
                                handlerName = handlerName.Split('.').Last();
                            }
                            if (!string.IsNullOrEmpty(handlerName) && !handlerName.StartsWith('/'))
                            {
                                symbolNode.References.Add(new Reference(handlerName, routeVal.Replace(":", " "), OntologyConstants.Relationships.Implements));
                            }
                        }
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

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(initializerText) && initializerText.StartsWith('/'))
                {
                    RouteDictionaryRegistry.Register(name, initializerText, null);
                }

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

                if (methodName is "Handler" or "HandlerFunc")
                {
                    if (operand.IsValid() && TryExtractChainedRoute(operand, out _, out _))
                    {
                        return true;
                    }
                }

                if (methodName is "HandleFunc" or "Handle" or "Method" or "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS"
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

                    if ((methodName is "Handle" or "Method") && exprArgs.Count >= 2 &&
                        (routeVal is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD"))
                    {
                        var secondArg = exprArgs[1];
                        var secondRoute = GoAstHelper.ResolveStringOrVariable(secondArg);
                        return !string.IsNullOrEmpty(secondRoute) && !secondRoute.StartWithIC(HttpProtocolPrefix) && !secondRoute.StartWithIC(HttpsProtocolPrefix);
                    }

                    if (string.IsNullOrEmpty(routeVal) ||
                        routeVal.StartWithIC(HttpProtocolPrefix) ||
                        routeVal.StartWithIC(HttpsProtocolPrefix))
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
            var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);

            if (field.IsValid() && operand.IsValid() && (field.Text is "Handler" or "HandlerFunc"))
            {
                if (TryExtractChainedRoute(operand, out var cRoute, out var cMethod))
                {
                    cRoute = GoAstHelper.CombineRoutes(prefix, cRoute);
                    if (RouteDictionaryRegistry.TryGetMountPrefix(relativePath, out var mountPrefix))
                    {
                        cRoute = GoAstHelper.CombineRoutes(mountPrefix, cRoute);
                    }
                    return $"{cMethod}:{cRoute}";
                }
            }

            if (field.IsValid())
            {
                var methodName = field.Text;
                if (methodName != "HandleFunc" && methodName != "Handle" && !methodName.Equals("Any", StringComparison.OrdinalIgnoreCase) && !methodName.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    method = methodName.ToUpperInvariant();
                }
            }

            if (operand.IsValid())
            {
                prefix = GoAstHelper.ResolveRoutePrefix(callNode, operand);
            }
        }

        var args = callNode.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        string? routeVal = null;
        if (args.IsValid() && args.Children.Count > 0)
        {
            var exprArgs = args.Children.Where(c => !c.Type.Equals("(") && !c.Type.Equals(")") && !c.Type.Equals(",")).ToList();
            if (exprArgs.Count >= 2)
            {
                var firstCandidate = GoAstHelper.ResolveStringOrVariable(exprArgs[0]);
                if (firstCandidate is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD")
                {
                    method = firstCandidate.ToUpperInvariant();
                    var secondVal = GoAstHelper.ResolveStringOrVariable(exprArgs[1]);
                    if (!string.IsNullOrEmpty(secondVal))
                    {
                        routeVal = secondVal.Trim('"', '`');
                    }
                }
            }

            if (string.IsNullOrEmpty(routeVal))
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

    private const string HttpProtocolPrefix = "http://";
    private const string HttpsProtocolPrefix = "https://";
    private const string MethodAny = "ANY";
    private const string LiteralValueNode = "literal_value";

    private const string FieldRoute = "Route";
    private const string FieldPath = "Path";
    private const string FieldMethods = "Methods";
    private const string FieldMethod = "Method";
    private const string FieldVersion = "Version";
    private const string FieldHandler = "Handler";
    private const string FieldHandlerFunc = "HandlerFunc";
    private const string FieldSettings = "Settings";
    private const string FieldGroup = "Group";

    private static bool IsGoEndpointLiteral(Node node, out List<string> methods, out string fullRoute, out string? handlerName)
    {
        methods = [];
        fullRoute = "/";
        handlerName = null;

        if (node.Type is not (TreeSitterSyntax.Go.CompositeLiteral or "literal_value"))
        {
            return false;
        }

        var keyedElements = GetKeyedElements(node);
        if (keyedElements.Count == 0) return false;

        if (!keyedElements.TryGetValue(FieldRoute, out var routeNode) &&
            !keyedElements.TryGetValue(FieldPath, out routeNode))
        {
            return false;
        }

        var routeVal = GoAstHelper.ResolveStringOrVariable(routeNode);
        if (string.IsNullOrWhiteSpace(routeVal))
        {
            routeVal = routeNode.Text.Trim('"', '`');
            if (string.IsNullOrWhiteSpace(routeVal) || routeVal.Contains('\n') || routeVal.Length > 200)
            {
                return false;
            }
        }

        var hasOtherEndpointKey = keyedElements.ContainsKey(FieldMethods) ||
                                  keyedElements.ContainsKey(FieldMethod) ||
                                  keyedElements.ContainsKey(FieldVersion) ||
                                  keyedElements.ContainsKey(FieldHandler) ||
                                  keyedElements.ContainsKey(FieldHandlerFunc) ||
                                  keyedElements.ContainsKey(FieldSettings) ||
                                  keyedElements.ContainsKey(FieldGroup);

        if (!hasOtherEndpointKey)
        {
            var typeNode = node.GetChildForField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid() && (typeNode.Text.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) ||
                                       typeNode.Text.EndsWith("Route", StringComparison.OrdinalIgnoreCase)))
            {
                hasOtherEndpointKey = true;
            }
        }

        if (!hasOtherEndpointKey) return false;

        if (keyedElements.TryGetValue(FieldMethods, out var methodsNode) ||
            keyedElements.TryGetValue(FieldMethod, out methodsNode))
        {
            methods = ExtractMethodsFromNode(methodsNode);
        }

        if (methods.Count == 0)
        {
            methods.Add(MethodAny);
        }

        string? versionStr = null;
        if (keyedElements.TryGetValue(FieldVersion, out var versionNode))
        {
            versionStr = GoAstHelper.ResolveStringOrVariable(versionNode);
            if (string.IsNullOrEmpty(versionStr))
            {
                var vText = versionNode.Text.Trim('"', '`');
                if (ConstantRegistry.TryResolve(null, vText, out var resolvedV) && !string.IsNullOrEmpty(resolvedV))
                {
                    versionStr = resolvedV;
                }
                else if (int.TryParse(vText, out var vInt))
                {
                    versionStr = $"v{vInt}.0";
                }
                else if (vText.Contains('.') && double.TryParse(vText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    versionStr = vText.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? vText : $"v{vText}";
                }
                else
                {
                    var vMatch = System.Text.RegularExpressions.Regex.Match(
                        vText,
                        @"(?:api)?version(v\d+)(?:(alpha\d+|beta\d+))?",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (vMatch.Success)
                    {
                        var majorPart = vMatch.Groups[1].Value.ToLowerInvariant();
                        var num = majorPart[1..];
                        var sub = vMatch.Groups[2].Success ? "-" + vMatch.Groups[2].Value.ToLowerInvariant() : "";
                        versionStr = $"v{num}.0{sub}";
                    }
                    else if (vText.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        versionStr = vText;
                    }
                }
            }
        }

        routeVal = routeVal.Trim();
        if (!string.IsNullOrEmpty(versionStr))
        {
            versionStr = versionStr.Trim('/');
            var cleanRoute = routeVal.TrimStart('/');
            fullRoute = $"/{versionStr}/{cleanRoute}";
        }
        else
        {
            fullRoute = routeVal.StartsWith('/') ? routeVal : $"/{routeVal}";
        }

        if (keyedElements.TryGetValue(FieldHandler, out var hNode) ||
            keyedElements.TryGetValue(FieldHandlerFunc, out hNode))
        {
            handlerName = ExtractHandlerName(hNode);
        }

        return true;
    }

    private static Dictionary<string, Node> GetKeyedElements(Node node)
    {
        var result = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        var container = node;
        if (node.Type is TreeSitterSyntax.Go.CompositeLiteral)
        {
            var body = node.GetChildForField(TreeSitterSyntax.Fields.Body);
            if (body.IsValid()) container = body;
            else
            {
                var litVal = node.FindChildOfType(LiteralValueNode);
                if (litVal.IsValid()) container = litVal;
            }
        }

        foreach (var rawChild in container.Children)
        {
            var elem = rawChild;
            if (elem.Type == "literal_element" && elem.Children.Count > 0)
            {
                elem = elem.Children[0];
            }

            if (elem.Type is "keyed_element" && elem.Children.Count >= 2)
            {
                var keyChild = elem.Children[0];
                if (keyChild.Type == "literal_element" && keyChild.Children.Count > 0)
                {
                    keyChild = keyChild.Children[0];
                }

                var valChild = elem.Children[^1];
                if (valChild.Type == "literal_element" && valChild.Children.Count > 0)
                {
                    valChild = valChild.Children[0];
                }

                var keyName = keyChild.Text.Trim();
                if (!string.IsNullOrEmpty(keyName) && valChild.IsValid())
                {
                    result[keyName] = valChild;
                }
            }
        }

        return result;
    }

    private static List<string> ExtractMethodsFromNode(Node methodsNode)
    {
        var methods = new List<string>();
        if (!methodsNode.IsValid()) return methods;

        if (methodsNode.Type is TreeSitterSyntax.Go.CompositeLiteral or "literal_value")
        {
            var elements = methodsNode.DescendantsAndSelf()
                .Where(d => d.Type is TreeSitterSyntax.Go.SelectorExpression or TreeSitterSyntax.Go.InterpretedStringLiteral or TreeSitterSyntax.Go.Identifier or TreeSitterSyntax.Go.RawStringLiteral)
                .ToList();

            foreach (var elem in elements)
            {
                var text = elem.Text.Trim('"', '`');
                var method = CleanHttpMethod(text);
                if (!string.IsNullOrEmpty(method) && !methods.Contains(method))
                {
                    methods.Add(method);
                }
            }
        }
        else
        {
            var text = methodsNode.Text.Trim('"', '`');
            var method = CleanHttpMethod(text);
            if (!string.IsNullOrEmpty(method))
            {
                methods.Add(method);
            }
        }

        return methods;
    }

    private static string? CleanHttpMethod(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.EndsWith("MethodGet", StringComparison.OrdinalIgnoreCase) || text.Equals("GET", StringComparison.OrdinalIgnoreCase)) return "GET";
        if (text.EndsWith("MethodPost", StringComparison.OrdinalIgnoreCase) || text.Equals("POST", StringComparison.OrdinalIgnoreCase)) return "POST";
        if (text.EndsWith("MethodPut", StringComparison.OrdinalIgnoreCase) || text.Equals("PUT", StringComparison.OrdinalIgnoreCase)) return "PUT";
        if (text.EndsWith("MethodDelete", StringComparison.OrdinalIgnoreCase) || text.Equals("DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
        if (text.EndsWith("MethodPatch", StringComparison.OrdinalIgnoreCase) || text.Equals("PATCH", StringComparison.OrdinalIgnoreCase)) return "PATCH";
        if (text.EndsWith("MethodOptions", StringComparison.OrdinalIgnoreCase) || text.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)) return "OPTIONS";
        if (text.EndsWith("MethodHead", StringComparison.OrdinalIgnoreCase) || text.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) return "HEAD";
        if (text.EndsWith("MethodConnect", StringComparison.OrdinalIgnoreCase) || text.Equals("CONNECT", StringComparison.OrdinalIgnoreCase)) return "CONNECT";
        if (text.EndsWith("MethodTrace", StringComparison.OrdinalIgnoreCase) || text.Equals("TRACE", StringComparison.OrdinalIgnoreCase)) return "TRACE";
        return null;
    }

    private static bool TryExtractChainedRoute(Node receiverNode, out string route, out string method)
    {
        route = string.Empty;
        method = "GET";

        var curr = receiverNode;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.Go.CallExpression))
            {
                var fn = curr.GetFunctionNode();
                if (fn.IsValid() && fn.Is(TreeSitterSyntax.Go.SelectorExpression))
                {
                    var field = fn.GetChildForField(TreeSitterSyntax.Fields.Field);
                    if (field.IsValid())
                    {
                        var mName = field.Text;
                        var args = GoAstHelper.GetCallArguments(curr);
                        if (mName == "Path" && args.Count > 0 && string.IsNullOrEmpty(route))
                        {
                            var r = GoAstHelper.ResolveStringOrVariable(args[0])?.Trim('"', '`');
                            if (string.IsNullOrEmpty(r) && args[0].Text.Trim('"', '`').StartsWith('/'))
                            {
                                r = args[0].Text.Trim('"', '`');
                            }
                            if (!string.IsNullOrEmpty(r)) route = r;
                        }
                        else if (mName is "Method" or "Methods" && args.Count > 0)
                        {
                            var m = CleanHttpMethod(args[0].Text);
                            if (!string.IsNullOrEmpty(m)) method = m;
                        }
                    }
                    curr = fn.GetChildForField(TreeSitterSyntax.Fields.Operand);
                    continue;
                }
            }
            break;
        }

        return !string.IsNullOrEmpty(route);
    }

    private static string? ExtractHandlerName(Node node)
    {
        if (node.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid()) return ExtractHandlerName(func);
        }
        if (node.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var f = node.GetChildForField(TreeSitterSyntax.Fields.Field);
            return f.IsValid() ? f.Text : node.Text;
        }
        if (node.Is(TreeSitterSyntax.Go.Identifier))
        {
            return node.Text;
        }
        return null;
    }
}
