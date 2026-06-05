using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python;

public class PythonParser : IProjectParser, IFileParser
{
    public string LanguageName => "python";

    public string ProjectType => "python";

    public IReadOnlyCollection<string> ExcludedFolders => ["venv", ".venv", "__pycache__"];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.ChromaDbLibraryParser(),
        new Libraries.CouchDbPythonLibraryParser(),
        new Libraries.ElasticsearchPythonLibraryParser(),
        new Libraries.MysqlConnectorPythonLibraryParser(),
        new Libraries.PeeweeLibraryParser(),
        new Libraries.PineconeLibraryParser(),
        new Libraries.Psycopg2LibraryParser(),
        new Libraries.PyMongoLibraryParser(),
        new Libraries.PyMysqlLibraryParser(),
        new Libraries.PythonRedisLibraryParser(),
        new Libraries.PythonSqlite3LibraryParser(),
        new Libraries.SqlAlchemyLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("stripe", "Stripe", "cloud", ["stripe"]),
        new GenericLibraryParser("aws", "AWS", "cloud", ["boto3"]),
        new GenericLibraryParser("gcp", "GCP", "cloud", ["google-cloud-", "google.cloud", "firebase-admin"]),
        new GenericLibraryParser("azure", "Azure", "cloud", ["azure-", "azure."]),

        // Generic Frameworks
        new GenericLibraryParser("django", "Django", "framework", ["django"]),
        new GenericLibraryParser("flask", "Flask", "framework", ["flask"]),
        new GenericLibraryParser("fastapi", "FastAPI", "framework", ["fastapi"]),

        // Generic API Clients
        new GenericLibraryParser("requests", "requests", "api", ["requests"]),
        new GenericLibraryParser("urllib", "requests", "api", ["urllib.request", "urllib3", "urllib"], isBuiltIn: true),
        new GenericLibraryParser("httpx", "httpx", "api", ["httpx"]),
        new GenericLibraryParser("aiohttp", "aiohttp", "api", ["aiohttp"]),
    ];

    public PythonParser()
    {
    }

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName is "requirements.txt" or "pyproject.toml" or "setup.py" or "setup.cfg")
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

        if (IsPythonDecoratorEntryPoint(node))
        {
            return "EntryPoint";
        }

        if (IsDjangoPath(node))
        {
            return "EntryPoint";
        }

        if (IsPythonHttpClientCall(node))
        {
            return "ExternalService";
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

        if (IsPythonDecoratorEntryPoint(node))
        {
            return ExtractPythonDecoratorRoute(node);
        }

        if (IsDjangoPath(node))
        {
            return ExtractDjangoPathRoute(node);
        }

        if (IsPythonHttpClientCall(node))
        {
            return ExtractPythonHttpClientTarget(node);
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

        // If this is a function_definition preceded by a decorator, check if parent is decorated_definition
        if (node.Type == "function_definition")
        {
            var parent = node.Parent;
            if (parent != null && parent.Type == "decorated_definition")
            {
                foreach (var child in parent.Children)
                {
                    if (IsPythonDecoratorEntryPoint(child))
                    {
                        var route = ExtractPythonDecoratorRoute(child);
                        if (!string.IsNullOrEmpty(route))
                        {
                            references.Add(new Reference(scopeSymbolId, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                        }
                    }
                }
            }
        }

        if (IsDjangoPath(node))
        {
            var route = ExtractDjangoPathRoute(node);
            if (!string.IsNullOrEmpty(route))
            {
                var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
                if (args != null && args.Children.Count > 1)
                {
                    var viewArg = args.Children.Skip(1).FirstOrDefault(c => c.Type is "identifier" or "attribute");
                    if (viewArg != null)
                    {
                        var viewName = viewArg.Text;
                        if (viewName.Contains('.'))
                        {
                            viewName = viewName.Split('.').Last();
                        }
                        references.Add(new Reference(viewName, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                }
            }
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
                var nameMatch = Regex.Match(content, @"name\s*=\s*['""]([^'""]+)['""]");
                if (nameMatch.Success)
                {
                    var name = nameMatch.Groups[1].Value;
                    var versionMatch = Regex.Match(content, @"version\s*=\s*['""]([^'""]+)['""]");
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
                        var match = Regex.Match(line, @"^([a-zA-Z0-9_\-\[\]]+)(.*)$");
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
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await TreeSitterFileParser.ParseFileAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISemanticModel GetSemanticModel(SyntaxTree syntaxTree) => new PythonSemanticModel(LibraryParsers, syntaxTree);

    private readonly ConcurrentDictionary<string, HashSet<string>> _pyRootCache = new(StringComparer.OrdinalIgnoreCase);

    private ImportType ResolvePyImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        // Python relative imports start with '.'
        if (importPath.StartsWith('.'))
            return ImportType.Internal;

        var dir = Path.GetDirectoryName(filePath);
        var projectRoot = FindPythonProjectRoot(dir, absoluteWorkspacePath);
        if (projectRoot != null)
        {
            var internalNames = _pyRootCache.GetOrAdd(projectRoot, r => LoadLocalPythonNames(r));
            var parts = importPath.Split('.');
            var firstSegment = parts[0];

            if (internalNames.Contains(firstSegment))
            {
                return ImportType.Internal;
            }
        }

        return ImportType.External;
    }

    private string? FindPythonProjectRoot(string? dir, string? workspaceRoot)
    {
        var current = dir;
        string? bestRoot = null;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "requirements.txt")) ||
                File.Exists(Path.Combine(current, "pyproject.toml")) ||
                File.Exists(Path.Combine(current, "setup.py")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            if (workspaceRoot != null && current.Replace('\\', '/').Equals(workspaceRoot.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                bestRoot = current;
            }
            current = Path.GetDirectoryName(current);
        }
        return bestRoot ?? dir;
    }

    private HashSet<string> LoadLocalPythonNames(string projectRoot)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(projectRoot))
            {
                foreach (var d in Directory.GetDirectories(projectRoot))
                {
                    var name = Path.GetFileName(d);
                    var lower = name.ToLowerInvariant();
                    if (lower == "venv" || lower == "env" || lower == ".venv" || lower == "build" || lower == "dist" || lower == ".git")
                        continue;
                    names.Add(name);
                }
                foreach (var f in Directory.GetFiles(projectRoot, "*.py"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    names.Add(name);
                }
            }
        }
        catch
        {
            // Ignore
        }
        return names;
    }

    public void CollectSemanticData(Node node, string filePath, List<RawImport> rawImports, List<RawVariable> rawVariables)
    {
        if (node.Type == "import_statement")
        {
            foreach (var child in node.Children)
            {
                if (child.Type is "dotted_name" or "aliased_name")
                {
                    var importPath = child.Text;
                    var type = ResolvePyImportType(importPath, filePath, null);
                    rawImports.Add(new RawImport(importPath, filePath, type));
                }
            }
        }
        else if (node.Type == "import_from_statement")
        {
            var moduleNode = node.GetChildForField("module_name");
            if (moduleNode == null || moduleNode.Id == IntPtr.Zero)
            {
                moduleNode = node.Children.FirstOrDefault(c => c.Type == "dotted_name");
            }
            if (moduleNode != null && moduleNode.Id != IntPtr.Zero)
            {
                var importPath = moduleNode.Text;
                var type = ResolvePyImportType(importPath, filePath, null);
                rawImports.Add(new RawImport(importPath, filePath, type));
            }
        }
        else if (node.Type == "assignment")
        {
            var leftNode = node.GetChildForField("left");
            if (leftNode == null || leftNode.Id == IntPtr.Zero)
            {
                leftNode = node.Children.FirstOrDefault(c => c.Type == "identifier");
            }

            var rightNode = node.GetChildForField("right");
            if (rightNode == null || rightNode.Id == IntPtr.Zero)
            {
                int eqIdx = -1;
                for (int i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i].Text == "=")
                    {
                        eqIdx = i;
                        break;
                    }
                }
                if (eqIdx >= 0 && eqIdx + 1 < node.Children.Count)
                {
                    rightNode = node.Children[eqIdx + 1];
                }
            }

            if (leftNode != null && leftNode.Id != IntPtr.Zero && leftNode.Type == "identifier")
            {
                var name = leftNode.Text;
                var initializerText = rightNode != null && rightNode.Id != IntPtr.Zero ? rightNode.Text : "";

                bool isConstant = name.All(c => !char.IsLower(c));
                string scope = DeterminePythonScope(node);

                rawVariables.Add(new RawVariable(
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

    private static string DeterminePythonScope(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type == "class_definition")
                return "class";
            if (curr.Type == "function_definition")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static bool IsPythonDecoratorEntryPoint(Node node)
    {
        if (node.Type != "decorator") return false;
        var call = node.Children.FirstOrDefault(c => c.Type == "call");
        if (call == null) return false;
        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "attribute")
        {
            var attr = func.GetChildForField("attribute");
            if (attr != null && attr.Id != IntPtr.Zero)
            {
                var attrName = attr.Text;
                if (attrName is "route" or "get" or "post" or "put" or "delete" or "patch")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string? ExtractPythonDecoratorRoute(Node decoratorNode)
    {
        var call = decoratorNode.Children.FirstOrDefault(c => c.Type == "call");
        if (call == null) return null;
        var func = call.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && call.Children.Count > 0)) func = call.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return null;

        string method = "GET";
        if (func.Type == "attribute")
        {
            var attr = func.GetChildForField("attribute");
            if (attr != null && attr.Id != IntPtr.Zero)
            {
                var attrName = attr.Text;
                if (attrName != "route")
                {
                    method = attrName.ToUpperInvariant();
                }
                else
                {
                    var argList = call.Children.FirstOrDefault(c => c.Type == "argument_list");
                    if (argList != null)
                    {
                        var keywordArg = argList.Children.FirstOrDefault(c => c.Type == "keyword_argument" && c.Text.StartsWith("methods"));
                        if (keywordArg != null)
                        {
                            var listNode = keywordArg.Children.FirstOrDefault(c => c.Type == "list");
                            if (listNode != null)
                            {
                                var firstStr = listNode.Children.FirstOrDefault(c => c.Type == "string");
                                if (firstStr != null)
                                {
                                    method = firstStr.Text.Trim('\'', '"').ToUpperInvariant();
                                }
                            }
                        }
                    }
                }
            }
        }

        var args = call.Children.FirstOrDefault(c => c.Type == "argument_list");
        string routeVal = "/";
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type == "string");
            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('\'', '"');
            }
        }

        return $"{method}:{routeVal}";
    }

    private static bool IsDjangoPath(Node node)
    {
        if (node.Type != "call") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        return func.Type == "identifier" && (func.Text == "path" || func.Text == "re_path");
    }

    private static string? ExtractDjangoPathRoute(Node callNode)
    {
        var args = callNode.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type == "string");
            if (firstArg != null)
            {
                var routeVal = firstArg.Text.Trim('\'', '"');
                return $"GET:{routeVal}";
            }
        }
        return "GET:/";
    }

    private static bool IsPythonHttpClientCall(Node node)
    {
        if (node.Type != "call") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "attribute")
        {
            var obj = func.GetChildForField("value") ?? func.GetChildForField("object") ?? (func.Children.Count > 0 ? func.Children[0] : null);
            var attr = func.GetChildForField("attribute");
            if (obj != null && attr != null && attr.Id != IntPtr.Zero)
            {
                var objName = obj.Text;
                var attrName = attr.Text;

                if (objName is "requests" or "httpx" or "urllib.request" or "urllib")
                {
                    return attrName is "get" or "post" or "put" or "delete" or "request" or "patch" or "head" or "urlopen";
                }
                if (objName.Contains("session") || objName.Contains("client") || objName.Contains("http"))
                {
                    return attrName is "get" or "post" or "put" or "delete" or "request" or "patch";
                }
            }
        }
        return false;
    }

    private static string? ExtractPythonHttpClientTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type == "string");
            if (firstArg != null)
            {
                var text = firstArg.Text.Trim('\'', '"');
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
        return "http:unknown-service";
    }
}
