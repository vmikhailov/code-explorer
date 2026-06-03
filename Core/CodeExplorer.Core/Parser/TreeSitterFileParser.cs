using System;
using System.Collections.Generic;
using System.IO;
using TreeSitter;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public static class TreeSitterFileParser
{
    public static async Task<FileNode> ParseFileAsync(
        string filePath,
        string relativePath,
        string parentNodeId,
        IFileParser fileParser,
        ParsingContext ctx)
    {
        var sourceText = await File.ReadAllTextAsync(filePath);
        using var language = new Language(fileParser.LanguageName);
        using var parser = new TreeSitter.Parser(language);
        using var tree = parser.Parse(sourceText);

        var fileNodeId = $"file:{ctx.AbsoluteWorkspacePath}:{relativePath}";
        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);

        if (tree != null && tree.RootNode != null)
        {
            TraverseAndBuildTree(tree.RootNode, fileNode, fileNodeId, fileParser, ctx.AbsoluteWorkspacePath, relativePath);
        }

        return fileNode;
    }

    private static void TraverseAndBuildTree(
        Node node,
        IOntologyNode currentParent,
        string parentId,
        IFileParser parser,
        string workspacePath,
        string filePath)
    {
        var kind = parser.MapNodeType(node);
        string? name = null;
        if (kind != null)
        {
            name = parser.ExtractIdentifier(node);
        }

        var nextParent = currentParent;
        var currentParentId = parentId;

        if (kind != null && !string.IsNullOrEmpty(name))
        {
            var symbolId = $"symbol:{workspacePath}:{filePath}:{kind}:{name}:{node.StartPosition.Row}";
            IOntologyNode typedNode = kind switch
            {
                OntologyConstants.NodeLabels.Class => new ClassNode(symbolId, name, symbolId, Path.GetFileName(filePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Interface => new InterfaceNode(symbolId, name, symbolId, Path.GetFileName(filePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Function => new FunctionNode(symbolId, name, symbolId, Path.GetFileName(filePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Variable => new VariableNode(symbolId, name, symbolId, Path.GetFileName(filePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Query => new QueryNode(symbolId, name, CleanQueryText(node.Text), filePath),
                _ => throw new InvalidOperationException($"Unsupported symbol type: {kind}")
            };

            currentParent.Children.Add(typedNode);
            nextParent = typedNode;
            currentParentId = symbolId;
        }

        // Collect references inside the current symbol scope
        if (currentParentId.StartsWith("symbol:"))
        {
            if (node.Type is "identifier" or "type_identifier")
            {
                nextParent.References.Add(new Reference(currentParentId, node.Text, OntologyConstants.Relationships.PotentialType));
            }

            parser.CollectReferences(node, currentParentId, nextParent.References);
        }

        foreach (var child in node.Children)
        {
            TraverseAndBuildTree(child, nextParent, currentParentId, parser, workspacePath, filePath);
        }
    }

    private static string CleanQueryText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if ((text.StartsWith('"') && text.EndsWith('"')) || 
            (text.StartsWith('\'') && text.EndsWith('\'')) || 
            (text.StartsWith('`') && text.EndsWith('`')))
        {
            if (text.Length >= 2)
            {
                return text.Substring(1, text.Length - 2);
            }
        }
        return text;
    }
}
