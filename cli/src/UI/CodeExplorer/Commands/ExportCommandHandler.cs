using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class ExportCommandHandler
{
    public static async Task<int> HandleAsync(ExportOptions opts)
    {
        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: No CodeExplorer workspace found at '{targetDir}'. Run 'ce init' first.");
            Console.ResetColor();
            return 1;
        }

        await using var client = new SqliteGraphClient(ws.DbPath);
        var diagram = await DiagramExporter.ExportAsync(client, opts.Format, opts.Type, opts.Project);

        if (!string.IsNullOrWhiteSpace(opts.Output))
        {
            var outPath = Path.GetFullPath(opts.Output);
            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outPath, diagram);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Exported {opts.Format.ToUpperInvariant()} diagram to '{outPath}'");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(diagram);
        }

        return 0;
    }
}
