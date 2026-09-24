using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public static class ConfigurationParser
{
    private static readonly Regex KeyValEnvRegex = new(@"^\s*([A-Za-z_][A-Za-z0-9_.]*)\s*=\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex ComposeServiceRegex = new(@"^\s{2}([A-Za-z0-9_-]+):\s*$", RegexOptions.Compiled);
    private static readonly Regex ComposeImageRegex = new(@"^\s{4}image:\s*([^\s#]+)", RegexOptions.Compiled);

    public static bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith("appsettings") && lower.EndsWith(".json") ||
               lower.StartsWith("docker-compose") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml")) ||
               lower.StartsWith("compose") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml")) ||
               lower.StartsWith(".env") ||
               lower.StartsWith("application") && (lower.EndsWith(".properties") || lower.EndsWith(".yml") || lower.EndsWith(".yaml"));
    }

    public static void ParseAndEnrich(
        string filePath,
        string relativePath,
        string workspaceId,
        IOntologyNode containerSemanticNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        if (!File.Exists(filePath)) return;

        var fileName = Path.GetFileName(filePath);
        var lower = fileName.ToLowerInvariant();
        var fileNodeId = $"{workspaceId}:file:{relativePath}";

        try
        {
            if (lower.StartsWith("appsettings") && lower.EndsWith(".json"))
            {
                ParseAppSettingsJson(filePath, relativePath, fileNodeId, workspaceId, containerSemanticNode, relationships, ctx);
            }
            else if (lower.StartsWith("docker-compose") || lower.StartsWith("compose"))
            {
                ParseDockerCompose(filePath, relativePath, fileNodeId, workspaceId, containerSemanticNode, relationships, ctx);
            }
            else if (lower.StartsWith(".env"))
            {
                ParseDotEnv(filePath, relativePath, fileNodeId, workspaceId, containerSemanticNode, relationships, ctx);
            }
            else if (lower.StartsWith("application") && lower.EndsWith(".properties"))
            {
                ParseApplicationProperties(filePath, relativePath, fileNodeId, workspaceId, containerSemanticNode, relationships, ctx);
            }
            else if (lower.StartsWith("application") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml")))
            {
                ParseApplicationYaml(filePath, relativePath, fileNodeId, workspaceId, containerSemanticNode, relationships, ctx);
            }
        }
        catch (Exception ex)
        {
            ctx.LogWarning($"[ConfigurationParser] Failed to parse config file '{relativePath}': {ex.Message}");
        }
    }

    private static void ParseAppSettingsJson(
        string filePath,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var jsonText = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object) return;

        // 1. ConnectionStrings
        if (root.TryGetProperty("ConnectionStrings", out var connStrings) && connStrings.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in connStrings.EnumerateObject())
            {
                var connName = prop.Name;
                var connVal = prop.Value.GetString() ?? "";
                InferAndCreateServiceFromConnectionString(connName, connVal, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
        }

        // 2. Look for top-level known services (Stripe, Redis, RabbitMQ, Kafka, AWS, OpenAI, Auth0, etc.)
        foreach (var prop in root.EnumerateObject())
        {
            var key = prop.Name;
            if (key.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Logging", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("AllowedHosts", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            InferServiceFromConfigSection(key, prop.Value, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
    }

    private static void ParseDotEnv(
        string filePath,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var match = KeyValEnvRegex.Match(line);
            if (!match.Success) continue;

            var key = match.Groups[1].Value.Trim();
            var val = match.Groups[2].Value.Trim().Trim('"', '\'');

            if (key.Contains("URL", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("CONNECTION", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("HOST", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("BROKER", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("DATABASE", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("REDIS", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("RABBITMQ", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("KAFKA", StringComparison.OrdinalIgnoreCase))
            {
                InferAndCreateServiceFromConnectionString(key, val, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
            else if (key.StartsWith("STRIPE", StringComparison.OrdinalIgnoreCase) ||
                     key.StartsWith("AWS", StringComparison.OrdinalIgnoreCase) ||
                     key.StartsWith("AZURE", StringComparison.OrdinalIgnoreCase) ||
                     key.StartsWith("AUTH0", StringComparison.OrdinalIgnoreCase) ||
                     key.StartsWith("OPENAI", StringComparison.OrdinalIgnoreCase))
            {
                InferServiceFromConfigSection(key, val, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
        }
    }

    private static void ParseApplicationProperties(
        string filePath,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var match = KeyValEnvRegex.Match(line);
            if (!match.Success) continue;

            var key = match.Groups[1].Value.Trim();
            var val = match.Groups[2].Value.Trim().Trim('"', '\'');

            if (key.StartsWith("spring.datasource.url", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("spring.data.mongodb.uri", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("spring.redis.url", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("spring.rabbitmq.addresses", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("spring.kafka.bootstrap-servers", StringComparison.OrdinalIgnoreCase))
            {
                InferAndCreateServiceFromConnectionString(key, val, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
            else if (key.StartsWith("spring.data.redis.host", StringComparison.OrdinalIgnoreCase))
            {
                CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
            else if (key.StartsWith("spring.rabbitmq.host", StringComparison.OrdinalIgnoreCase))
            {
                CreateTopicNode("rabbitmq", "default", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
        }
    }

    private static void ParseApplicationYaml(
        string filePath,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("url:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("uri:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("bootstrap-servers:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("addresses:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(':', 2);
                if (parts.Length == 2)
                {
                    var val = parts[1].Trim().Trim('"', '\'');
                    InferAndCreateServiceFromConnectionString("spring-datasource", val, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
                }
            }
        }
    }

    private static void ParseDockerCompose(
        string filePath,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var lines = File.ReadAllLines(filePath);
        string? currentService = null;

        foreach (var line in lines)
        {
            var svcMatch = ComposeServiceRegex.Match(line);
            if (svcMatch.Success)
            {
                currentService = svcMatch.Groups[1].Value.Trim();
                continue;
            }

            var imgMatch = ComposeImageRegex.Match(line);
            if (imgMatch.Success && !string.IsNullOrEmpty(currentService))
            {
                var image = imgMatch.Groups[1].Value.Trim().ToLowerInvariant();
                InferServiceFromDockerImage(currentService, image, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
        }
    }

    private static void InferServiceFromDockerImage(
        string serviceName,
        string image,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        if (image.Contains("postgres") || image.Contains("timescaledb"))
        {
            CreateDatabaseNode("PostgreSQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, serviceName);
        }
        else if (image.Contains("redis"))
        {
            CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, serviceName);
        }
        else if (image.Contains("mongo"))
        {
            CreateDatabaseNode("MongoDB", "document", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, serviceName);
        }
        else if (image.Contains("mysql") || image.Contains("mariadb"))
        {
            CreateDatabaseNode(image.Contains("mariadb") ? "MariaDB" : "MySQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, serviceName);
        }
        else if (image.Contains("mssql") || image.Contains("sqlserver"))
        {
            CreateDatabaseNode("SQL Server", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, serviceName);
        }
        else if (image.Contains("rabbitmq"))
        {
            CreateTopicNode("rabbitmq", serviceName, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (image.Contains("kafka"))
        {
            CreateTopicNode("kafka", serviceName, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (image.Contains("localstack"))
        {
            CreateCloudServiceNode("AWS (LocalStack)", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
    }

    private static void InferAndCreateServiceFromConnectionString(
        string name,
        string connStr,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var lower = connStr.ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        if (lower.StartsWith("postgres://") || lower.StartsWith("postgresql://") || lower.StartsWith("jdbc:postgresql://") ||
            lower.Contains("host=") && lower.Contains("database=") && (lower.Contains("username=") || lower.Contains("user id=")))
        {
            CreateDatabaseNode("PostgreSQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("mongodb://") || lower.StartsWith("mongodb+srv://") || lowerName.Contains("mongo"))
        {
            CreateDatabaseNode("MongoDB", "document", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("redis://") || lower.Contains("localhost:6379") || lowerName.Contains("redis"))
        {
            CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("amqp://") || lower.StartsWith("amqps://") || lowerName.Contains("rabbitmq"))
        {
            var ch = ExtractChannelName(connStr, name);
            CreateTopicNode("rabbitmq", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains(":9092") || lowerName.Contains("kafka"))
        {
            var ch = ExtractChannelName(connStr, name);
            CreateTopicNode("kafka", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("data source=") || lower.Contains("server=") || lower.StartsWith("jdbc:sqlserver://") || lowerName.Contains("sqlserver") || lowerName.Contains("mssql"))
        {
            CreateDatabaseNode("SQL Server", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("mysql://") || lower.StartsWith("jdbc:mysql://") || lowerName.Contains("mysql"))
        {
            CreateDatabaseNode("MySQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.Contains(".db") || lower.Contains(".sqlite") || lowerName.Contains("sqlite"))
        {
            CreateDatabaseNode("SQLite", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
    }

    private static void InferServiceFromConfigSection(
        string key,
        object value,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var lower = key.ToLowerInvariant();

        if (lower.Contains("stripe"))
        {
            CreateCloudServiceNode("Stripe", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("auth0"))
        {
            CreateCloudServiceNode("Auth0", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("openai"))
        {
            CreateCloudServiceNode("OpenAI", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("aws") || lower.Contains("amazon"))
        {
            CreateCloudServiceNode("AWS", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("azure"))
        {
            CreateCloudServiceNode("Azure", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("redis"))
        {
            CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("rabbitmq"))
        {
            CreateTopicNode("rabbitmq", key, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
        else if (lower.Contains("kafka"))
        {
            CreateTopicNode("kafka", key, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
        }
    }

    public static string? ExtractDatabaseCatalog(string connStr, string engine)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return null;

        // 1. ADO.NET / Standard Key-Value format: Database=xyz or Initial Catalog=xyz
        var dbMatch = Regex.Match(connStr, @"(?:^|;)\s*(?:Database|Initial\s+Catalog)\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        if (dbMatch.Success)
        {
            var db = dbMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (IsValidCatalogName(db)) return db;
        }

        // 2. URI format: (postgres|postgresql|mongodb|mysql|mariadb|redis)://.../[dbname]
        var uriMatch = Regex.Match(connStr, @"^[a-zA-Z][a-zA-Z0-9+.-]*://[^/]+/([^?#;\s]+)", RegexOptions.IgnoreCase);
        if (uriMatch.Success)
        {
            var db = uriMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (IsValidCatalogName(db)) return db;
        }

        // 3. SQLite: Data Source=xyz.db or Filename=xyz.db
        if (engine.Equals("SQLite", StringComparison.OrdinalIgnoreCase) || connStr.Contains(".db", StringComparison.OrdinalIgnoreCase) || connStr.Contains(".sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteMatch = Regex.Match(connStr, @"(?:^|;)\s*(?:Data\s+Source|Filename)\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
            if (sqliteMatch.Success)
            {
                var fullPath = sqliteMatch.Groups[1].Value.Trim().Trim('"', '\'');
                var fileName = Path.GetFileNameWithoutExtension(fullPath);
                if (!string.IsNullOrWhiteSpace(fileName) && IsValidCatalogName(fileName)) return fileName;
            }
        }

        return null;
    }

    private static bool IsValidCatalogName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        // Ignore unexpanded template placeholders like {{DB_NAME}}, ${DB_NAME}, %DB_NAME%, <DB_NAME>
        if (name.StartsWith("{{") || name.StartsWith("${") || name.StartsWith('%') || name.StartsWith('<')) return false;
        return true;
    }

    private static string ExtractChannelName(string connStr, string defaultName)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return defaultName;
        var uriMatch = Regex.Match(connStr, @"^[a-zA-Z][a-zA-Z0-9+.-]*://[^/]+/([^?#;\s]+)", RegexOptions.IgnoreCase);
        if (uriMatch.Success)
        {
            var ch = uriMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (IsValidCatalogName(ch)) return ch;
        }
        return defaultName;
    }

    private static void CreateDatabaseNode(
        string engine,
        string dbType,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx,
        string? customName = null,
        string? connStr = null)
    {
        string? catalog = null;
        if (!string.IsNullOrWhiteSpace(connStr))
        {
            catalog = ExtractDatabaseCatalog(connStr, engine);
        }

        var isGenericConfigKey = !string.IsNullOrWhiteSpace(customName) &&
            (customName.Contains('_') ||
             customName.Contains('.') ||
             customName.Equals("spring-datasource", StringComparison.OrdinalIgnoreCase) ||
             customName.Equals("spring.datasource.url", StringComparison.OrdinalIgnoreCase) ||
             customName.Equals("database", StringComparison.OrdinalIgnoreCase) ||
             customName.Equals("defaultconnection", StringComparison.OrdinalIgnoreCase) ||
             customName.Equals("connectionstring", StringComparison.OrdinalIgnoreCase) ||
             customName.Equals("connectionstrings", StringComparison.OrdinalIgnoreCase) ||
             customName.Equals("db", StringComparison.OrdinalIgnoreCase));

        string dbName;
        if (!string.IsNullOrWhiteSpace(catalog))
        {
            dbName = catalog;
        }
        else if (!isGenericConfigKey && !string.IsNullOrWhiteSpace(customName))
        {
            dbName = customName;
        }
        else
        {
            dbName = engine;
        }

        var dbId = $"{workspaceId}:database:{dbType}:{dbName.ToLowerInvariant()}";

        var aliases = new List<string>();
        if (!string.IsNullOrWhiteSpace(customName)) aliases.Add(customName);
        if (!string.IsNullOrWhiteSpace(catalog)) aliases.Add(catalog);
        if (!string.IsNullOrWhiteSpace(engine)) aliases.Add(engine);

        string? projId = null;
        if (containerNode is ProjectSemanticNode psn)
        {
            projId = $"{workspaceId}:project:{psn.Path}:";
        }
        else if (containerNode.Id.Contains(":project:"))
        {
            var id = containerNode.Id;
            if (id.EndsWith("project_semantic")) id = id[..^"project_semantic".Length];
            if (!id.EndsWith(':')) id += ":";
            projId = id;
        }

        ctx.ResourceRegistry.RegisterResource(
            workspaceId,
            dbName,
            engine,
            dbType,
            OntologyConstants.NodeLabels.Database,
            relativePath,
            projId,
            aliases
        );

        if (!containerNode.Children.Any(c => c.Id == dbId))
        {
            var dbNode = new DatabaseNode(dbId, dbName, relativePath, dbType, new Dictionary<string, string> { ["engine"] = engine });
            containerNode.Children.Add(dbNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, dbName, dbId);
        }

        relationships.Add(Relationship.FromRelationship(new ConfiguresRelationship(fileNodeId, dbId)));

        if (!string.IsNullOrEmpty(projId))
        {
            var usesDbRel = new UsesDbRelationship(projId, dbId);
            relationships.Add(Relationship.FromRelationship(usesDbRel));
            ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesDbRel));
        }
    }

    private static void CreateTopicNode(
        string brokerType,
        string topicName,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var topicId = $"{workspaceId}:topic:{brokerType}:{topicName.ToLowerInvariant()}";

        if (!containerNode.Children.Any(c => c.Id == topicId))
        {
            var topicNode = new TopicNode(topicId, topicName, relativePath, brokerType);
            containerNode.Children.Add(topicNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, topicName, topicId);
        }

        relationships.Add(Relationship.FromRelationship(new ConfiguresRelationship(fileNodeId, topicId)));

        string? projId = null;
        if (containerNode is ProjectSemanticNode psn)
        {
            projId = $"{workspaceId}:project:{psn.Path}:";
        }
        else if (containerNode.Id.Contains(":project:"))
        {
            var id = containerNode.Id;
            if (id.EndsWith("project_semantic")) id = id[..^"project_semantic".Length];
            if (!id.EndsWith(':')) id += ":";
            projId = id;
        }

        if (!string.IsNullOrEmpty(projId))
        {
            var pubRel = new PublishesToRelationship(projId, topicId);
            relationships.Add(Relationship.FromRelationship(pubRel));
            ctx.AddGlobalProjectDependency(Relationship.FromRelationship(pubRel));

            var triggersRel = new TriggersRelationship(topicId, projId);
            relationships.Add(Relationship.FromRelationship(triggersRel));
            ctx.AddGlobalProjectDependency(Relationship.FromRelationship(triggersRel));
        }
    }

    private static void CreateCloudServiceNode(
        string cloudName,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var cloudId = $"{workspaceId}:cloud:{cloudName.ToLowerInvariant().Replace(' ', '_')}";

        if (!containerNode.Children.Any(c => c.Id == cloudId))
        {
            var cloudNode = new CloudServiceNode(cloudId, cloudName, "CloudService", relativePath);
            containerNode.Children.Add(cloudNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.CloudService, cloudName, cloudId);
        }

        relationships.Add(Relationship.FromRelationship(new ConfiguresRelationship(fileNodeId, cloudId)));
    }
}
