using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

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
            var cleanName = WorkspaceConventions.NormalizeServiceName(project.Name);
            if (string.IsNullOrEmpty(cleanName)) cleanName = project.Name;
            if (!cleanName.Equals(project.Name, StringComparison.OrdinalIgnoreCase) && rawProjectNames.Contains(cleanName))
            {
                cleanName = project.Name.Trim().ToLowerInvariant();
            }

            var isTestProject = project.Role == OntologyConstants.ProjectRoles.Test ||
                                project.IsTest ||
                                string.Equals(project.PrimaryRole, "TestFramework", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(project.PrimaryRole, "Test", StringComparison.OrdinalIgnoreCase);

            var canonicalPrimaryRole = isTestProject
                ? "Test"
                : (ProjectNodeFactory.NormalizeRoleName(project.PrimaryRole)
                   ?? ProjectNodeFactory.NormalizeRoleName(project.Role)
                   ?? (project.IsLibrary ? "Library" : "Service"));

            var secRolesList = new List<string>();
            if (!string.IsNullOrWhiteSpace(project.SecondaryRoles))
            {
                foreach (var item in CodeExplorer.Core.Database.CypherValue.TryParseJsonText(project.SecondaryRoles).AsStringList())
                {
                    var norm = ProjectNodeFactory.NormalizeRoleName(item);
                    if (norm != null && norm != canonicalPrimaryRole && !secRolesList.Contains(norm))
                    {
                        secRolesList.Add(norm);
                    }
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

            var secRolesJson = System.Text.Json.JsonSerializer.Serialize(secRolesList);
            var allRolesJson = System.Text.Json.JsonSerializer.Serialize(new[] { canonicalPrimaryRole }.Concat(secRolesList).Distinct().ToList());

            project.PrimaryRole = canonicalPrimaryRole;
            project.SecondaryRoles = secRolesJson;
            project.AllRoles = allRolesJson;
            project.RawName = project.Name;
            project.CleanName = cleanName;

            CompositeNode workloadNode = isTestProject
                ? new TestSuiteNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType)
                {
                    Role = project.Role,
                    IsLibrary = project.IsLibrary,
                    PrimaryRole = canonicalPrimaryRole,
                    SecondaryRoles = secRolesJson,
                    AllRoles = allRolesJson,
                    Framework = project.Framework
                }
                : canonicalPrimaryRole switch
                {
                    "App" => new AppNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.App}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType)
                    {
                        Role = project.Role,
                        IsLibrary = project.IsLibrary,
                        PrimaryRole = canonicalPrimaryRole,
                        SecondaryRoles = secRolesJson,
                        AllRoles = allRolesJson,
                        Framework = project.Framework
                    },

                    "Service" => new ServiceNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType)
                    {
                        Role = project.Role,
                        IsLibrary = project.IsLibrary,
                        PrimaryRole = canonicalPrimaryRole,
                        SecondaryRoles = secRolesJson,
                        AllRoles = allRolesJson,
                        Framework = project.Framework
                    },

                    "Worker" => new WorkerNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Worker}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType)
                    {
                        Role = project.Role,
                        IsLibrary = project.IsLibrary,
                        PrimaryRole = canonicalPrimaryRole,
                        SecondaryRoles = secRolesJson,
                        AllRoles = allRolesJson,
                        Framework = project.Framework
                    },

                    "Library" => new LibraryNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType)
                    {
                        Role = project.Role,
                        IsLibrary = project.IsLibrary,
                        PrimaryRole = canonicalPrimaryRole,
                        SecondaryRoles = secRolesJson,
                        AllRoles = allRolesJson,
                        Framework = project.Framework
                    },

                    "CliTool" => new CliToolNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CliTool}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType)
                    {
                        Role = project.Role,
                        IsLibrary = project.IsLibrary,
                        PrimaryRole = canonicalPrimaryRole,
                        SecondaryRoles = secRolesJson,
                        AllRoles = allRolesJson,
                        Framework = project.Framework
                    },

                    "Test" => new TestSuiteNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType)
                    {
                        Role = project.Role,
                        IsLibrary = project.IsLibrary,
                        PrimaryRole = canonicalPrimaryRole,
                        SecondaryRoles = secRolesJson,
                        AllRoles = allRolesJson,
                        Framework = project.Framework
                    },

                    _ => project.IsLibrary
                        ? new LibraryNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{cleanName}", cleanName, project.Path, project.ProjectType)
                        {
                            Role = project.Role,
                            IsLibrary = project.IsLibrary,
                            PrimaryRole = canonicalPrimaryRole,
                            SecondaryRoles = secRolesJson,
                            AllRoles = allRolesJson,
                            Framework = project.Framework
                        }
                        : new ServiceNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{cleanName}", cleanName, project.Path, project.ProjectType)
                        {
                            Role = project.Role,
                            IsLibrary = project.IsLibrary,
                            PrimaryRole = canonicalPrimaryRole,
                            SecondaryRoles = secRolesJson,
                            AllRoles = allRolesJson,
                            Framework = project.Framework
                        }
                };

            workloadNode.ProjectId = project.Id;
            workloadNode.RawName = project.Name;
            workloadNode.CleanName = cleanName;

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
                semanticRelationships.Add(new Relationship(workloadNode.Id, project.Id, OntologyConstants.Relationships.ImplementedBy));
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
                    var pctInt = (int)Math.Round(pct);
                    var globalPct = 52 + (int)(16.0 * projectsProcessed / Math.Max(1, totalProjects));
                    ctx.ReportProgress(globalPct, "Layer 4: Semantic", $"Enriched {projectsProcessed}/{totalProjects} projects ({pctInt}%) - {project.Name}");
                    ctx.Layer4Log.EnrichingProgress(projectsProcessed, totalProjects, pctInt, project.Name);
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
                var projectDirectory = project.TryGetSignals()?.CargoManifestDir;
                var projectAbsDir = !string.IsNullOrWhiteSpace(projectDirectory)
                    ? projectDirectory.NormalizeFullPath()
                    : Path.Combine(ctx.AbsoluteWorkspacePath, project.Path).NormalizeFullPath();
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
                             ep.IsGraphqlSdl != true)
                .Select(ep => ($"{ep.OperationType?.ToCanonicalString() ?? "Query"}:{ep.RouteTemplate.Trim('/')}"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (codeFirstGqlFields.Count > 0)
            {
                var duplicateSdlIds = projectSemanticNodes
                    .OfType<EndpointNode>()
                    .Where(ep => ep.IsGraphqlSdl == true &&
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
                project.PrimaryRole is "App" or "Worker" or "CliTool")
            {
                var primRole = project.PrimaryRole;
                var secRolesList = new List<string>();
                if (!string.IsNullOrWhiteSpace(project.SecondaryRoles))
                {
                    try
                    {
                        secRolesList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(project.SecondaryRoles) ?? [];
                    }
                    catch { }
                }
                if (!secRolesList.Contains("Service"))
                {
                    secRolesList.Add("Service");
                    var newSecJson = System.Text.Json.JsonSerializer.Serialize(secRolesList);
                    var newAllJson = System.Text.Json.JsonSerializer.Serialize(new[] { primRole }.Concat(secRolesList).Distinct().ToList());
                    project.SecondaryRoles = newSecJson;
                    project.AllRoles = newAllJson;
                    switch (workloadNode)
                    {
                        case AppNode app:
                            app.SecondaryRoles = newSecJson;
                            app.AllRoles = newAllJson;
                            break;
                        case WorkerNode wrk:
                            wrk.SecondaryRoles = newSecJson;
                            wrk.AllRoles = newAllJson;
                            break;
                        case CliToolNode cli:
                            cli.SecondaryRoles = newSecJson;
                            cli.AllRoles = newAllJson;
                            break;
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
                            OntologyConstants.Relationships.DeclaredIn));
                    }
                    if (semNode is ExternalServiceNode es)
                    {
                        if (existingRelKeys.Add((workloadNode.Id, es.Id, OntologyConstants.Relationships.ServiceCall)))
                        {
                            semanticRelationships.Add(new Relationship(
                                workloadNode.Id,
                                es.Id,
                                OntologyConstants.Relationships.ServiceCall)
                            {
                                DependencyType = "service_call",
                                IsSemantic = true
                            });
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
                                OntologyConstants.Relationships.Tests)
                            {
                                DependencyType = "tests",
                                Category = "test",
                                IsSemantic = true
                            });
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
                                OntologyConstants.Relationships.DependsOn)
                            {
                                DependencyType = depType,
                                Category = depType,
                                IsSemantic = true
                            });
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
                            OntologyConstants.Relationships.DependsOn)
                        {
                            DependencyType = "package",
                            Category = "library",
                            IsSemantic = true
                        });
                    }
                }
            }
        }
    }
}
