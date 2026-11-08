using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    public static async Task<(List<Relationship> CanonicalRels, Dictionary<string, string> RawToCanonicalMap)> CanonicalizeDatabasesInMemoryAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        List<Relationship> allRelationships,
        IReadOnlyDictionary<string, ProjectNode>? prebuiltFileToProject = null)
    {
        var rawToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalDbNodes = new Dictionary<string, DatabaseNode>(StringComparer.OrdinalIgnoreCase);

        // 1. Collect all DatabaseNodes from AST / Semantic tree
        var allDbNodes = new List<DatabaseNode>();
        var visitedWalkNodes = new HashSet<IOntologyNode>(ReferenceEqualityComparer.Instance);
        void CollectDbNodes(IOntologyNode? node)
        {
            if (node == null || !visitedWalkNodes.Add(node))
                return;
            if (node is DatabaseNode db)
            {
                var eng = !string.IsNullOrWhiteSpace(db.Engine)
                    ? db.Engine
                    : db.Technology;
                if (IsLikelyDatabaseName(db.Name, eng) || IsLikelyDatabaseName(db.Id, eng))
                {
                    allDbNodes.Add(db);
                }
            }
            foreach (var child in node.Children)
            {
                CollectDbNodes(child);
            }
        }

        CollectDbNodes(l4Result.SemanticStructure);
        CollectDbNodes(l4Result.Prev.Prev.Prev.Workspace);

        foreach (var p in l4Result.Prev.Prev.Projects) CollectDbNodes(p);
        foreach (var f in l4Result.Prev.Prev.Prev.Files) CollectDbNodes(f);

        var projects = l4Result.Prev.Prev.Projects
            .OrderByDescending(p => p.Path?.Length ?? 0)
            .ThenByDescending(p => p.Id.Length)
            .ToList();
        var files = l4Result.Prev.Prev.Prev.Files;
        var fileToProject = prebuiltFileToProject != null
            ? (prebuiltFileToProject as Dictionary<string, ProjectNode> ?? new Dictionary<string, ProjectNode>(prebuiltFileToProject, StringComparer.OrdinalIgnoreCase))
            : Layer2ProjectParser.BuildFileToProjectMap(files, projects);

        var workloadToProject = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        if (l4Result.SemanticStructure != null)
        {
            foreach (var wl in l4Result.SemanticStructure.Children)
            {
                var projId = (wl as CompositeNode)?.ProjectId;
                var proj = !string.IsNullOrEmpty(projId) ? projects.FirstOrDefault(p => p.Id == projId) : null;
                if (proj != null)
                {
                    workloadToProject[wl.Id] = proj;
                }
            }
        }
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var rel in l4Result.SemanticRelationships)
            {
                if (rel.Kind == RelationshipKind.ImplementedBy)
                {
                    var proj = projects.FirstOrDefault(p => p.Id == rel.To || p.Id.TrimEnd(':') == rel.To.TrimEnd(':'));
                    if (proj != null) workloadToProject[rel.From] = proj;
                }
                else if (rel.Kind == RelationshipKind.Deploys)
                {
                    var proj = projects.FirstOrDefault(p => p.Id == rel.From || p.Id.TrimEnd(':') == rel.From.TrimEnd(':'));
                    if (proj != null) workloadToProject[rel.To] = proj;
                }
            }
        }

        var projectsById = new Dictionary<string, ProjectNode>(projects.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pr in projects)
        {
            projectsById.TryAdd(pr.Id, pr);
        }

        var testSourceCache = new Dictionary<(string Id, string Path), bool>();
        bool IsTestSource(string fromId, string? fromPath)
        {
            var key = (fromId ?? "", fromPath ?? "");
            if (testSourceCache.TryGetValue(key, out var cached))
                return cached;

            var result = ComputeIsTestSource(fromId ?? "", fromPath);
            testSourceCache[key] = result;
            return result;
        }

        bool ComputeIsTestSource(string fromId, string? fromPath)
        {
            if (!string.IsNullOrEmpty(fromPath) && WorkspaceConventions.IsTestFilePath(fromPath))
                return true;

            if (fileToProject.TryGetValue(fromId, out var p) ||
                workloadToProject.TryGetValue(fromId, out p) ||
                projectsById.TryGetValue(fromId, out p))
            {
                if (p.IsTest || string.Equals(p.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                    WorkspaceConventions.IsTestFilePath(p.Path))
                {
                    return true;
                }
            }

            if (fromId.Contains(":p:", StringComparison.OrdinalIgnoreCase) ||
                fromId.Contains(":project:", StringComparison.OrdinalIgnoreCase))
            {
                var matchingProj = projects.FirstOrDefault(pr => fromId.StartWithIC(pr.Id));
                if (matchingProj != null &&
                    (matchingProj.IsTest || string.Equals(matchingProj.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                     WorkspaceConventions.IsTestFilePath(matchingProj.Path)))
                {
                    return true;
                }
            }

            if (WorkspaceConventions.IsTestFilePath(fromId) ||
                fromId.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                fromId.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
                fromId.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == RelationshipKind.Contains)
                continue;

            var isForwardDbRel = rel.Kind == RelationshipKind.UsesDb ||
                                 rel.Kind == RelationshipKind.Configures ||
                                 rel.Kind == RelationshipKind.ReadsFrom ||
                                 rel.Kind == RelationshipKind.WritesTo ||
                                 rel.Kind == RelationshipKind.DependsOn;
            var isReverseDbRel = rel.Kind == RelationshipKind.AccessedBy ||
                                 rel.Kind == RelationshipKind.QueriedBy ||
                                 rel.Kind == RelationshipKind.DeclaredIn;
            if (!isForwardDbRel && !isReverseDbRel) continue;

            var dbTarget = isReverseDbRel ? rel.From : rel.To;
            var callerSource = isReverseDbRel ? rel.To : rel.From;

            if (!dbTarget.Contains(":db:") && !dbTarget.Contains(":database:") && !dbTarget.Contains(":res:db:"))
            {
                if (!isForwardDbRel || !IsLikelyDatabaseName(dbTarget, rel.Engine))
                    continue;
            }

            var filePath = rel.FilePath;
            if (!IsTestSource(callerSource, filePath))
            {
                nonTestAccessedTargets.Add(dbTarget);
                var parts = dbTarget.Split(':');
                var accum = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    accum = i == 0 ? parts[i] : $"{accum}:{parts[i]}";
                    nonTestAccessedTargets.Add(accum);
                }
            }
        }

        var primaryRelationalEngine = allDbNodes
            .Where(d => {
                var isDeclaredInProduction = !string.IsNullOrEmpty(d.Path) && !WorkspaceConventions.IsTestFilePath(d.Path);
                var isExplicitTest = d.IsTest == true ||
                                     string.Equals(d.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                                     WorkspaceConventions.IsTestFilePath(d.Path);
                var hasNonTest = (!isExplicitTest && isDeclaredInProduction) ||
                                 nonTestAccessedTargets.Contains(d.Id) ||
                                 d.Children.Any(c => nonTestAccessedTargets.Contains(c.Id));
                return hasNonTest && (d.DbType is CodeExplorer.Cypher.Common.DatabaseType.Unknown or CodeExplorer.Cypher.Common.DatabaseType.Relational);
            })
            .Select(d => {
                if (!string.IsNullOrWhiteSpace(d.Engine))
                    return d.Engine;
                if (!string.IsNullOrEmpty(d.Name) && d.Name.Contains('.'))
                    return d.Name.Split('.', 2)[0];
                return d.Name;
            })
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e) &&
                                 !e.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                                 !e.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                                 !ResourceReconciliationService.IsGenericConfigKey(e) &&
                                 IsRelationalEngine(e));

        var rawImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var db in allDbNodes)
        {
            var isDeclaredInProduction = !string.IsNullOrEmpty(db.Path) && !WorkspaceConventions.IsTestFilePath(db.Path);
            var isExplicitTest = db.IsTest == true ||
                                 string.Equals(db.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                                 WorkspaceConventions.IsTestFilePath(db.Path);
            var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) ||
                                   nonTestAccessedTargets.Contains(db.Id) ||
                                   db.Children.Any(c => nonTestAccessedTargets.Contains(c.Id));
            if (!hasNonTestAccess)
            {
                continue;
            }
            var rawEngine = !string.IsNullOrWhiteSpace(db.Engine)
                ? db.Engine
                : null;
            var rawSchema = !string.IsNullOrWhiteSpace(db.Schema)
                ? db.Schema
                : null;
            var (cName, cType, cKey) = CanonicalizeDatabase(db.Name, db.DbType.ToCanonicalString(), rawEngine, rawSchema, primaryRelationalEngine);
            var canonicalId = BuildCanonicalDatabaseId(db.Id, cType, cKey, ctx.WorkspaceId);
            rawToCanonical[db.Id] = canonicalId;
            nonTestAccessedTargets.Add(canonicalId);

            var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                ? rawEngine
                : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : (rawSchema ?? "");

            var isRawImplicit = db.IsImplicitSchema == true || db.Flags.HasFlag(SymbolFlags.IsImplicitSchema);
            var isRawExplicit = db.IsExplicitSchema == true || db.FromConfig == true;
            if (isRawImplicit && !isRawExplicit)
            {
                rawImplicitDbIds.Add(db.Id);
            }

            if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                (!string.IsNullOrEmpty(existingNode.Engine) && existingNode.Engine.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
            {
                var dispName = cName.Contains('.')
                    ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                    : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                        ? $"{cName} ({engineToUse}) [{cType}]"
                        : $"{cName} [{cType}]");

                var canonicalNode = new DatabaseNode(
                    canonicalId,
                    cName,
                    db.Path ?? "",
                    cType
                )
                {
                    DisplayName = dispName,
                    Role = "database",
                    Engine = engineToUse,
                    Technology = engineToUse,
                    Schema = schemaToUse,
                    IsCanonical = true,
                    IsSemanticEntity = true,
                    Layer = StandardLayers.Foundation.LayerId,
                    LayerId = StandardLayers.Foundation.LayerId,
                    LayerName = StandardLayers.Foundation.LayerName,
                    LayerColor = StandardLayers.Foundation.Color,
                    LayerIcon = StandardLayers.Foundation.Icon
                };
                if (isRawImplicit && !isRawExplicit)
                {
                    canonicalNode.IsImplicitSchema = true;
                    canonicalNode.Flags |= SymbolFlags.IsImplicitSchema;
                }
                else
                {
                    canonicalNode.IsExplicitSchema = true;
                }
                canonicalDbNodes[canonicalId] = canonicalNode;
            }

            var targetCanonical = canonicalDbNodes[canonicalId];
            if (!isRawImplicit || isRawExplicit)
            {
                targetCanonical.IsImplicitSchema = null;
                targetCanonical.Flags &= ~SymbolFlags.IsImplicitSchema;
                targetCanonical.IsExplicitSchema = true;
            }
            foreach (var child in db.Children)
            {
                var existingChild = targetCanonical.Children.FirstOrDefault(c => c.Id == child.Id);
                if (existingChild == null)
                {
                    targetCanonical.Children.Add(child);
                }
                else if (existingChild is DataSetNode existingDs && child is DataSetNode newDs)
                {
                    foreach (var grandChild in newDs.Children)
                    {
                        if (!existingDs.Children.Any(gc => gc.Id == grandChild.Id))
                        {
                            existingDs.Children.Add(grandChild);
                        }
                    }
                }
            }

            // Also map child DataSetNodes (representing schemas in SQL) to their canonical database nodes
            foreach (var ds in db.Children.OfType<DataSetNode>())
            {
                var (dsCName, dsCType, dsCKey) = CanonicalizeDatabase(db.Name, db.DbType.ToCanonicalString(), rawEngine, ds.Name, primaryRelationalEngine);
                var dsCanonicalId = BuildCanonicalDatabaseId(ds.Id, dsCType, dsCKey, ctx.WorkspaceId);
                rawToCanonical[ds.Id] = dsCanonicalId;
                if (isRawImplicit && !isRawExplicit &&
                    (string.IsNullOrEmpty(ds.Name) || ds.Name.Equals("dbo", StringComparison.OrdinalIgnoreCase) || ds.Name.Equals("default", StringComparison.OrdinalIgnoreCase)))
                {
                    rawImplicitDbIds.Add(ds.Id);
                }

                var dsEngine = dsCName.Contains('.')
                    ? dsCName.Split('.', 2)[0]
                    : (!string.IsNullOrWhiteSpace(primaryRelationalEngine) ? primaryRelationalEngine : engineToUse);

                if (!canonicalDbNodes.ContainsKey(dsCanonicalId))
                {
                    var dsNode = new DatabaseNode(
                        dsCanonicalId,
                        dsCName,
                        ds.Path ?? "",
                        dsCType
                    )
                    {
                        DisplayName = dsCName,
                        Role = "database",
                        Engine = dsEngine,
                        Technology = dsEngine,
                        Schema = ds.Name,
                        IsCanonical = true,
                        IsSemanticEntity = true,
                        Layer = StandardLayers.Foundation.LayerId,
                        LayerId = StandardLayers.Foundation.LayerId,
                        LayerName = StandardLayers.Foundation.LayerName,
                        LayerColor = StandardLayers.Foundation.Color,
                        LayerIcon = StandardLayers.Foundation.Icon
                    };
                    canonicalDbNodes[dsCanonicalId] = dsNode;
                }
            }
        }

        // Also inspect all relationships for DB targets not caught above
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == RelationshipKind.Contains)
                continue;

            var isForwardDbRel = rel.Kind == RelationshipKind.UsesDb ||
                                 rel.Kind == RelationshipKind.Configures ||
                                 rel.Kind == RelationshipKind.ReadsFrom ||
                                 rel.Kind == RelationshipKind.WritesTo ||
                                 rel.Kind == RelationshipKind.DependsOn;
            var isReverseDbRel = rel.Kind == RelationshipKind.AccessedBy ||
                                 rel.Kind == RelationshipKind.QueriedBy ||
                                 rel.Kind == RelationshipKind.DeclaredIn;
            var dbTarget = isReverseDbRel ? rel.From : rel.To;
            var callerSource = isReverseDbRel ? rel.To : rel.From;

            if (!isForwardDbRel && !isReverseDbRel && !dbTarget.Contains(":db:") && !dbTarget.Contains(":database:") && !dbTarget.Contains(":res:db:"))
                continue;

            if (!rawToCanonical.ContainsKey(dbTarget))
            {
                if (!nonTestAccessedTargets.Contains(dbTarget)) continue;
                var filePath = rel.FilePath;
                if (IsTestSource(callerSource, filePath)) continue;

                var parts = dbTarget.Split(':');
                var rawName = parts.Length > 0 ? parts[^1] : "";
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                var rawType = !string.IsNullOrEmpty(rel.DbType)
                    ? rel.DbType
                    : (rawName.Equals("redis", StringComparison.OrdinalIgnoreCase) || rawName.Equals("valkey", StringComparison.OrdinalIgnoreCase) ? "cache" : "relational");
                var rawEngine = rel.Engine;
                var rawSchema = rel.Schema;

                // STRICT VALIDATION: Only canonicalize if rawName is a genuine database!
                if (!IsLikelyDatabaseName(rawName, rawEngine))
                {
                    continue;
                }

                var (cName, cType, cKey) = CanonicalizeDatabase(rawName, rawType, rawEngine, rawSchema, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(dbTarget, cType, cKey, ctx.WorkspaceId);
                rawToCanonical[dbTarget] = canonicalId;

                var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? rawEngine
                    : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

                var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : (rawSchema ?? "");

                if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                    (!string.IsNullOrEmpty(existingNode.Engine) && existingNode.Engine.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
                {
                    var dispName = cName.Contains('.')
                        ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                        : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                            ? $"{cName} ({engineToUse}) [{cType}]"
                            : $"{cName} [{cType}]");

                    var canonicalNode = new DatabaseNode(
                        canonicalId,
                        cName,
                        "",
                        cType
                    )
                    {
                        DisplayName = dispName,
                        Role = "database",
                        Engine = engineToUse,
                        Technology = engineToUse,
                        Schema = schemaToUse,
                        IsCanonical = true,
                        IsSemanticEntity = true,
                        Layer = StandardLayers.Foundation.LayerId,
                        LayerId = StandardLayers.Foundation.LayerId,
                        LayerName = StandardLayers.Foundation.LayerName,
                        LayerColor = StandardLayers.Foundation.Color,
                        LayerIcon = StandardLayers.Foundation.Icon
                    };
                    canonicalDbNodes[canonicalId] = canonicalNode;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(primaryRelationalEngine))
        {
            var (defaultCName, defaultCType, defaultCKey) = CanonicalizeDatabase("Database", "relational", null, null, primaryRelationalEngine);
            var defaultCanonicalId = BuildCanonicalDatabaseId($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:database", defaultCType, defaultCKey, ctx.WorkspaceId);
            rawToCanonical["workspace:database:relational:database"] = defaultCanonicalId;
            rawToCanonical[$"{ctx.WorkspaceId}:database:relational:database"] = defaultCanonicalId;
            rawToCanonical[$"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Database}:relational:database"] = defaultCanonicalId;
            rawToCanonical[$"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:database"] = defaultCanonicalId;

            canonicalDbNodes.Remove("workspace:database:relational:database");
            canonicalDbNodes.Remove($"{ctx.WorkspaceId}:database:relational:database");
            canonicalDbNodes.Remove($"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Database}:relational:database");
            canonicalDbNodes.Remove($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:database");
        }

        var projectsByDomain = projects
            .GroupBy(p => {
                var d = ResourceSemanticAnalyzer.CleanProjectNameToDomain(p.Name);
                return !string.IsNullOrEmpty(d) ? d : p.Id;
            }, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        PruneUnverifiedConfigDbEdges(
            ctx,
            projects,
            files,
            fileToProject,
            allRelationships,
            allDbNodes,
            canonicalDbNodes,
            rawToCanonical,
            projectsByDomain,
            IsTestSource);

        var canonicalRels = await ConsolidateServiceDatabasesAsync(
            ctx,
            l4Result,
            projects,
            fileToProject,
            allRelationships,
            canonicalDbNodes,
            rawToCanonical,
            nonTestAccessedTargets,
            primaryRelationalEngine,
            projectsByDomain,
            IsTestSource,
            rawImplicitDbIds);

        return (canonicalRels, rawToCanonical);
    }
}
