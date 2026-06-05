using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public abstract class BaseSemanticAnalyzer : ISemanticAnalyzer
{
    protected readonly IEnumerable<ILibraryParser> _libraryParsers;

    protected BaseSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers)
    {
        _libraryParsers = libraryParsers ?? Array.Empty<ILibraryParser>();
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
                projectPackages.Any(pp => lp.Supports(pp))
            ).ToList();
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
                var parser = activeLibraryParsers.FirstOrDefault(lp => lp.Supports(import));
                if (parser != null && !matchedParsers.Contains(parser))
                {
                    matchedParsers.Add(parser);
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
                        
                        var dbId = $"{projectNode.Id}db:{parser.LibraryId}";
                        lock (projectNode.Children)
                        {
                            if (!projectNode.Children.Any(c => c.Id == dbId))
                            {
                                var dbNode = new DbNode(dbId, dbEngine, dbId);
                                dbNode.SetExtension("db_type", dbType);
                                projectNode.Children.Add(dbNode);
                            }
                        }
                        
                        var usesDbRel = new UsesDbRelationship(file.Id, dbId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesDbRel));
                        break;

                    case "api":
                        var apiId = $"{projectNode.Id}api:{parser.LibraryId}";
                        lock (projectNode.Children)
                        {
                            if (!projectNode.Children.Any(c => c.Id == apiId))
                            {
                                var apiNode = new ApiNode(apiId, parser.LibraryName, apiId);
                                projectNode.Children.Add(apiNode);
                            }
                        }

                        var usesApiRel = new UsesApiRelationship(file.Id, apiId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesApiRel));
                        break;

                    case "cloud":
                        var cloudService = parser.LibraryName;
                        var cloudId = $"{projectNode.Id}cloud:{parser.LibraryId}";
                        lock (projectNode.Children)
                        {
                            if (!projectNode.Children.Any(c => c.Id == cloudId))
                            {
                                var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                                projectNode.Children.Add(cloudNode);
                            }
                        }

                        var usesCloudRel = new UsesCloudRelationship(file.Id, cloudId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesCloudRel));
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
