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
        void CollectDbNodes(IOntologyNode node)
        {
            if (node is DatabaseNode db)
            {
                var eng = (db.Extensions != null && db.Extensions.TryGetValue("engine", out var eVal) && !string.IsNullOrWhiteSpace(eVal))
                    ? eVal
                    : (db.Extensions != null && db.Extensions.TryGetValue("technology", out var tVal) ? tVal : null);
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
                var projId = wl.Extensions?.GetValueOrDefault("project_id");
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
                if (rel.Kind == OntologyConstants.Relationships.ImplementedBy)
                {
                    var proj = projects.FirstOrDefault(p => p.Id == rel.To || p.Id.TrimEnd(':') == rel.To.TrimEnd(':'));
                    if (proj != null) workloadToProject[rel.From] = proj;
                }
                else if (rel.Kind == OntologyConstants.Relationships.Deploys)
                {
                    var proj = projects.FirstOrDefault(p => p.Id == rel.From || p.Id.TrimEnd(':') == rel.From.TrimEnd(':'));
                    if (proj != null) workloadToProject[rel.To] = proj;
                }
            }
        }

        bool IsTestSource(string fromId, string? fromPath)
        {
            if (!string.IsNullOrEmpty(fromPath) && WorkspaceConventions.IsTestFilePath(fromPath))
                return true;

            if (fileToProject.TryGetValue(fromId, out var p) || workloadToProject.TryGetValue(fromId, out p))
            {
                if (p.IsTest || string.Equals(p.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                    (p.Extensions != null && p.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                    WorkspaceConventions.IsTestFilePath(p.Path))
                {
                    return true;
                }
            }

            var matchingProj = projects.FirstOrDefault(pr => pr.Id == fromId || fromId.StartWithIC(pr.Id));
            if (matchingProj != null)
            {
                if (matchingProj.IsTest || string.Equals(matchingProj.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                    (matchingProj.Extensions != null && matchingProj.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                    WorkspaceConventions.IsTestFilePath(matchingProj.Path))
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
            if (rel.Kind == OntologyConstants.Relationships.Contains)
                continue;

            var isForwardDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                                 rel.Kind == OntologyConstants.Relationships.Configures ||
                                 rel.Kind == OntologyConstants.Relationships.ReadsFrom ||
                                 rel.Kind == OntologyConstants.Relationships.WritesTo ||
                                 rel.Kind == OntologyConstants.Relationships.DependsOn;
            var isReverseDbRel = rel.Kind == OntologyConstants.Relationships.AccessedBy ||
                                 rel.Kind == OntologyConstants.Relationships.QueriedBy ||
                                 rel.Kind == OntologyConstants.Relationships.DeclaredIn;
            if (!isForwardDbRel && !isReverseDbRel) continue;

            var dbTarget = isReverseDbRel ? rel.From : rel.To;
            var callerSource = isReverseDbRel ? rel.To : rel.From;

            if (!dbTarget.Contains(":db:") && !dbTarget.Contains(":database:") && !dbTarget.Contains(":res:db:"))
                continue;

            var filePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var fp) ? fp?.ToString() : null;
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
                var isExplicitTest = (d.Extensions != null && d.Extensions.TryGetValue("is_test", out var itVal) && (itVal == "true" || itVal == "1")) ||
                                     (d.Extensions != null && d.Extensions.TryGetValue("role", out var rVal) && rVal.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(d.Path);
                var hasNonTest = (!isExplicitTest && isDeclaredInProduction) ||
                                 nonTestAccessedTargets.Contains(d.Id) ||
                                 d.Children.Any(c => nonTestAccessedTargets.Contains(c.Id));
                return hasNonTest && (string.IsNullOrEmpty(d.DbType) || d.DbType.Equals("relational", StringComparison.OrdinalIgnoreCase));
            })
            .Select(d => {
                if (d.Extensions != null && d.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                    return eng;
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
            var isExplicitTest = (db.Extensions != null && db.Extensions.TryGetValue("is_test", out var itVal) && (itVal == "true" || itVal == "1")) ||
                                 (db.Extensions != null && db.Extensions.TryGetValue("role", out var rVal) && rVal.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                 WorkspaceConventions.IsTestFilePath(db.Path);
            var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) ||
                                   nonTestAccessedTargets.Contains(db.Id) ||
                                   db.Children.Any(c => nonTestAccessedTargets.Contains(c.Id));
            if (!hasNonTestAccess)
            {
                continue;
            }
            var rawEngine = (db.Extensions != null && db.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                ? eng
                : null;
            var rawSchema = (db.Extensions != null && db.Extensions.TryGetValue("schema", out var sch) && !string.IsNullOrWhiteSpace(sch))
                ? sch
                : null;
            var (cName, cType, cKey) = CanonicalizeDatabase(db.Name, db.DbType, rawEngine, rawSchema, primaryRelationalEngine);
            var canonicalId = BuildCanonicalDatabaseId(db.Id, cType, cKey, ctx.WorkspaceId);
            rawToCanonical[db.Id] = canonicalId;
            nonTestAccessedTargets.Add(canonicalId);

            var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                ? rawEngine
                : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : (rawSchema ?? "");

            var isRawImplicit = db.Extensions != null && db.Extensions.TryGetValue("is_implicit_schema", out var imp) && imp == "true";
            var isRawExplicit = db.Extensions != null && ((db.Extensions.TryGetValue("is_explicit_schema", out var exp) && exp == "true") || (db.Extensions.TryGetValue("from_config", out var fc) && fc == "true"));
            if (isRawImplicit && !isRawExplicit)
            {
                rawImplicitDbIds.Add(db.Id);
            }

            if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                (existingNode.Extensions != null && existingNode.Extensions.TryGetValue("engine", out var exEng) && exEng.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
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
                    cType,
                    new Dictionary<string, string>
                    {
                        ["name"] = cName,
                        ["display_name"] = dispName,
                        ["db_type"] = cType,
                        ["role"] = "database",
                        ["engine"] = engineToUse,
                        ["technology"] = engineToUse,
                        ["schema"] = schemaToUse,
                        ["is_canonical"] = "true",
                        ["is_semantic_entity"] = "true",
                        ["layer"] = StandardLayers.Foundation.LayerId,
                        ["layerId"] = StandardLayers.Foundation.LayerId,
                        ["layerName"] = StandardLayers.Foundation.LayerName,
                        ["layerColor"] = StandardLayers.Foundation.Color,
                        ["layerIcon"] = StandardLayers.Foundation.Icon
                    }
                );
                if (isRawImplicit && !isRawExplicit)
                {
                    canonicalNode.Extensions ??= new Dictionary<string, string>();
                    canonicalNode.Extensions["is_implicit_schema"] = "true";
                }
                else
                {
                    canonicalNode.Extensions ??= new Dictionary<string, string>();
                    canonicalNode.Extensions["is_explicit_schema"] = "true";
                }
                canonicalDbNodes[canonicalId] = canonicalNode;
            }

            var targetCanonical = canonicalDbNodes[canonicalId];
            if ((!isRawImplicit || isRawExplicit) && targetCanonical.Extensions != null)
            {
                targetCanonical.Extensions.Remove("is_implicit_schema");
                targetCanonical.Extensions["is_explicit_schema"] = "true";
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
                var (dsCName, dsCType, dsCKey) = CanonicalizeDatabase(db.Name, db.DbType, rawEngine, ds.Name, primaryRelationalEngine);
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
                        dsCType,
                        new Dictionary<string, string>
                        {
                            ["name"] = dsCName,
                            ["display_name"] = dsCName,
                            ["db_type"] = dsCType,
                            ["role"] = "database",
                            ["engine"] = dsEngine,
                            ["technology"] = dsEngine,
                            ["schema"] = ds.Name,
                            ["is_canonical"] = "true",
                            ["is_semantic_entity"] = "true",
                            ["layer"] = StandardLayers.Foundation.LayerId,
                            ["layerId"] = StandardLayers.Foundation.LayerId,
                            ["layerName"] = StandardLayers.Foundation.LayerName,
                            ["layerColor"] = StandardLayers.Foundation.Color,
                            ["layerIcon"] = StandardLayers.Foundation.Icon
                        }
                    );
                    canonicalDbNodes[dsCanonicalId] = dsNode;
                }
            }
        }

        // Also inspect all relationships for DB targets not caught above
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains)
                continue;

            var isForwardDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                                 rel.Kind == OntologyConstants.Relationships.Configures ||
                                 rel.Kind == OntologyConstants.Relationships.ReadsFrom ||
                                 rel.Kind == OntologyConstants.Relationships.WritesTo ||
                                 rel.Kind == OntologyConstants.Relationships.DependsOn;
            var isReverseDbRel = rel.Kind == OntologyConstants.Relationships.AccessedBy ||
                                 rel.Kind == OntologyConstants.Relationships.QueriedBy ||
                                 rel.Kind == OntologyConstants.Relationships.DeclaredIn;
            var dbTarget = isReverseDbRel ? rel.From : rel.To;
            var callerSource = isReverseDbRel ? rel.To : rel.From;

            if (!isForwardDbRel && !isReverseDbRel && !dbTarget.Contains(":db:") && !dbTarget.Contains(":database:") && !dbTarget.Contains(":res:db:"))
                continue;

            if (!rawToCanonical.ContainsKey(dbTarget))
            {
                var filePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var fp) ? fp?.ToString() : null;
                if (IsTestSource(callerSource, filePath)) continue;
                if (!nonTestAccessedTargets.Contains(dbTarget)) continue;

                var parts = dbTarget.Split(':');
                var rawName = parts.Length > 0 ? parts[^1] : "";
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                var rawType = "relational";
                if (rel.Properties != null && rel.Properties.TryGetValue("db_type", out var dtObj) && dtObj != null)
                {
                    rawType = dtObj.ToString() ?? "relational";
                }
                var rawEngine = rel.Properties != null && rel.Properties.TryGetValue("engine", out var engObj) && engObj != null
                    ? engObj.ToString()
                    : null;
                var rawSchema = rel.Properties != null && rel.Properties.TryGetValue("schema", out var schObj) && schObj != null
                    ? schObj.ToString()
                    : null;

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
                    (existingNode.Extensions != null && existingNode.Extensions.TryGetValue("engine", out var exEng) && exEng.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
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
                        cType,
                        new Dictionary<string, string>
                        {
                            ["name"] = cName,
                            ["display_name"] = dispName,
                            ["db_type"] = cType,
                            ["role"] = "database",
                            ["engine"] = engineToUse,
                            ["technology"] = engineToUse,
                            ["schema"] = schemaToUse,
                            ["is_canonical"] = "true",
                            ["is_semantic_entity"] = "true",
                            ["layer"] = StandardLayers.Foundation.LayerId,
                            ["layerId"] = StandardLayers.Foundation.LayerId,
                            ["layerName"] = StandardLayers.Foundation.LayerName,
                            ["layerColor"] = StandardLayers.Foundation.Color,
                            ["layerIcon"] = StandardLayers.Foundation.Icon
                        }
                    );
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
