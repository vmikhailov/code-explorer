using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Parser.Java.Libraries;

/// <summary>
/// Configuration descriptor for Spring Boot application.properties and application.yml settings.
/// Encapsulates Spring-specific datasource, messaging, and cache conventions away from Core.
/// </summary>
public sealed class SpringFrameworkConfigurationDescriptor : ILibraryConfigurationDescriptor
{
    public int Order => 10;

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
        if (key.StartWithIC("spring.datasource.url") ||
            key.StartWithIC("spring.data.mongodb.uri") ||
            key.StartWithIC("spring.redis.url") ||
            key.StartWithIC("spring.rabbitmq.addresses") ||
            key.StartWithIC("spring.kafka.bootstrap-servers"))
        {
            ConfigurationParser.InferAndCreateServiceFromConnectionString(key, value, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        if (key.StartWithIC("spring.data.redis.host"))
        {
            ConfigurationParser.CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        if (key.StartWithIC("spring.rabbitmq.host"))
        {
            ConfigurationParser.CreateTopicNode("rabbitmq", "default", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        if (key.Equals("url", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("uri", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("bootstrap-servers", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("addresses", StringComparison.OrdinalIgnoreCase))
        {
            ConfigurationParser.InferAndCreateServiceFromConnectionString("spring-datasource", value, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        return false;
    }
}
