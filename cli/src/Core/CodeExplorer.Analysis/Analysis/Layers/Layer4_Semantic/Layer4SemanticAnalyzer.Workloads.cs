using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer4SemanticParser
{
    private static void InitializeWorkloadsForProjects(
        List<ProjectNode> allProjects,
        Layer3Result l3Result,
        ParsingContext ctx,
        SemanticStructureNode semanticStructureNode,
        List<IOntologyNode> semanticNodes,
        List<Relationship> semanticRelationships,
        HashSet<string> semanticStructureChildIds,
        Dictionary<string, HashSet<string>> workloadChildIdsByWorkload,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        Dictionary<string, string> workloadByProjectId,
        Dictionary<string, IOntologyNode> workloadNodeByProjectId)
    {
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

            if (!project.IsLibrary &&
                !isTestProject &&
                canonicalPrimaryRole != "Worker" &&
                canonicalPrimaryRole != "CliTool" &&
                canonicalPrimaryRole != "Service" &&
                canonicalPrimaryRole != "App")
            {
                var projectFiles = l3Result.Prev.Prev.Files.Where(f => Layer2ProjectParser.IsEnclosedInProject(f, project, allProjects)).ToList();
                if (projectFiles.Any(f => f.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                                          f.Name.EndsWith(".vue", StringComparison.OrdinalIgnoreCase) ||
                                          f.Name.EndsWith(".svelte", StringComparison.OrdinalIgnoreCase) ||
                                          f.Name.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                                          f.Name.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase)))
                {
                    if (canonicalPrimaryRole != "App")
                    {
                        secRolesList.Add(canonicalPrimaryRole);
                        canonicalPrimaryRole = "App";
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
    }

    private async Task EnrichProjectsSemanticsAsync(
        List<ProjectNode> allProjects,
        Dictionary<string, List<FileNode>> filesByProjectId,
        Dictionary<string, List<SyntaxTree>> treesByProjectId,
        Dictionary<string, IOntologyNode> workloadNodeByProjectId,
        Dictionary<string, HashSet<string>> workloadChildIdsByWorkload,
        SemanticStructureNode semanticStructureNode,
        HashSet<string> semanticStructureChildIds,
        List<IOntologyNode> semanticNodes,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        ParsingContext ctx)
    {
        ResourceSemanticAnalyzer.ResetCache();
        var lastLogTicks = Environment.TickCount64;
        var projectsProcessed = 0;
        var totalProjects = allProjects.Count;

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

            var projectFiles = filesByProjectId.GetValueOrDefault(project.Id) ?? [];

            // Find project parser(s) — fast-path via in-memory ProjectType lookup before falling back to in-memory file list
            var directParser = WorkspaceIndexer.GetProjectParser(project.ProjectType.ToCanonicalString());
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
                var filesInDir = projectFiles.Select(f => f.FullPath).ToArray();
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
                if (!ctx.LightweightAstScanMode)
                {
                    syntaxTree.Dispose();
                }
            }

            // If code-first GraphQL EndpointNodes exist in this project for a given (OperationType, RouteTemplate),
            // suppress duplicate SDL-derived EndpointNodes (e.g. from schema.graphql dumps) for the same field.
            var codeFirstGqlFields = projectSemanticNodes
                .OfType<EndpointNode>()
                .Where(ep => string.Equals(ep.Protocol, "GraphQL", StringComparison.OrdinalIgnoreCase) &&
                             (ep.Extensions == null || !ep.Extensions.TryGetValue("graphql_sdl", out var isSdl) || isSdl != "true"))
                .Select(ep => ($"{ep.OperationType?.ToCanonicalString() ?? "Query"}:{ep.RouteTemplate.Trim('/')}"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (codeFirstGqlFields.Count > 0)
            {
                var duplicateSdlIds = projectSemanticNodes
                    .OfType<EndpointNode>()
                    .Where(ep => ep.Extensions != null &&
                                 ep.Extensions.TryGetValue("graphql_sdl", out var isSdl) &&
                                 isSdl == "true" &&
                                 codeFirstGqlFields.Contains($"{ep.OperationType?.ToCanonicalString() ?? "Query"}:{ep.RouteTemplate.Trim('/')}"))
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
    }

    private static void MaterializeWorkloadDependencies(
        Layer3Result l3Result,
        Dictionary<string, string> workloadByProjectId,
        SemanticStructureNode semanticStructureNode,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        List<Relationship> semanticRelationships)
    {
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
    }
}
