using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectNodeFactoryTests
{
    [Test]
    public void Create_InstantiatesServiceNode_ForBackendService()
    {
        var node = ProjectNodeFactory.Create(
            "ws:project:services/orders:",
            "orders-service",
            "services/orders",
            "csharp",
            "/workspace/services/orders",
            ["/workspace/services/orders/Program.cs", "/workspace/services/orders/Orders.csproj"]
        );

        node.Should().BeOfType<ProjectNode>();
        node.Kind.Should().Be("Project");
        node.Role.Should().Be("Service");
        node.IsLibrary.Should().BeFalse();
        node.Extensions?["role"].Should().Be("Service");
        node.Extensions?["entity_type"].Should().Be("service");
    }

    [Test]
    public void Create_InstantiatesAppNode_ForFrontendApp()
    {
        var node = ProjectNodeFactory.Create(
            "ws:project:apps/web-client:",
            "web-client",
            "apps/web-client",
            "typescript",
            "/workspace/apps/web-client",
            ["/workspace/apps/web-client/vite.config.ts", "/workspace/apps/web-client/index.html"],
            ["react", "react-dom"]
        );

        node.Should().BeOfType<ProjectNode>();
        node.Kind.Should().Be("Project");
        node.Role.Should().Be("FrontendApp");
        node.IsLibrary.Should().BeFalse();
    }

    [Test]
    public void Create_InstantiatesLibraryNode_ForSharedLibrary()
    {
        var node = ProjectNodeFactory.Create(
            "ws:project:libs/common:",
            "common-lib",
            "libs/common",
            "csharp",
            "/workspace/libs/common",
            ["/workspace/libs/common/Common.csproj"]
        );

        node.Should().BeOfType<ProjectNode>();
        node.Kind.Should().Be("Project");
        node.Role.Should().Be("SharedLibrary");
        node.IsLibrary.Should().BeTrue();
        node.Extensions?["entity_type"].Should().Be("library");
    }

    [Test]
    public void Create_InstantiatesWorkerNode_ForWorker()
    {
        var node = ProjectNodeFactory.Create(
            "ws:project:workers/email-worker:",
            "email-worker",
            "workers/email-worker",
            "csharp",
            "/workspace/workers/email-worker",
            ["/workspace/workers/email-worker/Worker.cs"]
        );

        node.Should().BeOfType<ProjectNode>();
        node.Kind.Should().Be("Project");
        node.Role.Should().Be("Worker");
        node.IsLibrary.Should().BeFalse();
    }

    [Test]
    public void Create_InstantiatesCliToolNode_ForCliTool()
    {
        var node = ProjectNodeFactory.Create(
            "ws:project:tools/db-migrator-cli:",
            "db-migrator-cli",
            "tools/db-migrator-cli",
            "csharp",
            "/workspace/tools/db-migrator-cli",
            ["/workspace/tools/db-migrator-cli/Program.cs"]
        );

        node.Should().BeOfType<ProjectNode>();
        node.Kind.Should().Be("Project");
        node.Role.Should().Be("CliTool");
        node.IsLibrary.Should().BeFalse();
    }

    [Test]
    public void Create_InstantiatesProjectNode_ForTestProjects()
    {
        var node = ProjectNodeFactory.Create(
            "ws:project:tests/order-tests:",
            "order-tests",
            "tests/order-tests",
            "csharp",
            "/workspace/tests/order-tests",
            ["/workspace/tests/order-tests/OrderTests.cs"],
            ["nunit"]
        );

        node.Should().BeOfType<ProjectNode>();
        node.Role.Should().Be("Test");
        node.IsLibrary.Should().BeTrue();
    }
}
