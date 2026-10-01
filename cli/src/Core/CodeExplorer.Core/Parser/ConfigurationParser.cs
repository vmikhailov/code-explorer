using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public static class ConfigurationParser
{
    public static bool IsConfigurationFile(string fileName) => ConfigStore.IsConfigurationFile(fileName);

    public static void ParseAndEnrich(
        string filePath,
        string relativePath,
        string workspaceId,
        IOntologyNode containerSemanticNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        if (!File.Exists(filePath)) return;

        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        if (fileName.Contains("docker-compose") || fileName.StartsWith("compose.") || fileName.StartsWith("compose-") ||
            fileName.Equals("compose.yml") || fileName.Equals("compose.yaml"))
        {
            return; // docker-compose / compose describes local dev containers, not production architecture
        }

        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";

        try
        {
            string? projName = null;
            if (containerSemanticNode is ProjectNode pn)
            {
                projName = pn.Name;
            }

            // Retrieve preloaded configuration entries from ConfigStore (single-pass I/O in Layer 3)
            var entries = ConfigStore.GetFileEntries(filePath);
            if (entries.Count == 0)
            {
                ConfigStore.LoadFile(filePath, projName, ctx);
                entries = ConfigStore.GetFileEntries(filePath);
            }

            // Process all configuration entries through the pluggable descriptor pipeline
            foreach (var entry in entries)
            {
                LibraryConfigurationRegistry.TryProcess(
                    entry.Key,
                    entry.Value,
                    relativePath,
                    fileNodeId,
                    workspaceId,
                    containerSemanticNode,
                    relationships,
                    ctx);
            }

            // Generic discovered config endpoints (Service calls & external APIs)
            ParseDiscoveredConfigEndpoints(relativePath, fileNodeId, workspaceId, containerSemanticNode, relationships, ctx);
        }
        catch (Exception ex)
        {
            ctx.LogWarning($"[ConfigurationParser] Failed to parse config file '{relativePath}': {ex.Message}");
        }
    }

    public static void InferAndCreateServiceFromConnectionString(
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

        // 2. URI format: (postgres|postgresql|mongodb|mysql|mariadb|redis)://.../[dbname] (including jdbc: prefixes)
        var uriMatch = Regex.Match(connStr, @"^[a-zA-Z][a-zA-Z0-9+:.-]*://[^/]+/([^?#;\s]+)", RegexOptions.IgnoreCase);
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

    public static string ExtractChannelName(string connStr, string defaultName)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return defaultName;
        var uriMatch = Regex.Match(connStr, @"^[a-zA-Z][a-zA-Z0-9+:.-]*://[^/]+/([^?#;\s]+)", RegexOptions.IgnoreCase);
        if (uriMatch.Success)
        {
            var ch = uriMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (IsValidCatalogName(ch)) return ch;
        }
        return defaultName;
    }

    public static void CreateDatabaseNode(
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

        var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:{dbType}:{dbName.ToLowerInvariant()}";

        var aliases = new List<string>();
        if (!string.IsNullOrWhiteSpace(customName)) aliases.Add(customName);
        if (!string.IsNullOrWhiteSpace(catalog)) aliases.Add(catalog);
        if (!string.IsNullOrWhiteSpace(engine)) aliases.Add(engine);

        string? projId = null;
        if (containerNode is ProjectNode pn)
        {
            projId = pn.Id;
        }
        else if (containerNode.Id.Contains($":{OntologyConstants.IdPrefixes.Project}:") || containerNode.Id.Contains(":project:"))
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

    public static void CreateTopicNode(
        string brokerType,
        string topicName,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var topicId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:{brokerType}:{topicName.ToLowerInvariant()}";

        if (!containerNode.Children.Any(c => c.Id == topicId))
        {
            var topicNode = new TopicNode(topicId, topicName, relativePath, brokerType);
            containerNode.Children.Add(topicNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, topicName, topicId);
        }

        relationships.Add(Relationship.FromRelationship(new ConfiguresRelationship(fileNodeId, topicId)));

        string? projId = null;
        if (containerNode is ProjectNode pn)
        {
            projId = pn.Id;
        }
        else if (containerNode.Id.Contains($":{OntologyConstants.IdPrefixes.Project}:") || containerNode.Id.Contains(":project:"))
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

    public static void CreateCloudServiceNode(
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

        string? projId = null;
        if (containerNode is ProjectNode pn)
        {
            projId = pn.Id;
        }
        else if (containerNode.Id.Contains($":{OntologyConstants.IdPrefixes.Project}:") || containerNode.Id.Contains(":project:"))
        {
            var id = containerNode.Id;
            if (id.EndsWith("project_semantic")) id = id[..^"project_semantic".Length];
            if (!id.EndsWith(':')) id += ":";
            projId = id;
        }

        if (!string.IsNullOrEmpty(projId))
        {
            var usesCloudRel = new UsesCloudRelationship(projId, cloudId);
            relationships.Add(Relationship.FromRelationship(usesCloudRel));
            ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesCloudRel));
        }
    }

    private static void ParseDiscoveredConfigEndpoints(
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        string? projId = null;
        string? projName = null;
        if (containerNode is ProjectNode pn)
        {
            projId = pn.Id;
            projName = pn.Name;
        }
        else if (containerNode.Id.Contains($":{OntologyConstants.IdPrefixes.Project}:") || containerNode.Id.Contains(":project:"))
        {
            var id = containerNode.Id;
            if (id.EndsWith("project_semantic")) id = id[..^"project_semantic".Length];
            if (!id.EndsWith(':')) id += ":";
            var parts = id.Split(':', StringSplitOptions.RemoveEmptyEntries);
            projName = parts.Length > 0 ? Path.GetFileName(parts[^1].TrimEnd('/')) : null;
        }

        var discovered = ConfigStore.GetDiscoveredUrls(projName);
        var normRelPath = relativePath.Replace('\\', '/');

        foreach (var ep in discovered)
        {
            if (!string.IsNullOrEmpty(ep.SourceFilePath))
            {
                var normSource = ep.SourceFilePath.Replace('\\', '/');
                if (!normSource.EndsWith(normRelPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (ep.IsExternal)
            {
                var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{ep.Scheme}:{ep.Host}";
                var extNode = new ExternalServiceNode(
                    extId,
                    ep.ServiceName,
                    ep.Scheme,
                    ep.Host,
                    "/",
                    new Dictionary<string, string>
                    {
                        ["file_path"] = relativePath,
                        ["base_url"] = ep.Url,
                        ["is_external"] = "true",
                        ["config_key"] = ep.ConfigKey
                    }
                );

                if (!containerNode.Children.Any(c => c.Id == extId))
                {
                    containerNode.Children.Add(extNode);
                    ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.ExternalService, ep.ServiceName, extId);
                }

                if (ctx.SemanticStructure != null && !ctx.SemanticStructure.Children.Any(c => c.Id == extId))
                {
                    ctx.SemanticStructure.Children.Add(extNode);
                }

                relationships.Add(Relationship.FromRelationship(new ConfiguresRelationship(fileNodeId, extId)));

                if (!string.IsNullOrEmpty(projId))
                {
                    var callRel = new Relationship(
                        projId,
                        extId,
                        OntologyConstants.Relationships.ServiceCall,
                        new Dictionary<string, object>
                        {
                            ["dependency_type"] = "service_call",
                            ["is_semantic"] = "true",
                            ["is_external"] = "true",
                            ["config_key"] = ep.ConfigKey,
                            ["url"] = ep.Url
                        });
                    relationships.Add(callRel);
                    ctx.AddGlobalProjectDependency(callRel);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(projId)) continue;

                var targetId = $"{workspaceId}:service_target:{ep.ServiceName.ToLowerInvariant()}";
                var rel = new Relationship(
                    projId,
                    targetId,
                    OntologyConstants.Relationships.ServiceCall,
                    new Dictionary<string, object>
                    {
                        ["service_key"] = ep.ServiceName,
                        ["url"] = ep.Url,
                        ["dependency_type"] = "service_call",
                        ["is_semantic"] = "true",
                        ["config_key"] = ep.ConfigKey
                    });
                relationships.Add(rel);
                ctx.AddGlobalProjectDependency(rel);
            }
        }
    }
}
