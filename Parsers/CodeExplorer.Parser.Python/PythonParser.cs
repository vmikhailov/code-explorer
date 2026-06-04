using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python;

public class PythonParser : IProjectParser, IFileParser
{
    public string LanguageName => "python";

    public string ProjectType => "python";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__"];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName is "requirements.txt" or "pyproject.toml" or "setup.py")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(Node node)
    {
        if (node.Type == "string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return "Query";
            }
        }

        return node.Type switch
        {
            "class_definition" => "Class",

            "function_definition" => "Function",

            "assignment" or
            "parameters" or
            "pattern" => "Variable",

            _ => null
        };
    }

    public string? ExtractIdentifier(Node node)
    {
        if (node.Type == "string")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        var nameNode = node.GetChildForField("name");
        if (nameNode != null && nameNode.Id != IntPtr.Zero)
        {
            return nameNode.Text;
        }

        // Fallback: search for first-level identifier or variable_name
        foreach (var child in node.Children)
        {
            if (child.Type is "identifier" or "variable_name")
            {
                return child.Text;
            }
        }

        // Fallback: search first-level recursively for contains("name")
        foreach (var child in node.Children)
        {
            if (child.Type.Contains("name"))
            {
                return child.Text;
            }
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        TryDetectCalls(node, scopeSymbolId, references);
        TryDetectInheritsFrom(node, scopeSymbolId, references);
        if (node.Type == "string")
        {
            NestedSqlParser.TryDetectSqlDependencies(node.Text, scopeSymbolId, references);
        }
    }

    private void TryDetectCalls(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "call")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
    }

    private void TryDetectInheritsFrom(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "class_definition")
        {
            var superclassesNode = node.GetChildForField("superclasses");
            if (superclassesNode != null && superclassesNode.Id != IntPtr.Zero && superclassesNode.Children.Count > 0)
            {
                foreach (var baseChild in superclassesNode.Children)
                {
                    if (baseChild.Type == "identifier")
                    {
                        references.Add(new Reference(scopeSymbolId, baseChild.Text, "INHERITS_FROM"));
                    }
                }
            }
        }
    }

    private static string? FindCallName(Node callNode)
    {
        var expr = callNode.GetChildForField("function");
        if (expr != null && expr.Id == IntPtr.Zero && callNode.Children.Count > 0)
        {
            expr = callNode.Children[0];
        }
        if (expr == null || expr.Id == IntPtr.Zero) return null;

        if (expr.Type == "identifier")
        {
            return expr.Text;
        }
        if (expr.Type == "attribute")
        {
            var attrChild = expr.GetChildForField("attribute");
            if (attrChild != null && attrChild.Id != IntPtr.Zero) return attrChild.Text;
        }
        return null;
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var pyprojectPath = Path.Combine(projectDirectory, "pyproject.toml");
        if (File.Exists(pyprojectPath))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(pyprojectPath);
                string? name = null;
                var version = "1.0.0";
                
                var inProjectSection = false;
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith("[project]") || line.StartsWith("[tool.poetry]"))
                    {
                        inProjectSection = true;
                        continue;
                    }
                    else if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inProjectSection = false;
                    }

                    if (inProjectSection)
                      {
                        if (line.StartsWith("name"))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                name = parts[1].Trim(' ', '"', '\'');
                            }
                        }
                        else if (line.StartsWith("version"))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                version = parts[1].Trim(' ', '"', '\'');
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(name))
                {
                    return new ProducedPackageInfo(name, version, "pip");
                }
            }
            catch
            {
                // Ignore
            }
        }

        var setupPyPath = Path.Combine(projectDirectory, "setup.py");
        if (File.Exists(setupPyPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(setupPyPath);
                var nameMatch = System.Text.RegularExpressions.Regex.Match(content, @"name\s*=\s*['""]([^'""]+)['""]");
                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value;
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(content, @"version\s*=\s*['""]([^'""]+)['""]");
                    var version = versionMatch.Success ? versionMatch.Groups[1].Value : "1.0.0";

                    return new ProducedPackageInfo(name, version, "pip");
                }
            }
            catch
            {
                // Ignore
            }
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        // 1. Try parsing pyproject.toml dependencies
        var pyprojectPath = Path.Combine(projectDirectory, "pyproject.toml");
        if (File.Exists(pyprojectPath))
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(pyprojectPath);
                var inDependencies = false;
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("[project.dependencies]") || line.StartsWith("[tool.poetry.dependencies]"))
                    {
                        inDependencies = true;
                        continue;
                    }
                    else if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        inDependencies = false;
                    }

                    if (inDependencies)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length >= 1)
                        {
                            var name = parts[0].Trim();
                            if (name.ToLowerInvariant() == "python") continue; // skip python version constraint

                            var version = parts.Length == 2 ? parts[1].Trim(' ', '"', '\'') : "unknown";
                            externalPackages.Add(new ProducedPackageInfo(name, version, "pip"));
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        // 2. Try parsing requirements.txt dependencies if externalPackages is empty
        if (externalPackages.Count == 0)
        {
            var reqPath = Path.Combine(projectDirectory, "requirements.txt");
            if (File.Exists(reqPath))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(reqPath);
                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("-")) continue;

                        // Parse package name and specifier, e.g. requests>=2.25.1 -> name = requests, version = >=2.25.1
                        var match = System.Text.RegularExpressions.Regex.Match(line, @"^([a-zA-Z0-9_\-\[\]]+)(.*)$");
                        if (match.Success)
                        {
                            var name = match.Groups[1].Value;
                            var versionSpec = match.Groups[2].Value.Trim();
                            var version = string.IsNullOrEmpty(versionSpec) ? "unknown" : versionSpec;
                            externalPackages.Add(new ProducedPackageInfo(name, version, "pip"));
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
            }
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public bool UsesTreeSitter => true;
    public Task<FileNode> ParseAsync(string filePath, string parentNodeId, ParsingContext ctx)
    {
        var relativePath = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
        return TreeSitterFileParser.ParseFileAsync(filePath, relativePath, parentNodeId, this, ctx);
    }
}
