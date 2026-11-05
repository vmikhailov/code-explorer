using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public partial class CSharpParser
{
    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var csprojFile = filesInDirectory.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csprojFile == null || !File.Exists(csprojFile)) return props;

        try
        {
            var content = PhysicalFileStore.ReadAllText(csprojFile);
            var projName = Path.GetFileNameWithoutExtension(csprojFile);
            var normalizedDir = directoryPath.Replace('\\', '/').Trim('/');

            // Stage 1 (Hard Physical Gates)
            // 1.1 Test Suite Priority: detect test projects via LibraryRole.TestFramework library parsers or MSBuild test flags
            var referencedPackages = ExtractReferencedPackagesAndSdks(csprojFile, content);
            var isTest = ((IProjectParser)this).HasTestFrameworkDependency(referencedPackages) ||
                         Regex.IsMatch(content, @"<IsTestProject>\s*true\s*</IsTestProject>", RegexOptions.IgnoreCase) ||
                         Regex.IsMatch(content, @"<IsTestingPlatformApplication>\s*true\s*</IsTestingPlatformApplication>", RegexOptions.IgnoreCase) ||
                         IsTestProjectName(projName);

            if (isTest)
            {
                props["manifest_type"] = "test";
                props["is_test_project"] = "true";
                props["is_library"] = "false";
                props["role"] = "Test";
                props["kind"] = "TestSuite";
                props["primary_role"] = "Test";
                props["secondary_roles"] = "[]";
                props["all_roles"] = JsonSerializer.Serialize(new[] { "Test" });
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
                props["all_roles"] = JsonSerializer.Serialize(new[] { "Library" });
                return props;
            }

            var outputTypeMatch = System.Text.RegularExpressions.Regex.Match(content, @"<OutputType(?:\s+[^>]*)?>\s*([A-Za-z0-9]+)\s*</OutputType>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var hasExeOutput = outputTypeMatch.Success &&
                               (outputTypeMatch.Groups[1].Value.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
                                outputTypeMatch.Groups[1].Value.Equals("WinExe", StringComparison.OrdinalIgnoreCase));
            var isExplicitLibraryOutput = outputTypeMatch.Success &&
                                          outputTypeMatch.Groups[1].Value.Equals("Library", StringComparison.OrdinalIgnoreCase);

            var isWebSdk = content.Contains("Sdk=\"Microsoft.NET.Sdk.Web\"", StringComparison.OrdinalIgnoreCase) ||
                           content.Contains("Sdk='Microsoft.NET.Sdk.Web'", StringComparison.OrdinalIgnoreCase);
            var isWorkerSdk = content.Contains("Sdk=\"Microsoft.NET.Sdk.Worker\"", StringComparison.OrdinalIgnoreCase) ||
                             content.Contains("Sdk='Microsoft.NET.Sdk.Worker'", StringComparison.OrdinalIgnoreCase);
            var isBlazorWasmSdk = content.Contains("Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\"", StringComparison.OrdinalIgnoreCase) ||
                                  content.Contains("Sdk='Microsoft.NET.Sdk.BlazorWebAssembly'", StringComparison.OrdinalIgnoreCase);

            var hasServiceProcessRef = content.Contains("Include=\"System.ServiceProcess\"", StringComparison.OrdinalIgnoreCase) ||
                                       content.Contains("Include='System.ServiceProcess'", StringComparison.OrdinalIgnoreCase);
            var hasServiceProcessHostingIndicator = content.Contains("System.Configuration.Install", StringComparison.OrdinalIgnoreCase) ||
                                                    content.Contains("<SubType>Component</SubType>", StringComparison.OrdinalIgnoreCase) ||
                                                    content.Contains("ProjectInstaller.cs", StringComparison.OrdinalIgnoreCase);

            var isWindowsService = System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseWindowsService>\s*true\s*</UseWindowsService>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                                   (hasServiceProcessRef && hasServiceProcessHostingIndicator) ||
                                   content.Contains("Microsoft.Extensions.Hosting.WindowsServices", StringComparison.OrdinalIgnoreCase) ||
                                   content.Contains("Include=\"Topshelf", StringComparison.OrdinalIgnoreCase);

            var hasLegacyDesktopUi = hasExeOutput &&
                                     (content.Contains("Include=\"PresentationFramework\"", StringComparison.OrdinalIgnoreCase) ||
                                      content.Contains("Include=\"System.Windows.Forms\"", StringComparison.OrdinalIgnoreCase) ||
                                      content.Contains("<ApplicationDefinition", StringComparison.OrdinalIgnoreCase) ||
                                      content.Contains("{60dc8134-eba5-43b8-bcc9-bb4bc16c2548}", StringComparison.OrdinalIgnoreCase));

            var isUiClientApp = !isExplicitLibraryOutput && !isWindowsService &&
                                (System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseMaui>\s*true\s*</UseMaui>", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                                 (hasExeOutput && System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseWPF>\s*true\s*</UseWPF>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ||
                                 (hasExeOutput && System.Text.RegularExpressions.Regex.IsMatch(content, @"<UseWindowsForms>\s*true\s*</UseWindowsForms>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ||
                                 hasLegacyDesktopUi ||
                                 isBlazorWasmSdk ||
                                 (hasExeOutput && content.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)) ||
                                 (hasExeOutput && content.Contains("Uno.Sdk", StringComparison.OrdinalIgnoreCase)));

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
                props["all_roles"] = JsonSerializer.Serialize(new[] { "Library" });
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
            var hasWorkerCapability = isWorkerSdk || isWindowsService;
            var hasCliCapability = hasCliBin;
            var hasServiceCapability = isWebSdk || isLegacyWeb;

            if (content.Contains("SuperSocket.WebSocket", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.Owin.Hosting", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("System.Web.Http.SelfHost", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("CoreWCF", StringComparison.OrdinalIgnoreCase) ||
                (isWindowsService && content.Contains("WCF.Server", StringComparison.OrdinalIgnoreCase)))
            {
                hasServiceCapability = true;
            }

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

            var hasProtobufServer = false;
            if (content.Contains("<Protobuf", StringComparison.OrdinalIgnoreCase))
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"<Protobuf\b([^>]*)/?>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var tag = m.Groups[1].Value;
                    var gsMatch = System.Text.RegularExpressions.Regex.Match(tag, @"GrpcServices\s*=\s*[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (gsMatch.Success)
                    {
                        var mode = gsMatch.Groups[1].Value;
                        if (mode.Equals("Server", StringComparison.OrdinalIgnoreCase) || mode.Equals("Both", StringComparison.OrdinalIgnoreCase))
                        {
                            hasProtobufServer = true;
                            break;
                        }
                    }
                    else if (content.Contains("Grpc.AspNetCore", StringComparison.OrdinalIgnoreCase))
                    {
                        hasProtobufServer = true;
                        break;
                    }
                }
            }

            if (content.Contains("Grpc.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Yarp.ReverseProxy", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Ocelot", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("HotChocolate", StringComparison.OrdinalIgnoreCase) ||
                hasProtobufServer)
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
                var allFiles = WorkspaceFileFilter.Default.EnumerateFiles(directoryPath, "*").ToList();
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
                        else if (fileName.EndsWith("GrpcService.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            props["has_grpc"] = "true";
                            hasServiceCapability = true;
                        }
                        else if (fileName.EndsWith("Grpc.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            props["has_grpc"] = "true";
                        }

                        if (fileName is "Startup.cs" or "Program.cs" or "ServiceStartup.cs" ||
                            fileName.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Module.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Worker.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Consumer.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("Handler.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            var fileText = PhysicalFileStore.ReadAllText(file);
                            if (fileText.Contains("AddRazorComponents") || fileText.Contains("MapRazorComponents"))
                            {
                                hasAppCapability = true;
                            }
                            if (fileText.Contains("BackgroundService") || fileText.Contains("IHostedService") ||
                                fileText.Contains("IIntegrationEventHandler") || fileText.Contains("IConsumer<") ||
                                fileText.Contains("IJob") || fileText.Contains(": ServiceBase"))
                            {
                                hasWorkerCapability = true;
                            }
                            if (fileText.Contains("new ServiceHost(") || fileText.Contains("ServiceHost(") ||
                                fileText.Contains("WebSocketServer") || fileText.Contains("WebApp.Start"))
                            {
                                hasServiceCapability = true;
                            }
                            if (fileText.Contains("AddGraphQLServer") || fileText.Contains("MapGraphQL") ||
                                fileText.Contains("AddQueryType") || fileText.Contains("AddMutationType"))
                            {
                                props["has_graphql"] = "true";
                                hasServiceCapability = true;
                            }
                            if (fileText.Contains("MapGrpcService") || fileText.Contains("AddGrpcService"))
                            {
                                props["has_grpc"] = "true";
                                hasServiceCapability = true;
                            }
                            else if (fileText.Contains("AddGrpcClient"))
                            {
                                props["has_grpc"] = "true";
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
            props["secondary_roles"] = JsonSerializer.Serialize(secondaryRoles);
            props["all_roles"] = JsonSerializer.Serialize(new[] { primaryRole }.Concat(secondaryRoles).Distinct());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return props;
    }

    private static HashSet<string> ExtractReferencedPackagesAndSdks(string csprojFile, string content)
    {
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectPackagesFromXmlText(content, packages);

        var dir = Path.GetDirectoryName(csprojFile);
        for (int depth = 0; depth < 4 && !string.IsNullOrEmpty(dir); depth++)
        {
            var buildProps = Path.Combine(dir, "Directory.Build.props");
            if (File.Exists(buildProps))
            {
                try
                {
                    CollectPackagesFromXmlText(PhysicalFileStore.ReadAllText(buildProps), packages);
                }
                catch
                {
                    // Ignore unreadable parent build props
                }
            }
            dir = Path.GetDirectoryName(dir);
        }

        return packages;
    }

    private static void CollectPackagesFromXmlText(string xmlText, HashSet<string> packages)
    {
        foreach (Match m in Regex.Matches(xmlText, @"<(?:PackageReference|Reference)\b[^>]*?\b(?:Include|Update)\s*=\s*[""']([^""',;\s]+)", RegexOptions.IgnoreCase))
        {
            if (m.Success) packages.Add(m.Groups[1].Value);
        }

        foreach (Match m in Regex.Matches(xmlText, @"<(?:Project|Sdk)\b[^>]*?\b(?:Sdk|Name)\s*=\s*[""']([^""'/;\s]+)", RegexOptions.IgnoreCase))
        {
            if (m.Success) packages.Add(m.Groups[1].Value);
        }
    }

    private static bool IsTestProjectName(string projName) =>
        projName.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith(".FunctionalTests", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith(".Benchmarks", StringComparison.OrdinalIgnoreCase) ||
        projName.Contains(".Benchmarks.", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith("Benchmarks", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
        projName.EndsWith("Test", StringComparison.OrdinalIgnoreCase);

}
