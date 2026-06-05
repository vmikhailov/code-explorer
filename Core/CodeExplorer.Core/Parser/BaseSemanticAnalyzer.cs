using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public abstract class BaseSemanticAnalyzer : ISemanticAnalyzer
{
    protected readonly IReadOnlyList<ILibraryParser> _libraryParsers;
    protected readonly LibraryTrieRegistry _trieRegistry;

    protected BaseSemanticAnalyzer(IReadOnlyList<ILibraryParser> libraryParsers)
    {
        _libraryParsers = libraryParsers;
        _trieRegistry = new LibraryTrieRegistry(libraryParsers);
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
        var files = FindAllFiles(projectNode);

        var packageNames = projectNode.Children
            .OfType<PackageNode>()
            .Concat(projectNode.Children.OfType<DependenciesNode>().SelectMany(dn => dn.Children.OfType<PackageNode>()))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Detect and enrich project-level framework using the Trie
        ILibraryParser? frameworkParser = null;
        if (packageNames.Count > 0)
        {
            foreach (var pkg in packageNames)
            {
                var match = _trieRegistry.Match(pkg);
                if (match is { Type: "framework" } && frameworkParser == null)
                {
                    frameworkParser = match;
                }
            }

            // Fallback to built-in frameworks if no match found
            frameworkParser ??= _libraryParsers.FirstOrDefault(lp => lp.IsBuiltIn && lp.Type == "framework");
        }
        else
        {
            frameworkParser = _libraryParsers.FirstOrDefault(lp => lp.Type == "framework");
        }

        if (frameworkParser != null)
        {
            projectNode.SetExtension("framework", frameworkParser.Name);
        }

        foreach (var file in files)
        {
            var relativePath = file.Path;
            // Extract libraries used as list of string
            var fileImports = ctx.RawImports
                .Where(i => i.FilePath == relativePath && i.Type == ImportType.External)
                .Select(i => i.Path)
                .ToList();

            var matchedParsers = new List<ILibraryParser>();
            foreach (var import in fileImports)
            {
                var match = _trieRegistry.Match(import);
                if (match != null && !matchedParsers.Contains(match))
                {
                    matchedParsers.Add(match);
                }
            }

            foreach (var parser in matchedParsers)
            {
                var mainType = parser.Type.Split(':')[0].ToLowerInvariant();
                switch (mainType)
                {
                    case "db":
                        var dbEngine = parser.Name;
                        var dbType = "unknown";
                        var parts = parser.Type.Split(':');
                        if (parts.Length > 1)
                        {
                            dbType = parts[1];
                        }

                        var dbId = $"{projectNode.Id}db:{parser.Id}";
                        lock (projectNode.Children)
                        {
                            var databasesNode = projectNode.Children.OfType<DataBasesNode>().FirstOrDefault();
                            if (databasesNode == null)
                            {
                                var dbGroupNodeId = $"{projectNode.Id}databases";
                                databasesNode = new DataBasesNode(dbGroupNodeId, "DataBases", projectNode.Path);
                                projectNode.Children.Add(databasesNode);
                            }

                            if (databasesNode.Children.All(c => c.Id != dbId))
                            {
                                var dbNode = new DbNode(dbId, dbEngine, dbId);
                                dbNode.SetExtension("db_type", dbType);
                                databasesNode.Children.Add(dbNode);
                            }
                        }

                        var usesDbRel = new UsesDbRelationship(file.Id, dbId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesDbRel));
                        break;

                    case "api":
                        var apiId = $"{projectNode.Id}api:{parser.Id}";
                        lock (projectNode.Children)
                        {
                            var apisNode = projectNode.Children.OfType<ApisInUseNode>().FirstOrDefault();
                            if (apisNode == null)
                            {
                                var apiGroupNodeId = $"{projectNode.Id}apis";
                                apisNode = new ApisInUseNode(apiGroupNodeId, "ApisInUse", projectNode.Path);
                                projectNode.Children.Add(apisNode);
                            }

                            if (!apisNode.Children.Any(c => c.Id == apiId))
                            {
                                var apiNode = new ApiInUseNode(apiId, parser.Name, apiId);
                                apisNode.Children.Add(apiNode);
                            }
                        }

                        var usesApiRel = new UsesApiRelationship(file.Id, apiId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesApiRel));
                        break;

                    case "cloud":
                        var cloudService = parser.Name;
                        var cloudId = $"{projectNode.Id}cloud:{parser.Id}";
                        lock (projectNode.Children)
                        {
                            var cloudServicesNode = projectNode.Children.OfType<CloudServicesNode>().FirstOrDefault();
                            if (cloudServicesNode == null)
                            {
                                var cloudGroupNodeId = $"{projectNode.Id}cloudservices";
                                cloudServicesNode = new CloudServicesNode(cloudGroupNodeId, "CloudServices", projectNode.Path);
                                projectNode.Children.Add(cloudServicesNode);
                            }

                            if (!cloudServicesNode.Children.Any(c => c.Id == cloudId))
                            {
                                var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                                cloudServicesNode.Children.Add(cloudNode);
                            }
                        }

                        var usesCloudRel = new UsesCloudRelationship(file.Id, cloudId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesCloudRel));
                        break;
                }
            }

            // foreach (var child in projectNode.Children.OfType<PackageNode>())
            // {
            //     var isInternal = internalPackages.Contains(child.Name);
            //     child.SetExtension("is_external", isInternal ? "false" : "true");
            // }

            var fileVariables = ctx.RawVariables.Where(v => v.FilePath == relativePath).ToList();
            foreach (var rawVar in fileVariables)
            {
                var isConfig = ConfigRegex.IsMatch(rawVar.Name) ||
                               ConfigInitializerRegex.IsMatch(rawVar.InitializerText);
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

    private static List<FileNode> FindAllFiles(IOntologyNode node)
    {
        var files = new List<FileNode>();
        if (node is FileNode f)
        {
            files.Add(f);
        }

        foreach (var child in node.Children)
        {
            files.AddRange(FindAllFiles(child));
        }

        return files;
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
