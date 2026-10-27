using CodeExplorer.Core.Analysis.ProjectMap;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectMapBuilderTests
{
    private static ProjectMapFile F(string path, bool test = false) => new(path, test, 10);

    private static ProjectMapSymbolEdge Call(string from, string to) => new(from, to, "CALLS", "a", "b", 1);

    [Test]
    public void Partition_DescendsIntoDominantSourceRoot()
    {
        var files = new[]
        {
            F("svc/src/main/java/com/acme/billing/controllers/A.java"),
            F("svc/src/main/java/com/acme/billing/services/B.java"),
            F("svc/src/main/java/com/acme/billing/repository/C.java"),
            F("svc/src/main/java/com/acme/billing/domain/D.java")
        };

        var result = ProjectMapPartitioner.Partition("svc", files);

        result.SourceRoot.Should().Be("src/main/java/com/acme/billing/");
        result.Granularity.Should().Be(ProjectMapGranularity.Module);
        result.Modules.Select(m => m.Name).Should().BeEquivalentTo("controllers", "services", "repository", "domain");
        result.Modules.Single(m => m.Name == "services").Path.Should().Be("svc/src/main/java/com/acme/billing/services");
    }

    [Test]
    public void Partition_RootFilesGetOwnModule()
    {
        var files = new[] { F("app/index.ts"), F("app/mcp/a.ts"), F("app/db/b.ts"), F("app/graph/c.ts") };

        var result = ProjectMapPartitioner.Partition("app", files);

        result.Modules.Should().Contain(m => m.IsRootFiles && m.Files.Contains("app/index.ts"));
        result.Modules.Count(m => !m.IsRootFiles).Should().Be(3);
    }

    [Test]
    public void Partition_FlatProject_FallsBackToFileGranularity()
    {
        var files = Enumerable.Range(0, 8).Select(i => F($"tool/file{i}.go")).ToArray();

        var result = ProjectMapPartitioner.Partition("tool", files);

        result.Granularity.Should().Be(ProjectMapGranularity.File);
        result.Modules.Should().HaveCount(8);
        result.Modules.Should().OnlyContain(m => m.Path.StartsWith("tool/file"));
    }

    [Test]
    public void Partition_FewTopFolders_UsesSubModules()
    {
        var files = new[]
        {
            F("p/src/core/a/x.ts"), F("p/src/core/b/y.ts"), F("p/src/core/c/z.ts"), F("p/src/api/w.ts")
        };

        var result = ProjectMapPartitioner.Partition("p", files);

        result.Granularity.Should().Be(ProjectMapGranularity.SubModule);
        result.Modules.Select(m => m.Name).Should().Contain(new[] { "core/a", "core/b", "core/c" });
    }

    [Test]
    public void Partition_TooManyFolders_MergesOverflowIntoOther()
    {
        var files = Enumerable.Range(0, ProjectMapPartitioner.MaxModules + 10)
            .Select(i => F($"p/m{i:D2}/f.cs"))
            .ToArray();

        var result = ProjectMapPartitioner.Partition("p", files);

        result.Modules.Should().HaveCount(ProjectMapPartitioner.MaxModules);
        result.Modules.Should().ContainSingle(m => m.IsMerged);
    }

    [Test]
    public void Build_ExcludesTestFiles_AndLiftsCrossModuleEdgesOnly()
    {
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files = [F("p/a/1.cs"), F("p/a/2.cs"), F("p/b/1.cs"), F("p/c/1.cs"), F("p/tests/t.cs", test: true)],
            SymbolEdges =
            [
                Call("p/a/1.cs", "p/b/1.cs"),
                Call("p/a/2.cs", "p/b/1.cs"),
                Call("p/a/1.cs", "p/a/2.cs"),
                Call("p/tests/t.cs", "p/a/1.cs"),
                Call("other/x.cs", "p/a/1.cs")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.TestFilesExcluded.Should().Be(1);
        result.Modules.Should().NotContain(m => m.Name == "tests");
        var edge = result.Edges.Should().ContainSingle().Subject;
        edge.SourceId.Should().Be("pm:p/a");
        edge.TargetId.Should().Be("pm:p/b");
        edge.Count.Should().Be(2);
        edge.Files.Should().HaveCount(3);
        edge.Samples.Should().HaveCount(2);
    }

    [Test]
    public void Build_DetectsThreeModuleCycle_ViaScc()
    {
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files = [F("p/a/1.cs"), F("p/b/1.cs"), F("p/c/1.cs"), F("p/d/1.cs")],
            SymbolEdges =
            [
                Call("p/a/1.cs", "p/b/1.cs"),
                Call("p/b/1.cs", "p/c/1.cs"),
                Call("p/c/1.cs", "p/a/1.cs"),
                Call("p/d/1.cs", "p/a/1.cs")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Cycles.Should().ContainSingle()
            .Which.Should().BeEquivalentTo("pm:p/a", "pm:p/b", "pm:p/c");
        result.Edges.Where(e => e.IsCircular).Should().HaveCount(3);
        result.Edges.Single(e => e.SourceId == "pm:p/d").IsCircular.Should().BeFalse();
        result.Modules.Single(m => m.Name == "d").CycleGroup.Should().BeNull();
    }

    [Test]
    public void Build_ClassifiesTiersFromTopology()
    {
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files =
            [
                F("p/handlers/1.cs"), F("p/orchestrator/1.cs"), F("p/engine/1.cs"),
                F("p/persistence/1.cs"), F("p/utils/1.cs")
            ],
            SymbolEdges =
            [
                Call("p/handlers/1.cs", "p/orchestrator/1.cs"),
                Call("p/handlers/1.cs", "p/utils/1.cs"),
                Call("p/orchestrator/1.cs", "p/engine/1.cs"),
                Call("p/orchestrator/1.cs", "p/persistence/1.cs"),
                Call("p/orchestrator/1.cs", "p/utils/1.cs"),
                Call("p/engine/1.cs", "p/utils/1.cs"),
                Call("p/persistence/1.cs", "p/utils/1.cs")
            ],
            ResourceLinks = [new ProjectMapResourceLink("p/persistence/1.cs", "tbl:orders", "Table", "orders", "WRITES_TO")],
            EntryPointsByFile = new Dictionary<string, int> { ["p/handlers/1.cs"] = 2 }
        };

        var result = ProjectMapBuilder.Build(input);
        ProjectMapTier TierOf(string name) => result.Modules.Single(m => m.Name == name).Tier;

        TierOf("handlers").Should().Be(ProjectMapTier.Ingress);
        TierOf("orchestrator").Should().Be(ProjectMapTier.Application);
        TierOf("engine").Should().Be(ProjectMapTier.Core);
        TierOf("persistence").Should().Be(ProjectMapTier.Data);
        TierOf("utils").Should().Be(ProjectMapTier.Foundation);

        result.Resources.Should().ContainSingle(r => r.Id == "tbl:orders");
        result.Edges.Should().Contain(e => e.IsResourceLink && e.SourceId == "pm:p/persistence" && e.TargetId == "tbl:orders");
    }

    [Test]
    public void Classifier_FolderNamesOnlyBreakTies()
    {
        // "db" folder with no data evidence and clearly unstable topology must not be forced into the Data ring.
        var input = new ProjectMapInput
        {
            ProjectPath = "p",
            Files = [F("p/db/1.cs"), F("p/x/1.cs"), F("p/y/1.cs"), F("p/z/1.cs")],
            SymbolEdges =
            [
                Call("p/x/1.cs", "p/db/1.cs"),
                Call("p/db/1.cs", "p/y/1.cs"),
                Call("p/db/1.cs", "p/z/1.cs"),
                Call("p/db/1.cs", "p/x/1.cs")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Modules.Single(m => m.Name == "db").Tier.Should().Be(ProjectMapTier.Application);
    }

    [Test]
    public void GraphMapper_ReusesExistingNodeKinds_AndEmitsMetadata()
    {
        var input = new ProjectMapInput
        {
            ProjectId = "ws:p:p:",
            ProjectName = "p",
            ProjectPath = "p",
            Files = [F("p/a/1.cs"), F("p/b/1.cs"), F("p/c/1.cs")],
            SymbolEdges = [Call("p/a/1.cs", "p/b/1.cs"), Call("p/b/1.cs", "p/a/1.cs")]
        };
        var graph = new CodeExplorer.Core.Protocol.GraphDataDto { Metadata = new Dictionary<string, string>() };

        ProjectMapGraphMapper.Populate(graph, ProjectMapBuilder.Build(input));

        graph.Nodes.Should().OnlyContain(n => n.Kind == CodeExplorer.Cypher.Common.NodeKind.Folder);
        graph.Metadata!["cycle_count"].Should().Be("1");
        graph.Metadata["granularity"].Should().Be("module");
        graph.Edges.Should().OnlyContain(e => e.Properties!["is_circular"] == "true");
    }

    [Test]
    public void Build_ParentProjectWithSubProjectsAndBuildOrchestrator_ShowsSubProjectsAndExcludesPublicSrcAndGulpScripts()
    {
        var input = new ProjectMapInput
        {
            ProjectId = "ws:p:src/services/cf-worker:",
            ProjectName = "cf-worker",
            ProjectPath = "src/services/cf-worker",
            IsBuildOrchestrator = true,
            ExcludedSubpaths = ["public_src"],
            Files =
            [
                F("src/services/cf-worker/gulpfile.js"),
                F("src/services/cf-worker/minify-html.js"),
                F("src/services/cf-worker/workers/tracker-worker/public_src/land_backends/1110/index.js")
            ],
            SubProjects =
            [
                new ProjectMapSubProject(
                    "ws:p:src/services/cf-worker/workers/tracker-worker:",
                    "tracker-worker",
                    "src/services/cf-worker/workers/tracker-worker",
                    "Worker",
                    [
                        F("src/services/cf-worker/workers/tracker-worker/workers-site/index.js"),
                        F("src/services/cf-worker/workers/tracker-worker/workers-site/src/save-impression.js")
                    ]),
                new ProjectMapSubProject(
                    "ws:p:src/services/cf-worker/workers/queue-consumer-worker:",
                    "queue-consumer-worker",
                    "src/services/cf-worker/workers/queue-consumer-worker",
                    "Worker",
                    [
                        F("src/services/cf-worker/workers/queue-consumer-worker/workers-site/index.js")
                    ])
            ],
            SymbolEdges =
            [
                Call(
                    "src/services/cf-worker/workers/queue-consumer-worker/workers-site/index.js",
                    "src/services/cf-worker/workers/tracker-worker/workers-site/src/save-impression.js")
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Modules.Select(m => m.Name).Should().BeEquivalentTo("tracker-worker", "queue-consumer-worker");
        result.Modules.Should().OnlyContain(m => m.IsSubProject);
        result.Edges.Should().ContainSingle(e =>
            e.SourceId == "pm:sub:queue-consumer-worker" &&
            e.TargetId == "pm:sub:tracker-worker");
    }

    [Test]
    public void Build_ClassMap_AssignsEntryPointsAndBfsClassOrbitsAndResolvesInterfacesAndOuterResourceOrbits()
    {
        var input = new ProjectMapInput
        {
            ProjectId = "ws:p:orders-api:",
            ProjectName = "Orders.Api",
            ProjectPath = "src/Orders.Api",
            Files =
            [
                F("src/Orders.Api/Controllers/OrdersController.cs"),
                F("src/Orders.Api/Services/IOrderService.cs"),
                F("src/Orders.Api/Services/OrderService.Part1.cs"),
                F("src/Orders.Api/Services/OrderService.Part2.cs"),
                F("src/Orders.Api/Domain/OrderValidator.cs"),
                F("src/Orders.Api/Repositories/OrderRepository.cs"),
                F("src/Orders.Api/Utils/ClockHelper.cs"),
                F("src/Orders.Api/Models/OrderDto.cs")
            ],
            EntryPointNodes =
            [
                new ProjectMapEntryPointNode(
                    "ep:post-orders",
                    "POST /api/orders",
                    "Endpoint",
                    "src/Orders.Api/Controllers/OrdersController.cs",
                    15,
                    "HTTP",
                    ["OrdersController"],
                    ["src/Orders.Api/Controllers/OrdersController.cs"])
            ],
            Types =
            [
                new ProjectMapTypeNode("t:ctrl", "OrdersController", "class", "src/Orders.Api/Controllers/OrdersController.cs", 10, 45, false, ["CreateOrder"]),
                new ProjectMapTypeNode("t:isvc", "IOrderService", "interface", "src/Orders.Api/Services/IOrderService.cs", 5, 12, false, ["PlaceOrderAsync"]),
                new ProjectMapTypeNode("t:svc1", "OrderService", "class", "src/Orders.Api/Services/OrderService.Part1.cs", 8, 60, false, ["PlaceOrderAsync"]),
                new ProjectMapTypeNode("t:svc2", "OrderService", "class", "src/Orders.Api/Services/OrderService.Part2.cs", 8, 40, false, ["NotifyOrderCreatedAsync"]),
                new ProjectMapTypeNode("t:val", "OrderValidator", "class", "src/Orders.Api/Domain/OrderValidator.cs", 6, 30, false, ["Validate"]),
                new ProjectMapTypeNode("t:repo", "OrderRepository", "class", "src/Orders.Api/Repositories/OrderRepository.cs", 9, 50, false, ["SaveAsync"]),
                new ProjectMapTypeNode("t:clk", "ClockHelper", "class", "src/Orders.Api/Utils/ClockHelper.cs", 4, 20, false, ["UtcNow"]),
                new ProjectMapTypeNode("t:dto", "OrderDto", "record", "src/Orders.Api/Models/OrderDto.cs", 3, 10, false, [])
            ],
            TypeEdges =
            [
                new ProjectMapTypeEdge("OrderService", "src/Orders.Api/Services/OrderService.Part1.cs", "IOrderService", "src/Orders.Api/Services/IOrderService.cs", "IMPLEMENTS", 8),
                new ProjectMapTypeEdge("OrdersController", "src/Orders.Api/Controllers/OrdersController.cs", "IOrderService", "src/Orders.Api/Services/IOrderService.cs", "CALLS", 20),
                new ProjectMapTypeEdge("OrderService", "src/Orders.Api/Services/OrderService.Part1.cs", "OrderValidator", "src/Orders.Api/Domain/OrderValidator.cs", "CALLS", 30),
                new ProjectMapTypeEdge("OrderValidator", "src/Orders.Api/Domain/OrderValidator.cs", "OrderRepository", "src/Orders.Api/Repositories/OrderRepository.cs", "CALLS", 18),
                new ProjectMapTypeEdge("OrderRepository", "src/Orders.Api/Repositories/OrderRepository.cs", "ClockHelper", "src/Orders.Api/Utils/ClockHelper.cs", "CALLS", 25)
            ],
            ResourceLinks =
            [
                new ProjectMapResourceLink("src/Orders.Api/Services/OrderService.Part2.cs", "ws:top:mediatr:OrderCreated", "Topic", "OrderCreated", "PUBLISHES", "OrderService", 22),
                new ProjectMapResourceLink("src/Orders.Api/Services/OrderService.Part1.cs", "ws:p:Orders.Shared:", "Library", "Orders.Shared", "CALLS", "OrderService", 14),
                new ProjectMapResourceLink("src/Orders.Api/Repositories/OrderRepository.cs", "ws:db:orders_db", "Database", "orders_db", "USES_DB", "OrderRepository", 33),
                new ProjectMapResourceLink("src/Orders.Api/Repositories/OrderRepository.cs", "ws:tbl:orders", "Table", "orders", "WRITES_TO", "OrderRepository", 35)
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        result.Granularity.Should().Be(ProjectMapGranularity.Class);

        // Pure DTO without behavior and interface should be excluded
        result.Modules.Select(m => m.Name).Should().NotContain(["IOrderService", "OrderDto"]);

        // Partial class OrderService merged into a single node with 2 files and 100 lines
        var orderService = result.Modules.Single(m => m.Name == "OrderService");
        orderService.Files.Should().HaveCount(2);
        orderService.LineCount.Should().Be(100);
        orderService.ImplementedInterfaces.Should().Contain("IOrderService");

        // Orbit assignment:
        // Orbit 0 (Ingress): POST /api/orders
        // Orbit 1 (Application): OrdersController (BFS Depth 1)
        // Orbit 2 (Core): OrderService (BFS Depth 2, resolved through IOrderService!)
        // Orbit 3 (Data): OrderValidator (BFS Depth 3)
        // Orbit 4 (Foundation): OrderRepository (BFS Depth 4) & ClockHelper (BFS Depth 5 -> clamped to Tier 4)
        result.Modules.Single(m => m.Name == "POST /api/orders").Tier.Should().Be(ProjectMapTier.Ingress);
        result.Modules.Single(m => m.Name == "OrdersController").Tier.Should().Be(ProjectMapTier.Application);
        orderService.Tier.Should().Be(ProjectMapTier.Core);
        result.Modules.Single(m => m.Name == "OrderValidator").Tier.Should().Be(ProjectMapTier.Data);
        result.Modules.Single(m => m.Name == "OrderRepository").Tier.Should().Be(ProjectMapTier.Foundation);
        result.Modules.Single(m => m.Name == "ClockHelper").Tier.Should().Be(ProjectMapTier.Foundation);

        // Outer Resource Orbits:
        // Orbit 5 (Resource): Topic (OrderCreated) + Library (Orders.Shared)
        // Orbit 6 (Database): Database (orders_db) + Table (orders)
        result.Resources.Single(r => r.Name == "OrderCreated").Tier.Should().Be(ProjectMapTier.Resource);
        result.Resources.Single(r => r.Name == "Orders.Shared").Tier.Should().Be(ProjectMapTier.Resource);
        result.Resources.Single(r => r.Name == "orders_db").Tier.Should().Be(ProjectMapTier.Database);
        result.Resources.Single(r => r.Name == "orders").Tier.Should().Be(ProjectMapTier.Database);
    }

    [Test]
    public void Build_ClassMap_ExpressRoutersAndControllers_EvenlyDistributesEntryPointsAndConnectsLanes()
    {
        var input = new ProjectMapInput
        {
            ProjectId = "ws:p:redis-route:",
            ProjectName = "redis-route",
            ProjectPath = "services/redis-route",
            Files =
            [
                F("services/redis-route/src/routers/auth.router.ts"),
                F("services/redis-route/src/routers/campaign.router.ts"),
                F("services/redis-route/src/routers/messages.router.ts"),
                F("services/redis-route/src/controllers/auth.controller.ts"),
                F("services/redis-route/src/controllers/campaigns.controller.ts"),
                F("services/redis-route/src/controllers/messages.controller.ts"),
                F("services/redis-route/src/repos/campaigns.repo.ts")
            ],
            EntryPointNodes =
            [
                new ProjectMapEntryPointNode("ep:get-auth", "GET /auth/:network", "Endpoint", "services/redis-route/src/routers/auth.router.ts", 5, "HTTP", ["getAuthData"], ["services/redis-route/src/controllers/auth.controller.ts"]),
                new ProjectMapEntryPointNode("ep:post-auth", "POST /auth/:network", "Endpoint", "services/redis-route/src/routers/auth.router.ts", 6, "HTTP", ["updateAuthData"], ["services/redis-route/src/controllers/auth.controller.ts"]),
                new ProjectMapEntryPointNode("ep:post-camp-1", "POST /campaigns/create", "Endpoint", "services/redis-route/src/routers/campaign.router.ts", 5, "HTTP", ["updateCampaignCreateQueues"], ["services/redis-route/src/controllers/campaigns.controller.ts"]),
                new ProjectMapEntryPointNode("ep:post-camp-2", "POST /campaigns/delete", "Endpoint", "services/redis-route/src/routers/campaign.router.ts", 6, "HTTP", ["updateCampaignDeleteQueues"], ["services/redis-route/src/controllers/campaigns.controller.ts"]),
                new ProjectMapEntryPointNode("ep:post-msg", "POST /messages/invalid", "Endpoint", "services/redis-route/src/routers/messages.router.ts", 5, "HTTP", ["addInvalidMessage"], ["services/redis-route/src/controllers/messages.controller.ts"])
            ],
            Functions =
            [
                new ProjectMapFunctionNode("fn:getAuthData", "getAuthData", "services/redis-route/src/controllers/auth.controller.ts", 5, 20, false),
                new ProjectMapFunctionNode("fn:updateAuthData", "updateAuthData", "services/redis-route/src/controllers/auth.controller.ts", 26, 20, false),
                new ProjectMapFunctionNode("fn:campCreate", "updateCampaignCreateQueues", "services/redis-route/src/controllers/campaigns.controller.ts", 10, 30, false),
                new ProjectMapFunctionNode("fn:campDelete", "updateCampaignDeleteQueues", "services/redis-route/src/controllers/campaigns.controller.ts", 41, 30, false),
                new ProjectMapFunctionNode("fn:addMsg", "addInvalidMessage", "services/redis-route/src/controllers/messages.controller.ts", 8, 25, false),
                new ProjectMapFunctionNode("fn:repoSave", "saveCampaign", "services/redis-route/src/repos/campaigns.repo.ts", 12, 35, false)
            ],
            SymbolEdges =
            [
                new ProjectMapSymbolEdge("services/redis-route/src/routers/auth.router.ts", "services/redis-route/src/controllers/auth.controller.ts", "CALLS", "router", "getAuthData", 5),
                new ProjectMapSymbolEdge("services/redis-route/src/routers/campaign.router.ts", "services/redis-route/src/controllers/campaigns.controller.ts", "CALLS", "router", "updateCampaignCreateQueues", 5),
                new ProjectMapSymbolEdge("services/redis-route/src/routers/messages.router.ts", "services/redis-route/src/controllers/messages.controller.ts", "CALLS", "router", "addInvalidMessage", 5),
                new ProjectMapSymbolEdge("services/redis-route/src/controllers/campaigns.controller.ts", "services/redis-route/src/repos/campaigns.repo.ts", "CALLS", "updateCampaignCreateQueues", "saveCampaign", 15)
            ]
        };

        var result = ProjectMapBuilder.Build(input);

        // Verify consistent module naming for router files: all routers have 'routers/' prefix
        var routerAuth = result.Modules.Single(m => m.Name == "routers/auth");
        var routerCampaign = result.Modules.Single(m => m.Name == "routers/campaign");
        var routerMessages = result.Modules.Single(m => m.Name == "routers/messages");

        // Verify POST-only routers are NOT starved and have incoming edges from Endpoints
        var campInEdges = result.Edges.Where(e => e.TargetId == routerCampaign.Id).ToList();
        campInEdges.Should().NotBeEmpty("POST-only campaign router must have incoming endpoint edge");

        var msgInEdges = result.Edges.Where(e => e.TargetId == routerMessages.Id).ToList();
        msgInEdges.Should().NotBeEmpty("POST-only messages router must have incoming endpoint edge");

        // Verify Tier / Swimlane placement:
        // Routers = Application Handlers (Lane 1)
        routerAuth.Tier.Should().Be(ProjectMapTier.Application);
        routerCampaign.Tier.Should().Be(ProjectMapTier.Application);
        routerMessages.Tier.Should().Be(ProjectMapTier.Application);

        // Controllers = Domain Core (Lane 2, called from routers)
        var ctrlAuth = result.Modules.Single(m => m.Name == "auth");
        var ctrlCamp = result.Modules.Single(m => m.Name == "campaigns");
        var ctrlMsg = result.Modules.Single(m => m.Name == "messages");
        ctrlAuth.Tier.Should().Be(ProjectMapTier.Core);
        ctrlCamp.Tier.Should().Be(ProjectMapTier.Core);
        ctrlMsg.Tier.Should().Be(ProjectMapTier.Core);

        // Repos = Repositories & Foundation (Lane 3)
        var repo = result.Modules.Single(m => m.Path.Contains("campaigns.repo.ts"));
        repo.Tier.Should().Be(ProjectMapTier.Data);

        // Verify edges exist from routers to controllers
        result.Edges.Should().Contain(e => e.SourceId == routerAuth.Id && e.TargetId == ctrlAuth.Id);
        result.Edges.Should().Contain(e => e.SourceId == routerCampaign.Id && e.TargetId == ctrlCamp.Id);
        result.Edges.Should().Contain(e => e.SourceId == routerMessages.Id && e.TargetId == ctrlMsg.Id);

        // Verify edge from controller to repo
        result.Edges.Should().Contain(e => e.SourceId == ctrlCamp.Id && e.TargetId == repo.Id);
    }
}
