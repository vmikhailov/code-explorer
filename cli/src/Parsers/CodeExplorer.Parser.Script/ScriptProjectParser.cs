using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Script;

/// <summary>
/// Fallback project parser that detects directories containing loose scripts
/// (.py, .ps1, .sh, .bash, .zsh, .rb, .pl, .bat, .cmd) when no standard dialect parser matched.
/// </summary>
[ProjectParser(Tier = ParserTier.Fallback)]
public class ScriptProjectParser : IProjectParser
{
    public string ProjectType => "script";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__", "node_modules", "bin", "obj", ".git"];

    public bool IsConfigurationFile(string fileName) => false;

    public static bool IsScriptFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".ps1" or ".sh" or ".bash" or ".zsh" or ".py" or ".rb" or ".pl" or ".bat" or ".cmd";
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        return filesInDirectory.Any(IsScriptFile);
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var name = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        return string.IsNullOrEmpty(name) ? "ScriptProject" : name;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory) => Task.FromResult<ProducedPackageInfo?>(null);

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        return Task.FromResult(new ProjectDependencyInfo([], []));
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var dominant = filesInDirectory
            .Where(IsScriptFile)
            .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var lang = dominant switch
        {
            ".py" => "python",
            ".ps1" => "powershell",
            ".sh" or ".bash" or ".zsh" => "shell",
            ".rb" => "ruby",
            ".pl" => "perl",
            ".bat" or ".cmd" => "batch",
            _ => "script"
        };

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["role"] = "ScriptProject",
            ["kind"] = "ScriptProject",
            ["entity_kind"] = "ScriptProject",
            ["manifest_type"] = "script",
            ["language"] = lang,
            ["project_type"] = lang
        };
    }

    public ProjectClassification? ClassifyProject(ProjectContext context) => ProjectClassification.ScriptProject;
}
