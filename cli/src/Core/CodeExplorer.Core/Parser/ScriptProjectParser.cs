using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Fallback project parser that detects directories containing loose scripts
/// (.py, .ps1, .sh, .cs, .js, .ts, etc.) when no other project parser matched.
/// </summary>
public class ScriptProjectParser : IProjectParser
{
    public string ProjectType => "script";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__", "node_modules", "bin", "obj", ".git"];

    public bool IsConfigurationFile(string fileName) => false;

    public static bool IsScriptFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".py" or ".ps1" or ".sh" or ".cs" or ".js" or ".ts" or ".mjs" or ".cjs" or ".rb" or ".bash" or ".zsh";
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
            ".ts" or ".js" or ".mjs" or ".cjs" => "typescript",
            ".cs" => "csharp",
            ".ps1" => "powershell",
            ".sh" or ".bash" or ".zsh" => "shell",
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
