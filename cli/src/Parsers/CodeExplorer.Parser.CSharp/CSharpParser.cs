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

    public IReadOnlyList<PackageDescriptor> Packages { get; }

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.CouchbaseLibraryParser(),
        new Libraries.DapperLibraryParser(),
        new Libraries.EfCoreLibraryParser(),
        new Libraries.ElasticsearchNetLibraryParser(),
        new Libraries.FlurlLibraryParser(),
        new Libraries.HttpClientLibraryParser(),
        new Libraries.MicrosoftDataSqlClientLibraryParser(),
        new Libraries.SystemDataSqlClientLibraryParser(),
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
        new Libraries.RefitLibraryParser(),
        new Libraries.StripeLibraryParser()
    ];

    public LanguageSyntaxProfile SyntaxProfile => CSharpSyntaxProfile.Instance;

    public CSharpParser()
    {
        Packages = [.. SemanticExtensions.SelectMany(e => e.GetPackageDescriptors())];

        /*
        // Unimplemented / Backlog packages (commented out until AST semantic extensions are implemented):
        // Web Frameworks
        new PackageDescriptor("aspnet-mvc", "ASP.NET MVC", LibraryRole.WebFramework, "library", ["Microsoft.AspNet.Mvc", "Microsoft.AspNet.WebApi", "Microsoft.AspNet.WebApi.Core", "Microsoft.AspNet.WebApi.WebHost"]),

        // API Clients
        new PackageDescriptor("webapiclient", "WebApiClient", LibraryRole.General, "library", ["WebApiClient"]),
        new PackageDescriptor("apizr", "Apizr", LibraryRole.General, "library", ["Apizr"]),
        new PackageDescriptor("notoriousclient", "NotoriousClient", LibraryRole.General, "library", ["NotoriousClient"]),

        // Generic Cloud Services
        new PackageDescriptor("aws", "AWS", LibraryRole.CloudSdk, "library", ["Amazon.S3", "AWSSDK"]),
        new PackageDescriptor("gcp", "GCP", LibraryRole.CloudSdk, "library", ["Google.Cloud."]),
        new PackageDescriptor("azure", "Azure", LibraryRole.CloudSdk, "library", ["Azure."]),

        // Test Frameworks
        new PackageDescriptor("nunit", "NUnit", LibraryRole.TestFramework, "library", ["nunit", "nunit3testadapter"]),
        new PackageDescriptor("xunit", "xUnit", LibraryRole.TestFramework, "library", ["xunit", "xunit.runner.*", "xunit.v3.*"]),
        new PackageDescriptor("mstest", "MSTest", LibraryRole.TestFramework, "library", ["mstest", "mstest.testframework", "mstest.testadapter"]),
        new PackageDescriptor("benchmarkdotnet", "BenchmarkDotNet", LibraryRole.TestFramework, "library", ["benchmarkdotnet"]),
        new PackageDescriptor("nettestsdk", "Microsoft.NET.Test.Sdk", LibraryRole.TestFramework, "library", ["microsoft.net.test.sdk"])
        */
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
            var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type");
            if (manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true") return ProjectClassification.Test;
            if (context.ManifestProperties.GetValueOrDefault("is_orchestrator") == "true" ||
                context.ManifestProperties.GetValueOrDefault("role") == "Library" ||
                manifestType == "library") return ProjectClassification.Library;

            var primaryRole = context.ManifestProperties.GetValueOrDefault("primary_role");
            var role = context.ManifestProperties.GetValueOrDefault("role");
            if (primaryRole == "App" || role == "FrontendApp" || role == "App") return ProjectClassification.WebApp;
            if (primaryRole == "Worker" || role == "Worker") return ProjectClassification.Worker;
            if (primaryRole == "CliTool" || role == "CliTool") return ProjectClassification.CliTool;
            if (primaryRole == "Service" || role == "Service") return ProjectClassification.Service;

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
            var projName = Path.GetFileNameWithoutExtension(csprojFile);
            var normalizedDir = directoryPath.Replace('\\', '/').Trim('/');

            // Stage 1 (Hard Physical Gates)
            // 1.1 Test Suite Priority: detect test projects FIRST before Maui, Exe, or Web SDK checks
            var isTest = System.Text.RegularExpressions.Regex.IsMatch(content, @"<IsTestProject>\s*true\s*</IsTestProject>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                         System.Text.RegularExpressions.Regex.IsMatch(content, @"<IsTestingPlatformApplication>\s*true\s*</IsTestingPlatformApplication>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                         content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("MSTest.Sdk", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("MSTest.TestFramework", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("MSTest", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"xunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include='xunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"NUnit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include='NUnit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"nunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include='nunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"TUnit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include='TUnit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"Verify.Xunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include='Verify.Xunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"Verify.NUnit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include='Verify.NUnit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"bunit", StringComparison.OrdinalIgnoreCase) ||
                         content.Contains("Include=\"FluentAssertions", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith(".FunctionalTests", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                         projName.EndsWith("Test", StringComparison.OrdinalIgnoreCase);

            if (isTest)
            {
                props["manifest_type"] = "test";
                props["is_test_project"] = "true";
                props["is_library"] = "false";
                props["role"] = "Test";
                props["kind"] = "TestSuite";
                props["primary_role"] = "Test";
                props["secondary_roles"] = "[]";
                props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Test" });
                return props;
            }

            // 1.2 Aspire AppHost (.AppHost / Aspire.AppHost.Sdk)
            var isAspireAppHost = content.Contains("Aspire.AppHost.Sdk", StringComparison.OrdinalIgnoreCase) ||
                                  projName.EndsWith(".AppHost", StringComparison.OrdinalIgnoreCase);
            if (isAspireAppHost)
            {
                props["sdk"] = "Aspire.AppHost.Sdk";
                props["is_library"] = "true";
                props["is_orchestrator"] = "true";
                props["manifest_type"] = "library";
                props["role"] = "Library";
                props["kind"] = "Library";
                props["primary_role"] = "Library";
                props["secondary_roles"] = "[]";
                props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Library" });
                return props;
            }

            var outputTypeMatch = System.Text.RegularExpressions.Regex.Match(content, @"<OutputType(?:\s+[^>]*)?>\s*([A-Za-z0-9]+)\s*</OutputType>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var hasExeOutput = outputTypeMatch.Success &&
                               (outputTypeMatch.Groups[1].Value.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
                                outputTypeMatch.Groups[1].Value.Equals("WinExe", StringComparison.OrdinalIgnoreCase));

            var isWebSdk = content.Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.OrdinalIgnoreCase) ||
                           content.Contains("Sdk='Microsoft.NET.Sdk.Web'", StringComparison.OrdinalIgnoreCase);
            var isWorkerSdk = content.Contains("Sdk=\"Microsoft.NET.Sdk.Worker\"", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("Sdk='Microsoft.NET.Sdk.Worker'", StringComparison.OrdinalIgnoreCase);
            var isBlazorWasmSdk = content.Contains("Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"", StringComparison.OrdinalIgnoreCase) ||
                                  content.Contains("Sdk='Microsoft.NET.Sdk.BlazorWebAssembly'", StringComparison.OrdinalIgnoreCase);

            var isUiClientApp = System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseMaui>\s*true\s*</UseMaui>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                                System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseWPF>\s*true\s*</UseWPF>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                                System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseWindowsForms>\s*true\s*</UseWindowsForms>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                                isBlazorWasmSdk ||
                                content.Contains("Avalonia", StringComparison.OrdinalIgnoreCase) ||
                                content.Contains("Uno.Sdk", StringComparison.OrdinalIgnoreCase);

            var hasWebConfig = filesInDirectory.Any(f => Path.GetFileName(f).Equals("web.config", StringComparison.OrdinalIgnoreCase));
            var hasWebGuid = content.Contains("{349c5851-65df-11da-9384-00065b846f21}", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("{E3E379DF-F4C6-4180-9B81-6769533ABE47}", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("{E53F8FEA-EAE0-44A6-8774-FFD645390401}", StringComparison.OrdinalIgnoreCase);
            var isLegacyWeb = hasWebConfig || hasWebGuid;

            // 1.3 Physical Non-Runnable Gate: If not Web SDK and no Exe/WinExe and not UI Client App and not legacy web -> Library
            if (!isWebSdk && !isLegacyWeb && !isWorkerSdk && !isBlazorWasmSdk && !hasExeOutput && !isUiClientApp)
            {
                props["manifest_type"] = "library";
                props["is_library"] = "true";
                props["role"] = "Library";
                props["kind"] = "Library";
                props["primary_role"] = "Library";
                props["secondary_roles"] = "[]";
                props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Library" });
                return props;
            }

            if (isWebSdk || isLegacyWeb)
            {
                if (isWebSdk) props["sdk"] = "Microsoft.NET.Sdk.Web";
                props["framework_type"] = "web";
            }
            else if (isWorkerSdk)
            {
                props["sdk"] = "Microsoft.NET.Sdk.Worker";
                props["framework_type"] = "worker";
            }
            else if (isBlazorWasmSdk)
            {
                props["sdk"] = "Microsoft.NET.Sdk.BlazorWebAssembly";
                props["framework_type"] = "frontend";
            }
            else if (content.Contains("Sdk=\"Microsoft.NET.Sdk\"", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("Sdk='Microsoft.NET.Sdk'", StringComparison.OrdinalIgnoreCase))
            {
                props["sdk"] = "Microsoft.NET.Sdk";
            }

            if (outputTypeMatch.Success)
            {
                props["output_type"] = outputTypeMatch.Groups[1].Value;
            }

            // Inspect <IsPackable>
            var isPackableMatch = System.Text.RegularExpressions.Regex.Match(content, @"<IsPackable>\s*(true|false)\s*</IsPackable>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (isPackableMatch.Success)
            {
                props["is_packable"] = isPackableMatch.Groups[1].Value.ToLowerInvariant();
            }

            // Inspect CLI tool metadata
            var hasCliBin = content.Contains("<PackAsTool>true</PackAsTool>", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("<ToolCommandName>", StringComparison.OrdinalIgnoreCase);
            if (hasCliBin) props["has_cli_bin"] = "true";

            // Stage 2 (Runnable Workload Capabilities — App, CliTool, Service, Worker)
            var hasAppCapability = isUiClientApp;
            var hasWorkerCapability = isWorkerSdk;
            var hasCliCapability = hasCliBin;
            var hasServiceCapability = isWebSdk || isLegacyWeb;

            var isExplicitBackendApi = projName.EndsWith(".API", StringComparison.OrdinalIgnoreCase) ||
                                       projName.EndsWith(".Api", StringComparison.OrdinalIgnoreCase) ||
                                       projName.Contains(".Bff", StringComparison.OrdinalIgnoreCase) ||
                                       projName.Contains("Gateway", StringComparison.OrdinalIgnoreCase);

            if (content.Contains("Microsoft.AspNetCore.Components.Web", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.AspNetCore.Components.QuickGrid", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("QuickGrid", StringComparison.OrdinalIgnoreCase))
            {
                hasAppCapability = true;
            }

            if (content.Contains("Grpc.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Yarp.ReverseProxy", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Ocelot", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("HotChocolate", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("<Protobuf", StringComparison.OrdinalIgnoreCase))
            {
                hasServiceCapability = true;
                if (content.Contains("HotChocolate", StringComparison.OrdinalIgnoreCase)) props["has_graphql"] = "true";
                if (content.Contains("Grpc", StringComparison.OrdinalIgnoreCase) || content.Contains("<Protobuf", StringComparison.OrdinalIgnoreCase)) props["has_grpc"] = "true";
            }

            if (content.Contains("Hangfire", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Quartz", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("MassTransit", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("EventBusRabbitMQ", StringComparison.OrdinalIgnoreCase))
            {
                hasWorkerCapability = true;
            }

            if (content.Contains("System.CommandLine", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Spectre.Console", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Cocona", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("CommandLineParser", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("McMaster.Extensions.CommandLineUtils", StringComparison.OrdinalIgnoreCase))
            {
                hasCliCapability = true;
            }

            // Inspect directory files for concrete capabilities
            try
            {
                var allFiles = WorkspaceFileFilter.EnumerateFiles(directoryPath, "*").ToList();
                foreach (var file in allFiles)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".razor")
                    {
                        hasAppCapability = true;
                    }
                    else if (ext == ".cshtml")
                    {
                        hasAppCapability = true;
                    }
                    else if (ext == ".cs")
                    {
                        var fileName = Path.GetFileName(file);
                        if (fileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            props["has_controllers"] = "true";
                            hasServiceCapability = true;
                        }
                        else if (fileName.EndsWith("Query.cs", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("Queries.cs", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("Mutation.cs", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("Mutations.cs", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("Subscription.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            props["has_graphql"] = "true";
                            hasServiceCapability = true;
                        }
                        else if (fileName.EndsWith("GrpcService.cs", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith("Grpc.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            props["has_grpc"] = "true";
                            hasServiceCapability = true;
                        }

                        if (fileName is "Startup.cs" or "Program.cs" or "ServiceStartup.cs" ||
                            fileName.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Module.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Worker.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Consumer.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Handler.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            var fileText = File.ReadAllText(file);
                            if (fileText.Contains("AddRazorComponents") || fileText.Contains("MapRazorComponents"))
                            {
                                hasAppCapability = true;
                            }
                            if (fileText.Contains("BackgroundService") || fileText.Contains("IHostedService") ||
                                fileText.Contains("IIntegrationEventHandler") || fileText.Contains("IConsumer<") ||
                                fileText.Contains("IJob"))
                            {
                                hasWorkerCapability = true;
                            }
                            if (fileText.Contains("AddGraphQLServer") || fileText.Contains("MapGraphQL") ||
                                fileText.Contains("AddQueryType") || fileText.Contains("AddMutationType"))
                            {
                                props["has_graphql"] = "true";
                                hasServiceCapability = true;
                            }
                            if (fileText.Contains("MapGrpcService") || fileText.Contains("AddGrpcService") || fileText.Contains("AddGrpcClient"))
                            {
                                props["has_grpc"] = "true";
                                hasServiceCapability = true;
                            }
                            if (fileText.Contains("MapControllers") || fileText.Contains("AddControllers") ||
                                fileText.Contains("[ApiController]") || fileText.Contains("ControllerBase") ||
                                fileText.Contains("MapGet(") || fileText.Contains("MapPost("))
                            {
                                props["has_controllers"] = "true";
                                hasServiceCapability = true;
                            }
                            if (fileText.Contains("System.CommandLine") || fileText.Contains("Spectre.Console") ||
                                fileText.Contains("CommandLineApplication"))
                            {
                                hasCliCapability = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            // Apply Strict Dominance Order
            string primaryRole;
            string role;
            string kind;
            string manifestType;

            var isExplicitCliTool = hasCliBin ||
                                    content.Contains("System.CommandLine", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("Spectre.Console", StringComparison.OrdinalIgnoreCase) ||
                                    content.Contains("Cocona", StringComparison.OrdinalIgnoreCase);

            if (hasCliCapability && isExplicitCliTool)
            {
                primaryRole = "CliTool";
                role = "CliTool";
                kind = "CliTool";
                manifestType = "cli";
            }
            else if (hasAppCapability && !isExplicitBackendApi)
            {
                primaryRole = "App";
                role = "FrontendApp";
                kind = "App";
                manifestType = "application";
                props["framework_type"] = "frontend";
            }
            else if (hasServiceCapability)
            {
                primaryRole = "Service";
                role = "Service";
                kind = "Service";
                manifestType = "application";
                props["framework_type"] = "web";
            }
            else if (hasWorkerCapability)
            {
                primaryRole = "Worker";
                role = "Worker";
                kind = "Worker";
                manifestType = "worker";
                props["framework_type"] = "worker";
            }
            else
            {
                primaryRole = "CliTool";
                role = "CliTool";
                kind = "CliTool";
                manifestType = "cli";
            }

            var detectedRoles = new List<string>();
            if (hasAppCapability) detectedRoles.Add("App");
            if (hasServiceCapability) detectedRoles.Add("Service");
            if (hasWorkerCapability) detectedRoles.Add("Worker");
            if (hasCliCapability) detectedRoles.Add("CliTool");

            var secondaryRoles = detectedRoles
                .Where(r => !string.Equals(r, primaryRole, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            props["primary_role"] = primaryRole;
            props["role"] = role;
            props["kind"] = kind;
            props["manifest_type"] = manifestType;
            props["is_library"] = "false";
            props["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secondaryRoles);
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { primaryRole }.Concat(secondaryRoles).Distinct());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        // Check legacy NuGet packages.config
        var packagesConfigFile = Path.Combine(projectDirectory, "packages.config");
        if (File.Exists(packagesConfigFile))
        {
            try
            {
                var pkgContent = await File.ReadAllTextAsync(packagesConfigFile);
                var pkgDoc = System.Xml.Linq.XDocument.Parse(pkgContent);
                foreach (var pkg in pkgDoc.Descendants("package"))
                {
                    var id = pkg.Attribute("id")?.Value;
                    var ver = pkg.Attribute("version")?.Value ?? "unknown";
                    if (!string.IsNullOrEmpty(id))
                    {
                        externalPackages.Add(new ProducedPackageInfo(id, ver, "nuget"));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // Ignore malformed packages.config
            }
        }

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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // Ignore malformed csproj or file access issues
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

                string? memberKind = null;
                string? typeName = null;

                if (node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
                {
                    memberKind = "property";
                    var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                    if (typeNode.IsValid())
                    {
                        typeName = typeNode.Text;
                    }
                }
                else if (node.Is(TreeSitterSyntax.CSharp.VariableDeclarator))
                {
                    var parent = node.Parent;
                    if (parent.IsValid() && parent.Is(TreeSitterSyntax.CSharp.VariableDeclaration))
                    {
                        var typeNode = parent.GetField(TreeSitterSyntax.Fields.Type);
                        if (typeNode.IsValid())
                        {
                            typeName = typeNode.Text;
                        }
                        if (parent.Parent.IsValid() && parent.Parent.Is(TreeSitterSyntax.CSharp.FieldDeclaration))
                        {
                            memberKind = "field";
                        }
                        else
                        {
                            memberKind = "variable";
                        }
                    }
                }

                rawVariables.Add(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    filePath,
                    node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1,
                    node.StartPosition.Column + 1,
                    node.EndPosition.Column + 1,
                    typeName,
                    memberKind
                ));
            }
        }
        else if (node.Is(TreeSitterSyntax.CSharp.Parameter))
        {
            var parent = node.Parent;
            if (parent.IsValid() && parent.Is(TreeSitterSyntax.CSharp.ParameterList))
            {
                var recordNode = parent.Parent;
                if (recordNode.IsValid() && recordNode.IsAny(TreeSitterSyntax.CSharp.RecordDeclaration, TreeSitterSyntax.CSharp.RecordStructDeclaration))
                {
                    var name = node.GetField(TreeSitterSyntax.Fields.Name)?.Text
                        ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;
                    if (!string.IsNullOrEmpty(name))
                    {
                        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                        var typeName = typeNode.IsValid() ? typeNode.Text : null;
                        var scope = DetermineCSharpScope(recordNode);
                        rawVariables.Add(new RawVariable(
                            name,
                            "",
                            scope,
                            false,
                            filePath,
                            node.StartPosition.Row + 1,
                            node.EndPosition.Row + 1,
                            node.StartPosition.Column + 1,
                            node.EndPosition.Column + 1,
                            typeName,
                            "property"
                        ));
                    }
                }
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
