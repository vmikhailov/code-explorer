using System.Collections.Concurrent;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.CSharp;

public class CSharpParser : IProjectParser, IFileParser
{
    public string LanguageName => "c-sharp";

    public string ProjectType => "csharp";

    public IReadOnlyCollection<string> ExcludedFolders => ["bin", "obj", ".vs"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith("appsettings") && lower.EndsWith(".json");
    }

    public IReadOnlyList<PackageDescriptor> Packages { get; } =
    [
        new PackageDescriptor("aspnetcore", "ASP.NET Core", LibraryRole.WebFramework, "nuget", ["Microsoft.AspNetCore", "Microsoft.AspNetCore.Mvc"], CustomMatch: ctx => ctx.ManifestProperties?.GetValueOrDefault("sdk") == "Microsoft.NET.Sdk.Web"),
        new PackageDescriptor("hotchocolate", "HotChocolate", LibraryRole.WebFramework, "nuget", ["HotChocolate"]),
        new PackageDescriptor("grpc", "gRPC", LibraryRole.WebFramework, "nuget", ["Grpc.AspNetCore", "Grpc.Net.Client"]),

        new PackageDescriptor("couchbase", "Couchbase", LibraryRole.OrmOrDatabase, "nuget", ["CouchbaseNetClient"]),
        new PackageDescriptor("dapper", "Dapper", LibraryRole.OrmOrDatabase, "nuget", ["Dapper"]),
        new PackageDescriptor("efcore", "EF Core", LibraryRole.OrmOrDatabase, "nuget", ["Microsoft.EntityFrameworkCore"]),
        new PackageDescriptor("elasticsearch", "Elasticsearch", LibraryRole.OrmOrDatabase, "nuget", ["Elasticsearch.Net"]),
        new PackageDescriptor("sqlclient", "Microsoft.Data.SqlClient", LibraryRole.OrmOrDatabase, "nuget", ["Microsoft.Data.SqlClient", "System.Data.SqlClient"]),
        new PackageDescriptor("mongodb", "MongoDB", LibraryRole.OrmOrDatabase, "nuget", ["MongoDB.Driver"]),
        new PackageDescriptor("mysql", "MySql.Data", LibraryRole.OrmOrDatabase, "nuget", ["MySql.Data", "MySqlConnector"]),
        new PackageDescriptor("nest", "NEST", LibraryRole.OrmOrDatabase, "nuget", ["Nest"]),
        new PackageDescriptor("npgsql", "Npgsql", LibraryRole.OrmOrDatabase, "nuget", ["Npgsql"]),
        new PackageDescriptor("oracle", "Oracle.ManagedDataAccess", LibraryRole.OrmOrDatabase, "nuget", ["Oracle.ManagedDataAccess"]),
        new PackageDescriptor("redis", "StackExchange.Redis", LibraryRole.OrmOrDatabase, "nuget", ["StackExchange.Redis"]),
        new PackageDescriptor("neo4j", "Neo4j", LibraryRole.OrmOrDatabase, "nuget", ["Neo4j.Driver"]),

        new PackageDescriptor("masstransit", "MassTransit", LibraryRole.MessageBroker, "nuget", ["MassTransit"]),
        new PackageDescriptor("mediatr", "MediatR", LibraryRole.General, "nuget", ["MediatR"]),
        new PackageDescriptor("kafkaflow", "KafkaFlow", LibraryRole.MessageBroker, "nuget", ["KafkaFlow"]),
        new PackageDescriptor("orleans", "Microsoft.Orleans", LibraryRole.General, "nuget", ["Microsoft.Orleans"]),
        new PackageDescriptor("flurl", "Flurl", LibraryRole.General, "nuget", ["Flurl.Http"]),
        new PackageDescriptor("httpclient", "HttpClient", LibraryRole.General, "nuget", ["System.Net.Http"]),
        new PackageDescriptor("restsharp", "RestSharp", LibraryRole.General, "nuget", ["RestSharp"]),
        new PackageDescriptor("refit", "Refit", LibraryRole.General, "nuget", ["Refit"]),
        new PackageDescriptor("webapiclient", "WebApiClient", LibraryRole.General, "nuget", ["WebApiClient"]),
        new PackageDescriptor("apizr", "Apizr", LibraryRole.General, "nuget", ["Apizr"]),
        new PackageDescriptor("notoriousclient", "NotoriousClient", LibraryRole.General, "nuget", ["NotoriousClient"]),

        // Generic Cloud Services
        new PackageDescriptor("stripe", "Stripe", LibraryRole.CloudSdk, "nuget", ["stripe", "Stripe"]),
        new PackageDescriptor("aws", "AWS", LibraryRole.CloudSdk, "nuget", ["Amazon.S3", "AWSSDK"]),
        new PackageDescriptor("gcp", "GCP", LibraryRole.CloudSdk, "nuget", ["Google.Cloud."]),
        new PackageDescriptor("azure", "Azure", LibraryRole.CloudSdk, "nuget", ["Azure."]),

        // Test Frameworks
        new PackageDescriptor("nunit", "NUnit", LibraryRole.TestFramework, "nuget", ["nunit", "nunit3testadapter"]),
        new PackageDescriptor("xunit", "xUnit", LibraryRole.TestFramework, "nuget", ["xunit", "xunit.runner.*", "xunit.v3.*"]),
        new PackageDescriptor("mstest", "MSTest", LibraryRole.TestFramework, "nuget", ["mstest", "mstest.testframework", "mstest.testadapter"]),
        new PackageDescriptor("benchmarkdotnet", "BenchmarkDotNet", LibraryRole.TestFramework, "nuget", ["benchmarkdotnet"]),
        new PackageDescriptor("nettestsdk", "Microsoft.NET.Test.Sdk", LibraryRole.TestFramework, "nuget", ["microsoft.net.test.sdk"])
    ];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.CouchbaseLibraryParser(),
        new Libraries.DapperLibraryParser(),
        new Libraries.EfCoreLibraryParser(),
        new Libraries.ElasticsearchNetLibraryParser(),
        new Libraries.FlurlLibraryParser(),
        new Libraries.HttpClientLibraryParser(),
        new Libraries.MicrosoftDataSqlClientLibraryParser(),
        new Libraries.MongoDbCsLibraryParser(),
        new Libraries.MySqlDataLibraryParser(),
        new Libraries.NestLibraryParser(),
        new Libraries.NpgsqlLibraryParser(),
        new Libraries.OracleDataAccessLibraryParser(),
        new Libraries.StackExchangeRedisLibraryParser(),
        new Libraries.Neo4jDriverLibraryParser(),
        new Libraries.AspNetCoreLibraryParser(),
        new Libraries.HotChocolateLibraryParser(),
        new Libraries.GrpcCSharpLibraryParser(),
        new Libraries.MassTransitLibraryParser(),
        new Libraries.MediatRLibraryParser(),
        new Libraries.KafkaFlowLibraryParser(),
        new Libraries.OrleansLibraryParser(),
        new Libraries.RestSharpLibraryParser(),
        new Libraries.RefitLibraryParser()
    ];

    public LanguageSyntaxProfile SyntaxProfile => CSharpSyntaxProfile.Instance;

    public CSharpParser()
    {
    }


    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        CSharpDeclarationExtractor.Extract(rootNode, registerDeclaration);
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

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var csprojFile = filesInDirectory.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csprojFile != null)
        {
            var name = Path.GetFileNameWithoutExtension(csprojFile);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        return Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
    }

    public ProjectClassification? ClassifyProject(ProjectContext context)
    {
        if (context.ManifestProperties != null)
        {
            var sdk = context.ManifestProperties.GetValueOrDefault("sdk");
            if (sdk == "Microsoft.NET.Sdk.Worker") return ProjectClassification.Worker;
            if (sdk == "Microsoft.NET.Sdk.BlazorWebAssembly") return ProjectClassification.WebApp;
            if (sdk == "Microsoft.NET.Sdk.Web") return ProjectClassification.Service;
            if (sdk == "Microsoft.Azure.Functions.Worker") return ProjectClassification.FunctionApp;
        }

        return IProjectParser.DefaultClassifyProject(this, context);
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var csprojFile = filesInDirectory.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csprojFile == null || !File.Exists(csprojFile)) return props;

        try
        {
            var content = File.ReadAllText(csprojFile);

            // 1. Inspect SDK
            if (content.Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Sdk='Microsoft.NET.Sdk.Web'", StringComparison.OrdinalIgnoreCase))
            {
                props["sdk"] = "Microsoft.NET.Sdk.Web";
                props["framework_type"] = "web";
                props["manifest_type"] = "application";
            }
            else if (content.Contains("Sdk=\"Microsoft.NET.Sdk.Worker\"", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("Sdk='Microsoft.NET.Sdk.Worker'", StringComparison.OrdinalIgnoreCase))
            {
                props["sdk"] = "Microsoft.NET.Sdk.Worker";
                props["framework_type"] = "worker";
                props["manifest_type"] = "worker";
            }
            else if (content.Contains("Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("Sdk='Microsoft.NET.Sdk.BlazorWebAssembly'", StringComparison.OrdinalIgnoreCase))
            {
                props["sdk"] = "Microsoft.NET.Sdk.BlazorWebAssembly";
                props["framework_type"] = "frontend";
                props["manifest_type"] = "application";
            }
            else if (content.Contains("Sdk=\"Microsoft.NET.Sdk.Razor\"", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("Sdk='Microsoft.NET.Sdk.Razor'", StringComparison.OrdinalIgnoreCase))
            {
                props["sdk"] = "Microsoft.NET.Sdk.Razor";
                props["framework_type"] = "web";
                props["manifest_type"] = "application";
            }
            else if (content.Contains("Sdk=\"Microsoft.NET.Sdk\"", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("Sdk='Microsoft.NET.Sdk'", StringComparison.OrdinalIgnoreCase))
            {
                props["sdk"] = "Microsoft.NET.Sdk";
            }

            if (content.Contains("Microsoft.Azure.Functions.Worker", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.NET.Sdk.Functions", StringComparison.OrdinalIgnoreCase))
            {
                props["manifest_type"] = "function";
            }

            if (content.Contains("<IsTestProject>true</IsTestProject>", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
            {
                props["manifest_type"] = "test";
            }

            // 2. Inspect <OutputType>
            var outputTypeMatch = System.Text.RegularExpressions.Regex.Match(content, @"<OutputType>\s*([A-Za-z0-9]+)\s*</OutputType>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (outputTypeMatch.Success)
            {
                var outputType = outputTypeMatch.Groups[1].Value;
                props["output_type"] = outputType;
                if (outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase) || outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!props.ContainsKey("manifest_type"))
                    {
                        props["manifest_type"] = "application";
                    }
                }
                else if (outputType.Equals("Library", StringComparison.OrdinalIgnoreCase))
                {
                    if (!props.ContainsKey("manifest_type") && props.GetValueOrDefault("sdk") != "Microsoft.NET.Sdk.Web")
                    {
                        props["manifest_type"] = "library";
                    }
                }
            }
            else
            {
                // In .NET SDK, OutputType defaults to Library if not Exe
                if (props.GetValueOrDefault("sdk") == "Microsoft.NET.Sdk" && !props.ContainsKey("manifest_type"))
                {
                    props["output_type"] = "Library";
                    props["manifest_type"] = "library";
                }
            }

            // 3. Inspect <IsPackable>
            var isPackableMatch = System.Text.RegularExpressions.Regex.Match(content, @"<IsPackable>\s*(true|false)\s*</IsPackable>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (isPackableMatch.Success)
            {
                props["is_packable"] = isPackableMatch.Groups[1].Value.ToLowerInvariant();
            }

            // 4. Inspect CLI / Tool properties (<PackAsTool>true</PackAsTool> or ToolCommandName)
            if (content.Contains("<PackAsTool>true</PackAsTool>", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("<ToolCommandName>", StringComparison.OrdinalIgnoreCase))
            {
                props["has_cli_bin"] = "true";
                props["manifest_type"] = "cli";
            }

            // 5. Inspect Ingress Contracts (GraphQL, gRPC, Controllers) for Web / Service projects
            var sdk = props.GetValueOrDefault("sdk");
            var isWeb = sdk == "Microsoft.NET.Sdk.Web" ||
                        string.Equals(props.GetValueOrDefault("framework_type"), "web", StringComparison.OrdinalIgnoreCase);

            var hasGraphQl = content.Contains("HotChocolate", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("GraphQL", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("GrapQL", StringComparison.OrdinalIgnoreCase);
            var hasGrpc = content.Contains("Grpc.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("Grpc.Tools", StringComparison.OrdinalIgnoreCase) ||
                          content.Contains("<Protobuf", StringComparison.OrdinalIgnoreCase);

            if (hasGraphQl) props["has_graphql"] = "true";
            if (hasGrpc) props["has_grpc"] = "true";

            try
            {
                var csFiles = WorkspaceFileFilter.EnumerateFiles(directoryPath, "*.cs");
                foreach (var file in csFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        props["has_controllers"] = "true";
                    }
                    else if (fileName.EndsWith("Query.cs", StringComparison.OrdinalIgnoreCase) ||
                             fileName.EndsWith("Queries.cs", StringComparison.OrdinalIgnoreCase) ||
                             fileName.EndsWith("Mutation.cs", StringComparison.OrdinalIgnoreCase) ||
                             fileName.EndsWith("Mutations.cs", StringComparison.OrdinalIgnoreCase) ||
                             fileName.EndsWith("Subscription.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        props["has_graphql"] = "true";
                    }
                    else if (fileName.EndsWith("GrpcService.cs", StringComparison.OrdinalIgnoreCase) ||
                             fileName.EndsWith("Grpc.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        props["has_grpc"] = "true";
                    }

                    if (fileName is "Startup.cs" or "Program.cs" or "ServiceStartup.cs" || fileName.EndsWith("Module.cs", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileText = File.ReadAllText(file);
                        if (fileText.Contains("AddGraphQLServer") || fileText.Contains("MapGraphQL") ||
                            fileText.Contains("AddQueryType") || fileText.Contains("AddMutationType"))
                        {
                            props["has_graphql"] = "true";
                        }
                        if (fileText.Contains("MapGrpcService") || fileText.Contains("AddGrpcService") || fileText.Contains("AddGrpcClient"))
                        {
                            props["has_grpc"] = "true";
                        }
                        if (fileText.Contains("MapControllers") || fileText.Contains("AddControllers") ||
                            fileText.Contains("[ApiController]") || fileText.Contains("ControllerBase"))
                        {
                            props["has_controllers"] = "true";
                        }
                    }
                }
            }
            catch { }

            if (isWeb && (props.ContainsKey("has_graphql") || props.ContainsKey("has_grpc") || props.ContainsKey("has_controllers")))
            {
                props["has_ingress_contract"] = "true";
            }
        }
        catch { }

        return props;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new CSharpFileVisitor(
            rootNode,
            activeExtensions,
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
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

            var propChildren = doc.Root?.Elements()
                .Where(e => e.Name.LocalName == "PropertyGroup")
                .SelectMany(g => g.Elements())
                .ToList();

            // Check IsPackable
            var isPackableStr = propChildren?.FirstOrDefault(e => e.Name.LocalName == "IsPackable")?.Value;
            if (!string.IsNullOrEmpty(isPackableStr) && bool.TryParse(isPackableStr, out var isPackable) && !isPackable)
            {
                return null;
            }

            // Check OutputType (if Exe and not packable, return null)
            var outputType = propChildren?.FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value;
            var hasGeneratePackageOnBuild = propChildren?.FirstOrDefault(e => e.Name.LocalName == "GeneratePackageOnBuild")?.Value;
            var generateOnBuild = !string.IsNullOrEmpty(hasGeneratePackageOnBuild) &&
                                  bool.TryParse(hasGeneratePackageOnBuild, out var gen) && gen;

            var explicitPackable = !string.IsNullOrEmpty(isPackableStr) &&
                                   bool.TryParse(isPackableStr, out var p) && p;

            if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) && !generateOnBuild && !explicitPackable)
            {
                return null;
            }

            var packageId = propChildren?.FirstOrDefault(e => e.Name.LocalName == "PackageId" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                         ?? propChildren?.FirstOrDefault(e => e.Name.LocalName == "AssemblyName" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                         ?? Path.GetFileNameWithoutExtension(csprojFile);

            var version = propChildren?.FirstOrDefault(e => e.Name.LocalName == "Version" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                       ?? propChildren?.FirstOrDefault(e => e.Name.LocalName == "PackageVersion" && !string.IsNullOrWhiteSpace(e.Value))?.Value
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

                    var resolvedInclude = include;
                    if (resolvedInclude.Contains("$(SolutionDir)", StringComparison.OrdinalIgnoreCase))
                    {
                        var slnDir = FindSolutionDir(Path.GetDirectoryName(csprojFile)!);
                        resolvedInclude = resolvedInclude.Replace("$(SolutionDir)", slnDir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                    }
                    if (resolvedInclude.Contains("$(MSBuildThisFileDirectory)", StringComparison.OrdinalIgnoreCase))
                    {
                        var thisDir = Path.GetDirectoryName(csprojFile)!;
                        resolvedInclude = resolvedInclude.Replace("$(MSBuildThisFileDirectory)", thisDir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                    }
                    if (resolvedInclude.Contains("$(ProjectDir)", StringComparison.OrdinalIgnoreCase))
                    {
                        var thisDir = Path.GetDirectoryName(csprojFile)!;
                        resolvedInclude = resolvedInclude.Replace("$(ProjectDir)", thisDir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                    }

                    var referencedCsprojPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojFile)!, resolvedInclude)).Replace('\\', '/');
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

                // Check Web SDK or FrameworkReference
                var sdkAttr = doc.Root?.Attribute("Sdk")?.Value ?? "";
                var hasWebSdk = sdkAttr.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
                                doc.Descendants("Import").Any(i => (i.Attribute("Sdk")?.Value ?? "").Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase));
                var hasAspNetCoreFrameworkRef = doc.Descendants("FrameworkReference")
                    .Any(fr => string.Equals(fr.Attribute("Include")?.Value, "Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase));

                if (hasWebSdk || hasAspNetCoreFrameworkRef)
                {
                    externalPackages.Add(new ProducedPackageInfo("Microsoft.AspNetCore.App", "implicit", "nuget"));
                }
            }
            catch
            {
                // Ignore
            }
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    private static string FindSolutionDir(string startDir)
    {
        var curr = new DirectoryInfo(startDir);
        while (curr != null)
        {
            if (curr.GetFiles("*.sln").Length > 0 || curr.GetFiles("*.slnx").Length > 0)
            {
                return curr.FullName;
            }
            curr = curr.Parent;
        }
        return startDir;
    }

    public bool UsesTreeSitter => true;
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

    private readonly ConcurrentDictionary<string, string> _csProjCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string?> _dirToCsprojCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveCsImportType(string importPath, string filePath)
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

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveCsImportType(importPath, filePath);
    }

    private string? FindCsprojFile(string? dir)
    {
        if (dir == null) return null;
        return _dirToCsprojCache.GetOrAdd(dir, d =>
        {
            var current = d;
            while (current != null && Directory.Exists(current))
            {
                var files = Directory.GetFiles(current, "*.csproj");
                if (files.Length > 0)
                    return files[0];
                current = Path.GetDirectoryName(current);
            }
            return null;
        });
    }

    public void CollectSemanticData(Node node, string filePath, List<RawImport> rawImports, List<RawVariable> rawVariables)
    {
        if (node.Is(TreeSitterSyntax.CSharp.UsingDirective))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name)
                ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.Common.Identifier));
            if (nameNode.IsValid())
            {
                var importPath = nameNode.Text;
                var type = ResolveCsImportType(importPath, filePath);
                rawImports.Add(new RawImport(importPath, filePath, type));
            }
        }
        else if (node.IsAny(TreeSitterSyntax.CSharp.VariableDeclarator, TreeSitterSyntax.CSharp.PropertyDeclaration))
        {
            var name = node.GetField(TreeSitterSyntax.Fields.Name)?.Text;
            if (string.IsNullOrEmpty(name))
            {
                name = node.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
                if (!valueNode.IsValid())
                {
                    var eqClause = node.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                    if (eqClause.IsValid() && eqClause.Children.Count > 1)
                    {
                        valueNode = eqClause.Children[1];
                    }
                }
                var initializerText = valueNode.IsValid() ? valueNode.Text : "";
                var isConstant = IsCSharpConstant(node);
                var scope = DetermineCSharpScope(node);

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

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.FieldDeclaration, TreeSitterSyntax.CSharp.LocalDeclarationStatement))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.CSharp.Const, TreeSitterSyntax.CSharp.Readonly) || child.Text is "const" or "readonly")
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
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration, TreeSitterSyntax.CSharp.InterfaceDeclaration))
                return "class";
            if (curr.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement, TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.ConstructorDeclaration))
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }
}
