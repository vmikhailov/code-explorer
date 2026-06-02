using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using TreeSitter;
using CodeExplorer.Database;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public class FileLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _filePath;
    private readonly string _parentFolderOrProjectId;
    private readonly ILanguageParser _languageParser;

    public FileLevelParser(ParsingContext ctx, string filePath, string parentFolderOrProjectId, ILanguageParser languageParser)
    {
        _ctx = ctx;
        _filePath = filePath.Replace('\\', '/');
        _parentFolderOrProjectId = parentFolderOrProjectId;
        _languageParser = languageParser;
    }

    public async Task ParseAsync()
    {
        var relativePath = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, _filePath).Replace('\\', '/');
        Console.Error.WriteLine($"[WorkspaceParser] Parsing file: '{relativePath}' ({_languageParser.ProjectType})");

        try
        {
            var sourceText = await File.ReadAllTextAsync(_filePath);

            using var language = new Language(_languageParser.LanguageName);
            using var parser = new TreeSitter.Parser(language);
            using var tree = parser.Parse(sourceText);

            if (tree == null || tree.RootNode == null) return;

            var fileCtx = new FileContext(_ctx.AbsoluteWorkspacePath, relativePath, sourceText, _languageParser);

            // 1. Create and register the File Node
            var fileNodeId = $"file:{_ctx.AbsoluteWorkspacePath}:{relativePath}";
            var fileNode = new CodeExplorer.Database.Node(fileNodeId, OntologyConstants.NodeLabels.File, new Dictionary<string, object>
            {
                ["path"] = Path.GetFileName(_filePath),
                ["name"] = Path.GetFileName(_filePath)
            });
            fileCtx.Nodes.Add(fileNode);

            // 2. Link File Node to Parent (WorkspaceFolder or ProjectFolder or Project)
            fileCtx.Relationships.Add(new Relationship(_parentFolderOrProjectId, fileNodeId, OntologyConstants.Relationships.Contains));

            // 3. Traverse the AST
            TraverseNode(tree.RootNode, fileNodeId, fileCtx);

            // 4. Flush Mapped File Data to Persistence Channel & Update Global Stats
            if (fileCtx.Nodes.Count > 0)
            {
                await _ctx.SharedChannel.Writer.WriteAsync(() => _ctx.DbClient.UploadNodesAsync(fileCtx.Nodes));
                lock (_ctx.NodesByKind)
                {
                    foreach (var node in fileCtx.Nodes)
                    {
                        if (!_ctx.NodesByKind.ContainsKey(node.Kind)) _ctx.NodesByKind[node.Kind] = 0;
                        _ctx.NodesByKind[node.Kind]++;

                        // Map global symbols for reference resolution
                        if (node.Kind == OntologyConstants.NodeLabels.Class || node.Kind == OntologyConstants.NodeLabels.Interface || node.Kind == OntologyConstants.NodeLabels.Function)
                        {
                            if (node.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
                            {
                                lock (_ctx.GlobalSymbols)
                                {
                                    _ctx.GlobalSymbols[(node.Kind, nameStr)] = node.Id;
                                }
                            }
                        }
                    }
                }
                _ctx.TotalNodesCount += fileCtx.Nodes.Count;
            }

            if (fileCtx.Relationships.Count > 0)
            {
                await _ctx.SharedChannel.Writer.WriteAsync(() => _ctx.DbClient.UploadRelationshipsAsync(fileCtx.Relationships));
                _ctx.TotalRelsCount += fileCtx.Relationships.Count;
            }

            if (fileCtx.References.Count > 0)
            {
                lock (_ctx.GlobalReferences)
                {
                    _ctx.GlobalReferences.AddRange(fileCtx.References);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing file {_filePath}: {ex.Message}");
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
            var properties = new Dictionary<string, object>
            {
                ["name"] = name,
                ["symbol"] = symbolId,
                ["start_line"] = node.StartPosition.Row,
                ["start_col"] = node.StartPosition.Column,
                ["end_line"] = node.EndPosition.Row,
                ["end_col"] = node.EndPosition.Column,
                ["file_path"] = Path.GetFileName(ctx.FilePath)
            };

            ctx.Nodes.Add(new CodeExplorer.Database.Node(symbolId, kind, properties));
            ctx.Relationships.Add(new Relationship(parentId, symbolId, OntologyConstants.Relationships.Contains));
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
