using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class SyntaxEnricher : ISyntaxEnricher
{
    private readonly IReadOnlyList<ILibraryParser> _libraryParsers;
    private readonly LibraryTrieRegistry _trieRegistry;
    private readonly SyntaxTree _syntaxTree;

    public SyntaxEnricher(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree)
    {
        _libraryParsers = libraryParsers;
        _trieRegistry = new LibraryTrieRegistry(libraryParsers);
        _syntaxTree = syntaxTree;
    }

    private static readonly Regex ConfigRegex = new(
        @"(?i)(config|settings?|cfg|\benv\b|db_?conn|\burl\b|\buri\b|\bport\b|\bhost\b|user(name)?|pass(word)?|token|secret|\bkey\b|auth|api_?key|connection_?string)",
        RegexOptions.Compiled
    );

    private static readonly Regex ConfigInitializerRegex = new(
        @"(?i)(process\.env|Configuration\[|Environment\.GetEnvironmentVariable|System\.Environment|import\.meta\.env|dotenv|require\(['""]dotenv['""]\))",
        RegexOptions.Compiled
    );

    private static readonly Regex EtlRegex = new(
        @"(?i)(\betl\b|\bsql\b|\bquery\b|\bselect\b|\binsert\b|\bupsert\b|\bschema\b|\btable\b|\bcolumn\b|\bdatabase\b|\bmigration\b|\bextract\b|\btransform\b|\bload\b)",
        RegexOptions.Compiled
    );

    private static readonly Regex SqlQueryRegex = new(
        @"(?i)^[\s@$""'\`]*\s*(select|insert|update|delete|create\s+table|drop\s+table|merge|alter\s+table)\b",
        RegexOptions.Compiled
    );

    public virtual async Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        var packageNames = projectNode.Children
            .OfType<PackageNode>()
            .Concat(projectNode.Children.OfType<SemanticStructureNode>().SelectMany(dn => dn.Children.OfType<PackageNode>()))
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

        var fileNode = _syntaxTree.FileNode;
        if (fileNode != null)
        {
            var relativePath = fileNode.Path;
            // Extract libraries used as list of string
            var fileImports = _syntaxTree.RawImports
                .Where(i => i.Type == ImportType.External)
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
                        var semanticNodeForDb = GetOrCreateSemanticStructure(projectNode);
                        if (semanticNodeForDb.Children.All(c => c.Id != dbId))
                        {
                            var dbNode = new DatabaseNode(dbId, dbEngine, dbId, dbType);
                            semanticNodeForDb.Children.Add(dbNode);
                        }

                        var usesDbRel = new UsesDbRelationship(fileNode.Id, dbId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesDbRel));
                        break;

                    case "api":
                        var apiId = $"{projectNode.Id}api:{parser.Id}";
                        var semanticNodeForApi = GetOrCreateSemanticStructure(projectNode);
                        if (!semanticNodeForApi.Children.Any(c => c.Id == apiId))
                        {
                            var apiNode = new ApiInUseNode(apiId, parser.Name, apiId);
                            semanticNodeForApi.Children.Add(apiNode);
                        }

                        var usesApiRel = new UsesApiRelationship(fileNode.Id, apiId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesApiRel));
                        break;

                    case "cloud":
                        var cloudService = parser.Name;
                        var cloudId = $"{projectNode.Id}cloud:{parser.Id}";
                        var semanticNodeForCloud = GetOrCreateSemanticStructure(projectNode);
                        if (!semanticNodeForCloud.Children.Any(c => c.Id == cloudId))
                        {
                            var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                            semanticNodeForCloud.Children.Add(cloudNode);
                        }

                        var usesCloudRel = new UsesCloudRelationship(fileNode.Id, cloudId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesCloudRel));
                        break;
                }
            }

            var fileVariables = _syntaxTree.RawVariables;
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
                    var varId = $"{ctx.WorkspaceId}:symbol:{relativePath}:Member:{rawVar.Name}:{rawVar.StartLine}";

                    var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["variable_type"] = varType,
                        ["initializer_expression"] = rawVar.InitializerText,
                        ["is_constant"] = isConstant ? "true" : "false"
                    };

                    var varNode = new MemberNode(
                        varId,
                        rawVar.Name,
                        varId,
                        fileNode.FullPath,
                        varId,
                        rawVar.StartLine,
                        rawVar.EndLine,
                        rawVar.StartCol,
                        rawVar.EndCol,
                        "variable",
                        ext
                    );

                    TryInsertVariable(fileNode, varNode, rawVar.StartLine);
                }
            }
        }

        await Task.CompletedTask;
    }

    private static bool TryInsertVariable(IOntologyNode parentNode, MemberNode varNode, int line)
    {
        foreach (var child in parentNode.Children)
        {
            if (child is TypeNode cn && line >= cn.StartLine && line <= cn.EndLine)
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

    private static SemanticStructureNode GetOrCreateSemanticStructure(ProjectNode projectNode)
    {
        var node = projectNode.Children.OfType<SemanticStructureNode>().FirstOrDefault();
        if (node == null)
        {
            var id = $"{projectNode.Id}semantic_structure";
            node = new SemanticStructureNode(id, "SemanticStructure", projectNode.Path);
            projectNode.Children.Add(node);
        }
        return node;
    }
}
