using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class WorkspaceIndexerService(MemgraphClient dbClient)
{
    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexWorkspaceAsync(string dirPath, bool clear)
    {
        if (!Directory.Exists(dirPath))
        {
            throw new DirectoryNotFoundException($"Directory '{dirPath}' does not exist.");
        }

        var absolutePath = Path.GetFullPath(dirPath).Replace('\\', '/');

        var parser = new WorkspaceParser(absolutePath, dbClient, clear);
        return await parser.IndexAsync();
    }
}
