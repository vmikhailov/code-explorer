using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go;

public class GoFileVisitor : BaseParserVisitor
{
    private readonly GoParser _parser;

    public GoFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        GoParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
        : base(rootNode, activeLibraryParsers, relativePath, absoluteWorkspacePath, fileParser, libraryRegistry)
    {
        _parser = parser;
    }

    protected override string? MapNodeType(Node node)
    {
        if (IsGoEntryPoint(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsGoHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (node.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.StringLiteral, TreeSitterSyntax.Go.RawStringLiteral))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        if (node.Is(TreeSitterSyntax.Go.TypeSpec))
        {
            var isInterface = false;
            foreach (var child in node.Children)
            {
                if (child.Is(TreeSitterSyntax.Go.InterfaceType))
                {
                    isInterface = true;
                    break;
                }
            }
            return isInterface ? "Interface" : "Class";
        }

        return node.Type switch
        {
            TreeSitterSyntax.Go.FunctionDeclaration or TreeSitterSyntax.Go.MethodDeclaration => OntologyConstants.NodeLabels.Function,
            _ => null
        };
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (IsGoEntryPoint(node))
        {
            return ExtractGoEntryPointRoute(node);
        }

        if (IsGoHttpClientCall(node))
        {
            return ExtractGoHttpClientTarget(node);
        }

        if (node.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.StringLiteral, TreeSitterSyntax.Go.RawStringLiteral))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractGoIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(Node node, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
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

    private string? ExtractGoIdentifier(Node node)
    {
        var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            return nameNode.Text;
        }

        foreach (var child in node.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.VariableName))
            {
                return child.Text;
            }
        }

        foreach (var child in node.Children)
        {
            if (child.Type.Contains("name"))
            {
                return child.Text;
            }
        }

        return null;
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        CollectVariable(node);
        VisitChildren(node, depth);
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        var pathNode = node.GetChildForField(TreeSitterSyntax.Fields.Path) ?? node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Go.StringLiteral));
        if (pathNode.IsValid())
        {
            var importPath = pathNode.Text.Trim('"');
            RawImports.Add(new RawImport(importPath, "", ImportType.External));
            ResolveAndInjectLibraryParser(importPath);
        }
        VisitChildren(node, depth);
    }

    protected override string? FindCallName(Node callNode)
    {
        var expr = callNode.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (expr.IsValid() && expr.Id == IntPtr.Zero && callNode.Children.Count > 0)
        {
            expr = callNode.Children[0];
        }
        if (!expr.IsValid()) return null;

        if (expr.Is(TreeSitterSyntax.Go.Identifier))
        {
            return expr.Text;
        }
        if (expr.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var fieldChild = expr.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (fieldChild.IsValid()) return fieldChild.Text;
        }
        return null;
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
                    node.StartPosition.Row,
                    node.EndPosition.Row,
                    node.StartPosition.Column,
                    node.EndPosition.Column
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
                        node.StartPosition.Row,
                        node.EndPosition.Row,
                        node.StartPosition.Column,
                        node.EndPosition.Column
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
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid())
            {
                var methodName = field.Text;
                if (methodName is "HandleFunc" or "Handle" or "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "Any")
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
        if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid())
            {
                var methodName = field.Text;
                if (methodName != "HandleFunc" && methodName != "Handle" && methodName != "Any")
                {
                    method = methodName.ToUpperInvariant();
                }
            }
        }

        var args = callNode.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        var routeVal = "/";
        if (args.IsValid() && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.StringLiteral, TreeSitterSyntax.Go.RawStringLiteral));
            if (firstArg.IsValid())
            {
                routeVal = firstArg.Text.Trim('"', '`');
            }
        }

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
                if (objName.Contains("client") || objName.Contains("Client"))
                {
                    return methodName is "Get" or "Post" or "Head" or "PostForm" or "Do";
                }
            }
        }
        return false;
    }

    private static string? ExtractGoHttpClientTarget(Node node)
    {
        var args = node.FindChildOfType(TreeSitterSyntax.Go.ArgumentList);
        if (args.IsValid() && args.Children.Count > 1)
        {
            var firstStrArg = args.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.StringLiteral, TreeSitterSyntax.Go.RawStringLiteral));
            if (firstStrArg.IsValid())
            {
                var text = firstStrArg.Text.Trim('"', '`');
                if (text.Contains("://"))
                {
                    try
                    {
                        var uri = new Uri(text);
                        return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
                    }
                    catch
                    {
                    }
                }
                return $"http:{text}";
            }
        }
        return "http:unknown-service";
    }
}
