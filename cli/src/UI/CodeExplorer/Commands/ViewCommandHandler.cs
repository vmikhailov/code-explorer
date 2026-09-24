using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class ViewCommandHandler
{
    public static async Task<int> HandleAsync(ViewOptions opts)
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

        if (opts.Target.Equals("layers", StringComparison.OrdinalIgnoreCase) ||
            opts.Target.Equals("ontology", StringComparison.OrdinalIgnoreCase))
        {
            var engine = new ArchitectureViewEngine(client);
            var layersDto = await engine.GetOntologyLayersAsync();

            if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(JsonSerializer.Serialize(layersDto, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            Console.WriteLine("## Semantic Graph Ontology Layers\n");
            foreach (var layer in layersDto.Layers)
            {
                Console.WriteLine($"### Layer {layer.LayerId}: {layer.Name} ({layer.TotalCount:N0} elements)");
                foreach (var cat in layer.Categories)
                {
                    Console.WriteLine($"- **{cat.Label}**: {cat.Count:N0} ({cat.Icon})");
                }
                Console.WriteLine();
            }
            return 0;
        }

        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
        var level = opts.Target.Equals("domain", StringComparison.OrdinalIgnoreCase) || opts.Target.Equals("context", StringComparison.OrdinalIgnoreCase)
            ? "domain"
            : opts.Level;

        var output = await repository.GetArchitectureViewAsync(
            level: level,
            scope: opts.Scope,
            includeLibraries: opts.IncludeLibraries,
            format: opts.Format,
            workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
