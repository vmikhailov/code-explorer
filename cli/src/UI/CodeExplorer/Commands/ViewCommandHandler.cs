using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class ViewCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(ViewOptions opts)
    {
        try
        {
            var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
            if (ws == null) return 1;

            WorkspaceConventions.LoadFromWorkspace(ws.RootDirectory);
            await using var client = new SqliteGraphClient(ws.DbPath);

            if (HasTarget(opts.Target, "layers", "ontology"))
            {
                var engine = new ArchitectureViewEngine(client);
                var layersDto = await engine.GetOntologyLayersAsync();

                if (IsJsonFormat(opts.Format))
                {
                    Console.WriteLine(JsonSerializer.Serialize(layersDto,
                        new JsonSerializerOptions { WriteIndented = true }));
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

                Console.WriteLine("### Cypher Node Properties & Query Patterns (`ce query \"<cypher>\" --no-truncate` / `ce callers <target>`)");
                Console.WriteLine("- **Node Labels**: `Project` (`Service`, `Library`, `Worker`), `ExternalService`, `Type`, `Function`, `Table`, `Topic`, `Endpoint`, `EntryPoint`");
                Console.WriteLine("- **Direct Properties on `Type` & `Function`**: `n.name`, `n.path` (relative file path), `n.start_line`, `n.project_name`, `n.symbol`, `n.is_test`");
                Console.WriteLine("- **Key Edges**: `[:CALLS]`, `[:SERVICE_CALL]`, `[:IMPLEMENTS]`, `[:INHERITS_FROM*1..3]`, `[:PERSISTED_IN]`, `[:PUBLISHES_TO]`, `[:SUBSCRIBES_TO]`");

                return 0;
            }

            var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
            string output;

            if (HasTarget(opts.Target, "context", "contexts", "bounded-context", "bounded-contexts", "context-map"))
            {
                output = await repository.GetBoundedContextsAsync(format: opts.Format, workspacePath: ws.RootDirectory);
            }
            else if (HasTarget(opts.Target, "domain", "domains"))
            {
                output = await repository.GetDomainArchitectureAsync(includeLibraries: opts.IncludeLibraries,
                    format: opts.Format, workspacePath: ws.RootDirectory);
            }
            else
            {
                output = await repository.GetArchitectureViewAsync(level: opts.Level, scope: opts.Scope,
                    includeLibraries: opts.IncludeLibraries, format: opts.Format, workspacePath: ws.RootDirectory);
            }

            Console.WriteLine(output);
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"View Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
