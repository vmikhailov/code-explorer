using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    internal static bool IsGenericOrLibraryDbIdInMemory(
        string targetId,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        HashSet<string> multiDomainSharedLibDbIds)
    {
        if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:mysql", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:h2", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var node = canonicalDbNodes.GetValueOrDefault(targetId);
        if (node == null) return true;
        var eng = node.Engine ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[0] : node.Name);
        var sch = node.Schema ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[1] : "");
        var isImplicit = node.IsImplicitSchema == true || node.Flags.HasFlag(SymbolFlags.IsImplicitSchema);
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
                var targetEng = targetNode?.Engine ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
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
                var targetEng = targetNode?.Engine ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
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
                var s = n.Schema ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isImplicit = implicitSchemaIds.Contains(n.Id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(n.Id);
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !isImplicit &&
                       !isMultiDomainLib;
            })
            .Select(n => n.Engine ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var eng = n.Engine ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name);
                var s = n.Schema ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
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
            .Where(n => IsAbstractEngineOnlyDatabase(n.Name, n.Schema, n.DbType.ToCanonicalString(), n.Id))
            .ToList();

        foreach (var purgeNode in abstractDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any test database nodes (name/schema ends with -test, _test, or test config with 0 non-test callers)
        var testDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Schema ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isTestName = s.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 s.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                var isExplicitTest = n.IsTest == true ||
                                     string.Equals(n.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
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
            var eng = cNode.Engine ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[0] : cNode.Name);
            var sch = cNode.Schema ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[1] : "");

            cNode.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
            foreach (var child in cNode.Children)
            {
                switch (child)
                {
                    case DataSetNode dsNode:
                        dsNode.Engine = eng;
                        if (!string.IsNullOrEmpty(sch)) dsNode.Schema = sch;
                        break;
                    case TableNode tblNode:
                        tblNode.Engine = eng;
                        if (!string.IsNullOrEmpty(sch)) tblNode.Schema = sch;
                        break;
                    case ProcedureNode procNode:
                        procNode.Engine = eng;
                        if (!string.IsNullOrEmpty(sch)) procNode.Schema = sch;
                        break;
                }
                if (child is DataSetNode ds)
                {
                    ds.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
                    foreach (var tc in ds.Children)
                    {
                        var childSch = !string.IsNullOrEmpty(ds.Name) ? ds.Name : sch;
                        switch (tc)
                        {
                            case TableNode tblChild:
                                tblChild.Engine = eng;
                                tblChild.Schema = childSch;
                                break;
                            case ProcedureNode procChild:
                                procChild.Engine = eng;
                                procChild.Schema = childSch;
                                break;
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
            ctx.PostIndexLog.MaterializingCanonicalDatabaseNodes(canonicalDbNodes.Count);
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
            var targetEng = targetNode?.Engine ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
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
                var created = new Relationship(effectiveFrom, target, OntologyConstants.Relationships.UsesDb)
                {
                    DependencyType = "database",
                    IsSemantic = true,
                    IsCanonical = true
                };
                EvidenceProvenanceHelper.MergeRelationships(created, rel);
                created.DependencyType = "database";
                created.IsSemantic = true;
                created.IsCanonical = true;
                if (!string.Equals(rel.From, effectiveFrom, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(rel.To, target, StringComparison.OrdinalIgnoreCase))
                {
                    EvidenceProvenanceHelper.RecordContributingEdge(created, rel);
                }
                canonicalRels.Add(created);
            }
            else
            {
                var existing = canonicalRels.FirstOrDefault(r => r.From == effectiveFrom && r.To == target);
                if (existing != null)
                {
                    EvidenceProvenanceHelper.MergeRelationships(existing, rel);
                    if (!string.Equals(rel.From, effectiveFrom, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(rel.To, target, StringComparison.OrdinalIgnoreCase))
                    {
                        EvidenceProvenanceHelper.RecordContributingEdge(existing, rel);
                    }
                }
            }
        }

        if (ctx.DbClient != null)
        {
            if (canonicalRels.Count > 0)
            {
                ctx.PostIndexLog.MaterializingCanonicalDatabaseRelationships(canonicalRels.Count);
                ctx.AddRelsCount(canonicalRels.Count);
            }

            var remaps = new List<DomainEdgeTargetRemap>();
            foreach (var (domain, dMap) in domainScopedRemap)
            {
                var domProjs = projectsByDomain.GetValueOrDefault(domain, []);
                var domProjIds = domProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var (loserId, winnerId) in dMap)
                {
                    foreach (var pId in domProjIds)
                    {
                        remaps.Add(new DomainEdgeTargetRemap(loserId, winnerId, $"{pId}%"));
                        if (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(pId, out var wlId) && !string.IsNullOrEmpty(wlId))
                        {
                            remaps.Add(new DomainEdgeTargetRemap(loserId, winnerId, $"{wlId}%"));
                        }
                    }
                }
            }

            await ctx.DbClient.ReplaceProjectDatabaseRelationshipsAndRemapAsync(canonicalRels, remaps);
        }

        return canonicalRels;
    }

    internal static async Task CleanupDatabaseEntitiesInMemoryAsync(
        ParsingContext ctx,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, DatabaseNode> canonicalDbNodes)
    {
        if (ctx.DbClient == null) return;

        var rootDirName = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
        var orderedDbs = canonicalDbNodes
            .OrderBy(kvp =>
            {
                var dbName = kvp.Value.Name ?? "";
                var clean = dbName.Contains('.') ? dbName.Split('.', 2)[1] : dbName;
                return (!string.IsNullOrEmpty(rootDirName) && clean.Equals(rootDirName, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
            })
            .Select(kvp => new CanonicalDatabasePropagationInfo(kvp.Key, kvp.Value.Name ?? "", kvp.Value.Engine ?? ""))
            .ToList();

        await ctx.DbClient.CleanupAndPropagateDatabaseEntitiesAsync(
            rawToCanonical,
            orderedDbs,
            propagateChildSchemaProperties: false);
    }
}
