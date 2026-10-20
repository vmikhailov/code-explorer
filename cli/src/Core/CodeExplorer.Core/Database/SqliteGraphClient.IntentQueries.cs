using System.Text.Json;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient
{
    public async Task<List<IntentCandidate>> LoadIntentCandidatesAsync(
        string workspaceId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = limit;
        if (!effectiveLimit.HasValue && int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_MAX_FILES"), out var envLimit) && envLimit > 0)
        {
            effectiveLimit = envLimit;
        }

        var limitClause = effectiveLimit.HasValue ? "LIMIT @limit" : "";
        var results = new List<IntentCandidate>();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            if (effectiveLimit.HasValue)
            {
                cmd.Parameters.AddWithValue("@limit", effectiveLimit.Value);
            }

            // 1. Architectural candidates (classes/interfaces/files)
            cmd.CommandText = $"""
                               SELECT id, kind,
                                      COALESCE(json_extract(properties, '$.name'), '') AS name,
                                      COALESCE(json_extract(properties, '$.path'), json_extract(properties, '$.file_path'), '') AS rel_path,
                                      COALESCE(json_extract(properties, '$.full_path'), '') AS full_path
                               FROM nodes
                               WHERE kind IN ('Type', 'File')
                                 AND (
                                     name LIKE '%Service%' OR name LIKE '%Controller%' OR name LIKE '%Repository%'
                                     OR name LIKE '%Handler%' OR name LIKE '%Consumer%' OR name LIKE '%Manager%'
                                     OR name LIKE '%Producer%' OR name LIKE '%Listener%' OR name LIKE '%Worker%'
                                     OR name LIKE '%Job%' OR name LIKE '%Processor%' OR name LIKE '%Store%'
                                     OR name LIKE '%Command%' OR name LIKE '%Query%' OR name LIKE '%Entity%'
                                     OR name LIKE '%DTO%' OR name LIKE '%Model%' OR name LIKE '%Aggregate%'
                                     OR name LIKE '%Policy%' OR name LIKE '%Factory%'
                                     OR rel_path LIKE '%.service.%' OR rel_path LIKE '%.controller.%'
                                     OR rel_path LIKE '%.repository.%' OR rel_path LIKE '%.handler.%'
                                     OR rel_path LIKE '%.consumer.%' OR rel_path LIKE '%.manager.%'
                                     OR rel_path LIKE '%/services/%' OR rel_path LIKE '%/controllers/%'
                                     OR rel_path LIKE '%/repositories/%' OR rel_path LIKE '%/handlers/%'
                                     OR rel_path LIKE '%/consumers/%' OR rel_path LIKE '%/domain/%'
                                     OR rel_path LIKE '%/entities/%' OR rel_path LIKE '%/commands/%'
                                     OR rel_path LIKE '%/queries/%'
                                 )
                                 AND rel_path NOT LIKE '%.spec.%'
                                 AND rel_path NOT LIKE '%.test.%'
                                 AND rel_path NOT LIKE '%/tests/%'
                                 AND rel_path NOT LIKE '%/test/%'
                                 AND rel_path NOT LIKE '%/obj/%'
                                 AND rel_path NOT LIKE '%/bin/%'
                                 AND rel_path NOT LIKE '%/node_modules/%'
                                 AND rel_path NOT LIKE '%.sql'
                               {limitClause};
                               """;

            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new IntentCandidate(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
                }
            }

            // 2. Fallback if no specific architectural keywords were found: take top Types
            if (results.Count == 0)
            {
                cmd.CommandText = $"""
                                   SELECT id, kind,
                                          COALESCE(json_extract(properties, '$.name'), '') AS name,
                                          COALESCE(json_extract(properties, '$.path'), json_extract(properties, '$.file_path'), '') AS rel_path,
                                          COALESCE(json_extract(properties, '$.full_path'), '') AS full_path
                                   FROM nodes
                                   WHERE kind = 'Type'
                                     AND rel_path NOT LIKE '%.spec.%'
                                     AND rel_path NOT LIKE '%.test.%'
                                     AND rel_path NOT LIKE '%/tests/%'
                                     AND rel_path NOT LIKE '%/test/%'
                                     AND rel_path NOT LIKE '%/obj/%'
                                     AND rel_path NOT LIKE '%/bin/%'
                                     AND rel_path NOT LIKE '%/node_modules/%'
                                   {limitClause};
                                   """;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new IntentCandidate(reader.GetString(0), reader.GetString(1),
                        reader.GetString(2), reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4)));
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    public async Task SaveIntentPredictionsAsync(
        string workspaceId,
        List<CodeIntentPredictionResult> predictions,
        CancellationToken cancellationToken = default)
    {
        if (predictions.Count == 0) return;

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 1. Update node properties with intent fields
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            await using (var updCmd = _conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandTimeout = CommandTimeoutSeconds;

                updCmd.CommandText = """
                                     UPDATE nodes SET properties = json_set(
                                         properties,
                                         '$.intent_domain', @domain,
                                         '$.intent_layer', @layer,
                                         '$.intent_pattern', @pattern,
                                         '$.intent_operation', @operation,
                                         '$.intent_capability', @capability,
                                         '$.intent_summary', @summary,
                                         '$.is_pure_domain', @isPureDomain,
                                         '$.target_entities', json(@targetEntities),
                                         '$.emitted_events', json(@emittedEvents)
                                     ) WHERE id = @id;
                                     """;

                var pId = updCmd.Parameters.Add("@id", SqliteType.Text);
                var pDomain = updCmd.Parameters.Add("@domain", SqliteType.Text);
                var pLayer = updCmd.Parameters.Add("@layer", SqliteType.Text);
                var pPattern = updCmd.Parameters.Add("@pattern", SqliteType.Text);
                var pOp = updCmd.Parameters.Add("@operation", SqliteType.Text);
                var pCap = updCmd.Parameters.Add("@capability", SqliteType.Text);
                var pSummary = updCmd.Parameters.Add("@summary", SqliteType.Text);
                var pPure = updCmd.Parameters.Add("@isPureDomain", SqliteType.Integer);
                var pEntities = updCmd.Parameters.Add("@targetEntities", SqliteType.Text);
                var pEvents = updCmd.Parameters.Add("@emittedEvents", SqliteType.Text);

                foreach (var pred in predictions)
                {
                    pId.Value = pred.Id;
                    pDomain.Value = (object?)pred.Domain ?? DBNull.Value;
                    pLayer.Value = (object?)pred.Layer ?? DBNull.Value;
                    pPattern.Value = (object?)pred.Pattern ?? DBNull.Value;
                    pOp.Value = (object?)pred.OperationType ?? DBNull.Value;
                    pCap.Value = (object?)pred.CapabilityTag ?? DBNull.Value;
                    pSummary.Value = (object?)pred.IntentSummary ?? DBNull.Value;
                    pPure.Value = pred.IsPureDomain.HasValue ? (pred.IsPureDomain.Value ? 1 : 0) : DBNull.Value;
                    pEntities.Value = JsonSerializer.Serialize(pred.TargetEntities ?? []);
                    pEvents.Value = JsonSerializer.Serialize(pred.EmittedEvents ?? []);

                    await updCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // 2. Materialize Domain nodes and BELONGS_TO_DOMAIN relationships
            var distinctDomains = predictions.Where(p => !string.IsNullOrWhiteSpace(p.Domain))
                .Select(p => p.Domain!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctDomains.Count > 0)
            {
                await using var domCmd = _conn.CreateCommand();

                domCmd.Transaction = tx;
                domCmd.CommandTimeout = CommandTimeoutSeconds;

                domCmd.CommandText = """
                                     INSERT INTO nodes (id, kind, properties)
                                     VALUES (@id, 'Domain', @props)
                                     ON CONFLICT(id) DO NOTHING;
                                     """;
                var pDomId = domCmd.Parameters.Add("@id", SqliteType.Text);
                var pDomProps = domCmd.Parameters.Add("@props", SqliteType.Text);

                foreach (var dom in distinctDomains)
                {
                    var domId = $"{widPrefix}dom:{dom.ToLowerInvariant()}";

                    var domProps = JsonSerializer.Serialize(new Dictionary<string, object>
                    {
                        ["id"] = domId, ["name"] = dom
                    });

                    pDomId.Value = domId;
                    pDomProps.Value = domProps;
                    await domCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await using var edgeCmd = _conn.CreateCommand();

                edgeCmd.Transaction = tx;
                edgeCmd.CommandTimeout = CommandTimeoutSeconds;

                edgeCmd.CommandText = """
                                      INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                      VALUES (@fromId, @toId, 'BELONGS_TO_DOMAIN', '{}');
                                      """;
                var pFromId = edgeCmd.Parameters.Add("@fromId", SqliteType.Text);
                var pToId = edgeCmd.Parameters.Add("@toId", SqliteType.Text);

                foreach (var pred in predictions.Where(p => !string.IsNullOrWhiteSpace(p.Domain)))
                {
                    var domId = $"{widPrefix}dom:{pred.Domain!.Trim().ToLowerInvariant()}";
                    pFromId.Value = pred.Id;
                    pToId.Value = domId;
                    await edgeCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<IntentRecord>> LoadExistingIntentsAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<IntentRecord>();
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            cmd.CommandText = """
                              SELECT file_path, workspace_id, file_id, content_hash, last_modified_utc,
                                     domain, layer, pattern, operation_type, capability_tag,
                                     intent_summary, target_entities, emitted_events, is_pure_domain,
                                     error_count, last_error, analyzed_at_utc
                              FROM intents
                              WHERE (@workspaceId = '' 
                                     OR workspace_id = @workspaceId COLLATE NOCASE 
                                     OR REPLACE(workspace_id, '\', '/') = REPLACE(@workspaceId, '\', '/') COLLATE NOCASE);
                              """;
            cmd.Parameters.AddWithValue("@workspaceId", workspaceId ?? "");

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var filePath = reader.GetString(0);
                var wsId = reader.GetString(1);
                var fileId = reader.GetString(2);
                var contentHash = reader.GetString(3);
                var lastMod = DateTime.TryParse(reader.GetString(4), out var dt) ? dt : DateTime.UtcNow;
                var domain = reader.IsDBNull(5) ? null : reader.GetString(5);
                var layer = reader.IsDBNull(6) ? null : reader.GetString(6);
                var pattern = reader.IsDBNull(7) ? null : reader.GetString(7);
                var opType = reader.IsDBNull(8) ? null : reader.GetString(8);
                var capTag = reader.IsDBNull(9) ? null : reader.GetString(9);
                var summary = reader.IsDBNull(10) ? null : reader.GetString(10);

                var targetEntities = reader.IsDBNull(11)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(reader.GetString(11));

                var emittedEvents = reader.IsDBNull(12)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(reader.GetString(12));
                var isPure = reader.IsDBNull(13) ? (bool?)null : reader.GetInt32(13) == 1;
                var errorCount = reader.GetInt32(14);
                var lastError = reader.IsDBNull(15) ? null : reader.GetString(15);

                var analyzedAt = reader.IsDBNull(16)
                    ? (DateTime?)null
                    : (DateTime.TryParse(reader.GetString(16), out var adt) ? adt : null);

                results.Add(new IntentRecord(filePath, wsId, fileId, contentHash, lastMod, domain, layer,
                    pattern, opType, capTag, summary, targetEntities, emittedEvents, isPure, errorCount,
                    lastError, analyzedAt));
            }
        }
        finally
        {
            _lock.Release();
        }

        return results;
    }

    public async Task SaveIntentRecordAsync(
        IntentRecord record,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;

            cmd.CommandText = """
                              INSERT INTO intents (
                                  file_path, workspace_id, file_id, content_hash, last_modified_utc,
                                  domain, layer, pattern, operation_type, capability_tag,
                                  intent_summary, target_entities, emitted_events, is_pure_domain,
                                  error_count, last_error, analyzed_at_utc
                              ) VALUES (
                                  @filePath, @workspaceId, @fileId, @contentHash, @lastModifiedUtc,
                                  @domain, @layer, @pattern, @operationType, @capabilityTag,
                                  @intentSummary, @targetEntities, @emittedEvents, @isPureDomain,
                                  0, NULL, @analyzedAtUtc
                              )
                              ON CONFLICT(file_path) DO UPDATE SET
                                  workspace_id = excluded.workspace_id,
                                  file_id = excluded.file_id,
                                  content_hash = excluded.content_hash,
                                  last_modified_utc = excluded.last_modified_utc,
                                  domain = excluded.domain,
                                  layer = excluded.layer,
                                  pattern = excluded.pattern,
                                  operation_type = excluded.operation_type,
                                  capability_tag = excluded.capability_tag,
                                  intent_summary = excluded.intent_summary,
                                  target_entities = excluded.target_entities,
                                  emitted_events = excluded.emitted_events,
                                  is_pure_domain = excluded.is_pure_domain,
                                  error_count = 0,
                                  last_error = NULL,
                                  analyzed_at_utc = excluded.analyzed_at_utc;
                              """;

            cmd.Parameters.AddWithValue("@filePath", record.FilePath);
            cmd.Parameters.AddWithValue("@workspaceId", record.WorkspaceId);
            cmd.Parameters.AddWithValue("@fileId", record.FileId);
            cmd.Parameters.AddWithValue("@contentHash", record.ContentHash);
            cmd.Parameters.AddWithValue("@lastModifiedUtc", record.LastModifiedUtc.ToString("o"));
            cmd.Parameters.AddWithValue("@domain", (object?)record.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@layer", (object?)record.Layer ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pattern", (object?)record.Pattern ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@operationType", (object?)record.OperationType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@capabilityTag", (object?)record.CapabilityTag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@intentSummary", (object?)record.IntentSummary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@targetEntities", JsonSerializer.Serialize(record.TargetEntities ?? []));
            cmd.Parameters.AddWithValue("@emittedEvents", JsonSerializer.Serialize(record.EmittedEvents ?? []));

            cmd.Parameters.AddWithValue("@isPureDomain",
                record.IsPureDomain.HasValue ? (record.IsPureDomain.Value ? 1 : 0) : DBNull.Value);

            cmd.Parameters.AddWithValue("@analyzedAtUtc",
                record.AnalyzedAtUtc?.ToString("o") ?? DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }


    public async Task PurgeIntentsByPathsAsync(
        List<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        if (filePaths == null || filePaths.Count == 0) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            foreach (var chunk in filePaths.Chunk(500))
            {
                await using var cmd = _conn.CreateCommand();

                cmd.Transaction = tx;
                cmd.CommandTimeout = CommandTimeoutSeconds;

                var paramNames = new List<string>();
                for (var i = 0; i < chunk.Length; i++)
                {
                    var pName = $"@p{i}";
                    paramNames.Add(pName);
                    cmd.Parameters.AddWithValue(pName, chunk[i]);
                }

                var inClause = string.Join(", ", paramNames);
                cmd.CommandText = $"""
                                   DELETE FROM intents WHERE file_path IN ({inClause});
                                   DELETE FROM edges
                                   WHERE kind = 'BELONGS_TO_DOMAIN'
                                     AND from_id IN (
                                         SELECT id FROM nodes
                                         WHERE json_extract(properties, '$.path') IN ({inClause})
                                            OR json_extract(properties, '$.file_path') IN ({inClause})
                                            OR id IN ({inClause})
                                     );
                                   UPDATE nodes
                                   SET properties = json_remove(
                                       properties,
                                       '$.intent_domain',
                                       '$.intent_layer',
                                       '$.intent_pattern',
                                       '$.intent_operation',
                                       '$.intent_capability',
                                       '$.intent_summary',
                                       '$.is_pure_domain',
                                       '$.target_entities',
                                       '$.emitted_events'
                                   )
                                   WHERE json_extract(properties, '$.path') IN ({inClause})
                                      OR json_extract(properties, '$.file_path') IN ({inClause})
                                      OR id IN ({inClause});
                                   """;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> ApplyCachedIntentsToGraphAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var updatedNodes = 0;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // 1. Update matching nodes (File or Type nodes matching file_path or file_id)
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

            await using (var updCmd = _conn.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandTimeout = CommandTimeoutSeconds;

                // 0. Ensure no raw .sql scripts are lingering in intents or domain edges
                updCmd.CommandText = """
                                     DELETE FROM intents WHERE file_path LIKE '%.sql';
                                     DELETE FROM edges
                                     WHERE kind = 'BELONGS_TO_DOMAIN'
                                       AND from_id IN (
                                           SELECT id FROM nodes
                                           WHERE json_extract(properties, '$.path') LIKE '%.sql'
                                              OR json_extract(properties, '$.file_path') LIKE '%.sql'
                                              OR id LIKE '%.sql'
                                       );
                                     """;
                await updCmd.ExecuteNonQueryAsync(cancellationToken);

                updCmd.CommandText = """
                                     UPDATE nodes
                                     SET properties = json_set(
                                         properties,
                                         '$.intent_domain', i.domain,
                                         '$.intent_layer', i.layer,
                                         '$.intent_pattern', i.pattern,
                                         '$.intent_operation', i.operation_type,
                                         '$.intent_capability', i.capability_tag,
                                         '$.intent_summary', i.intent_summary,
                                         '$.is_pure_domain', i.is_pure_domain,
                                         '$.target_entities', json(i.target_entities),
                                         '$.emitted_events', json(i.emitted_events)
                                     )
                                     FROM intents i
                                     WHERE (nodes.id = i.file_id 
                                            OR json_extract(nodes.properties, '$.path') = i.file_path 
                                            OR json_extract(nodes.properties, '$.file_path') = i.file_path)
                                       AND i.domain IS NOT NULL;
                                     """;
                updatedNodes = await updCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 2. Materialize Domain nodes from distinct domains in intents
            await using (var domCmd = _conn.CreateCommand())
            {
                domCmd.Transaction = tx;
                domCmd.CommandTimeout = CommandTimeoutSeconds;

                domCmd.CommandText = """
                                     INSERT INTO nodes (id, kind, properties)
                                     SELECT DISTINCT
                                         @widPrefix || 'dom:' || lower(domain) AS id,
                                         'Domain' AS kind,
                                         json_object('id', @widPrefix || 'dom:' || lower(domain), 'name', domain) AS properties
                                     FROM intents
                                     WHERE domain IS NOT NULL AND trim(domain) != ''
                                     ON CONFLICT(id) DO NOTHING;
                                     """;
                domCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await domCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 3. Create BELONGS_TO_DOMAIN edges from nodes to Domain nodes
            await using (var edgeCmd = _conn.CreateCommand())
            {
                edgeCmd.Transaction = tx;
                edgeCmd.CommandTimeout = CommandTimeoutSeconds;

                edgeCmd.CommandText = """
                                      INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                      SELECT DISTINCT
                                          n.id AS from_id,
                                          @widPrefix || 'dom:' || lower(i.domain) AS to_id,
                                          'BELONGS_TO_DOMAIN' AS kind,
                                          '{}' AS properties
                                      FROM nodes n
                                      JOIN intents i ON (n.id = i.file_id 
                                                         OR json_extract(n.properties, '$.path') = i.file_path 
                                                         OR json_extract(n.properties, '$.file_path') = i.file_path)
                                      WHERE i.domain IS NOT NULL AND trim(i.domain) != '';
                                      """;
                edgeCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await edgeCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 4. Create EXPOSES_DOMAIN edges from Project/App nodes to Domain nodes
            await using (var projDomCmd = _conn.CreateCommand())
            {
                projDomCmd.Transaction = tx;
                projDomCmd.CommandTimeout = CommandTimeoutSeconds;

                projDomCmd.CommandText = """
                                         INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                         SELECT DISTINCT
                                             p.id AS from_id,
                                             @widPrefix || 'dom:' || lower(i.domain) AS to_id,
                                             'EXPOSES_DOMAIN' AS kind,
                                             '{}' AS properties
                                         FROM edges e
                                         JOIN nodes p ON e.to_id = p.id AND (p.kind = 'Project' OR p.kind = 'App')
                                         JOIN nodes n ON e.from_id = n.id
                                         JOIN intents i ON (n.id = i.file_id 
                                                            OR json_extract(n.properties, '$.path') = i.file_path 
                                                            OR json_extract(n.properties, '$.file_path') = i.file_path)
                                         WHERE e.kind = 'BELONGS_TO'
                                           AND i.domain IS NOT NULL AND trim(i.domain) != '';
                                         """;
                projDomCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await projDomCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 5. Connect fine-grained subdomains sharing a project boundary via SUBDOMAIN_OF
            await using (var subDomCmd = _conn.CreateCommand())
            {
                subDomCmd.Transaction = tx;
                subDomCmd.CommandTimeout = CommandTimeoutSeconds;

                subDomCmd.CommandText = """
                                        INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                                        SELECT DISTINCT
                                            e_sub.to_id AS from_id,
                                            e_base.to_id AS to_id,
                                            'SUBDOMAIN_OF' AS kind,
                                            '{}' AS properties
                                        FROM edges e_sub
                                        JOIN edges e_base ON e_sub.from_id = e_base.from_id 
                                                         AND e_sub.kind = 'EXPOSES_DOMAIN' 
                                                         AND e_base.kind = 'EXPOSES_DOMAIN'
                                        JOIN nodes d_sub ON e_sub.to_id = d_sub.id AND d_sub.kind = 'Domain'
                                        JOIN nodes d_base ON e_base.to_id = d_base.id AND d_base.kind = 'Domain'
                                        WHERE d_sub.id != d_base.id
                                          AND length(json_extract(d_base.properties, '$.name')) >= 4
                                          AND length(json_extract(d_sub.properties, '$.name')) > length(json_extract(d_base.properties, '$.name'))
                                          AND (lower(json_extract(d_sub.properties, '$.name')) LIKE lower(json_extract(d_base.properties, '$.name')) || '%'
                                               OR lower(json_extract(d_sub.properties, '$.name')) LIKE '%' || lower(json_extract(d_base.properties, '$.name')));
                                        """;
                await subDomCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 6. Clean up orphaned Domain nodes and edges that have no active intents
            await using (var purgeCmd = _conn.CreateCommand())
            {
                purgeCmd.Transaction = tx;
                purgeCmd.CommandTimeout = CommandTimeoutSeconds;

                purgeCmd.CommandText = """
                                       DELETE FROM edges
                                       WHERE kind = 'BELONGS_TO_DOMAIN'
                                         AND from_id NOT IN (
                                             SELECT file_id FROM intents WHERE file_id IS NOT NULL
                                             UNION
                                             SELECT id FROM nodes WHERE json_extract(properties, '$.intent_domain') IS NOT NULL
                                             UNION
                                             SELECT id FROM nodes WHERE kind IN ('Project', 'Service', 'App', 'Worker')
                                         );

                                       DELETE FROM edges
                                       WHERE kind = 'BELONGS_TO_DOMAIN'
                                         AND to_id NOT IN (
                                             SELECT @widPrefix || 'dom:' || lower(domain)
                                             FROM intents
                                             WHERE domain IS NOT NULL AND trim(domain) != ''
                                             UNION
                                             SELECT @widPrefix || 'dom:' || lower(json_extract(properties, '$.intent_domain'))
                                             FROM nodes
                                             WHERE json_extract(properties, '$.intent_domain') IS NOT NULL 
                                               AND trim(json_extract(properties, '$.intent_domain')) != ''
                                         );

                                       DELETE FROM edges
                                       WHERE kind = 'EXPOSES_DOMAIN'
                                         AND to_id NOT IN (
                                             SELECT @widPrefix || 'dom:' || lower(domain)
                                             FROM intents
                                             WHERE domain IS NOT NULL AND trim(domain) != ''
                                             UNION
                                             SELECT @widPrefix || 'dom:' || lower(json_extract(properties, '$.intent_domain'))
                                             FROM nodes
                                             WHERE json_extract(properties, '$.intent_domain') IS NOT NULL 
                                               AND trim(json_extract(properties, '$.intent_domain')) != ''
                                         );

                                       DELETE FROM nodes
                                       WHERE kind = 'Domain'
                                         AND id NOT IN (
                                             SELECT @widPrefix || 'dom:' || lower(domain)
                                             FROM intents
                                             WHERE domain IS NOT NULL AND trim(domain) != ''
                                             UNION
                                             SELECT @widPrefix || 'dom:' || lower(json_extract(properties, '$.intent_domain'))
                                             FROM nodes
                                             WHERE json_extract(properties, '$.intent_domain') IS NOT NULL 
                                               AND trim(json_extract(properties, '$.intent_domain')) != ''
                                         );
                                       """;
                purgeCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await purgeCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }

        return updatedNodes;
    }


    public async Task<List<DomainInfrastructureLinkRecord>> LoadDomainInfrastructureLinksAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            const string sql = """
                               SELECT 
                                   i.domain AS Domain,
                                   n_infra.id AS InfraId,
                                   COALESCE(json_extract(n_infra.properties, '$.name'), n_infra.id) AS InfraName,
                                   n_infra.kind AS InfraKind,
                                   e.kind AS EdgeKind
                               FROM edges e
                               JOIN nodes n ON e.from_id = n.id
                               JOIN nodes n_infra ON e.to_id = n_infra.id
                               JOIN intents i ON (n.id = i.file_id 
                                                  OR json_extract(n.properties, '$.path') = i.file_path 
                                                  OR json_extract(n.properties, '$.file_path') = i.file_path)
                               WHERE (@workspaceId = '' OR i.workspace_id = @workspaceId COLLATE NOCASE OR REPLACE(i.workspace_id, '\', '/') = REPLACE(@workspaceId, '\', '/') COLLATE NOCASE)
                                 AND i.domain IS NOT NULL AND trim(i.domain) != ''
                                 AND n_infra.kind IN ('Database', 'Topic')
                               GROUP BY i.domain, n_infra.id, n_infra.kind, e.kind;
                               """;

            var rows = await _conn.QueryAsync<(string Domain, string InfraId, string InfraName, string InfraKind, string EdgeKind)>(
                new CommandDefinition(sql, new { workspaceId = workspaceId ?? "" }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
            return
            [
                .. rows.Select(r =>
                    new DomainInfrastructureLinkRecord(r.Domain, r.InfraId, r.InfraName, r.InfraKind, r.EdgeKind))
            ];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SqliteGraphClient] Note on querying domain infrastructure links: {Message}", ex.Message);
            return [];
        }
        finally
        {
            _lock.Release();
        }
    }
}
