using System.Collections.Frozen;
using System.Diagnostics;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Parsers.CSharp;
using CodeExplorer.Parsers.Go;
using CodeExplorer.Parsers.Java;
using CodeExplorer.Parsers.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class DeclarativeCallChainCombinatorTests
{
    private static void WalkAndCollect(
        SyntaxNode node,
        ISemanticExtension ext,
        string scopeId,
        List<Reference> refs,
        ParsingContext ctx)
    {
        ext.CollectReferences(node, scopeId, refs, ctx);
        for (var i = 0; i < node.Children.Count; i++)
        {
            WalkAndCollect(node.Children[i], ext, scopeId, refs, ctx);
        }
    }

    [Test]
    public void SuperpowerCallChain_FluentDataStore_DistinguishesReadsAndWrites_AcrossInlineAndVariableSplitChains()
    {
        var ext = new DeclarativeSemanticExtension
        {
            Id = "test-fluent-db",
            Name = "Test Fluent DB",
            DataStoreAccessRules =
            [
                new DataStoreAccessRule
                {
                    CallMethodNames = new[] { "table", "from", "collection" }.ToFrozenSet(StringComparer.Ordinal),
                    TargetSelectorMethods = new[] { "table", "from", "collection" }.ToFrozenSet(StringComparer.Ordinal),
                    ReadMethodNames = new[] { "select", "first", "findOne", "find" }.ToFrozenSet(StringComparer.Ordinal),
                    WriteMethodNames = new[] { "update", "insert", "delete", "insertOne" }.ToFrozenSet(StringComparer.Ordinal),
                    ReceiverSubstrings = new[] { "db", "knex" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
                }
            ]
        };

        const string tsCode = """
            export class OrderRepository {
                async processOrder(db: any, orderId: string) {
                    // 1. Multi-hop inline fluent write chain with intermediate filters
                    await db.table("orders").where("id", orderId).andWhere("status", "open").timeout(500).update({ status: "paid" });

                    // 2. Multi-hop inline fluent read chain
                    const customer = await db.from("customers").where("active", true).orderBy("created_at").select("id", "email");

                    // 3. Variable-split two-step fluent chain (receiver on delete() is 'staleQuery', not 'db')
                    const staleQuery = db.table("invoices").where("overdue", true);
                    await staleQuery.delete();

                    // 4. Variable-split collection handle
                    const auditCol = db.collection("audit_events");
                    await auditCol.insertOne({ orderId });
                }
            }
            """;

        var parser = new TypeScriptFileParser();
        var doc = parser.ParseDocument(tsCode, "order.repo.ts", "order.repo.ts")!;
        var refs = new List<Reference>();
        var ctx = new ParsingContext("test", ".");

        WalkAndCollect(doc.RootNode, ext, "OrderRepository.processOrder", refs, ctx);

        refs.Should().Contain(r => r.TargetName == "orders" && r.Kind == RelationshipKind.WritesTo);
        refs.Should().Contain(r => r.TargetName == "customers" && r.Kind == RelationshipKind.ReadsFrom);
        refs.Should().Contain(r => r.TargetName == "invoices" && r.Kind == RelationshipKind.WritesTo);
        refs.Should().Contain(r => r.TargetName == "audit_events" && r.Kind == RelationshipKind.WritesTo);
    }

    [Test]
    public void SuperpowerCallChain_GenericAndClassLiteralTargets_ExtractEntityNamesAndReadWriteSemantics()
    {
        var csExt = new DeclarativeSemanticExtension
        {
            Id = "test-cs-fluent-orm",
            Name = "Test C# Fluent ORM",
            DataStoreAccessRules =
            [
                new DataStoreAccessRule
                {
                    CallMethodNames = new[] { "Query" }.ToFrozenSet(StringComparer.Ordinal),
                    TargetSelectorMethods = new[] { "Query" }.ToFrozenSet(StringComparer.Ordinal),
                    ReadMethodNames = new[] { "ToListAsync", "FirstOrDefaultAsync" }.ToFrozenSet(StringComparer.Ordinal),
                    WriteMethodNames = new[] { "DeleteAsync", "UpdateAsync" }.ToFrozenSet(StringComparer.Ordinal),
                    ReceiverSubstrings = new[] { "session", "queryFactory" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
                }
            ],
            MessagingBrokerRules =
            [
                new MessagingBrokerRule
                {
                    Protocol = "masstransit",
                    PublishMethodNames = new[] { "Publish" }.ToFrozenSet(StringComparer.Ordinal),
                    PublishReceiverSubstrings = new[] { "bus" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
                }
            ]
        };

        const string csCode = """
            using System.Threading.Tasks;

            public class BillingService
            {
                public async Task RunAsync(dynamic session, dynamic queryFactory, dynamic bus, object evt)
                {
                    var entries = await session.Query<LedgerEntry>().Where(x => x.Amount > 0).ToListAsync();
                    await queryFactory.Query("shipments").Where("Status", "Cancelled").DeleteAsync();
                    await bus.Publish<InvoicePaidEvent>(evt);
                }
            }
            """;

        var csParser = new CSharpFileParser();
        var csDoc = csParser.ParseDocument(csCode, "BillingService.cs", "BillingService.cs")!;
        var csRefs = new List<Reference>();
        var ctx = new ParsingContext("test", ".");

        WalkAndCollect(csDoc.RootNode, csExt, "BillingService.RunAsync", csRefs, ctx);

        csRefs.Should().Contain(r => r.TargetName == "LedgerEntry" && r.Kind == RelationshipKind.ReadsFrom);
        csRefs.Should().Contain(r => r.TargetName == "shipments" && r.Kind == RelationshipKind.WritesTo);
        csRefs.Should().Contain(r => r.TargetName == "masstransit:InvoicePaidEvent" && r.Kind == RelationshipKind.PublishesTo);
    }

    [Test]
    public void SuperpowerCallChain_RouteGroups_CombinesChainedVariableAndNestedBlockPrefixes()
    {
        var routeExt = new DeclarativeSemanticExtension
        {
            Id = "test-route-chains",
            Name = "Test Route Chains",
            InboundRouteCallRules =
            [
                new InboundRouteCallRule
                {
                    MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["get"] = "GET", ["post"] = "POST", ["delete"] = "DELETE",
                        ["GET"] = "GET", ["POST"] = "POST", ["DELETE"] = "DELETE"
                    }.ToFrozenDictionary(StringComparer.Ordinal),
                    ReceiverSubstrings = new[] { "app", "router", "r" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                    GroupMethodNames = new[] { "group", "Group", "route" }.ToFrozenSet(StringComparer.Ordinal)
                }
            ]
        };

        // 1. TypeScript chained & nested callback route groups
        const string tsRoutes = """
            function registerRoutes(app: any) {
                // Direct fluent chain of route groups
                app.group("/api/v1").group("/orders").post("/cancel", cancelOrder);

                // Nested callback DSL route groups
                app.group("/api/v2", (r: any) => {
                    r.group("/users", (u: any) => {
                        u.delete("/purge", purgeUsers);
                    });
                });
            }
            """;

        var tsParser = new TypeScriptFileParser();
        var tsDoc = tsParser.ParseDocument(tsRoutes, "routes.ts", "routes.ts")!;
        var tsRefs = new List<Reference>();
        var ctx = new ParsingContext("test", ".");

        WalkAndCollect(tsDoc.RootNode, routeExt, "registerRoutes", tsRefs, ctx);

        tsRefs.Should().Contain(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "POST:/api/v1/orders/cancel");
        tsRefs.Should().Contain(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "DELETE:/api/v2/users/purge");

        // 2. Go variable-split multi-hop route groups (v1 := app.Group("/api/v1"); billing := v1.Group("/billing"); billing.POST("/charge", h))
        const string goRoutes = """
            package main

            func setupRouter(app *Engine) {
                v1 := app.Group("/api/v1")
                billing := v1.Group("/billing")
                billing.POST("/charge", chargeHandler)
            }
            """;

        var goParser = new GoFileParser();
        var goDoc = goParser.ParseDocument(goRoutes, "router.go", "router.go")!;
        var goRefs = new List<Reference>();

        WalkAndCollect(goDoc.RootNode, routeExt, "setupRouter", goRefs, ctx);

        goRefs.Should().Contain(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "POST:/api/v1/billing/charge");
    }

    [Test]
    public void SuperpowerCallChain_OutboundHttpAndMessagingBuilders_ExtractsUriAndTopicAcrossFluentSteps()
    {
        var javaExt = new DeclarativeSemanticExtension
        {
            Id = "test-java-fluent-http-msg",
            Name = "Test Java Fluent HTTP & Messaging",
            OutboundHttpRules =
            [
                new OutboundHttpRule
                {
                    CallMethodNames = new[] { "retrieve", "exchange" }.ToFrozenSet(StringComparer.Ordinal)
                }
            ],
            MessagingBrokerRules =
            [
                new MessagingBrokerRule
                {
                    Protocol = "pulsar",
                    PublishMethodNames = new[] { "send", "sendAsync", "publish" }.ToFrozenSet(StringComparer.Ordinal)
                }
            ]
        };

        const string javaCode = """
            package com.example;

            public class PaymentGateway {
                public void chargeAndNotify() {
                    webClient.post()
                        .uri("https://payments.internal/v1/charge")
                        .header("X-Api-Key", "secret")
                        .retrieve();

                    pulsarClient.newMessage()
                        .topic("billing.invoice.paid")
                        .key("inv-1")
                        .value(payload)
                        .sendAsync();

                    snsClient.publish(
                        PublishRequest.builder()
                            .topicArn("orders-dispatched")
                            .message("ok")
                            .build()
                    );
                }
            }
            """;

        var javaParser = new JavaFileParser();
        var javaDoc = javaParser.ParseDocument(javaCode, "PaymentGateway.java", "PaymentGateway.java")!;
        var javaRefs = new List<Reference>();
        var ctx = new ParsingContext("test", ".");

        string? extractedHttpTarget = null;
        void WalkJava(SyntaxNode n)
        {
            if (javaExt.MapNodeType(n, ctx) == OntologyConstants.NodeLabels.ExternalService)
            {
                extractedHttpTarget = javaExt.ExtractIdentifier(n, ctx, "test", "PaymentGateway.java");
            }
            javaExt.CollectReferences(n, "PaymentGateway.chargeAndNotify", javaRefs, ctx);
            for (var i = 0; i < n.Children.Count; i++) WalkJava(n.Children[i]);
        }

        WalkJava(javaDoc.RootNode);

        extractedHttpTarget.Should().NotBeNull();
        extractedHttpTarget.Should().Contain("payments.internal/v1/charge");
        javaRefs.Should().Contain(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "pulsar:billing.invoice.paid");
        javaRefs.Should().Contain(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "pulsar:orders-dispatched");
    }

    [Test]
    public void Benchmark_SuperpowerCombinators_Vs_FlatRules_RecognitionAndSpeed()
    {
        const string tsComplexWorkload = """
            export class EnterpriseCommerceGateway {
                async registerAndExecute(app: any, db: any, webClient: any, pulsar: any, sns: any, orderId: string) {
                    // 1. Chained route groups (2 prefixes)
                    app.group("/api/v1").group("/orders").post("/cancel", cancelOrder);

                    // 2. Variable-split route groups (2 variable hops)
                    const v2 = app.group("/api/v2");
                    const billing = v2.group("/billing");
                    billing.post("/charge", chargeOrder);

                    // 3. Nested callback DSL route groups (2 enclosing blocks)
                    app.group("/api/v3", (r: any) => {
                        r.group("/users", (u: any) => {
                            u.delete("/purge", purgeUsers);
                        });
                    });

                    // 4. Multi-hop fluent data-store write chain
                    await db.table("orders").where("id", orderId).andWhere("status", "open").timeout(500).update({ status: "paid" });

                    // 5. Multi-hop fluent data-store read chain
                    const customer = await db.from("customers").where("active", true).orderBy("created_at").select("id", "email");

                    // 6. Variable-split data-store delete chain
                    const staleInvoices = db.table("invoices").where("overdue", true);
                    await staleInvoices.delete();

                    // 7. Variable-split collection insert chain
                    const auditCol = db.collection("audit_events");
                    await auditCol.insertOne({ orderId });

                    // 8. Fluent outbound HTTP builder chain
                    await webClient.post().uri("https://fraud.internal/v1/score").header("X-Trace", orderId).retrieve();

                    // 9. Fluent messaging producer builder chain
                    await pulsar.newMessage().topic("commerce.order.paid").key(orderId).value({ orderId }).sendAsync();

                    // 10. Nested builder argument inside messaging publish call
                    await sns.publish(PublishRequest.builder().topicArn("commerce-notifications").build());
                }
            }
            """;

        var parser = new TypeScriptFileParser();
        var doc = parser.ParseDocument(tsComplexWorkload, "gateway.ts", "gateway.ts")!;
        var allNodes = doc.RootNode.Descendants().ToList();
        var ctx = new ParsingContext("bench", ".");

        DeclarativeSemanticExtension BuildExtension(bool enableSuperpowerCombinators) => new()
        {
            Id = enableSuperpowerCombinators ? "superpower-enabled" : "flat-legacy",
            Name = enableSuperpowerCombinators ? "Superpower CallChain Combinators" : "Flat Single-Call Rules",
            InboundRouteCallRules =
            [
                new InboundRouteCallRule
                {
                    MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["get"] = "GET", ["post"] = "POST", ["delete"] = "DELETE"
                    }.ToFrozenDictionary(StringComparer.Ordinal),
                    ReceiverSubstrings = new[] { "app", "router", "r" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                    GroupMethodNames = new[] { "group" }.ToFrozenSet(StringComparer.Ordinal),
                    ResolveChainedRouteGroups = enableSuperpowerCombinators
                }
            ],
            DataStoreAccessRules =
            [
                new DataStoreAccessRule
                {
                    CallMethodNames = new[] { "table", "from", "collection" }.ToFrozenSet(StringComparer.Ordinal),
                    TargetSelectorMethods = enableSuperpowerCombinators
                        ? new[] { "table", "from", "collection" }.ToFrozenSet(StringComparer.Ordinal)
                        : FrozenSet<string>.Empty,
                    ReadMethodNames = enableSuperpowerCombinators
                        ? new[] { "select", "first", "find" }.ToFrozenSet(StringComparer.Ordinal)
                        : FrozenSet<string>.Empty,
                    WriteMethodNames = enableSuperpowerCombinators
                        ? new[] { "update", "insert", "delete", "insertOne" }.ToFrozenSet(StringComparer.Ordinal)
                        : FrozenSet<string>.Empty,
                    ReceiverSubstrings = new[] { "db" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                    ResolveFluentChains = enableSuperpowerCombinators
                }
            ],
            OutboundHttpRules =
            [
                new OutboundHttpRule
                {
                    CallMethodNames = new[] { "retrieve" }.ToFrozenSet(StringComparer.Ordinal),
                    ResolveFluentChains = enableSuperpowerCombinators
                }
            ],
            MessagingBrokerRules =
            [
                new MessagingBrokerRule
                {
                    Protocol = "broker",
                    PublishMethodNames = new[] { "sendAsync", "publish" }.ToFrozenSet(StringComparer.Ordinal),
                    ResolveFluentChains = enableSuperpowerCombinators
                }
            ]
        };

        var flatExt = BuildExtension(enableSuperpowerCombinators: false);
        var superpowerExt = BuildExtension(enableSuperpowerCombinators: true);
        var spIndex = CompiledDeclarativeRuleIndex.Build(superpowerExt);
        var httpRule = superpowerExt.OutboundHttpRules[0];

        const int warmupIterations = 100;
        const int measuredIterations = 1000;
        var flatRefs = new List<Reference>(32);
        var spRefs = new List<Reference>(32);
        var trieRefs = new List<Reference>(32);

        for (var i = 0; i < warmupIterations; i++)
        {
            flatRefs.Clear();
            spRefs.Clear();
            trieRefs.Clear();
            foreach (var n in allNodes)
            {
                flatExt.CollectReferences(n, "Scope", flatRefs, ctx);
                if (n.SyntaxKind == SyntaxKind.CallExpression)
                {
                    spIndex.EvaluateCallNodeViaSuperpowerCombinators(n, "Scope", spRefs);
                }
                superpowerExt.CollectReferences(n, "Scope", trieRefs, ctx);
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocBeforeFlat = GC.GetAllocatedBytesForCurrentThread();
        var swFlat = Stopwatch.StartNew();
        var flatHttpCount = 0;
        for (var i = 0; i < measuredIterations; i++)
        {
            flatRefs.Clear();
            flatHttpCount = 0;
            foreach (var n in allNodes)
            {
                flatExt.CollectReferences(n, "Scope", flatRefs, ctx);
                if (n.SyntaxKind == SyntaxKind.CallExpression &&
                    flatExt.MapNodeType(n, ctx) == OntologyConstants.NodeLabels.ExternalService)
                {
                    var id = flatExt.ExtractIdentifier(n, ctx);
                    if (!string.IsNullOrEmpty(id) && id != "http:unknown-service")
                    {
                        flatHttpCount++;
                    }
                }
            }
        }
        swFlat.Stop();
        var allocFlat = GC.GetAllocatedBytesForCurrentThread() - allocBeforeFlat;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocBeforeSp = GC.GetAllocatedBytesForCurrentThread();
        var swSp = Stopwatch.StartNew();
        var httpCount = 0;
        for (var i = 0; i < measuredIterations; i++)
        {
            spRefs.Clear();
            httpCount = 0;
            foreach (var n in allNodes)
            {
                if (n.SyntaxKind == SyntaxKind.CallExpression)
                {
                    spIndex.EvaluateCallNodeViaSuperpowerCombinators(n, "Scope", spRefs);
                    if (superpowerExt.MapNodeType(n, ctx) == OntologyConstants.NodeLabels.ExternalService &&
                        DeclarativeCallChainCombinators.TryMatchOutboundHttpChain(n, httpRule, out var m) &&
                        !string.IsNullOrEmpty(m.TargetUrl))
                    {
                        httpCount++;
                    }
                }
            }
        }
        swSp.Stop();
        var allocSp = GC.GetAllocatedBytesForCurrentThread() - allocBeforeSp;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocBeforeTrie = GC.GetAllocatedBytesForCurrentThread();
        var swTrie = Stopwatch.StartNew();
        var trieHttpCount = 0;
        for (var i = 0; i < measuredIterations; i++)
        {
            trieRefs.Clear();
            trieHttpCount = 0;
            foreach (var n in allNodes)
            {
                superpowerExt.CollectReferences(n, "Scope", trieRefs, ctx);
                if (n.SyntaxKind == SyntaxKind.CallExpression &&
                    superpowerExt.MapNodeType(n, ctx) == OntologyConstants.NodeLabels.ExternalService)
                {
                    var id = superpowerExt.ExtractIdentifier(n, ctx);
                    if (!string.IsNullOrEmpty(id) && id != "http:unknown-service")
                    {
                        trieHttpCount++;
                    }
                }
            }
        }
        swTrie.Stop();
        var allocTrie = GC.GetAllocatedBytesForCurrentThread() - allocBeforeTrie;

        // Ground-truth expectations for the 10 complex chains:
        // 1. POST:/api/v1/orders/cancel
        // 2. POST:/api/v2/billing/charge
        // 3. DELETE:/api/v3/users/purge
        // 4. WRITES_TO orders (not merely USES_DB)
        // 5. READS_FROM customers (not merely USES_DB)
        // 6. WRITES_TO invoices (not merely USES_DB)
        // 7. WRITES_TO audit_events (not merely USES_DB)
        // 8. ExternalService fraud.internal/v1/score
        // 9. PUBLISHES_TO broker:commerce.order.paid
        // 10. PUBLISHES_TO broker:commerce-notifications
        int CountMatchedGroundTruth(List<Reference> rList, int extHttp)
        {
            var matched = 0;
            if (rList.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "POST:/api/v1/orders/cancel")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "POST:/api/v2/billing/charge")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.Exposes && r.TargetName == "DELETE:/api/v3/users/purge")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.WritesTo && r.TargetName == "orders")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.ReadsFrom && r.TargetName == "customers")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.WritesTo && r.TargetName == "invoices")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.WritesTo && r.TargetName == "audit_events")) matched++;
            if (extHttp > 0) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "broker:commerce.order.paid")) matched++;
            if (rList.Any(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName == "broker:commerce-notifications")) matched++;
            return matched;
        }

        var flatMatched = CountMatchedGroundTruth(flatRefs, flatHttpCount);
        var spMatched = CountMatchedGroundTruth(spRefs, httpCount);
        var trieMatched = CountMatchedGroundTruth(trieRefs, trieHttpCount);
        var totalVisits = (long)measuredIterations * allNodes.Count;
        TestContext.Out.WriteLine($"=== SUPERPOWER vs COMPILED REVERSE-TRIE CALL-CHAIN BENCHMARK ({measuredIterations} files / {totalVisits:N0} AST node visits) ===");
        TestContext.Out.WriteLine($"1) Flat Single-Call Rules         : {swFlat.Elapsed.TotalMilliseconds,7:F2} ms | {swFlat.Elapsed.TotalNanoseconds / totalVisits,6:F1} ns/node | Alloc: {allocFlat / 1024.0,7:F1} KB | Exact Chain Recall: {flatMatched}/10 ({flatMatched * 10}%)");
        TestContext.Out.WriteLine($"2) Superpower Island Combinators  : {swSp.Elapsed.TotalMilliseconds,7:F2} ms | {swSp.Elapsed.TotalNanoseconds / totalVisits,6:F1} ns/node | Alloc: {allocSp / 1024.0,7:F1} KB | Exact Chain Recall: {spMatched}/10 ({spMatched * 10}%)");
        TestContext.Out.WriteLine($"3) Compiled Call-Chain ReverseTrie: {swTrie.Elapsed.TotalMilliseconds,7:F2} ms | {swTrie.Elapsed.TotalNanoseconds / totalVisits,6:F1} ns/node | Alloc: {allocTrie / 1024.0,7:F1} KB | Exact Chain Recall: {trieMatched}/10 ({trieMatched * 10}%)");

        flatMatched.Should().Be(0);
        spMatched.Should().Be(10);
        trieMatched.Should().Be(10);
    }
}
