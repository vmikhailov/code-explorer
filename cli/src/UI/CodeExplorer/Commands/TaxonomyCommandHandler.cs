using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class TaxonomyCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(TaxonomyOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.Kind))
        {
            Console.WriteLine(OntologyRegistry.GetNodeDefinition(opts.Kind));
            return 0;
        }

        if (opts.StatusOnly)
        {
            var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
            var statusWs = WorkspaceLocator.Find(targetDir);
            if (statusWs == null || !File.Exists(statusWs.DbPath))
            {
                var unindexedRepo = new CodeExplorerRepository(new InMemoryGraphRepository(), defaultWorkspacePath: targetDir);
                Console.WriteLine(await unindexedRepo.GetWorkspaceStatusAsync(targetDir));
                return 0;
            }

            await using var statusClient = new SqliteGraphClient(statusWs.DbPath);
            var statusRepo = new CodeExplorerRepository(statusClient, defaultWorkspacePath: statusWs.RootDirectory);
            Console.WriteLine(await statusRepo.GetWorkspaceStatusAsync(statusWs.RootDirectory));
            return 0;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var taxonomyYaml = await repository.GetTaxonomyAsync(ws.RootDirectory);
        Console.WriteLine(taxonomyYaml);
        return 0;
    }
}
