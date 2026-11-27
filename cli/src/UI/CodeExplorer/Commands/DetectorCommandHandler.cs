using System.Text;
using System.Text.Json;
using CodeExplorer.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CodeExplorer.Commands;

public static class DetectorCommandHandler
{
    private const string ActionList = "list";
    private const string ActionValidate = "validate";
    private const string ActionSchema = "schema";
    private const string FormatJson = "json";
    private const string FormatYaml = "yaml";
    private const string FormatYml = "yml";
    private const string HeaderSeparator = "----------------------------------------------------------------------------------------------------";

    public static Task<int> HandleAsync(DetectorOptions opts)
    {
        var action = (opts.Action ?? ActionList).Trim().ToLowerInvariant();
        return Task.FromResult(action switch
        {
            ActionList => HandleList(opts),
            ActionValidate => HandleValidate(opts),
            ActionSchema => HandleSchema(opts),
            _ => ReportUnknownAction(opts.Action)
        });
    }

    private static int HandleList(DetectorOptions opts)
    {
        var workspaceRoot = ResolveWorkspaceRoot(opts.Dir);
        var items = WorkspaceDetectorLoader.ListAllDetectors(workspaceRoot, includeBuiltIn: !opts.CustomOnly);

        if (!string.IsNullOrWhiteSpace(opts.Language))
        {
            if (WorkspaceDetectorLanguageMap.TryExpandLanguages(opts.Language, out var targetLangs))
            {
                items = items
                    .Where(d => d.Languages.Contains("*") ||
                                d.Languages.Any(l => targetLangs.Contains(l, StringComparer.OrdinalIgnoreCase)))
                    .ToList();
            }
            else
            {
                items = items
                    .Where(d => d.Languages.Any(l => l.Equals(opts.Language.Trim(), StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        var fmt = (opts.Format ?? string.Empty).Trim().ToLowerInvariant();
        string rendered;
        if (fmt == FormatJson)
        {
            rendered = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        }
        else if (fmt is FormatYaml or FormatYml)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            rendered = serializer.Serialize(items);
        }
        else
        {
            rendered = RenderDetectorTable(items);
        }

        return WriteOutput(rendered, opts.OutputPath);
    }

    private static int HandleValidate(DetectorOptions opts)
    {
        var workspaceRoot = ResolveWorkspaceRoot(opts.Dir);
        var (catalog, issues) = WorkspaceDetectorLoader.ValidateWorkspace(workspaceRoot);
        var errorCount = issues.Count(i => !i.IsWarning);
        var warnCount = issues.Count(i => i.IsWarning);
        var failed = errorCount > 0 || (opts.Strict && warnCount > 0);

        var fmt = (opts.Format ?? string.Empty).Trim().ToLowerInvariant();
        string rendered;
        if (fmt == FormatJson)
        {
            var payload = new
            {
                valid = !failed,
                custom_detectors = catalog.CustomDetectors.Count,
                combined_hash = catalog.CombinedHash,
                errors = errorCount,
                warnings = warnCount,
                issues
            };
            rendered = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Validated {catalog.CustomDetectors.Count} custom YAML detector(s) (hash: {catalog.CombinedHash}).");
            foreach (var issue in issues)
            {
                var tag = issue.IsWarning ? "[WARN]" : "[ERROR]";
                sb.AppendLine($"  {tag} {issue.FilePath}: {issue.Message}");
            }
            if (issues.Count == 0)
            {
                sb.AppendLine("All custom YAML detectors are valid.");
            }
            rendered = sb.ToString().TrimEnd();
        }

        var writeRes = WriteOutput(rendered, opts.OutputPath);
        return writeRes != 0 ? writeRes : (failed ? 1 : 0);
    }

    private static int HandleSchema(DetectorOptions opts)
    {
        var content = opts.Template
            ? DeclarativeDetectorSchemaGenerator.StarterYamlTemplate
            : DeclarativeDetectorSchemaGenerator.BuildJsonSchema();
        return WriteOutput(content, opts.OutputPath);
    }

    private static string RenderDetectorTable(IReadOnlyList<DetectorDescriptorSummary> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{"ORIGIN",-10} {"ID",-32} {"LANGUAGES",-22} {"ROLE",-20} {"STATUS"}");
        sb.AppendLine(HeaderSeparator);
        foreach (var d in items)
        {
            var langs = string.Join(",", d.Languages);
            var status = d.Origin == DetectorOrigin.BuiltIn
                ? (d.OverridesBuiltIn ? "overridden" : "active")
                : (d.OverridesBuiltIn ? (d.ShadowsImperativeRules ? "override (shadows C#)" : "override") : "active");
            sb.AppendLine($"{d.Origin.ToString().ToLowerInvariant(),-10} {d.Id,-32} {langs,-22} {d.Role,-20} {status}");
        }
        sb.Append($"Total: {items.Count} detector(s)");
        return sb.ToString();
    }

    private static string ResolveWorkspaceRoot(string? explicitDir)
    {
        try
        {
            return WorkspaceLocator.FindOrThrow(explicitDir).RootDirectory;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or InvalidOperationException or IOException)
        {
            return !string.IsNullOrWhiteSpace(explicitDir)
                ? Path.GetFullPath(explicitDir)
                : Directory.GetCurrentDirectory();
        }
    }

    private static int WriteOutput(string content, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var fullOut = Path.GetFullPath(outputPath);
            var parent = Path.GetDirectoryName(fullOut);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            File.WriteAllText(fullOut, content);
            Console.WriteLine($"Output written to {fullOut}");
            return 0;
        }

        Console.WriteLine(content);
        return 0;
    }

    private static int ReportUnknownAction(string? action)
    {
        Console.Error.WriteLine($"Unknown detector action '{action}'. Supported actions: list, validate, schema.");
        return 1;
    }
}
