using TreeSitter;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public class CSharpParser : IProjectParser, IFileParser
{
    public string LanguageName => "c-sharp";

    public string ProjectType => "csharp";

    public IReadOnlyCollection<string> ExcludedFolders => new[] { "bin", "obj", ".vs" };

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".csproj")
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
            "class_declaration" or
            "struct_declaration" or
            "record_declaration" => "Class",

            "interface_declaration" => "Interface",

            "method_declaration" or
            "function_declaration" or
            "constructor_declaration" or
            "local_function_statement" => "Function",

            "variable_declarator" or
            "parameter" or
            "property_declaration" or
            "field_declaration" => "Variable",

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
        if (node.Type == "invocation_expression")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
        else if (node.Type == "base_list")
        {
            foreach (var child in node.Children)
            {
                if (child.Type.Contains("identifier") || child.Type.Contains("name"))
                {
                    var baseName = child.Text;
                    var refKind = baseName.StartsWith('I') && baseName.Length > 1 && char.IsUpper(baseName[1])
                        ? "IMPLEMENTS"
                        : "INHERITS_FROM";
                    references.Add(new Reference(scopeSymbolId, baseName, refKind));
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
        if (expr.Type == "member_access_expression")
        {
            var nameChild = expr.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero) return nameChild.Text;
        }
        return null;
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
        if (csprojFiles.Length == 0) return null;

        var csprojFile = csprojFiles[0];
        try
        {
            var content = await File.ReadAllTextAsync(csprojFile);
            var doc = System.Xml.Linq.XDocument.Parse(content);

            // Check IsPackable
            var isPackableStr = doc.Descendants("IsPackable").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(isPackableStr) && bool.TryParse(isPackableStr, out var isPackable) && !isPackable)
            {
                return null;
            }

            // Check OutputType (if Exe and not packable, return null)
            var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
            var hasGeneratePackageOnBuild = doc.Descendants("GeneratePackageOnBuild").FirstOrDefault()?.Value;
            var generateOnBuild = !string.IsNullOrEmpty(hasGeneratePackageOnBuild) && 
                                  bool.TryParse(hasGeneratePackageOnBuild, out var gen) && gen;

            var explicitPackable = !string.IsNullOrEmpty(isPackableStr) && 
                                   bool.TryParse(isPackableStr, out var p) && p;

            if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) && !generateOnBuild && !explicitPackable)
            {
                return null;
            }

            var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value 
                         ?? doc.Descendants("AssemblyName").FirstOrDefault()?.Value 
                         ?? Path.GetFileNameWithoutExtension(csprojFile);

            var version = doc.Descendants("Version").FirstOrDefault()?.Value 
                       ?? doc.Descendants("PackageVersion").FirstOrDefault()?.Value 
                       ?? "1.0.0";

            return new ProducedPackageInfo(packageId, version, "nuget");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
        foreach (var csprojFile in csprojFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(csprojFile);
                var doc = System.Xml.Linq.XDocument.Parse(content);

                // Extract local project references
                var projectRefs = doc.Descendants("ProjectReference");
                foreach (var pref in projectRefs)
                {
                    var include = pref.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;

                    var referencedCsprojPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojFile)!, include)).Replace('\\', '/');
                    var referencedProjectDir = Path.GetFullPath(Path.GetDirectoryName(referencedCsprojPath)!).Replace('\\', '/');
                    localProjectPaths.Add(referencedProjectDir);
                }

                // Extract NuGet package references
                var packageRefs = doc.Descendants("PackageReference");
                foreach (var packRef in packageRefs)
                {
                    var name = packRef.Attribute("Include")?.Value;
                    var version = packRef.Attribute("Version")?.Value ?? packRef.Element("Version")?.Value ?? "unknown";
                    if (string.IsNullOrEmpty(name)) continue;

                    externalPackages.Add(new ProducedPackageInfo(name, version, "nuget"));
                }
            }
            catch
            {
                // Ignore
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
