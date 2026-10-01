using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Standard descriptor for relational, document, and cache database connection strings.
/// </summary>
public sealed class DatabaseConfigurationDescriptor : ILibraryConfigurationDescriptor
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
        if (string.IsNullOrWhiteSpace(value) || IsDockerCompose(relativePath)) return false;

        var lowerKey = key.ToLowerInvariant();
        var lowerVal = value.ToLowerInvariant();

        // 1. ConnectionStrings sections or explicitly named connection string keys
        var isConnStringKey = lowerKey.StartsWith("connectionstrings:") ||
                              lowerKey.Equals("connectionstring") ||
                              lowerKey.EndsWith(":connectionstring") ||
                              lowerKey.Contains("database_url") ||
                              lowerKey.Contains("db_connection") ||
                              lowerKey.Contains("connection_string");

        // 2. Recognized database URI schemes or ADO.NET connection patterns
        var isDbVal = lowerVal.StartsWith("postgres://") ||
                      lowerVal.StartsWith("postgresql://") ||
                      lowerVal.StartsWith("jdbc:postgresql://") ||
                      lowerVal.StartsWith("mongodb://") ||
                      lowerVal.StartsWith("mongodb+srv://") ||
                      lowerVal.StartsWith("redis://") ||
                      lowerVal.StartsWith("jdbc:sqlserver://") ||
                      lowerVal.StartsWith("jdbc:mysql://") ||
                      lowerVal.StartsWith("mysql://") ||
                      (lowerVal.Contains("data source=") && lowerVal.Contains(".db")) ||
                      (lowerVal.Contains("host=") && lowerVal.Contains("database="));

        if (!isConnStringKey && !isDbVal)
        {
            return false;
        }

        var connName = ExtractKeyLeaf(key);
        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            connName,
            value,
            relativePath,
            fileNodeId,
            workspaceId,
            containerNode,
            relationships,
            ctx);

        return true;
    }

    public int Order => 1000;

    private static string ExtractKeyLeaf(string key)
    {
        var sepIdx = Math.Max(key.LastIndexOf(':'), key.LastIndexOf('.'));
        return sepIdx >= 0 && sepIdx < key.Length - 1 ? key[(sepIdx + 1)..] : key;
    }

    internal static bool IsDockerCompose(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        return fileName.Contains("docker-compose") || fileName.StartsWith("compose.") || fileName.StartsWith("compose-") ||
               fileName.Equals("compose.yml") || fileName.Equals("compose.yaml");
    }
}

/// <summary>
/// Standard descriptor for message brokers, queues, and topics (Kafka, RabbitMQ).
/// </summary>
public sealed class MessageBrokerConfigurationDescriptor : ILibraryConfigurationDescriptor
{
    public int Order => 1000;

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
        if (DatabaseConfigurationDescriptor.IsDockerCompose(relativePath)) return false;

        var lowerKey = key.ToLowerInvariant();
        var lowerVal = value.ToLowerInvariant();

        if (lowerKey.Contains("rabbitmq") || lowerVal.StartsWith("amqp://") || lowerVal.StartsWith("amqps://"))
        {
            var ch = ConfigurationParser.ExtractChannelName(value, ExtractKeyLeaf(key));
            ConfigurationParser.CreateTopicNode("rabbitmq", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        if (lowerKey.Contains("kafka"))
        {
            var ch = ConfigurationParser.ExtractChannelName(value, ExtractKeyLeaf(key));
            ConfigurationParser.CreateTopicNode("kafka", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
            return true;
        }

        return false;
    }

    private static string ExtractKeyLeaf(string key)
    {
        var sepIdx = Math.Max(key.LastIndexOf(':'), key.LastIndexOf('.'));
        var leaf = sepIdx >= 0 && sepIdx < key.Length - 1 ? key[(sepIdx + 1)..] : key;
        if (leaf.Equals("host", StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals("port", StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals("addresses", StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals("bootstrap-servers", StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals("url", StringComparison.OrdinalIgnoreCase) ||
            leaf.Equals("uri", StringComparison.OrdinalIgnoreCase))
        {
            return "default";
        }
        return leaf;
    }
}

/// <summary>
/// Standard declarative descriptor for known cloud/SaaS services (Stripe, Auth0, OpenAI, AWS, Azure).
/// </summary>
public sealed class CloudServicesConfigurationDescriptor : ILibraryConfigurationDescriptor
{
    public int Order => 1000;
    private static readonly (string Pattern, string CloudName)[] CloudProviders =
    [
        ("stripe", "Stripe"),
        ("auth0", "Auth0"),
        ("openai", "OpenAI"),
        ("aws", "AWS"),
        ("amazon", "AWS"),
        ("azure", "Azure")
    ];

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
        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        if (fileName.Contains("docker-compose") || fileName.StartsWith("compose.") || fileName.StartsWith("compose-") ||
            fileName.Equals("compose.yml") || fileName.Equals("compose.yaml"))
        {
            return false;
        }

        var lowerKey = key.ToLowerInvariant();

        foreach (var (pattern, cloudName) in CloudProviders)
        {
            if (lowerKey.Contains(pattern))
            {
                ConfigurationParser.CreateCloudServiceNode(cloudName, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
                return true;
            }
        }

        return false;
    }
}
