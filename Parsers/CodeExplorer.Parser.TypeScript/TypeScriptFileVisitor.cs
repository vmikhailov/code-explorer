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

    public TypeScriptFileVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        TypeScriptParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry) :
        base(rootNode, activeLibraryParsers, relativePath, absoluteWorkspacePath, fileParser, libraryRegistry)
    {
        _parser = parser;

        // Register a sequence detector rule for CommonJS 'require' statements
        SequenceDetector.Register([
            n => n.Type == "call_expression" && n.GetChildForField("function")?.Text == "require"
        ], path =>
        {
            var callNode = path[^1];
            var argList = callNode.GetChildForField("arguments");

            if (argList != null && argList.Children.Count > 1)
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.Type == "string");

                if (firstArg != null)
                {
                    var importPath = firstArg.Text.Trim('\'', '"');
                    RawImports.Add(new RawImport(importPath, ""));
                    ResolveAndInjectLibraryParser(importPath);
                }
            }
        });
    }

    protected override string? MapNodeType(Node node)
    {
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

            if (parent.IsValid())
            {
                if (parent!.Type == "variable_declarator")
                {
                    var parentName = parent.GetChildFieldText("name");
                    if (parentName != null) return parentName;

                    var firstIdent = parent.Children.FirstOrDefault(c => c.Type == "identifier");

                    if (firstIdent.IsValid())
                    {
                        return firstIdent!.Text;
                    }
                }
                else if (parent.Type == "assignment_expression")
                {
                    var leftText = parent.GetChildFieldText("left");
                    if (leftText != null) return leftText;
                }
            }
        }

        var nameText = node.GetChildFieldText("name");
        if (nameText != null) return nameText;

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

    protected override string? FindCallName(Node callNode)
    {
        var expr = callNode.GetFunctionNode();

        if (!expr.IsValid()) return null;

        if (expr!.Type == "identifier")
        {
            return expr.Text;
        }

        if (expr.Type == "member_expression")
        {
            var propChild = expr.GetChildForField("property");
            if (propChild.IsValid()) return propChild!.Text;
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
            ResolveAndInjectLibraryParser(importPath);
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

        while (curr.IsValid())
        {
            if (curr!.Type is "class_declaration" or "interface_declaration")
            {
                var nameText = curr.GetChildFieldText("name");
                if (nameText != null) return nameText;
            }
            else if (curr.Type is "function_declaration" or "method_definition")
            {
                var nameText = curr.GetChildFieldText("name");

                if (nameText != null)
                {
                    var nameTextStr = nameText;

                    if (nameTextStr == "constructor")
                    {
                        var classNode = curr.Parent;

                        while (classNode.IsValid())
                        {
                            if (classNode!.Type is "class_declaration" or "interface_declaration")
                            {
                                var classNameText = classNode.GetChildFieldText("name");
                                if (classNameText != null) return classNameText;
                            }

                            classNode = classNode.Parent;
                        }
                    }

                    return nameTextStr;
                }
            }

            curr = curr.Parent;
        }

        return "global";
    }
}
