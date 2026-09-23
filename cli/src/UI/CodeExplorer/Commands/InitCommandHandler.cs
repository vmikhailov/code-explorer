using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class InitCommandHandler
{
    public static async Task<int> HandleAsync(InitOptions opts)
    {
        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var name = string.IsNullOrWhiteSpace(opts.Name) ? new DirectoryInfo(targetDir).Name : opts.Name.Trim();

        var existing = WorkspaceLocator.Find(targetDir);
        if (existing != null && existing.RootDirectory.Equals(targetDir, StringComparison.OrdinalIgnoreCase) && !opts.Force)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Workspace already exists at '{existing.RootDirectory}'. Use -f or --force to reinitialize.");
            Console.ResetColor();
            return 1;
        }

        var ws = WorkspaceLocator.Initialize(targetDir, name);

        // Initialize DB schema & seed Workspace node
        await using (var client = new SqliteGraphClient(ws.DbPath))
        {
            await client.ExecuteWriteAsync(
                "INSERT INTO nodes (id, kind, properties) VALUES ('workspace', 'Workspace', json_object('id', 'workspace', 'name', @name, 'path', @path)) " +
                "ON CONFLICT(id) DO UPDATE SET properties = json_object('id', 'workspace', 'name', @name, 'path', @path);",
                new Dictionary<string, object?> { ["name"] = name, ["path"] = ws.RootDirectory });
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Initialized CodeExplorer workspace '{name}'");
        Console.ResetColor();
        Console.WriteLine($"  Root:     {ws.RootDirectory}");
        Console.WriteLine($"  Database: {ws.DbPath}");
        Console.WriteLine($"  Queries:  {ws.QueriesDirectory}");
        Console.WriteLine($"\nNext: run 'ce scan' to index your codebase.");
        return 0;
    }
}
