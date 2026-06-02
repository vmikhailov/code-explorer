using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class WorkspaceIndexerService(MemgraphClient dbClient)
{
    public async Task<(int NodesCount, int RelationshipsCount)> IndexWorkspaceAsync(string dirPath, bool clear)
    {
        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException($"Directory '{dirPath}' does not exist.");
        }

        var absolutePath = Path.GetFullPath(dirPath).Replace('\\', '/');

        // 1. Parse directory and construct AST tree with TreeSitter
        var (nodes, relationships) = SolutionParser.ParseDirectory(absolutePath);

        // 2. Clear previous workspace data surgically if clear option is enabled
        if (clear)
        {
            await dbClient.ClearWorkspaceAsync(absolutePath);
        }

        // 3. Ensure database indexes exist
        await dbClient.CreateIndicesAsync();

        // 4. Bulk upload nodes and relationships
        await dbClient.UploadNodesAsync(nodes);
        await dbClient.UploadRelationshipsAsync(relationships);

        return (nodes.Count, relationships.Count);
    }
}
