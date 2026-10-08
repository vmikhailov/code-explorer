using System.Text.RegularExpressions;
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
        ctx.Log("[Layer3] Starting tree-sitter AST syntactic parsing pass...");

        WorkspaceConventions.LoadFromWorkspace(l2Result.Prev.Workspace.Path);

        var syntaxNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SyntaxStructure}";
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

        // O(1) parent index of Layer 1 tree (eliminates 160M recursive DFS traversals)
        var parentByChildId = new Dictionary<string, IOntologyNode>();
        void IndexPhysicalTree(IOntologyNode parent)
        {
            foreach (var child in parent.Children)
            {
                parentByChildId[child.Id] = parent;
                IndexPhysicalTree(child);
            }
        }
        IndexPhysicalTree(l2Result.Prev.Workspace);

        // O(F * P) pre-grouping of files to enclosing projects (runs once in <2ms, eliminates 167M comparisons in inner loops)
        var filesByProjectId = new Dictionary<string, List<FileNode>>(l2Result.Projects.Count);
        foreach (var file in l2Result.Prev.Files)
        {
            ProjectNode? bestMatch = null;
            int bestMatchLength = -1;
            foreach (var p in l2Result.Projects)
            {
                if (string.IsNullOrEmpty(p.Path))
                {
                    if (bestMatchLength < 0)
                    {
                        bestMatch = p;
                        bestMatchLength = 0;
                    }
                    continue;
                }

                var prefix = p.Path.Replace('\\', '/').TrimEnd('/') + "/";
                if (file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > bestMatchLength)
                {
                    bestMatch = p;
                    bestMatchLength = prefix.Length;
                }
            }

            if (bestMatch != null)
            {
                if (!filesByProjectId.TryGetValue(bestMatch.Id, out var list))
                {
                    list = [];
                    filesByProjectId[bestMatch.Id] = list;
                }
                list.Add(file);
            }
        }

        // Phase 0: Unified ConfigStore early initialization
        ConfigStore.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();

        var allProjectFileNodeIds = new HashSet<string>(filesByProjectId.Values.SelectMany(v => v).Select(f => f.Id));
        var workspaceRootFiles = l2Result.Prev.Files.Where(f => !allProjectFileNodeIds.Contains(f.Id)).ToList();
        foreach (var wfile in workspaceRootFiles)
        {
            if (ConfigStore.IsConfigurationFile(wfile.Name))
            {
                ConfigStore.LoadFile(wfile.FullPath, null, ctx);
            }
        }

        foreach (var project in l2Result.Projects)
        {
            if (filesByProjectId.TryGetValue(project.Id, out var pFiles))
            {
                foreach (var pfile in pFiles)
                {
                    if (ConfigStore.IsConfigurationFile(pfile.Name))
                    {
                        ConfigStore.LoadFile(pfile.FullPath, project.Name, ctx);
                    }
                }
            }
        }
        ctx.Log($"[ConfigStore] Pre-loaded {ConfigStore.GetDiscoveredUrls().Count} service endpoints and configuration keys into ConstantRegistry.");

        // Phase 0b: RouteDictionaryRegistry and early constants pre-scanning across all candidate files
        var allCandidateFiles = new List<(string FullPath, string? ProjectName)>();
        foreach (var wfile in workspaceRootFiles)
        {
            if (IsRouteOrConfigCandidate(wfile.Path)) allCandidateFiles.Add((wfile.FullPath, null));
        }
        foreach (var project in l2Result.Projects)
        {
            if (filesByProjectId.TryGetValue(project.Id, out var pFiles))
            {
                foreach (var pfile in pFiles)
                {
                    if (IsRouteOrConfigCandidate(pfile.Path)) allCandidateFiles.Add((pfile.FullPath, project.Name));
                }
            }
        }

        var fileContents = new List<(string Content, string? ProjectName, string FullPath)>();
        foreach (var (fPath, pName) in allCandidateFiles)
        {
            try
            {
                if (File.Exists(fPath))
                {
                    var content = File.ReadAllText(fPath);
                    RouteDictionaryRegistry.ScanServiceDomains(content);
                    fileContents.Add((content, pName, fPath));
                }
            }
            catch { }
        }

        foreach (var (content, pName, fPath) in fileContents)
        {
            try
            {
                RouteDictionaryRegistry.ScanAndRegister(content);
                ConstantRegistry.ScanAndRegister(fPath, content, pName);

                var prefixMatch = Regex.Match(content, @"setGlobalPrefix\s*\(\s*([^,\)]+)");
                if (prefixMatch.Success)
                {
                    var targetProj = pName ?? GetProjectNameFromRelativePath(fPath);
                    var prefix = ResolveGlobalPrefix(prefixMatch.Groups[1].Value, content, targetProj);
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        RouteDictionaryRegistry.RegisterGlobalPrefix(targetProj, prefix);
                    }
                }
            }
            catch { }
        }

        foreach (var project in l2Result.Projects)
        {
            nProject++;
            ctx.CancellationToken.ThrowIfCancellationRequested();

            ctx.Log($"[Layer3] Parsing project {nProject} of {l2Result.Projects.Count} at:'{project.Path}'...");

            var projectSyntaxId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Project}:{project.Path}:syntax";
            var projectSyntaxNode = new ProjectSyntaxNode(projectSyntaxId, "ProjectSyntax", project.Path);
            syntaxStructureNode.Children.Add(projectSyntaxNode);

            var belongsToRel = Relationship.FromRelationship(new BelongsToRelationship(projectSyntaxId, project.Id));
            await ctx.EnqueueUploadRelationshipsAsync([belongsToRel]);
            ctx.AddRelsCount(1);

            if (!filesByProjectId.TryGetValue(project.Id, out var projectFiles) || projectFiles.Count == 0)
            {
                continue;
            }

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ctx.CancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var parsedResults = new (FileNode File, SyntaxTree SyntaxTree, IOntologyNode ParentNode)?[projectFiles.Count];

            // Pass 1: Parse all syntax trees and discover declarations (constants, enums, raw variables)
            await Parallel.ForAsync(0, projectFiles.Count, parallelOptions, async (i, ct) =>
            {
                var file = projectFiles[i];
                var fileParser = WorkspaceIndexer.GetParserForFile(file.FullPath);
                if (fileParser == null) return;

                if (!parentByChildId.TryGetValue(file.Id, out var parentNode)) return;
                var parentId = parentNode.Id;

                try
                {
                    var syntaxTree = await fileParser.ParseAsync(file.FullPath, parentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
                    if (syntaxTree.Tree != null)
                    {
                        AstConstantExtractor.ExtractAndRegister(syntaxTree, project.Name);

                        for (int v = 0; v < syntaxTree.RawVariables.Count; v++)
                        {
                            var rv = syntaxTree.RawVariables[v];
                            if (rv.Scope != "local" && rv.IsConstant &&
                                !string.IsNullOrWhiteSpace(rv.Name) && !string.IsNullOrWhiteSpace(rv.InitializerText))
                            {
                                var rawInit = rv.InitializerText.Trim();
                                if ((rawInit.StartsWith('"') && rawInit.EndsWith('"')) ||
                                    (rawInit.StartsWith('\'') && rawInit.EndsWith('\'')) ||
                                    (rawInit.StartsWith('`') && rawInit.EndsWith('`')))
                                {
                                    ConstantRegistry.Register(project.Name, rv.Name, rawInit.Trim('\'', '"', '`'));
                                }
                            }
                        }

                        // Discover setGlobalPrefix('api/v1') in bootstrap/main files
                        if (file.Name.StartsWith("main.", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("app.", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("bootstrap.", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("index.", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("program.", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.StartsWith("startup.", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var fileText = File.ReadAllText(file.FullPath);
                                var prefixMatch = Regex.Match(fileText, @"setGlobalPrefix\s*\(\s*([^,\)]+)");
                                if (prefixMatch.Success)
                                {
                                    var resolvedPrefix = ResolveGlobalPrefix(prefixMatch.Groups[1].Value, fileText, project.Name);
                                    if (!string.IsNullOrEmpty(resolvedPrefix))
                                    {
                                        RouteDictionaryRegistry.RegisterGlobalPrefix(project.Name, resolvedPrefix);
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    parsedResults[i] = (file, syntaxTree, parentNode);
                }
                catch (Exception ex)
                {
                    ctx.LogWarning($"[Layer3] Error parsing file '{file.Path}': {ex.Message}", ex);
                }
            });

            // Pass 2: Traverse ASTs with language visitors and component parsers (now that all constants and symbols are registered)
            var projectAbsDir = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path)).Replace('\\', '/');
            var filesInProjectDir = Directory.Exists(projectAbsDir) ? Directory.GetFiles(projectAbsDir) : [];
            var projectContext = new ProjectContext(
                projectAbsDir,
                project.Path,
                project.Name,
                project.ProjectType,
                filesInProjectDir,
                [],
                project.Extensions ?? new Dictionary<string, string>());
            var componentParsers = Components.ComponentLibraryParserRegistry.GetParsers(projectContext);

            await Parallel.ForAsync(0, projectFiles.Count, parallelOptions, (i, ct) =>
            {
                var item = parsedResults[i];
                if (item == null) return ValueTask.CompletedTask;

                var (_, syntaxTree, _) = item.Value;
                if (syntaxTree.Tree != null)
                {
                    try
                    {
                        ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, project.Name);

                        if (componentParsers.Count > 0)
                        {
                            for (int cp = 0; cp < componentParsers.Count; cp++)
                            {
                                componentParsers[cp].EnrichWithSyntax(null, syntaxTree, ctx);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.LogWarning($"[Layer3] Error processing visitor for '{syntaxTree.FilePath}': {ex.Message}", ex);
                    }
                    finally
                    {
                        syntaxTree.Dispose(); // Free native TreeSitter memory immediately after Pass 2
                    }
                }

                return ValueTask.CompletedTask;
            });

            for (int i = 0; i < projectFiles.Count; i++)
            {
                var item = parsedResults[i];
                if (item == null) continue;

                var (file, syntaxTree, parentNode) = item.Value;

                // Replace the empty FileNode in physical tree with the parsed FileNode
                var idx = parentNode.Children.FindIndex(c => c.Id == file.Id);
                if (idx >= 0)
                {
                    parentNode.Children[idx] = syntaxTree.FileNode;
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
        }

        ctx.Log($"[Layer3] Syntactic parsing pass complete. Parsed {syntaxTrees.Count} AST trees.");
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

    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects) =>
        Layer2ProjectParser.IsEnclosedInProject(file, project, projects);

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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IFileParser, (List<ISemanticExtension> Active, SemanticExtensionRegistry Registry)> _parserRegistryCache = new();

    public static void ProcessVisitor(SyntaxTree syntaxTree, string workspaceId, string absoluteWorkspacePath, ParsingContext? ctx = null, string? projectName = null)
    {
        if (syntaxTree.Tree == null) return;

        var fileParser = syntaxTree.FileParser;
        var relativePath = syntaxTree.RelativePath;

        var (cachedActive, registry) = _parserRegistryCache.GetOrAdd(fileParser, fp =>
        {
            var active = fp.SemanticExtensions.Where(lp => lp.IsImplemented && lp.IsBuiltIn).ToList();
            var reg = new SemanticExtensionRegistry(fp.SemanticExtensions);
            return (active, reg);
        });

        var activeSemanticExtensions = new List<ISemanticExtension>(cachedActive);

        var mainVisitor = fileParser.CreateVisitor(syntaxTree.Tree.RootNode, activeSemanticExtensions, relativePath,
            absoluteWorkspacePath, fileParser, registry);
        mainVisitor.ProjectName = projectName;
        mainVisitor.Context = ctx;

        mainVisitor.Visit(syntaxTree.Tree.RootNode);

        var rootCounts = new Dictionary<(string Kind, string Name), int>();
        foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
        {
            var key = (childSyntactic.Kind, childSyntactic.Name);
            rootCounts.TryGetValue(key, out var count);
            count++;
            rootCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(syntaxTree.FilePath),
                relativePath, workspaceId, syntaxTree.FileNode.Id, ctx, count > 1 ? count : 0, projectName);
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
        string parentScopeId,
        ParsingContext? ctx = null,
        int overloadIndex = 0,
        string? projectName = null)
    {
        var node = syntactic.Node;
        var kind = syntactic.Kind;
        var name = syntactic.Name;

        var isType = kind == "Class" || kind == "Interface" || kind == OntologyConstants.NodeLabels.Type;
        var mappedKind = isType ? "Type" : kind;
        var idSuffix = overloadIndex > 1 ? $"#{overloadIndex}" : "";
        var symbolId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:{mappedKind}:{name}{idSuffix}";

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
            var sqlCandidate = syntactic.Text ?? node.Text;
            if (!NestedSqlParser.TryParseSql(sqlCandidate, out _, out _))
            {
                if (name.Contains(':'))
                {
                    var afterColon = name[(name.IndexOf(':') + 1)..].Trim();
                    if (NestedSqlParser.TryParseSql(afterColon, out _, out _))
                    {
                        sqlCandidate = afterColon;
                    }
                }
            }
            typedNode = NestedSqlParser.ParseNestedSql(sqlCandidate, symbolId, relativePath, ctx, name) ??
                        new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(sqlCandidate), relativePath);
        }
        else if (kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            var colonIdx = name.IndexOf(':');
            var isEndpoint = false;
            if (colonIdx > 0)
            {
                var method = name[..colonIdx].ToUpperInvariant();
                isEndpoint = method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "HEAD" or "GRAPHQL" or "RPC" or "GRPC" or "QUERY" or "MUTATION" or "SUBSCRIPTION";
            }
            if (syntactic.Protocol is "GraphQL" or "gRPC" or "REST")
            {
                isEndpoint = true;
            }

            if (isEndpoint)
            {
                typedNode = CreateEndpointNode(name, node, relativePath, workspaceId, syntactic, projectName);
            }
            else
            {
                typedNode = CreateEntryPointNode(name, node, relativePath, workspaceId, projectName);
            }
        }
        else if (kind == OntologyConstants.NodeLabels.ExternalService)
        {
            typedNode = CreateExternalServiceNode(name, node, relativePath, workspaceId, parentScopeId);
            if (!string.IsNullOrEmpty(parentScopeId))
            {
                ctx?.AddGlobalProjectDependency(new Relationship(
                    parentScopeId,
                    typedNode.Id,
                    OntologyConstants.Relationships.ServiceCall,
                    new Dictionary<string, object>
                    {
                        ["dependency_type"] = "service_call",
                        ["is_external"] = "true"
                    }
                ));
            }
        }
        else if (kind == OntologyConstants.NodeLabels.Table)
        {
            var resolvedName = name;
            if (ConstantRegistry.TryResolve(relativePath, name, out var rName))
            {
                resolvedName = rName;
            }
            var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{resolvedName.ToLowerInvariant()}";
            typedNode = new TableNode(tableId, resolvedName, relativePath);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported symbol type: {kind}");
        }

        var childCounts = new Dictionary<(string Kind, string Name), int>();
        foreach (var childSyntactic in syntactic.Children)
        {
            var key = (childSyntactic.Kind, childSyntactic.Name);
            childCounts.TryGetValue(key, out var count);
            count++;
            childCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId, ctx, count > 1 ? count : 0, projectName);
            typedNode.Children.Add(childNode);
        }

        foreach (var reference in syntactic.References)
        {
            var target = reference.TargetName;
            if (reference.Kind == OntologyConstants.Relationships.PersistedIn &&
                ConstantRegistry.TryResolve(relativePath, target, out var resolvedTarget))
            {
                target = resolvedTarget;
            }
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId, TargetName = target });
        }

        foreach (var (k, v) in syntactic.Properties)
        {
            typedNode.SetExtension(k, v);
        }

        return typedNode;
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim('/');
        route = (route ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
        return $"/{prefix}/{route}";
    }

    private static EndpointNode CreateEndpointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol? syntactic = null,
        string? projectName = null)
    {
        var idx = name.IndexOf(':');
        var method = idx > 0 ? name[..idx].ToUpperInvariant() : (syntactic?.Protocol == "gRPC" ? "RPC" : "GET");
        var route = idx > 0 ? name[(idx + 1)..] : name;

        var resolvedProj = !string.IsNullOrEmpty(projectName) ? projectName : GetProjectNameFromRelativePath(relativePath);

        // Apply project global route prefix (e.g. 'api/v1' or 'api') if discovered and not already present
        if (!string.IsNullOrEmpty(resolvedProj) &&
            RouteDictionaryRegistry.TryGetGlobalPrefix(resolvedProj, out var globalPrefix) &&
            !string.IsNullOrEmpty(globalPrefix))
        {
            var normRoute = route.Trim('/');
            if (!normRoute.StartsWith(globalPrefix, StringComparison.OrdinalIgnoreCase) &&
                !normRoute.Equals("ping", StringComparison.OrdinalIgnoreCase))
            {
                route = CombineRoutes(globalPrefix, route);
                name = $"{method}:{route}";
            }
        }

        var protocol = syntactic?.Protocol ?? (method is "RPC" or "GRPC" ? "gRPC" : (method is "GRAPHQL" or "QUERY" or "MUTATION" or "SUBSCRIPTION" ? "GraphQL" : "REST"));
        var operationType = syntactic?.OperationType ?? (protocol == "GraphQL"
            ? (method is "MUTATION" ? "Mutation" : (method is "SUBSCRIPTION" ? "Subscription" : "Query"))
            : (protocol == "gRPC" ? "Unary" : null));

        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";
        var endpointId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{projPart}{method}:{route}";
        return new EndpointNode(
            endpointId,
            name,
            relativePath,
            method,
            route,
            protocol,
            syntactic?.IsAnonymous ?? false,
            syntactic?.RequiredRoles,
            syntactic?.Policies,
            syntactic?.RequestType,
            syntactic?.ResponseType,
            operationType);
    }

    private static EntryPointNode CreateEntryPointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        string? projectName = null)
    {
        var resolvedProj = !string.IsNullOrEmpty(projectName) ? projectName : GetProjectNameFromRelativePath(relativePath);
        if (string.IsNullOrEmpty(resolvedProj)) resolvedProj = "default";

        var entryType = "grpc";
        var cleanName = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name[3..];
        }
        else if (name.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name[6..];
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            entryType = name[..idx];
            cleanName = name[(idx + 1)..];
        }

        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";
        var entryPointId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:{projPart}{entryType}:{cleanName}";

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
        string workspaceId,
        string? parentScopeId = null)
    {
        var cleanName = name?.Trim('"', '\'', '`', ' ', ';') ?? "";
        if (WorkspaceConventions.IsTestFilePath(relativePath) ||
            string.IsNullOrWhiteSpace(cleanName) ||
            cleanName.Length > 256 ||
            cleanName.Contains('\n') ||
            cleanName.Contains('\r') ||
            (!cleanName.StartsWith("http", StringComparison.OrdinalIgnoreCase) && (cleanName.Contains('{') || cleanName.Contains('}'))) ||
            cleanName.Contains('<') ||
            cleanName.Contains('>') ||
            cleanName.Contains('+') ||
            cleanName.Contains('(') ||
            cleanName.Contains(')') ||
            cleanName.Contains('"') ||
            cleanName.Contains('\'') ||
            cleanName.StartsWith('$') ||
            cleanName.Contains("=>") ||
            cleanName.Contains(';') ||
            int.TryParse(cleanName, out _))
        {
            cleanName = "unknown-service";
        }

        var protocol = "http";
        var domainOrService = cleanName;

        if (domainOrService.Contains("://"))
        {
            var pIdx = domainOrService.IndexOf("://");
            protocol = domainOrService[..pIdx];
            domainOrService = domainOrService[(pIdx + 3)..];
        }
        else if (domainOrService.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            domainOrService = domainOrService[3..];
        }
        else if (domainOrService.StartsWith("http:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "http";
            domainOrService = domainOrService[5..];
        }
        else if (domainOrService.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "https";
            domainOrService = domainOrService[6..];
        }
        else if (domainOrService.Contains(':'))
        {
            var idx = domainOrService.IndexOf(':');
            var candidateProto = domainOrService[..idx].ToLowerInvariant();
            if (candidateProto is "http" or "https" or "ws" or "wss" or "grpc")
            {
                protocol = candidateProto;
                domainOrService = domainOrService[(idx + 1)..];
            }
        }

        var path = "/";
        var slashIdx = domainOrService.IndexOf('/');
        if (slashIdx > 0)
        {
            var firstSegment = domainOrService[..slashIdx].ToLowerInvariant();
            if (firstSegment is "api" or "rest" or "v1" or "v2" or "v3" or "v4" or "v5" or "graphql")
            {
                domainOrService = "/" + domainOrService;
                slashIdx = 0;
            }
            else
            {
                path = domainOrService[slashIdx..];
                domainOrService = domainOrService[..slashIdx];
            }
        }
        else if (slashIdx == 0)
        {
            path = domainOrService;
            var segments = domainOrService.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? candidate = null;
            foreach (var seg in segments)
            {
                if (seg.Equals("api", StringComparison.OrdinalIgnoreCase)) continue;
                if (seg.StartsWith("v", StringComparison.OrdinalIgnoreCase) && seg.Length <= 6 && seg.Skip(1).All(c => char.IsDigit(c) || c == '.')) continue;
                if (seg.Equals("assets", StringComparison.OrdinalIgnoreCase) || seg.Equals("static", StringComparison.OrdinalIgnoreCase) || seg.Equals("public", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = null;
                    break;
                }
                candidate = seg;
                break;
            }

            if (!string.IsNullOrWhiteSpace(candidate) && candidate.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            {
                domainOrService = candidate;
            }
            else
            {
                domainOrService = "unknown-service";
            }
        }

        var colonPortIdx = domainOrService.LastIndexOf(':');
        if (colonPortIdx > 0 && int.TryParse(domainOrService[(colonPortIdx + 1)..], out _))
        {
            domainOrService = domainOrService[..colonPortIdx];
        }

        if (domainOrService.Contains('.'))
        {
            if (domainOrService.Contains(' ') || domainOrService.Any(c => c is '"' or '\'' or '{' or '}'))
            {
                domainOrService = "unknown-service";
            }
        }
        else if (domainOrService != "*" &&
                 protocol != "ws" && protocol != "wss" && protocol != "grpc")
        {
            if (string.IsNullOrWhiteSpace(domainOrService) ||
                domainOrService.Contains(' ') ||
                !Regex.IsMatch(domainOrService, @"^[a-zA-Z0-9_\-]+$"))
            {
                domainOrService = "unknown-service";
            }
        }

        var normalizedDomain = WorkspaceConventions.NormalizeServiceName(domainOrService);
        if (!string.IsNullOrEmpty(normalizedDomain) && !domainOrService.Contains('.'))
        {
            domainOrService = normalizedDomain;
        }

        if (PostIndexAnalyzer.IsGarbageExternalService(domainOrService))
        {
            domainOrService = "unknown-service";
        }

        var serviceScopeSuffix = domainOrService == "unknown-service"
            ? $":{ConstantRegistry.ExtractProjectName(relativePath) ?? "generic"}"
            : "";
        var extServiceId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{protocol}:{domainOrService}{serviceScopeSuffix}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath }, { "start_line", node.StartPosition.Row.ToString() }
        };
        if (!string.IsNullOrEmpty(parentScopeId))
        {
            ext["caller_symbol_id"] = parentScopeId;
        }

        if (slashIdx == 0)
        {
            ext["is_relative_path"] = "true";
            ext["inferred_from_path"] = "true";
            ext["url"] = path;
        }
        else
        {
            ext["is_external"] = "true";
        }
        return new ExternalServiceNode(extServiceId, domainOrService, protocol, domainOrService, path, ext);
    }

    private static bool IsRouteOrConfigCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = path.ToLowerInvariant();
        return (p.EndsWith(".ts") || p.EndsWith(".js") || p.EndsWith(".cs") || p.EndsWith(".go") || p.EndsWith(".py") || p.EndsWith(".json")) &&
               (p.Contains("route") || p.Contains("const") || p.Contains("config") || p.Contains("api") || p.Contains("endpoint") || p.Contains("url") || p.Contains("env") ||
                p.Contains("main.") || p.Contains("app.") || p.Contains("bootstrap.") || p.Contains("index.") || p.Contains("program.") || p.Contains("startup."));
    }

    private static string? ResolveGlobalPrefix(string rawArg, string fileText, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(rawArg)) return null;

        var cleaned = rawArg.Trim();

        // 1. If fallback pattern: expr || 'fallback'
        var orIdx = cleaned.IndexOf("||", StringComparison.Ordinal);
        if (orIdx >= 0)
        {
            var primary = cleaned[..orIdx].Trim();
            var fallback = cleaned[(orIdx + 2)..].Trim();
            var primaryResolved = ResolveGlobalPrefix(primary, fileText, projectName);
            if (!string.IsNullOrEmpty(primaryResolved) && !primaryResolved.Contains("${"))
            {
                return primaryResolved;
            }
            return ResolveGlobalPrefix(fallback, fileText, projectName);
        }

        // 2. If simple string literal: 'api/v1', "api/v1", `api/v1` (without template interpolation)
        if ((cleaned.StartsWith('\'') && cleaned.EndsWith('\'')) ||
            (cleaned.StartsWith('"') && cleaned.EndsWith('"')) ||
            (cleaned.StartsWith('`') && cleaned.EndsWith('`') && !cleaned.Contains("${")))
        {
            return cleaned.Trim('\'', '"', '`').Trim('/');
        }

        // 3. Scan local variable declarations in fileText (e.g. const apiVersion = 'v1')
        var localVars = new Dictionary<string, string>(StringComparer.Ordinal);
        var varMatches = Regex.Matches(fileText, @"(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*['""`]([^'""`\r\n]+)['""`]");
        foreach (Match m in varMatches)
        {
            var name = m.Groups[1].Value.Trim();
            var val = m.Groups[2].Value.Trim();
            localVars[name] = val;
            if (!string.IsNullOrEmpty(projectName))
            {
                ConstantRegistry.Register(projectName, name, val);
            }
        }

        string ResolveVar(string varName)
        {
            if (localVars.TryGetValue(varName, out var lv)) return lv;
            if (!string.IsNullOrEmpty(projectName) && ConstantRegistry.TryResolve(projectName, varName, out var cv)) return cv;
            if (ConstantRegistry.TryResolve(null, varName, out var gv)) return gv;
            if (ConfigStore.TryGetConfig(projectName, varName, out var cfgVal)) return cfgVal;
            return varName;
        }

        // 4. Template string with interpolation: `api/${apiVersion}` or `api/${apiVersion}/campaigns`
        if (cleaned.StartsWith('`') && cleaned.EndsWith('`'))
        {
            var templateContent = cleaned[1..^1];
            var resolved = Regex.Replace(templateContent, @"\$\{([A-Za-z0-9_]+)\}", m =>
            {
                var varName = m.Groups[1].Value;
                var val = ResolveVar(varName);
                return val != varName ? val : m.Value;
            });

            if (!resolved.Contains("${"))
            {
                return resolved.Trim('/');
            }
        }

        // 5. String concatenation with +: 'api/' + apiVersion
        if (cleaned.Contains('+'))
        {
            var parts = cleaned.Split('+');
            var sb = new System.Text.StringBuilder();
            var allOk = true;
            foreach (var rawPart in parts)
            {
                var p = rawPart.Trim();
                if ((p.StartsWith('\'') && p.EndsWith('\'')) ||
                    (p.StartsWith('"') && p.EndsWith('"')) ||
                    (p.StartsWith('`') && p.EndsWith('`')))
                {
                    sb.Append(p.Trim('\'', '"', '`'));
                }
                else if (Regex.IsMatch(p, @"^[A-Za-z0-9_]+$"))
                {
                    var val = ResolveVar(p);
                    if (val != p)
                    {
                        sb.Append(val);
                    }
                    else
                    {
                        allOk = false;
                        break;
                    }
                }
                else
                {
                    allOk = false;
                    break;
                }
            }
            if (allOk && sb.Length > 0)
            {
                return sb.ToString().Trim('/');
            }
        }

        // 6. Direct identifier: apiVersion or API_PREFIX
        if (Regex.IsMatch(cleaned, @"^[A-Za-z0-9_]+$"))
        {
            var val = ResolveVar(cleaned);
            if (val != cleaned)
            {
                return val.Trim('/');
            }
        }

        return null;
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
