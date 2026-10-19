using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectEntityClassifierTests
{
    [Test]
    public void Classify_IdentifiesLibraries()
    {
        // 1. Manifest type
        var kind1 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/pkg/core",
            ["/workspace/pkg/core/index.ts"],
            "pkg/core",
            "core",
            "typescript",
            extensions: new Dictionary<string, string> { ["manifest_type"] = "library" }
        );
        kind1?.Kind.Should().Be(ProjectEntityKind.Library);

        // 2. Standard source library path
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/src/lib/widgets",
            ["/workspace/src/lib/widgets/button.ts"],
            "src/lib/widgets",
            "widgets",
            "typescript"
        );
        kind2?.Kind.Should().Be(ProjectEntityKind.Library);

        // 3. Domain contracts naming
        var kind3 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/src/Orders.Contracts",
            ["/workspace/src/Orders.Contracts/IOrderService.cs"],
            "src/Orders.Contracts",
            "Orders.Contracts",
            "csharp"
        );
        kind3?.Kind.Should().Be(ProjectEntityKind.Library);
    }

    [Test]
    public void Classify_IdentifiesServices()
    {
        // 1. Web SDK
        var kind1 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/services/OrderingApi",
            ["/workspace/services/OrderingApi/Program.cs"],
            "services/OrderingApi",
            "OrderingApi",
            "csharp",
            extensions: new Dictionary<string, string> { ["sdk"] = "Microsoft.NET.Sdk.Web" }
        );
        kind1?.Kind.Should().Be(ProjectEntityKind.Service);

        // 2. Executable backend service
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/services/order-service",
            ["/workspace/services/order-service/server.ts"],
            "services/order-service",
            "order-service",
            "typescript",
            dependencies: ["express", "pg"]
        );
        kind2?.Kind.Should().Be(ProjectEntityKind.Service);
    }

    [Test]
    public void Classify_IdentifiesWorkers()
    {
        // 1. Worker SDK
        var kind1 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/services/EmailWorker",
            ["/workspace/services/EmailWorker/Program.cs"],
            "services/EmailWorker",
            "EmailWorker",
            "csharp",
            extensions: new Dictionary<string, string> { ["sdk"] = "Microsoft.NET.Sdk.Worker" }
        );
        kind1?.Kind.Should().Be(ProjectEntityKind.Worker);

        // 2. Cloudflare worker runtime
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/workers/cdn-worker",
            ["/workspace/workers/cdn-worker/index.ts"],
            "workers/cdn-worker",
            "cdn-worker",
            "typescript",
            extensions: new Dictionary<string, string> { ["cloud_runtime"] = "cloudflare-worker" }
        );
        kind2?.Kind.Should().Be(ProjectEntityKind.Worker);
    }

    [Test]
    public void Classify_IdentifiesCliTools()
    {
        // 1. Manifest CLI bin
        var kind1 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/tools/runner",
            ["/workspace/tools/runner/index.ts"],
            "tools/runner",
            "runner",
            "typescript",
            extensions: new Dictionary<string, string> { ["has_cli_bin"] = "true" }
        );
        kind1?.Kind.Should().Be(ProjectEntityKind.CliTool);
        kind1?.SubKind.Should().Be(ProjectEntitySubKind.Cli);

        // 2. CLI directory & suffix
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/cli/my-tool",
            ["/workspace/cli/my-tool/Program.cs"],
            "cli/my-tool",
            "my-tool-cli",
            "csharp"
        );
        kind2?.Kind.Should().Be(ProjectEntityKind.CliTool);
        kind2?.SubKind.Should().Be(ProjectEntitySubKind.Cli);
    }

    [Test]
    public void Classify_IdentifiesFrontendApps()
    {
        var kind = ProjectEntityClassifierRegistry.Classify(
            "/workspace/apps/portal",
            ["/workspace/apps/portal/vite.config.ts", "/workspace/apps/portal/index.html"],
            "apps/portal",
            "portal",
            "typescript",
            dependencies: ["react", "react-dom"]
        );
        kind?.Kind.Should().Be(ProjectEntityKind.App);
        kind?.SubKind.Should().Be(ProjectEntitySubKind.Web);
    }

    [Test]
    public void Classify_IdentifiesMigrationTools()
    {
        var kind = ProjectEntityClassifierRegistry.Classify(
            "/workspace/db/migrations",
            ["/workspace/db/migrations/V1__init.sql"],
            "db/migrations",
            "migrations",
            "sql"
        );
        kind?.Kind.Should().Be(ProjectEntityKind.DatabaseMigration);
    }

    [Test]
    public void Detect_ReturnsComprehensiveEntityKind()
    {
        var (role, isLib, classification) = ProjectRoleDetector.Detect(
            "/workspace/apps/web",
            ["/workspace/apps/web/next.config.js"],
            "apps/web",
            "web",
            "typescript"
        );
        role.Should().Be(ProjectRole.FrontendApp);
        isLib.Should().BeFalse();
        classification.Kind.Should().Be(ProjectEntityKind.App);
        classification.SubKind.Should().Be(ProjectEntitySubKind.Web);
    }

    [Test]
    public void Detect_ServiceWithBackgroundSchedule_SelectsPrimaryWebServiceAndSecondaryScheduler()
    {
        var extensions = new Dictionary<string, string>();
        var (role, isLib, classification) = ProjectRoleDetector.Detect(
            "/workspace/services/bundle-priority",
            ["/workspace/services/bundle-priority/package.json"],
            "services/bundle-priority",
            "integration-service-bundle-priority",
            "typescript",
            dependencies: ["@nestjs/common", "@nestjs/core", "@nestjs/schedule"],
            extensions: extensions
        );

        role.Should().Be(ProjectRole.Service);
        isLib.Should().BeFalse();
        classification.Kind.Should().Be(ProjectEntityKind.Service);
        extensions["primary_role"].Should().Be("WebService");
        extensions["secondary_roles"].Should().Contain("Scheduler");
        extensions["all_roles"].Should().Contain("WebService").And.Contain("Scheduler");
    }

    [Test]
    public void Detect_DedicatedScheduler_SelectsPrimaryScheduler()
    {
        var extensions = new Dictionary<string, string>();
        var (role, isLib, classification) = ProjectRoleDetector.Detect(
            "/workspace/services/action-scheduler",
            ["/workspace/services/action-scheduler/package.json"],
            "services/action-scheduler",
            "internal-service-action-scheduler",
            "typescript",
            dependencies: ["@nestjs/common", "@nestjs/core", "@nestjs/schedule"],
            extensions: extensions
        );

        role.Should().Be(ProjectRole.Worker);
        isLib.Should().BeFalse();
        classification.Kind.Should().Be(ProjectEntityKind.Worker);
        extensions["primary_role"].Should().Be("Scheduler");
    }

    [Test]
    public void ProjectNodeFactory_PopulatesPrimaryAndSecondaryRolesOnNode()
    {
        var node = ProjectNodeFactory.Create(
            "ws:p:bundle-priority:",
            "integration-service-bundle-priority",
            "services/bundle-priority",
            "typescript",
            "/workspace/services/bundle-priority",
            ["/workspace/services/bundle-priority/package.json"],
            externalPackages: ["@nestjs/common", "@nestjs/core", "@nestjs/schedule"]
        );

        node.Role.Should().Be("Service");
        node.IsLibrary.Should().BeFalse();
        node.PrimaryRole.Should().Be("Service");
        node.SecondaryRoles.Should().Contain("Worker");
        node.AllRoles.Should().Contain("Service").And.Contain("Worker");
    }

    [Test]
    public void Classify_IdentifiesVsCodeExtensionAsFrontendAppInIngress()
    {
        var extensions = new Dictionary<string, string>
        {
            ["is_vscode_extension"] = "true",
            ["framework_type"] = "extension",
            ["ui_type"] = "extension",
            ["manifest_type"] = "application"
        };

        var classification = ProjectEntityClassifierRegistry.Classify(
            "/workspace/vscode-extension",
            ["/workspace/vscode-extension/package.json"],
            "vscode-extension",
            "code-explorer-vscode",
            "typescript",
            dependencies: ["react", "react-dom", "@types/vscode"],
            extensions: extensions
        );

        classification.Kind.Should().Be(ProjectEntityKind.App);
        classification.SubKind.Should().Be(ProjectEntitySubKind.Desktop);

        var (role, isLib, _) = ProjectRoleDetector.Detect(
            "/workspace/vscode-extension",
            ["/workspace/vscode-extension/package.json"],
            "vscode-extension",
            "code-explorer-vscode",
            "typescript",
            dependencies: ["react", "react-dom", "@types/vscode"],
            extensions: extensions
        );

        role.Should().Be(ProjectRole.FrontendApp);
        isLib.Should().BeFalse();

        var layer = ProjectLayerClassifier.Classify(
            [
                new ProjectClassifierItem
                {
                    Id = "ws:p:vscode-extension:",
                    Name = "code-explorer-vscode",
                    FilePath = "vscode-extension",
                    Role = role.ToString(),
                    IsLibrary = isLib,
                    Extensions = extensions
                }
            ],
            []
        );

        layer["ws:p:vscode-extension:"].LayerId.Should().Be(StandardLayers.Ingress.LayerId);
    }

    [Test]
    public void Classify_IdentifiesToolsDirectoryWithMixedCaseAsCliTool()
    {
        var classification = ProjectEntityClassifierRegistry.Classify(
            "/workspace/cli/src/Tools/CodeExplorer.OntologyGen",
            ["/workspace/cli/src/Tools/CodeExplorer.OntologyGen/Program.cs"],
            "cli/src/Tools/CodeExplorer.OntologyGen",
            "CodeExplorer.OntologyGen",
            "csharp"
        );

        classification.Kind.Should().Be(ProjectEntityKind.CliTool);
        classification.SubKind.Should().Be(ProjectEntitySubKind.Cli);
    }

    [Test]
    public void ScriptProjectParser_DoesNotTreatTsOrJsAsScripts()
    {
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.ts").Should().BeFalse();
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.js").Should().BeFalse();
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.mjs").Should().BeFalse();
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.cs").Should().BeFalse();
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.ps1").Should().BeTrue();
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.sh").Should().BeTrue();
        CodeExplorer.Core.Parser.ScriptProjectParser.IsScriptFile("file.py").Should().BeTrue();
    }
}
