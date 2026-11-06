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
        if (string.IsNullOrEmpty(filePath)) return;

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
            ctx.Layer4Log.ConfigParseFailed(relativePath, ex);
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
                      lower.Contains("com.mysql.cj.jdbc.driver") ||
                      lower.Contains("com.mysql.jdbc.driver") ||
                      lowerName.Contains("mysql") ||
                      lower.Contains("port=3306") ||
                      lower.Contains("uid=root");

        var isH2 = lower.StartsWith("jdbc:h2:") ||
                   lower.Contains("org.h2.driver") ||
                   lowerName.Contains("h2");

        if (isPostgres)
        {
            CreateDatabaseNode("PostgreSQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("mongodb://") || lower.StartsWith("mongodb+srv://") || lowerName.Contains("mongo"))
        {
            CreateDatabaseNode("MongoDB", "document", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("redis://") || lower.StartsWith("valkey://") || lower.Contains("localhost:6379") || lowerName.Contains("redis") || lowerName.Contains("valkey"))
        {
            CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (lower.StartsWith("amqp://") || lower.StartsWith("amqps://") || lowerName.Contains("rabbitmq"))
        {
            var ch = ExtractChannelName(connStr, name);
            if (!string.IsNullOrEmpty(ch))
            {
                CreateTopicNode("rabbitmq", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
        }
        else if (lower.Contains(":9092") || lowerName.Contains("kafka"))
        {
            var ch = ExtractChannelName(connStr, name);
            if (!string.IsNullOrEmpty(ch))
            {
                CreateTopicNode("kafka", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            }
        }
        else if (isMySql)
        {
            CreateDatabaseNode("MySQL", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
        }
        else if (isH2)
        {
            CreateDatabaseNode("H2", "relational", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx, name, connStr);
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

        // 4. H2: jdbc:h2:mem:AZ;... or jdbc:h2:file:... or jdbc:h2:tcp://...
        if (engine.Equals("H2", StringComparison.OrdinalIgnoreCase) || connStr.Contains("jdbc:h2:", StringComparison.OrdinalIgnoreCase))
        {
            var h2Match = Regex.Match(connStr, @"jdbc:h2:(?:mem:|file:|tcp://[^/]+/|ssl://[^/]+/)?([^;?#\s]+)", RegexOptions.IgnoreCase);
            if (h2Match.Success)
            {
                var rawDb = h2Match.Groups[1].Value.Trim().Trim('"', '\'');
                var dbName = Path.GetFileNameWithoutExtension(rawDb);
                if (!string.IsNullOrWhiteSpace(dbName) && IsValidCatalogName(dbName)) return dbName;
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

    public static string? ExtractChannelName(string connStr, string defaultName)
    {
        var leaf = ExtractKeyLeaf(defaultName);
        if (!string.IsNullOrWhiteSpace(connStr))
        {
            var uriMatch = Regex.Match(connStr, @"^[a-zA-Z][a-zA-Z0-9+:.-]*://[^/]+/([^?#;\s]+)", RegexOptions.IgnoreCase);
            if (uriMatch.Success)
            {
                var ch = uriMatch.Groups[1].Value.Trim().Trim('"', '\'');
                if (IsValidCatalogName(ch)) return ch;
            }

            var lowerConn = connStr.Trim().ToLowerInvariant();
            if (lowerConn.StartsWith("amqp://") || lowerConn.StartsWith("amqps://") || lowerConn.StartsWith("kafka://"))
            {
                if (IsValidCatalogName(leaf) && !MessageBrokerConfigurationDescriptor.IgnoredInfraKeys.Contains(leaf))
                {
                    return leaf;
                }
            }
        }

        var lowerLeaf = leaf.ToLowerInvariant();
        var lowerDefaultName = defaultName.ToLowerInvariant();

        if (lowerLeaf is "topic" or "topics" or "queue" or "queues" or "channel" or "channels" or "routingkey" or "routing_key" or "destination" or "destinations")
        {
            var valTrimmed = connStr?.Trim().Trim('"', '\'') ?? "";
            if (IsValidCatalogName(valTrimmed)) return valTrimmed;
        }

        if (lowerDefaultName.Contains(".topic.") || lowerDefaultName.Contains(".topics.") ||
            lowerDefaultName.Contains(".queue.") || lowerDefaultName.Contains(".queues.") ||
            lowerDefaultName.Contains(":topic:") || lowerDefaultName.Contains(":topics:") ||
            lowerDefaultName.Contains(":queue:") || lowerDefaultName.Contains(":queues:"))
        {
            var valTrimmed = connStr?.Trim().Trim('"', '\'') ?? "";
            if (IsValidCatalogName(valTrimmed) && !valTrimmed.Equals("true", StringComparison.OrdinalIgnoreCase) && !valTrimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return valTrimmed;
            }
            if (IsValidCatalogName(leaf))
            {
                return leaf;
            }
        }

        if ((lowerLeaf.EndsWith("-topic") || lowerLeaf.EndsWith("_topic") ||
             lowerLeaf.EndsWith("-queue") || lowerLeaf.EndsWith("_queue")) &&
            !string.IsNullOrWhiteSpace(connStr))
        {
            var valTrimmed = connStr.Trim().Trim('"', '\'');
            if (IsValidCatalogName(valTrimmed)) return valTrimmed;
        }

        return null;
    }

    private static string ExtractKeyLeaf(string key)
    {
        var sepIdx = Math.Max(key.LastIndexOf(':'), Math.Max(key.LastIndexOf('.'), key.LastIndexOf('_')));
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
             ResourceNamingHelper.AbstractEngineNames.Contains(leaf) ||
             ResourceNamingHelper.KnownDbEnginesAndNames.Contains(leaf) ||
             ResourceNamingHelper.IsGenericConfigKey(leaf));

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
                !ResourceNamingHelper.AbstractEngineNames.Contains(cleanCustom) &&
                !ResourceNamingHelper.IsGenericConfigKey(cleanCustom))
            {
                dbName = cleanCustom;
            }
        }

        var isCache = string.Equals(dbType, "cache", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(engine, "Redis", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(dbName))
        {
            var rawProjName = (containerNode as CompositeNode)?.RawName ?? containerNode.GetName();
            var isLibContainer = (containerNode is ProjectNode pLib && pLib.IsLibrary) ||
                                 containerNode is LibraryNode ||
                                 (containerNode is ServiceNode { IsLibrary: true } or AppNode { IsLibrary: true } or WorkerNode { IsLibrary: true } or CliToolNode { IsLibrary: true });
            var isTstContainer = (containerNode is ProjectNode pTst && (pTst.IsTest || pTst.EntityKind == ProjectEntityKind.Test || pTst.Role == "Test")) ||
                                 containerNode is TestSuiteNode ||
                                 (containerNode is ServiceNode { Role: "Test" } or AppNode { Role: "Test" } or WorkerNode { Role: "Test" } or CliToolNode { Role: "Test" } or LibraryNode { Role: "Test" });

            if (isCache)
            {
                var domain = (containerNode is ProjectNode or ServiceNode or AppNode or WorkerNode or CliToolNode or LibraryNode or TestSuiteNode)
                    ? ResourceNamingHelper.CleanProjectNameToDomain(rawProjName)
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
                    var domain = ResourceNamingHelper.CleanProjectNameToDomain(rawProjName);
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
                ResourceNamingHelper.IsAbstractEngineOnlyDatabase(dbName, catalog, dbType, null))
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
                              (containerNode is ServiceNode { Role: "Test" } or AppNode { Role: "Test" } or WorkerNode { Role: "Test" } or CliToolNode { Role: "Test" } or LibraryNode { Role: "Test" }) ||
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
            var dbNode = new DatabaseNode(dbId, dbName, relativePath, dbType)
            {
                Engine = engine,
                FromConfig = true,
                ConfigKey = customName ?? "",
                Schema = !string.IsNullOrWhiteSpace(catalog) ? catalog : null,
                IsTest = isTestContainer ? true : null,
                Role = isTestContainer ? "Test" : null
            };
            targetTreeContainer.Children.Add(dbNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, dbName, dbId);
        }

        var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(dbId, fileNodeId)
        {
            FilePath = relativePath,
            Operation = "CONFIG",
            EvidenceKind = OntologyConstants.EvidenceKinds.Config
        });
        relationships.Add(cfgRel);

        if (!string.IsNullOrEmpty(workloadId))
        {
            var usesDbRel = Relationship.FromRelationship(new UsesDbRelationship(workloadId, dbId)
            {
                FilePath = relativePath,
                Operation = "CONFIG",
                EvidenceKind = OntologyConstants.EvidenceKinds.Config
            });
            EvidenceProvenanceHelper.RecordContributingEdge(usesDbRel, cfgRel);
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

        var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(topicId, fileNodeId)
        {
            FilePath = relativePath,
            Operation = "CONFIG",
            EvidenceKind = OntologyConstants.EvidenceKinds.Config
        });
        relationships.Add(cfgRel);

        var (_, workloadId, _) = ResolveContainerIds(containerNode, workspaceId, ctx);

        if (!string.IsNullOrEmpty(workloadId))
        {
            var pubRel = Relationship.FromRelationship(new PublishesToRelationship(workloadId, topicId)
            {
                FilePath = relativePath,
                Operation = "CONFIG",
                EvidenceKind = OntologyConstants.EvidenceKinds.Config
            });
            EvidenceProvenanceHelper.RecordContributingEdge(pubRel, cfgRel);
            relationships.Add(pubRel);
            ctx.AddGlobalProjectDependency(pubRel);

            var triggersRel = Relationship.FromRelationship(new TriggersRelationship(topicId, workloadId)
            {
                FilePath = relativePath,
                Operation = "CONFIG",
                EvidenceKind = OntologyConstants.EvidenceKinds.Config
            });
            EvidenceProvenanceHelper.RecordContributingEdge(triggersRel, cfgRel);
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

        var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(cloudId, fileNodeId)
        {
            FilePath = relativePath,
            Operation = "CONFIG",
            EvidenceKind = OntologyConstants.EvidenceKinds.Config
        });
        relationships.Add(cfgRel);

        var (_, workloadId, _) = ResolveContainerIds(containerNode, workspaceId, ctx);

        if (!string.IsNullOrEmpty(workloadId))
        {
            var usesCloudRel = Relationship.FromRelationship(new UsesCloudRelationship(workloadId, cloudId)
            {
                FilePath = relativePath,
                Operation = "CONFIG",
                EvidenceKind = OntologyConstants.EvidenceKinds.Config
            });
            EvidenceProvenanceHelper.RecordContributingEdge(usesCloudRel, cfgRel);
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
            var comp = (CompositeNode)containerNode;
            var pid = comp.ProjectId;
            var pname = comp.RawName ?? containerNode.GetName();
            return (pid, containerNode.Id, pname);
        }

        if (containerNode is ProjectNode pn)
        {
            string? wlId = null;
            if (ctx.SemanticStructure != null)
            {
                wlId = ctx.SemanticStructure.Children
                    .FirstOrDefault(c => c is CompositeNode comp &&
                                         string.Equals(comp.ProjectId, pn.Id, StringComparison.OrdinalIgnoreCase))?.Id;
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
                    "/"
                )
                {
                    FilePath = relativePath,
                    BaseUrl = ep.Url,
                    IsExternal = true,
                    ConfigKey = ep.ConfigKey
                };

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

                var cfgRel = Relationship.FromRelationship(new DeclaredInRelationship(extId, fileNodeId)
                {
                    FilePath = relativePath,
                    Operation = "CONFIG",
                    EvidenceKind = OntologyConstants.EvidenceKinds.Config
                });
                relationships.Add(cfgRel);

                if (!string.IsNullOrEmpty(workloadId))
                {
                    var callRel = new Relationship(workloadId, extId, OntologyConstants.Relationships.ServiceCall)
                    {
                        DependencyType = "service_call",
                        IsSemantic = true,
                        IsExternal = true,
                        ConfigKey = ep.ConfigKey,
                        Url = ep.Url,
                        FilePath = relativePath,
                        Operation = "CONFIG",
                        EvidenceKind = OntologyConstants.EvidenceKinds.Config
                    };
                    EvidenceProvenanceHelper.RecordContributingEdge(callRel, cfgRel);
                    relationships.Add(callRel);
                    ctx.AddGlobalProjectDependency(callRel);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(workloadId)) continue;

                var targetId = $"{workspaceId}:service_target:{ep.ServiceName.ToLowerInvariant()}";
                var rel = new Relationship(workloadId, targetId, OntologyConstants.Relationships.ServiceCall)
                {
                    ServiceKey = ep.ServiceName,
                    Url = ep.Url,
                    DependencyType = "service_call",
                    IsSemantic = true,
                    ConfigKey = ep.ConfigKey,
                    FilePath = relativePath,
                    Operation = "CONFIG",
                    EvidenceKind = OntologyConstants.EvidenceKinds.Config
                };
                relationships.Add(rel);
                ctx.AddGlobalProjectDependency(rel);
            }
        }
    }
}
