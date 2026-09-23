using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class ClearCommandHandler
{
    public static async Task<int> HandleAsync(ClearOptions opts)
    {
        try
        {
            var ws = WorkspaceLocator.FindOrThrow(opts.Dir);
            await using var client = new SqliteGraphClient(ws.DbPath);

            if (!string.IsNullOrWhiteSpace(opts.Path))
            {
                var targetPath = Path.GetFullPath(opts.Path);
                Console.WriteLine($"Clearing data for path '{targetPath}' from workspace '{ws.RootDirectory}'...");
                var cleared = await client.ClearWorkspaceAsync(targetPath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(cleared ? "✓ Cleared path data successfully." : "Path not found in database.");
                Console.ResetColor();
                return 0;
            }

            if (!opts.Yes)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"Are you sure you want to clear the entire graph database at '{ws.DbPath}'? (y/N): ");
                Console.ResetColor();
                var answer = Console.ReadLine();
                if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Operation cancelled.");
                    return 0;
                }
            }

            await client.ClearDatabaseAsync();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Cleared entire database at '{ws.DbPath}'.");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Clear Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
