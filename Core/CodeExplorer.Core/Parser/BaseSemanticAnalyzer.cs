using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public abstract class BaseSemanticAnalyzer : ISemanticAnalyzer
{
    protected readonly IEnumerable<ILibraryParser> _libraryParsers;
    protected readonly HashSet<string> _supportedLibraryNames;

    protected BaseSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers)
    {
        _libraryParsers = libraryParsers ?? Array.Empty<ILibraryParser>();
        _supportedLibraryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lp in _libraryParsers)
        {
            foreach (var lib in lp.SupportedLibraries)
            {
                _supportedLibraryNames.Add(lib);
            }
        }
    }

    protected static readonly Regex ConfigRegex = new(
        @"(?i)(config|settings?|cfg|\benv\b|db_?conn|\burl\b|\buri\b|\bport\b|\bhost\b|user(name)?|pass(word)?|token|secret|\bkey\b|auth|api_?key|connection_?string)",
        RegexOptions.Compiled
    );

    protected static readonly Regex ConfigInitializerRegex = new(
        @"(?i)(process\.env|Configuration\[|Environment\.GetEnvironmentVariable|System\.Environment|import\.meta\.env|dotenv|require\(['""]dotenv['""]\))",
        RegexOptions.Compiled
    );

    protected static readonly Regex EtlRegex = new(
        @"(?i)(\betl\b|\bsql\b|\bquery\b|\bselect\b|\binsert\b|\bupsert\b|\bschema\b|\btable\b|\bcolumn\b|\bdatabase\b|\bmigration\b|\bextract\b|\btransform\b|\bload\b)",
        RegexOptions.Compiled
    );

    protected static readonly Regex SqlQueryRegex = new(
        @"(?i)^[\s@$""'\`]*\s*(select|insert|update|delete|create\s+table|drop\s+table|merge|alter\s+table)\b",
        RegexOptions.Compiled
    );



    private static bool IsLibraryMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;

        var aParts = a.Split(new[] { '.', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var bParts = b.Split(new[] { '.', '/' }, StringSplitOptions.RemoveEmptyEntries);

        var minLen = Math.Min(aParts.Length, bParts.Length);
        if (minLen == 0) return false;

        for (int i = 0; i < minLen; i++)
        {
            if (!aParts[i].Equals(bParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private string? FindResolvedLibraryName(string importPath, HashSet<string> activeLibraryNames)
    {
        var clean = Path.GetFileName(importPath);
        if (activeLibraryNames.Contains(importPath)) return importPath;
        if (activeLibraryNames.Contains(clean)) return clean;

        foreach (var lib in activeLibraryNames)
        {
            if (IsLibraryMatch(importPath, lib))
            {
                return lib;
            }
        }
        return null;
    }

    public virtual async Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        var files = new List<FileNode>();
        FindAllFiles(projectNode, files);

        var projectPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var internalPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in projectNode.Children.OfType<PackageNode>())
        {
            projectPackages.Add(child.Name);
            internalPackages.Add(child.Name);
        }

        // Determine active library parsers based on project-level packages + built-in standard ones
        var activeLibraryParsers = _libraryParsers.ToList();
        if (projectPackages.Count > 0)
        {
            activeLibraryParsers = _libraryParsers.Where(lp =>
                lp.IsBuiltIn ||
                lp.SupportedLibraries.Any(sl =>
                    projectPackages.Any(pp => IsLibraryMatch(pp, sl))
                )
            ).ToList();
        }

        var activeLibraryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lp in activeLibraryParsers)
        {
            foreach (var lib in lp.SupportedLibraries)
            {
                activeLibraryNames.Add(lib);
            }
        }

        // Detect and enrich project-level framework
        foreach (var lp in activeLibraryParsers)
        {
            if (lp.LibraryType == "framework")
            {
                projectNode.SetExtension("framework", lp.LibraryName);
                break;
            }
        }

        foreach (var file in files)
        {
            var relativePath = file.Path;
            // Extract libraries used as list of string
            var fileImports = ctx.RawImports
                .Where(i => i.FilePath == relativePath)
                .Select(i => i.Path)
                .ToList();

            var matchedParsers = new List<ILibraryParser>();
            foreach (var import in fileImports)
            {
                // Try to resolve each library against only the active project-level packages
                var resolvedName = FindResolvedLibraryName(import, activeLibraryNames);
                if (resolvedName != null)
                {
                    var parser = activeLibraryParsers.FirstOrDefault(lp => lp.SupportedLibraries.Any(sl =>
                        sl.Equals(resolvedName, StringComparison.OrdinalIgnoreCase)));

                    if (parser != null && !matchedParsers.Contains(parser))
                    {
                        matchedParsers.Add(parser);
                    }
                }
            }

            foreach (var parser in matchedParsers)
            {
                var mainType = parser.LibraryType.Split(':')[0].ToLowerInvariant();
                switch (mainType)
                {
                    case "db":
                        var dbEngine = parser.LibraryName;
                        var dbType = "unknown";
                        var parts = parser.LibraryType.Split(':');
                        if (parts.Length > 1)
                        {
                            dbType = parts[1];
                        }
                        file.SetExtension("db_type", dbType);
                        var dbId = $"{ctx.WorkspaceId}:db:{parser.LibraryId}";
                        var dbNode = new DbNode(dbId, dbEngine, dbId);
                        dbNode.SetExtension("db_type", dbType);
                        file.Children.Add(dbNode);
                        break;

                    case "api":
                        file.SetExtension("api_library", parser.LibraryName);
                        break;

                    case "cloud":
                        var cloudService = parser.LibraryName;
                        file.SetExtension("cloud_service", cloudService);
                        var cloudId = $"{ctx.WorkspaceId}:cloud:{parser.LibraryId}";
                        var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                        file.Children.Add(cloudNode);
                        break;
                }
            }

            foreach (var child in projectNode.Children.OfType<PackageNode>())
            {
                var isInternal = internalPackages.Contains(child.Name);
                child.SetExtension("is_external", isInternal ? "false" : "true");
            }

            var fileVariables = ctx.RawVariables.Where(v => v.FilePath == relativePath).ToList();
            foreach (var rawVar in fileVariables)
            {
                var isConfig = ConfigRegex.IsMatch(rawVar.Name) || ConfigInitializerRegex.IsMatch(rawVar.InitializerText);
                var isEtl = EtlRegex.IsMatch(rawVar.Name) || SqlQueryRegex.IsMatch(rawVar.InitializerText);
                var isConstant = rawVar.IsConstant;
                var isGlobal = rawVar.Scope == "global";

                if (isConfig || isEtl || isConstant || isGlobal)
                {
                    var varTypeStr = new List<string>();
                    if (isConfig) varTypeStr.Add("config");
                    if (isEtl) varTypeStr.Add("etl");
                    if (isConstant) varTypeStr.Add("constant");
                    if (isGlobal) varTypeStr.Add("global");

                    var varType = string.Join(",", varTypeStr);
                    var varId = $"{ctx.WorkspaceId}:symbol:{relativePath}:Variable:{rawVar.Name}:{rawVar.StartLine}";

                    var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["variable_type"] = varType,
                        ["initializer_expression"] = rawVar.InitializerText,
                        ["is_constant"] = isConstant ? "true" : "false"
                    };

                    var varNode = new VariableNode(
                        varId,
                        rawVar.Name,
                        varId,
                        file.FullPath,
                        varId,
                        rawVar.StartLine,
                        rawVar.EndLine,
                        rawVar.StartCol,
                        rawVar.EndCol,
                        ext
                    );

                    TryInsertVariable(file, varNode, rawVar.StartLine);
                }
            }
        }
        await Task.CompletedTask;
    }

    private static void FindAllFiles(IOntologyNode node, List<FileNode> files)
    {
        if (node is FileNode f)
        {
            files.Add(f);
        }
        foreach (var child in node.Children)
        {
            FindAllFiles(child, files);
        }
    }

    private static bool TryInsertVariable(IOntologyNode parentNode, VariableNode varNode, int line)
    {
        foreach (var child in parentNode.Children)
        {
            if (child is ClassNode cn && line >= cn.StartLine && line <= cn.EndLine)
            {
                if (TryInsertVariable(cn, varNode, line)) return true;
            }
            if (child is FunctionNode fn && line >= fn.StartLine && line <= fn.EndLine)
            {
                if (TryInsertVariable(fn, varNode, line)) return true;
            }
        }

        parentNode.Children.Add(varNode);
        return true;
    }
}
