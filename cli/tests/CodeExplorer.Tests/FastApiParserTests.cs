using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Components.Parsers;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Python;

namespace CodeExplorer.Tests;

[TestFixture]
public class FastApiParserTests
{
    private readonly PythonParser _parser = new();

    [SetUp]
    public void SetUp()
    {
        RouteDictionaryRegistry.Clear();
    }

    private static async Task<(List<EndpointNode> Endpoints, List<IOntologyNode> AllNodes)> ParsePythonFileAsync(PythonParser parser, string code, string fileName = "main.py")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_fastapi_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var endpoints = new List<EndpointNode>();
            var all = new List<IOntologyNode>();
            CollectNodes(syntaxTree.FileNode.Children, endpoints, all);
            return (endpoints, all);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void CollectNodes(IEnumerable<IOntologyNode> nodes, List<EndpointNode> endpoints, List<IOntologyNode> all)
    {
        foreach (var node in nodes)
        {
            all.Add(node);
            if (node is EndpointNode ep)
            {
                endpoints.Add(ep);
            }
            CollectNodes(node.Children, endpoints, all);
        }
    }

    [Test]
    public async Task FastApi_BasicRouteMethods_ParsedAsEndpoints()
    {
        var code = """
        from fastapi import FastAPI

        app = FastAPI()

        @app.get("/")
        def read_root():
            return {"message": "Hello World"}

        @app.post("/items")
        async def create_item():
            return {"item": "created"}

        @app.put("/items/{item_id}")
        def update_item(item_id: int):
            return {"item_id": item_id}

        @app.delete("/items/{item_id}")
        def delete_item(item_id: int):
            return {"deleted": True}
        """;

        var (endpoints, _) = await ParsePythonFileAsync(_parser, code);

        Assert.That(endpoints, Has.Count.EqualTo(4));

        var getRoot = endpoints.FirstOrDefault(e => e.HttpMethod == "GET" && e.RouteTemplate == "/");
        Assert.That(getRoot, Is.Not.Null);
        Assert.That(getRoot!.Protocol, Is.EqualTo("REST"));

        var postItem = endpoints.FirstOrDefault(e => e.HttpMethod == "POST" && e.RouteTemplate == "/items");
        Assert.That(postItem, Is.Not.Null);

        var putItem = endpoints.FirstOrDefault(e => e.HttpMethod == "PUT" && e.RouteTemplate == "/items/{item_id}");
        Assert.That(putItem, Is.Not.Null);

        var delItem = endpoints.FirstOrDefault(e => e.HttpMethod == "DELETE" && e.RouteTemplate == "/items/{item_id}");
        Assert.That(delItem, Is.Not.Null);
    }

    [Test]
    public async Task FastApi_APIRouter_ResolvesPrefix()
    {
        var code = """
        from fastapi import APIRouter

        router = APIRouter(prefix="/api/v1/users", tags=["users"])

        @router.get("/")
        def list_users():
            return []

        @router.post("/{user_id}/activate")
        def activate_user(user_id: int):
            return {"status": "active"}
        """;

        var (endpoints, _) = await ParsePythonFileAsync(_parser, code, "users.py");

        Assert.That(endpoints, Has.Count.EqualTo(2));

        var listEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
        Assert.That(listEp, Is.Not.Null);
        Assert.That(listEp!.RouteTemplate, Is.EqualTo("/api/v1/users"));

        var actEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST");
        Assert.That(actEp, Is.Not.Null);
        Assert.That(actEp!.RouteTemplate, Is.EqualTo("/api/v1/users/{user_id}/activate"));
    }

    [Test]
    public async Task FastApi_AppIncludeRouter_ResolvesPrefix()
    {
        var code = """
        from fastapi import FastAPI, APIRouter

        app = FastAPI()
        order_router = APIRouter()

        app.include_router(order_router, prefix="/orders")

        @order_router.get("/{order_id}")
        def get_order(order_id: str):
            return {"order": order_id}
        """;

        var (endpoints, _) = await ParsePythonFileAsync(_parser, code);

        Assert.That(endpoints, Has.Count.EqualTo(1));
        var ep = endpoints[0];
        Assert.That(ep.HttpMethod, Is.EqualTo("GET"));
        Assert.That(ep.RouteTemplate, Is.EqualTo("/orders/{order_id}"));
    }

    [Test]
    public async Task FastApi_WebSocketAndApiRoute_ParsedCorrectly()
    {
        var code = """
        from fastapi import FastAPI, WebSocket

        app = FastAPI()

        @app.websocket("/ws/notifications")
        async def websocket_notifications(ws: WebSocket):
            await ws.accept()

        @app.api_route("/sync", methods=["GET", "POST"])
        def sync_data():
            return {"synced": True}
        """;

        var (endpoints, allNodes) = await ParsePythonFileAsync(_parser, code);

        var wsEp = allNodes.OfType<EntryPointNode>().FirstOrDefault(e => e.Name.Contains("/ws/notifications"));
        Assert.That(wsEp, Is.Not.Null);

        var syncEp = endpoints.FirstOrDefault(e => e.RouteTemplate == "/sync");
        Assert.That(syncEp, Is.Not.Null);
        Assert.That(syncEp!.HttpMethod, Is.EqualTo("GET,POST"));
    }

    [Test]
    public async Task FastApi_PayloadSchemas_ExtractedFromModelsAndAnnotations()
    {
        var code = """
        from fastapi import FastAPI, Body
        from typing import List
        from pydantic import BaseModel

        class UserCreate(BaseModel):
            username: str

        class UserResponse(BaseModel):
            id: int
            username: str

        class OrderDto(BaseModel):
            order_id: str

        app = FastAPI()

        @app.post("/users", response_model=UserResponse, status_code=201, summary="Create a user")
        def create_user(payload: UserCreate):
            return {"id": 1, "username": payload.username}

        @app.get("/orders", response_model=List[OrderDto])
        def list_orders():
            return []

        @app.put("/custom")
        def custom_action(data: dict = Body(...)) -> UserResponse:
            return None
        """;

        var (endpoints, _) = await ParsePythonFileAsync(_parser, code);

        Assert.That(endpoints, Has.Count.EqualTo(3));

        var createEp = endpoints.FirstOrDefault(e => e.HttpMethod == "POST" && e.RouteTemplate == "/users");
        Assert.That(createEp, Is.Not.Null);
        Assert.That(createEp!.RequestType, Is.EqualTo("UserCreate"));
        Assert.That(createEp.ResponseType, Is.EqualTo("UserResponse"));
        Assert.That(createEp.Extensions, Is.Not.Null);
        Assert.That(createEp.Extensions!.GetValueOrDefault("status_code"), Is.EqualTo("201"));
        Assert.That(createEp.Extensions!.GetValueOrDefault("summary"), Is.EqualTo("Create a user"));

        var listOrdersEp = endpoints.FirstOrDefault(e => e.HttpMethod == "GET" && e.RouteTemplate == "/orders");
        Assert.That(listOrdersEp, Is.Not.Null);
        Assert.That(listOrdersEp!.ResponseType, Is.EqualTo("OrderDto"));

        var customEp = endpoints.FirstOrDefault(e => e.HttpMethod == "PUT" && e.RouteTemplate == "/custom");
        Assert.That(customEp, Is.Not.Null);
        Assert.That(customEp!.RequestType, Is.EqualTo("dict"));
        Assert.That(customEp.ResponseType, Is.EqualTo("UserResponse"));
    }

    [Test]
    public async Task FastApi_DependenciesAndSecurityScopes_Extracted()
    {
        var code = """
        from fastapi import FastAPI, Depends, Security

        def get_db():
            return None

        def get_current_user():
            return "user"

        app = FastAPI()

        @app.get("/admin/dashboard")
        def admin_view(
            db = Depends(get_db),
            user = Security(get_current_user, scopes=["admin", "metrics:read"])
        ):
            return {"admin": True}
        """;

        var (endpoints, _) = await ParsePythonFileAsync(_parser, code);

        Assert.That(endpoints, Has.Count.EqualTo(1));
        var ep = endpoints[0];

        Assert.That(ep.RequiredRoles, Is.EqualTo("admin,metrics:read"));

        var callsRefs = ep.References.Where(r => r.Kind == CodeExplorer.Core.Common.OntologyConstants.Relationships.Calls).ToList();
        var depNames = callsRefs.Select(r => r.TargetName).ToList();
        Assert.That(depNames, Does.Contain("get_db"));
        Assert.That(depNames, Does.Contain("get_current_user"));

        var triggerRef = ep.References.FirstOrDefault(r => r.Kind == CodeExplorer.Core.Common.OntologyConstants.Relationships.Triggers);
        Assert.That(triggerRef, Is.Not.Null);
        Assert.That(triggerRef!.TargetName, Is.EqualTo("admin_view"));
    }

    [Test]
    public void FastApiComponentParser_ClassifiesProjectManifest()
    {
        var componentParser = new FastApiComponentParser();
        var context = new ProjectContext(
            DirectoryPath: "c:/repo/service",
            RelativeProjectDir: "service",
            ProjectName: "my-service",
            ProjectType: "python",
            FilesInDirectory: ["main.py", "requirements.txt"],
            Dependencies: ["fastapi", "uvicorn", "pydantic"],
            ManifestProperties: new Dictionary<string, string>()
        );

        Assert.That(componentParser.CanHandle(context), Is.True);

        var result = componentParser.AnalyzeManifest(context);
        Assert.That(result.ComponentId, Is.EqualTo("fastapi"));
        Assert.That(result.Role, Is.EqualTo(CodeExplorer.Core.Parser.LibraryRole.WebService));
        Assert.That(result.Capabilities, Is.EqualTo(CodeExplorer.Core.Parser.Components.ComponentCapabilities.HttpEndpoints));
        Assert.That(result.Metadata["web_framework"], Is.EqualTo("fastapi"));
    }
}
