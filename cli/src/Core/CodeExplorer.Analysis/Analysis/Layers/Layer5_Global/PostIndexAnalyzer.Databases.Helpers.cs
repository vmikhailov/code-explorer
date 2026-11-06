using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    private const string ColId = "id";
    private const string ColName = "name";
    private const string ColDbType = "db_type";
    private const string ColEngine = "engine";
    private const string ColSchema = "schema";
    private const string ColPath = "path";
    private const string ColIsTest = "is_test";
    private const string ColRole = "role";
    private const string ColIsImplicitSchema = "is_implicit_schema";
    private const string ColIsExplicitSchema = "is_explicit_schema";
    private const string ColFromConfig = "from_config";
    private const string ColEntityKind = "entity_kind";
    private const string ColSource = "source";
    private const string ColTarget = "target";
    private const string ColKind = "kind";
    private const string DefaultRelational = "relational";
    private const string DefaultDatabase = "Database";

    internal static bool IsGenericOrLibraryDbIdStatic(
        string targetId,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        HashSet<string> implicitSchemaIds,
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
        if (!canonicalNodes.TryGetValue(targetId, out var info)) return true;
        var eng = info.Engine;
        var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
        var isImplicit = implicitSchemaIds.Contains(targetId);
        var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(targetId);
        return string.IsNullOrWhiteSpace(sch) ||
               sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
               isImplicit ||
               isMultiDomainLibDb ||
               sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
               info.Name.Equals(eng, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<(string? PrimaryRelationalEngine, HashSet<string> ImplicitSchemaIds, HashSet<string> RawImplicitDbIds)> QueryDatabasesAndBuildCanonicalNodesAsync(
        IGraphClient db,
        string widPrefix,
        HashSet<string> nonTestAccessedTargets,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        CancellationToken cancellationToken)
    {
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine, d.schema AS schema, d.path AS path, json_extract(d.properties, '$.is_test') AS is_test, json_extract(d.properties, '$.is_implicit_schema') AS is_implicit_schema, json_extract(d.properties, '$.is_explicit_schema') AS is_explicit_schema, json_extract(d.properties, '$.from_config') AS from_config, d.role AS role";
        var dbRes = await db.ExecuteCypherAsync(dbQuery, null, cancellationToken);
        string? primaryRelationalEngine = null;
        var implicitCanonicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var explicitCanonicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in dbRes.Rows)
        {
            var dt = row.GetStringProp(ColDbType, DefaultRelational);
            if (!string.Equals(dt, DefaultRelational, StringComparison.OrdinalIgnoreCase)) continue;

            var eng = row.GetStringProp(ColEngine);
            var nm = row.GetStringProp(ColName);
            var candidate = !string.IsNullOrWhiteSpace(eng) && !eng.Equals(DefaultDatabase, StringComparison.OrdinalIgnoreCase) && !eng.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? eng
                : (nm.Contains('.') ? nm.Split('.', 2)[0] : nm);

            if (!string.IsNullOrWhiteSpace(candidate) &&
                !candidate.Equals(DefaultDatabase, StringComparison.OrdinalIgnoreCase) &&
                !candidate.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                !ResourceReconciliationService.IsGenericConfigKey(candidate) &&
                IsRelationalEngine(candidate))
            {
                primaryRelationalEngine = candidate;
                break;
            }
        }

        foreach (var row in dbRes.Rows)
        {
            var id = row.GetStringProp(ColId);
            var name = row.GetStringProp(ColName, id);
            var rawEngine = row.TryGetProperty(ColEngine, out var eg) && eg.Kind == CypherValueKind.String ? eg.AsNullableString() : null;
            if (!IsLikelyDatabaseName(name, rawEngine) && !IsLikelyDatabaseName(id, rawEngine)) continue;

            var dPath = row.TryGetProperty(ColPath, out var dp) && dp.Kind == CypherValueKind.String ? dp.AsNullableString() : null;
            var isDeclaredInProduction = !string.IsNullOrEmpty(dPath) && !WorkspaceConventions.IsTestFilePath(dPath);
            var isExplicitTest = row.GetBoolProp(ColIsTest) ||
                                 string.Equals(row.GetStringProp(ColRole), "Test", StringComparison.OrdinalIgnoreCase) ||
                                 WorkspaceConventions.IsTestFilePath(dPath);

            var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) || nonTestAccessedTargets.Contains(id);
            if (!hasNonTestAccess)
            {
                continue;
            }

            var dbType = row.GetStringProp(ColDbType, DefaultDatabase);
            var rawSchema = row.TryGetProperty(ColSchema, out var sc) && sc.Kind == CypherValueKind.String ? sc.AsNullableString() : null;

            var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType, rawEngine, rawSchema, primaryRelationalEngine);
            var canonicalId = BuildCanonicalDatabaseId(id, cType, cKey, widPrefix);
            rawToCanonical[id] = canonicalId;
            nonTestAccessedTargets.Add(canonicalId);

            var isRawImplicit = row.GetBoolProp(ColIsImplicitSchema);
            var isRawExplicit = row.GetBoolProp(ColIsExplicitSchema) || row.GetBoolProp(ColFromConfig);

            if (isRawImplicit && !isRawExplicit)
            {
                rawImplicitDbIds.Add(id);
            }

            if (isRawExplicit || (!isRawImplicit && !string.IsNullOrEmpty(rawSchema)))
            {
                explicitCanonicalIds.Add(canonicalId);
                implicitCanonicalIds.Remove(canonicalId);
            }
            else if (isRawImplicit && !explicitCanonicalIds.Contains(canonicalId))
            {
                implicitCanonicalIds.Add(canonicalId);
            }

            var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                ? rawEngine
                : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

            if (!canonicalNodes.TryGetValue(canonicalId, out var existing) ||
                (existing.Engine.Equals(existing.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
            {
                canonicalNodes[canonicalId] = (cName, cType, cKey, engineToUse);
            }
        }

        // Also map any dataset (schema) nodes
        try
        {
            var dsQuery = "MATCH (ds:DataSet) RETURN ds.id AS id, ds.name AS name";
            var dsRows = await db.QueryAsync<(string Id, string Name)>(dsQuery, null, cancellationToken);

            foreach (var (dsId, dsName) in dsRows)
            {
                if (string.IsNullOrEmpty(dsId) || string.IsNullOrEmpty(dsName)) continue;
                if (!nonTestAccessedTargets.Contains(dsId)) continue;

                var targetEngine = !string.IsNullOrWhiteSpace(primaryRelationalEngine) ? primaryRelationalEngine : "PostgreSQL";
                var (cName, cType, cKey) = CanonicalizeDatabase(targetEngine, "relational", null, dsName, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(dsId, cType, cKey, widPrefix);
                rawToCanonical[dsId] = canonicalId;

                var isImplicitDs = dsId.Contains(":db:default:dbo", StringComparison.OrdinalIgnoreCase) ||
                                   dsName.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
                                   dsName.Equals("default", StringComparison.OrdinalIgnoreCase);
                if (isImplicitDs)
                {
                    rawImplicitDbIds.Add(dsId);
                    if (!explicitCanonicalIds.Contains(canonicalId))
                    {
                        implicitCanonicalIds.Add(canonicalId);
                    }
                }
                else
                {
                    explicitCanonicalIds.Add(canonicalId);
                    implicitCanonicalIds.Remove(canonicalId);
                }

                if (!canonicalNodes.ContainsKey(canonicalId))
                {
                    canonicalNodes[canonicalId] = (cName, cType, cKey, targetEngine);
                }
            }
        }
        catch { }

        return (primaryRelationalEngine, implicitCanonicalIds, rawImplicitDbIds);
    }

    internal static Dictionary<string, HashSet<string>> PurgeRedundantAndTestDatabases(
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        Dictionary<string, string> rawToCanonical,
        List<((string Id, string Name) Owner, string CanonicalTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        HashSet<string> nonTestAccessedTargets)
    {
        var ownerConcreteEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, _, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (!IsGenericOrLibraryDbIdStatic(target, canonicalNodes, implicitSchemaIds, multiDomainSharedLibDbIds) && !implicitSchemaIds.Contains(target) && !isImplicitEdge)
            {
                if (canonicalNodes.TryGetValue(target, out var tInfo))
                {
                    var targetEng = tInfo.Engine ?? (tInfo.Name.Contains('.') ? tInfo.Name.Split('.', 2)[0] : tInfo.Name);
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
        }

        var activelyNeededImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, propsElem, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (implicitSchemaIds.Contains(target) || isImplicitEdge)
            {
                if (canonicalNodes.TryGetValue(target, out var tInfo))
                {
                    var targetEng = tInfo.Engine ?? (tInfo.Name.Contains('.') ? tInfo.Name.Split('.', 2)[0] : tInfo.Name);
                    var hasConcrete = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);
                    if (!hasConcrete)
                    {
                        activelyNeededImplicitDbIds.Add(target);
                    }
                }
            }
        }

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalNodes
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
            .Select(kvp => kvp.Value.Engine)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isImplicit = implicitSchemaIds.Contains(id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(id);
                var isEngineOnlyOrLibrary = string.IsNullOrWhiteSpace(s) ||
                                           s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                           isMultiDomainLib ||
                                           info.Name.Equals(info.Engine, StringComparison.OrdinalIgnoreCase);

                if (isEngineOnlyOrLibrary && concreteEngines.Contains(info.Engine)) return true;
                if (isImplicit && concreteEngines.Contains(info.Engine) && !activelyNeededImplicitDbIds.Contains(id)) return true;
                return false;
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

        return ownerConcreteEngines;
    }

    internal static async Task<int> UploadCanonicalDatabaseNodesAsync(
        IGraphClient db,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes)
    {
        var nodesToUpload = new List<Node>();
        foreach (var (canonicalId, (cName, cType, _, engineToUse)) in canonicalNodes)
        {
            var dispName = cName.Contains('.')
                ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                    ? $"{cName} ({engineToUse}) [{cType}]"
                    : $"{cName} [{cType}]");

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : "";

            var dbNode = new DatabaseNode(canonicalId, cName, string.Empty, EnumExtensions.ParseDatabaseType(cType))
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
            nodesToUpload.Add(Node.FromNode(dbNode));
        }

        if (nodesToUpload.Count > 0)
        {
            await db.UploadNodesAsync(nodesToUpload);
        }

        return nodesToUpload.Count;
    }

    internal static async Task<int> UploadProjectDatabaseRelationshipsAsync(
        IGraphClient db,
        List<((string Id, string Name) Owner, string CanonicalTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        Dictionary<string, HashSet<string>> ownerConcreteEngines,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        Dictionary<string, int> concreteEnginesCount,
        List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)> projectList,
        CancellationToken cancellationToken,
        Dictionary<string, string>? projectToWorkloadMap = null)
    {
        var relationshipsToUpload = new List<Relationship>();
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

            if (!canonicalNodes.ContainsKey(target))
            {
                continue;
            }

            var targetNode = canonicalNodes.TryGetValue(target, out var tInfo) ? tInfo : default;
            var targetEng = targetNode.Engine ?? (targetNode.Name?.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode.Name ?? "");
            var isImplicit = implicitSchemaIds.Contains(target) || isImplicitEdge;
            var hasConcreteForEngine = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);

            if (isImplicit && hasConcreteForEngine)
            {
                continue;
            }
            if (IsGenericOrLibraryDbIdStatic(target, canonicalNodes, implicitSchemaIds, new HashSet<string>()) && !isImplicit && concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
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

                relationshipsToUpload.Add(created);
            }
        }

        await db.ExecuteWriteAsync(
            "DELETE FROM edges WHERE kind = 'USES_DB' AND (from_id LIKE '%:p:%' OR from_id LIKE '%:project:%' OR from_id LIKE '%:svc:%' OR from_id LIKE '%:app:%' OR from_id LIKE '%:wrk:%' OR from_id LIKE '%:lib:%');",
            null, cancellationToken);

        if (relationshipsToUpload.Count > 0)
        {
            await db.UploadRelationshipsAsync(relationshipsToUpload);
        }

        // Apply domain-scoped remappings to edges
        foreach (var (domain, dMap) in domainScopedRemap)
        {
            var domProjIds = projectList.Where(p => {
                var d = ResourceSemanticAnalyzer.CleanProjectNameToDomain(p.Name);
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
                    if (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(pId, out var wlId) && !string.IsNullOrEmpty(wlId))
                    {
                        await db.ExecuteWriteAsync(
                            "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @wlIdPrefix;",
                            new { winnerId, loserId, wlIdPrefix = $"{wlId}%" }, cancellationToken);
                    }
                }
            }
        }

        return relationshipsToUpload.Count;
    }

    internal static async Task CleanupAndPropagateDatabaseEntitiesAsync(
        IGraphClient db,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        CancellationToken cancellationToken)
    {
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
            var paramDict = new Dictionary<string, string>();
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

        // Link orphaned DDL tables (e.g. created via .sql schema files without explicit CREATE DATABASE) to canonical database
        if (canonicalNodes.Count > 0)
        {
            // 1. Link each Table to the matching canonical Database based on path/project/name
            foreach (var (dbId, tuple) in canonicalNodes)
            {
                var cName = tuple.Name;
                var cleanDbName = cName.Contains('.') ? cName.Split('.', 2)[1] : cName;
                var dbKey = dbId.Split(':').LastOrDefault() ?? cleanDbName;
                if (string.IsNullOrWhiteSpace(cleanDbName) || cleanDbName.Length < 3) continue;

                await db.ExecuteWriteAsync(@"
                    INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                    SELECT @dbId, t.id, 'CONTAINS', '{}'
                    FROM nodes t
                    WHERE t.kind = 'Table'
                      AND (
                          instr(replace(lower(coalesce(json_extract(t.properties, '$.path'), '')), '\', '/'), '/' || lower(@cleanDbName) || '/') > 0
                          OR instr(replace(lower(coalesce(json_extract(t.properties, '$.path'), '')), '\', '/'), '/' || lower(@cleanDbName) || '-') > 0
                          OR instr(replace(lower(coalesce(json_extract(t.properties, '$.path'), '')), '\', '/'), '/' || lower(@cleanDbName) || '_') > 0
                          OR instr(lower(t.id), lower(@cleanDbName)) > 0
                      )
                      AND NOT EXISTS (
                          SELECT 1 FROM edges e
                          JOIN nodes d ON e.from_id = d.id
                          WHERE e.to_id = t.id AND e.kind = 'CONTAINS' AND d.kind IN ('Database', 'DataSet')
                      );",
                    new { dbId, cleanDbName, dbKey }, cancellationToken);
            }

            var primaryCanonicalId = canonicalNodes.Keys.First();
            await db.ExecuteWriteAsync(@"
                INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                SELECT @primaryCanonicalId, t.id, 'CONTAINS', '{}'
                FROM nodes t
                WHERE t.kind = 'Table'
                  AND NOT EXISTS (
                      SELECT 1 FROM edges e
                      JOIN nodes d ON e.from_id = d.id
                      WHERE e.to_id = t.id AND e.kind = 'CONTAINS' AND d.kind IN ('Database', 'DataSet')
                  );",
                new { primaryCanonicalId }, cancellationToken);

            // Link owning Project to Table via DEFINES
            await db.ExecuteWriteAsync(@"
                INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                SELECT p.id, t.id, 'DEFINES', '{}'
                FROM nodes t
                JOIN edges e1 ON e1.to_id = t.id AND e1.kind = 'CONTAINS'
                JOIN nodes f ON e1.from_id = f.id AND f.kind = 'File'
                JOIN edges e2 ON e2.to_id = f.id AND e2.kind = 'CONTAINS'
                JOIN nodes p ON e2.from_id = p.id AND p.kind = 'Project'
                WHERE t.kind = 'Table'
                  AND NOT EXISTS (
                    SELECT 1 FROM edges ep WHERE ep.from_id = p.id AND ep.to_id = t.id AND ep.kind = 'DEFINES'
                );",
                null, cancellationToken);
        }

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
    }

    internal static async Task<List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>> QueryProjectsForCanonicalizationAsync(
        IGraphClient db,
        CancellationToken cancellationToken)
    {
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path, p.role AS role, json_extract(p.properties, '$.is_test') AS is_test, json_extract(p.properties, '$.entity_kind') AS entity_kind";
        var projsRes = await db.ExecuteCypherAsync(projQuery, null, cancellationToken);
        var projectList = new List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>();
        foreach (var row in projsRes.Rows)
        {
            var pId = row.GetStringProp(ColId);
            var pName = row.GetStringProp(ColName);
            var pPath = row.TryGetProperty(ColPath, out var pt) && pt.Kind == CypherValueKind.String ? pt.AsNullableString() : null;
            var pRole = row.GetStringProp(ColRole);
            var isTestProp = row.GetStringProp(ColIsTest);
            var entityKind = row.GetStringProp(ColEntityKind);

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
        return projectList;
    }

    internal static async Task<HashSet<string>> QueryNonTestAccessedTargetsAsync(
        IGraphClient db,
        List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)> projectList,
        CancellationToken cancellationToken)
    {
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'READS_FROM', 'WRITES_TO', 'ACCESSED_BY', 'QUERIED_BY', 'DECLARED_IN'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties, src.role AS src_role, src.path AS src_path, json_extract(src.properties, '$.is_test') AS src_is_test, tgt.role AS tgt_role, tgt.path AS tgt_path, json_extract(tgt.properties, '$.is_test') AS tgt_is_test";
        var edgesRes = await db.ExecuteCypherAsync(edgesQuery, null, cancellationToken);
        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in edgesRes.Rows)
        {
            var src = row.GetStringProp(ColSource);
            var tgt = row.GetStringProp(ColTarget);
            var kind = row.GetStringProp(ColKind);
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            var isReverse = string.Equals(kind, "ACCESSED_BY", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(kind, "QUERIED_BY", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(kind, "DECLARED_IN", StringComparison.OrdinalIgnoreCase);

            var dbTarget = isReverse ? src : tgt;
            var callerSrc = isReverse ? tgt : src;
            var callerRole = row.GetStringProp(isReverse ? "tgt_role" : "src_role");
            var callerPath = row.GetStringProp(isReverse ? "tgt_path" : "src_path");
            var callerIsTest = row.GetStringProp(isReverse ? "tgt_is_test" : "src_is_test");

            var matchingProj = projectList.FirstOrDefault(p => p.Id == callerSrc || callerSrc.StartWithIC(p.Id) || (!string.IsNullOrEmpty(p.Path) && callerSrc.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));
            var isTestSrc = string.Equals(callerRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(callerRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(callerIsTest, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(callerIsTest, "1", StringComparison.OrdinalIgnoreCase) ||
                            WorkspaceConventions.IsTestFilePath(callerPath) ||
                            WorkspaceConventions.IsTestFilePath(callerSrc) ||
                            (matchingProj != default && matchingProj.IsTest);

            if (!isTestSrc)
            {
                nonTestAccessedTargets.Add(dbTarget);
            }
        }
        return nonTestAccessedTargets;
    }
}
