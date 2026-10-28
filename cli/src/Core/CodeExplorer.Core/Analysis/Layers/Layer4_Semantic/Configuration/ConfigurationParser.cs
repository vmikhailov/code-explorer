using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public static class ConfigurationParser
{
    public static bool IsConfigurationFile(string fileName) => ConfigurationIndex.IsConfigurationFile(fileName);

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
            fileName.Equals("compose.yml") || fileName.Equals("compose.yaml") ||
            fileName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase) ||
            WorkspaceConventions.IsTestFilePath(relativePath))
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

            // Entries were parsed once by shared file parsers (Layer 2) and registered in Layer 3
            var entries = ConfigurationIndex.GetFileEntries(filePath);

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

        var isPostgres = lower.StartsWith("postgres://") ||
                         lower.StartsWith("postgresql://") ||
                         lower.Contains("jdbc:postgresql://") ||
                         lowerName.Contains("postgres") ||
                         lowerName.Contains("npgsql") ||
                         lower.Contains("port=5432") ||
                         lower.Contains("user id=postgres") ||
                         lower.Contains("username=postgres") ||
                         lower.Contains("include error detail=") ||
                         lower.Contains("search path=") ||
                         lower.Contains("searchpath=") ||
                         ((lower.Contains("host=") || lower.Contains("server=")) && lower.Contains("database=") && (lower.Contains("username=") || lower.Contains("user id=postgres") || lower.Contains("port=5432")));

        var isMySql = lower.StartsWith("mysql://") ||
                      lower.StartsWith("jdbc:mysql://") ||
                      lowerName.Contains("mysql") ||
                      lower.Contains("port=3306") ||
                      lower.Contains("uid=root");

        if (isPostgres)
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
        else if (isMySql)
        {
            CreateDatabaseNode("MySQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.Contains("data source=") || lower.Contains("server=") || lower.StartsWith("jdbc:sqlserver://") || lowerName.Contains("sqlserver") || lowerName.Contains("mssql"))
        {
            CreateDatabaseNode("SQL Server", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
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
        var leaf = ExtractKeyLeaf(defaultName);
        if (string.IsNullOrWhiteSpace(connStr)) return leaf;
        var uriMatch = Regex.Match(connStr, @"^[a-zA-Z][a-zA-Z0-9+:.-]*://[^/]+/([^?#;\s]+)", RegexOptions.IgnoreCase);
        if (uriMatch.Success)
        {
            var ch = uriMatch.Groups[1].Value.Trim().Trim('"', '\'');
            if (IsValidCatalogName(ch)) return ch;
        }
        return leaf;
    }

    private static string ExtractKeyLeaf(string key)
    {
        var sepIdx = Math.Max(key.LastIndexOf(':'), key.LastIndexOf('.'));
        return sepIdx >= 0 && sepIdx < key.Length - 1 ? key[(sepIdx + 1)..] : key;
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

        var leaf = !string.IsNullOrWhiteSpace(customName) ? ExtractKeyLeaf(customName) : null;
        var isGenericConfigKey = !string.IsNullOrWhiteSpace(leaf) &&
            (leaf.Equals("database", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals("defaultconnection", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals("connectionstring", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals("connectionstrings", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals("spring-datasource", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals("spring.datasource.url", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals("db", StringComparison.OrdinalIgnoreCase) ||
             leaf.Equals(engine, StringComparison.OrdinalIgnoreCase) ||
             ResourceReconciliationService.AbstractEngineNames.Contains(leaf) ||
             ResourceReconciliationService.KnownDbEnginesAndNames.Contains(leaf) ||
             ResourceReconciliationService.IsGenericConfigKey(leaf));

        string? dbName = null;
        if (!string.IsNullOrWhiteSpace(catalog))
        {
            dbName = catalog;
        }
        else if (!isGenericConfigKey && !string.IsNullOrWhiteSpace(leaf))
        {
            var cleanCustom = leaf;
            if (cleanCustom.EndsWith("Options", StringComparison.OrdinalIgnoreCase) && cleanCustom.Length > "Options".Length)
            {
                cleanCustom = cleanCustom[..^"Options".Length];
            }
            if (!cleanCustom.Equals(engine, StringComparison.OrdinalIgnoreCase) &&
                !ResourceReconciliationService.AbstractEngineNames.Contains(cleanCustom) &&
                !ResourceReconciliationService.IsGenericConfigKey(cleanCustom))
            {
                dbName = cleanCustom;
            }
        }

        var isCache = string.Equals(dbType, "cache", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(engine, "Redis", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(dbName))
        {
            var rawProjName = containerNode.Extensions?.GetValueOrDefault("raw_name") ?? containerNode.GetName();
            var isLibContainer = (containerNode is ProjectNode pLib && pLib.IsLibrary) ||
                                 containerNode is LibraryNode ||
                                 (containerNode.Extensions != null && containerNode.Extensions.TryGetValue("is_library", out var il) && il == "true");
            var isTstContainer = (containerNode is ProjectNode pTst && (pTst.IsTest || pTst.EntityKind == ProjectEntityKind.Test || pTst.Role == "Test")) ||
                                 containerNode is TestSuiteNode ||
                                 (containerNode.Extensions != null && containerNode.Extensions.TryGetValue("role", out var rl) && rl == "Test");

            if (isCache)
            {
                var domain = (containerNode is ProjectNode or ServiceNode or AppNode or WorkerNode or CliToolNode or LibraryNode or TestSuiteNode)
                    ? ResourceReconciliationService.CleanProjectNameToDomain(rawProjName)
                    : "";
                if (!string.IsNullOrEmpty(domain))
                {
                    dbName = $"Redis.{domain}";
                    catalog ??= domain;
                }
                else
                {
                    dbName = "Redis.cache";
                }
            }
            else
            {
                if ((containerNode is ProjectNode or ServiceNode or AppNode or WorkerNode or CliToolNode) && !isLibContainer && !isTstContainer)
                {
                    var domain = ResourceReconciliationService.CleanProjectNameToDomain(rawProjName);
                    if (!string.IsNullOrWhiteSpace(domain))
                    {
                        dbName = WorkspaceConventions.NormalizeServiceName(domain);
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(dbName))
        {
            return;
        }

        if (!isCache)
        {
            if (string.Equals(dbName, engine, StringComparison.OrdinalIgnoreCase) ||
                ResourceReconciliationService.IsAbstractEngineOnlyDatabase(dbName, catalog, dbType, null))
            {
                return;
            }
        }

        var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:{dbType}:{dbName.ToLowerInvariant()}";

        var aliases = new List<string>();
        if (!string.IsNullOrWhiteSpace(customName))
        {
            aliases.Add(customName);
            if (!string.IsNullOrWhiteSpace(leaf) && !aliases.Contains(leaf, StringComparer.OrdinalIgnoreCase))
            {
                aliases.Add(leaf);
            }
            var colIdx = customName.IndexOf(':');
            if (colIdx > 0)
            {
                var section = customName[..colIdx];
                if (!string.IsNullOrWhiteSpace(section) && !aliases.Contains(section, StringComparer.OrdinalIgnoreCase))
                {
                    aliases.Add(section);
                }
            }
            var dotIdx = customName.IndexOf('.');
            if (dotIdx > 0)
            {
                var section = customName[..dotIdx];
                if (!string.IsNullOrWhiteSpace(section) && !aliases.Contains(section, StringComparer.OrdinalIgnoreCase))
                {
                    aliases.Add(section);
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(catalog)) aliases.Add(catalog);
        if (!string.IsNullOrWhiteSpace(engine)) aliases.Add(engine);

        var (projId, workloadId, _) = ResolveContainerIds(containerNode, workspaceId, ctx);

        var isTestContainer = (containerNode is ProjectNode pNode && (pNode.IsTest || pNode.EntityKind == ProjectEntityKind.Test || pNode.Role == "Test")) ||
                              containerNode is TestSuiteNode ||
                              (containerNode.Extensions != null && containerNode.Extensions.TryGetValue("role", out var rRole) && rRole == "Test") ||
                              WorkspaceConventions.IsTestFilePath(relativePath);

        var regProps = new Dictionary<string, string>
        {
            ["from_config"] = "true",
            ["config_key"] = customName ?? ""
        };
        if (!string.IsNullOrWhiteSpace(catalog))
        {
            regProps["schema"] = catalog;
        }
        if (isTestContainer)
        {
            regProps["is_test"] = "true";
            regProps["role"] = "Test";
        }

        ctx.ResourceRegistry.RegisterResource(
            workspaceId,
            dbName,
            engine,
            dbType,
            OntologyConstants.NodeLabels.Database,
            relativePath,
            projId,
            aliases,
            regProps
        );

        var targetTreeContainer = ctx.SemanticStructure ?? containerNode;
        if (!targetTreeContainer.Children.Any(c => c.Id == dbId))
        {
            var ext = new Dictionary<string, string>
            {
                ["engine"] = engine,
                ["from_config"] = "true",
                ["config_key"] = customName ?? ""
            };
            if (!string.IsNullOrWhiteSpace(catalog))
            {
                ext["schema"] = catalog;
            }
            if (isTestContainer)
            {
                ext["is_test"] = "true";
                ext["role"] = "Test";
            }
            var dbNode = new DatabaseNode(dbId, dbName, relativePath, dbType, ext);
            targetTreeContainer.Children.Add(dbNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, dbName, dbId);
        }

        var cfgExt = new Dictionary<string, string>
        {
            [OntologyConstants.PropertyNames.FilePath] = relativePath,
            [OntologyConstants.PropertyNames.Operation] = "CONFIG",
            [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Config
        };
        var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(dbId, fileNodeId, cfgExt));
        relationships.Add(cfgRel);

        if (!string.IsNullOrEmpty(workloadId))
        {
            var usesDbRel = Relationship.FromRelationship(new UsesDbRelationship(workloadId, dbId, cfgExt));
            EvidenceProvenanceHelper.RecordContributingEdge(usesDbRel.Properties, cfgRel);
            relationships.Add(usesDbRel);
            ctx.AddGlobalProjectDependency(usesDbRel);
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
        var topicId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Topic}:{brokerType}:{topicName}";

        var targetTreeContainer = ctx.SemanticStructure ?? containerNode;
        if (!targetTreeContainer.Children.Any(c => c.Id == topicId))
        {
            var topicNode = new TopicNode(topicId, topicName, relativePath, brokerType);
            targetTreeContainer.Children.Add(topicNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, topicName, topicId);
        }

        var cfgExt = new Dictionary<string, string>
        {
            [OntologyConstants.PropertyNames.FilePath] = relativePath,
            [OntologyConstants.PropertyNames.Operation] = "CONFIG",
            [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Config
        };
        var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(topicId, fileNodeId, cfgExt));
        relationships.Add(cfgRel);

        var (_, workloadId, _) = ResolveContainerIds(containerNode, workspaceId, ctx);

        if (!string.IsNullOrEmpty(workloadId))
        {
            var pubRel = Relationship.FromRelationship(new PublishesToRelationship(workloadId, topicId, cfgExt));
            EvidenceProvenanceHelper.RecordContributingEdge(pubRel.Properties, cfgRel);
            relationships.Add(pubRel);
            ctx.AddGlobalProjectDependency(pubRel);

            var triggersRel = Relationship.FromRelationship(new TriggersRelationship(topicId, workloadId, cfgExt));
            EvidenceProvenanceHelper.RecordContributingEdge(triggersRel.Properties, cfgRel);
            relationships.Add(triggersRel);
            ctx.AddGlobalProjectDependency(triggersRel);
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

        var targetTreeContainer = ctx.SemanticStructure ?? containerNode;
        if (!targetTreeContainer.Children.Any(c => c.Id == cloudId))
        {
            var cloudNode = new CloudServiceNode(cloudId, cloudName, "CloudService", relativePath);
            targetTreeContainer.Children.Add(cloudNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.CloudService, cloudName, cloudId);
        }

        var cfgExt = new Dictionary<string, string>
        {
            [OntologyConstants.PropertyNames.FilePath] = relativePath,
            [OntologyConstants.PropertyNames.Operation] = "CONFIG",
            [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Config
        };
        var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(cloudId, fileNodeId, cfgExt));
        relationships.Add(cfgRel);

        var (_, workloadId, _) = ResolveContainerIds(containerNode, workspaceId, ctx);

        if (!string.IsNullOrEmpty(workloadId))
        {
            var usesCloudRel = Relationship.FromRelationship(new UsesCloudRelationship(workloadId, cloudId, cfgExt));
            EvidenceProvenanceHelper.RecordContributingEdge(usesCloudRel.Properties, cfgRel);
            relationships.Add(usesCloudRel);
            ctx.AddGlobalProjectDependency(usesCloudRel);
        }
    }

    private static (string? ProjectId, string? WorkloadId, string? ProjectName) ResolveContainerIds(
        IOntologyNode containerNode,
        string workspaceId,
        ParsingContext ctx)
    {
        if (containerNode is SemanticStructureNode)
        {
            return (null, null, null);
        }

        if (containerNode is ServiceNode or AppNode or WorkerNode or CliToolNode or LibraryNode or TestSuiteNode)
        {
            var pid = containerNode.Extensions?.GetValueOrDefault("project_id");
            var pname = containerNode.Extensions?.GetValueOrDefault("raw_name") ?? containerNode.GetName();
            return (pid, containerNode.Id, pname);
        }

        if (containerNode is ProjectNode pn)
        {
            string? wlId = null;
            if (ctx.SemanticStructure != null)
            {
                wlId = ctx.SemanticStructure.Children
                    .FirstOrDefault(c => c.Extensions != null &&
                                         c.Extensions.TryGetValue("project_id", out var p) &&
                                         string.Equals(p, pn.Id, StringComparison.OrdinalIgnoreCase))?.Id;
            }
            wlId ??= $"{workspaceId}:{(pn.IsTest ? OntologyConstants.IdPrefixes.TestSuite : pn.IsLibrary ? OntologyConstants.IdPrefixes.Library : OntologyConstants.IdPrefixes.Service)}:{pn.Name}";
            return (pn.Id, wlId, pn.Name);
        }

        if (containerNode.Id.Contains($":{OntologyConstants.IdPrefixes.Project}:") || containerNode.Id.Contains(":project:"))
        {
            var id = containerNode.Id;
            if (id.EndsWith("project_semantic")) id = id[..^"project_semantic".Length];
            if (!id.EndsWith(':')) id += ":";
            var parts = id.Split(':', StringSplitOptions.RemoveEmptyEntries);
            var projName = parts.Length > 0 ? Path.GetFileName(parts[^1].TrimEnd('/')) : null;
            var wlId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Service}:{projName ?? containerNode.GetName()}";
            return (id, wlId, projName);
        }

        return (null, null, null);
    }

    private static void ParseDiscoveredConfigEndpoints(
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var (_, workloadId, projName) = ResolveContainerIds(containerNode, workspaceId, ctx);

        var discovered = ConfigurationIndex.GetDiscoveredUrls(projName);
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

                if (containerNode is not ProjectNode && !containerNode.Children.Any(c => c.Id == extId))
                {
                    containerNode.Children.Add(extNode);
                    ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.ExternalService, ep.ServiceName, extId);
                }

                if (ctx.SemanticStructure != null && !ctx.SemanticStructure.Children.Any(c => c.Id == extId))
                {
                    ctx.SemanticStructure.Children.Add(extNode);
                    ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.ExternalService, ep.ServiceName, extId);
                }

                var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(
                    extId,
                    fileNodeId,
                    new Dictionary<string, string>
                    {
                        [OntologyConstants.PropertyNames.FilePath] = relativePath,
                        [OntologyConstants.PropertyNames.Operation] = "CONFIG",
                        [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Config
                    }));
                relationships.Add(cfgRel);

                if (!string.IsNullOrEmpty(workloadId))
                {
                    var callProps = new Dictionary<string, object>
                    {
                        ["dependency_type"] = "service_call",
                        ["is_semantic"] = "true",
                        ["is_external"] = "true",
                        ["config_key"] = ep.ConfigKey,
                        ["url"] = ep.Url,
                        [OntologyConstants.PropertyNames.FilePath] = relativePath,
                        [OntologyConstants.PropertyNames.Operation] = "CONFIG",
                        [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Config
                    };
                    EvidenceProvenanceHelper.RecordContributingEdge(callProps, cfgRel);
                    var callRel = new Relationship(
                        workloadId,
                        extId,
                        OntologyConstants.Relationships.ServiceCall,
                        callProps);
                    relationships.Add(callRel);
                    ctx.AddGlobalProjectDependency(callRel);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(workloadId)) continue;

                var targetId = $"{workspaceId}:service_target:{ep.ServiceName.ToLowerInvariant()}";
                var rel = new Relationship(
                    workloadId,
                    targetId,
                    OntologyConstants.Relationships.ServiceCall,
                    new Dictionary<string, object>
                    {
                        ["service_key"] = ep.ServiceName,
                        ["url"] = ep.Url,
                        ["dependency_type"] = "service_call",
                        ["is_semantic"] = "true",
                        ["config_key"] = ep.ConfigKey,
                        [OntologyConstants.PropertyNames.FilePath] = relativePath,
                        [OntologyConstants.PropertyNames.Operation] = "CONFIG",
                        [OntologyConstants.PropertyNames.EvidenceKind] = OntologyConstants.EvidenceKinds.Config
                    });
                relationships.Add(rel);
                ctx.AddGlobalProjectDependency(rel);
            }
        }
    }
}
