using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.ColdFusion;

public class ColdFusionProjectParser : IProjectParser
{
    public string LanguageName => "coldfusion";

    public string ProjectType => "coldfusion";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        ".git", ".svn", ".hg", "node_modules", "bin", "obj", ".idea", ".vscode", "dist", "build", ".codeexplorer"
    ];

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        // 1. Explicit Application boundary files
        if (filesInDirectory.Any(f =>
            f.Equals("Application.cfc", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("Application.cfm", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("box.json", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("coldfusion.json", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 2. Directory with ColdFusion files that does not have an Application.cfc/cfm in parent
        var hasCfFiles = filesInDirectory.Any(f =>
            f.EndsWith(".cfc", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".cfm", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".cfml", StringComparison.OrdinalIgnoreCase));

        return hasCfFiles;
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        // 1. Try Application.cfc (this.name = "...")
        var appCfcPath = Path.Combine(directoryPath, "Application.cfc");
        if (File.Exists(appCfcPath))
        {
            try
            {
                var text = File.ReadAllText(appCfcPath);
                var match = Regex.Match(text, @"(?:this\s*\.\s*name\s*=\s*|applicationName\s*=\s*)['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var rawName = match.Groups[1].Value.Trim();
                    // Clean versions or special chars if needed: "NSAProd v2.0" -> "NSAProd"
                    var clean = Regex.Replace(rawName, @"\s*v?\d+(?:\.\d+)*.*$", "").Trim();
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        return clean;
                    }
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        return rawName;
                    }
                }
            }
            catch
            {
                // Fallback to directory name
            }
        }

        // 2. Try Application.cfm (<cfapplication name="...">)
        var appCfmPath = Path.Combine(directoryPath, "Application.cfm");
        if (File.Exists(appCfmPath))
        {
            try
            {
                var text = File.ReadAllText(appCfmPath);
                var match = Regex.Match(text, @"<\s*cfapplication\b[^>]*\bname\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Fallback
            }
        }

        // 3. Try box.json ("name": "...")
        var boxJsonPath = Path.Combine(directoryPath, "box.json");
        if (File.Exists(boxJsonPath))
        {
            try
            {
                var text = File.ReadAllText(boxJsonPath);
                var match = Regex.Match(text, @"""name""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Fallback
            }
        }

        // 4. Default: Folder name
        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        return !string.IsNullOrWhiteSpace(folderName) ? folderName : "ColdFusionApp";
    }

    public static string? GetDefaultDatasource(string directoryPath)
    {
        var appCfcPath = Path.Combine(directoryPath, "Application.cfc");
        if (File.Exists(appCfcPath))
        {
            try
            {
                var text = File.ReadAllText(appCfcPath);
                // Look for this.datasource = '...' or Application.DSN = '...' or Application.datasource = '...'
                var match = Regex.Match(text, @"(?:this\s*\.\s*datasource|Application\s*\.\s*(?:DSN|datasource))\s*=\s*(?:this\s*\.\s*datasource|['""]([^'""]+)['""])", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var val = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && !val.Contains('.'))
                    {
                        return val;
                    }
                }

                var directMatch = Regex.Match(text, @"(?:this\s*\.\s*datasource)\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
                if (directMatch.Success)
                {
                    return directMatch.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Ignore
            }
        }

        return null;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        return Task.FromResult<ProducedPackageInfo?>(null);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        return Task.FromResult(new ProjectDependencyInfo(new List<string>(), new List<ProducedPackageInfo>()));
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree)
    {
        return new ColdFusionSyntaxEnricher(syntaxTree);
    }
}
