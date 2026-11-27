using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

internal sealed class MessageBrokerConfigurationDescriptor : ILibraryConfigurationDescriptor
{
    public int Order => 1000;

    internal static readonly HashSet<string> IgnoredInfraKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "host", "port", "addr", "address", "addresses", "dockerfile", "image", "enabled",
        "version", "servers", "bootstrap-servers", "bootstrapservers", "url", "uri", "user",
        "username", "password", "ssl", "tls", "tls_verify", "group", "group_id", "groupid",
        "heap_opts", "listeners", "advertised_listeners", "quorum_voters", "retries", "timeout"
    };

    public bool TryHandle(
        string key,
        string value,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        SemanticParseContext ctx)
    {
        if (DatabaseConfigurationDescriptor.IsDockerCompose(relativePath)) return false;

        var leaf = ExtractKeyLeaf(key);
        if (IgnoredInfraKeys.Contains(leaf)) return false;

        var lowerKey = key.ToLowerInvariant();
        var lowerVal = value.ToLowerInvariant();

        if (lowerKey.Contains("rabbitmq") || lowerVal.StartsWith("amqp://") || lowerVal.StartsWith("amqps://"))
        {
            var ch = ConfigurationParser.ExtractChannelName(value, key);
            if (!string.IsNullOrEmpty(ch) && !IgnoredInfraKeys.Contains(ch) && !ch.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                ConfigurationParser.CreateTopicNode("rabbitmq", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
                return true;
            }
        }

        if (lowerKey.Contains("kafka"))
        {
            var ch = ConfigurationParser.ExtractChannelName(value, key);
            if (!string.IsNullOrEmpty(ch) && !IgnoredInfraKeys.Contains(ch) && !ch.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                ConfigurationParser.CreateTopicNode("kafka", ch, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx);
                return true;
            }
        }

        return false;
    }

    private static string ExtractKeyLeaf(string key)
    {
        var sepIdx = Math.Max(key.LastIndexOf(':'), Math.Max(key.LastIndexOf('.'), key.LastIndexOf('_')));
        return sepIdx >= 0 && sepIdx < key.Length - 1 ? key[(sepIdx + 1)..] : key;
    }
}
