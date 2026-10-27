using CodeExplorer.Core.Analysis;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectRoleDetectorTests
{
    [Test]
    public void DetectRole_IdentifiesTestProjects()
    {
        var (role1, isLib1) = ProjectRoleDetector.DetectRole(
            "/workspace/tests/Orders.Tests",
            ["/workspace/tests/Orders.Tests/OrderTests.cs"],
            "tests/Orders.Tests",
            "Orders.Tests",
            "csharp",
            ["nunit", "nunit3testadapter"]
        );
        role1.Should().Be(ProjectRole.Test);
        isLib1.Should().BeTrue();

        var (role2, isLib2) = ProjectRoleDetector.DetectRole(
            "/workspace/apps/frontend/test",
            ["/workspace/apps/frontend/test/app.test.ts"],
            "apps/frontend/test",
            "frontend-test",
            "typescript",
            ["jest", "@types/jest"]
        );
        role2.Should().Be(ProjectRole.Test);
        isLib2.Should().BeTrue();
    }

    [Test]
    public void DetectRole_IdentifiesFrontendApps()
    {
        var (role1, isLib1) = ProjectRoleDetector.DetectRole(
            "/workspace/apps/web-app",
            ["/workspace/apps/web-app/vite.config.ts", "/workspace/apps/web-app/index.html"],
            "apps/web-app",
            "web-app",
            "typescript",
            ["react", "react-dom"]
        );
        role1.Should().Be(ProjectRole.FrontendApp);
        isLib1.Should().BeFalse();

        var (role2, isLib2) = ProjectRoleDetector.DetectRole(
            "/workspace/apps/admin-portal",
            ["/workspace/apps/admin-portal/next.config.js"],
            "apps/admin-portal",
            "admin-portal",
            "typescript"
        );
        role2.Should().Be(ProjectRole.FrontendApp);
        isLib2.Should().BeFalse();
    }

    [Test]
    public void DetectRole_IdentifiesSharedLibraries()
    {
        var (role1, isLib1) = ProjectRoleDetector.DetectRole(
            "/workspace/libs/common-contracts",
            ["/workspace/libs/common-contracts/OrderDto.ts"],
            "libs/common-contracts",
            "common-contracts",
            "typescript"
        );
        role1.Should().Be(ProjectRole.SharedLibrary);
        isLib1.Should().BeTrue();

        var (role2, isLib2) = ProjectRoleDetector.DetectRole(
            "/workspace/src/Core/Domain",
            ["/workspace/src/Core/Domain/User.cs"],
            "src/Core/Domain",
            "MyApp.Domain",
            "csharp"
        );
        role2.Should().Be(ProjectRole.SharedLibrary);
        isLib2.Should().BeTrue();
    }

    [Test]
    public void DetectRole_IdentifiesWorkersAndCliTools()
    {
        var (roleWorker, isLibWorker) = ProjectRoleDetector.DetectRole(
            "/workspace/services/email-worker",
            ["/workspace/services/email-worker/Program.cs"],
            "services/email-worker",
            "email-worker",
            "csharp"
        );
        roleWorker.Should().Be(ProjectRole.Worker);
        isLibWorker.Should().BeFalse();

        var (roleCli, isLibCli) = ProjectRoleDetector.DetectRole(
            "/workspace/tools/db-migrator-cli",
            ["/workspace/tools/db-migrator-cli/index.ts"],
            "tools/db-migrator-cli",
            "db-migrator-cli",
            "typescript"
        );
        roleCli.Should().Be(ProjectRole.CliTool);
        isLibCli.Should().BeFalse();
    }

    [Test]
    public void DetectRole_IdentifiesDatabaseMigrations()
    {
        var (role1, isLib1) = ProjectRoleDetector.DetectRole(
            "/workspace/db/migrations",
            ["/workspace/db/migrations/V1__init.sql"],
            "db/migrations",
            "migrations",
            "sql"
        );
        role1.Should().Be(ProjectRole.DatabaseMigration);
        isLib1.Should().BeFalse();
    }

    [Test]
    public void DetectRole_DefaultsToServiceForMicroservicesAndApis()
    {
        var (role, isLib) = ProjectRoleDetector.DetectRole(
            "/workspace/services/order-service",
            ["/workspace/services/order-service/server.ts"],
            "services/order-service",
            "order-service",
            "typescript",
            ["express", "pg"]
        );
        role.Should().Be(ProjectRole.Service);
        isLib.Should().BeFalse();
    }

    [Test]
    public void DetectRole_IdentifiesAngularLibrariesAndSecondaryEntryPoints()
    {
        var (roleLib, isLib) = ProjectRoleDetector.DetectRole(
            "/workspace/fe/projects/ui/src/lib/button",
            ["/workspace/fe/projects/ui/src/lib/button/button.component.ts", "/workspace/fe/projects/ui/src/lib/button/public-api.ts"],
            "fe/projects/ui/src/lib/button",
            "button",
            "typescript"
        );
        roleLib.Should().Be(ProjectRole.SharedLibrary);
        isLib.Should().BeTrue();

        var (roleRootLib, isRootLib) = ProjectRoleDetector.DetectRole(
            "/workspace/fe/projects/ui",
            ["/workspace/fe/projects/ui/ng-package.json", "/workspace/fe/projects/ui/package.json"],
            "fe/projects/ui",
            "@atsystems/ui",
            "typescript"
        );
        roleRootLib.Should().Be(ProjectRole.SharedLibrary);
        isRootLib.Should().BeTrue();
    }

    [Test]
    public void DetectRole_DoesNotClassifyBackendServiceWithJestAsTestProject()
    {
        var (role, isLib) = ProjectRoleDetector.DetectRole(
            "/workspace/services/bff",
            ["/workspace/services/bff/main.ts", "/workspace/services/bff/app.module.ts", "/workspace/services/bff/app.controller.spec.ts"],
            "services/bff",
            "bff",
            "typescript",
            ["@nestjs/core", "jest", "@types/jest", "supertest"]
        );
        role.Should().Be(ProjectRole.Service);
        isLib.Should().BeFalse();
    }

    [Test]
    public void DetectRole_UsesExplicitManifestEvidence()
    {
        // 1. Library from manifest_type
        var (roleLib, isLib) = ProjectRoleDetector.DetectRole(
            "/workspace/packages/auth",
            ["/workspace/packages/auth/index.ts"],
            "packages/auth",
            "auth",
            "typescript",
            extensions: new Dictionary<string, string> { ["manifest_type"] = "library" }
        );
        roleLib.Should().Be(ProjectRole.SharedLibrary);
        isLib.Should().BeTrue();

        // 2. CLI from has_cli_bin
        var (roleCli, isCli) = ProjectRoleDetector.DetectRole(
            "/workspace/tools/runner",
            ["/workspace/tools/runner/index.ts"],
            "tools/runner",
            "runner",
            "typescript",
            extensions: new Dictionary<string, string> { ["has_cli_bin"] = "true" }
        );
        roleCli.Should().Be(ProjectRole.CliTool);
        isCli.Should().BeFalse();

        // 3. Worker from worker SDK
        var (roleWorker, isWorker) = ProjectRoleDetector.DetectRole(
            "/workspace/services/queue",
            ["/workspace/services/queue/Program.cs"],
            "services/queue",
            "queue",
            "csharp",
            extensions: new Dictionary<string, string> { ["sdk"] = "Microsoft.NET.Sdk.Worker" }
        );
        roleWorker.Should().Be(ProjectRole.Worker);
        isWorker.Should().BeFalse();
    }
}
