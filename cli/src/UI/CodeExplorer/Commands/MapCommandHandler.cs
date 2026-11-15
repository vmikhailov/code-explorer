using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class MapCommandHandler : BaseCommandHandler
{
    private const string ViewOverview = "overview";
    private const string ViewLineage = "lineage";
    private const string ViewCqrs = "cqrs";
    private const string FormatYaml = "yaml";
    private const string FormatMermaid = "mermaid";
    private const string FormatC4 = "c4";
    private const string DiagramArchitecture = "architecture";

    public static async Task<int> HandleAsync(MapOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var normView = string.IsNullOrWhiteSpace(opts.View) ? ViewOverview : opts.View.Trim().ToLowerInvariant();
        var normFormat = string.IsNullOrWhiteSpace(opts.Format) ? FormatYaml : opts.Format.Trim().ToLowerInvariant();
        var projectName = opts.EffectiveProject;

        string output;
        if (normFormat is FormatMermaid or FormatC4)
        {
            var diagramView = normView is ViewLineage or ViewCqrs ? normView : DiagramArchitecture;
            output = await repository.ExportArchitectureDiagramAsync(normFormat, diagramView, projectName, opts.IncludeLibraries, ws.RootDirectory);
        }
        else if (normView is ViewLineage or ViewCqrs)
        {
            output = await repository.ExportArchitectureDiagramAsync(FormatMermaid, normView, projectName, opts.IncludeLibraries, ws.RootDirectory);
        }
        else if (normView == ViewOverview)
        {
            output = await repository.GetArchitectureMapAsync(projectName, ws.RootDirectory);
        }
        else
        {
            output = await repository.GetArchitectureViewAsync(normView, projectName, opts.IncludeLibraries, ws.RootDirectory);
        }

        if (!string.IsNullOrWhiteSpace(opts.Output))
        {
            var outPath = Path.GetFullPath(opts.Output);
            var outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            await File.WriteAllTextAsync(outPath, output);
            PrintSuccess($"✓ Exported architecture {normFormat} ({normView}) to '{outPath}'");
        }
        else
        {
            Console.WriteLine(output);
        }

        return 0;
    }
}
