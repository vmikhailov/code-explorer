using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptFileVisitor : BaseParserVisitor
{
    private readonly TypeScriptParser _parser;

    public TypeScriptFileVisitor(Node rootNode, List<ILibraryParser> activeLibraryParsers, TypeScriptParser parser) :
        base(rootNode, activeLibraryParsers)
    {
        _parser = parser;
    }

    protected override string? MapNodeType(Node node)
    {
        if (IsTsDecoratorEntryPoint(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsExpressRoute(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsTsHttpClientCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        if (node.Type is "string" or "template_string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return OntologyConstants.NodeLabels.Query;
            }
        }

        return node.Type switch
        {
            "class_declaration" or "class_expression" or "enum_declaration" => OntologyConstants.NodeLabels.Class,
            "interface_declaration" or "type_alias_declaration" => OntologyConstants.NodeLabels.Interface,
            "method_definition" or "function_declaration" or "function_expression" or "arrow_function" =>
                OntologyConstants.NodeLabels.Function,
            _ => null
        };
    }

    protected override string? ExtractIdentifier(Node node)
    {
        if (IsTsDecoratorEntryPoint(node))
        {
            return ExtractTsDecoratorRoute(node);
        }

        if (IsExpressRoute(node))
        {
            return ExtractExpressRoute(node);
        }

        if (IsTsHttpClientCall(node))
        {
            return ExtractTsHttpClientTarget(node);
        }

        if (node.Type is "string" or "template_string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        return ExtractTsIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
        // If this is a decorator (EntryPoint) on a method, link it to the next method sibling via TRIGGERS
        if (node.Type == "decorator" && symbolNode.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var nextNode = GetNextNamedSibling(node);

            if (nextNode != null && (nextNode.Type == "method_definition" || nextNode.Type == "class_declaration"))
            {
                var targetName = ExtractTsIdentifier(nextNode);

                if (!string.IsNullOrEmpty(targetName))
                {
                    symbolNode.References.Add(new Reference("", targetName, "TRIGGERS"));
                }
            }
        }
    }

    private static bool IsTsDecoratorEntryPoint(Node node)
    {
        if (node.Type != "decorator") return false;

        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return false;

        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];

        if (func.Id == IntPtr.Zero) return false;

        var name = func.Text;
        return name is "Controller" or "Get" or "Post" or "Put" or "Delete" or "Patch" or "SubscribeMessage";
    }

    private static string? ExtractTsDecoratorRoute(Node node)
    {
        var call = node.Children.FirstOrDefault(c => c.Type == "call_expression");
        if (call == null) return null;

        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];

        if (func.Id == IntPtr.Zero) return null;

        var name = func.Text;

        var args = call.Children.FirstOrDefault(c => c.Type == "arguments");
        var routeVal = "/";

        if (args != null && args.Children.Count > 2)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");

            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('\'', '"', '`');
            }
        }

        if (name == "SubscribeMessage")
        {
            return $"ws:{routeVal}";
        }

        var method = name == "Controller" ? "GET" : name.ToUpperInvariant();
        return $"{method}:{routeVal}";
    }

    private static bool IsExpressRoute(Node node)
    {
        if (node.Type != "call_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];

        if (func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");

            if (obj != null &&
                (obj.Text.Contains("app") || obj.Text.Contains("router") || obj.Text.Contains("express")))
            {
                var prop = func.GetChildForField("property");

                if (prop != null && prop.Id != IntPtr.Zero)
                {
                    var method = prop.Text;
                    return method is "get" or "post" or "put" or "delete";
                }
            }
        }

        return false;
    }

    private static string? ExtractExpressRoute(Node node)
    {
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];

        if (func.Id == IntPtr.Zero) return null;

        var prop = func.GetChildForField("property");
        if (prop == null) return null;

        var method = prop.Text.ToUpperInvariant();

        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        var routeVal = "/";

        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");

            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('\'', '"', '`');
            }
        }

        return $"{method}:{routeVal}";
    }

    private static bool IsTsHttpClientCall(Node node)
    {
        if (node.Type != "call_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        
        if (func.Id == IntPtr.Zero) return false;

        if (func.Type == "identifier" && func.Text == "fetch") return true;

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");

            if (obj != null && obj.Text == "axios")
            {
                var prop = func.GetChildForField("property");

                if (prop != null)
                {
                    var method = prop.Text;
                    return method is "get" or "post" or "put" or "delete" or "request";
                }
            }
        }

        return false;
    }

    private static string? ExtractTsHttpClientTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");

        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");

            if (firstArg != null)
            {
                var text = firstArg.Text.Trim('\'', '"', '`');

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

    private static Node? GetNextNamedSibling(Node node)
    {
        var parent = node.Parent;
        if (parent == null || parent.Id == IntPtr.Zero) return null;

        var children = parent.Children;
        var idx = -1;

        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Id == node.Id)
            {
                idx = i;
                break;
            }
        }

        if (idx >= 0)
        {
            for (var i = idx + 1; i < children.Count; i++)
            {
                var sibling = children[i];

                if (sibling.Type == "method_definition")
                {
                    return sibling;
                }

                if (sibling.Type == "class_declaration")
                {
                    return sibling;
                }
            }
        }

        return null;
    }

    private string? ExtractTsIdentifier(Node node)
    {
        if (node.Type is "arrow_function" or "function_expression")
        {
            var parent = node.Parent;

            if (parent != null && parent.Id != IntPtr.Zero)
            {
                if (parent.Type == "variable_declarator")
                {
                    var parentNameNode = parent.GetChildForField("name");

                    if (parentNameNode != null && parentNameNode.Id != IntPtr.Zero)
                    {
                        return parentNameNode.Text;
                    }

                    var firstIdent = parent.Children.FirstOrDefault(c => c.Type == "identifier");

                    if (firstIdent != null && firstIdent.Id != IntPtr.Zero)
                    {
                        return firstIdent.Text;
                    }
                }
                else if (parent.Type == "assignment_expression")
                {
                    var leftNode = parent.GetChildForField("left");

                    if (leftNode != null && leftNode.Id != IntPtr.Zero)
                    {
                        return leftNode.Text;
                    }
                }
            }
        }

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
        CollectVariableAndTypeBindings(node);
        VisitChildren(node, depth);
    }

    protected override void VisitParameter(Node node, int depth)
    {
        var identNode = node.Children.FirstOrDefault(c => c.Type == "identifier");

        if (identNode == null && node.Type == "parameter_property")
        {
            var nestedParam = node.Children.FirstOrDefault(c => c.Type is "required_parameter" or "optional_parameter");

            if (nestedParam != null)
            {
                identNode = nestedParam.Children.FirstOrDefault(c => c.Type == "identifier");
            }
        }

        var typeAnnotation = node.Children.FirstOrDefault(c => c.Type == "type_annotation") ?? node.Children
            .FirstOrDefault(c => c.Type is "required_parameter" or "optional_parameter")?.Children
            .FirstOrDefault(c => c.Type == "type_annotation");

        if (identNode != null && typeAnnotation != null)
        {
            var typeNode = typeAnnotation.Children.FirstOrDefault(c => c.Type != ":");

            if (typeNode != null)
            {
                var varName = identNode.Text;
                var typeName = typeNode.Text;
                var scopeName = GetContainingScopeName(node);
                RawTypeBindings.Add(new RawTypeBinding(varName, typeName, "", scopeName));
                RawTypeBindings.Add(new RawTypeBinding("this." + varName, typeName, "", scopeName));
            }
        }

        VisitChildren(node, depth);
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        CollectImport(node);
        VisitChildren(node, depth);
    }

    protected override void VisitCallExpression(Node node, int depth)
    {
        var funcNode = node.GetChildForField("function");

        if (funcNode != null && funcNode.Text == "require")
        {
            var argList = node.GetChildForField("arguments");

            if (argList != null && argList.Children.Count > 1)
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.Type == "string");

                if (firstArg != null)
                {
                    var importPath = firstArg.Text.Trim('\'', '"');
                    RawImports.Add(new RawImport(importPath, "", ImportType.External));
                }
            }
        }

        base.VisitCallExpression(node, depth);
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

        if (expr.Type == "member_expression")
        {
            var propChild = expr.GetChildForField("property");
            if (propChild != null && propChild.Id != IntPtr.Zero) return propChild.Text;
        }

        return null;
    }

    protected override void VisitInheritanceClause(Node node, int depth)
    {
        var scopeSymbol = SymbolStack.Peek();

        if (scopeSymbol.Kind != "file")
        {
            var kind = node.Type == "implements_clause" ? "IMPLEMENTS" : "INHERITS_FROM";

            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    SymbolStack.Peek().References.Add(new Reference("", child.Text, kind));
                }
            }
        }

        VisitChildren(node, depth);
    }

    private void CollectImport(Node node)
    {
        var sourceNode = node.GetChildForField("source");

        if (sourceNode == null || sourceNode.Id == IntPtr.Zero)
        {
            sourceNode = node.Children.FirstOrDefault(c => c.Type == "string");
        }

        if (sourceNode != null && sourceNode.Id != IntPtr.Zero)
        {
            var importPath = sourceNode.Text.Trim('\'', '"');
            RawImports.Add(new RawImport(importPath, "", ImportType.External));
        }
    }

    private void CollectVariableAndTypeBindings(Node node)
    {
        if (node.Type == "variable_declarator")
        {
            var nameNode = node.GetChildForField("name") ?? node.Children.FirstOrDefault(c => c.Type == "identifier");
            var name = nameNode?.Text;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetChildForField("value");
                var initializerText = valueNode != null && valueNode.Id != IntPtr.Zero ? valueNode.Text : "";
                var isConstant = IsTypeScriptConstant(node);
                var scope = DetermineTypeScriptScope(node);

                RawVariables.Add(new RawVariable(name, initializerText, scope, isConstant, "", node.StartPosition.Row,
                    node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column));

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Type == "type_annotation") ??
                                     nameNode?.Children.FirstOrDefault(c => c.Type == "type_annotation");
                string? typeName = null;

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => c.Type != ":");
                    if (typeNode != null) typeName = typeNode.Text;
                }
                else if (valueNode != null && valueNode.Type == "new_expression")
                {
                    var constructorNode = valueNode.GetChildForField("constructor");

                    if (constructorNode != null && constructorNode.Id != IntPtr.Zero)
                    {
                        typeName = constructorNode.Text;
                    }
                }

                if (!string.IsNullOrEmpty(typeName))
                {
                    var scopeName = GetContainingScopeName(node);
                    RawTypeBindings.Add(new RawTypeBinding(name, typeName, "", scopeName));
                }
            }
        }
        else if (node.Type is "public_field_definition" or "property_definition")
        {
            var nameNode = node.GetChildForField("name") ??
                           node.Children.FirstOrDefault(c => c.Type == "property_identifier");
            var name = nameNode?.Text;

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetChildForField("value");
                var initializerText = valueNode != null && valueNode.Id != IntPtr.Zero ? valueNode.Text : "";
                var isConstant = false;
                var scope = "class";

                RawVariables.Add(new RawVariable(name, initializerText, scope, isConstant, "", node.StartPosition.Row,
                    node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column));

                var typeAnnotation = node.Children.FirstOrDefault(c => c.Type == "type_annotation");

                if (typeAnnotation != null)
                {
                    var typeNode = typeAnnotation.Children.FirstOrDefault(c => c.Type != ":");

                    if (typeNode != null)
                    {
                        var scopeName = GetContainingScopeName(node);
                        RawTypeBindings.Add(new RawTypeBinding(name, typeNode.Text, "", scopeName));
                        RawTypeBindings.Add(new RawTypeBinding("this." + name, typeNode.Text, "", scopeName));
                    }
                }
            }
        }
    }

    private static bool IsTypeScriptConstant(Node node)
    {
        var curr = node.Parent;

        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type == "lexical_declaration")
            {
                return curr.Text.StartsWith("const");
            }

            curr = curr.Parent;
        }

        return false;
    }

    private static string DetermineTypeScriptScope(Node node)
    {
        var curr = node.Parent;

        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "interface_declaration")
                return "class";

            if (curr.Type is "function_declaration" or "arrow_function" or "method_definition" or "statement_block")
                return "local";

            curr = curr.Parent;
        }

        return "global";
    }

    private static string GetContainingScopeName(Node node)
    {
        var curr = node.Parent;

        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "interface_declaration")
            {
                var nameNode = curr.GetChildForField("name");
                if (nameNode != null && nameNode.Id != IntPtr.Zero) return nameNode.Text;
            }
            else if (curr.Type is "function_declaration" or "method_definition")
            {
                var nameNode = curr.GetChildForField("name");

                if (nameNode != null && nameNode.Id != IntPtr.Zero)
                {
                    var nameText = nameNode.Text;

                    if (nameText == "constructor")
                    {
                        var classNode = curr.Parent;

                        while (classNode != null && classNode.Id != IntPtr.Zero)
                        {
                            if (classNode.Type is "class_declaration" or "interface_declaration")
                            {
                                var classNameNode = classNode.GetChildForField("name");
                                if (classNameNode != null && classNameNode.Id != IntPtr.Zero) return classNameNode.Text;
                            }

                            classNode = classNode.Parent;
                        }
                    }

                    return nameText;
                }
            }

            curr = curr.Parent;
        }

        return "global";
    }
}
