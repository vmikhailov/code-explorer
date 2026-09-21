using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Python.Libraries;
using TreeSitter;

namespace CodeExplorer.Parser.Python;

public class PythonFileVisitor : BaseParserVisitor
{
    private readonly PythonParser _parser;

    public PythonFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        PythonParser parser,
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
        if (IsPythonDecoratorEntryPoint(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsDjangoPath(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsPythonHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (node.Is(TreeSitterSyntax.Python.String))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }
        return node.Type switch
        {
            TreeSitterSyntax.Python.ClassDefinition => "Class",
            TreeSitterSyntax.Python.FunctionDefinition => OntologyConstants.NodeLabels.Function,
            _ => null
        };
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (IsPythonDecoratorEntryPoint(node))
        {
            return ExtractPythonDecoratorRoute(node);
        }

        if (IsDjangoPath(node))
        {
            return ExtractDjangoPathRoute(node);
        }

        if (IsPythonHttpClientCall(node))
        {
            return ExtractPythonHttpClientTarget(node);
        }

        if (node.Is(TreeSitterSyntax.Python.String))
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractPythonIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(Node node, SyntacticSymbol symbolNode, SyntacticSymbol parentNode)
    {
        if (symbolNode.Kind == OntologyConstants.NodeLabels.Function)
        {
            var parent = node.Parent;
            if (parent.IsValid() && parent.Is(TreeSitterSyntax.Python.DecoratedDefinition))
            {
                foreach (var child in parent.Children)
                {
                    if (IsPythonDecoratorEntryPoint(child))
                    {
                        var route = ExtractPythonDecoratorRoute(child);
                        if (!string.IsNullOrEmpty(route))
                        {
                            symbolNode.References.Add(new Reference("", route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                        }
                    }
                }
            }
        }
        else if (symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint && IsDjangoPath(node))
        {
            var route = ExtractDjangoPathRoute(node);
            if (!string.IsNullOrEmpty(route))
            {
                var args = node.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                if (args.IsValid() && args.Children.Count > 1)
                {
                    var viewArg = args.Children.Skip(1).FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Python.Identifier, TreeSitterSyntax.Python.Attribute));
                    if (viewArg.IsValid())
                    {
                        var viewName = viewArg.Text;
                        if (viewName.Contains('.'))
                        {
                            viewName = viewName.Split('.').Last();
                        }
                        symbolNode.References.Add(new Reference(viewName, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                }
            }
        }
    }

    private string? ExtractPythonIdentifier(Node node)
    {
        var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            return nameNode.Text;
        }

        foreach (var child in node.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Python.Identifier, TreeSitterSyntax.Python.VariableName))
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
        if (node.Is(TreeSitterSyntax.Python.ImportStatement))
        {
            foreach (var child in node.Children)
            {
                if (child.IsAny(TreeSitterSyntax.Python.DottedName, TreeSitterSyntax.Python.AliasedName))
                {
                    var importPath = child.Text;
                    RawImports.Add(new RawImport(importPath, "", ImportType.External));
                    ResolveAndInjectLibraryParser(importPath);
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.Python.ImportFromStatement))
        {
            var moduleNode = node.GetChildForField("module_name");
            if (!moduleNode.IsValid())
            {
                moduleNode = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.DottedName));
            }
            if (moduleNode.IsValid())
            {
                var importPath = moduleNode.Text;
                RawImports.Add(new RawImport(importPath, "", ImportType.External));
                ResolveAndInjectLibraryParser(importPath);
            }
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

        if (expr.Is(TreeSitterSyntax.Python.Identifier))
        {
            return expr.Text;
        }
        if (expr.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attrChild = expr.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attrChild.IsValid()) return attrChild.Text;
        }
        return null;
    }

    private void CollectVariable(Node node)
    {
        if (node.Is(TreeSitterSyntax.Python.Assignment))
        {
            var leftNode = node.GetChildForField(TreeSitterSyntax.Fields.Left);
            if (!leftNode.IsValid())
            {
                leftNode = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));
            }

            var rightNode = node.GetChildForField(TreeSitterSyntax.Fields.Right);
            if (!rightNode.IsValid())
            {
                var eqIdx = -1;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i].Text == "=")
                    {
                        eqIdx = i;
                        break;
                    }
                }
                if (eqIdx >= 0 && eqIdx + 1 < node.Children.Count)
                {
                    rightNode = node.Children[eqIdx + 1];
                }
            }

            if (leftNode.IsValid() && leftNode.Is(TreeSitterSyntax.Python.Identifier))
            {
                var name = leftNode.Text;
                var initializerText = rightNode.IsValid() ? rightNode.Text : "";

                var isConstant = name.All(c => !char.IsLower(c));
                var scope = DeterminePythonScope(node);

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
    }

    private static string DeterminePythonScope(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.Python.ClassDefinition))
                return "class";
            if (curr.Is(TreeSitterSyntax.Python.FunctionDefinition))
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static bool IsPythonDecoratorEntryPoint(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;
        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return false;
        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (func.IsValid() && func.Id == IntPtr.Zero && call.Children.Count > 0) func = call.Children[0];
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attr.IsValid())
            {
                var attrName = attr.Text;
                if (attrName is "route" or "get" or "post" or "put" or "delete" or "patch")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string? ExtractPythonDecoratorRoute(Node decoratorNode)
    {
        var call = decoratorNode.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return null;
        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (func.IsValid() && func.Id == IntPtr.Zero && call.Children.Count > 0) func = call.Children[0];
        if (!func.IsValid()) return null;

        var method = "GET";
        if (func.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attr.IsValid())
            {
                var attrName = attr.Text;
                if (attrName != "route")
                {
                    method = attrName.ToUpperInvariant();
                }
                else
                {
                    var argList = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                    if (argList.IsValid())
                    {
                        var keywordArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.KeywordArgument) && c.Text.StartsWith("methods"));
                        if (keywordArg.IsValid())
                        {
                            var listNode = keywordArg.FindChildOfType(TreeSitterSyntax.Python.List);
                            if (listNode.IsValid())
                            {
                                var firstStr = listNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.String));
                                if (firstStr.IsValid())
                                {
                                    method = firstStr.Text.Trim('\'', '"').ToUpperInvariant();
                                }
                            }
                        }
                    }
                }
            }
        }

        var args = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        var routeVal = "/";
        if (args.IsValid() && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.String));
            if (firstArg.IsValid())
            {
                routeVal = firstArg.Text.Trim('\'', '"');
            }
        }

        return $"{method}:{routeVal}";
    }

    private static bool IsDjangoPath(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (func.IsValid() && func.Id == IntPtr.Zero && node.Children.Count > 0) func = node.Children[0];
        if (!func.IsValid()) return false;

        return func.Is(TreeSitterSyntax.Python.Identifier) && (func.Text == "path" || func.Text == "re_path");
    }

    private static string? ExtractDjangoPathRoute(Node callNode)
    {
        var args = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (args.IsValid() && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.String));
            if (firstArg.IsValid())
            {
                var routeVal = firstArg.Text.Trim('\'', '"');
                return $"GET:{routeVal}";
            }
        }
        return "GET:/";
    }

    private static bool IsPythonHttpClientCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;
        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (func.IsValid() && func.Id == IntPtr.Zero && node.Children.Count > 0) func = node.Children[0];
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Python.Attribute))
        {
            var obj = func.GetChildForField(TreeSitterSyntax.Fields.Value) ?? func.GetChildForField(TreeSitterSyntax.Fields.Object) ?? (func.Children.Count > 0 ? func.Children[0] : null);
            var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (obj.IsValid() && attr.IsValid())
            {
                var objName = obj.Text;
                var attrName = attr.Text;

                if (objName is "requests" or "httpx" or "urllib.request" or "urllib")
                {
                    return attrName is "get" or "post" or "put" or "delete" or "request" or "patch" or "head" or "urlopen";
                }
                if (objName.Contains("session") || objName.Contains("client") || objName.Contains("http"))
                {
                    return attrName is "get" or "post" or "put" or "delete" or "request" or "patch";
                }
            }
        }
        return false;
    }

    private static string? ExtractPythonHttpClientTarget(Node node)
    {
        var args = node.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (args.IsValid())
        {
            foreach (var child in args.Children)
            {
                if (child.Is(TreeSitterSyntax.Python.KeywordArgument))
                {
                    var kwName = child.Children.FirstOrDefault()?.Text;
                    if (kwName == "url")
                    {
                        var kwVal = child.Children.Skip(2).FirstOrDefault() ?? child.Children.LastOrDefault();
                        var resolved = PythonAstHelper.ResolveStringOrVariable(kwVal);
                        if (!string.IsNullOrEmpty(resolved)) return resolved;
                    }
                }
                else if (child.IsAny(TreeSitterSyntax.Python.String,
                                    TreeSitterSyntax.Python.Identifier,
                                    TreeSitterSyntax.Python.VariableName,
                                    TreeSitterSyntax.Python.BinaryOperator,
                                    TreeSitterSyntax.Python.Subscript,
                                    TreeSitterSyntax.Python.Call))
                {
                    var resolved = PythonAstHelper.ResolveStringOrVariable(child);
                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                }
            }
        }
        return "http:unknown-service";
    }
}
