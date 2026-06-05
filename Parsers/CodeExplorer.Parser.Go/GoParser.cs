using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go;

public class GoParser : IProjectParser, IFileParser
{
    public string LanguageName => "go";

    public string ProjectType => "go";

    public IReadOnlyCollection<string> ExcludedFolders => ["vendor"];

    public IEnumerable<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.ElasticsearchGoLibraryParser(),
        new Libraries.GoRedisLegacyLibraryParser(),
        new Libraries.GoRedisLibraryParser(),
        new Libraries.GormLibraryParser(),
        new Libraries.GoSqlDriverMysqlLibraryParser(),
        new Libraries.GoSqlite3LibraryParser(),
        new Libraries.GoSqlLibraryParser(),
        new Libraries.LibPqLibraryParser(),
        new Libraries.MongoGoLibraryParser()
    ];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "go.mod")
            {
                return true;
            }
        }
        return false;
    }

    public string? MapNodeType(Node node)
    {
        if (node.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out _, out _))
            {
                return "Query";
            }
        }

        if (IsGoEntryPoint(node))
        {
            return "EntryPoint";
        }

        if (IsGoHttpClientCall(node))
        {
            return "ExternalService";
        }

        if (node.Type == "type_spec")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "interface_type")
                {
                    return "Interface";
                }
            }
            return "Class";
        }

        return node.Type switch
        {
            "function_declaration" or
            "method_declaration" => "Function",

            "parameter_declaration" or
            "const_spec" or
            "var_spec" or
            "field_declaration" => "Variable",

            _ => null
        };
    }

    public string? ExtractIdentifier(Node node)
    {
        if (node.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal")
        {
            if (NestedSqlParser.TryParseSql(node.Text, out var firstWord, out _))
            {
                return $"{firstWord} Query";
            }
        }

        if (IsGoEntryPoint(node))
        {
            return ExtractGoEntryPointRoute(node);
        }

        if (IsGoHttpClientCall(node))
        {
            return ExtractGoHttpClientTarget(node);
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
        if (node.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal")
        {
            NestedSqlParser.TryDetectSqlDependencies(node.Text, scopeSymbolId, references);
        }

        if (IsGoEntryPoint(node))
        {
            var route = ExtractGoEntryPointRoute(node);
            if (!string.IsNullOrEmpty(route))
            {
                var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
                if (args != null && args.Children.Count > 1)
                {
                    var handlerArg = args.Children.Skip(1).FirstOrDefault(c => c.Type is "identifier" or "selector_expression");
                    if (handlerArg != null)
                    {
                        var handlerName = handlerArg.Text;
                        if (handlerName.Contains('.'))
                        {
                            handlerName = handlerName.Split('.').Last();
                        }
                        references.Add(new Reference(handlerName, route.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                }
            }
        }
    }

    private void TryDetectCalls(Node node, string scopeSymbolId, List<Reference> references)
    {
        if (node.Type == "call_expression")
        {
            var callName = FindCallName(node);
            if (!string.IsNullOrEmpty(callName))
            {
                references.Add(new Reference(scopeSymbolId, callName, "CALLS"));
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
        if (expr.Type == "selector_expression")
        {
            var fieldChild = expr.GetChildForField("field");
            if (fieldChild != null && fieldChild.Id != IntPtr.Zero) return fieldChild.Text;
        }
        return null;
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var moduleLine = lines.FirstOrDefault(l => l.Trim().StartsWith("module "));
            if (moduleLine != null)
            {
                var modName = moduleLine.Trim().Substring("module ".Length).Trim();
                if (!string.IsNullOrEmpty(modName))
                {
                    string version = "";
                    var versionFilePath = Path.Combine(projectDirectory, "VERSION");
                    if (File.Exists(versionFilePath))
                    {
                        version = (await File.ReadAllTextAsync(versionFilePath)).Trim();
                    }
                    else
                    {
                        try
                        {
                            using var process = new System.Diagnostics.Process();
                            process.StartInfo.FileName = "git";
                            process.StartInfo.Arguments = "describe --tags --always";
                            process.StartInfo.WorkingDirectory = projectDirectory;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.CreateNoWindow = true;
                            process.Start();
                            var output = await process.StandardOutput.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            if (process.ExitCode == 0)
                            {
                                version = output.Trim();
                            }
                        }
                        catch
                        {
                        }
                    }
                    return new ProducedPackageInfo(modName, version, "go");
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var inRequireBlock = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Handle single-line require
                if (line.StartsWith("require ") && !line.EndsWith("("))
                {
                    var content = line.Substring("require ".Length).Trim();
                    var parts = content.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
                else if (line.StartsWith("require ("))
                {
                    inRequireBlock = true;
                }
                else if (line == ")")
                {
                    inRequireBlock = false;
                }
                else if (inRequireBlock)
                {
                    // Line inside require block
                    var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public bool UsesTreeSitter => true;
    public Task<FileNode> ParseAsync(string filePath, string parentNodeId, ParsingContext ctx)
    {
        var relativePath = Path.GetRelativePath(ctx.AbsoluteWorkspacePath, filePath).Replace('\\', '/');
        return TreeSitterFileParser.ParseFileAsync(filePath, relativePath, parentNodeId, this, ctx);
    }

    private readonly GoSemanticAnalyzer _analyzer = new();

    public ISemanticAnalyzer GetSemanticAnalyzer() => _analyzer;

    public void CollectSemanticData(Node node, string filePath, ParsingContext ctx)
    {
        if (node.Type == "import_spec")
        {
            var pathNode = node.GetChildForField("path");
            if (pathNode == null || pathNode.Id == IntPtr.Zero)
            {
                pathNode = node.Children.FirstOrDefault(c => c.Type == "string_literal");
            }
            if (pathNode != null && pathNode.Id != IntPtr.Zero)
            {
                var importPath = pathNode.Text.Trim('"');
                ctx.AddRawImport(new RawImport(importPath, filePath));
            }
        }
        else if (node.Type is "var_spec" or "const_spec")
        {
            var identifiers = new List<Node>();
            var values = new List<Node>();
            bool passedTypeOrEq = false;

            foreach (var child in node.Children)
            {
                if (child.Text == "=" || child.Type.Contains("type"))
                {
                    passedTypeOrEq = true;
                }
                else if (!passedTypeOrEq && child.Type == "identifier")
                {
                    identifiers.Add(child);
                }
                else if (passedTypeOrEq && child.Type != "=")
                {
                    values.Add(child);
                }
            }

            bool isConstant = node.Type == "const_spec";
            string scope = DetermineGoScope(node);

            for (int i = 0; i < identifiers.Count; i++)
            {
                var name = identifiers[i].Text;
                var initializerText = i < values.Count ? values[i].Text : "";

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
        else if (node.Type == "short_var_declaration")
        {
            var leftNode = node.Children.FirstOrDefault();
            var rightNode = node.Children.LastOrDefault();

            if (leftNode != null && rightNode != null && leftNode.Id != rightNode.Id)
            {
                var names = new List<string>();
                if (leftNode.Type == "expression_list")
                {
                    names.AddRange(leftNode.Children.Where(c => c.Type == "identifier").Select(c => c.Text));
                }
                else if (leftNode.Type == "identifier")
                {
                    names.Add(leftNode.Text);
                }

                var values = new List<string>();
                if (rightNode.Type == "expression_list")
                {
                    values.AddRange(rightNode.Children.Select(c => c.Text));
                }
                else
                {
                    values.Add(rightNode.Text);
                }

                string scope = DetermineGoScope(node);
                for (int i = 0; i < names.Count; i++)
                {
                    var name = names[i];
                    var initializerText = i < values.Count ? values[i] : "";

                    ctx.AddRawVariable(new RawVariable(
                        name,
                        initializerText,
                        scope,
                        false,
                        filePath,
                        node.StartPosition.Row,
                        node.EndPosition.Row,
                        node.StartPosition.Column,
                        node.EndPosition.Column
                    ));
                }
            }
        }
    }

    private static string DetermineGoScope(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.Id != IntPtr.Zero)
        {
            if (curr.Type is "type_spec" or "struct_type" or "interface_type")
                return "class";
            if (curr.Type is "function_declaration" or "method_declaration" or "block")
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }

    private static bool IsGoEntryPoint(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "selector_expression")
        {
            var field = func.GetChildForField("field");
            if (field != null && field.Id != IntPtr.Zero)
            {
                var methodName = field.Text;
                if (methodName is "HandleFunc" or "Handle" or "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "OPTIONS" or "Any")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string? ExtractGoEntryPointRoute(Node callNode)
    {
        var func = callNode.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && callNode.Children.Count > 0)) func = callNode.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return null;

        string method = "GET";
        if (func.Type == "selector_expression")
        {
            var field = func.GetChildForField("field");
            if (field != null && field.Id != IntPtr.Zero)
            {
                var methodName = field.Text;
                if (methodName != "HandleFunc" && methodName != "Handle" && methodName != "Any")
                {
                    method = methodName.ToUpperInvariant();
                }
            }
        }

        var args = callNode.Children.FirstOrDefault(c => c.Type == "argument_list");
        string routeVal = "/";
        if (args != null && args.Children.Count > 1)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal");
            if (firstArg != null)
            {
                routeVal = firstArg.Text.Trim('"', '`');
            }
        }

        return $"{method}:{routeVal}";
    }

    private static bool IsGoHttpClientCall(Node node)
    {
        if (node.Type != "call_expression") return false;
        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "selector_expression")
        {
            var operand = func.GetChildForField("operand");
            var field = func.GetChildForField("field");
            if (operand != null && field != null && field.Id != IntPtr.Zero)
            {
                var objName = operand.Text;
                var methodName = field.Text;

                if (objName == "http")
                {
                    return methodName is "Get" or "Post" or "Head" or "PostForm" or "NewRequest" or "NewRequestWithContext";
                }
                if (objName.Contains("client") || objName.Contains("Client"))
                {
                    return methodName is "Get" or "Post" or "Head" or "PostForm" or "Do";
                }
            }
        }
        return false;
    }

    private static string? ExtractGoHttpClientTarget(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "argument_list");
        if (args != null && args.Children.Count > 1)
        {
            var firstStrArg = args.Children.FirstOrDefault(c => c.Type is "interpreted_string_literal" or "string_literal" or "raw_string_literal");
            if (firstStrArg != null)
            {
                var text = firstStrArg.Text.Trim('"', '`');
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
