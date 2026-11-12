using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient
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
    private const string ColProps = "properties";
    private const string ColLbl = "lbl";
    private const string ColIsLib = "is_lib";
    private const string ColDomain = "domain";
    private const string ColInferredFromPath = "inferred_from_path";
    private const string ColIsRelativePath = "is_relative_path";
    private const string ColIsExt = "is_ext";
    private const string ColInferredFromIdent = "inferred_from_ident";
    private const string ColSrcRole = "src_role";
    private const string ColSrcPath = "src_path";
    private const string ColSrcIsTest = "src_is_test";
    private const string ColTgtRole = "tgt_role";
    private const string ColTgtPath = "tgt_path";
    private const string ColTgtIsTest = "tgt_is_test";
    private const string DefaultRelational = "relational";
    private const string DefaultDatabase = "Database";
    private const string DefaultNodeKind = "Node";

    public async Task WriteFallbackReachabilityAsync(string widPrefix, CancellationToken cancellationToken = default)
    {
        const string transitivelyCallsQuery = """
            MATCH path = (caller:Function)-[:CALLS|CALLS_ENDPOINT|USES_DB*1..15]->(sink)
            WHERE caller.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Database OR sink:Query)
            WITH caller, sink, min(length(path)) AS hops
            MERGE (caller)-[r:TRANSITIVELY_CALLS]->(sink)
            SET r.hops = hops
            """;
        const string attributedToQuery = """
            MATCH path = (ep)-[:IMPLEMENTS|IMPLEMENTED_BY|EXPOSED_BY]-(fn:Function)-[:CALLS|CALLS_ENDPOINT|USES_DB*0..15]->(sink)
            WHERE (ep:EntryPoint OR ep:Endpoint) AND ep.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Database OR sink:Query)
            WITH ep, sink, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
            MERGE (ep)-[r:ATTRIBUTED_TO]->(sink)
            SET r.hops = hops, r.sink_kind = sinkKind
            """;
        const string projectApiAnnotationsQuery = """
            MATCH (p:Project)-[:CONTAINS|EXPOSES*1..2]->(ep)-[:ATTRIBUTED_TO]->(es:ExternalService)
            WHERE (ep:EntryPoint OR ep:Endpoint) AND p.id STARTS WITH $widPrefix
            WITH p, collect(DISTINCT es.domain_or_service) AS domains
            SET p.external_apis = domains
            """;

        var parameters = new { widPrefix };
        await ExecuteWriteAsync(transitivelyCallsQuery, parameters, cancellationToken);
        await ExecuteWriteAsync(attributedToQuery, parameters, cancellationToken);
        await ExecuteWriteAsync(projectApiAnnotationsQuery, parameters, cancellationToken);
    }

    public async Task<EdgeNormalizationSnapshot> LoadEdgeNormalizationSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var nodeMap = new Dictionary<string, (NodeKind Kind, bool IsLibrary)>(StringComparer.OrdinalIgnoreCase);
        const string nodesQuery =
            "MATCH (n) WHERE n.kind IN ['Project', 'Service', 'App', 'Worker', 'Library', 'Package', 'Database', 'Topic', 'Endpoint', 'EntryPoint', 'ExternalService', 'CloudService', 'PeerMesh'] RETURN n.id AS id, labels(n) AS lbl, n.is_library AS is_lib, n.role AS role";

        var nodesRes = await ExecuteCypherAsync(nodesQuery, null, cancellationToken);
        foreach (var row in nodesRes.Rows)
        {
            var id = row.GetStringProp(ColId);
            if (string.IsNullOrEmpty(id)) continue;
            var rawKindStr = DefaultNodeKind;
            if (row.TryGetProperty(ColLbl, out var lblProp))
            {
                if (lblProp.Kind == CypherValueKind.Array && lblProp.AsArray() is { Count: > 0 } arr)
                {
                    rawKindStr = arr[0].AsString(DefaultNodeKind);
                }
                else if (lblProp.Kind == CypherValueKind.String && lblProp.AsStringList() is { Count: > 0 } strList)
                {
                    rawKindStr = strList[0];
                }
            }

            var kind = EnumExtensions.ParseNodeKind(rawKindStr);
            var isLib = kind is NodeKind.Package or NodeKind.Library || row.GetBoolProp(ColIsLib);
            var role = row.GetStringProp(ColRole);
            if (role is "SharedLibrary" or "Test") isLib = true;

            nodeMap[id] = (kind, isLib);
        }

        var edgeQuery =
            $"SELECT fn.id AS source, tn.id AS target, e.kind AS kind, e.properties FROM edges e JOIN nodes fn ON fn.rowid = e.from_id JOIN nodes tn ON tn.rowid = e.to_id WHERE e.kind IN ({GraphKindCodec.RelKindsInSql(RelationshipKind.UsesDb, RelationshipKind.Configures, RelationshipKind.DependsOn, RelationshipKind.ServiceCall, RelationshipKind.IntegratesWith, RelationshipKind.CallsEndpoint, RelationshipKind.PublishesTo, RelationshipKind.Triggers, RelationshipKind.SubscribesTo, RelationshipKind.Library)})";
        var edgesRes = await ExecuteCypherAsync(edgeQuery, null, cancellationToken);
        var edges = new List<MacroEdgeSnapshot>(edgesRes.Rows.Count);

        foreach (var row in edgesRes.Rows)
        {
            var src = row.GetStringProp(ColSource);
            var tgt = row.GetStringProp(ColTarget);
            var rawKind = row.TryGetProperty(ColKind, out var kindVal) && kindVal.Kind == CypherValueKind.Int64
                ? (RelationshipKind)(int)kindVal.AsInt64()
                : EnumExtensions.ParseRelationshipKind(row.GetStringProp(ColKind));
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            if (row.TryGetProperty(ColProps, out var pElem))
            {
                foreach (var (k, valStr) in pElem.ExtractStringMap())
                {
                    props[k] = valStr;
                }
            }

            edges.Add(new MacroEdgeSnapshot(src, tgt, rawKind, props));
        }

        return new EdgeNormalizationSnapshot(nodeMap, edges);
    }

    public async Task<int> ApplyEdgeNormalizationUpdatesAsync(
        IReadOnlyList<EdgeNormalizationUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0) return 0;

        var updatedCount = 0;
        const int updateBatchSize = 100;
        for (var i = 0; i < updates.Count; i += updateBatchSize)
        {
            var chunk = updates.Skip(i).Take(updateBatchSize).ToList();
            var sb = new System.Text.StringBuilder(chunk.Count * 160);
            var paramDict = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var j = 0; j < chunk.Count; j++)
            {
                var u = chunk[j];
                sb.AppendLine($"UPDATE OR IGNORE edges SET kind = @k{j}, properties = jsonb(@p{j}) WHERE from_id = (SELECT rowid FROM nodes WHERE id = @s{j}) AND to_id = (SELECT rowid FROM nodes WHERE id = @t{j}) AND kind = @rk{j};");
                paramDict[$"k{j}"] = (int)u.NormalizedKind;
                paramDict[$"p{j}"] = u.PropsJson;
                paramDict[$"s{j}"] = u.Source;
                paramDict[$"t{j}"] = u.Target;
                paramDict[$"rk{j}"] = (int)u.RawKind;
            }

            await ExecuteWriteAsync(sb.ToString(), paramDict, cancellationToken);
            updatedCount += chunk.Count;
        }

        return updatedCount;
    }

    public async Task<ExternalServicePurgeSnapshot> LoadExternalServicePurgeSnapshotAsync(CancellationToken cancellationToken = default)
    {
        const string projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path";
        var projsRows = await QueryAsync<(string Id, string Name, string? Path)>(projQuery, null, cancellationToken);

        const string epQuery =
            "MATCH (ep:Endpoint) RETURN ep.id AS id, ep.name AS name, json_extract(ep.properties, '$.route_template') AS route, json_extract(ep.properties, '$.http_method') AS method, json_extract(ep.properties, '$.path') AS path";
        var epRows = await QueryAsync<(string Id, string Name, string Route, string Method, string Path)>(epQuery, null, cancellationToken);
        var endpoints = epRows.Select(e => new EndpointCandidateSnapshot(e.Id, e.Name, e.Route, e.Method, e.Path)).ToList();

        const string callsEpQuery = "MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) RETURN es.id AS es_id";
        var callsEpIds = await QueryAsync<string>(callsEpQuery, null, cancellationToken);
        var servicesCallingEndpoints = new HashSet<string>(callsEpIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.OrdinalIgnoreCase);

        const string extQuery =
            "MATCH (es:ExternalService) RETURN es.id AS id, es.name AS name, json_extract(es.properties, '$.domain_or_service') AS domain, json_extract(es.properties, '$.path') AS path, json_extract(es.properties, '$.inferred_from_path') AS inferred_from_path, json_extract(es.properties, '$.is_relative_path') AS is_relative_path, json_extract(es.properties, '$.is_external') AS is_ext, json_extract(es.properties, '$.inferred_from_identifier') AS inferred_from_ident";
        var extRes = await ExecuteCypherAsync(extQuery, null, cancellationToken);
        var externalServices = new List<ExternalServiceCandidateSnapshot>(extRes.Rows.Count);

        foreach (var row in extRes.Rows)
        {
            externalServices.Add(new ExternalServiceCandidateSnapshot(
                row.GetStringProp(ColId),
                row.GetStringProp(ColName),
                row.GetStringProp(ColDomain),
                row.GetStringProp(ColPath),
                row.GetStringProp(ColInferredFromPath),
                row.GetStringProp(ColIsRelativePath),
                row.GetStringProp(ColIsExt),
                row.GetStringProp(ColInferredFromIdent)
            ));
        }

        return new ExternalServicePurgeSnapshot(projsRows, endpoints, servicesCallingEndpoints, externalServices);
    }

    public async Task<List<(string Id, string Name)>> LoadTopicsForPurgeAsync(CancellationToken cancellationToken = default)
    {
        const string topicQuery = "MATCH (t:Topic) RETURN t.id AS id, t.name AS name";
        return await QueryAsync<(string Id, string Name)>(topicQuery, null, cancellationToken);
    }

    public async Task DeleteNodesAndConnectedEdgesAsync(
        IReadOnlyList<string> nodeIds,
        CancellationToken cancellationToken = default)
    {
        if (nodeIds.Count == 0) return;

        const int deleteBatchSize = 100;
        for (var i = 0; i < nodeIds.Count; i += deleteBatchSize)
        {
            var chunk = nodeIds.Skip(i).Take(deleteBatchSize).ToList();
            var paramDict = new Dictionary<string, string>(StringComparer.Ordinal);
            var inParams = string.Join(", ", chunk.Select((id, idx) =>
            {
                var p = $"@id{idx}";
                paramDict[$"id{idx}"] = id;
                return p;
            }));

            var sql =
                $"DELETE FROM edges WHERE from_id IN (SELECT rowid FROM nodes WHERE id IN ({inParams})); DELETE FROM edges WHERE to_id IN (SELECT rowid FROM nodes WHERE id IN ({inParams})); DELETE FROM nodes WHERE id IN ({inParams});";
            await ExecuteWriteAsync(sql, paramDict, cancellationToken);
        }
    }

    public async Task DeduplicateLegacyProjectNodesAsync(
        string widPrefix = "",
        CancellationToken cancellationToken = default)
    {
        var projKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Project);
        var projSemSynKindsSql = GraphKindCodec.NodeKindsInSql(NodeKind.ProjectSemantic, NodeKind.ProjectSyntax);
        var allProjKindsSql = GraphKindCodec.NodeKindsInSql(NodeKind.Project, NodeKind.ProjectSemantic, NodeKind.ProjectSyntax);

        await using (var checkCmd = _conn.CreateCommand())
        {
            checkCmd.CommandText = $"SELECT 1 FROM nodes WHERE kind IN ({allProjKindsSql}) AND (id LIKE '%:project:%' OR id LIKE '%:project') LIMIT 1;";
            var hasLegacy = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (hasLegacy == null || hasLegacy == DBNull.Value) return;
        }

        await ExecuteWriteAsync($"""
            DELETE FROM edges WHERE from_id IN (
                SELECT p1.rowid FROM nodes p1
                WHERE p1.kind = {projKindId} AND (p1.id LIKE '%:project:%' OR p1.id LIKE '%:project')
                AND EXISTS (
                    SELECT 1 FROM nodes p2
                    WHERE p2.kind = {projKindId} AND p2.id LIKE '%:p:%'
                    AND json_extract(p2.properties, '$.path') = json_extract(p1.properties, '$.path')
                )
            );
            DELETE FROM edges WHERE to_id IN (
                SELECT p1.rowid FROM nodes p1
                WHERE p1.kind = {projKindId} AND (p1.id LIKE '%:project:%' OR p1.id LIKE '%:project')
                AND EXISTS (
                    SELECT 1 FROM nodes p2
                    WHERE p2.kind = {projKindId} AND p2.id LIKE '%:p:%'
                    AND json_extract(p2.properties, '$.path') = json_extract(p1.properties, '$.path')
                )
            );
            DELETE FROM nodes
            WHERE kind = {projKindId} AND (id LIKE '%:project:%' OR id LIKE '%:project')
            AND EXISTS (
                SELECT 1 FROM nodes p2
                WHERE p2.kind = {projKindId} AND p2.id LIKE '%:p:%'
                AND json_extract(p2.properties, '$.path') = json_extract(nodes.properties, '$.path')
            );
            DELETE FROM edges WHERE from_id IN (
                SELECT rowid FROM nodes WHERE kind IN ({projSemSynKindsSql}) AND id LIKE '%:project:%'
            );
            DELETE FROM edges WHERE to_id IN (
                SELECT rowid FROM nodes WHERE kind IN ({projSemSynKindsSql}) AND id LIKE '%:project:%'
            );
            DELETE FROM nodes WHERE kind IN ({projSemSynKindsSql}) AND id LIKE '%:project:%';
            """, null, cancellationToken);
    }

    public async Task<List<DbProjectSnapshot>> LoadProjectsForDbCanonicalizationAsync(CancellationToken cancellationToken = default)
    {
        const string projQuery =
            "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path, p.role AS role, json_extract(p.properties, '$.is_test') AS is_test, json_extract(p.properties, '$.entity_kind') AS entity_kind";
        var projsRes = await ExecuteCypherAsync(projQuery, null, cancellationToken);
        var list = new List<DbProjectSnapshot>(projsRes.Rows.Count);
        foreach (var row in projsRes.Rows)
        {
            var pId = row.GetStringProp(ColId);
            if (string.IsNullOrEmpty(pId)) continue;
            var pName = row.GetStringProp(ColName);
            var pPath = row.TryGetProperty(ColPath, out var pt) && pt.Kind == CypherValueKind.String ? pt.AsNullableString() : null;
            var pRole = row.GetStringProp(ColRole);
            var isTestProp = row.GetStringProp(ColIsTest);
            var entityKind = row.GetStringProp(ColEntityKind);
            list.Add(new DbProjectSnapshot(pId, pName, pPath, pRole, isTestProp, entityKind));
        }

        return list;
    }

    public async Task<List<DbAccessEdgeSnapshot>> LoadAccessEdgesForDbCanonicalizationAsync(CancellationToken cancellationToken = default)
    {
        const string edgesQuery =
            "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'READS_FROM', 'WRITES_TO', 'ACCESSED_BY', 'QUERIED_BY', 'DECLARED_IN'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties, src.role AS src_role, src.path AS src_path, json_extract(src.properties, '$.is_test') AS src_is_test, tgt.role AS tgt_role, tgt.path AS tgt_path, json_extract(tgt.properties, '$.is_test') AS tgt_is_test";
        var edgesRes = await ExecuteCypherAsync(edgesQuery, null, cancellationToken);
        var list = new List<DbAccessEdgeSnapshot>(edgesRes.Rows.Count);
        foreach (var row in edgesRes.Rows)
        {
            var src = row.GetStringProp(ColSource);
            var tgt = row.GetStringProp(ColTarget);
            var kind = EnumExtensions.ParseRelationshipKind(row.GetStringProp(ColKind));
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            list.Add(new DbAccessEdgeSnapshot(
                src,
                tgt,
                kind,
                row.GetStringProp(ColSrcRole),
                row.GetStringProp(ColSrcPath),
                row.GetStringProp(ColSrcIsTest),
                row.GetStringProp(ColTgtRole),
                row.GetStringProp(ColTgtPath),
                row.GetStringProp(ColTgtIsTest)
            ));
        }

        return list;
    }

    public async Task<(List<RawDatabaseNodeSnapshot> Databases, List<(string Id, string Name)> DataSets)> LoadRawDatabasesAndDataSetsAsync(
        CancellationToken cancellationToken = default)
    {
        const string dbQuery =
            "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine, d.schema AS schema, d.path AS path, json_extract(d.properties, '$.is_test') AS is_test, json_extract(d.properties, '$.is_implicit_schema') AS is_implicit_schema, json_extract(d.properties, '$.is_explicit_schema') AS is_explicit_schema, json_extract(d.properties, '$.from_config') AS from_config, d.role AS role";
        var dbRes = await ExecuteCypherAsync(dbQuery, null, cancellationToken);
        var databases = new List<RawDatabaseNodeSnapshot>(dbRes.Rows.Count);

        foreach (var row in dbRes.Rows)
        {
            var id = row.GetStringProp(ColId);
            var name = row.GetStringProp(ColName, id);
            var dbType = row.GetStringProp(ColDbType, DefaultDatabase);
            var rawEngine = row.TryGetProperty(ColEngine, out var eg) && eg.Kind == CypherValueKind.String ? eg.AsNullableString() : null;
            var rawSchema = row.TryGetProperty(ColSchema, out var sc) && sc.Kind == CypherValueKind.String ? sc.AsNullableString() : null;
            var dPath = row.TryGetProperty(ColPath, out var dp) && dp.Kind == CypherValueKind.String ? dp.AsNullableString() : null;
            var isTest = row.GetBoolProp(ColIsTest);
            var role = row.GetStringProp(ColRole);
            var isImplicit = row.GetBoolProp(ColIsImplicitSchema);
            var isExplicit = row.GetBoolProp(ColIsExplicitSchema);
            var fromConfig = row.GetBoolProp(ColFromConfig);

            databases.Add(new RawDatabaseNodeSnapshot(
                id, name, dbType, rawEngine, rawSchema, dPath, isTest, role, isImplicit, isExplicit, fromConfig));
        }

        var dataSets = new List<(string Id, string Name)>();
        try
        {
            const string dsQuery = "MATCH (ds:DataSet) RETURN ds.id AS id, ds.name AS name";
            dataSets = await QueryAsync<(string Id, string Name)>(dsQuery, null, cancellationToken);
        }
        catch
        {
            // Ignore when no DataSet nodes exist
        }

        return (databases, dataSets);
    }

    public async Task<List<Relationship>> LoadDatabaseCandidateRelationshipsAsync(CancellationToken cancellationToken = default)
    {
        var relEdgesQuery =
            $"SELECT fn.id AS from_id, tn.id AS to_id, ce_rel_kind(e.kind) AS kind, e.properties FROM edges e JOIN nodes fn ON fn.rowid = e.from_id JOIN nodes tn ON tn.rowid = e.to_id WHERE e.kind IN ({GraphKindCodec.RelKindsInSql(RelationshipKind.UsesDb, RelationshipKind.Configures, RelationshipKind.DependsOn, RelationshipKind.ReadsFrom, RelationshipKind.WritesTo, RelationshipKind.ImplementedBy, RelationshipKind.Deploys, RelationshipKind.AccessedBy, RelationshipKind.QueriedBy, RelationshipKind.DeclaredIn)})";
        return await QueryAsync<Relationship>(relEdgesQuery, null, cancellationToken);
    }

    public async Task ReplaceProjectDatabaseRelationshipsAndRemapAsync(
        IReadOnlyList<Relationship> relationshipsToUpload,
        IReadOnlyList<DomainEdgeTargetRemap> domainRemaps,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWriteAsync(
            $"DELETE FROM edges WHERE kind = {GraphKindCodec.EncodeRelKind(RelationshipKind.UsesDb)} AND from_id IN (SELECT rowid FROM nodes WHERE id LIKE '%:p:%' OR id LIKE '%:project:%' OR id LIKE '%:svc:%' OR id LIKE '%:app:%' OR id LIKE '%:wrk:%' OR id LIKE '%:lib:%');",
            null, cancellationToken);

        if (relationshipsToUpload.Count > 0)
        {
            await UploadRelationshipsAsync(relationshipsToUpload.ToList());
        }

        foreach (var remap in domainRemaps)
        {
            await ExecuteWriteAsync(
                "UPDATE OR IGNORE edges SET to_id = (SELECT rowid FROM nodes WHERE id = @winnerId) WHERE to_id = (SELECT rowid FROM nodes WHERE id = @loserId) AND from_id IN (SELECT rowid FROM nodes WHERE id LIKE @pIdPrefix);",
                new { winnerId = remap.WinnerId, loserId = remap.LoserId, pIdPrefix = remap.Prefix },
                cancellationToken);
        }
    }

    public async Task CleanupAndPropagateDatabaseEntitiesAsync(
        IReadOnlyDictionary<string, string> rawToCanonical,
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        bool propagateChildSchemaProperties,
        CancellationToken cancellationToken = default)
    {
        var dbKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Database);
        var dbSchemaKindId = GraphKindCodec.EncodeNodeKind(NodeKind.DbSchema);
        var containsRelId = GraphKindCodec.EncodeRelKind(RelationshipKind.Contains);
        var definesRelId = GraphKindCodec.EncodeRelKind(RelationshipKind.Defines);
        var tableKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Table);
        var procKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Procedure);
        var fileKindId = GraphKindCodec.EncodeNodeKind(NodeKind.File);
        var projKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Project);
        var dataSetKindId = GraphKindCodec.EncodeNodeKind(NodeKind.DataSet);
        var dbDataSetKindsSql = GraphKindCodec.NodeKindsInSql(NodeKind.Database, NodeKind.DbSchema, NodeKind.DataSet);

        foreach (var (rawId, canonicalId) in rawToCanonical)
        {
            if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
            {
                await ExecuteWriteAsync("""
                    UPDATE OR IGNORE edges SET to_id = (SELECT rowid FROM nodes WHERE id = @canonicalId) WHERE to_id = (SELECT rowid FROM nodes WHERE id = @rawId);
                    DELETE FROM edges WHERE to_id = (SELECT rowid FROM nodes WHERE id = @rawId);
                    UPDATE OR IGNORE edges SET from_id = (SELECT rowid FROM nodes WHERE id = @canonicalId) WHERE from_id = (SELECT rowid FROM nodes WHERE id = @rawId);
                    DELETE FROM edges WHERE from_id = (SELECT rowid FROM nodes WHERE id = @rawId);
                    DELETE FROM nodes WHERE id = @rawId;
                    """, new { canonicalId, rawId }, cancellationToken);
            }
        }

        await ExecuteWriteAsync(
            $"DELETE FROM edges WHERE to_id IN (SELECT rowid FROM nodes WHERE kind = {dbKindId}) AND kind = {containsRelId} AND from_id NOT IN (SELECT rowid FROM nodes WHERE id LIKE '%semantic_structure%');",
            null, cancellationToken);

        var validIds = canonicalDatabases.Select(c => c.Id).ToList();
        if (validIds.Count > 0)
        {
            var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
            var paramDict = new Dictionary<string, string>();
            for (var i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

            await ExecuteWriteAsync(
                $"DELETE FROM edges WHERE to_id IN (SELECT rowid FROM nodes WHERE kind = {dbKindId} AND id NOT IN ({placeholders})); DELETE FROM edges WHERE from_id IN (SELECT rowid FROM nodes WHERE kind = {dbKindId} AND id NOT IN ({placeholders})); DELETE FROM nodes WHERE kind = {dbKindId} AND id NOT IN ({placeholders});",
                paramDict, cancellationToken);

            await ExecuteWriteAsync(
                $"DELETE FROM edges WHERE to_id IN (SELECT rowid FROM nodes WHERE kind = {dbSchemaKindId} AND coalesce(json_extract(properties, '$.database_id'), '') NOT IN ({placeholders})); DELETE FROM edges WHERE from_id IN (SELECT rowid FROM nodes WHERE kind = {dbSchemaKindId} AND coalesce(json_extract(properties, '$.database_id'), '') NOT IN ({placeholders})); DELETE FROM nodes WHERE kind = {dbSchemaKindId} AND coalesce(json_extract(properties, '$.database_id'), '') NOT IN ({placeholders});",
                paramDict, cancellationToken);
        }
        else
        {
            await ExecuteWriteAsync(
                $"DELETE FROM edges WHERE to_id IN (SELECT rowid FROM nodes WHERE kind IN ({dbKindId}, {dbSchemaKindId})); DELETE FROM edges WHERE from_id IN (SELECT rowid FROM nodes WHERE kind IN ({dbKindId}, {dbSchemaKindId})); DELETE FROM nodes WHERE kind IN ({dbKindId}, {dbSchemaKindId});",
                null, cancellationToken);
        }

        // Reroute any direct Database -[:CONTAINS]-> Table/Procedure edges onto the Database's DbSchema node
        await ExecuteWriteAsync($"""
            INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
            SELECT sch.rowid, e.to_id, {containsRelId}, e.properties
            FROM edges e
            JOIN nodes dbn ON e.from_id = dbn.rowid AND dbn.kind = {dbKindId}
            JOIN nodes tbl ON e.to_id = tbl.rowid AND tbl.kind IN ({tableKindId}, {procKindId})
            JOIN edges es ON es.from_id = dbn.rowid AND es.kind = {containsRelId}
            JOIN nodes sch ON es.to_id = sch.rowid AND sch.kind = {dbSchemaKindId}
            WHERE e.kind = {containsRelId};

            DELETE FROM edges
            WHERE kind = {containsRelId}
              AND from_id IN (SELECT rowid FROM nodes WHERE kind = {dbKindId})
              AND to_id IN (SELECT rowid FROM nodes WHERE kind IN ({tableKindId}, {procKindId}))
              AND EXISTS (
                  SELECT 1 FROM edges es
                  JOIN nodes sch ON es.to_id = sch.rowid AND sch.kind = {dbSchemaKindId}
                  WHERE es.from_id = edges.from_id AND es.kind = {containsRelId}
              );
            """, null, cancellationToken);

        if (propagateChildSchemaProperties)
        {
            await ExecuteWriteAsync(
                $"DELETE FROM edges WHERE to_id IN (SELECT rowid FROM nodes WHERE kind = {tableKindId} AND json_extract(properties, '$.name') LIKE '% %'); DELETE FROM edges WHERE from_id IN (SELECT rowid FROM nodes WHERE kind = {tableKindId} AND json_extract(properties, '$.name') LIKE '% %'); DELETE FROM nodes WHERE kind = {tableKindId} AND json_extract(properties, '$.name') LIKE '% %';",
                null, cancellationToken);
        }

        if (canonicalDatabases.Count > 0)
        {
            await LinkOrphanTablesToCanonicalDatabasesAsync(
                canonicalDatabases, containsRelId, definesRelId, tableKindId, dbSchemaKindId, fileKindId, projKindId, dbDataSetKindsSql, cancellationToken);
        }

        if (propagateChildSchemaProperties)
        {
            await PropagateDatabaseEngineAndSchemaToChildrenAsync(
                canonicalDatabases, containsRelId, dataSetKindId, dbSchemaKindId, tableKindId, cancellationToken);
        }
    }

    private async Task LinkOrphanTablesToCanonicalDatabasesAsync(
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        int containsRelId,
        int definesRelId,
        int tableKindId,
        int dbSchemaKindId,
        int fileKindId,
        int projKindId,
        string dbDataSetKindsSql,
        CancellationToken cancellationToken)
    {
        foreach (var dbInfo in canonicalDatabases)
        {
            var cName = dbInfo.Name ?? "";
            var cleanDbName = cName.Contains('.') ? cName.Split('.', 2)[1] : cName;
            var dbKey = dbInfo.Id.Split(':').LastOrDefault() ?? cleanDbName;
            if (string.IsNullOrWhiteSpace(cleanDbName) || cleanDbName.Length < 3) continue;

            await ExecuteWriteAsync($"""
                INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                SELECT COALESCE(
                    (SELECT sch.rowid FROM edges es JOIN nodes sch ON es.to_id = sch.rowid AND sch.kind = {dbSchemaKindId} WHERE es.from_id = dbn.rowid AND es.kind = {containsRelId} LIMIT 1),
                    dbn.rowid
                ), t.rowid, {containsRelId}, jsonb(json_object())
                FROM nodes t, nodes dbn
                WHERE dbn.id = @dbId
                  AND t.kind = {tableKindId}
                  AND (
                      instr(replace(lower(coalesce(json_extract(t.properties, '$.path'), '')), '\', '/'), '/' || lower(@cleanDbName) || '/') > 0
                      OR instr(replace(lower(coalesce(json_extract(t.properties, '$.path'), '')), '\', '/'), '/' || lower(@cleanDbName) || '-') > 0
                      OR instr(replace(lower(coalesce(json_extract(t.properties, '$.path'), '')), '\', '/'), '/' || lower(@cleanDbName) || '_') > 0
                      OR instr(lower(t.id), lower(@cleanDbName)) > 0
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM edges e
                      JOIN nodes d ON e.from_id = d.rowid
                      WHERE e.to_id = t.rowid AND e.kind = {containsRelId} AND d.kind IN ({dbDataSetKindsSql})
                  );
                """,
                new { dbId = dbInfo.Id, cleanDbName, dbKey }, cancellationToken);
        }

        var primaryCanonicalId = canonicalDatabases[0].Id;
        await ExecuteWriteAsync($"""
            INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
            SELECT COALESCE(
                (SELECT sch.rowid FROM edges es JOIN nodes sch ON es.to_id = sch.rowid AND sch.kind = {dbSchemaKindId} WHERE es.from_id = dbn.rowid AND es.kind = {containsRelId} LIMIT 1),
                dbn.rowid
            ), t.rowid, {containsRelId}, jsonb(json_object())
            FROM nodes t, nodes dbn
            WHERE dbn.id = @primaryCanonicalId
              AND t.kind = {tableKindId}
              AND NOT EXISTS (
                  SELECT 1 FROM edges e
                  JOIN nodes d ON e.from_id = d.rowid
                  WHERE e.to_id = t.rowid AND e.kind = {containsRelId} AND d.kind IN ({dbDataSetKindsSql})
              );
            """,
            new { primaryCanonicalId }, cancellationToken);

        await ExecuteWriteAsync($"""
            INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
            SELECT p.rowid, t.rowid, {definesRelId}, jsonb(json_object())
            FROM nodes t
            JOIN edges e1 ON e1.to_id = t.rowid AND e1.kind = {containsRelId}
            JOIN nodes f ON e1.from_id = f.rowid AND f.kind = {fileKindId}
            JOIN edges e2 ON e2.to_id = f.rowid AND e2.kind = {containsRelId}
            JOIN nodes p ON e2.from_id = p.rowid AND p.kind = {projKindId}
            WHERE t.kind = {tableKindId}
              AND NOT EXISTS (
                SELECT 1 FROM edges ep WHERE ep.from_id = p.rowid AND ep.to_id = t.rowid AND ep.kind = {definesRelId}
            );
            """,
            null, cancellationToken);
    }

    private async Task PropagateDatabaseEngineAndSchemaToChildrenAsync(
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        int containsRelId,
        int dataSetKindId,
        int dbSchemaKindId,
        int tableKindId,
        CancellationToken cancellationToken)
    {
        foreach (var dbInfo in canonicalDatabases)
        {
            var cName = dbInfo.Name ?? "";
            var sch = cName.Contains('.') ? cName.Split('.', 2)[1] : "";
            var engineToUse = dbInfo.Engine;
            if (string.IsNullOrWhiteSpace(engineToUse)) continue;

            await ExecuteWriteAsync($"""
                UPDATE nodes
                SET properties = jsonb_set(COALESCE(NULLIF(properties, ''), jsonb(json_object())), '$.engine', @engineToUse, '$.schema', COALESCE(json_extract(properties, '$.name'), @sch))
                WHERE rowid IN (
                    SELECT e.to_id FROM edges e JOIN nodes cn ON cn.rowid = e.from_id WHERE cn.id = @canonicalId AND e.kind = {containsRelId}
                ) AND kind IN ({dataSetKindId}, {dbSchemaKindId});
                """,
                new { canonicalId = dbInfo.Id, engineToUse, sch }, cancellationToken);

            await ExecuteWriteAsync($"""
                UPDATE nodes
                SET properties = jsonb_set(COALESCE(NULLIF(properties, ''), jsonb(json_object())), '$.engine', @engineToUse, '$.schema', @sch)
                WHERE rowid IN (
                    SELECT e.to_id FROM edges e JOIN nodes cn ON cn.rowid = e.from_id WHERE cn.id = @canonicalId AND e.kind = {containsRelId}
                ) AND kind = {tableKindId};
                """,
                new { canonicalId = dbInfo.Id, engineToUse, sch }, cancellationToken);

            await ExecuteWriteAsync($"""
                UPDATE nodes
                SET properties = jsonb_set(COALESCE(NULLIF(properties, ''), jsonb(json_object())),
                    '$.engine', @engineToUse,
                    '$.schema', COALESCE((SELECT json_extract(ds.properties, '$.name') FROM nodes ds JOIN edges e2 ON ds.rowid = e2.from_id WHERE e2.to_id = nodes.rowid AND e2.kind = {containsRelId} AND ds.kind IN ({dataSetKindId}, {dbSchemaKindId}) LIMIT 1), @sch)
                )
                WHERE rowid IN (
                    SELECT e2.to_id FROM edges e1
                    JOIN nodes cn ON cn.rowid = e1.from_id
                    JOIN edges e2 ON e1.to_id = e2.from_id AND e2.kind = {containsRelId}
                    WHERE cn.id = @canonicalId AND e1.kind = {containsRelId}
                ) AND kind = {tableKindId};
                """,
                new { canonicalId = dbInfo.Id, engineToUse, sch }, cancellationToken);
        }
    }
}
