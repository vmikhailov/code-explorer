using CodeExplorer.Common;
using CodeExplorer.Database;
using TreeSitter;

namespace CodeExplorer.Parser;

public class FileLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _filePath;
    private readonly string _parentFolderOrProjectId;
    private readonly IFileParser _fileParser;

    public FileLevelParser(ParsingContext ctx, string filePath, string parentFolderOrProjectId, IFileParser fileParser)
    {
        _ctx = ctx;
        _filePath = filePath.Replace('\\', '/');
        _parentFolderOrProjectId = parentFolderOrProjectId;
        _fileParser = fileParser;
    }

    public async Task ParseAsync()
    {
        var relativePath = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, _filePath).Replace('\\', '/');
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Parsing file: '{relativePath}' ({_fileParser.LanguageName})");

        try
        {
            if (!_fileParser.UsesTreeSitter)
            {
                await _fileParser.ParseCustomAsync(_filePath, _parentFolderOrProjectId, _ctx);
                return;
            }

            var sourceText = await File.ReadAllTextAsync(_filePath);

            using var language = new Language(_fileParser.LanguageName);
            using var parser = new TreeSitter.Parser(language);
            using var tree = parser.Parse(sourceText);

            if (tree == null || tree.RootNode == null) return;

            var fileCtx = new FileContext(_ctx.AbsoluteWorkspacePath, relativePath, sourceText, _fileParser);

            // 1. Create and register the File Node
            var fileNodeId = $"file:{_ctx.AbsoluteWorkspacePath}:{relativePath}";
            var fileNode = new FileNode(fileNodeId, Path.GetFileName(_filePath), relativePath, _filePath);
            fileCtx.Nodes.Add(Database.Node.FromNode(fileNode));

            // 2. Link File Node to Parent (WorkspaceFolder or ProjectFolder or Project)
            fileCtx.Relationships.Add(Relationship.FromRelationship(new ContainsRelationship(_parentFolderOrProjectId, fileNodeId)));

            // 3. Traverse the AST
            TraverseNode(tree.RootNode, fileNodeId, fileCtx);

            // 4. Flush Mapped File Data to Persistence Channel & Update Global Stats
            if (fileCtx.Nodes.Count > 0)
            {
                await _ctx.EnqueueUploadNodesAsync(fileCtx.Nodes);
                foreach (var node in fileCtx.Nodes)
                {
                    _ctx.IncrementNodeKind(node.Kind);

                    // Map global symbols for reference resolution
                    if (node.Kind == OntologyConstants.NodeLabels.Class || node.Kind == OntologyConstants.NodeLabels.Interface || node.Kind == OntologyConstants.NodeLabels.Function)
                    {
                        if (node.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
                        {
                            _ctx.AddGlobalSymbol(node.Kind, nameStr, node.Id);
                        }
                    }
                }
                _ctx.AddNodesCount(fileCtx.Nodes.Count);
            }

            if (fileCtx.Relationships.Count > 0)
            {
                await _ctx.EnqueueUploadRelationshipsAsync(fileCtx.Relationships);
                _ctx.AddRelsCount(fileCtx.Relationships.Count);
            }

            if (fileCtx.References.Count > 0)
            {
                _ctx.AddGlobalReferences(fileCtx.References);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error parsing file {_filePath}: {ex.Message}");
        }
    }

    private static void TraverseNode(TreeSitter.Node node, string parentId, FileContext ctx)
    {
        string? kind = ctx.Parser.MapNodeType(node);
        string? name = null;

        if (kind != null)
        {
            name = ctx.Parser.ExtractIdentifier(node);
        }

        string currentParentId = parentId;

        if (kind != null && !string.IsNullOrEmpty(name))
        {
            var symbolId = $"symbol:{ctx.WorkspacePath}:{ctx.FilePath}:{kind}:{name}:{node.StartPosition.Row}";
            IOntologyNode typedNode = kind switch
            {
                OntologyConstants.NodeLabels.Class => new ClassNode(symbolId, name, symbolId, Path.GetFileName(ctx.FilePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Interface => new InterfaceNode(symbolId, name, symbolId, Path.GetFileName(ctx.FilePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Function => new FunctionNode(symbolId, name, symbolId, Path.GetFileName(ctx.FilePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                OntologyConstants.NodeLabels.Variable => new VariableNode(symbolId, name, symbolId, Path.GetFileName(ctx.FilePath), node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
                _ => throw new InvalidOperationException($"Unsupported symbol type: {kind}")
            };

            ctx.Nodes.Add(Database.Node.FromNode(typedNode));
            ctx.Relationships.Add(Relationship.FromRelationship(new ContainsRelationship(parentId, symbolId)));
            currentParentId = symbolId;
        }

        // Collect references inside the current symbol scope
        if (currentParentId.StartsWith("symbol:"))
        {
            if (node.Type is "identifier" or "type_identifier")
            {
                ctx.References.Add(new Reference(currentParentId, node.Text, OntologyConstants.Relationships.PotentialType));
            }

            ctx.Parser.CollectReferences(node, currentParentId, ctx.References);
        }

        foreach (var child in node.Children)
        {
            TraverseNode(child, currentParentId, ctx);
        }
    }
}
