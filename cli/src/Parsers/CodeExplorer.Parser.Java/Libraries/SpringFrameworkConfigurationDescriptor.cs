using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Parser.Java.Libraries;

/// <summary>
/// Configuration descriptor for Spring Boot application.properties and application.yml settings.
/// Encapsulates Spring-specific datasource, messaging, and cache conventions away from Core.
/// </summary>
public sealed class SpringFrameworkConfigurationDescriptor : ILibraryConfigurationDescriptor
{
    public int Order => 10;

    private const string ServerServletContextPath = "server.servlet.context-path";
    private const string ServerContextPath = "server.context-path";
    private const string ServerServletContextPathAlt = "server.servlet.contextpath";

    private const string KeySpringDatasourceUrl = "spring.datasource.url";
    private const string KeySpringDataMongoUri = "spring.data.mongodb.uri";
    private const string KeySpringRedisUrl = "spring.redis.url";
    private const string KeySpringRabbitAddresses = "spring.rabbitmq.addresses";
    private const string KeySpringKafkaBootstrapServers = "spring.kafka.bootstrap-servers";

    private const string KeySpringDataMongoHost = "spring.data.mongodb.host";
    private const string KeySpringDataMongoDatabase = "spring.data.mongodb.database";
    private const string KeySpringDataRedisHost = "spring.data.redis.host";
    private const string KeySpringRabbitHost = "spring.rabbitmq.host";

    private const string MongoDb = "MongoDB";
    private const string MongoTypeDocument = "document";
    private const string Redis = "Redis";
    private const string RedisTypeCache = "cache";
    private const string RabbitMq = "rabbitmq";
    private const string RabbitTopicDefault = "default";
    private const string SpringDatasource = "spring-datasource";

    private const string KeyUrl = "url";
    private const string KeyUri = "uri";
    private const string KeyBootstrapServers = "bootstrap-servers";
    private const string KeyAddresses = "addresses";

    public bool TryHandle(
        string key,
        string value,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        var normKey = key.Replace(':', '.');

        var effectiveContainer = containerNode;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
        if (ctx.ProjectsStructure != null && !string.IsNullOrEmpty(fileNameWithoutExt))
        {
            var targetProj = ctx.ProjectsStructure.Children.OfType<ProjectNode>()
                .FirstOrDefault(p => string.Equals(p.Name, fileNameWithoutExt, StringComparison.OrdinalIgnoreCase));
            if (targetProj != null)
            {
                effectiveContainer = targetProj;
            }
        }

        var targetProjectName = (effectiveContainer as ProjectNode)?.Name ?? fileNameWithoutExt;

        if (normKey.Equals(ServerServletContextPath, StringComparison.OrdinalIgnoreCase) ||
            normKey.Equals(ServerContextPath, StringComparison.OrdinalIgnoreCase) ||
            normKey.Equals(ServerServletContextPathAlt, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = value.Trim().Trim('"', '\'').Trim('/');
            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(targetProjectName))
            {
                RouteDictionaryRegistry.RegisterGlobalPrefix(targetProjectName, prefix);
                return true;
            }
        }

        if (normKey.StartWithIC(KeySpringDatasourceUrl) ||
            normKey.StartWithIC(KeySpringDataMongoUri) ||
            normKey.StartWithIC(KeySpringRedisUrl) ||
            normKey.StartWithIC(KeySpringRabbitAddresses) ||
            normKey.StartWithIC(KeySpringKafkaBootstrapServers))
        {
            ConfigurationParser.InferAndCreateServiceFromConnectionString(key, value, relativePath, fileNodeId, workspaceId, effectiveContainer, relationships, ctx);
            return true;
        }

        if (normKey.StartWithIC(KeySpringDataMongoHost))
        {
            ConfigurationParser.CreateDatabaseNode(MongoDb, MongoTypeDocument, relativePath, fileNodeId, workspaceId, effectiveContainer, relationships, ctx, customName: MongoDb);
            return true;
        }

        if (normKey.StartWithIC(KeySpringDataMongoDatabase))
        {
            ConfigurationParser.CreateDatabaseNode(MongoDb, MongoTypeDocument, relativePath, fileNodeId, workspaceId, effectiveContainer, relationships, ctx, customName: value);
            return true;
        }

        if (normKey.StartWithIC(KeySpringDataRedisHost))
        {
            ConfigurationParser.CreateDatabaseNode(Redis, RedisTypeCache, relativePath, fileNodeId, workspaceId, effectiveContainer, relationships, ctx);
            return true;
        }

        if (normKey.StartWithIC(KeySpringRabbitHost))
        {
            ConfigurationParser.CreateTopicNode(RabbitMq, RabbitTopicDefault, relativePath, fileNodeId, workspaceId, effectiveContainer, relationships, ctx);
            return true;
        }

        if (key.Equals(KeyUrl, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(KeyUri, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(KeyBootstrapServers, StringComparison.OrdinalIgnoreCase) ||
            key.Equals(KeyAddresses, StringComparison.OrdinalIgnoreCase))
        {
            ConfigurationParser.InferAndCreateServiceFromConnectionString(SpringDatasource, value, relativePath, fileNodeId, workspaceId, effectiveContainer, relationships, ctx);
            return true;
        }

        return false;
    }
}
