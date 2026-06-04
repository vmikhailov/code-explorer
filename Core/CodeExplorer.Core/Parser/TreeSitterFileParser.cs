using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

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

        var fileNodeId = $"{ctx.WorkspaceId}:file:{relativePath}";
        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);

        if (tree != null)
        {
            TraverseAndBuildTree(tree.RootNode, fileNode, fileNodeId, fileParser, ctx, relativePath);
        }

        Console.WriteLine($"Finished parsing file: {relativePath} with {fileNode.Children.Count} top-level symbols.");
        return fileNode;
    }

    private static void TraverseAndBuildTree(
        Node node,
        IOntologyNode currentParent,
        string parentId,
        IFileParser parser,
        ParsingContext ctx,
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
            if (kind == OntologyConstants.NodeLabels.Variable)
            {
                // Skip variable nodes in the graph as it is too deep level
            }
            else
            {
                var symbolId = $"{ctx.WorkspaceId}:symbol:{filePath}:{kind}:{name}:{node.StartPosition.Row}";
                IOntologyNode typedNode = kind switch
                {
                    OntologyConstants.NodeLabels.Class => new ClassNode(symbolId, name, symbolId, Path.GetFileName(filePath), filePath, node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                    OntologyConstants.NodeLabels.Interface => new InterfaceNode(symbolId, name, symbolId, Path.GetFileName(filePath), filePath, node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                    OntologyConstants.NodeLabels.Function => new FunctionNode(symbolId, name, symbolId, Path.GetFileName(filePath), filePath, node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                    // OntologyConstants.NodeLabels.Variable => new VariableNode(symbolId, name, symbolId, Path.GetFileName(filePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                    OntologyConstants.NodeLabels.Query => NestedSqlParser.ParseNestedSql(node.Text, symbolId, filePath) ?? new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(node.Text), filePath),
                    OntologyConstants.NodeLabels.EntryPoint => CreateEntryPointNode(name, filePath, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, node),
                    OntologyConstants.NodeLabels.ExternalService => CreateExternalServiceNode(name, filePath, ctx.WorkspaceId, node),
                    _ => throw new InvalidOperationException($"Unsupported symbol type: {kind}")
                };

                currentParent.Children.Add(typedNode);
                nextParent = typedNode;
                currentParentId = typedNode.Id;
            }
        }

        // Collect references inside the current symbol scope
        if (currentParentId.Contains(":symbol:"))
        {
            if (node.Type is "identifier" or "type_identifier")
            {
                nextParent.References.Add(new Reference(currentParentId, node.Text, OntologyConstants.Relationships.PotentialType));
            }

            parser.CollectReferences(node, currentParentId, nextParent.References);
        }

        foreach (var child in node.Children)
        {
            TraverseAndBuildTree(child, nextParent, currentParentId, parser, ctx, filePath);
        }
    }

    private static EntryPointNode CreateEntryPointNode(string name, string filePath, string workspaceId, string workspacePath, Node node)
    {
        var fullPath = Path.GetFullPath(Path.Combine(workspacePath, filePath));
        var projectDir = FindProjectDirectory(fullPath, workspacePath);
        var projectName = Path.GetFileName(projectDir);
        if (string.IsNullOrEmpty(projectName)) projectName = "default";

        string protocol = "http";
        string route = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            route = name.Substring(3);
        }
        else if (name.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "event";
            route = name.Substring(6);
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            route = name.Substring(idx + 1);
        }

        var entryPointId = $"{workspaceId}:entrypoint:{projectName}:{protocol}:{name.Replace(":", "_")}";
        var ext = new Dictionary<string, string>
        {
            { "file_path", filePath },
            { "start_line", node.StartPosition.Row.ToString() }
        };
        return new EntryPointNode(entryPointId, name.Replace(":", " "), protocol, route, filePath, ext);
    }

    private static ExternalServiceNode CreateExternalServiceNode(string name, string filePath, string workspaceId, Node node)
    {
        string protocol = "http";
        string domainOrService = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            domainOrService = name.Substring(3);
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            protocol = name.Substring(0, idx);
            domainOrService = name.Substring(idx + 1);
        }

        var extServiceId = $"{workspaceId}:externalservice:{protocol}:{domainOrService}";
        var ext = new Dictionary<string, string>
        {
            { "file_path", filePath },
            { "start_line", node.StartPosition.Row.ToString() }
        };
        return new ExternalServiceNode(extServiceId, domainOrService, protocol, domainOrService, filePath, ext);
    }

    private static string FindProjectDirectory(string filePath, string workspacePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir != null && dir.Replace('\\', '/').StartsWith(workspacePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0 ||
                File.Exists(Path.Combine(dir, "package.json")) ||
                File.Exists(Path.Combine(dir, "go.mod")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return Path.GetDirectoryName(filePath) ?? "";
    }
}
