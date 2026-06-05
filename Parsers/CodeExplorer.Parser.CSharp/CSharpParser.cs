using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp;

public class CSharpParser : IProjectParser, IFileParser
{

    public string LanguageName => "c-sharp";

    public string ProjectType => "csharp";

    public IReadOnlyCollection<string> ExcludedFolders => ["bin", "obj", ".vs"];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.CouchbaseLibraryParser(),
        new Libraries.DapperLibraryParser(),
        new Libraries.EfCoreLibraryParser(),
        new Libraries.ElasticsearchNetLibraryParser(),
        new Libraries.FlurlLibraryParser(),
        new Libraries.MicrosoftDataSqlClientLibraryParser(),
        new Libraries.MongoDbCsLibraryParser(),
        new Libraries.MySqlDataLibraryParser(),
        new Libraries.NestLibraryParser(),
        new Libraries.NpgsqlLibraryParser(),
        new Libraries.OracleDataAccessLibraryParser(),
        new Libraries.StackExchangeRedisLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("Stripe", "cloud", ["stripe", "Stripe"], libraryName: "Stripe"),
        new GenericLibraryParser("AWS", "cloud", ["Amazon.S3", "AWSSDK"], libraryName: "AWS"),
        new GenericLibraryParser("GCP", "cloud", ["Google.Cloud."], libraryName: "GCP"),
        new GenericLibraryParser("Azure", "cloud", ["Azure."], libraryName: "Azure"),

        // Generic Frameworks
        new GenericLibraryParser("AspNetCore", "framework", ["Microsoft.AspNetCore.App", "Microsoft.AspNetCore"], libraryName: "ASP.NET Core"),

        // Generic API Clients
        new GenericLibraryParser("HttpClient", "api", ["System.Net.Http"], libraryName: "HttpClient", isBuiltIn: true),
        new GenericLibraryParser("RestSharp", "api", ["RestSharp"], libraryName: "RestSharp"),
        new GenericLibraryParser("Refit", "api", ["Refit"], libraryName: "Refit"),
        new GenericLibraryParser("WebApiClient", "api", ["WebApiClient"], libraryName: "WebApiClient"),
        new GenericLibraryParser("Apizr", "api", ["Apizr"], libraryName: "Apizr"),
        new GenericLibraryParser("NotoriousClient", "api", ["NotoriousClient"], libraryName: "NotoriousClient"),
    ];

    public CSharpParser()
    {
        _analyzer = new CSharpSemanticAnalyzer(LibraryParsers);
    }


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
        if (node.Type == "attribute")
        {
            var nameNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
            if (nameNode != null && (nameNode.Text == "Route" || nameNode.Text.StartsWith("Http")))
            {
                return "EntryPoint";
            }
        }

        if (IsHttpClientCall(node))
        {
            return "ExternalService";
        }

        if (node.Type.Contains("string") &&
            node.Type != "interpolated_string_expression" &&
            node.Type != "interpolated_verbatim_string_expression" &&
            node.Type != "interpolated_raw_string_expression")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return "Query";
            }
        }

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
        if (node.Type == "attribute")
        {
            return ExtractCSharpAttributeRoute(node);
        }

        if (IsHttpClientCall(node))
        {
            return ExtractHttpClientTarget(node);
        }

        if (node.Type.Contains("string"))
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

    private static bool IsHttpClientCall(Node node)
    {
        if (node.Type != "invocation_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
            {
                var methodName = nameChild.Text;
                return methodName is "GetAsync" or "PostAsync" or "PutAsync" or "DeleteAsync" or "SendAsync" or "PostAsJsonAsync" or "GetFromJsonAsync";
            }
        }
        return false;
    }

    private static string? ExtractHttpClientTarget(Node node)
    {
        var argList = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (argList != null && argList.Children.Count > 1)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "argument");
            if (arg != null)
            {
                var valNode = arg.Children.FirstOrDefault();
                if (valNode != null)
                {
                    var text = valNode.Text.Trim('"');
                    if (text.Contains("://"))
                    {
                        try
                        {
                            var uri = new Uri(text);
                            return $"http:{uri.Host}";
                        }
                        catch
                        {
                        }
                    }
                    return $"http:{text}";
                }
            }
        }
        return "http:unknown-service";
    }

    private static string? ExtractCSharpAttributeRoute(Node attributeNode)
    {
        var nameNode = attributeNode.Children.FirstOrDefault(c => c.Type == "identifier");
        if (nameNode == null) return null;
        var name = nameNode.Text;
        if (name != "Route" && name != "HttpGet" && name != "HttpPost" && name != "HttpPut" && name != "HttpDelete" && name != "HttpPatch")
        {
            return null;
        }

        var argList = attributeNode.Children.FirstOrDefault(c => c.Type == "attribute_argument_list");
        string routeVal = "/";
        if (argList != null)
        {
            var arg = argList.Children.FirstOrDefault(c => c.Type == "attribute_argument");
            if (arg != null)
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode != null)
                {
                    routeVal = strNode.Text.Trim('"');
                }
            }
        }

        var method = name == "Route" ? "GET" : name.Replace("Http", "").ToUpperInvariant();
        return $"{method}:{routeVal}";
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references)
    {
        TryDetectCalls(node, scopeSymbolId, references);
        TryDetectInheritsFromAndImplements(node, scopeSymbolId, references);
        if (node.Type.Contains("string") &&
            node.Type != "interpolated_string_expression" &&
            node.Type != "interpolated_verbatim_string_expression" &&
            node.Type != "interpolated_raw_string_expression")
        {
            NestedSqlParser.TryDetectSqlDependencies(node.Text, scopeSymbolId, references);
        }
    }

    private void TryDetectCalls(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "invocation_expression")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
            }
        }
    }

    private void TryDetectInheritsFromAndImplements(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "base_list")
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

    private readonly CSharpSemanticAnalyzer _analyzer;

    public ISemanticAnalyzer GetSemanticAnalyzer() => _analyzer;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _csProjCache = new(StringComparer.OrdinalIgnoreCase);

    private ImportType ResolveCsImportType(string importPath, string filePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        var dir = Path.GetDirectoryName(filePath);
        var csprojFile = FindCsprojFile(dir);
        if (csprojFile != null)
        {
            var rootNamespace = _csProjCache.GetOrAdd(csprojFile, f => Path.GetFileNameWithoutExtension(f));
            var rootPrefix = rootNamespace.Split('.')[0]; // e.g. "CodeExplorer" from "CodeExplorer.Core"

            if (importPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                importPath.StartsWith(rootNamespace, StringComparison.OrdinalIgnoreCase))
            {
                return ImportType.Internal;
            }
        }

        // Standard built-in .NET namespaces
        var builtInPrefixes = new[] { "System", "Microsoft.Win32", "Microsoft.CSharp", "Microsoft.VisualBasic" };
        foreach (var prefix in builtInPrefixes)
        {
            if (importPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                importPath.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            {
                return ImportType.External;
            }
        }

        return ImportType.External;
    }

    private string? FindCsprojFile(string? dir)
    {
        while (dir != null && Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.csproj");
            if (files.Length > 0)
                return files[0];
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public void CollectSemanticData(Node node, string filePath, ParsingContext ctx)
    {
        if (node.Type == "using_directive")
        {
            var nameNode = node.GetChildForField("name");
            if (nameNode == null || nameNode.Id == IntPtr.Zero)
            {
                nameNode = node.Children.FirstOrDefault(c => c.Type is "qualified_name" or "identifier");
            }
            if (nameNode != null && nameNode.Id != IntPtr.Zero)
            {
                var importPath = nameNode.Text;
                var type = ResolveCsImportType(importPath, filePath);
                ctx.AddRawImport(new RawImport(importPath, filePath, type));
            }
        }
        else if (node.Type == "variable_declarator" || node.Type == "property_declaration")
        {
            var name = node.GetChildForField("name")?.Text;
            if (string.IsNullOrEmpty(name))
            {
                name = node.Children.FirstOrDefault(c => c.Type == "identifier")?.Text;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetChildForField("value");
                if (valueNode == null || valueNode.Id == IntPtr.Zero)
                {
                    var eqClause = node.Children.FirstOrDefault(c => c.Type == "equals_value_clause");
                    if (eqClause != null && eqClause.Children.Count > 1)
                    {
                        valueNode = eqClause.Children[1];
                    }
                }
                string initializerText = valueNode != null && valueNode.Id != IntPtr.Zero ? valueNode.Text : "";
                bool isConstant = IsCSharpConstant(node);
                string scope = DetermineCSharpScope(node);

                ctx.AddRawVariable(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    filePath,
                    node.StartPosition.Row,
                    node.EndPosition.Row,
                    node.StartPosition.Column,
                    node.EndPosition.Column
                ));
            }
        }
    }

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "field_declaration" or "local_declaration_statement")
            {
                foreach (var child in curr.Children)
                {
                    if (child.Type is "const" or "readonly" || child.Text is "const" or "readonly")
                        return true;
                }
            }
            curr = curr.Parent;
        }
        return false;
    }

    private static string DetermineCSharpScope(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "class_declaration" or "struct_declaration" or "record_declaration" or "interface_declaration")
                return "class";
            if (curr.Type is "method_declaration" or "local_function_statement" or "block" or "constructor_declaration")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }
}
