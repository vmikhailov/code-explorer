using System.Text.Json;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class SnippetsCommandHandler : BaseCommandHandler
{
    private const string MissingInputError = "Error: Provide one or more 'file:start-end' ranges or '--nodes <yaml>' (e.g. 'ce snippets src/Program.cs:1-30').";

    public static async Task<int> HandleAsync(SnippetsOptions opts)
    {
        var nodesPayload = opts.Nodes;
        if (string.IsNullOrWhiteSpace(nodesPayload))
        {
            var rangeSpecs = opts.Ranges?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (rangeSpecs == null || rangeSpecs.Count == 0)
            {
                PrintError(MissingInputError);
                return 1;
            }

            if (!TryBuildNodesJsonFromSpecs(rangeSpecs, out nodesPayload, out var parseError))
            {
                PrintError(parseError);
                return 1;
            }
        }

        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        var wsRoot = ws?.RootDirectory ?? targetDir;

        var repository = new CodeExplorerRepository(new InMemoryGraphRepository(), defaultWorkspacePath: wsRoot);
        var output = await repository.FetchCodeSnippetsAsync(nodesPayload!, wsRoot);
        Console.WriteLine(output);
        return 0;
    }

    private static bool TryBuildNodesJsonFromSpecs(IReadOnlyList<string> specs, out string json, out string error)
    {
        var items = new List<McpRAGNode>(specs.Count);
        foreach (var raw in specs)
        {
            var spec = raw.Trim();
            if (!TryParseSpec(spec, out var node))
            {
                json = string.Empty;
                error = $"Error: Invalid snippet range '{spec}'. Expected '<file>:<start>-<end>' (e.g. 'src/OrderService.cs:10-35').";
                return false;
            }
            items.Add(node);
        }

        json = JsonSerializer.Serialize(items);
        error = string.Empty;
        return true;
    }

    private static bool TryParseSpec(string spec, out McpRAGNode node)
    {
        node = new McpRAGNode();
        var lastColon = spec.LastIndexOf(':');
        if (lastColon <= 1 || lastColon >= spec.Length - 1)
        {
            return false;
        }

        var suffix = spec[(lastColon + 1)..];
        var prefix = spec[..lastColon];

        var dashIdx = suffix.IndexOf('-');
        if (dashIdx > 0)
        {
            if (int.TryParse(suffix[..dashIdx], out var s) && int.TryParse(suffix[(dashIdx + 1)..], out var e) && s >= 1 && e >= s)
            {
                node = new McpRAGNode { file_path = prefix, start_line = s, end_line = e };
                return true;
            }
            return false;
        }

        if (int.TryParse(suffix, out var singleOrEnd) && singleOrEnd >= 1)
        {
            var prevColon = prefix.LastIndexOf(':');
            if (prevColon > 1 && int.TryParse(prefix[(prevColon + 1)..], out var startBeforeColon) && startBeforeColon >= 1 && singleOrEnd >= startBeforeColon)
            {
                node = new McpRAGNode { file_path = prefix[..prevColon], start_line = startBeforeColon, end_line = singleOrEnd };
                return true;
            }

            node = new McpRAGNode { file_path = prefix, start_line = singleOrEnd, end_line = singleOrEnd };
            return true;
        }

        return false;
    }
}
