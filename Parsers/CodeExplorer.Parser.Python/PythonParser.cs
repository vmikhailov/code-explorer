using TreeSitter;

namespace CodeExplorer.Parser;

public class PythonParser : ILanguageParser
{
    public string LanguageName => "python";

    public string ProjectType => "python";

    public System.Collections.Generic.IReadOnlyCollection<string> ExcludedFolders => new[] { "venv", ".venv", "__pycache__" };

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = System.IO.Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "requirements.txt" || fileName == "pyproject.toml" || fileName == "setup.py")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(Node node)
    {
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
        if (node.Type == "call")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
        else if (node.Type == "class_definition")
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
        var pyprojectPath = System.IO.Path.Combine(projectDirectory, "pyproject.toml");
        if (System.IO.File.Exists(pyprojectPath))
        {
            try
            {
                var lines = await System.IO.File.ReadAllLinesAsync(pyprojectPath);
                string? name = null;
                string version = "1.0.0";
                
                bool inProjectSection = false;
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

        var setupPyPath = System.IO.Path.Combine(projectDirectory, "setup.py");
        if (System.IO.File.Exists(setupPyPath))
        {
            try
            {
                var content = await System.IO.File.ReadAllTextAsync(setupPyPath);
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
}
