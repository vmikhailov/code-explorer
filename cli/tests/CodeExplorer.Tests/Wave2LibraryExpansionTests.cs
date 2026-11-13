using System.Collections.Frozen;
using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Kotlin;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class Wave2LibraryExpansionTests
{
    private IDisposable? _lock;

    [SetUp]
    public async Task SetUp()
    {
        _lock = await TestRegistryLock.AcquireAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _lock?.Dispose();
        _lock = null;
    }

    private static async Task<(FileNode FileNode, List<Reference> References)> ParseFileAsync(IFileParser parser, string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wave2_lib_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            return (syntaxTree.FileNode, FindAllReferences(syntaxTree.FileNode));
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore temp cleanup error
            }
        }
    }

    private static List<Reference> FindAllReferences(FileNode fileNode)
    {
        var result = new List<Reference>(fileNode.References);
        result.AddRange(FindReferences(fileNode.Children));
        return result;
    }

    private static List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            result.AddRange(node.References);
            result.AddRange(FindReferences(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Wave2_Phase1_Messaging_And_Orchestration_Emit_Broker_References()
    {
        const string csCode = """
            using NServiceBus;
            using Dapr.Client;

            public class OrderPlacedHandler : IHandleMessages<OrderPlaced>
            {
                public async Task Handle(OrderPlaced message, DaprClient dapr)
                {
                    await dapr.PublishEventAsync("orders.confirmed", message);
                }
            }
            """;
        var (_, csRefs) = await ParseFileAsync(new CSharpFileParser(), csCode, "OrderPlacedHandler.cs");
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.SubscribesTo && r.TargetName.Contains("OrderPlaced")), Is.True);
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName.Contains("orders.confirmed")), Is.True);

        const string javaCode = """
            import io.vertx.core.Vertx;
            import io.vertx.ext.web.Router;

            public class OrderVerticle {
                public void start(Vertx vertx, Router router) {
                    router.get("/api/vertx/orders");
                    vertx.eventBus().publish("orders.created", "payload");
                }
            }
            """;
        var (_, javaRefs) = await ParseFileAsync(new JavaFileParser(), javaCode, "OrderVerticle.java");
        Assert.That(javaRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName.Contains("/api/vertx/orders")), Is.True);
        Assert.That(javaRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName.Contains("orders.created")), Is.True);

        const string pyCode = """
            from faststream import FastStream

            @broker.subscriber("payments.in")
            async def handle_payment(msg):
                await broker.publish(msg, "payments.out")
            """;
        var (_, pyRefs) = await ParseFileAsync(new PythonFileParser(), pyCode, "stream_app.py");
        Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.SubscribesTo && r.TargetName == "payments.in"), Is.True);
        Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "payments.out"), Is.True);

        const string goCode = """
            package main
            import "github.com/dapr/go-sdk/client"

            func publishOrder(c client.Client) {
                c.PublishEvent(ctx, "pubsub", "orders.shipped", data)
                c.SaveState(ctx, "statestore", "key", data, nil)
            }
            """;
        var (_, goRefs) = await ParseFileAsync(new GoFileParser(), goCode, "dapr_app.go");
        Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "orders.shipped"), Is.True);
        Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "dapr_state_store"), Is.True);
    }

    [Test]
    public async Task Wave2_Phase2_Web_Routers_Gateways_And_MetaFrameworks_Emit_Endpoints()
    {
        const string csCode = """
            using FastEndpoints;
            using Carter;

            public class CreateInvoiceEndpoint : EndpointWithoutRequest
            {
                public override void Configure()
                {
                    Post("/api/invoices/create");
                }
                public void AddRoutes(IEndpointRouteBuilder app)
                {
                    app.MapGet("/api/invoices/list", () => {});
                }
            }
            """;
        var (_, csRefs) = await ParseFileAsync(new CSharpFileParser(), csCode, "CreateInvoiceEndpoint.cs");
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "POST:/api/invoices/create"), Is.True);
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "GET:/api/invoices/list"), Is.True);

        const string ktCode = """
            import org.http4k.core.Method.GET
            import org.http4k.routing.bind
            import org.http4k.routing.routes
            import io.grpc.kotlin.AbstractCoroutineServerImpl

            class BillingService : BillingGrpcKt.BillingCoroutineImplBase() {
                val app = routes("/api/http4k/ping" bind GET to { Response(OK) })
            }
            """;
        var (_, ktRefs) = await ParseFileAsync(new KotlinFileParser(), ktCode, "BillingService.kt");
        Assert.That(ktRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "GET:/api/http4k/ping"), Is.True);
        Assert.That(ktRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "GRPC:/Billing"), Is.True);

        const string tsCode = """
            import { Hono } from '@cloudflare/workers-types';
            import useSWR from 'swr';

            export default {
                async fetch(req, env) {
                    await env.DB.prepare("SELECT * FROM users");
                    await env.QUEUE.send({ id: 1 });
                }
            };

            export function useProfile() {
                return useSWR('/api/v1/profile');
            }
            """;
        var (_, tsRefs) = await ParseFileAsync(new TypeScriptFileParser(), tsCode, "worker.ts");
        Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "cloudflare_d1"), Is.True);
        Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "cloudflare-worker-queue"), Is.True);
        Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "/api/v1/profile"), Is.True);

        const string goCode = """
            package main
            import "github.com/gorilla/mux"

            func setupRouter(r *mux.Router) {
                r.HandleFunc("/api/v2/customers", handleCustomers)
            }
            """;
        var (_, goRefs) = await ParseFileAsync(new GoFileParser(), goCode, "router.go");
        Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "ALL:/api/v2/customers"), Is.True);
    }

    [Test]
    public async Task Wave2_Phase3_Databases_BaaS_And_VectorStores_Emit_Table_References()
    {
        const string csCode = """
            using Azure.Messaging.ServiceBus;
            using Microsoft.Azure.Cosmos;

            public class CosmosRepo
            {
                public void Run(ServiceBusClient sb, CosmosClient cosmos)
                {
                    sb.CreateSender("billing-events");
                    cosmos.GetContainer("BillingDb", "Invoices");
                }
            }
            """;
        var (_, csRefs) = await ParseFileAsync(new CSharpFileParser(), csCode, "CosmosRepo.cs");
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName.Contains("billing-events")), Is.True);
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "Invoices"), Is.True);

        const string javaCode = """
            import org.jooq.DSLContext;
            import org.redisson.api.RedissonClient;

            public class LedgerDao {
                public void query(DSLContext dsl, RedissonClient redisson) {
                    dsl.selectFrom("ledger_entries");
                    redisson.getMap("session_cache");
                }
            }
            """;
        var (_, javaRefs) = await ParseFileAsync(new JavaFileParser(), javaCode, "LedgerDao.java");
        Assert.That(javaRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "ledger_entries"), Is.True);
        Assert.That(javaRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "session_cache"), Is.True);

        const string tsCode = """
            import { createClient } from '@supabase/supabase-js';

            export async function loadSubscriptions(supabase) {
                return await supabase.from('subscriptions').select('*');
            }
            """;
        var (_, tsRefs) = await ParseFileAsync(new TypeScriptFileParser(), tsCode, "supabaseRepo.ts");
        Assert.That(tsRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "subscriptions"), Is.True);

        const string pyCode = """
            from qdrant_client import QdrantClient

            def search_embeddings(client: QdrantClient):
                client.search("document_embeddings", [0.1, 0.2])
            """;
        var (_, pyRefs) = await ParseFileAsync(new PythonFileParser(), pyCode, "vector_repo.py");
        Assert.That(pyRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "document_embeddings"), Is.True);
    }

    [Test]
    public async Task Wave2_Phase4_AiAgents_DiContainers_And_Observability_Emit_Semantic_References()
    {
        const string csCode = """
            using Microsoft.SemanticKernel;

            public class OrderAgent
            {
                public async Task AskAsync(Kernel kernel)
                {
                    await kernel.InvokePromptAsync("Summarize order");
                }
            }
            """;
        var (_, csRefs) = await ParseFileAsync(new CSharpFileParser(), csCode, "OrderAgent.cs");
        Assert.That(csRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "llm:semantic-kernel"), Is.True);

        const string ktCode = """
            import dagger.Binds
            import io.mockk.mockk

            abstract class AppModule {
                @Binds
                abstract fun bindRepo(impl: SqlUserRepository): UserRepository

                val mockSvc = mockk<PaymentGateway>()
            }
            """;
        var (_, ktRefs) = await ParseFileAsync(new KotlinFileParser(), ktCode, "AppModule.kt");
        Assert.That(ktRefs.Any(r => r.Kind == RelationshipKind.DependsOn && r.TargetName == "SqlUserRepository"), Is.True);
        Assert.That(ktRefs.Any(r => r.Kind == RelationshipKind.Calls && r.TargetName == "PaymentGateway"), Is.True);

        const string goCode = """
            package main
            import (
                "go.uber.org/fx"
                "github.com/casbin/casbin/v2"
                "github.com/prometheus/client_golang/prometheus/promhttp"
            )

            func boot(e *casbin.Enforcer) {
                fx.Provide(NewOrderService)
                e.Enforce("alice", "orders", "read")
                promhttp.Handler()
            }
            """;
        var (_, goRefs) = await ParseFileAsync(new GoFileParser(), goCode, "boot.go");
        Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.DependsOn && r.TargetName == "NewOrderService"), Is.True);
        Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "casbin_rule"), Is.True);
        Assert.That(goRefs.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "GET:/metrics"), Is.True);
    }

    [Test]
    public async Task Benchmark_Imperative_Vs_Declarative_Vs_Fused_Extensions()
    {
        const string csWorkload = """
            using FastEndpoints;
            using Carter;
            using Dapr.Client;
            using Azure.Messaging.ServiceBus;
            using Microsoft.Azure.Cosmos;
            using Microsoft.SemanticKernel;

            public class EnterpriseOrderService : EndpointWithoutRequest, IHandleMessages<OrderPlaced>
            {
                public override void Configure()
                {
                    Post("/api/orders/create");
                    Get("/api/orders/status");
                }

                public void AddRoutes(IEndpointRouteBuilder app)
                {
                    app.MapGet("/api/orders/list", () => {});
                    app.MapPost("/api/orders/archive", () => {});
                    app.MapReverseProxy();
                }

                public async Task ProcessOrderAsync(
                    OrderPlaced msg,
                    DaprClient dapr,
                    ServiceBusClient sb,
                    CosmosClient cosmos,
                    Kernel kernel,
                    IBus rebus)
                {
                    await dapr.PublishEventAsync("orders.confirmed", msg);
                    sb.CreateSender("billing-events");
                    cosmos.GetContainer("BillingDb", "Invoices");
                    await kernel.InvokePromptAsync("Summarize order risk");
                    await rebus.Publish("orders.audit");
                }
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "wave2_bench_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "EnterpriseOrderService.cs");
        await File.WriteAllTextAsync(filePath, csWorkload);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new CSharpFileParser();

            using var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var allNodes = syntaxTree.RootNode!.Descendants().ToList();
            TestContext.Out.WriteLine($"AST Nodes per file: {allNodes.Count}");

            var declarative10 = new DeclarativeSemanticExtension[]
            {
                new CodeExplorer.Parser.CSharp.Libraries.FastEndpointsLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.CarterLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.DaprDotNetLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.RebusLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.NServiceBusLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.YarpReverseProxyLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.AzureSdkDotNetLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.SemanticKernelAiDotNetLibraryParser(),
                new CodeExplorer.Parser.CSharp.Libraries.Linq2DbNHibernateLibraryParser(),
                new CodeExplorer.Parser.Java.Libraries.VertxLibraryParser()
            };

            var fusedDeclarative = new DeclarativeSemanticExtension
            {
                Id = "fused-wave2",
                Name = "Fused Wave 2 Rules",
                InboundRouteCallRules = declarative10.SelectMany(d => d.InboundRouteCallRules).ToList(),
                DataStoreAccessRules = declarative10.SelectMany(d => d.DataStoreAccessRules).ToList(),
                ServiceCallSinkRules = declarative10.SelectMany(d => d.ServiceCallSinkRules).ToList(),
                MessagingBrokerRules = declarative10.SelectMany(d => d.MessagingBrokerRules).ToList(),
                OrmTableRules = declarative10.SelectMany(d => d.OrmTableRules).ToList()
            };

            var mergedSingleRule = new DeclarativeSemanticExtension
            {
                Id = "merged-single-dispatch",
                Name = "Merged Single Dispatch",
                InboundRouteCallRules =
                [
                    new InboundRouteCallRule
                    {
                        MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["Get"] = "GET", ["Post"] = "POST", ["Put"] = "PUT", ["Delete"] = "DELETE", ["Patch"] = "PATCH",
                            ["MapGet"] = "GET", ["MapPost"] = "POST", ["MapPut"] = "PUT", ["MapDelete"] = "DELETE", ["MapPatch"] = "PATCH",
                            ["get"] = "GET", ["post"] = "POST", ["put"] = "PUT", ["delete"] = "DELETE", ["route"] = "ALL"
                        }.ToFrozenDictionary(StringComparer.Ordinal)
                    }
                ],
                DataStoreAccessRules =
                [
                    new DataStoreAccessRule
                    {
                        CallMethodNames = new[] { "GetContainer" }.ToFrozenSet(StringComparer.Ordinal),
                        TableArgumentIndex = 1
                    }
                ],
                ServiceCallSinkRules =
                [
                    new ServiceCallSinkRule
                    {
                        CallMethodNames = new[] { "MapReverseProxy", "AddReverseProxy", "InvokePromptAsync", "InvokeAsync", "GetBlobClient" }.ToFrozenSet(StringComparer.Ordinal),
                        TargetRelation = OntologyConstants.Relationships.Calls,
                        StaticTargetName = "service-sink"
                    }
                ],
                MessagingBrokerRules =
                [
                    new MessagingBrokerRule
                    {
                        Protocol = "broker",
                        ConsumerInterfaceSubstrings = new[] { "IHandleMessages" }.ToFrozenSet(StringComparer.Ordinal),
                        PublishMethodNames = new[] { "PublishEventAsync", "Send", "Publish", "CreateSender" }.ToFrozenSet(StringComparer.Ordinal),
                        SubscribeMethodNames = new[] { "Subscribe", "CreateProcessor" }.ToFrozenSet(StringComparer.Ordinal)
                    }
                ]
            };

            var imperative10 = Enumerable.Range(0, 10)
                .Select(i => (ISemanticExtension)new ImperativeBenchExtension(i))
                .ToArray();

            const int warmupIterations = 200;
            const int measuredIterations = 3000;
            var refs = new List<Reference>(32);

            // Warmup
            for (var i = 0; i < warmupIterations; i++)
            {
                refs.Clear();
                foreach (var n in allNodes)
                {
                    foreach (var ext in imperative10) ext.CollectReferences(n, "Scope", refs, ctx);
                    foreach (var ext in declarative10) ext.CollectReferences(n, "Scope", refs, ctx);
                    fusedDeclarative.CollectReferences(n, "Scope", refs, ctx);
                    mergedSingleRule.CollectReferences(n, "Scope", refs, ctx);
                }
            }

            // 1. Measure 10 Imperative Extensions
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocBeforeImp = GC.GetAllocatedBytesForCurrentThread();
            var swImp = System.Diagnostics.Stopwatch.StartNew();
            var impRefCount = 0;
            for (var i = 0; i < measuredIterations; i++)
            {
                refs.Clear();
                foreach (var n in allNodes)
                {
                    for (var j = 0; j < imperative10.Length; j++)
                    {
                        imperative10[j].CollectReferences(n, "Scope", refs, ctx);
                    }
                }
                impRefCount = refs.Count;
            }
            swImp.Stop();
            var allocImp = GC.GetAllocatedBytesForCurrentThread() - allocBeforeImp;

            // 2. Measure 10 Separate Declarative Extensions
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocBeforeDec10 = GC.GetAllocatedBytesForCurrentThread();
            var swDec10 = System.Diagnostics.Stopwatch.StartNew();
            var dec10RefCount = 0;
            for (var i = 0; i < measuredIterations; i++)
            {
                refs.Clear();
                foreach (var n in allNodes)
                {
                    for (var j = 0; j < declarative10.Length; j++)
                    {
                        declarative10[j].CollectReferences(n, "Scope", refs, ctx);
                    }
                }
                dec10RefCount = refs.Count;
            }
            swDec10.Stop();
            var allocDec10 = GC.GetAllocatedBytesForCurrentThread() - allocBeforeDec10;

            // 3. Measure 1 Fused Declarative Extension (10 rule sets in 1 extension)
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocBeforeFused = GC.GetAllocatedBytesForCurrentThread();
            var swFused = System.Diagnostics.Stopwatch.StartNew();
            var fusedRefCount = 0;
            for (var i = 0; i < measuredIterations; i++)
            {
                refs.Clear();
                foreach (var n in allNodes)
                {
                    fusedDeclarative.CollectReferences(n, "Scope", refs, ctx);
                }
                fusedRefCount = refs.Count;
            }
            swFused.Stop();
            var allocFused = GC.GetAllocatedBytesForCurrentThread() - allocBeforeFused;

            // 4. Measure 1 Merged Single-Lookup Declarative Extension
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocBeforeMerged = GC.GetAllocatedBytesForCurrentThread();
            var swMerged = System.Diagnostics.Stopwatch.StartNew();
            var mergedRefCount = 0;
            for (var i = 0; i < measuredIterations; i++)
            {
                refs.Clear();
                foreach (var n in allNodes)
                {
                    mergedSingleRule.CollectReferences(n, "Scope", refs, ctx);
                }
                mergedRefCount = refs.Count;
            }
            swMerged.Stop();
            var allocMerged = GC.GetAllocatedBytesForCurrentThread() - allocBeforeMerged;

            var totalNodeVisits = (long)measuredIterations * allNodes.Count;
            TestContext.Out.WriteLine($"=== BENCHMARK RESULTS ({measuredIterations} files / {totalNodeVisits:N0} AST node visits) ===");
            TestContext.Out.WriteLine($"1) 10 Imperative Extensions       : {swImp.Elapsed.TotalMilliseconds,8:F2} ms | {swImp.Elapsed.TotalNanoseconds / totalNodeVisits,6:F1} ns/node | Alloc: {allocImp / 1024.0,8:F1} KB | Refs/file: {impRefCount}");
            TestContext.Out.WriteLine($"2) 10 Declarative Extensions      : {swDec10.Elapsed.TotalMilliseconds,8:F2} ms | {swDec10.Elapsed.TotalNanoseconds / totalNodeVisits,6:F1} ns/node | Alloc: {allocDec10 / 1024.0,8:F1} KB | Refs/file: {dec10RefCount}");
            TestContext.Out.WriteLine($"3) 1 Fused Declarative (10 lists) : {swFused.Elapsed.TotalMilliseconds,8:F2} ms | {swFused.Elapsed.TotalNanoseconds / totalNodeVisits,6:F1} ns/node | Alloc: {allocFused / 1024.0,8:F1} KB | Refs/file: {fusedRefCount}");
            TestContext.Out.WriteLine($"4) 1 Merged Lookup Table (O(1))   : {swMerged.Elapsed.TotalMilliseconds,8:F2} ms | {swMerged.Elapsed.TotalNanoseconds / totalNodeVisits,6:F1} ns/node | Alloc: {allocMerged / 1024.0,8:F1} KB | Refs/file: {mergedRefCount}");

            Assert.That(dec10RefCount, Is.EqualTo(12));
            Assert.That(fusedRefCount, Is.EqualTo(12));
            Assert.That(mergedRefCount, Is.EqualTo(11));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    private sealed class ImperativeBenchExtension(int index) : ISemanticExtension
    {
        private static readonly char[] Quotes = ['"', '\'', '`'];
        public string Name => $"Imp{index}";
        public string Id => $"imp-{index}";
        public SemanticExtensionType Type => SemanticExtensionType.Framework;
        public IReadOnlyList<string> SupportedPatterns => ["*"];
        public bool IsImplemented => true;
        public LibraryRole LibraryRole => LibraryRole.WebService;
        public string? MapNodeType(CodeExplorer.Core.Parser.Syntax.SyntaxNode node, ParsingContext ctx) => null;
        public string? ExtractIdentifier(CodeExplorer.Core.Parser.Syntax.SyntaxNode node, ParsingContext ctx) => null;

        public void CollectReferences(CodeExplorer.Core.Parser.Syntax.SyntaxNode node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
        {
            if (node is CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax { Kind: CodeExplorer.Core.Parser.Syntax.TypeSyntaxKind.Class } && index == 0)
            {
                var span = node.AsSpan();
                var brace = span.IndexOf('{');
                if (brace > 0) span = span[..brace];
                if (span.Contains("IHandleMessages<OrderPlaced>".AsSpan(), StringComparison.Ordinal))
                {
                    references.Add(new Reference(scopeSymbolId, "nservicebus:OrderPlaced", OntologyConstants.Relationships.SubscribesTo));
                }
                return;
            }

            if (node is not CodeExplorer.Core.Parser.Syntax.CallExprSyntax { CallKind: CodeExplorer.Core.Parser.Syntax.CallSyntaxKind.Method or CodeExplorer.Core.Parser.Syntax.CallSyntaxKind.Function } callExpr)
                return;

            var method = callExpr.MethodNameSpan;
            if (method.IsEmpty) return;

            ReadOnlySpan<char> firstLiteral = default;
            if (callExpr.Arguments.Count > 0)
            {
                for (var i = 0; i < callExpr.Arguments.Count; i++)
                {
                    if (callExpr.Arguments[i].Value is CodeExplorer.Core.Parser.Syntax.LiteralExprSyntax { LiteralKind: CodeExplorer.Core.Parser.Syntax.LiteralSyntaxKind.String } lit)
                    {
                        firstLiteral = lit.AsSpan().Trim(Quotes);
                        break;
                    }
                }
            }

            if (index == 1 && (method.SequenceEqual("Get") || method.SequenceEqual("Post")) && !firstLiteral.IsEmpty)
                references.Add(new Reference(scopeSymbolId, $"{method.ToString().ToUpperInvariant()}:{firstLiteral}", OntologyConstants.Relationships.Exposes));
            else if (index == 2 && (method.SequenceEqual("MapGet") || method.SequenceEqual("MapPost")) && !firstLiteral.IsEmpty)
                references.Add(new Reference(scopeSymbolId, $"{method[3..].ToString().ToUpperInvariant()}:{firstLiteral}", OntologyConstants.Relationships.Exposes));
            else if (index == 3 && method.SequenceEqual("PublishEventAsync") && !firstLiteral.IsEmpty)
                references.Add(new Reference(scopeSymbolId, $"dapr:{firstLiteral}", OntologyConstants.Relationships.PublishesTo));
            else if (index == 4 && method.SequenceEqual("Publish") && !firstLiteral.IsEmpty)
                references.Add(new Reference(scopeSymbolId, $"rebus:{firstLiteral}", OntologyConstants.Relationships.PublishesTo));
            else if (index == 5 && method.SequenceEqual("MapReverseProxy"))
                references.Add(new Reference(scopeSymbolId, "yarp:reverse-proxy", OntologyConstants.Relationships.Calls));
            else if (index == 6 && (method.SequenceEqual("CreateSender") || method.SequenceEqual("GetContainer")) && !firstLiteral.IsEmpty)
                references.Add(new Reference(scopeSymbolId, firstLiteral.ToString(), method.SequenceEqual("CreateSender") ? OntologyConstants.Relationships.PublishesTo : OntologyConstants.Relationships.UsesDb));
            else if (index == 7 && method.SequenceEqual("InvokePromptAsync"))
                references.Add(new Reference(scopeSymbolId, "llm:semantic-kernel", OntologyConstants.Relationships.Calls));
        }
    }
}
