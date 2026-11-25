using System.Text;

namespace CodeExplorer.Core.Diagrams;

public static class DiagramExporter
{
    public static async Task<string> ExportAsync(
        IGraphClient client,
        string format = "mermaid",
        string type = "architecture",
        string? projectFilter = null,
        bool includeLibraries = false,
        CancellationToken cancellationToken = default)
    {
        return type.ToLowerInvariant() switch
        {
            "lineage" or "data_lineage" => await GenerateDataLineageDiagramAsync(client, projectFilter, cancellationToken),
            "er" or "erd" or "schema" or "data_schema" => await GenerateErDiagramAsync(client, cancellationToken),
            "cqrs" or "saga" or "events" => await GenerateCqrsPipelineDiagramAsync(client, cancellationToken),
            "context" or "contexts" or "bounded-context" or "bounded-contexts" or "context-map" or "contextmap" => await GenerateBoundedContextDiagramAsync(client, format, cancellationToken),
            "domain" or "domains" or "domain-services" => await GenerateDomainDiagramAsync(client, format, includeLibraries, cancellationToken),
            _ => format.ToLowerInvariant() == "c4"
                ? await GenerateC4ArchitectureAsync(client, projectFilter, includeLibraries, cancellationToken)
                : await GenerateMermaidArchitectureAsync(client, projectFilter, includeLibraries, cancellationToken)
        };
    }

    public static async Task<string> GenerateBoundedContextDiagramAsync(
        IGraphClient client,
        string format = "mermaid",
        CancellationToken cancellationToken = default)
    {
        var engine = new ArchitectureViewEngine(client);
        var mapDto = await engine.GetBoundedContextMapAsync(ct: cancellationToken);
        return ArchitectureViewEngine.SerializeBoundedContextMap(mapDto, format);
    }

    public static async Task<string> GenerateDomainDiagramAsync(
        IGraphClient client,
        string format = "mermaid",
        bool includeLibraries = false,
        CancellationToken cancellationToken = default)
    {
        var engine = new ArchitectureViewEngine(client);
        var domainDto = await engine.GetDomainArchitectureAsync(includeLibraries: includeLibraries, cancellationToken);
        return ArchitectureViewEngine.SerializeDomainArchitecture(domainDto, format);
    }

    public static async Task<string> GenerateMermaidArchitectureAsync(
        IGraphClient client,
        string? projectFilter = null,
        bool includeLibraries = false,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        var allNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Query Projects
        var projQuery = "MATCH (p) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.id AS id, p.name AS name, p.framework AS framework, labels(p) AS labels, p.role AS role, p.is_library AS is_library";
        var projRows = await client.QueryAsync<(string Id, string Name, string? Framework, List<string> Labels, string? Role, bool IsLibrary)>(projQuery, null, cancellationToken);

        var serviceNodes = new List<(string id, string label)>();
        var libNodes = new List<(string id, string label)>();

        foreach (var row in projRows)
        {
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "proj");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Project";
            var label = string.IsNullOrEmpty(row.Framework) ? name : $"{name}\\n({row.Framework})";

            var isLib = row.IsLibrary ||
                        string.Equals(row.Role, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
                        row.Labels.Any(l => string.Equals(l, "Library", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(l, "SharedLibrary", StringComparison.OrdinalIgnoreCase));

            if (isLib)
            {
                if (includeLibraries)
                {
                    libNodes.Add((id, label));
                    allNodes.Add(id);
                }
            }
            else
            {
                serviceNodes.Add((id, label));
                allNodes.Add(id);
            }
        }

        if (serviceNodes.Count > 0)
        {
            sb.AppendLine("  subgraph Projects [Applications & Services]");
            foreach (var (id, label) in serviceNodes)
            {
                sb.AppendLine($"    {id}[\"{label}\"]");
            }
            sb.AppendLine("  end");
        }

        if (includeLibraries && libNodes.Count > 0)
        {
            sb.AppendLine("  subgraph Libraries [Shared Libraries & SDKs]");
            foreach (var (id, label) in libNodes)
            {
                sb.AppendLine($"    {id}[\"{label}\"]");
            }
            sb.AppendLine("  end");
        }

        // 2. Query Databases
        var dbQuery =
            "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine";
        var dbRows = await client.QueryAsync<(string Id, string Name, string? DbType, string? Engine)>(dbQuery, null, cancellationToken);

        var hasDbs = false;
        var dbSb = new StringBuilder();
        dbSb.AppendLine("  subgraph Databases [Data Storage]");

        foreach (var row in dbRows)
        {
            hasDbs = true;
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "db");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Database";
            allNodes.Add(id);

            var dbType = !string.IsNullOrEmpty(row.DbType) ? row.DbType : "Database";
            var engine = row.Engine;

            var label = !string.IsNullOrEmpty(engine) &&
                        !engine.Equals(name, StringComparison.OrdinalIgnoreCase)
                ? $"{name} ({engine})\\n[{dbType}]"
                : $"{name}\\n[{dbType}]";
            dbSb.AppendLine($"    {id}[(\"{label}\")]");
        }

        dbSb.AppendLine("  end");
        if (hasDbs) sb.Append(dbSb);

        // 3. Query Topics / Message Brokers
        var topicQuery =
            "MATCH (t:Topic) WHERE (t.broker_type IS NULL OR (t.broker_type <> 'mediatr' AND t.broker_type <> 'in-memory')) AND (t.is_internal IS NULL OR t.is_internal <> 'true') RETURN t.id AS id, t.name AS name, t.broker_type AS broker";
        var topicRows = await client.QueryAsync<(string Id, string Name, string? Broker)>(topicQuery, null, cancellationToken);

        var hasTopics = false;
        var topicSb = new StringBuilder();
        topicSb.AppendLine("  subgraph MessageBrokers [Event & Message Brokers]");

        foreach (var row in topicRows)
        {
            hasTopics = true;
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "topic");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Topic";
            allNodes.Add(id);

            var broker = !string.IsNullOrEmpty(row.Broker) ? row.Broker : "Queue";
            topicSb.AppendLine($"    {id}{{\"{name}\\n[{broker}]\"}}");
        }

        topicSb.AppendLine("  end");
        if (hasTopics) sb.Append(topicSb);

        // 4. Query External Services / Cloud
        var extQuery =
            "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name UNION MATCH (c:CloudService) RETURN c.id AS id, c.name AS name";
        var extRows = await client.QueryAsync<(string Id, string Name)>(extQuery, null, cancellationToken);

        var hasExt = false;
        var extSb = new StringBuilder();
        extSb.AppendLine("  subgraph ExternalServices [External & Cloud Services]");

        foreach (var row in extRows)
        {
            hasExt = true;
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "ext");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Service";
            allNodes.Add(id);
            extSb.AppendLine($"    {id}[\"{name}\"]");
        }

        extSb.AppendLine("  end");
        if (hasExt) sb.Append(extSb);

        // 5. Query Relationships (USES_DB, USES_CLOUD, PUBLISHES_TO, SUBSCRIBES_TO, CALLS, CONFIGURES)
        var relsQuery = """
                        MATCH (a)-[r:USES_DB|USES_CLOUD|USES_API|CONFIGURES]->(b)
                        RETURN a.id AS from_id, b.id AS to_id, type(r) AS rel_type
                        UNION
                        MATCH (t:Topic)-[:PUBLISHED_BY]->(producer) WHERE (t.broker_type IS NULL OR (t.broker_type <> 'mediatr' AND t.broker_type <> 'in-memory')) AND (t.is_internal IS NULL OR t.is_internal <> 'true')
                        RETURN producer.id AS from_id, t.id AS to_id, 'PUBLISHES_TO' AS rel_type
                        UNION
                        MATCH (t:Topic)-[:SUBSCRIBED_BY]->(consumer) WHERE (t.broker_type IS NULL OR (t.broker_type <> 'mediatr' AND t.broker_type <> 'in-memory')) AND (t.is_internal IS NULL OR t.is_internal <> 'true')
                        RETURN t.id AS from_id, consumer.id AS to_id, 'SUBSCRIBES_TO' AS rel_type
                        """;
        var relsRows = await client.QueryAsync<(string FromId, string ToId, string RelType)>(relsQuery, null, cancellationToken);

        var addedEdges = new HashSet<string>();

        foreach (var row in relsRows)
        {
            var from = SanitizeId(row.FromId);
            var to = SanitizeId(row.ToId);
            var rel = !string.IsNullOrEmpty(row.RelType) ? row.RelType : "USES";

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;
            if (!allNodes.Contains(from) || !allNodes.Contains(to)) continue;

            var edgeKey = $"{from}->{to}:{rel}";

            if (addedEdges.Add(edgeKey))
            {
                sb.AppendLine($"  {from} -->|{rel}| {to}");
            }
        }

        return sb.ToString();
    }

    public static async Task<string> GenerateC4ArchitectureAsync(
        IGraphClient client,
        string? projectFilter = null,
        bool includeLibraries = false,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("C4Container");
        sb.AppendLine("  title System Architecture Diagram");
        sb.AppendLine();

        var allNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Containers (Projects)
        var projQuery = "MATCH (p) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.id AS id, p.name AS name, p.framework AS framework, labels(p) AS labels, p.role AS role, p.is_library AS is_library";
        var projRows = await client.QueryAsync<(string Id, string Name, string? Framework, List<string> Labels, string? Role, bool IsLibrary)>(projQuery, null, cancellationToken);

        foreach (var row in projRows)
        {
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "proj");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Project";
            var framework = !string.IsNullOrEmpty(row.Framework) ? row.Framework : "Application";

            var isLib = row.IsLibrary ||
                        string.Equals(row.Role, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
                        row.Labels.Any(l => string.Equals(l, "Library", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(l, "SharedLibrary", StringComparison.OrdinalIgnoreCase));

            if (isLib && !includeLibraries)
            {
                continue;
            }

            allNodes.Add(id);
            var desc = isLib ? "Shared Library" : "Service component";
            sb.AppendLine($"  Container({id}, \"{name}\", \"{framework}\", \"{desc}\")");
        }

        // 2. Databases
        var dbQuery =
            "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine";
        var dbRows = await client.QueryAsync<(string Id, string Name, string? DbType, string? Engine)>(dbQuery, null, cancellationToken);

        foreach (var row in dbRows)
        {
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "db");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Database";
            allNodes.Add(id);

            var dbType = !string.IsNullOrEmpty(row.DbType) ? row.DbType : "Database";
            var tech = !string.IsNullOrEmpty(row.Engine) ? row.Engine : dbType;
            sb.AppendLine($"  ContainerDb({id}, \"{name}\", \"{tech}\", \"Data storage\")");
        }

        // 3. Topics / Queues
        var topicQuery =
            "MATCH (t:Topic) WHERE (t.broker_type IS NULL OR (t.broker_type <> 'mediatr' AND t.broker_type <> 'in-memory')) AND (t.is_internal IS NULL OR t.is_internal <> 'true') RETURN t.id AS id, t.name AS name, t.broker_type AS broker";
        var topicRows = await client.QueryAsync<(string Id, string Name, string? Broker)>(topicQuery, null, cancellationToken);

        foreach (var row in topicRows)
        {
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "topic");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "Topic";
            allNodes.Add(id);

            var broker = !string.IsNullOrEmpty(row.Broker) ? row.Broker : "Queue";
            sb.AppendLine($"  ContainerQueue({id}, \"{name}\", \"{broker}\", \"Message / Event Broker\")");
        }

        // 4. External Services
        var extQuery =
            "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name UNION MATCH (c:CloudService) RETURN c.id AS id, c.name AS name";
        var extRows = await client.QueryAsync<(string Id, string Name)>(extQuery, null, cancellationToken);

        foreach (var row in extRows)
        {
            var id = SanitizeId(!string.IsNullOrEmpty(row.Id) ? row.Id : "ext");
            var name = !string.IsNullOrEmpty(row.Name) ? row.Name : "External Service";
            allNodes.Add(id);
            sb.AppendLine($"  System_Ext({id}, \"{name}\", \"External / Cloud Service\")");
        }

        sb.AppendLine();

        // 5. Relationships
        var relsQuery = """
                        MATCH (a)-[r:USES_DB|USES_CLOUD|USES_API]->(b)
                        RETURN a.id AS from_id, b.id AS to_id, type(r) AS rel_type
                        UNION
                        MATCH (t:Topic)-[:PUBLISHED_BY]->(producer) WHERE (t.broker_type IS NULL OR (t.broker_type <> 'mediatr' AND t.broker_type <> 'in-memory')) AND (t.is_internal IS NULL OR t.is_internal <> 'true')
                        RETURN producer.id AS from_id, t.id AS to_id, 'Publishes to' AS rel_type
                        UNION
                        MATCH (t:Topic)-[:SUBSCRIBED_BY]->(consumer) WHERE (t.broker_type IS NULL OR (t.broker_type <> 'mediatr' AND t.broker_type <> 'in-memory')) AND (t.is_internal IS NULL OR t.is_internal <> 'true')
                        RETURN t.id AS from_id, consumer.id AS to_id, 'Subscribes from' AS rel_type
                        """;
        var relsRows = await client.QueryAsync<(string FromId, string ToId, string RelType)>(relsQuery, null, cancellationToken);

        var addedEdges = new HashSet<string>();

        foreach (var row in relsRows)
        {
            var from = SanitizeId(row.FromId);
            var to = SanitizeId(row.ToId);
            var rel = !string.IsNullOrEmpty(row.RelType) ? row.RelType : "Uses";

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;
            if (!allNodes.Contains(from) || !allNodes.Contains(to)) continue;

            var edgeKey = $"{from}->{to}:{rel}";

            if (addedEdges.Add(edgeKey))
            {
                sb.AppendLine($"  Rel({from}, {to}, \"{rel}\")");
            }
        }

        return sb.ToString();
    }

    public static Task<string> GenerateDataLineageDiagramAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default) =>
        GenerateDataLineageDiagramAsync(client, null, cancellationToken);

    public static async Task<string> GenerateDataLineageDiagramAsync(
        IGraphClient client,
        string? projectFilter,
        CancellationToken cancellationToken = default)
    {
        const string defaultEntityLabel = "Entity";
        const string defaultAccessorLabel = "Accessor";
        const string defaultTableLabel = "Table";
        const string persistedInEdge = "PERSISTED_IN";
        const string readsFromEdge = "READS_FROM";
        const string writesToEdge = "WRITES_TO";
        const string definesEdge = "DEFINES";

        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        var tables = new Dictionary<string, string>(StringComparer.Ordinal);
        var entities = new Dictionary<string, string>(StringComparer.Ordinal);
        var edges = new List<(string From, string To, string Label)>();
        var seenEdges = new HashSet<(string From, string To, string Label)>();

        void AddLineageEdge(string rawFromId, string rawFromName, string fallbackFromLabel, string rawTableId, string rawTableName, string edgeLabel)
        {
            var fromId = SanitizeId(rawFromId);
            var fromName = !string.IsNullOrEmpty(rawFromName) ? rawFromName : fallbackFromLabel;
            var tableId = SanitizeId(rawTableId);
            var tableName = !string.IsNullOrEmpty(rawTableName) ? rawTableName : defaultTableLabel;

            entities[fromId] = fromName;
            tables[tableId] = tableName;
            if (seenEdges.Add((fromId, tableId, edgeLabel)))
            {
                edges.Add((fromId, tableId, edgeLabel));
            }
        }

        var hasFilter = !string.IsNullOrWhiteSpace(projectFilter);
        var ormQuery = hasFilter
            ? """
              MATCH (p)<-[:BELONGS_TO]-(:ProjectSyntax)-[:CONTAINS]->(entity:Type)<-[:IMPLEMENTED_BY]-(t:Table)
              WHERE p.id = $proj OR p.name = $proj
              RETURN entity.id AS entity_id, entity.name AS entity_name, t.id AS table_id, t.name AS table_name
              """
            : """
              MATCH (t:Table)-[:IMPLEMENTED_BY]->(entity:Type)
              RETURN entity.id AS entity_id, entity.name AS entity_name, t.id AS table_id, t.name AS table_name
              """;
        var lineageRows = await client.QueryAsync<(string EntityId, string EntityName, string TableId, string TableName)>(
            ormQuery,
            hasFilter ? new { proj = projectFilter } : null,
            cancellationToken);

        foreach (var row in lineageRows)
        {
            AddLineageEdge(row.EntityId, row.EntityName, defaultEntityLabel, row.TableId, row.TableName, persistedInEdge);
        }

        // Also include raw-SQL and direct Function/Type table accessors (pgx, database/sql, Dapper, psycopg, sqlite3)
        var directAccessQuery = hasFilter
            ? """
              MATCH (p)<-[:BELONGS_TO]-(:ProjectSyntax)-[:CONTAINS]->(fn)-[r:READS_FROM|WRITES_TO]->(t:Table)
              WHERE (p.id = $proj OR p.name = $proj) AND (fn:Function OR fn:Type) AND (fn.is_test IS NULL OR fn.is_test = false)
              RETURN fn.id AS fn_id, fn.name AS fn_name, t.id AS table_id, t.name AS table_name, type(r) AS rel_kind
              """
            : """
              MATCH (fn)-[r:READS_FROM|WRITES_TO]->(t:Table)
              WHERE (fn:Function OR fn:Type) AND (fn.is_test IS NULL OR fn.is_test = false)
              RETURN fn.id AS fn_id, fn.name AS fn_name, t.id AS table_id, t.name AS table_name, type(r) AS rel_kind
              """;
        var directRows = await client.QueryAsync<(string FnId, string FnName, string TableId, string TableName, string RelKind)>(
            directAccessQuery,
            hasFilter ? new { proj = projectFilter } : null,
            cancellationToken);

        foreach (var row in directRows)
        {
            var label = string.Equals(row.RelKind, writesToEdge, StringComparison.OrdinalIgnoreCase) ? writesToEdge : readsFromEdge;
            AddLineageEdge(row.FnId, row.FnName, defaultAccessorLabel, row.TableId, row.TableName, label);
        }

        var queryAccessCypher = hasFilter
            ? """
              MATCH (p)-[:CONTAINS]->(:File)<-[:DECLARED_IN]-(q:Query)-[:ACCESSED_BY]->(fn:Function), (q)-[r:READS_FROM|WRITES_TO|DEPENDS_ON]->(t:Table)
              WHERE (p.id = $proj OR p.name = $proj) AND (fn.is_test IS NULL OR fn.is_test = false)
              RETURN fn.id AS fn_id, fn.name AS fn_name, t.id AS table_id, t.name AS table_name, type(r) AS rel_kind, q.name AS query_name
              """
            : """
              MATCH (q:Query)-[:ACCESSED_BY]->(fn:Function), (q)-[r:READS_FROM|WRITES_TO|DEPENDS_ON]->(t:Table)
              WHERE fn.is_test IS NULL OR fn.is_test = false
              RETURN fn.id AS fn_id, fn.name AS fn_name, t.id AS table_id, t.name AS table_name, type(r) AS rel_kind, q.name AS query_name
              """;
        var queryRows = await client.QueryAsync<(string FnId, string FnName, string TableId, string TableName, string RelKind, string QueryName)>(
            queryAccessCypher,
            hasFilter ? new { proj = projectFilter } : null,
            cancellationToken);

        foreach (var row in queryRows)
        {
            var label = ClassifyQueryLineageEdge(row.RelKind, row.QueryName, readsFromEdge, writesToEdge, definesEdge);
            AddLineageEdge(row.FnId, row.FnName, defaultAccessorLabel, row.TableId, row.TableName, label);
        }

        var subgraphTitle = lineageRows.Count > 0 && directRows.Count == 0 && queryRows.Count == 0
            ? "ORM Entities & Models"
            : "Entities & Data Accessors";
        sb.AppendLine($"  subgraph Entities [{subgraphTitle}]");

        foreach (var (id, name) in entities)
        {
            sb.AppendLine($"    {id}[\"{name}\"]");
        }

        sb.AppendLine("  end");
        sb.AppendLine("  subgraph Tables [Database Tables]");

        foreach (var (id, name) in tables)
        {
            sb.AppendLine($"    {id}[(\"{name}\")]");
        }

        sb.AppendLine("  end");

        foreach (var (from, to, edgeLabel) in edges)
        {
            sb.AppendLine($"  {from} -->|{edgeLabel}| {to}");
        }

        return sb.ToString();
    }

    private static string ClassifyQueryLineageEdge(
        string? relKind,
        string? queryName,
        string readsLabel,
        string writesLabel,
        string definesLabel)
    {
        if (string.Equals(relKind, writesLabel, StringComparison.OrdinalIgnoreCase)) return writesLabel;
        if (string.Equals(relKind, readsLabel, StringComparison.OrdinalIgnoreCase)) return readsLabel;

        var qUpper = (queryName ?? string.Empty).Trim().ToUpperInvariant();
        if (qUpper.StartsWith("INSERT", StringComparison.Ordinal) ||
            qUpper.StartsWith("UPDATE", StringComparison.Ordinal) ||
            qUpper.StartsWith("DELETE", StringComparison.Ordinal) ||
            qUpper.StartsWith("UPSERT", StringComparison.Ordinal) ||
            qUpper.StartsWith("MERGE", StringComparison.Ordinal) ||
            qUpper.StartsWith("REPLACE", StringComparison.Ordinal))
        {
            return writesLabel;
        }
        if (qUpper.StartsWith("CREATE", StringComparison.Ordinal) ||
            qUpper.StartsWith("ALTER", StringComparison.Ordinal) ||
            qUpper.StartsWith("DROP", StringComparison.Ordinal))
        {
            return definesLabel;
        }
        return readsLabel;
    }

    private sealed record ErColumnDto(
        [property: System.Text.Json.Serialization.JsonPropertyName("name")] string? Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("type")] string? SqlFormat,
        [property: System.Text.Json.Serialization.JsonPropertyName("pk")] bool? IsPrimaryKey,
        [property: System.Text.Json.Serialization.JsonPropertyName("fk")] string? ForeignKey);

    public static async Task<string> GenerateErDiagramAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        const string tableQuery = """
        MATCH (t:Table)
        RETURN t.id AS id, t.name AS name, t.sub_kind AS sub_kind, t.columns AS columns
        """;
        var tableRows = await client.QueryAsync<(string Id, string Name, string? SubKind, string? Columns)>(tableQuery, null, cancellationToken);

        var nameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in tableRows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            var safeName = SanitizeId(!string.IsNullOrWhiteSpace(row.Name) ? row.Name : row.Id);
            nameById[row.Id] = safeName;

            if (string.IsNullOrWhiteSpace(row.Columns))
            {
                sb.AppendLine($"  {safeName} {{}}");
                continue;
            }

            sb.AppendLine($"  {safeName} {{");
            try
            {
                var cols = System.Text.Json.JsonSerializer.Deserialize<List<ErColumnDto>>(row.Columns) ?? [];
                foreach (var col in cols)
                {
                    if (string.IsNullOrWhiteSpace(col.Name)) continue;
                    var safeType = string.IsNullOrWhiteSpace(col.SqlFormat) ? "unknown" : col.SqlFormat.Replace(' ', '_');
                    var isPk = col.IsPrimaryKey == true;
                    var hasFk = !string.IsNullOrEmpty(col.ForeignKey);
                    var keyTag = (isPk, hasFk) switch
                    {
                        (true, true) => " PK,FK",
                        (true, false) => " PK",
                        (false, true) => " FK",
                        _ => string.Empty
                    };
                    sb.AppendLine($"    {safeType} {col.Name}{keyTag}");
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Skip malformed column JSON gracefully
            }
            sb.AppendLine("  }");
        }

        const string fkQuery = """
        MATCH (child:Table)-[r:REFERENCES]->(parent:Table)
        RETURN child.id AS child_id, child.name AS child_name, parent.id AS parent_id, parent.name AS parent_name
        """;
        var fkRows = await client.QueryAsync<(string ChildId, string ChildName, string ParentId, string ParentName)>(fkQuery, null, cancellationToken);
        var seenEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in fkRows)
        {
            var childName = nameById.GetValueOrDefault(row.ChildId) ?? SanitizeId(row.ChildName);
            var parentName = nameById.GetValueOrDefault(row.ParentId) ?? SanitizeId(row.ParentName);
            if (seenEdges.Add($"{parentName}->{childName}"))
            {
                sb.AppendLine($"  {parentName} ||--o{{ {childName} : references");
            }
        }

        return sb.ToString();
    }

    public static async Task<string> GenerateCqrsPipelineDiagramAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        var cqrsQuery = """
        MATCH (topic:Topic)-[:PUBLISHED_BY]->(pub), (topic)-[:SUBSCRIBED_BY]->(sub)
        RETURN pub.id AS pub_id, pub.name AS pub_name, topic.id AS topic_id, topic.name AS topic_name, topic.broker_type AS broker, sub.id AS sub_id, sub.name AS sub_name
        """;
        var cqrsRows = await client.QueryAsync<(string PubId, string PubName, string TopicId, string TopicName, string? Broker, string SubId, string SubName)>(cqrsQuery, null, cancellationToken);

        var producers = new Dictionary<string, string>();
        var topics = new Dictionary<string, (string Name, string Broker)>();
        var consumers = new Dictionary<string, string>();
        var pubEdges = new HashSet<(string, string)>();
        var subEdges = new HashSet<(string, string)>();

        foreach (var row in cqrsRows)
        {
            var pubId = SanitizeId(row.PubId);
            var pubName = !string.IsNullOrEmpty(row.PubName) ? row.PubName : "Producer";
            var topicId = SanitizeId(row.TopicId);
            var topicName = !string.IsNullOrEmpty(row.TopicName) ? row.TopicName : "Message";
            var broker = !string.IsNullOrEmpty(row.Broker) ? row.Broker : "Event";
            var subId = SanitizeId(row.SubId);
            var subName = !string.IsNullOrEmpty(row.SubName) ? row.SubName : "Consumer";

            producers[pubId] = pubName;
            topics[topicId] = (topicName, broker);
            consumers[subId] = subName;

            pubEdges.Add((pubId, topicId));
            subEdges.Add((topicId, subId));
        }

        sb.AppendLine("  subgraph Producers [Command / Event Emitters]");

        foreach (var (id, name) in producers)
        {
            sb.AppendLine($"    {id}[\"{name}\"]");
        }

        sb.AppendLine("  end");

        sb.AppendLine("  subgraph Messages [Commands & Events]");

        foreach (var (id, (name, broker)) in topics)
        {
            sb.AppendLine($"    {id}{{\"{name}\\n[{broker}]\"}}");
        }

        sb.AppendLine("  end");

        sb.AppendLine("  subgraph Consumers [Handlers & Consumers]");

        foreach (var (id, name) in consumers)
        {
            sb.AppendLine($"    {id}[\"{name}\"]");
        }

        sb.AppendLine("  end");

        foreach (var (from, to) in pubEdges)
        {
            sb.AppendLine($"  {from} -->|PUBLISHES| {to}");
        }

        foreach (var (from, to) in subEdges)
        {
            sb.AppendLine($"  {from} -->|HANDLED_BY| {to}");
        }

        return sb.ToString();
    }

    private static string SanitizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "node";
        return new string(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }
}
