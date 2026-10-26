using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Extracts Rust top-level declarations (structs, enums, traits, functions, constants, macros) in Pass 1.
/// </summary>
public static class RustDeclarationExtractor
{
    public static void Extract(Node node, Action<string, Node?, string?> registerDeclaration)
    {
        ExtractInternal(node, null, registerDeclaration);
    }

    private static void ExtractInternal(Node node, string? currentScope, Action<string, Node?, string?> register)
    {
        var type = node.Type;

        if (type is TreeSitterSyntax.Rust.ConstItem or TreeSitterSyntax.Rust.StaticItem)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);

            var valNode = node.GetChildForField(TreeSitterSyntax.Fields.Value);
            if (!valNode.IsValid())
            {
                var equalsIdx = -1;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i].Text == "=")
                    {
                        equalsIdx = i;
                        break;
                    }
                }
                if (equalsIdx >= 0 && equalsIdx + 1 < node.Children.Count)
                {
                    valNode = node.Children[equalsIdx + 1];
                }
            }

            if (nameNode.IsValid())
            {
                var name = nameNode.Text;
                if (!string.IsNullOrEmpty(currentScope))
                {
                    register($"{currentScope}::{name}", valNode.IsValid() ? valNode : null, null);
                }
                register(name, valNode.IsValid() ? valNode : null, null);
            }
            return;
        }

        if (type == TreeSitterSyntax.Rust.EnumItem)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.TypeIdentifier);
            var enumName = nameNode.IsValid() ? nameNode.Text : "Enum";

            register(enumName, node, null);
            if (!string.IsNullOrEmpty(currentScope))
            {
                register($"{currentScope}::{enumName}", node, null);
            }

            var variantList = node.FindChildOfType(TreeSitterSyntax.Rust.EnumVariantList);
            if (variantList.IsValid())
            {
                foreach (var child in variantList.Children)
                {
                    if (child.Type == TreeSitterSyntax.Rust.EnumVariant)
                    {
                        var varNameNode = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                          child.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
                        if (varNameNode.IsValid())
                        {
                            var varValNode = child.GetChildForField(TreeSitterSyntax.Fields.Value);
                            var key = $"{enumName}::{varNameNode.Text}";
                            register(key, varValNode.IsValid() ? varValNode : null, varValNode.IsValid() ? null : varNameNode.Text);
                        }
                    }
                }
            }
            return;
        }

        if (type is TreeSitterSyntax.Rust.StructItem or TreeSitterSyntax.Rust.UnionItem)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.TypeIdentifier);
            if (nameNode.IsValid())
            {
                var name = nameNode.Text;
                register(name, node, null);
                if (!string.IsNullOrEmpty(currentScope))
                {
                    register($"{currentScope}::{name}", node, null);
                }

                var isPubStruct = node.Children.Any(c => c.Type == "visibility_modifier");
                var fieldList = node.FindChildOfType(TreeSitterSyntax.Rust.FieldDeclarationList);
                if (fieldList.IsValid())
                {
                    Node? lastAttr = null;
                    foreach (var child in fieldList.Children)
                    {
                        if (child.Type == TreeSitterSyntax.Rust.AttributeItem)
                        {
                            lastAttr = child;
                        }
                        else if (child.Type == TreeSitterSyntax.Rust.FieldDeclaration)
                        {
                            var attr = child.FindChildOfType(TreeSitterSyntax.Rust.AttributeItem);
                            var effectiveAttr = attr.IsValid() ? attr : lastAttr;
                            if (effectiveAttr.IsValid() && Regex.IsMatch(effectiveAttr.Text, @"default\s*=\s*""([A-Za-z0-9_]+)"""))
                            {
                                var fieldNameNode = child.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                                    child.FindChildOfType(TreeSitterSyntax.Rust.FieldIdentifier);
                                if (fieldNameNode.IsValid())
                                {
                                    var fieldName = fieldNameNode.Text;
                                    if (isPubStruct)
                                    {
                                        register(fieldName, effectiveAttr, null);
                                        register($"self.{fieldName}", effectiveAttr, null);
                                    }
                                    register($"{name}::{fieldName}", effectiveAttr, null);
                                }
                            }
                            lastAttr = null;
                        }
                    }
                }
            }
            return;
        }

        if (type == TreeSitterSyntax.Rust.TraitItem)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.TypeIdentifier);
            if (nameNode.IsValid())
            {
                var name = nameNode.Text;
                register(name, node, null);
                if (!string.IsNullOrEmpty(currentScope))
                {
                    register($"{currentScope}::{name}", node, null);
                }
            }
            return;
        }

        if (type is TreeSitterSyntax.Rust.FunctionItem or TreeSitterSyntax.Rust.FunctionSignatureItem)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
            if (nameNode.IsValid())
            {
                var name = nameNode.Text;
                register(name, node, null);
                if (!string.IsNullOrEmpty(currentScope))
                {
                    register($"{currentScope}::{name}", node, null);
                    register($"self.{name}", node, null);
                }
            }

            foreach (var child in node.Children)
            {
                ExtractInternal(child, currentScope, register);
            }
            return;
        }

        if (type == TreeSitterSyntax.Rust.MacroDefinition)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
            if (nameNode.IsValid())
            {
                register(nameNode.Text, node, null);
            }
            return;
        }

        if (type == TreeSitterSyntax.Rust.ModItem)
        {
            var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
            var modName = nameNode.IsValid() ? nameNode.Text : null;
            var nextScope = !string.IsNullOrEmpty(modName)
                ? (!string.IsNullOrEmpty(currentScope) ? $"{currentScope}::{modName}" : modName)
                : currentScope;

            foreach (var child in node.Children)
            {
                ExtractInternal(child, nextScope, register);
            }
            return;
        }

        if (type == TreeSitterSyntax.Rust.ImplItem)
        {
            var typeNode = node.GetChildForField(TreeSitterSyntax.Fields.Type) ??
                           node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.TypeIdentifier);
            var implTarget = typeNode.IsValid() ? typeNode.Text : currentScope;

            foreach (var child in node.Children)
            {
                ExtractInternal(child, implTarget, register);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractInternal(child, currentScope, register);
        }
    }
}
