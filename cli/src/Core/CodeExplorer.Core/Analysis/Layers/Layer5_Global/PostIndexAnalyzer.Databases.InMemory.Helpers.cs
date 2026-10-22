using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    internal static bool IsGenericOrLibraryDbIdInMemory(
        string targetId,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        HashSet<string> multiDomainSharedLibDbIds)
    {
        if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var node = canonicalDbNodes.GetValueOrDefault(targetId);
        if (node == null) return true;
        var eng = node.Extensions?.GetValueOrDefault("engine") ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[0] : node.Name);
        var sch = node.Extensions?.GetValueOrDefault("schema") ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[1] : "");
        var isImplicit = node.Extensions?.GetValueOrDefault("is_implicit_schema") == "true";
        var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(targetId);
        return string.IsNullOrWhiteSpace(sch) ||
               sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
               isImplicit ||
               isMultiDomainLibDb ||
               sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
               node.Name.Equals(eng, StringComparison.OrdinalIgnoreCase);
    }

    internal static Dictionary<string, HashSet<string>> PurgeRedundantAndTestDatabasesInMemory(
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, string> rawToCanonical,
        List<(ProjectNode Owner, string RawTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        HashSet<string> nonTestAccessedTargets)
    {
        var ownerConcreteEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, rel, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (!IsGenericOrLibraryDbIdInMemory(target, canonicalDbNodes, multiDomainSharedLibDbIds) && !implicitSchemaIds.Contains(target) && !isImplicitEdge)
            {
                var targetNode = canonicalDbNodes.GetValueOrDefault(target);
                var targetEng = targetNode?.Extensions?.GetValueOrDefault("engine") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
                if (!string.IsNullOrEmpty(targetEng))
                {
                    if (!ownerConcreteEngines.TryGetValue(owner.Id, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        ownerConcreteEngines[owner.Id] = set;
                    }
                    set.Add(targetEng);
                }
            }
        }

        var activelyNeededImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, rel, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (implicitSchemaIds.Contains(target) || isImplicitEdge || multiDomainSharedLibDbIds.Contains(target))
            {
                var targetNode = canonicalDbNodes.GetValueOrDefault(target);
                var targetEng = targetNode?.Extensions?.GetValueOrDefault("engine") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
                var hasConcrete = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);
                if (!hasConcrete && !owner.IsLibrary)
                {
                    activelyNeededImplicitDbIds.Add(target);
                }
            }
        }

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isImplicit = implicitSchemaIds.Contains(n.Id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(n.Id);
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !isImplicit &&
                       !isMultiDomainLib;
            })
            .Select(n => n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var eng = n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name);
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isImplicit = implicitSchemaIds.Contains(n.Id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(n.Id);
                var isEngineOnly = string.IsNullOrWhiteSpace(s) ||
                                   s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                   n.Name.Equals(eng, StringComparison.OrdinalIgnoreCase);

                if (isEngineOnly && concreteEngines.Contains(eng) && !activelyNeededImplicitDbIds.Contains(n.Id)) return true;
                if (isMultiDomainLib && concreteEngines.Contains(eng) && !activelyNeededImplicitDbIds.Contains(n.Id)) return true;
                if (isImplicit && concreteEngines.Contains(eng) && !activelyNeededImplicitDbIds.Contains(n.Id)) return true;
                return false;
            })
            .ToList();

        foreach (var purgeNode in genericOrLibraryDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalDbNodes.Values
            .Where(n => IsAbstractEngineOnlyDatabase(n.Name, n.Extensions?.GetValueOrDefault("schema"), n.DbType, n.Id))
            .ToList();

        foreach (var purgeNode in abstractDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any test database nodes (name/schema ends with -test, _test, or test config with 0 non-test callers)
        var testDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isTestName = s.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 s.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                var isExplicitTest = (n.Extensions != null && n.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                                     (n.Extensions != null && n.Extensions.TryGetValue("role", out var r) && r.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(n.Path);
                return (isTestName || isExplicitTest) && !nonTestAccessedTargets.Contains(n.Id);
            })
            .ToList();

        foreach (var purgeNode in testDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        return ownerConcreteEngines;
    }

    internal static async Task PropagateSchemaAndUploadNodesInMemoryAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        string? primaryRelationalEngine)
    {
        foreach (var cNode in canonicalDbNodes.Values)
        {
            var eng = cNode.Extensions?.GetValueOrDefault("engine") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[0] : cNode.Name);
            var sch = cNode.Extensions?.GetValueOrDefault("schema") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[1] : "");

            cNode.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
            foreach (var child in cNode.Children)
            {
                if (child.Extensions != null)
                {
                    child.Extensions["engine"] = eng;
                    if (!string.IsNullOrEmpty(sch)) child.Extensions["schema"] = sch;
                }
                if (child is DataSetNode ds)
                {
                    ds.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
                    foreach (var tc in ds.Children)
                    {
                        if (tc.Extensions != null)
                        {
                            tc.Extensions["engine"] = eng;
                            tc.Extensions["schema"] = !string.IsNullOrEmpty(ds.Name) ? ds.Name : sch;
                        }
                    }
                }
            }
        }

        // 3. Add canonical nodes to SemanticStructure and upload
        var semanticStructure = ctx.SemanticStructure ?? l4Result.SemanticStructure;
        if (semanticStructure != null)
        {
            if (!string.IsNullOrWhiteSpace(primaryRelationalEngine))
            {
                semanticStructure.Children.RemoveAll(c => c is DatabaseNode dn &&
                    (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith($":{OntologyConstants.IdPrefixes.Database}:relational:database", StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var node in canonicalDbNodes.Values)
            {
                if (!semanticStructure.Children.Any(c => c.Id == node.Id))
                {
                    semanticStructure.Children.Add(node);
                }
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, node.Name, node.Id);
            }
        }

        if (canonicalDbNodes.Count > 0 && ctx.DbClient != null)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalDbNodes.Count} canonical Database nodes into graph...");
            await ctx.DbClient.UploadNodesAsync([.. canonicalDbNodes.Values.Select(Node.FromNode)]);
            ctx.AddNodesCount(canonicalDbNodes.Count);
        }
    }

    internal static async Task<List<Relationship>> UploadCanonicalRelationshipsInMemoryAsync(
        ParsingContext ctx,
        List<(ProjectNode Owner, string RawTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, HashSet<string>> ownerConcreteEngines,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        Dictionary<string, int> concreteEnginesCount,
        Dictionary<string, List<ProjectNode>> projectsByDomain,
        Dictionary<string, string>? projectToWorkloadMap = null)
    {
        var canonicalRels = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (owner, rawTarget, rel, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;

            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
            {
                target = dWin;
            }
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
            {
                target = win;
            }

            var targetNode = canonicalDbNodes.GetValueOrDefault(target);
            var targetEng = targetNode?.Extensions?.GetValueOrDefault("engine") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
            var isImplicit = implicitSchemaIds.Contains(target) || isImplicitEdge;
            var hasConcreteForEngine = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);

            if (isImplicit && hasConcreteForEngine)
            {
                continue;
            }
            if (multiDomainSharedLibDbIds.Contains(target) && hasConcreteForEngine)
            {
                continue;
            }
            if (!multiDomainSharedLibDbIds.Contains(target) &&
                IsGenericOrLibraryDbIdInMemory(target, canonicalDbNodes, multiDomainSharedLibDbIds) &&
                !isImplicit &&
                concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
            {
                continue;
            }

            var effectiveFrom = (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(owner.Id, out var semFrom) && !string.IsNullOrEmpty(semFrom))
                ? semFrom
                : owner.Id;

            if (effectiveFrom != target && seen.Add((effectiveFrom, target)))
            {
                var props = new Dictionary<string, object>
                {
                    ["dependency_type"] = "database",
                    ["is_semantic"] = "true",
                    ["is_canonical"] = "true"
                };
                if (rel.Properties != null)
                {
                    foreach (var (k, v) in rel.Properties)
                    {
                        if (v != null && !props.ContainsKey(k))
                        {
                            props[k] = v;
                        }
                    }
                }
                if (!string.Equals(rel.From, effectiveFrom, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(rel.To, target, StringComparison.OrdinalIgnoreCase))
                {
                    EvidenceProvenanceHelper.RecordContributingEdge(props, rel);
                }
                canonicalRels.Add(new Relationship(
                    effectiveFrom,
                    target,
                    OntologyConstants.Relationships.UsesDb,
                    props
                ));
            }
            else if (rel.Properties != null)
            {
                var existing = canonicalRels.FirstOrDefault(r => r.From == effectiveFrom && r.To == target);
                if (existing?.Properties != null)
                {
                    EvidenceProvenanceHelper.MergeProvenance(existing.Properties, rel.Properties);
                    if (!string.Equals(rel.From, effectiveFrom, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(rel.To, target, StringComparison.OrdinalIgnoreCase))
                    {
                        EvidenceProvenanceHelper.RecordContributingEdge(existing.Properties, rel);
                    }
                    foreach (var (k, v) in rel.Properties)
                    {
                        if (v != null && !existing.Properties.ContainsKey(k))
                        {
                            existing.Properties[k] = v;
                        }
                    }
                }
            }
        }

        if (ctx.DbClient != null)
        {
            await ctx.DbClient.ExecuteWriteAsync(
                "DELETE FROM edges WHERE kind = 'USES_DB' AND (from_id LIKE '%:p:%' OR from_id LIKE '%:project:%' OR from_id LIKE '%:svc:%' OR from_id LIKE '%:app:%' OR from_id LIKE '%:wrk:%' OR from_id LIKE '%:lib:%');");
        }

        if (canonicalRels.Count > 0 && ctx.DbClient != null)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalRels.Count} direct workload->canonical database relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(canonicalRels);
            ctx.AddRelsCount(canonicalRels.Count);
        }

        if (ctx.DbClient != null)
        {
            // Apply domain-scoped remappings to edges
            foreach (var (domain, dMap) in domainScopedRemap)
            {
                var domProjs = projectsByDomain.GetValueOrDefault(domain, []);
                var domProjIds = domProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var (loserId, winnerId) in dMap)
                {
                    foreach (var pId in domProjIds)
                    {
                        await ctx.DbClient.ExecuteWriteAsync(
                            "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @pIdPrefix;",
                            new { winnerId, loserId, pIdPrefix = $"{pId}%" });
                        if (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(pId, out var wlId) && !string.IsNullOrEmpty(wlId))
                        {
                            await ctx.DbClient.ExecuteWriteAsync(
                                "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @wlIdPrefix;",
                                new { winnerId, loserId, wlIdPrefix = $"{wlId}%" });
                        }
                    }
                }
            }
        }

        return canonicalRels;
    }

    internal static async Task CleanupDatabaseEntitiesInMemoryAsync(
        ParsingContext ctx,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, DatabaseNode> canonicalDbNodes)
    {
        // Clean up duplicate non-canonical database nodes
        foreach (var (rawId, canonicalId) in rawToCanonical)
        {
            if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
            {
                await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;", new { canonicalId, rawId });
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId });
                await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;", new { canonicalId, rawId });
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId });
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId });
            }
        }

        // Remove any improper CONTAINS edges pointing to Database nodes (Databases are infrastructure, only contained in semantic_structure)
        await ctx.DbClient.ExecuteWriteAsync(
            "DELETE FROM edges WHERE kind = 'CONTAINS' AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database') AND from_id NOT LIKE '%semantic_structure%';");

        // Purge any remaining phantom Database nodes that were not canonicalized
        var validIds = canonicalDbNodes.Keys.ToList();
        if (validIds.Count > 0)
        {
            var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
            var paramDict = new Dictionary<string, object>();
            for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

            await ctx.DbClient.ExecuteWriteAsync(
                $"DELETE FROM edges WHERE (to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database')) OR (from_id NOT IN ({placeholders}) AND from_id IN (SELECT id FROM nodes WHERE kind = 'Database'));",
                paramDict);
            await ctx.DbClient.ExecuteWriteAsync(
                $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});",
                paramDict);
        }
        else
        {
            await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Database') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Database');");
            await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE kind = 'Database';");
        }
    }
}
