using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public abstract class BaseSemanticAnalyzer : ISemanticAnalyzer
{
    protected readonly IEnumerable<ILibraryParser> _libraryParsers;

    private static readonly List<ILibraryParser> StandardLibraryParsers =
    [
        // Cloud Services
        new GenericLibraryParser("StripeLibraryParser", "cloud", ["stripe", "stripe-go", "Stripe"], cloudService: "Stripe"),
        new GenericLibraryParser("AwsLibraryParser", "cloud", ["Amazon.S3", "aws-sdk", "boto3", "github.com/aws/aws-sdk-go", "@aws-sdk"], cloudService: "AWS"),
        new GenericLibraryParser("GcpLibraryParser", "cloud", ["google-cloud-", "google.cloud", "@google-cloud/", "cloud.google.com/", "firebase"], cloudService: "GCP"),
        new GenericLibraryParser("AzureLibraryParser", "cloud", ["Azure.", "@azure/", "azure-", "/Azure/", "/azure-sdk-for-go"], cloudService: "Azure"),

        // Common API Libraries
        new GenericLibraryParser("AxiosLibraryParser", "api", ["axios"], apiLibrary: "Axios"),
        new GenericLibraryParser("HttpClientLibraryParser", "api", ["system.net.http", "httpclient", "net/http", "http", "https", "requests", "urllib", "httpx", "aiohttp"], apiLibrary: "HttpClient"),
        new GenericLibraryParser("RestSharpLibraryParser", "api", ["restsharp"], apiLibrary: "RestSharp"),
        new GenericLibraryParser("FlurlLibraryParser", "api", ["flurl"], apiLibrary: "Flurl"),
        new GenericLibraryParser("RefitLibraryParser", "api", ["refit"], apiLibrary: "Refit"),
        new GenericLibraryParser("RestyLibraryParser", "api", ["resty"], apiLibrary: "Resty"),
        new GenericLibraryParser("ReqLibraryParser", "api", ["req"], apiLibrary: "req"),
        new GenericLibraryParser("GrequestsLibraryParser", "api", ["grequests"], apiLibrary: "grequests"),
        new GenericLibraryParser("UndiciLibraryParser", "api", ["undici"], apiLibrary: "undici")
    ];

    protected BaseSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers)
    {
        var active = libraryParsers ?? Array.Empty<ILibraryParser>();
        // Merge standard library parsers with language specific library parsers
        _libraryParsers = active.Concat(StandardLibraryParsers).ToList();
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

        var internalPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in projectNode.Children.OfType<PackageNode>())
        {
            internalPackages.Add(child.Name);
        }

        foreach (var file in files)
        {
            var relativePath = file.Path;
            var fileImports = ctx.RawImports.Where(i => i.FilePath == relativePath).ToList();

            ILibraryParser? dbParser = null;
            ILibraryParser? apiParser = null;
            ILibraryParser? cloudParser = null;

            foreach (var import in fileImports)
            {
                var clean = Path.GetFileName(import.Path);
                var parser = _libraryParsers.FirstOrDefault(lp => lp.SupportedLibraries.Any(sl =>
                    sl.Equals(import.Path, StringComparison.OrdinalIgnoreCase) ||
                    sl.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                    import.Path.StartsWith(sl + ".", StringComparison.OrdinalIgnoreCase) ||
                    import.Path.StartsWith(sl + "/", StringComparison.OrdinalIgnoreCase) ||
                    (sl.Contains('/') && import.Path.StartsWith(sl, StringComparison.OrdinalIgnoreCase))));

                if (parser != null)
                {
                    if (parser.Category == "database" && dbParser == null)
                    {
                        dbParser = parser;
                    }
                    else if (parser.Category == "api" && apiParser == null)
                    {
                        apiParser = parser;
                    }
                    else if (parser.Category == "cloud" && cloudParser == null)
                    {
                        cloudParser = parser;
                    }
                }
            }

            if (dbParser != null)
            {
                var dbEngine = dbParser.DbEngine ?? "unknown";
                var dbType = dbParser.DbType ?? "unknown";
                file.SetExtension("db_type", dbType);
                var dbId = $"{ctx.WorkspaceId}:db:{dbEngine.ToLowerInvariant()}";
                var dbNode = new DbNode(dbId, dbEngine, dbId);
                dbNode.SetExtension("db_type", dbType);
                file.Children.Add(dbNode);
            }

            if (apiParser != null)
            {
                var apiLib = apiParser.ApiLibrary ?? "unknown";
                file.SetExtension("api_library", apiLib);
            }

            if (cloudParser != null)
            {
                var cloudService = cloudParser.CloudService ?? "unknown";
                file.SetExtension("cloud_service", cloudService);
                var cloudId = $"{ctx.WorkspaceId}:cloud:{cloudService.ToLowerInvariant()}";
                var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                file.Children.Add(cloudNode);
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
