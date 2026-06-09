using System;
using System.Collections.Generic;
using System.Linq;
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

        if (node.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        if (node.Type == "type_spec")
        {
            var isInterface = false;
            foreach (var child in node.Children)
            {
                if (child.Type == "interface_type")
                {
                    isInterface = true;
                    break;
                }
            }
            return isInterface ? "Interface" : "Class";
        }

        return node.Type switch
        {
            "function_declaration" or "method_declaration" => OntologyConstants.NodeLabels.Function,
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

        if (node.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal")
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
                var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
                if (args != null && args.Children.Count > 1)
                {
                    var handlerArg = args.Children.Skip(1).FirstOrDefault(c => c.Type is "identifier" or "selector_expression");
                    if (handlerArg != null)
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
        var pathNode = node.GetChildForField("path") ?? node.Children.FirstOrDefault(c => c.Type == "string_literal");
        if (pathNode != null && pathNode.Id != IntPtr.Zero)
        {
            var importPath = pathNode.Text.Trim('"');
            RawImports.Add(new RawImport(importPath, "", ImportType.External));
            ResolveAndInjectLibraryParser(importPath);
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
        if (expr.Type == "selector_expression")
        {
            var fieldChild = expr.GetChildForField("field");
            if (fieldChild != null && fieldChild.Id != IntPtr.Zero) return fieldChild.Text;
        }
        return null;
    }

    private void CollectVariable(Node node)
    {
        if (node.Type is "var_spec" or "const_spec")
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
                else if (!passedTypeOrEq && child.Type == "identifier")
                {
                    identifiers.Add(child);
                }
                else if (passedTypeOrEq && child.Type != "=")
                {
                    values.Add(child);
                }
            }

            var isConstant = node.Type == "const_spec";
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
        else if (node.Type == "short_var_declaration")
        {
            var leftNode = node.Children.FirstOrDefault();
            var rightNode = node.Children.LastOrDefault();

            if (leftNode != null && rightNode != null && leftNode.Id != rightNode.Id)
            {
                var names = new List<string>();
                if (leftNode.Type == "expression_list")
                {
                    names.AddRange(leftNode.Children.Where(c => c.Type == "identifier").Select(c => c.Text));
                }
                else if (leftNode.Type == "identifier")
                {
                    names.Add(leftNode.Text);
                }

                var values = new List<string>();
                if (rightNode.Type == "expression_list")
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
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "type_spec" or "struct_type" or "interface_type")
                return "class";
            if (curr.Type is "function_declaration" or "method_declaration" or "block")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static bool IsGoEntryPoint(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "selector_expression")
        {
            var field = func.GetChildForField("field");
            if (field != null && field.Id != IntPtr.Zero)
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
        var func = callNode.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && callNode.Children.Count > 0)) func = callNode.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return null;

        var method = "GET";
        if (func.Type == "selector_expression")
        {
            var field = func.GetChildForField("field");
            if (field != null && field.Id != IntPtr.Zero)
            {
                var methodName = field.Text;
                if (methodName != "HandleFunc" && methodName != "Handle" && methodName != "Any")
                {
                    method = methodName.ToUpperInvariant();
                }
            }
        }

        var args = callNode.Children.FirstOrDefault(c => c.Type == "argument_list");
        var routeVal = "/";
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal");
            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('"', '`');
            }
        }

        return $"{method}:{routeVal}";
    }

    private static bool IsGoHttpClientCall(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "selector_expression")
        {
            var operand = func.GetChildForField("operand");
            var field = func.GetChildForField("field");
            if (operand != null && field != null && field.Id != IntPtr.Zero)
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
        var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (args != null && args.Children.Count > 1)
        {
            var firstStrArg = args.Children.FirstOrDefault(c => c.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal");
            if (firstStrArg != null)
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
