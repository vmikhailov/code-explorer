using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Diagrams;

public static class DiagramExporter
{
    public static async Task<string> ExportAsync(
        IGraphClient client,
        string format = "mermaid",
        string type = "architecture",
        string? projectFilter = null,
        CancellationToken cancellationToken = default)
    {
        return type.ToLowerInvariant() switch
        {
            "lineage" or "data_lineage" => await GenerateDataLineageDiagramAsync(client, cancellationToken),
            "cqrs" or "saga" or "events" => await GenerateCqrsPipelineDiagramAsync(client, cancellationToken),
            _ => format.ToLowerInvariant() == "c4"
                ? await GenerateC4ArchitectureAsync(client, projectFilter, cancellationToken)
                : await GenerateMermaidArchitectureAsync(client, projectFilter, cancellationToken)
        };
    }

    public static async Task<string> GenerateMermaidArchitectureAsync(
        IGraphClient client,
        string? projectFilter = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        // 1. Query Projects
        var projQuery = "MATCH (p) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.id AS id, p.name AS name, p.framework AS framework";
        var projJson = await client.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projJson);

        var projects = new HashSet<string>();
        sb.AppendLine("  subgraph Projects [Applications & Services]");
        foreach (var row in projDoc.RootElement.EnumerateArray())
        {
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "proj");
            var name = row.GetProperty("name").GetString() ?? "Project";
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            var label = string.IsNullOrEmpty(framework) ? name : $"{name}\\n({framework})";
            sb.AppendLine($"    {id}[\"{label}\"]");
            projects.Add(id);
        }
        sb.AppendLine("  end");

        // 2. Query Databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);

        var hasDbs = false;
        var dbSb = new StringBuilder();
        dbSb.AppendLine("  subgraph Databases [Data Storage]");
        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            hasDbs = true;
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "db");
            var name = row.GetProperty("name").GetString() ?? "Database";
            var dbType = row.TryGetProperty("db_type", out var dt) && dt.ValueKind == JsonValueKind.String ? dt.GetString() : "Database";
            var engine = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;

            var label = !string.IsNullOrEmpty(engine) && !engine.Equals(name, StringComparison.OrdinalIgnoreCase)
                ? $"{name} ({engine})\\n[{dbType}]"
                : $"{name}\\n[{dbType}]";
            dbSb.AppendLine($"    {id}[(\"{label}\")]");
        }
        dbSb.AppendLine("  end");
        if (hasDbs) sb.Append(dbSb);

        // 3. Query Topics / Message Brokers
        var topicQuery = "MATCH (t:Topic) RETURN t.id AS id, t.name AS name, t.broker_type AS broker";
        var topicJson = await client.ExecuteQueryAsync(topicQuery, null, cancellationToken);
        using var topicDoc = JsonDocument.Parse(topicJson);

        var hasTopics = false;
        var topicSb = new StringBuilder();
        topicSb.AppendLine("  subgraph MessageBrokers [Event & Message Brokers]");
        foreach (var row in topicDoc.RootElement.EnumerateArray())
        {
            hasTopics = true;
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "topic");
            var name = row.GetProperty("name").GetString() ?? "Topic";
            var broker = row.TryGetProperty("broker", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : "Queue";
            topicSb.AppendLine($"    {id}{{\"{name}\\n[{broker}]\"}}");
        }
        topicSb.AppendLine("  end");
        if (hasTopics) sb.Append(topicSb);

        // 4. Query External Services / Cloud
        var extQuery = "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name UNION MATCH (c:CloudService) RETURN c.id AS id, c.name AS name";
        var extJson = await client.ExecuteQueryAsync(extQuery, null, cancellationToken);
        using var extDoc = JsonDocument.Parse(extJson);

        var hasExt = false;
        var extSb = new StringBuilder();
        extSb.AppendLine("  subgraph ExternalServices [External & Cloud Services]");
        foreach (var row in extDoc.RootElement.EnumerateArray())
        {
            hasExt = true;
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "ext");
            var name = row.GetProperty("name").GetString() ?? "Service";
            extSb.AppendLine($"    {id}[\"{name}\"]");
        }
        extSb.AppendLine("  end");
        if (hasExt) sb.Append(extSb);

        // 5. Query Relationships (USES_DB, USES_CLOUD, PUBLISHES_TO, SUBSCRIBES_TO, CALLS, CONFIGURES)
        var relsQuery = """
        MATCH (a)-[r:USES_DB|USES_CLOUD|USES_API|CONFIGURES]->(b)
        RETURN a.id AS from_id, b.id AS to_id, type(r) AS rel_type
        UNION
        MATCH (t:Topic)-[:PUBLISHED_BY]->(producer)
        RETURN producer.id AS from_id, t.id AS to_id, 'PUBLISHES_TO' AS rel_type
        UNION
        MATCH (t:Topic)-[:SUBSCRIBED_BY]->(consumer)
        RETURN t.id AS from_id, consumer.id AS to_id, 'SUBSCRIBES_TO' AS rel_type
        """;
        var relsJson = await client.ExecuteQueryAsync(relsQuery, null, cancellationToken);
        using var relsDoc = JsonDocument.Parse(relsJson);

        var addedEdges = new HashSet<string>();
        foreach (var row in relsDoc.RootElement.EnumerateArray())
        {
            var from = SanitizeId(row.GetProperty("from_id").GetString() ?? "");
            var to = SanitizeId(row.GetProperty("to_id").GetString() ?? "");
            var rel = row.GetProperty("rel_type").GetString() ?? "USES";

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;

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
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("C4Container");
        sb.AppendLine("  title System Architecture Diagram");
        sb.AppendLine();

        // 1. Containers (Projects)
        var projQuery = "MATCH (p) WHERE (p:Project OR p:Service OR p:App OR p:Worker OR p:Library OR p:CliTool OR p:FrontendApp OR p:SharedLibrary) RETURN p.id AS id, p.name AS name, p.framework AS framework";
        var projJson = await client.ExecuteQueryAsync(projQuery, null, cancellationToken);
        using var projDoc = JsonDocument.Parse(projJson);

        foreach (var row in projDoc.RootElement.EnumerateArray())
        {
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "proj");
            var name = row.GetProperty("name").GetString() ?? "Project";
            var framework = row.TryGetProperty("framework", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : "Application";
            sb.AppendLine($"  Container({id}, \"{name}\", \"{framework}\", \"Service component\")");
        }

        // 2. Databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine";
        var dbJson = await client.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        using var dbDoc = JsonDocument.Parse(dbJson);

        foreach (var row in dbDoc.RootElement.EnumerateArray())
        {
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "db");
            var name = row.GetProperty("name").GetString() ?? "Database";
            var dbType = row.TryGetProperty("db_type", out var dt) && dt.ValueKind == JsonValueKind.String ? dt.GetString() : "Database";
            var engine = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;
            var tech = !string.IsNullOrEmpty(engine) ? engine : dbType;
            sb.AppendLine($"  ContainerDb({id}, \"{name}\", \"{tech}\", \"Data storage\")");
        }

        // 3. Topics / Queues
        var topicQuery = "MATCH (t:Topic) RETURN t.id AS id, t.name AS name, t.broker_type AS broker";
        var topicJson = await client.ExecuteQueryAsync(topicQuery, null, cancellationToken);
        using var topicDoc = JsonDocument.Parse(topicJson);

        foreach (var row in topicDoc.RootElement.EnumerateArray())
        {
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "topic");
            var name = row.GetProperty("name").GetString() ?? "Topic";
            var broker = row.TryGetProperty("broker", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : "Queue";
            sb.AppendLine($"  ContainerQueue({id}, \"{name}\", \"{broker}\", \"Message / Event Broker\")");
        }

        // 4. External Services
        var extQuery = "MATCH (s:ExternalService) RETURN s.id AS id, s.name AS name UNION MATCH (c:CloudService) RETURN c.id AS id, c.name AS name";
        var extJson = await client.ExecuteQueryAsync(extQuery, null, cancellationToken);
        using var extDoc = JsonDocument.Parse(extJson);

        foreach (var row in extDoc.RootElement.EnumerateArray())
        {
            var id = SanitizeId(row.GetProperty("id").GetString() ?? "ext");
            var name = row.GetProperty("name").GetString() ?? "External Service";
            sb.AppendLine($"  System_Ext({id}, \"{name}\", \"External / Cloud Service\")");
        }

        sb.AppendLine();

        // 5. Relationships
        var relsQuery = """
        MATCH (a)-[r:USES_DB|USES_CLOUD|USES_API]->(b)
        RETURN a.id AS from_id, b.id AS to_id, type(r) AS rel_type
        UNION
        MATCH (t:Topic)-[:PUBLISHED_BY]->(producer)
        RETURN producer.id AS from_id, t.id AS to_id, 'Publishes to' AS rel_type
        UNION
        MATCH (t:Topic)-[:SUBSCRIBED_BY]->(consumer)
        RETURN t.id AS from_id, consumer.id AS to_id, 'Subscribes from' AS rel_type
        """;
        var relsJson = await client.ExecuteQueryAsync(relsQuery, null, cancellationToken);
        using var relsDoc = JsonDocument.Parse(relsJson);

        var addedEdges = new HashSet<string>();
        foreach (var row in relsDoc.RootElement.EnumerateArray())
        {
            var from = SanitizeId(row.GetProperty("from_id").GetString() ?? "");
            var to = SanitizeId(row.GetProperty("to_id").GetString() ?? "");
            var rel = row.GetProperty("rel_type").GetString() ?? "Uses";

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;

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
        MATCH (entity:Type)-[:PERSISTED_IN]->(t:Table)
        RETURN entity.id AS entity_id, entity.name AS entity_name, t.id AS table_id, t.name AS table_name
        """;
        var lineageJson = await client.ExecuteQueryAsync(lineageQuery, null, cancellationToken);
        using var doc = JsonDocument.Parse(lineageJson);

        var tables = new Dictionary<string, string>();
        var entities = new Dictionary<string, string>();
        var edges = new List<(string From, string To)>();

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var entityId = SanitizeId(row.GetProperty("entity_id").GetString() ?? "");
            var entityName = row.GetProperty("entity_name").GetString() ?? "Entity";
            var tableId = SanitizeId(row.GetProperty("table_id").GetString() ?? "");
            var tableName = row.GetProperty("table_name").GetString() ?? "Table";

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
        var cqrsJson = await client.ExecuteQueryAsync(cqrsQuery, null, cancellationToken);
        using var doc = JsonDocument.Parse(cqrsJson);

        var producers = new Dictionary<string, string>();
        var topics = new Dictionary<string, (string Name, string Broker)>();
        var consumers = new Dictionary<string, string>();
        var pubEdges = new HashSet<(string, string)>();
        var subEdges = new HashSet<(string, string)>();

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var pubId = SanitizeId(row.GetProperty("pub_id").GetString() ?? "");
            var pubName = row.GetProperty("pub_name").GetString() ?? "Producer";
            var topicId = SanitizeId(row.GetProperty("topic_id").GetString() ?? "");
            var topicName = row.GetProperty("topic_name").GetString() ?? "Message";
            var broker = row.GetProperty("broker").GetString() ?? "Event";
            var subId = SanitizeId(row.GetProperty("sub_id").GetString() ?? "");
            var subName = row.GetProperty("sub_name").GetString() ?? "Consumer";

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
