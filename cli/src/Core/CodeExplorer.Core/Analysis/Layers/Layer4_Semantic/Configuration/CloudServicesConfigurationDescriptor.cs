using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

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
        SemanticParseContext ctx)
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
