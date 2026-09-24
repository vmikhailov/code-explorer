using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
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
        Assert.That(role1, Is.EqualTo(ProjectRole.Test));
        Assert.That(isLib1, Is.True);

        var (role2, isLib2) = ProjectRoleDetector.DetectRole(
            "/workspace/apps/frontend/test",
            ["/workspace/apps/frontend/test/app.test.ts"],
            "apps/frontend/test",
            "frontend-test",
            "typescript",
            ["jest", "@types/jest"]
        );
        Assert.That(role2, Is.EqualTo(ProjectRole.Test));
        Assert.That(isLib2, Is.True);
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
        Assert.That(role1, Is.EqualTo(ProjectRole.FrontendApp));
        Assert.That(isLib1, Is.False);

        var (role2, isLib2) = ProjectRoleDetector.DetectRole(
            "/workspace/apps/admin-portal",
            ["/workspace/apps/admin-portal/next.config.js"],
            "apps/admin-portal",
            "admin-portal",
            "typescript"
        );
        Assert.That(role2, Is.EqualTo(ProjectRole.FrontendApp));
        Assert.That(isLib2, Is.False);
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
        Assert.That(role1, Is.EqualTo(ProjectRole.SharedLibrary));
        Assert.That(isLib1, Is.True);

        var (role2, isLib2) = ProjectRoleDetector.DetectRole(
            "/workspace/src/Core/Domain",
            ["/workspace/src/Core/Domain/User.cs"],
            "src/Core/Domain",
            "MyApp.Domain",
            "csharp"
        );
        Assert.That(role2, Is.EqualTo(ProjectRole.SharedLibrary));
        Assert.That(isLib2, Is.True);
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
        Assert.That(roleWorker, Is.EqualTo(ProjectRole.Worker));
        Assert.That(isLibWorker, Is.False);

        var (roleCli, isLibCli) = ProjectRoleDetector.DetectRole(
            "/workspace/tools/db-migrator-cli",
            ["/workspace/tools/db-migrator-cli/index.ts"],
            "tools/db-migrator-cli",
            "db-migrator-cli",
            "typescript"
        );
        Assert.That(roleCli, Is.EqualTo(ProjectRole.CliTool));
        Assert.That(isLibCli, Is.False);
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
        Assert.That(role1, Is.EqualTo(ProjectRole.DatabaseMigration));
        Assert.That(isLib1, Is.False);
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
        Assert.That(role, Is.EqualTo(ProjectRole.Service));
        Assert.That(isLib, Is.False);
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
        Assert.That(roleLib, Is.EqualTo(ProjectRole.SharedLibrary));
        Assert.That(isLib, Is.True);

        var (roleRootLib, isRootLib) = ProjectRoleDetector.DetectRole(
            "/workspace/fe/projects/ui",
            ["/workspace/fe/projects/ui/ng-package.json", "/workspace/fe/projects/ui/package.json"],
            "fe/projects/ui",
            "@atsystems/ui",
            "typescript"
        );
        Assert.That(roleRootLib, Is.EqualTo(ProjectRole.SharedLibrary));
        Assert.That(isRootLib, Is.True);
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
        Assert.That(role, Is.EqualTo(ProjectRole.Service));
        Assert.That(isLib, Is.False);
    }
}
