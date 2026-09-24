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

        Assert.That(node, Is.InstanceOf<ProjectNode>());
        Assert.That(node.Kind, Is.EqualTo("Project"));
        Assert.That(node.Role, Is.EqualTo("Service"));
        Assert.That(node.IsLibrary, Is.False);
        Assert.That(node.Extensions?["role"], Is.EqualTo("Service"));
        Assert.That(node.Extensions?["entity_type"], Is.EqualTo("service"));
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

        Assert.That(node, Is.InstanceOf<ProjectNode>());
        Assert.That(node.Kind, Is.EqualTo("Project"));
        Assert.That(node.Role, Is.EqualTo("FrontendApp"));
        Assert.That(node.IsLibrary, Is.False);
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

        Assert.That(node, Is.InstanceOf<ProjectNode>());
        Assert.That(node.Kind, Is.EqualTo("Project"));
        Assert.That(node.Role, Is.EqualTo("SharedLibrary"));
        Assert.That(node.IsLibrary, Is.True);
        Assert.That(node.Extensions?["entity_type"], Is.EqualTo("library"));
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

        Assert.That(node, Is.InstanceOf<ProjectNode>());
        Assert.That(node.Kind, Is.EqualTo("Project"));
        Assert.That(node.Role, Is.EqualTo("Worker"));
        Assert.That(node.IsLibrary, Is.False);
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

        Assert.That(node, Is.InstanceOf<ProjectNode>());
        Assert.That(node.Kind, Is.EqualTo("Project"));
        Assert.That(node.Role, Is.EqualTo("CliTool"));
        Assert.That(node.IsLibrary, Is.False);
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

        Assert.That(node, Is.InstanceOf<ProjectNode>());
        Assert.That(node.Role, Is.EqualTo("Test"));
        Assert.That(node.IsLibrary, Is.True);
    }
}
