using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;

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
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path, p.role AS role, json_extract(p.properties, '$.is_test') AS is_test, json_extract(p.properties, '$.entity_kind') AS entity_kind";
        var projsJson = await db.ExecuteQueryAsync(projQuery, null, cancellationToken);
        var projectList = new List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>();
        using (var projsDoc = JsonDocument.Parse(projsJson))
        {
            foreach (var row in projsDoc.RootElement.EnumerateArray())
            {
                var pId = GetStringProp(row, "id");
                var pName = GetStringProp(row, "name");
                var pPath = row.TryGetProperty("path", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
                var pRole = row.TryGetProperty("role", out var rl) && rl.ValueKind == JsonValueKind.String ? rl.GetString() : "";
                var isTestProp = row.TryGetProperty("is_test", out var it) && it.ValueKind == JsonValueKind.String ? it.GetString() : "";
                var entityKind = row.TryGetProperty("entity_kind", out var ek) && ek.ValueKind == JsonValueKind.String ? ek.GetString() : "";

                var isTest = string.Equals(pRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(pRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(isTestProp, "true", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(isTestProp, "1", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(entityKind, "Test", StringComparison.OrdinalIgnoreCase) ||
                             WorkspaceConventions.IsTestFilePath(pPath) ||
                             (!string.IsNullOrEmpty(pId) && (pId.Contains("/tests/", StringComparison.OrdinalIgnoreCase) || pId.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) || pId.Contains(".Tests", StringComparison.OrdinalIgnoreCase)));

                var isLibrary = string.Equals(pRole, "Library", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(pRole, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(entityKind, "Library", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(pPath) && (pPath.Contains("/Infrastructure/", StringComparison.OrdinalIgnoreCase) || pPath.Contains("\\Infrastructure\\", StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrEmpty(pId))
                {
                    projectList.Add((pId, pName, pPath, isTest, isLibrary));
                }
            }
        }

        // 2. Query relationships to find targets accessed by non-test callers
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'READS_FROM', 'WRITES_TO'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties, src.role AS src_role, src.path AS src_path, json_extract(src.properties, '$.is_test') AS src_is_test";
        var edgesJson = await db.ExecuteQueryAsync(edgesQuery, null, cancellationToken);
        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var edgesDoc = JsonDocument.Parse(edgesJson))
        {
            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                var srcRole = row.TryGetProperty("src_role", out var sr) && sr.ValueKind == JsonValueKind.String ? sr.GetString() : "";
                var srcPath = row.TryGetProperty("src_path", out var sp) && sp.ValueKind == JsonValueKind.String ? sp.GetString() : "";
                var srcIsTest = row.TryGetProperty("src_is_test", out var sit) && sit.ValueKind == JsonValueKind.String ? sit.GetString() : "";

                var matchingProj = projectList.FirstOrDefault(p => p.Id == src || src.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(p.Path) && src.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));

                var isTest = (!string.IsNullOrEmpty(matchingProj.Id) && matchingProj.IsTest) ||
                             string.Equals(srcRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(srcRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(srcIsTest, "true", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(srcIsTest, "1", StringComparison.OrdinalIgnoreCase) ||
                             WorkspaceConventions.IsTestFilePath(srcPath) ||
                             WorkspaceConventions.IsTestFilePath(src) ||
                             src.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                             src.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
                             src.Contains(".Tests", StringComparison.OrdinalIgnoreCase);

                if (!isTest)
                {
                    nonTestAccessedTargets.Add(tgt);
                    var parts = tgt.Split(':');
                    var accum = "";
                    for (int i = 0; i < parts.Length; i++)
                    {
                        accum = i == 0 ? parts[i] : $"{accum}:{parts[i]}";
                        nonTestAccessedTargets.Add(accum);
                    }
                }
            }
        }

        // 3. Query databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine, d.schema AS schema, d.path AS path, json_extract(d.properties, '$.is_test') AS is_test, d.role AS role";
        var dbJson = await db.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        string? primaryRelationalEngine = null;
        using (var dbDoc = JsonDocument.Parse(dbJson))
        {
            foreach (var row in dbDoc.RootElement.EnumerateArray())
            {
                var dt = row.TryGetProperty("db_type", out var dtElem) && dtElem.ValueKind == JsonValueKind.String ? dtElem.GetString() : "relational";
                if (!string.Equals(dt, "relational", StringComparison.OrdinalIgnoreCase)) continue;

                var eng = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;
                var nm = GetStringProp(row, "name");
                var candidate = !string.IsNullOrWhiteSpace(eng) && !eng.Equals("Database", StringComparison.OrdinalIgnoreCase) && !eng.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? eng
                    : (nm.Contains('.') ? nm.Split('.', 2)[0] : nm);

                if (!string.IsNullOrWhiteSpace(candidate) &&
                    !candidate.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                    !candidate.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                    !ResourceReconciliationService.IsGenericConfigKey(candidate) &&
                    IsRelationalEngine(candidate))
                {
                    primaryRelationalEngine = candidate;
                    break;
                }
            }

            foreach (var row in dbDoc.RootElement.EnumerateArray())
            {
                var id = GetStringProp(row, "id");
                var name = GetStringProp(row, "name", id);
                var rawEngine = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;
                if (!IsLikelyDatabaseName(name, rawEngine) && !IsLikelyDatabaseName(id, rawEngine)) continue;

                var dPath = row.TryGetProperty("path", out var dp) && dp.ValueKind == JsonValueKind.String ? dp.GetString() : null;
                var isDeclaredInProduction = !string.IsNullOrEmpty(dPath) && !WorkspaceConventions.IsTestFilePath(dPath);
                var isExplicitTest = (row.TryGetProperty("is_test", out var dit) && dit.ValueKind == JsonValueKind.String && (dit.GetString() == "true" || dit.GetString() == "1")) ||
                                     (row.TryGetProperty("role", out var drl) && drl.ValueKind == JsonValueKind.String && string.Equals(drl.GetString(), "Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(dPath);

                var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) || nonTestAccessedTargets.Contains(id);
                if (!hasNonTestAccess)
                {
                    continue;
                }

                var dbType = row.TryGetProperty("db_type", out var dt) && dt.ValueKind == JsonValueKind.String ? (dt.GetString() ?? "Database") : "Database";
                var rawSchema = row.TryGetProperty("schema", out var sc) && sc.ValueKind == JsonValueKind.String ? sc.GetString() : null;

                var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType, rawEngine, rawSchema, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(id, cType, cKey, widPrefix);
                rawToCanonical[id] = canonicalId;

                var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? rawEngine
                    : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

                if (!canonicalNodes.TryGetValue(canonicalId, out var existing) ||
                    (existing.Engine.Equals(existing.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
                {
                    canonicalNodes[canonicalId] = (cName, cType, cKey, engineToUse);
                }
            }
        }

        // Also map any dataset (schema) nodes
        try
        {
            var dsQuery = "MATCH (ds:DataSet) RETURN ds.id AS id, ds.name AS name";
            var dsJson = await db.ExecuteQueryAsync(dsQuery, null, cancellationToken);
            using var dsDoc = JsonDocument.Parse(dsJson);

            foreach (var row in dsDoc.RootElement.EnumerateArray())
            {
                var dsId = GetStringProp(row, "id");
                var dsName = GetStringProp(row, "name");
                if (string.IsNullOrEmpty(dsId) || string.IsNullOrEmpty(dsName)) continue;
                if (!nonTestAccessedTargets.Contains(dsId)) continue;

                var targetEngine = !string.IsNullOrWhiteSpace(primaryRelationalEngine) ? primaryRelationalEngine : "PostgreSQL";
                var (cName, cType, cKey) = CanonicalizeDatabase(targetEngine, "relational", null, dsName, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(dsId, cType, cKey, widPrefix);
                rawToCanonical[dsId] = canonicalId;

                if (!canonicalNodes.ContainsKey(canonicalId))
                {
                    canonicalNodes[canonicalId] = (cName, cType, cKey, targetEngine);
                }
            }
        }
        catch { }

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

        var projectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties)>();

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
                            src.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(p.Path) &&
                             src.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));
                        if (matched.IsTest) continue;
                        projId = matched.Id;
                        projName = matched.Name;
                    }

                    if (!string.IsNullOrEmpty(projId) && projId != canonicalId)
                    {
                        row.TryGetProperty("properties", out var pElem);
                        projectDbEdges.Add(((projId, projName), canonicalId, pElem.Clone()));
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

        bool IsGenericOrLibraryDbId(string targetId)
        {
            if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!canonicalNodes.TryGetValue(targetId, out var info)) return true;
            var eng = info.Engine;
            var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
            return string.IsNullOrWhiteSpace(sch) ||
                   sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   info.Name.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   info.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
        }

        var concreteEnginesCount = canonicalNodes.Values
            .Where(info => {
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase);
            })
            .GroupBy(info => info.Engine, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Filter out edges from multiDomainSharedLibs to generic databases, and propagate non-generic lib edges to services
        var filteredProjectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties)>();
        foreach (var edge in projectDbEdges)
        {
            var isMultiDomainLib = multiDomainSharedLibs.Contains(edge.Owner.Id);
            var isGenericTarget = IsGenericOrLibraryDbId(edge.CanonicalTarget);

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
                            filteredProjectDbEdges.Add(((svcProj.Id, svcProj.Name), edge.CanonicalTarget, edge.Properties));
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

            var localRemap = ResolveDatabaseConsolidations(candidateNodes);
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

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalNodes.Values
            .Where(info => {
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
            })
            .Select(info => info.Engine)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isGenericOrLibrary = string.IsNullOrWhiteSpace(s) ||
                                         s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                         s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                                         info.Name.Equals(info.Engine, StringComparison.OrdinalIgnoreCase) ||
                                         info.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
                return isGenericOrLibrary && concreteEngines.Contains(info.Engine);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in genericOrLibraryDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return IsAbstractEngineOnlyDatabase(info.Name, sch, info.DbType, id);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in abstractDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // Remove any test database nodes
        var testDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isTestName = sch.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 sch.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 info.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 info.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                return isTestName && !nonTestAccessedTargets.Contains(id);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in testDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // 2. Upload canonical Database nodes (only active, consolidated nodes)
        var nodesToUpload = new List<Node>();
        foreach (var (canonicalId, (cName, cType, cKey, engineToUse)) in canonicalNodes)
        {
            var dispName = cName.Contains('.')
                ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                    ? $"{cName} ({engineToUse}) [{cType}]"
                    : $"{cName} [{cType}]");

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : "";

            var props = new Dictionary<string, object>
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
            };
            nodesToUpload.Add(new Node(canonicalId, "Database", props));
        }

        if (nodesToUpload.Count > 0)
        {
            await db.UploadNodesAsync(nodesToUpload);
        }

        // 3. Upload relationships targeting consolidated canonical databases
        var relationshipsToUpload = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (owner, rawTarget, propsElem) in projectDbEdges)
        {
            var domain = SyntaxEnricher.CleanProjectNameToDomain(owner.Name);
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

            if (!canonicalNodes.ContainsKey(target))
            {
                continue;
            }

            var targetNode = canonicalNodes.TryGetValue(target, out var tInfo) ? tInfo : default;
            var targetEng = targetNode.Engine ?? (targetNode.Name?.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode.Name ?? "");
            if (IsGenericOrLibraryDbId(target) && concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
            {
                continue;
            }

            if (owner.Id != target && seen.Add((owner.Id, target)))
            {
                var props = new Dictionary<string, object>
                {
                    ["dependency_type"] = "database",
                    ["is_semantic"] = "true",
                    ["is_canonical"] = "true"
                };

                if (propsElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var jp in propsElem.EnumerateObject())
                    {
                        if (!props.ContainsKey(jp.Name))
                        {
                            props[jp.Name] = jp.Value.ToString();
                        }
                    }
                }

                relationshipsToUpload.Add(new Relationship(owner.Id, target,
                    OntologyConstants.Relationships.UsesDb, props));
            }
        }

        if (relationshipsToUpload.Count > 0)
        {
            await db.UploadRelationshipsAsync(relationshipsToUpload);
        }

        // Apply domain-scoped remappings to edges
        foreach (var (domain, dMap) in domainScopedRemap)
        {
            var domProjIds = projectList.Where(p => {
                var d = SyntaxEnricher.CleanProjectNameToDomain(p.Name);
                if (string.IsNullOrEmpty(d)) d = p.Id;
                return d.Equals(domain, StringComparison.OrdinalIgnoreCase);
            }).Select(p => p.Id).ToList();

            foreach (var (loserId, winnerId) in dMap)
            {
                foreach (var pId in domProjIds)
                {
                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @pIdPrefix;",
                        new { winnerId, loserId, pIdPrefix = $"{pId}%" }, cancellationToken);
                }
            }
        }

            // 5. Clean up duplicate / project-scoped database nodes where id != canonicalId
            foreach (var (rawId, canonicalId) in rawToCanonical)
            {
                if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
                {
                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;",
                        new { canonicalId, rawId }, cancellationToken);

                    await db.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId },
                        cancellationToken);

                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;",
                        new { canonicalId, rawId }, cancellationToken);

                    await db.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId },
                        cancellationToken);

                    await db.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId },
                        cancellationToken);
                }
            }

            // Remove any improper CONTAINS edges pointing to Database nodes (Databases are infrastructure, only contained in semantic_structure)
            await db.ExecuteWriteAsync(
                "DELETE FROM edges WHERE kind = 'CONTAINS' AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database') AND from_id NOT LIKE '%semantic_structure%';",
                null, cancellationToken);

            // Purge any remaining phantom Database nodes that were not canonicalized
            var validIds = canonicalNodes.Keys.ToList();

            if (validIds.Count > 0)
            {
                var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
                var paramDict = new Dictionary<string, object>();
                for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

                await db.ExecuteWriteAsync(
                    $"DELETE FROM edges WHERE (to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database')) OR (from_id NOT IN ({placeholders}) AND from_id IN (SELECT id FROM nodes WHERE kind = 'Database'));",
                    paramDict, cancellationToken);

                await db.ExecuteWriteAsync(
                    $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});", paramDict,
                    cancellationToken);
            }
            else
            {
                await db.ExecuteWriteAsync("DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Database') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Database');", null, cancellationToken);
                await db.ExecuteWriteAsync("DELETE FROM nodes WHERE kind = 'Database';", null, cancellationToken);
            }

            // Purge any invalid Table nodes (e.g. containing spaces like 'ordering api')
            await db.ExecuteWriteAsync(
                "DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Table' AND json_extract(properties, '$.name') LIKE '% %') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Table' AND json_extract(properties, '$.name') LIKE '% %');",
                null, cancellationToken);
            await db.ExecuteWriteAsync(
                "DELETE FROM nodes WHERE kind = 'Table' AND json_extract(properties, '$.name') LIKE '% %';",
                null, cancellationToken);

            // Propagate canonical engine and schema to child DataSets and Tables
            foreach (var (canonicalId, (cName, _, _, engineToUse)) in canonicalNodes)
            {
                var sch = cName.Contains('.') ? cName.Split('.', 2)[1] : "";
                if (string.IsNullOrWhiteSpace(engineToUse)) continue;

                await db.ExecuteWriteAsync(@"
                    UPDATE nodes 
                    SET properties = json_set(COALESCE(NULLIF(properties, ''), '{}'), '$.engine', @engineToUse, '$.schema', COALESCE(json_extract(properties, '$.name'), @sch))
                    WHERE id IN (
                        SELECT to_id FROM edges WHERE from_id = @canonicalId AND kind = 'CONTAINS'
                    ) AND kind = 'DataSet';",
                    new { canonicalId, engineToUse, sch }, cancellationToken);

                await db.ExecuteWriteAsync(@"
                    UPDATE nodes 
                    SET properties = json_set(COALESCE(NULLIF(properties, ''), '{}'), '$.engine', @engineToUse, '$.schema', @sch)
                    WHERE id IN (
                        SELECT to_id FROM edges WHERE from_id = @canonicalId AND kind = 'CONTAINS'
                    ) AND kind = 'Table';",
                    new { canonicalId, engineToUse, sch }, cancellationToken);

                await db.ExecuteWriteAsync(@"
                    UPDATE nodes 
                    SET properties = json_set(COALESCE(NULLIF(properties, ''), '{}'), 
                        '$.engine', @engineToUse, 
                        '$.schema', COALESCE((SELECT json_extract(ds.properties, '$.name') FROM nodes ds JOIN edges e2 ON ds.id = e2.from_id WHERE e2.to_id = nodes.id AND e2.kind = 'CONTAINS' LIMIT 1), @sch)
                    )
                    WHERE id IN (
                        SELECT e2.to_id FROM edges e1
                        JOIN edges e2 ON e1.to_id = e2.from_id AND e2.kind = 'CONTAINS'
                        WHERE e1.from_id = @canonicalId AND e1.kind = 'CONTAINS'
                    ) AND kind = 'Table';",
                    new { canonicalId, engineToUse, sch }, cancellationToken);
            }

            return new CanonicalizeDatabasesResult(nodesToUpload.Count, relationshipsToUpload.Count);
        }
    }
