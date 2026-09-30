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
        Assert.That(kind1?.Kind, Is.EqualTo(ProjectEntityKind.Library));

        // 2. Standard source library path
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/src/lib/widgets",
            ["/workspace/src/lib/widgets/button.ts"],
            "src/lib/widgets",
            "widgets",
            "typescript"
        );
        Assert.That(kind2?.Kind, Is.EqualTo(ProjectEntityKind.Library));

        // 3. Domain contracts naming
        var kind3 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/src/Orders.Contracts",
            ["/workspace/src/Orders.Contracts/IOrderService.cs"],
            "src/Orders.Contracts",
            "Orders.Contracts",
            "csharp"
        );
        Assert.That(kind3?.Kind, Is.EqualTo(ProjectEntityKind.Library));
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
        Assert.That(kind1?.Kind, Is.EqualTo(ProjectEntityKind.Service));

        // 2. Executable backend service
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/services/order-service",
            ["/workspace/services/order-service/server.ts"],
            "services/order-service",
            "order-service",
            "typescript",
            dependencies: ["express", "pg"]
        );
        Assert.That(kind2?.Kind, Is.EqualTo(ProjectEntityKind.Service));
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
        Assert.That(kind1?.Kind, Is.EqualTo(ProjectEntityKind.Worker));

        // 2. Cloudflare worker runtime
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/workers/cdn-worker",
            ["/workspace/workers/cdn-worker/index.ts"],
            "workers/cdn-worker",
            "cdn-worker",
            "typescript",
            extensions: new Dictionary<string, string> { ["cloud_runtime"] = "cloudflare-worker" }
        );
        Assert.That(kind2?.Kind, Is.EqualTo(ProjectEntityKind.Worker));
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
        Assert.That(kind1?.Kind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(kind1?.SubKind, Is.EqualTo(ProjectEntitySubKind.Cli));

        // 2. CLI directory & suffix
        var kind2 = ProjectEntityClassifierRegistry.Classify(
            "/workspace/cli/my-tool",
            ["/workspace/cli/my-tool/Program.cs"],
            "cli/my-tool",
            "my-tool-cli",
            "csharp"
        );
        Assert.That(kind2?.Kind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(kind2?.SubKind, Is.EqualTo(ProjectEntitySubKind.Cli));
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
        Assert.That(kind?.Kind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(kind?.SubKind, Is.EqualTo(ProjectEntitySubKind.Web));
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
        Assert.That(kind?.Kind, Is.EqualTo(ProjectEntityKind.DatabaseMigration));
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
        Assert.That(role, Is.EqualTo(ProjectRole.FrontendApp));
        Assert.That(isLib, Is.False);
        Assert.That(classification.Kind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(classification.SubKind, Is.EqualTo(ProjectEntitySubKind.Web));
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

        Assert.That(role, Is.EqualTo(ProjectRole.Service));
        Assert.That(isLib, Is.False);
        Assert.That(classification.Kind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(extensions["primary_role"], Is.EqualTo("WebService"));
        Assert.That(extensions["secondary_roles"], Does.Contain("Scheduler"));
        Assert.That(extensions["all_roles"], Does.Contain("WebService").And.Contain("Scheduler"));
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

        Assert.That(role, Is.EqualTo(ProjectRole.Worker));
        Assert.That(isLib, Is.False);
        Assert.That(classification.Kind, Is.EqualTo(ProjectEntityKind.Worker));
        Assert.That(extensions["primary_role"], Is.EqualTo("Scheduler"));
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

        Assert.That(node.Role, Is.EqualTo("Service"));
        Assert.That(node.IsLibrary, Is.False);
        Assert.That(node.PrimaryRole, Is.EqualTo("WebService"));
        Assert.That(node.SecondaryRoles, Does.Contain("Scheduler"));
        Assert.That(node.AllRoles, Does.Contain("WebService").And.Contain("Scheduler"));
    }
}
