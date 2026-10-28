using System.Text.Json;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
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
        var dbJson = await db.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        string? primaryRelationalEngine = null;
        var implicitCanonicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var explicitCanonicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                nonTestAccessedTargets.Add(canonicalId);

                var isRawImplicit = (row.TryGetProperty("is_implicit_schema", out var imp) && imp.ValueKind == JsonValueKind.String && imp.GetString() == "true");
                var isRawExplicit = (row.TryGetProperty("is_explicit_schema", out var exp) && exp.ValueKind == JsonValueKind.String && exp.GetString() == "true") ||
                                    (row.TryGetProperty("from_config", out var fc) && fc.ValueKind == JsonValueKind.String && fc.GetString() == "true");

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
        List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        HashSet<string> nonTestAccessedTargets)
    {
        var ownerConcreteEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, propsElem, isImplicitEdge) in projectDbEdges)
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

        return nodesToUpload.Count;
    }

    internal static async Task<int> UploadProjectDatabaseRelationshipsAsync(
        IGraphClient db,
        List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties, bool IsImplicitEdge)> projectDbEdges,
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

        foreach (var (owner, rawTarget, propsElem, isImplicitEdge) in projectDbEdges)
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

                relationshipsToUpload.Add(new Relationship(effectiveFrom, target,
                    OntologyConstants.Relationships.UsesDb, props));
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
    }

    internal static async Task<List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>> QueryProjectsForCanonicalizationAsync(
        IGraphClient db,
        CancellationToken cancellationToken)
    {
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
        return projectList;
    }

    internal static async Task<HashSet<string>> QueryNonTestAccessedTargetsAsync(
        IGraphClient db,
        List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)> projectList,
        CancellationToken cancellationToken)
    {
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'READS_FROM', 'WRITES_TO', 'ACCESSED_BY', 'QUERIED_BY', 'DECLARED_IN'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties, src.role AS src_role, src.path AS src_path, json_extract(src.properties, '$.is_test') AS src_is_test, tgt.role AS tgt_role, tgt.path AS tgt_path, json_extract(tgt.properties, '$.is_test') AS tgt_is_test";
        var edgesJson = await db.ExecuteQueryAsync(edgesQuery, null, cancellationToken);
        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var edgesDoc = JsonDocument.Parse(edgesJson))
        {
            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                var kind = GetStringProp(row, "kind");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                var isReverse = string.Equals(kind, "ACCESSED_BY", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(kind, "QUERIED_BY", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(kind, "DECLARED_IN", StringComparison.OrdinalIgnoreCase);

                var dbTarget = isReverse ? src : tgt;
                var callerSrc = isReverse ? tgt : src;
                var callerRole = row.TryGetProperty(isReverse ? "tgt_role" : "src_role", out var sr) && sr.ValueKind == JsonValueKind.String ? sr.GetString() : "";
                var callerPath = row.TryGetProperty(isReverse ? "tgt_path" : "src_path", out var sp) && sp.ValueKind == JsonValueKind.String ? sp.GetString() : "";
                var callerIsTest = row.TryGetProperty(isReverse ? "tgt_is_test" : "src_is_test", out var sit) && sit.ValueKind == JsonValueKind.String ? sit.GetString() : "";

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
        }
        return nonTestAccessedTargets;
    }
}
