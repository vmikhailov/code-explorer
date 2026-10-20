using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Go;
using NUnit.Framework;

namespace CodeExplorer.Tests;

public class GoWebFrameworksTests
{
    private static List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();
        foreach (var node in nodes)
        {
            if (node is EndpointNode ep)
            {
                result.Add(ep);
            }
            result.AddRange(FindEndpointNodes(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Gin_RoutesAndGroups_ExtractsEndpointsAndHandlers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-gin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "main.go");

        var code = @"package main

import ""github.com/gin-gonic/gin""

func getOrders(c *gin.Context) {}
func createOrder(c *gin.Context) {}
func deleteItem(c *gin.Context) {}

func setupRouter() {
    r := gin.Default()
    r.GET(""/orders"", getOrders)

    v1 := r.Group(""/api/v1"")
    v1.POST(""/orders"", createOrder)

    api := r.Group(""/api"")
    sub := api.Group(""/v2"")
    sub.DELETE(""/items/:id"", deleteItem)
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);

            var rootGet = endpoints.FirstOrDefault(e => e.Name == "GET:/orders");
            rootGet.Should().NotBeNull();
            rootGet!.RouteTemplate.Should().Be("/orders");
            rootGet.HttpMethod.Should().Be("GET");

            var v1Post = endpoints.FirstOrDefault(e => e.Name == "POST:/api/v1/orders");
            v1Post.Should().NotBeNull();
            v1Post!.RouteTemplate.Should().Be("/api/v1/orders");
            v1Post.HttpMethod.Should().Be("POST");

            var v2Delete = endpoints.FirstOrDefault(e => e.Name == "DELETE:/api/v2/items/:id");
            v2Delete.Should().NotBeNull();
            v2Delete!.RouteTemplate.Should().Be("/api/v2/items/:id");
            v2Delete.HttpMethod.Should().Be("DELETE");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Fiber_RoutesAndGroups_ExtractsEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-fiber-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "main.go");

        var code = @"package main

import ""github.com/gofiber/fiber/v2""

func handleHealth(c *fiber.Ctx) error { return nil }
func handleUsers(c *fiber.Ctx) error { return nil }
func handleUpdate(c *fiber.Ctx) error { return nil }

func setupApp() {
    app := fiber.New()
    app.Get(""/health"", handleHealth)

    api := app.Group(""/api/v1"")
    api.Post(""/users"", handleUsers)

    app.Add(""PUT"", ""/orders/:id"", handleUpdate)
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);

            var healthEp = endpoints.FirstOrDefault(e => e.Name == "GET:/health");
            healthEp.Should().NotBeNull();
            healthEp!.RouteTemplate.Should().Be("/health");
            healthEp.HttpMethod.Should().Be("GET");

            var usersEp = endpoints.FirstOrDefault(e => e.Name == "POST:/api/v1/users");
            usersEp.Should().NotBeNull();
            usersEp!.RouteTemplate.Should().Be("/api/v1/users");
            usersEp.HttpMethod.Should().Be("POST");

            var updateEp = endpoints.FirstOrDefault(e => e.Name == "PUT:/orders/:id");
            updateEp.Should().NotBeNull();
            updateEp!.RouteTemplate.Should().Be("/orders/:id");
            updateEp.HttpMethod.Should().Be("PUT");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Echo_RoutesAndGroups_ExtractsEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-echo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "main.go");

        var code = @"package main

import ""github.com/labstack/echo/v4""

func handleRoot(c echo.Context) error { return nil }
func handleItems(c echo.Context) error { return nil }
func handlePatch(c echo.Context) error { return nil }

func setupEcho() {
    e := echo.New()
    e.GET(""/"", handleRoot)

    v1 := e.Group(""/api/v1"")
    v1.GET(""/items"", handleItems)

    e.Add(""PATCH"", ""/users/:id"", handlePatch)
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);

            var rootEp = endpoints.FirstOrDefault(e => e.Name == "GET:/");
            rootEp.Should().NotBeNull();
            rootEp!.RouteTemplate.Should().Be("/");
            rootEp.HttpMethod.Should().Be("GET");

            var itemsEp = endpoints.FirstOrDefault(e => e.Name == "GET:/api/v1/items");
            itemsEp.Should().NotBeNull();
            itemsEp!.RouteTemplate.Should().Be("/api/v1/items");
            itemsEp.HttpMethod.Should().Be("GET");

            var patchEp = endpoints.FirstOrDefault(e => e.Name == "PATCH:/users/:id");
            patchEp.Should().NotBeNull();
            patchEp!.RouteTemplate.Should().Be("/users/:id");
            patchEp.HttpMethod.Should().Be("PATCH");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SampleProjects_Go_Services_ExtractAllEndpoints()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "cli", "tests", "SampleProjects")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("SampleProjects root must be resolvable");
        var sampleProjectsRoot = Path.Combine(dir!.FullName, "cli", "tests", "SampleProjects");

        var parser = new GoParser();

        // 1. Gin service
        var ginPath = Path.Combine(sampleProjectsRoot, "go", "gin-service", "main.go");
        File.Exists(ginPath).Should().BeTrue();
        var ginTree = await parser.ParseAsync(ginPath, "gin-proj", "ws", sampleProjectsRoot);
        Layer3SyntacticParser.ProcessVisitor(ginTree, "ws", sampleProjectsRoot);
        var ginEndpoints = FindEndpointNodes(ginTree.FileNode!.Children);
        ginEndpoints.Select(e => e.Name).Should().Contain(["GET:/api/v1/orders", "POST:/api/v1/orders"]);

        // 2. Fiber service
        var fiberPath = Path.Combine(sampleProjectsRoot, "go", "fiber-service", "main.go");
        File.Exists(fiberPath).Should().BeTrue();
        var fiberTree = await parser.ParseAsync(fiberPath, "fiber-proj", "ws", sampleProjectsRoot);
        Layer3SyntacticParser.ProcessVisitor(fiberTree, "ws", sampleProjectsRoot);
        var fiberEndpoints = FindEndpointNodes(fiberTree.FileNode!.Children);
        fiberEndpoints.Select(e => e.Name).Should().Contain(["GET:/health", "GET:/api/v1/users"]);

        // 3. Echo service
        var echoPath = Path.Combine(sampleProjectsRoot, "go", "echo-service", "main.go");
        File.Exists(echoPath).Should().BeTrue();
        var echoTree = await parser.ParseAsync(echoPath, "echo-proj", "ws", sampleProjectsRoot);
        Layer3SyntacticParser.ProcessVisitor(echoTree, "ws", sampleProjectsRoot);
        var echoEndpoints = FindEndpointNodes(echoTree.FileNode!.Children);
        echoEndpoints.Select(e => e.Name).Should().Contain(["GET:/", "GET:/api/v1/items"]);
    }
}
