using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class WorkspaceIndexerService(MemgraphClient dbClient)
{
    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexWorkspaceAsync(string dirPath, bool clear)
    {
        var resolvedPath = Common.PathTools.TranslateHostPathToContainerPath(dirPath);

        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException($"Directory '{dirPath}' (resolved as '{resolvedPath}') does not exist.");
        }

        var absolutePath = Path.GetFullPath(resolvedPath).Replace('\\', '/');

        var parser = new WorkspaceParser(absolutePath, dbClient, clear);
        return await parser.IndexAsync();
    }
}
