using CodeExplorer.Core.Database;
using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
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
            await using var client = new InMemoryGraphRepository();
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
            await using var client = new InMemoryGraphRepository();
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
            await using var client = new InMemoryGraphRepository();
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

    [Test]
    public async Task Chi_RoutesAndGroups_ExtractsEndpointsAndHandlers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-chi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "main.go");

        var code = @"package main

import (
	""net/http""
	""github.com/go-chi/chi/v5""
)

func getWorkouts(w http.ResponseWriter, r *http.Request) {}
func createWorkout(w http.ResponseWriter, r *http.Request) {}
func cancelWorkout(w http.ResponseWriter, r *http.Request) {}

func setupRouter() {
	r := chi.NewRouter()
	r.Get(""/workouts"", getWorkouts)

	r.Route(""/api/v1"", func(r chi.Router) {
		r.Post(""/workouts"", createWorkout)
		r.Delete(""/workouts/{id}"", cancelWorkout)
	})
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);

            var rootGet = endpoints.FirstOrDefault(e => e.Name == "GET:/workouts");
            rootGet.Should().NotBeNull();
            rootGet!.RouteTemplate.Should().Be("/workouts");
            rootGet.HttpMethod.Should().Be("GET");

            var v1Post = endpoints.FirstOrDefault(e => e.Name == "POST:/api/v1/workouts");
            v1Post.Should().NotBeNull();
            v1Post!.RouteTemplate.Should().Be("/api/v1/workouts");
            v1Post.HttpMethod.Should().Be("POST");

            var v1Delete = endpoints.FirstOrDefault(e => e.Name == "DELETE:/api/v1/workouts/*");
            v1Delete.Should().NotBeNull();
            v1Delete!.RouteTemplate.Should().Be("/api/v1/workouts/*");
            v1Delete.HttpMethod.Should().Be("DELETE");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task GrpcGateway_RoutesAndPatterns_ExtractsRestEndpointsAndCallsGrpc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-gw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "service.pb.gw.go");

        var code = @"package service

import (
	""context""
	""net/http""
	""github.com/grpc-ecosystem/grpc-gateway/runtime""
	""google.golang.org/grpc""
)

type ItemServiceClient interface{}

func RegisterItemServiceHandlerClient(ctx context.Context, mux *runtime.ServeMux, client ItemServiceClient) error {
	mux.Handle(""GET"", pattern_ItemService_GetItem_0, func(w http.ResponseWriter, req *http.Request, pathParams map[string]string) {})
	mux.Handle(""POST"", pattern_ItemService_CreateItem_0, func(w http.ResponseWriter, req *http.Request, pathParams map[string]string) {})
	return nil
}

var (
	pattern_ItemService_GetItem_0 = runtime.MustPattern(runtime.NewPattern(1, []int{2, 0, 2, 1, 1, 0, 4, 1, 5, 2}, []string{""api"", ""items"", ""id""}, """", runtime.AssumeColonVerbOpt(true)))
	pattern_ItemService_CreateItem_0 = runtime.MustPattern(runtime.NewPattern(1, []int{2, 0, 2, 1}, []string{""api"", ""items""}, """", runtime.AssumeColonVerbOpt(true)))
)
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(2);

            var getEp = endpoints.FirstOrDefault(e => e.Name == "GET:/api/items/{id}");
            getEp.Should().NotBeNull();
            getEp!.RouteTemplate.Should().Be("/api/items/{id}");
            getEp.HttpMethod.Should().Be("GET");
            getEp.Protocol.Should().Be("REST");

            var postEp = endpoints.FirstOrDefault(e => e.Name == "POST:/api/items");
            postEp.Should().NotBeNull();
            postEp!.RouteTemplate.Should().Be("/api/items");
            postEp.HttpMethod.Should().Be("POST");

            // Verify calls to gRPC services
            var references = fileNode.Children.SelectMany(c => c.References).ToList();
            references.Should().Contain(r => r.TargetName == "RPC:/ItemService/GetItem" && r.Kind == "CALLS");
            references.Should().Contain(r => r.TargetName == "RPC:/ItemService/CreateItem" && r.Kind == "CALLS");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Grpc_StreamingAndQuerySignatures_ExtractsEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-grpc-stream-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "server.go");

        var code = @"package repository

import (
	""context""
	""google.golang.org/grpc""
)

type Service struct{}

type Query struct{}
type AppResponse struct{}

type RepoService_StreamFilesServer interface {
	grpc.ServerStream
}

func (s *Service) WatchItems(q *Query, stream RepoService_StreamFilesServer) error {
	return nil
}

func (s *Service) GetDetails(ctx context.Context, q *Query) (*AppResponse, error) {
	return nil, nil
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(2);
            endpoints.Select(e => e.Name).Should().Contain(["RPC:/RepoService/WatchItems", "RPC:/RepoService/GetDetails"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Fasthttp_Router_ExtractsEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-fasthttp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "main.go");

        var code = @"package main

import (
	""github.com/buaazp/fasthttprouter""
	""github.com/valyala/fasthttp""
)

func Index(ctx *fasthttp.RequestCtx) {}
func GetOrder(ctx *fasthttp.RequestCtx) {}
func CreateOrder(ctx *fasthttp.RequestCtx) {}

func setupRouter() {
	router := fasthttprouter.New()
	router.GET(""/orders/:id"", GetOrder)
	router.POST(""/orders"", CreateOrder)
	router.Handle(""DELETE"", ""/orders/:id"", Index)
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);
            endpoints.Select(e => e.Name).Should().Contain(["GET:/orders/:id", "POST:/orders", "DELETE:/orders/:id"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Dapr_DeclarativeEndpointsTable_ExtractsEndpoints()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-dapr-endpoints-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "metadata.go");

        var code = @"package http

import (
	""net/http""
	""github.com/dapr/dapr/pkg/api/http/endpoints""
)

type api struct {}

func (a *api) onGetMetadata(w http.ResponseWriter, r *http.Request) {}
func (a *api) onSetMetadata(w http.ResponseWriter, r *http.Request) {}

func (a *api) Endpoints() []endpoints.Endpoint {
	return []endpoints.Endpoint{
		{
			Methods: []string{http.MethodGet, http.MethodPost},
			Route:   ""metadata"",
			Version: 1,
			Handler: a.onGetMetadata,
		},
		{
			Methods: []string{http.MethodPut},
			Route:   ""metadata/:key"",
			Version: 1,
			Handler: a.onSetMetadata,
		},
	}
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);
            endpoints.Select(e => e.Name).Should().Contain(["GET:/v1.0/metadata", "POST:/v1.0/metadata", "PUT:/v1.0/metadata/:key"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Dapr_Grpc_CleanServiceNames_ExtractsNormalizedServices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-test-dapr-grpc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "server.go");

        var code = @"package internal

import (
	""context""
	""google.golang.org/grpc""
)

type InternalServer interface {
	CallLocal(ctx context.Context, in *internalv1pb.InternalInvokeRequest) (*internalv1pb.InternalInvokeResponse, error)
}

type Server struct{}

func (s *Server) CallLocal(ctx context.Context, in *internalv1pb.InternalInvokeRequest) (*internalv1pb.InternalInvokeResponse, error) {
	return nil, nil
}
";
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(1);
            endpoints[0].Name.Should().Be("RPC:/InternalService/CallLocal");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
