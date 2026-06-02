using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
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

        return await SolutionParser.IndexDirectoryAsync(absolutePath, dbClient, clear);
    }
}
