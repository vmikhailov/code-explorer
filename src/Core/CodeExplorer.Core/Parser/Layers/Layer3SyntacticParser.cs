using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer3SyntacticParser
{
    public async Task<Layer3Result> ParseAsync(Layer2Result l2Result, ParsingContext ctx)
    {
        ctx.Log("[Layer3SyntacticParser] Starting tree-sitter AST syntactic parsing pass...");

        var syntaxNodeId = $"{ctx.WorkspaceId}:syntax_structure";
        var syntaxStructureNode = new SyntaxStructureNode(syntaxNodeId, "SyntaxStructure", l2Result.Prev.Workspace.Path);
        l2Result.Prev.Workspace.Children.Add(syntaxStructureNode);
        ctx.SyntaxStructure = syntaxStructureNode;

        var syntaxTrees = new List<SyntaxTree>();
        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();
        var globalReferences = new List<Reference>();
        var globalSymbols = new Dictionary<(string Kind, string Name), string>();
        var nProject = 0;

        foreach (var project in l2Result.Projects)
        {
            nProject++;
            ctx.CancellationToken.ThrowIfCancellationRequested();

            ctx.Log($"[Layer3SyntacticParser] Parsing project {nProject} of {l2Result.Projects.Count} at:'{project.Path}'...");

            var projectSyntaxId = $"{ctx.WorkspaceId}:project:{project.Path}:project_syntax";
            var projectSyntaxNode = new ProjectSyntaxNode(projectSyntaxId, "ProjectSyntax", project.Path);
            syntaxStructureNode.Children.Add(projectSyntaxNode);

            var belongsToRel = Relationship.FromRelationship(new BelongsToRelationship(projectSyntaxId, project.Id));
            await ctx.EnqueueUploadRelationshipsAsync([belongsToRel]);
            ctx.AddRelsCount(1);

            // Find all files belonging to this project
            var projectFiles = l2Result.Prev.Files.Where(f => IsEnclosedInProject(f, project, l2Result.Projects)).ToList();

            foreach (var file in projectFiles)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                var ext = Path.GetExtension(file.Name).ToLower();
                var fileParser = WorkspaceIndexer._fileParsers.FirstOrDefault(p => p.CanParse(ext));
                if (fileParser == null) continue;

                // Find parent of the file node in Layer 1 tree
                string? parentId = FindParentId(l2Result.Prev.Workspace, file.Id);
                if (parentId == null) continue;

                try
                {
                    var syntaxTree = await fileParser.ParseAsync(file.FullPath, parentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
                    if (syntaxTree.Tree != null)
                    {
                        ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
                    }

                    // Replace the empty FileNode in physical tree with the parsed FileNode
                    var parentNode = FindNodeById(l2Result.Prev.Workspace, parentId);
                    if (parentNode != null)
                    {
                        var idx = parentNode.Children.FindIndex(c => c.Id == file.Id);
                        if (idx >= 0)
                        {
                            parentNode.Children[idx] = syntaxTree.FileNode;
                        }
                    }

                    // Add top-level symbols to project syntax node
                    foreach (var child in syntaxTree.FileNode.Children)
                    {
                        if (child is TypeNode || child is FunctionNode || child is MemberNode)
                        {
                            projectSyntaxNode.Children.Add(child);
                        }
                    }

                    syntaxTrees.Add(syntaxTree);
                    rawImports.AddRange(syntaxTree.RawImports);
                    rawVariables.AddRange(syntaxTree.RawVariables);
                    rawTypeBindings.AddRange(syntaxTree.RawTypeBindings);

                    // Add to global lists in context for late binding / indexing compatibility
                    ctx.RawImports.AddRange(syntaxTree.RawImports);
                    ctx.RawVariables.AddRange(syntaxTree.RawVariables);
                    ctx.RawTypeBindings.AddRange(syntaxTree.RawTypeBindings);
                }
                catch (Exception ex)
                {
                    ctx.Log($"[Layer3SyntacticParser] Error parsing file '{file.Path}': {ex.Message}");
                }
            }
        }

        ctx.Log($"[Layer3SyntacticParser] Syntactic parsing pass complete. Parsed {syntaxTrees.Count} AST trees.");
        return new Layer3Result(
            l2Result,
            syntaxStructureNode,
            syntaxTrees,
            rawImports,
            rawVariables,
            rawTypeBindings,
            globalReferences,
            globalSymbols
        );
    }

    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects)
    {
        ProjectNode? bestMatch = null;
        int bestMatchLength = -1;

        foreach (var p in projects)
        {
            var pPath = p.Path;
            if (pPath == "")
            {
                if (bestMatchLength < 0)
                {
                    bestMatch = p;
                    bestMatchLength = 0;
                }
                continue;
            }

            var pPrefix = pPath + "/";
            if (file.Path.StartsWith(pPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (pPrefix.Length > bestMatchLength)
                {
                    bestMatch = p;
                    bestMatchLength = pPrefix.Length;
                }
            }
        }

        return bestMatch?.Id == project.Id;
    }

    private static string? FindParentId(IOntologyNode root, string childId)
    {
        foreach (var child in root.Children)
        {
            if (child.Id == childId) return root.Id;
            var parentId = FindParentId(child, childId);
            if (parentId != null) return parentId;
        }
        return null;
    }

    private static IOntologyNode? FindNodeById(IOntologyNode root, string id)
    {
        if (root.Id == id) return root;
        foreach (var child in root.Children)
        {
            var found = FindNodeById(child, id);
            if (found != null) return found;
        }
        return null;
    }

    public static void ProcessVisitor(SyntaxTree syntaxTree, string workspaceId, string absoluteWorkspacePath)
    {
        if (syntaxTree.Tree == null) return;

        var fileParser = syntaxTree.FileParser;
        var relativePath = syntaxTree.RelativePath;

        var activeLibraryParsers = fileParser.LibraryParsers.Where(lp => lp.IsImplemented && lp.IsBuiltIn).ToList();
        var registry = new LibraryTrieRegistry(fileParser.LibraryParsers);

        var mainVisitor = fileParser.CreateVisitor(syntaxTree.Tree.RootNode, activeLibraryParsers, relativePath,
            absoluteWorkspacePath, fileParser, registry);

        mainVisitor.Visit(syntaxTree.Tree.RootNode);

        foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
        {
            var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(syntaxTree.FilePath),
                relativePath, workspaceId, syntaxTree.FileNode.Id);
            syntaxTree.FileNode.Children.Add(childNode);
        }

        foreach (var reference in mainVisitor.RootSymbol.References)
        {
            syntaxTree.FileNode.References.Add(reference with { ScopeSymbolId = syntaxTree.FileNode.Id });
        }

        var rawImports = mainVisitor.RawImports.Select(ri => ri with
        {
            FilePath = relativePath,
            Type = fileParser.ResolveImportType(ri.Path, relativePath, absoluteWorkspacePath)
        }).ToList();

        var rawVariables = mainVisitor.RawVariables.Select(rv => rv with { FilePath = relativePath }).ToList();
        var rawTypeBindings = mainVisitor.RawTypeBindings.Select(rt => rt with { FilePath = relativePath }).ToList();

        syntaxTree.RawImports.AddRange(rawImports);
        syntaxTree.RawVariables.AddRange(rawVariables);
        syntaxTree.RawTypeBindings.AddRange(rawTypeBindings);
    }

    private static IOntologyNode MapSyntacticSymbolToOntology(
        SyntacticSymbol syntactic,
        string fileName,
        string relativePath,
        string workspaceId,
        string parentScopeId)
    {
        var node = syntactic.Node;
        var kind = syntactic.Kind;
        var name = syntactic.Name;

        var isType = kind == "Class" || kind == "Interface" || kind == OntologyConstants.NodeLabels.Type;
        var mappedKind = isType ? "Type" : kind;
        var symbolId = $"{workspaceId}:symbol:{relativePath}:{mappedKind}:{name}:{node.StartPosition.Row}";

        IOntologyNode typedNode;
        if (kind == "Class")
        {
            typedNode = new TypeNode(symbolId, name, symbolId, fileName, relativePath,
                node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column, "class");
        }
        else if (kind == "Interface")
        {
            typedNode = new TypeNode(symbolId, name, symbolId, fileName, relativePath,
                node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column, "interface");
        }
        else if (kind == OntologyConstants.NodeLabels.Function)
        {
            typedNode = new FunctionNode(symbolId, name, symbolId, fileName, relativePath,
                node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column);
        }
        else if (kind == OntologyConstants.NodeLabels.Query)
        {
            typedNode = NestedSqlParser.ParseNestedSql(syntactic.Text ?? node.Text, symbolId, relativePath) ??
                        new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(syntactic.Text ?? node.Text), relativePath);
        }
        else if (kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var colonIdx = name.IndexOf(':');
            var isHttp = false;
            if (colonIdx > 0)
            {
                var method = name.Substring(0, colonIdx).ToUpperInvariant();
                isHttp = method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH";
            }

            if (isHttp)
            {
                typedNode = CreateEndpointNode(name, node, relativePath, workspaceId);
            }
            else
            {
                typedNode = CreateEntryPointNode(name, node, relativePath, workspaceId);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            typedNode = CreateExternalServiceNode(name, node, relativePath, workspaceId);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported symbol type: {kind}");
        }

        foreach (var childSyntactic in syntactic.Children)
        {
            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId);
            typedNode.Children.Add(childNode);
        }

        foreach (var reference in syntactic.References)
        {
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId });
        }

        return typedNode;
    }

    private static EndpointNode CreateEndpointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId)
    {
        var idx = name.IndexOf(':');
        var method = name.Substring(0, idx).ToUpperInvariant();
        var route = name.Substring(idx + 1);

        var endpointId = $"{workspaceId}:endpoint:{method}:{route}";
        return new EndpointNode(endpointId, name, relativePath, method, route);
    }

    private static EntryPointNode CreateEntryPointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId)
    {
        var projectName = GetProjectNameFromRelativePath(relativePath);
        if (string.IsNullOrEmpty(projectName)) projectName = "default";

        var entryType = "grpc";
        var cleanName = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name.Substring(3);
        }
        else if (name.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name.Substring(6);
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            entryType = name.Substring(0, idx);
            cleanName = name.Substring(idx + 1);
        }

        var entryPointId = $"{workspaceId}:entrypoint:{entryType}:{cleanName}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath }, { "start_line", node.StartPosition.Row.ToString() }
        };
        return new EntryPointNode(entryPointId, cleanName, relativePath, entryType, ext);
    }

    private static ExternalServiceNode CreateExternalServiceNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId)
    {
        var protocol = "http";
        var domainOrService = name;

        if (domainOrService.Contains("://"))
        {
            var pIdx = domainOrService.IndexOf("://");
            protocol = domainOrService.Substring(0, pIdx);
            domainOrService = domainOrService.Substring(pIdx + 3);
        }
        else if (domainOrService.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            domainOrService = domainOrService.Substring(3);
        }
        else if (domainOrService.Contains(':'))
        {
            var idx = domainOrService.IndexOf(':');
            protocol = domainOrService.Substring(0, idx);
            domainOrService = domainOrService.Substring(idx + 1);
        }

        var path = "/";
        var slashIdx = domainOrService.IndexOf('/');
        if (slashIdx >= 0)
        {
            path = domainOrService.Substring(slashIdx);
            domainOrService = domainOrService.Substring(0, slashIdx);
        }

        var extServiceId = $"{workspaceId}:externalservice:{protocol}:{domainOrService}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath }, { "start_line", node.StartPosition.Row.ToString() }
        };
        return new ExternalServiceNode(extServiceId, domainOrService, protocol, domainOrService, path, ext);
    }

    private static string GetProjectNameFromRelativePath(string relativePath)
    {
        var cleanPath = relativePath.Replace('\\', '/').Trim('/');
        var parts = cleanPath.Split('/');
        if (parts.Length == 0) return "default";

        if (parts.Length >= 2 && (parts[0] is "Core" or "Parsers" or "Tests"))
        {
            return parts[1];
        }

        return parts[0];
    }
}
