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
            "lineage" or "data_lineage" => await GenerateDataLineageDiagramAsync(client, cancellationToken),
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

    public static async Task<string> GenerateDataLineageDiagramAsync(
        IGraphClient client,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        sb.AppendLine("  subgraph Entities [ORM Entities & Models]");

        var lineageQuery = """
        MATCH (t:Table)-[:IMPLEMENTED_BY]->(entity:Type)
        RETURN entity.id AS entity_id, entity.name AS entity_name, t.id AS table_id, t.name AS table_name
        """;
        var lineageRows = await client.QueryAsync<(string EntityId, string EntityName, string TableId, string TableName)>(lineageQuery, null, cancellationToken);

        var tables = new Dictionary<string, string>();
        var entities = new Dictionary<string, string>();
        var edges = new List<(string From, string To)>();

        foreach (var row in lineageRows)
        {
            var entityId = SanitizeId(row.EntityId);
            var entityName = !string.IsNullOrEmpty(row.EntityName) ? row.EntityName : "Entity";
            var tableId = SanitizeId(row.TableId);
            var tableName = !string.IsNullOrEmpty(row.TableName) ? row.TableName : "Table";

            entities[entityId] = entityName;
            tables[tableId] = tableName;
            edges.Add((entityId, tableId));
        }

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

        foreach (var (from, to) in edges)
        {
            sb.AppendLine($"  {from} -->|PERSISTED_IN| {to}");
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
