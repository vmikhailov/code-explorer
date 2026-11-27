using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public sealed class DatabaseConfigurationDescriptor : ILibraryConfigurationDescriptor
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
        SemanticParseContext ctx)
    {
        if (string.IsNullOrWhiteSpace(value) || IsDockerCompose(relativePath)) return false;

        var lowerKey = key.ToLowerInvariant();
        var lowerVal = value.ToLowerInvariant();

        var isConnStringKey = lowerKey.StartsWith("connectionstrings:") ||
                              lowerKey.Equals("connectionstring") ||
                              lowerKey.EndsWith(":connectionstring") ||
                              lowerKey.Contains("database_url") ||
                              lowerKey.Contains("db_connection") ||
                              lowerKey.Contains("connection_string") ||
                              lowerKey.Contains("valkey_addr") ||
                              lowerKey.Contains("redis_addr");

        var isDbVal = lowerVal.StartsWith("postgres://") ||
                      lowerVal.StartsWith("postgresql://") ||
                      lowerVal.StartsWith("jdbc:postgresql://") ||
                      lowerVal.StartsWith("mongodb://") ||
                      lowerVal.StartsWith("mongodb+srv://") ||
                      lowerVal.StartsWith("redis://") ||
                      lowerVal.StartsWith("valkey://") ||
                      lowerVal.StartsWith("jdbc:sqlserver://") ||
                      lowerVal.StartsWith("jdbc:mysql://") ||
                      lowerVal.StartsWith("mysql://") ||
                      (lowerVal.Contains("data source=") && lowerVal.Contains(".db")) ||
                      ((lowerVal.Contains("host=") || lowerVal.Contains("server=") || lowerVal.Contains("data source=")) &&
                       (lowerVal.Contains("database=") || lowerVal.Contains("initial catalog=")));

        if (!isConnStringKey && !isDbVal)
        {
            return false;
        }

        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            key,
            value,
            relativePath,
            fileNodeId,
            workspaceId,
            containerNode,
            relationships,
            ctx);

        return true;
    }

    internal static bool IsDockerCompose(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath).ToLowerInvariant();
        return fileName.Contains("docker-compose") || fileName.Contains("podman-compose") ||
               fileName.StartsWith("compose.") || fileName.StartsWith("compose-") ||
               fileName.Equals("compose.yml") || fileName.Equals("compose.yaml");
    }
}
