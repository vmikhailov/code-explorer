using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    public Task<CanonicalizeDatabasesResult> CanonicalizeDatabasesAsync(string widPrefix, CancellationToken ct = default)
    {
        return CanonicalizeDatabasesAsync(db, widPrefix, ct);
    }

    public static async Task<CanonicalizeDatabasesResult> CanonicalizeDatabasesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var rawToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalNodes = new Dictionary<string, (string Name, string DbType, string Key, string Engine)>(StringComparer.OrdinalIgnoreCase);

        // 1. Query projects to identify test projects
        var projectList = await QueryProjectsForCanonicalizationAsync(db, cancellationToken);

        // 2. Query relationships to find targets accessed by non-test callers
        var nonTestAccessedTargets = await QueryNonTestAccessedTargetsAsync(db, projectList, cancellationToken);


        // 3. Query databases
        var (_, implicitSchemaIds, rawImplicitDbIds) =
            await QueryDatabasesAndBuildCanonicalNodesAsync(
                db, widPrefix, nonTestAccessedTargets, rawToCanonical, canonicalNodes, cancellationToken);
        if (canonicalNodes.Count == 0)
        {
            return new CanonicalizeDatabasesResult(0, 0);
        }

        // 4. Query relationships targeting any database and consolidate per service/project
        var relEdgesQuery =
            "SELECT from_id AS source, to_id AS target, kind, properties FROM edges WHERE kind IN ('USES_DB', 'CONFIGURES', 'DEPENDS_ON')";
        var relEdgesJson = await db.ExecuteQueryAsync(relEdgesQuery, null, cancellationToken);

        var libraryProjects = projectList.Where(p => p.IsLibrary).ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
        var serviceToReferencedLibs = new Dictionary<string, List<(string Id, string Name)>>(StringComparer.OrdinalIgnoreCase);

        var projectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties, bool IsImplicitEdge)>();

        using (var edgesDoc = JsonDocument.Parse(relEdgesJson))
        {
            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                var kind = GetStringProp(row, "kind");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                if (string.Equals(kind, "DEPENDS_ON", StringComparison.OrdinalIgnoreCase))
                {
                    if (libraryProjects.TryGetValue(tgt, out var lib))
                    {
                        if (!serviceToReferencedLibs.TryGetValue(src, out var lList))
                        {
                            lList = [];
                            serviceToReferencedLibs[src] = lList;
                        }
                        lList.Add((lib.Id, lib.Name));
                    }
                    continue;
                }

                if (rawToCanonical.TryGetValue(tgt, out var canonicalId))
                {
                    if (WorkspaceConventions.IsTestFilePath(src) ||
                        src.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                        src.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
                        src.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    // Resolve owning project
                    string? projId = null;
                    string projName = "";
                    var exactProj = projectList.FirstOrDefault(p => p.Id == src);

                    if (!string.IsNullOrEmpty(exactProj.Id))
                    {
                        if (exactProj.IsTest) continue;
                        projId = exactProj.Id;
                        projName = exactProj.Name;
                    }
                    else
                    {
                        var matched = projectList.FirstOrDefault(p =>
                            src.StartWithIC(p.Id) ||
                            (!string.IsNullOrEmpty(p.Path) &&
                             src.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));
                        if (matched.IsTest) continue;
                        projId = matched.Id;
                        projName = matched.Name;
                    }

                    if (!string.IsNullOrEmpty(projId) && projId != canonicalId)
                    {
                        row.TryGetProperty("properties", out var pElem);
                        var isRawImplicitEdge = rawImplicitDbIds.Contains(tgt) ||
                                                tgt.EndsWith(":db:default:dbo", StringComparison.OrdinalIgnoreCase) ||
                                                (pElem.ValueKind == JsonValueKind.Object && pElem.TryGetProperty("is_implicit_schema", out var impProp) && impProp.ValueKind == JsonValueKind.String && impProp.GetString() == "true");

                        if (isRawImplicitEdge && libraryProjects.ContainsKey(projId))
                        {
                            continue;
                        }

                        var clonedProps = pElem.Clone();
                        projectDbEdges.Add(((projId, projName), canonicalId, clonedProps, isRawImplicitEdge));

                        if (!isRawImplicitEdge && src.Contains(":f:", StringComparison.OrdinalIgnoreCase))
                        {
                            var normSrc = src.Replace('\\', '/');
                            var binIdx = normSrc.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase);
                            if (binIdx >= 0)
                            {
                                var afterBin = normSrc[(binIdx + 5)..];
                                var slashIdx = afterBin.IndexOf('/');
                                if (slashIdx > 0)
                                {
                                    var subBinName = afterBin[..slashIdx];
                                    var matchingBinProj = projectList.FirstOrDefault(p =>
                                        !p.IsLibrary && !p.IsTest && !string.IsNullOrEmpty(p.Id) && p.Id != projId &&
                                        string.Equals(p.Name, subBinName, StringComparison.OrdinalIgnoreCase));
                                    if (!string.IsNullOrEmpty(matchingBinProj.Id))
                                    {
                                        projectDbEdges.Add(((matchingBinProj.Id, matchingBinProj.Name), canonicalId, clonedProps, false));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Detect libraries referenced by multiple distinct service domains (e.g. IntegrationEventLogEF)
        var libReferencingDomains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (svcId, libs) in serviceToReferencedLibs)
        {
            var svcProj = projectList.FirstOrDefault(p => p.Id == svcId);
            if (svcProj.IsTest || string.IsNullOrEmpty(svcProj.Id)) continue;
            var dom = SyntaxEnricher.CleanProjectNameToDomain(svcProj.Name);
            if (string.IsNullOrEmpty(dom)) dom = svcProj.Id;
            foreach (var l in libs)
            {
                if (!libReferencingDomains.TryGetValue(l.Id, out var domSet))
                {
                    domSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    libReferencingDomains[l.Id] = domSet;
                }
                domSet.Add(dom);
            }
        }

        var multiDomainSharedLibs = libReferencingDomains
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dbOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in projectDbEdges)
        {
            if (!dbOwners.TryGetValue(edge.CanonicalTarget, out var owners))
            {
                owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                dbOwners[edge.CanonicalTarget] = owners;
            }
            owners.Add(edge.Owner.Id);
        }

        var multiDomainSharedLibDbIds = canonicalNodes.Keys
            .Where(dbId => {
                if (dbOwners.TryGetValue(dbId, out var owners) && owners.Count > 0)
                {
                    return owners.All(oId => multiDomainSharedLibs.Contains(oId));
                }
                return false;
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsGenericOrLibraryDbId(string targetId) =>
            IsGenericOrLibraryDbIdStatic(targetId, canonicalNodes, implicitSchemaIds, multiDomainSharedLibDbIds);

        var concreteEnginesCount = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isImplicit = implicitSchemaIds.Contains(id);
                var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(id);
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !isImplicit &&
                       !isMultiDomainLibDb;
            })
            .GroupBy(kvp => kvp.Value.Engine, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Filter out edges from multiDomainSharedLibs to generic databases, and propagate non-generic lib edges to services
        var filteredProjectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties, bool IsImplicitEdge)>();
        foreach (var edge in projectDbEdges)
        {
            var isMultiDomainLib = multiDomainSharedLibs.Contains(edge.Owner.Id);
            var isGenericTarget = IsGenericOrLibraryDbId(edge.CanonicalTarget) || edge.IsImplicitEdge;

            if (!isMultiDomainLib || !isGenericTarget)
            {
                filteredProjectDbEdges.Add(edge);
            }

            if (!isMultiDomainLib || !isGenericTarget)
            {
                foreach (var (svcId, libs) in serviceToReferencedLibs)
                {
                    if (libs.Any(l => l.Id == edge.Owner.Id))
                    {
                        var svcProj = projectList.FirstOrDefault(p => p.Id == svcId);
                        if (!string.IsNullOrEmpty(svcProj.Id) && !svcProj.IsTest)
                        {
                            filteredProjectDbEdges.Add(((svcProj.Id, svcProj.Name), edge.CanonicalTarget, edge.Properties, edge.IsImplicitEdge));
                        }
                    }
                }
            }
        }
        projectDbEdges = filteredProjectDbEdges;

        var edgesByService = projectDbEdges
            .GroupBy(e => {
                var domain = SyntaxEnricher.CleanProjectNameToDomain(e.Owner.Name);
                return !string.IsNullOrEmpty(domain) ? domain : e.Owner.Id;
            }, StringComparer.OrdinalIgnoreCase);

        var globalConsolidationRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainScopedRemap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sGroup in edgesByService)
        {
            var domainKey = sGroup.Key;
            var candidateNodes = sGroup
                .Select(e => canonicalNodes.TryGetValue(e.CanonicalTarget, out var info) ? (e.CanonicalTarget, info) : default)
                .Where(t => t.CanonicalTarget != null)
                .Select(t => {
                    var name = t.info.Name;
                    var engine = t.info.Engine;
                    var schema = name.Contains('.') ? name.Split('.', 2)[1] : "";
                    return (t.CanonicalTarget, name, engine, schema);
                })
                .DistinctBy(t => t.CanonicalTarget)
                .ToList();

            var localRemap = ResolveDatabaseConsolidations(candidateNodes, implicitSchemaIds);
            foreach (var (loser, winner) in localRemap)
            {
                var loserNode = canonicalNodes.TryGetValue(loser, out var lInfo) ? lInfo : default;
                var loserEng = loserNode.Engine ?? (loserNode.Name?.Contains('.') == true ? loserNode.Name.Split('.', 2)[0] : loserNode.Name ?? "");
                var loserSch = loserNode.Name?.Contains('.') == true ? loserNode.Name.Split('.', 2)[1] : "";
                var isLoserGeneric = IsGenericOrLibraryDbId(loser) || IsAbstractEngineOnlyDatabase(loserNode.Name, loserSch, loserNode.DbType, loser);

                if (isLoserGeneric)
                {
                    if (!domainScopedRemap.TryGetValue(domainKey, out var dMap))
                    {
                        dMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        domainScopedRemap[domainKey] = dMap;
                    }
                    dMap[loser] = winner;
                }
                else
                {
                    globalConsolidationRemap[loser] = winner;
                }
            }

            // Consolidate single configured database with AST schema nodes for the service
            foreach (var engGroup in candidateNodes.GroupBy(n => n.engine, StringComparer.OrdinalIgnoreCase))
            {
                var engList = engGroup.ToList();
                if (engList.Count <= 1) continue;

                var cfgNodes = engList.Where(n => {
                    // Check if node has a catalog/schema name without generic prefix
                    var s = n.schema;
                    return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
                }).ToList();

                if (cfgNodes.Count > 1)
                {
                    // Check if one has clean stem (ordering) and another has db suffix (orderingdb)
                    for (int i = 0; i < cfgNodes.Count; i++)
                    {
                        var n1 = cfgNodes[i];
                        var s1Stem = ResourceReconciliationService.StripDatabaseSuffix(n1.schema);
                        for (int j = 0; j < cfgNodes.Count; j++)
                        {
                            if (i == j) continue;
                            var n2 = cfgNodes[j];
                            var s2Stem = ResourceReconciliationService.StripDatabaseSuffix(n2.schema);
                            if (string.Equals(s1Stem, s2Stem, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!n2.schema.Equals(s2Stem, StringComparison.OrdinalIgnoreCase) && n1.schema.Equals(s1Stem, StringComparison.OrdinalIgnoreCase))
                                {
                                    globalConsolidationRemap[n2.CanonicalTarget] = n1.CanonicalTarget;
                                }
                            }
                            else if (string.Equals(s1Stem, s2Stem + "s", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(s1Stem, s2Stem + "es", StringComparison.OrdinalIgnoreCase))
                            {
                                globalConsolidationRemap[n2.CanonicalTarget] = n1.CanonicalTarget;
                            }
                            else if (string.Equals(s2Stem, s1Stem + "s", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(s2Stem, s1Stem + "es", StringComparison.OrdinalIgnoreCase))
                            {
                                globalConsolidationRemap[n1.CanonicalTarget] = n2.CanonicalTarget;
                            }
                        }
                    }
                }
            }
        }

        // Guard: globalConsolidationRemap must NEVER map an engine-only ID
        foreach (var k in globalConsolidationRemap.Keys.ToList())
        {
            var node = canonicalNodes.TryGetValue(k, out var cInfo) ? cInfo : default;
            var sch = node.Name?.Contains('.') == true ? node.Name.Split('.', 2)[1] : "";
            if (IsGenericOrLibraryDbId(k) || IsAbstractEngineOnlyDatabase(node.Name, sch, node.DbType, k))
            {
                globalConsolidationRemap.Remove(k);
            }
        }

        if (globalConsolidationRemap.Count > 0)
        {
            foreach (var (loserId, winnerId) in globalConsolidationRemap)
            {
                rawToCanonical[loserId] = winnerId;
            }

            foreach (var k in rawToCanonical.Keys.ToList())
            {
                var cur = rawToCanonical[k];
                if (globalConsolidationRemap.TryGetValue(cur, out var win))
                {
                    rawToCanonical[k] = win;
                }
            }
            foreach (var loserId in globalConsolidationRemap.Keys)
            {
                canonicalNodes.Remove(loserId);
            }
        }

        var ownerConcreteEngines = PurgeRedundantAndTestDatabases(
            canonicalNodes,
            rawToCanonical,
            projectDbEdges,
            domainScopedRemap,
            globalConsolidationRemap,
            implicitSchemaIds,
            multiDomainSharedLibDbIds,
            nonTestAccessedTargets);

        // 2. Upload canonical Database nodes (only active, consolidated nodes)
        var uploadedNodesCount = await UploadCanonicalDatabaseNodesAsync(db, canonicalNodes);

        // 3. Upload relationships targeting consolidated canonical databases
        var uploadedRelationshipsCount = await UploadProjectDatabaseRelationshipsAsync(
            db,
            projectDbEdges,
            canonicalNodes,
            ownerConcreteEngines,
            domainScopedRemap,
            globalConsolidationRemap,
            implicitSchemaIds,
            concreteEnginesCount,
            projectList,
            cancellationToken);

        // 4. Clean up duplicate / project-scoped database nodes and propagate properties
        await CleanupAndPropagateDatabaseEntitiesAsync(db, rawToCanonical, canonicalNodes, cancellationToken);

        return new CanonicalizeDatabasesResult(uploadedNodesCount, uploadedRelationshipsCount);
    }
}

