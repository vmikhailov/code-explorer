using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer4SemanticParser
{
    public async Task<Layer4Result> ParseAsync(Layer3Result l3Result, ParsingContext ctx)
    {
        ctx.Log("[Layer 4    ] Starting semantic enrichment pass...");

        var semanticNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SemanticStructure}";
        var semanticStructureNode = new SemanticStructureNode(semanticNodeId, "SemanticStructure", l3Result.Prev.Prev.Workspace.Path);
        l3Result.Prev.Prev.Workspace.Children.Add(semanticStructureNode);
        ctx.SemanticStructure = semanticStructureNode;

        var semanticNodes = new List<IOntologyNode>();
        var semanticRelationships = new List<Relationship>();

        var allProjects = l3Result.Prev.Projects;
        var allFiles = l3Result.Prev.Prev.Files;
        var allTrees = l3Result.SyntaxTrees;

        // O(F * P) pre-grouping once instead of O(P * F * P) in inner loops (eliminates 1.72B comparisons)
        var filesByProjectId = new Dictionary<string, List<FileNode>>(allProjects.Count);
        var treesByProjectId = new Dictionary<string, List<SyntaxTree>>(allProjects.Count);
        var fileToProjectId = new Dictionary<string, string>(allFiles.Count, StringComparer.OrdinalIgnoreCase);
        var workspaceRootFiles = new List<FileNode>();

        foreach (var project in allProjects)
        {
            filesByProjectId[project.Id] = [];
            treesByProjectId[project.Id] = [];
        }

        foreach (var file in allFiles)
        {
            var bestProject = Layer2ProjectParser.FindProjectForFilePath(file.Path, allProjects);
            if (bestProject != null)
            {
                filesByProjectId[bestProject.Id].Add(file);
                fileToProjectId[file.Id] = bestProject.Id;
            }
            else
            {
                workspaceRootFiles.Add(file);
            }
        }

        foreach (var st in allTrees)
        {
            if (st.FileNode != null && fileToProjectId.TryGetValue(st.FileNode.Id, out var projId))
            {
                treesByProjectId[projId].Add(st);
            }
        }

        // 1. Parse workspace-level configuration files (e.g. root .env) first
        foreach (var wfile in workspaceRootFiles)
        {
            if (ConfigurationParser.IsConfigurationFile(wfile.Name))
            {
                ConfigurationParser.ParseAndEnrich(wfile.FullPath, wfile.Path, ctx.WorkspaceId, semanticStructureNode, semanticRelationships, ctx);
            }
        }

        var totalProjects = allProjects.Count;
        ctx.Log($"[Layer 4    ] Enriching {totalProjects} projects ({allTrees.Count} syntax trees) with semantic information...");

        var existingRelKeys = new HashSet<(string From, string To, string Kind)>();
        foreach (var r in semanticRelationships)
        {
            existingRelKeys.Add((r.From, r.To, r.Kind));
        }

        // Materialize first-class semantic workload nodes under SemanticStructure before per-project enrichment
        ctx.Log($"[Layer 4    ] Materializing semantic workload hierarchy ({allProjects.Count} projects)...");
        var workloadByProjectId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var workloadNodeByProjectId = new Dictionary<string, IOntologyNode>(StringComparer.OrdinalIgnoreCase);
        var semanticStructureChildIds = new HashSet<string>(semanticStructureNode.Children.Select(c => c.Id), StringComparer.Ordinal);
        var workloadChildIdsByWorkload = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var rawProjectNames = allProjects.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var project in allProjects)
        {
            var extensions = new Dictionary<string, string>(project.Extensions ?? new())
            {
                ["project_id"] = project.Id,
                ["role"] = project.Role,
                ["is_library"] = project.IsLibrary ? "true" : "false"
            };

            var cleanName = WorkspaceConventions.NormalizeServiceName(project.Name);
            if (string.IsNullOrEmpty(cleanName)) cleanName = project.Name;
            if (!cleanName.Equals(project.Name, StringComparison.OrdinalIgnoreCase) && rawProjectNames.Contains(cleanName))
            {
                cleanName = project.Name.Trim().ToLowerInvariant();
            }
            extensions["raw_name"] = project.Name;
            extensions["clean_name"] = cleanName;

            var isTestProject = project.Role == OntologyConstants.ProjectRoles.Test ||
                                project.IsTest ||
                                string.Equals(project.Extensions?.GetValueOrDefault("primary_role"), "TestFramework", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(project.Extensions?.GetValueOrDefault("primary_role"), "Test", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(project.Extensions?.GetValueOrDefault("is_test_project"), "true", StringComparison.OrdinalIgnoreCase);

            var canonicalPrimaryRole = isTestProject
                ? "Test"
                : (ProjectNodeFactory.NormalizeRoleName(extensions.GetValueOrDefault("primary_role"))
                   ?? ProjectNodeFactory.NormalizeRoleName(project.Role)
                   ?? (project.IsLibrary ? "Library" : "Service"));

            var secRolesList = new List<string>();
            if (extensions.TryGetValue("secondary_roles", out var secJson) && !string.IsNullOrWhiteSpace(secJson))
            {
                try
                {
                    using var sDoc = System.Text.Json.JsonDocument.Parse(secJson);
                    if (sDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in sDoc.RootElement.EnumerateArray())
                        {
                            var norm = ProjectNodeFactory.NormalizeRoleName(item.GetString());
                            if (norm != null && norm != canonicalPrimaryRole && !secRolesList.Contains(norm))
                            {
                                secRolesList.Add(norm);
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            if (!project.IsLibrary && !isTestProject && canonicalPrimaryRole is "Service" or "App" or "Worker" or "CliTool")
            {
                var hasWorkerSignals = string.Equals(extensions.GetValueOrDefault("is_queue_worker"), "true", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(extensions.GetValueOrDefault("is_scheduler"), "true", StringComparison.OrdinalIgnoreCase) ||
                                      l3Result.Prev.ProjectDependencies.Any(d => d.From == project.Id && (d.Kind == "SUBSCRIBES" || d.Kind == "SUBSCRIBES_TO" || d.Kind == "CONSUMES" || d.Kind == "TRIGGERS"));

                if (hasWorkerSignals && canonicalPrimaryRole != "Worker")
                {
                    if (!secRolesList.Contains("Worker"))
                    {
                        secRolesList.Add("Worker");
                    }
                }
            }

            secRolesList.Remove(canonicalPrimaryRole);

            extensions["primary_role"] = canonicalPrimaryRole;
            extensions["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secRolesList);
            extensions["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { canonicalPrimaryRole }.Concat(secRolesList).Distinct().ToList());

            if (project.Extensions != null)
            {
                project.Extensions["primary_role"] = canonicalPrimaryRole;
                project.Extensions["secondary_roles"] = extensions["secondary_roles"];
                project.Extensions["all_roles"] = extensions["all_roles"];
            }

            IOntologyNode workloadNode = isTestProject
                ? new TestSuiteNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType,
                    extensions)
                : canonicalPrimaryRole switch
                {
                    "App" => new AppNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.App}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                    "Service" => new ServiceNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                    "Worker" => new WorkerNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Worker}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                    "Library" => new LibraryNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                    "CliTool" => new CliToolNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CliTool}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                    "Test" => new TestSuiteNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                    _ => project.IsLibrary
                        ? new LibraryNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{cleanName}", cleanName, project.Path, project.ProjectType, extensions)
                        : new ServiceNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{cleanName}", cleanName, project.Path, project.ProjectType, extensions)
                };

            if (semanticStructureChildIds.Add(workloadNode.Id))
            {
                semanticStructureNode.Children.Add(workloadNode);
                semanticNodes.Add(workloadNode);
            }
            if (!workloadChildIdsByWorkload.ContainsKey(workloadNode.Id))
            {
                workloadChildIdsByWorkload[workloadNode.Id] = new HashSet<string>(workloadNode.Children.Select(c => c.Id), StringComparer.Ordinal);
            }

            if (existingRelKeys.Add((workloadNode.Id, project.Id, OntologyConstants.Relationships.ImplementedBy)))
            {
                semanticRelationships.Add(new Relationship(workloadNode.Id, project.Id, OntologyConstants.Relationships.ImplementedBy, new Dictionary<string, object>()));
            }

            workloadByProjectId[project.Id] = workloadNode.Id;
            workloadNodeByProjectId[project.Id] = workloadNode;
        }

        ResourceSemanticAnalyzer.ResetCache();
        var lastLogTicks = Environment.TickCount64;
        var projectsProcessed = 0;

        foreach (var project in allProjects)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            projectsProcessed++;
            var workloadNode = workloadNodeByProjectId[project.Id];

            void ReportProgressIfNeeded(bool force = false)
            {
                var now = Environment.TickCount64;
                if (force || projectsProcessed == 1 || projectsProcessed == totalProjects ||
                    projectsProcessed % 10 == 0 || (now - lastLogTicks) >= 3000)
                {
                    var pct = totalProjects > 0 ? (double)projectsProcessed / totalProjects * 100.0 : 100.0;
                    var globalPct = 52 + (int)(16.0 * projectsProcessed / Math.Max(1, totalProjects));
                    ctx.ReportProgress(globalPct, "Layer 4: Semantic", $"Enriched {projectsProcessed}/{totalProjects} projects ({pct:F0}%) - {project.Name}");
                    ctx.Log($"[Layer 4    ] Enriched {projectsProcessed}/{totalProjects} projects ({pct:F0}%) - {project.Name}...");
                    lastLogTicks = now;
                    ctx.TriggerProgressReport();
                }
            }

            // Find project parser(s) — fast-path via in-memory ProjectType lookup before falling back to directory scan
            var directParser = WorkspaceIndexer.GetProjectParser(project.ProjectType);
            List<IProjectParser> matchingParsers;
            if (directParser != null)
            {
                matchingParsers = [directParser];
            }
            else
            {
                var projectDirectory = project.Extensions?.GetValueOrDefault("cargo_manifest_dir");
                var projectAbsDir = !string.IsNullOrWhiteSpace(projectDirectory)
                    ? Path.GetFullPath(projectDirectory)
                    : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path));
                projectAbsDir = projectAbsDir.Replace('\\', '/');
                var filesInDir = Directory.Exists(projectAbsDir) ? Directory.GetFiles(projectAbsDir) : [];
                matchingParsers = WorkspaceIndexer._projectParsers
                    .Where(p => p.IsProjectDirectory(projectAbsDir, filesInDir))
                    .ToList();
            }
            var projectParser = matchingParsers.FirstOrDefault();

            if (projectParser == null)
            {
                ReportProgressIfNeeded();
                continue;
            }

            // 2. Parse project-level configuration files before syntax enrichment so resources are available in registry
            var projectFiles = filesByProjectId.GetValueOrDefault(project.Id) ?? [];
            var initialRelCount = semanticRelationships.Count;
            foreach (var pfile in projectFiles)
            {
                if (ConfigurationParser.IsConfigurationFile(pfile.Name))
                {
                    ConfigurationParser.ParseAndEnrich(pfile.FullPath, pfile.Path, ctx.WorkspaceId, workloadNode, semanticRelationships, ctx);
                }
            }
            if (semanticRelationships.Count > initialRelCount)
            {
                for (var i = initialRelCount; i < semanticRelationships.Count; i++)
                {
                    var r = semanticRelationships[i];
                    existingRelKeys.Add((r.From, r.To, r.Kind));
                }
            }

            // 3. Get syntax trees belonging to this project and run semantic enrichers
            var projectTrees = treesByProjectId.GetValueOrDefault(project.Id) ?? [];

            foreach (var syntaxTree in projectTrees)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                EndpointSemanticAnalyzer.Enrich(syntaxTree, project, ctx, semanticRelationships);

                var targetParser = projectParser;
                if (syntaxTree.FileNode != null)
                {
                    var ext = Path.GetExtension(syntaxTree.FileNode.Name);
                    var fileMatchedParser = matchingParsers.FirstOrDefault(p => p is IFileParser fp && fp.CanParse(ext))
                                            ?? WorkspaceIndexer._projectParsers.FirstOrDefault(p => p is IFileParser fp && fp.CanParse(ext));
                    if (fileMatchedParser != null)
                    {
                        targetParser = fileMatchedParser;
                    }
                }
                await ResourceSemanticAnalyzer.EnrichAsync(syntaxTree, project, targetParser, ctx, workloadNode);
            }

            // Collect semantic nodes parsed from files
            var projectSemanticNodes = new List<IOntologyNode>();
            foreach (var syntaxTree in projectTrees)
            {
                if (syntaxTree.FileNode != null)
                {
                    CollectSemanticNodes(syntaxTree.FileNode, projectSemanticNodes);
                }
                syntaxTree.Dispose();
            }

            // If code-first GraphQL EndpointNodes exist in this project for a given (OperationType, RouteTemplate),
            // suppress duplicate SDL-derived EndpointNodes (e.g. from schema.graphql dumps) for the same field.
            var codeFirstGqlFields = projectSemanticNodes
                .OfType<EndpointNode>()
                .Where(ep => string.Equals(ep.Protocol, "GraphQL", StringComparison.OrdinalIgnoreCase) &&
                             (ep.Extensions == null || !ep.Extensions.TryGetValue("graphql_sdl", out var isSdl) || isSdl != "true"))
                .Select(ep => ($"{ep.OperationType ?? "Query"}:{ep.RouteTemplate.Trim('/')}"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (codeFirstGqlFields.Count > 0)
            {
                var duplicateSdlIds = projectSemanticNodes
                    .OfType<EndpointNode>()
                    .Where(ep => ep.Extensions != null &&
                                 ep.Extensions.TryGetValue("graphql_sdl", out var isSdl) &&
                                 isSdl == "true" &&
                                 codeFirstGqlFields.Contains($"{ep.OperationType ?? "Query"}:{ep.RouteTemplate.Trim('/')}"))
                    .Select(ep => ep.Id)
                    .ToHashSet(StringComparer.Ordinal);

                if (duplicateSdlIds.Count > 0)
                {
                    projectSemanticNodes.RemoveAll(n => duplicateSdlIds.Contains(n.Id));
                    foreach (var syntaxTree in projectTrees)
                    {
                        syntaxTree.FileNode?.Children.RemoveAll(c => duplicateSdlIds.Contains(c.Id));
                    }
                }
            }

            if (!project.IsLibrary && projectSemanticNodes.OfType<EndpointNode>().Any() &&
                workloadNode.Extensions != null &&
                workloadNode.Extensions.TryGetValue("primary_role", out var primRole) &&
                primRole is "App" or "Worker" or "CliTool")
            {
                var secRolesList = new List<string>();
                if (workloadNode.Extensions.TryGetValue("secondary_roles", out var secJson) && !string.IsNullOrWhiteSpace(secJson))
                {
                    try
                    {
                        secRolesList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(secJson) ?? [];
                    }
                    catch { }
                }
                if (!secRolesList.Contains("Service"))
                {
                    secRolesList.Add("Service");
                    var newSecJson = System.Text.Json.JsonSerializer.Serialize(secRolesList);
                    var newAllJson = System.Text.Json.JsonSerializer.Serialize(new[] { primRole }.Concat(secRolesList).Distinct().ToList());
                    workloadNode.Extensions["secondary_roles"] = newSecJson;
                    workloadNode.Extensions["all_roles"] = newAllJson;
                    if (project.Extensions != null)
                    {
                        project.Extensions["secondary_roles"] = newSecJson;
                        project.Extensions["all_roles"] = newAllJson;
                    }
                }
            }

            var nestedSemanticIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var semNode in projectSemanticNodes)
            {
                foreach (var child in semNode.Children)
                {
                    nestedSemanticIds.Add(child.Id);
                    foreach (var grandChild in child.Children)
                    {
                        nestedSemanticIds.Add(grandChild.Id);
                    }
                }
            }

            var workloadChildIds = workloadChildIdsByWorkload[workloadNode.Id];
            foreach (var semNode in projectSemanticNodes)
            {
                semanticNodes.Add(semNode);
                if (semNode is EndpointNode or ExternalServiceNode)
                {
                    if (workloadChildIds.Add(semNode.Id))
                    {
                        workloadNode.Children.Add(semNode);
                    }
                    if (existingRelKeys.Add((semNode.Id, project.Id, OntologyConstants.Relationships.DeclaredIn)))
                    {
                        semanticRelationships.Add(new Relationship(
                            semNode.Id,
                            project.Id,
                            OntologyConstants.Relationships.DeclaredIn,
                            new Dictionary<string, object>()));
                    }
                    if (semNode is ExternalServiceNode es)
                    {
                        if (existingRelKeys.Add((workloadNode.Id, es.Id, OntologyConstants.Relationships.ServiceCall)))
                        {
                            semanticRelationships.Add(new Relationship(
                                workloadNode.Id,
                                es.Id,
                                OntologyConstants.Relationships.ServiceCall,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "service_call",
                                    ["is_semantic"] = "true"
                                }));
                        }
                    }
                }
                else if (!nestedSemanticIds.Contains(semNode.Id))
                {
                    if (semanticStructureChildIds.Add(semNode.Id))
                    {
                        semanticStructureNode.Children.Add(semNode);
                    }
                }
            }

            // Group EntryPoints under the Layer 4 workload node
            GroupEntryPoints(workloadNode, project, projectTrees, semanticRelationships, existingRelKeys, ctx);

            ReportProgressIfNeeded();
        }

        SemanticTopologySynthesizer.Synthesize(
            allProjects,
            filesByProjectId,
            l3Result.Prev.ProjectDependencies,
            l3Result.Prev.Packages,
            workloadByProjectId,
            allFiles,
            semanticStructureNode,
            semanticRelationships,
            existingRelKeys,
            ctx);

        // Materialize relationships between semantic workloads (TESTS, DEPENDS_ON to library/project/package)
        foreach (var dep in l3Result.Prev.ProjectDependencies)
        {
            if (dep.Kind == OntologyConstants.Relationships.DependsOn &&
                workloadByProjectId.TryGetValue(dep.From, out var sourceWorkloadId))
            {
                if (workloadByProjectId.TryGetValue(dep.To, out var targetWorkloadId) &&
                    sourceWorkloadId != targetWorkloadId)
                {
                    var sourceWorkload = semanticStructureNode.Children.FirstOrDefault(c => c.Id == sourceWorkloadId);
                    var targetWorkload = semanticStructureNode.Children.FirstOrDefault(c => c.Id == targetWorkloadId);

                    if (sourceWorkload is TestSuiteNode)
                    {
                        if (existingRelKeys.Add((sourceWorkloadId, targetWorkloadId, OntologyConstants.Relationships.Tests)))
                        {
                            semanticRelationships.Add(new Relationship(
                                sourceWorkloadId,
                                targetWorkloadId,
                                OntologyConstants.Relationships.Tests,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = "tests",
                                    ["category"] = "test",
                                    ["is_semantic"] = "true"
                                }));
                        }
                    }
                    else
                    {
                        if (existingRelKeys.Add((sourceWorkloadId, targetWorkloadId, OntologyConstants.Relationships.DependsOn)))
                        {
                            var depType = targetWorkload is LibraryNode ? "library" : "project";
                            semanticRelationships.Add(new Relationship(
                                sourceWorkloadId,
                                targetWorkloadId,
                                OntologyConstants.Relationships.DependsOn,
                                new Dictionary<string, object>
                                {
                                    ["dependency_type"] = depType,
                                    ["category"] = depType,
                                    ["is_semantic"] = "true"
                                }));
                        }
                    }
                }
                else if (dep.To.Contains($":{OntologyConstants.IdPrefixes.Package}:") ||
                         l3Result.Prev.Packages.Any(p => p.Id == dep.To))
                {
                    if (existingRelKeys.Add((sourceWorkloadId, dep.To, OntologyConstants.Relationships.DependsOn)))
                    {
                        semanticRelationships.Add(new Relationship(
                            sourceWorkloadId,
                            dep.To,
                            OntologyConstants.Relationships.DependsOn,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "package",
                                ["category"] = "library",
                                ["is_semantic"] = "true"
                            }));
                    }
                }
            }
        }

        // Collect all semantic nodes from projects and semanticStructureNode
        foreach (var project in allProjects)
        {
            CollectSemanticNodes(project, semanticNodes);
        }
        CollectSemanticNodes(semanticStructureNode, semanticNodes);
        semanticNodes = [.. semanticNodes.DistinctBy(n => n.Id)];

        if (semanticRelationships.Count > 0)
        {
            ctx.Log($"[Layer 4    ] Uploading {semanticRelationships.Count} semantic relationships...");
            await ctx.EnqueueUploadRelationshipsAsync(semanticRelationships);
            ctx.TotalRelsCount += semanticRelationships.Count;
        }

        // 3. Upload the entire Workspace Node tree using OntologyUploader
        ctx.CancellationToken.ThrowIfCancellationRequested();
        ctx.LogDebug("[Layer 4    ] Queuing workspace ontology tree for database persistence...");
        await OntologyUploader.UploadNodeTreeAsync(l3Result.Prev.Prev.Workspace, null, ctx);

        // Free any remaining native TreeSitter memory after Layer 4
        foreach (var tree in l3Result.SyntaxTrees)
        {
            tree.Dispose();
        }

        ctx.Log($"[Layer 4    ] Semantic enrichment pass complete. Identified {semanticNodes.Count} semantic nodes.");
        return new Layer4Result(l3Result, semanticStructureNode, semanticNodes, semanticRelationships);
    }

    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects) =>
        Layer2ProjectParser.IsEnclosedInProject(file, project, projects);

    private static void CollectSemanticNodes(IOntologyNode node, List<IOntologyNode> semanticNodes)
    {
        foreach (var child in node.Children)
        {
            if (child is DatabaseNode || child is EndpointNode || child is QueryNode || child is ExternalServiceNode || child is TopicNode || child is CloudServiceNode || child is PeerMeshNode || child is ApiInUseNode || child is TableNode || child is DataSetNode || child is ServiceNode || child is AppNode || child is WorkerNode || child is LibraryNode || child is CliToolNode)
            {
                semanticNodes.Add(child);
            }
            CollectSemanticNodes(child, semanticNodes);
        }
    }

    private void GroupEntryPoints(
        IOntologyNode? workloadNode,
        ProjectNode project,
        List<SyntaxTree> projectTrees,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
        var entryPoints = new List<EntryPointNode>();
        var parentMap = new Dictionary<string, string>();

        foreach (var syntaxTree in projectTrees)
        {
            if (syntaxTree.FileNode != null)
            {
                FindAndCollectEntryPoints(syntaxTree.FileNode, entryPoints, parentMap);
            }
        }

        if (entryPoints.Count > 0)
        {
            foreach (var ep in entryPoints.DistinctBy(e => e.Id))
            {
                if (project.IsLibrary && ep.EntryType is "main-method" or "top-level" or "script-main")
                {
                    continue;
                }

                if (project.Children.All(c => c.Id != ep.Id))
                {
                    project.Children.Add(ep);
                }

                if (existingRelKeys.Add((project.Id, ep.Id, OntologyConstants.Relationships.HasEntrypoint)))
                {
                    semanticRelationships.Add(new Relationship(
                        project.Id,
                        ep.Id,
                        OntologyConstants.Relationships.HasEntrypoint,
                        new Dictionary<string, object>()));
                }

                if (existingRelKeys.Add((ep.Id, project.Id, OntologyConstants.Relationships.BelongsTo)))
                {
                    semanticRelationships.Add(new Relationship(
                        ep.Id,
                        project.Id,
                        OntologyConstants.Relationships.BelongsTo,
                        new Dictionary<string, object>()));
                }

                if (parentMap.TryGetValue(ep.Id, out var parentId) &&
                    !parentId.Contains($":{OntologyConstants.IdPrefixes.File}:", StringComparison.Ordinal))
                {
                    var implRel = new ImplementedByRelationship(ep.Id, parentId);
                    ctx.AddGlobalProjectDependency(Relationship.FromRelationship(implRel));

                    var fnImplRel = new ImplementsRelationship(parentId, ep.Id);
                    ctx.AddGlobalProjectDependency(Relationship.FromRelationship(fnImplRel));
                }
            }
        }
    }

    private void FindAndCollectEntryPoints(
        IOntologyNode node,
        List<EntryPointNode> entryPoints,
        Dictionary<string, string> parentMap)
    {
        var epsInNode = node.Children.OfType<EntryPointNode>().ToList();

        foreach (var ep in epsInNode)
        {
            entryPoints.Add(ep);
            if (!node.Id.Contains($":{OntologyConstants.IdPrefixes.File}:", StringComparison.Ordinal) ||
                !parentMap.ContainsKey(ep.Id))
            {
                parentMap[ep.Id] = node.Id;
            }
            node.Children.Remove(ep);
        }

        var childrenCopy = node.Children.ToList();

        foreach (var child in childrenCopy)
        {
            FindAndCollectEntryPoints(child, entryPoints, parentMap);
        }
    }
}
