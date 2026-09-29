using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Java.Libraries;

/// <summary>
/// Configuration descriptor for Spring Boot application.properties and application.yml settings.
/// Encapsulates Spring-specific datasource, messaging, and cache conventions away from Core.
/// </summary>
public sealed class SpringFrameworkConfigurationDescriptor : ILibraryConfigurationDescriptor
{
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
        if (key.StartsWith("spring.datasource.url", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("spring.data.mongodb.uri", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("spring.redis.url", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("spring.rabbitmq.addresses", StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith("spring.kafka.bootstrap-servers", StringComparison.OrdinalIgnoreCase))
        {
            ConfigurationParser.InferAndCreateServiceFromConnectionString(key, value, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        if (key.StartsWith("spring.data.redis.host", StringComparison.OrdinalIgnoreCase))
        {
            ConfigurationParser.CreateDatabaseNode("Redis", "cache", relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        if (key.StartsWith("spring.rabbitmq.host", StringComparison.OrdinalIgnoreCase))
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
