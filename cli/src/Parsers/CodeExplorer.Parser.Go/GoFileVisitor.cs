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
            return ExtractGoEntryPointRoute(node);
        }

        return base.ExtractIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(Node node, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
        if (symbolNode.Kind == OntologyConstants.NodeLabels.Function &&
            symbolNode.Name == "main" &&
            !WorkspaceConventions.IsTestFilePath(RelativePath))
        {
            symbolNode.Children.Add(new SyntacticSymbol(OntologyConstants.NodeLabels.EntryPoint, "main-method:main", node) { Text = node.Text });
        }

        if (symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var routeVal = ExtractGoEntryPointRoute(node);
            if (!string.IsNullOrEmpty(routeVal))
            {
                var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
                if (args.IsValid() && args.Children.Count > 1)
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
                var initializerText = i < values.Count ? values[i].Text : "";

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
            if (operand.IsValid() && (operand.Text == "http" ||
                                      operand.Text.Contains("client", StringComparison.OrdinalIgnoreCase) ||
                                      operand.Text.Contains("resty", StringComparison.OrdinalIgnoreCase) ||
                                      operand.Text.Contains(".R()")))
            {
                if (field.IsValid() && field.Text is not ("HandleFunc" or "Handle"))
                {
                    return false;
                }
            }

            if (field.IsValid())
            {
                var methodName = field.Text;
                if (methodName is "HandleFunc" or "Handle" or "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "Any"
                    or "Get" or "Post" or "Put" or "Delete" or "Patch" or "Options" or "Head" or "All")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string? ExtractGoEntryPointRoute(Node callNode)
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
        var routeVal = "/";
        if (args.IsValid() && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.StringLiteral, TreeSitterSyntax.Go.RawStringLiteral, TreeSitterSyntax.Go.Identifier));
            if (firstArg.IsValid())
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(firstArg);
                if (!string.IsNullOrEmpty(resolved))
                {
                    routeVal = resolved.Trim('"', '`');
                }
            }
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

        return $"{method}:{routeVal}";
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
