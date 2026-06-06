using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python;

public class PythonFileVisitor : BaseParserVisitor
{
    private readonly PythonParser _parser;

    public PythonFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        PythonParser parser)
        : base(rootNode, activeLibraryParsers)
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

        if (node.Type == "string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        return node.Type switch
        {
            "class_definition" => OntologyConstants.NodeLabels.Class,
            "function_definition" => OntologyConstants.NodeLabels.Function,
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

        if (node.Type == "string")
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
            if (parent != null && parent.Type == "decorated_definition")
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
                var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
                if (args != null && args.Children.Count > 1)
                {
                    var viewArg = args.Children.Skip(1).FirstOrDefault(c => c.Type is "identifier" or "attribute");
                    if (viewArg != null)
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
        var nameNode = node.GetChildForField("name");
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            return nameNode.Text;
        }

        foreach (var child in node.Children)
        {
            if (child.Type is "identifier" or "variable_name")
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
        if (node.Type == "import_statement")
        {
            foreach (var child in node.Children)
            {
                if (child.Type is "dotted_name" or "aliased_name")
                {
                    var importPath = child.Text;
                    RawImports.Add(new RawImport(importPath, "", ImportType.External));
                }
            }
        }
        else if (node.Type == "import_from_statement")
        {
            var moduleNode = node.GetChildForField("module_name");
            if (moduleNode == null || moduleNode.Id == IntPtr.Zero)
            {
                moduleNode = node.Children.FirstOrDefault(c => c.Type == "dotted_name");
            }
            if (moduleNode != null && moduleNode.Id != IntPtr.Zero)
            {
                var importPath = moduleNode.Text;
                RawImports.Add(new RawImport(importPath, "", ImportType.External));
            }
        }
        VisitChildren(node, depth);
    }

    protected override string? FindCallName(Node callNode)
    {
        var expr = callNode.GetChildForField("function");
        if (expr != null && expr.Id == IntPtr.Zero && callNode.Children.Count > 0)
        {
            expr = callNode.Children[0];
        }
        if (expr == null || expr.Id == IntPtr.Zero) return null;

        if (expr.Type == "identifier")
        {
            return expr.Text;
        }
        if (expr.Type == "attribute")
        {
            var attrChild = expr.GetChildForField("attribute");
            if (attrChild != null && attrChild.Id != IntPtr.Zero) return attrChild.Text;
        }
        return null;
    }

    private void CollectVariable(Node node)
    {
        if (node.Type == "assignment")
        {
            var leftNode = node.GetChildForField("left");
            if (leftNode == null || leftNode.Id == IntPtr.Zero)
            {
                leftNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
            }

            var rightNode = node.GetChildForField("right");
            if (rightNode == null || rightNode.Id == IntPtr.Zero)
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

            if (leftNode != null && leftNode.Id != IntPtr.Zero && leftNode.Type == "identifier")
            {
                var name = leftNode.Text;
                var initializerText = rightNode != null && rightNode.Id != IntPtr.Zero ? rightNode.Text : "";

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
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type == "class_definition")
                return "class";
            if (curr.Type == "function_definition")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static bool IsPythonDecoratorEntryPoint(Node node)
    {
        if (node.Type != "decorator") return false;
        var call = node.Children.FirstOrDefault(c => c.Type == "call");
        if (call == null) return false;
        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "attribute")
        {
            var attr = func.GetChildForField("attribute");
            if (attr != null && attr.Id != IntPtr.Zero)
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
        var call = decoratorNode.Children.FirstOrDefault(c => c.Type == "call");
        if (call == null) return null;
        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return null;

        var method = "GET";
        if (func.Type == "attribute")
        {
            var attr = func.GetChildForField("attribute");
            if (attr != null && attr.Id != IntPtr.Zero)
            {
                var attrName = attr.Text;
                if (attrName != "route")
                {
                    method = attrName.ToUpperInvariant();
                }
                else
                {
                    var argList = call.Children.FirstOrDefault(c => c.Type == "argument_list");
                    if (argList != null)
                    {
                        var keywordArg = argList.Children.FirstOrDefault(c => c.Type == "keyword_argument" && c.Text.StartsWith("methods"));
                        if (keywordArg != null)
                        {
                            var listNode = keywordArg.Children.FirstOrDefault(c => c.Type == "list");
                            if (listNode != null)
                            {
                                var firstStr = listNode.Children.FirstOrDefault(c => c.Type == "string");
                                if (firstStr != null)
                                {
                                    method = firstStr.Text.Trim('\'', '"').ToUpperInvariant();
                                }
                            }
                        }
                    }
                }
            }
        }

        var args = call.Children.FirstOrDefault(c => c.Type == "argument_list");
        var routeVal = "/";
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type == "string");
            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('\'', '"');
            }
        }

        return $"{method}:{routeVal}";
    }

    private static bool IsDjangoPath(Node node)
    {
        if (node.Type != "call") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        return func.Type == "identifier" && (func.Text == "path" || func.Text == "re_path");
    }

    private static string? ExtractDjangoPathRoute(Node callNode)
    {
        var args = callNode.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type == "string");
            if (firstArg != null)
            {
                var routeVal = firstArg.Text.Trim('\'', '"');
                return $"GET:{routeVal}";
            }
        }
        return "GET:/";
    }

    private static bool IsPythonHttpClientCall(Node node)
    {
        if (node.Type != "call") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "attribute")
        {
            var obj = func.GetChildForField("value") ?? func.GetChildForField("object") ?? (func.Children.Count > 0 ? func.Children[0] : null);
            var attr = func.GetChildForField("attribute");
            if (obj != null && attr != null && attr.Id != IntPtr.Zero)
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
        var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type == "string");
            if (firstArg != null)
            {
                var text = firstArg.Text.Trim('\'', '"');
                if (text.Contains("://"))
                {
                    try
                    {
                        var uri = new Uri(text);
                        return $"http:{uri.Host}";
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
