using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Explicit("Manual run: invokes LLM architectural intent distillation and live inference pipelines.")]
[Category("Manual")]
public class CodeIntentAnalyzerTests
{
    [Test]
    public async Task SqliteGraphClient_LoadIntentCandidates_ReturnsArchitecturalNodes()
    {
        using var client = new SqliteGraphClient(":memory:");

        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            new("ws:type:1", OntologyConstants.NodeLabels.Type,
                new Dictionary<string, object>
                {
                    ["id"] = "ws:type:1",
                    ["name"] = "ScheduledActionService",
                    ["path"] = "src/services/scheduled-action.service.ts",
                    ["kind"] = "class"
                }),
            new("ws:type:2", OntologyConstants.NodeLabels.Type,
                new Dictionary<string, object>
                {
                    ["id"] = "ws:type:2",
                    ["name"] = "OrderController",
                    ["path"] = "src/controllers/order.controller.cs",
                    ["kind"] = "class"
                }),
            new("ws:type:3", OntologyConstants.NodeLabels.Type,
                new Dictionary<string, object>
                {
                    ["id"] = "ws:type:3",
                    ["name"] = "SomeHelperUtilsSpec",
                    ["path"] = "src/utils.spec.ts",
                    ["kind"] = "class"
                })
        };

        await client.UploadNodesAsync(nodes);

        var candidates = await client.LoadIntentCandidatesAsync("ws");

        (candidates.Any(c => c.Name == "ScheduledActionService")).Should().BeTrue();
        (candidates.Any(c => c.Name == "OrderController")).Should().BeTrue();
        (candidates.Any(c => c.Name == "SomeHelperUtilsSpec")).Should().BeFalse();
    }

    [Test]
    public async Task SqliteGraphClient_SaveIntentPredictions_UpdatesNodeAndCreatesDomainEdge()
    {
        using var client = new SqliteGraphClient(":memory:");

        await client.CreateIndicesAsync();

        var node = new Node("ws:type:svc", OntologyConstants.NodeLabels.Type,
            new Dictionary<string, object>
            {
                ["id"] = "ws:type:svc",
                ["name"] = "BillingService",
                ["path"] = "src/services/billing.service.ts",
                ["kind"] = "class"
            });
        await client.UploadNodesAsync([node]);

        var predictions = new List<CodeIntentPredictionResult>
        {
            new(Id: "ws:type:svc", FilePath: "src/services/billing.service.ts", Domain: "Billing",
                Layer: "Application", Pattern: "Service", OperationType: "Command",
                CapabilityTag: "ProcessPayment", IntentSummary: "Handles invoices and payment execution.",
                IsPureDomain: false, TargetEntities: ["Invoice", "Payment"],
                EmittedEvents: ["InvoicePaidEvent"])
        };

        await client.SaveIntentPredictionsAsync("ws", predictions);

        // Verify updated properties via Cypher query
        var queryResult = await client.ExecuteQueryAsync(
            "MATCH (n:Type {id: 'ws:type:svc'}) RETURN n.intent_domain AS domain, n.intent_layer AS layer, n.intent_summary AS summary");
        queryResult.Contains("Billing").Should().BeTrue();
        queryResult.Contains("Application").Should().BeTrue();
        queryResult.Contains("Handles invoices and payment execution.").Should().BeTrue();

        // Verify Domain node creation and relationship
        var domainResult = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.id AS id, d.name AS name");
        domainResult.Contains("Billing").Should().BeTrue();

        var relResult =
            await client.ExecuteQueryAsync(
                "MATCH (n:Type)-[r:BELONGS_TO_DOMAIN]->(d:Domain) RETURN n.id AS from_id, d.id AS to_id");
        relResult.Contains("ws:type:svc").Should().BeTrue();
        domainResult.ToLowerInvariant().Contains("ws:dom:billing").Should().BeTrue();
    }

    [Test]
    public async Task CodeIntentAnalyzer_EnrichAsync_FallbackWhenNoModel_RunsCleanlyWithoutError()
    {
        using var client = new SqliteGraphClient(":memory:");

        await client.CreateIndicesAsync();

        var channel = System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>();
        var tempDir = Path.GetTempPath();
        var ctx = new ParsingContext(tempDir, tempDir, client, channel);
        ctx.WorkspaceId = "test_ws";

        // Call EnrichAsync - should not throw, should log fallback message
        await CodeIntentAnalyzer.EnrichAsync(ctx);

        Assert.Pass();
    }

    [Test]
    public void ModelManager_ResolveModelPath_FindsExistingModel()
    {
        var path = ModelManager.ResolveModelPath();
        if (File.Exists(ModelManager.DefaultModelPath))
        {
            path.Should().NotBeNull();
            File.Exists(path).Should().BeTrue();
        }
        else
        {
            Assert.Pass("Model not present on machine in default path; skipping check.");
        }
    }

    [Test]
    public async Task NativeIntentPredictor_Inference_ReturnsValidDomainAndIntent()
    {
        var modelPath = ModelManager.ResolveModelPath();
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            Assert.Ignore("GGUF model not available on this environment; skipping inference test.");
            return;
        }

        using var predictor = new NativeIntentPredictor(modelPath, contextSize: 2048, gpuLayers: 0);

        var sampleCode = @"
namespace OrderSystem.Services;

public class OrderPlacementService : IOrderService
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEventBus _eventBus;

    public async Task PlaceOrderAsync(Order order)
    {
        await _paymentGateway.ChargeAsync(order.Total);
        await _eventBus.PublishAsync(new OrderPlacedEvent(order.Id));
    }
}
";

        var (result, raw) = await predictor.PredictWithRawAsync("OrderPlacementService.cs", sampleCode);
        TestContext.Out.WriteLine($"RAW MODEL OUTPUT:\n{raw}");

        result.Should().NotBeNull();
        result!.Domain.Should().NotBeNullOrEmpty();
        result.IntentSummary.Should().NotBeNullOrEmpty();

        TestContext.Out.WriteLine(
            $"Inferred Domain: {result.Domain}, Layer: {result.Layer}, Pattern: {result.Pattern}, Summary: {result.IntentSummary}");
    }

    [Test]
    public async Task NativeIntentPredictor_ProjectSignature_Inference()
    {
        var modelPath = ModelManager.ResolveModelPath();
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            Assert.Ignore("GGUF model not available on this environment; skipping inference test.");
            return;
        }

        using var predictor = new NativeIntentPredictor(modelPath, contextSize: 2048, gpuLayers: 99);

        var testProjects = new[]
        {
            (Name: "bq-routes-calculation", Signature: @"
Endpoints:
- GET /api/bundle-loss
- GET /approve-cost-loss
- GET /api/daily-offers-statistics
- GET /api/get-sources-with-incorrect-traffic
Databases & Tables:
- BigQuery: bundle_cost_loss, calculated_rates_temp, calculation_queue_new_campaigns_results
Domain Entities:
- ICalcRatesTbClickType, IDesiredMaxCpmResult, ICampaignResultTable
"),
            (Name: "cpm-streaming-aggregator", Signature: @"
Endpoints:
- GET /api/v1/bundles/maxcpm
- GET /api/v1/results
- POST /api/v1/bundles
Tables:
- bundle_placements, bundle_snapshots, engine_lifecycle
Domain Entities:
- BundleCalculator, ConversionEvent, CostJournal, Campaign, BundleLifecycleStatus
"),
            (Name: "domain-checker", Signature: @"
Endpoints:
- GET /proxy
- POST /proxy
- DELETE /proxy
- QUERY getProxy
Tables:
- domain_check, proxy
Domain Entities:
- DomainCheckEntity, CreateProxyDto, GetProxyDto, IGetDomainCheckResponse
"),
            (Name: "rule-tree-updater", Signature: @"
Endpoints:
- DELETE /system/bundle-rules
- DELETE /system/popunders
- DELETE /system/rates
- DELETE /system/split-bundles
Tables:
- country_traffic_skins, custom_rates, tb_click_type_rates
Domain Entities:
- BundleRuleConfig, BbhConfig, IBundleRuleResponse
")
        };

        foreach (var proj in testProjects)
        {
            var (result, raw) = await predictor.PredictWithRawAsync($"{proj.Name}/project-signature.spec",
                proj.Signature, projectName: proj.Name);

            TestContext.Out.WriteLine($"==================================================");
            TestContext.Out.WriteLine($"PROJECT: {proj.Name}");
            TestContext.Out.WriteLine($"RAW:\n{raw}");

            if (result != null)
            {
                TestContext.Out.WriteLine($"--> INFERRED DOMAIN:  {result.Domain}");
                TestContext.Out.WriteLine($"--> INFERRED LAYER:   {result.Layer}");
                TestContext.Out.WriteLine($"--> INFERRED PATTERN: {result.Pattern}");
                TestContext.Out.WriteLine($"--> SUMMARY:          {result.IntentSummary}");
            }

            TestContext.Out.WriteLine();
        }
    }

    [Test]
    public async Task SqliteGraphClient_IntentCacheLifecycle_PreservedAcrossClearDatabase()
    {
        using var client = new SqliteGraphClient(":memory:");

        await client.CreateIndicesAsync();

        var node = new Node("ws:f:src/service.ts", OntologyConstants.NodeLabels.File,
            new Dictionary<string, object>
            {
                ["id"] = "ws:f:src/service.ts", ["name"] = "service.ts", ["path"] = "src/service.ts"
            });
        await client.UploadNodesAsync([node]);

        var record = new IntentRecord(FilePath: "src/service.ts", WorkspaceId: "ws", FileId: "ws:f:src/service.ts",
            ContentHash: "hash123", LastModifiedUtc: DateTime.UtcNow, Domain: "Payments", Layer: "Application",
            Pattern: "Service", OperationType: "Command", CapabilityTag: "CapturePayment",
            IntentSummary: "Processes payments securely.", TargetEntities: ["Payment"],
            EmittedEvents: ["PaymentProcessed"], IsPureDomain: false, ErrorCount: 0, LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow);

        await client.SaveIntentRecordAsync(record);

        // Verify loaded
        var existing = await client.LoadExistingIntentsAsync("ws");
        existing.Count.Should().Be(1);
        existing[0].Domain.Should().Be("Payments");

        // Apply to graph
        var applied = await client.ApplyCachedIntentsToGraphAsync("ws");
        applied.Should().BeGreaterThanOrEqualTo(1);

        var domainQuery = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.id AS id, d.name AS name");
        domainQuery.Contains("Payments").Should().BeTrue();

        // Clear database (e.g. ce scan --clear)
        await client.ClearDatabaseAsync();

        // Node should be deleted
        var candidatesAfterClear = await client.LoadIntentCandidatesAsync("ws");
        candidatesAfterClear.Count.Should().Be(0);

        // BUT Intent record should STILL BE PRESERVED
        var intentsAfterClear = await client.LoadExistingIntentsAsync("ws");
        intentsAfterClear.Count.Should().Be(1);
        intentsAfterClear[0].ContentHash.Should().Be("hash123");

        // Test error increment & reset
        await client.IncrementIntentErrorAsync("src/service.ts", "ws", "ws:f:src/service.ts", "hash123",
            DateTime.UtcNow, "LLM timeout");
        var withError = await client.LoadExistingIntentsAsync("ws");
        withError[0].ErrorCount.Should().Be(1);
        withError[0].LastError.Should().Be("LLM timeout");

        await client.ResetIntentErrorsAsync("ws");
        var resetErrors = await client.LoadExistingIntentsAsync("ws");
        resetErrors[0].ErrorCount.Should().Be(0);
        resetErrors[0].LastError.Should().BeNull();

        // Test explicit ClearIntents
        await client.ClearIntentsAsync("ws");
        var emptyIntents = await client.LoadExistingIntentsAsync("ws");
        emptyIntents.Count.Should().Be(0);
    }

    [Test]
    public void NativeIntentPredictor_CalculateOptimalConcurrency_HardwareBased()
    {
        // 1. Explicit caller override
        var (cExplicit, rExplicit) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: 3,
            isGpu: true,
            executionDevice: "GPU (Metal)",
            gpuLayers: 99
        );
        cExplicit.Should().Be(3);
        rExplicit.Should().Contain("explicitly requested");

        // 2. Apple Silicon Metal with typical memory (8 cores, 24 GB) -> 1
        var (cM2, rM2) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: null,
            isGpu: true,
            executionDevice: "GPU (Metal: Apple M2)",
            gpuLayers: 99,
            cpuCoreCount: 8,
            memoryBytes: 24UL * 1024 * 1024 * 1024
        );
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            cM2.Should().Be(1);
            rM2.Should().Contain("single stream prevents command queue contention");
        }

        // 3. Apple Silicon Metal Max/Ultra (16 cores, 64 GB) -> 2
        var (cMax, rMax) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: null,
            isGpu: true,
            executionDevice: "GPU (Metal: Apple M3 Max)",
            gpuLayers: 99,
            cpuCoreCount: 16,
            memoryBytes: 64UL * 1024 * 1024 * 1024
        );
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            cMax.Should().Be(2);
            rMax.Should().Contain("Max/Ultra");
        }

        // 4. Discrete GPU (CUDA) on 16-core workstation with 32 GB RAM -> 4
        var (cCudaHigh, rCudaHigh) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: null,
            isGpu: true,
            executionDevice: "GPU (CUDA: NVIDIA RTX 4090)",
            gpuLayers: 99,
            cpuCoreCount: 16,
            memoryBytes: 32UL * 1024 * 1024 * 1024
        );
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            cCudaHigh.Should().Be(4);
        }

        // 5. CPU-only with low resources (4 cores, 8 GB RAM) -> 1
        var (cCpuLow, _) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: null,
            isGpu: false,
            executionDevice: "CPU",
            gpuLayers: 0,
            cpuCoreCount: 4,
            memoryBytes: 8UL * 1024 * 1024 * 1024
        );
        cCpuLow.Should().Be(1);

        // 6. CPU-only with high resources (16 cores, 32 GB RAM) -> 3
        var (cCpuHigh, _) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: null,
            isGpu: false,
            executionDevice: "CPU",
            gpuLayers: 0,
            cpuCoreCount: 16,
            memoryBytes: 32UL * 1024 * 1024 * 1024
        );
        cCpuHigh.Should().Be(3);
    }

    [Test]
    public void ComputeSha256_NormalizesLineEndings_ProducesIdenticalHashForCrlfAndLf()
    {
        var crlfContent = "using System;\r\n\r\npublic class Service\r\n{\r\n    public void Run() => Console.WriteLine(\"test\");\r\n}\r\n";
        var lfContent = "using System;\n\npublic class Service\n{\n    public void Run() => Console.WriteLine(\"test\");\n}\n";
        var differentContent = "using System;\n\npublic class Service\n{\n    public void Run() => Console.WriteLine(\"other\");\n}\n";

        var crlfBytes = System.Text.Encoding.UTF8.GetBytes(crlfContent);
        var lfBytes = System.Text.Encoding.UTF8.GetBytes(lfContent);
        var diffBytes = System.Text.Encoding.UTF8.GetBytes(differentContent);

        var crlfHash = CodeIntentAnalyzer.ComputeSha256(crlfBytes);
        var lfHash = CodeIntentAnalyzer.ComputeSha256(lfBytes);
        var diffHash = CodeIntentAnalyzer.ComputeSha256(diffBytes);

        crlfHash.Should().Be(lfHash);
        crlfHash.Should().NotBe(diffHash);
        crlfHash.Length.Should().Be(64);
    }

    [Test]
    public void SynthesizeDomainsTopologically_WithConfiguredDomains_CorrectlyAggregatesConfiguredProfiles()
    {
        var config = new WorkspaceDomainsConfig(
        [
            new("Integrations", "External partner integrations", ["integration", "nrt", "network"]),
            new("Ops", "Internal operations and workflows", ["approval", "journal", "ops"]),
            new("Edge", "Edge proxy and KV", ["kv", "edge", "worker"]),
            new("Auctions", "Auction and bidding", ["bidding", "bundle", "auction"]),
            new("UserInterface", "UI widgets and presentations", ["ui", "component", "widget"])
        ]);

        var signatures = new List<ProjectSignature>
        {
            new("p1", "IntegrationServiceNrt", "services/integration-service-nrt", [], [], [], []),
            new("p2", "InternalServiceApproval", "services/internal-service-approval", [], [], [], []),
            new("p3", "InternalServiceJournal", "services/internal-service-journal", [], [], [], []),
            new("p4", "InternalServiceKvV2", "services/internal-service-kv-v2", [], [], [], []),
            new("p5", "AuctionService", "services/auction-service", [], [], [], []),
            new("p6", "Button", "packages/ui/button", [], [], [], []),
            new("p7", "Modal", "packages/ui/modal", [], [], [], []),
            new("p8", "SelectButton", "packages/ui/select-button", [], [], [], []),
            new("p9", "ContextMenu", "packages/ui/context-menu", [], [], [], []),
            new("p10", "ProgressBar", "packages/ui/progress-bar", [], [], [], []),
            new("p11", "Toast", "packages/ui/toast", [], [], [], []),
            new("p12", "Table", "packages/ui/table", [], [], [], []),
            new("p13", "SharedKernel", "packages/shared-kernel", [], [], [], [])
        };

        var result = CodeIntentAnalyzer.SynthesizeDomainsTopologically(signatures, config);

        var domainNames = result.Domains.Select(d => d.Name).ToList();

        // 1. Configured business profiles matched
        var integrations = result.Domains.FirstOrDefault(d => d.Name == "Integrations");
        integrations.Should().NotBeNull();
        integrations!.Services.Should().Contain("IntegrationServiceNrt");

        var ops = result.Domains.FirstOrDefault(d => d.Name == "Ops");
        ops.Should().NotBeNull();
        ops!.Services.Should().Contain("InternalServiceApproval");
        ops.Services.Should().Contain("InternalServiceJournal");

        var edge = result.Domains.FirstOrDefault(d => d.Name == "Edge");
        edge.Should().NotBeNull();
        edge!.Services.Should().Contain("InternalServiceKvV2");

        var auctions = result.Domains.FirstOrDefault(d => d.Name == "Auctions");
        auctions.Should().NotBeNull();
        auctions!.Services.Should().Contain("AuctionService");

        // 2. UI widgets collapsed into UserInterface
        var ui = result.Domains.FirstOrDefault(d => d.Name == "UserInterface");
        ui.Should().NotBeNull();
        ui!.Services.Should().Contain("Button");
        ui.Services.Should().Contain("Modal");
        ui.Services.Should().Contain("SelectButton");
        ui.Services.Should().Contain("ContextMenu");
        ui.Services.Should().Contain("ProgressBar");
        ui.Services.Should().Contain("Toast");
        ui.Services.Should().Contain("Table");

        // 3. Ensure NO micro-domains created for individual UI widgets
        domainNames.Should().NotContain("Button");
        domainNames.Should().NotContain("Modal");
        domainNames.Should().NotContain("SelectButton");
        domainNames.Should().NotContain("ProgressBar");
    }

    [Test]
    public void SynthesizeDomainsTopologically_WithoutConfig_ClustersByGraphAffinityAndTechnicalSubdomains()
    {
        var signatures = new List<ProjectSignature>
        {
            // Services sharing database tables: order_items, orders
            new("p1", "OrderProcessingService", "src/orders/processor", ["orders", "order_items"], [], [], []),
            new("p2", "OrderDispatchWorker", "src/orders/dispatch", ["orders"], [], [], []),

            // Services in billing namespace sharing invoices table
            new("p3", "BillingService", "src/billing/service", ["invoices"], [], [], []),
            new("p4", "InvoiceGenerator", "src/billing/invoices", ["invoices"], [], [], []),

            // UI presentation components
            new("p5", "Button", "packages/ui/button", [], [], [], []),
            new("p6", "ModalDialog", "packages/ui/modal", [], [], [], []),

            // Shared contracts/primitives
            new("p7", "SharedKernel", "src/common/shared-kernel", [], [], [], []),

            // Tooling
            new("p8", "DbMigrationTool", "tools/migrator", [], [], [], [])
        };

        var result = CodeIntentAnalyzer.SynthesizeDomainsTopologically(signatures, null);

        var domainNames = result.Domains.Select(d => d.Name).ToList();

        // 1. UI components collapsed into UserInterface
        var ui = result.Domains.FirstOrDefault(d => d.Name == "UserInterface");
        ui.Should().NotBeNull();
        ui!.Services.Should().Contain("Button");
        ui.Services.Should().Contain("ModalDialog");

        // 2. SharedKernel collapsed into SharedKernel
        var shared = result.Domains.FirstOrDefault(d => d.Name == "SharedKernel");
        shared.Should().NotBeNull();
        shared!.Services.Should().Contain("SharedKernel");

        // 3. DeveloperTooling collapsed into DeveloperTooling
        var tooling = result.Domains.FirstOrDefault(d => d.Name == "DeveloperTooling");
        tooling.Should().NotBeNull();
        tooling!.Services.Should().Contain("DbMigrationTool");

        // 4. Orders services clustered together via shared table & directory namespace
        var ordersDomain = result.Domains.FirstOrDefault(d => d.Services.Contains("OrderProcessingService"));
        ordersDomain.Should().NotBeNull();
        ordersDomain!.Services.Should().Contain("OrderDispatchWorker");

        // 5. Billing services clustered together via shared table & directory namespace
        var billingDomain = result.Domains.FirstOrDefault(d => d.Services.Contains("BillingService"));
        billingDomain.Should().NotBeNull();
        billingDomain!.Services.Should().Contain("InvoiceGenerator");
    }

    [Test]
    public void LoadWorkspaceDomainsConfig_LoadsFromDotCodeExplorerDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_" + Guid.NewGuid().ToString("N"));
        var ceDir = Path.Combine(tempDir, ".codeexplorer");
        Directory.CreateDirectory(ceDir);

        try
        {
            var json = """
            {
              "domains": [
                {
                  "name": "Finance",
                  "description": "Accounting and ledger",
                  "keywords": ["ledger", "invoice", "payment"]
                }
              ]
            }
            """;
            File.WriteAllText(Path.Combine(ceDir, "domains.json"), json);

            var config = CodeIntentAnalyzer.LoadWorkspaceDomainsConfig(tempDir);
            config.Should().NotBeNull();
            config!.Domains.Should().HaveCount(1);
            config.Domains[0].Name.Should().Be("Finance");
            config.Domains[0].Description.Should().Be("Accounting and ledger");
            config.Domains[0].Keywords.Should().Contain("ledger");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public async Task SqliteGraphClient_LoadIntentCandidates_ExcludesSqlFiles()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var nodes = new List<Node>
        {
            new("ws:file:1", OntologyConstants.NodeLabels.File,
                new Dictionary<string, object>
                {
                    ["id"] = "ws:file:1",
                    ["name"] = "get-bundles.query.sql",
                    ["path"] = "sql/scripts/get-bundles.query.sql",
                    ["kind"] = "file"
                }),
            new("ws:type:2", OntologyConstants.NodeLabels.Type,
                new Dictionary<string, object>
                {
                    ["id"] = "ws:type:2",
                    ["name"] = "OrderQueryService",
                    ["path"] = "src/services/order.query.service.cs",
                    ["kind"] = "class"
                })
        };

        await client.UploadNodesAsync(nodes);

        var candidates = await client.LoadIntentCandidatesAsync("ws");

        (candidates.Any(c => c.Name == "OrderQueryService")).Should().BeTrue();
        (candidates.Any(c => c.Name == "get-bundles.query.sql")).Should().BeFalse();
    }

    [Test]
    public async Task SqliteGraphClient_PurgeIntentsByPaths_RemovesIntentsAndCleansOrphanDomains()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var fileNode = new Node("ws:file:orphan", OntologyConstants.NodeLabels.File,
            new Dictionary<string, object>
            {
                ["id"] = "ws:file:orphan",
                ["name"] = "orphan.js",
                ["path"] = "scripts/orphan.js",
                ["kind"] = "file"
            });
        await client.UploadNodesAsync([fileNode]);

        await client.SaveIntentRecordAsync(new IntentRecord(
            FilePath: "scripts/orphan.js",
            WorkspaceId: "ws",
            FileId: "ws:file:orphan",
            ContentHash: "hash",
            LastModifiedUtc: DateTime.UtcNow,
            Domain: "GhostDomain",
            Layer: "Infrastructure",
            Pattern: "Script",
            OperationType: "Execute",
            CapabilityTag: "Orphan",
            IntentSummary: "Orphan script",
            TargetEntities: [],
            EmittedEvents: [],
            IsPureDomain: false,
            ErrorCount: 0,
            LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow));

        await client.ApplyCachedIntentsToGraphAsync("ws");

        // Verify GhostDomain exists before purge
        var domainsBefore = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.name AS name");
        domainsBefore.Contains("GhostDomain").Should().BeTrue();

        // Purge orphan intent
        await client.PurgeIntentsByPathsAsync(["scripts/orphan.js"]);

        // Apply cache again: orphan Domain node must be cleaned up
        await client.ApplyCachedIntentsToGraphAsync("ws");

        var domainsAfter = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.name AS name");
        domainsAfter.Contains("GhostDomain").Should().BeFalse();
    }

    [Test]
    public async Task SqliteGraphClient_ApplyCachedIntents_AutoPurgesSqlIntents()
    {
        using var client = new SqliteGraphClient(":memory:");
        await client.CreateIndicesAsync();

        var fileNode = new Node("ws:file:query", OntologyConstants.NodeLabels.File,
            new Dictionary<string, object>
            {
                ["id"] = "ws:file:query",
                ["name"] = "test.query.sql",
                ["path"] = "sql/test.query.sql",
                ["kind"] = "file"
            });
        await client.UploadNodesAsync([fileNode]);

        await client.SaveIntentRecordAsync(new IntentRecord(
            FilePath: "sql/test.query.sql",
            WorkspaceId: "ws",
            FileId: "ws:file:query",
            ContentHash: "hash",
            LastModifiedUtc: DateTime.UtcNow,
            Domain: "SqlDomain",
            Layer: "Infrastructure",
            Pattern: "Script",
            OperationType: "Query",
            CapabilityTag: "Query",
            IntentSummary: "SQL query",
            TargetEntities: [],
            EmittedEvents: [],
            IsPureDomain: false,
            ErrorCount: 0,
            LastError: null,
            AnalyzedAtUtc: DateTime.UtcNow));

        // Apply cached intents: step 0 auto-deletes .sql intents and step 6 prevents SqlDomain creation
        await client.ApplyCachedIntentsToGraphAsync("ws");

        var domains = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.name AS name");
        domains.Contains("SqlDomain").Should().BeFalse();

        var intents = await client.LoadExistingIntentsAsync("ws");
        intents.Any(i => i.FilePath.EndsWith(".sql")).Should().BeFalse();
    }

    [Test]
    public void CanonicalizeDomain_CorrectlyMapsMicroservicesAndTablesToProblemSpaces()
    {
        // 1. Service name normalization into clean PascalCase domain
        WorkspaceConventions.CanonicalizeDomain(null, "internal-service-approval").Should().Be("Approval");
        WorkspaceConventions.CanonicalizeDomain(null, "internal-service-notifier").Should().Be("Notifier");
        WorkspaceConventions.CanonicalizeDomain(null, "internal-service-scheduler").Should().Be("Scheduler");
        WorkspaceConventions.CanonicalizeDomain(null, "internal-service-billing").Should().Be("Billing");
        WorkspaceConventions.CanonicalizeDomain(null, "integration-service-payment").Should().Be("Payment");

        // 2. Directory structure extraction
        WorkspaceConventions.CanonicalizeDomain(null, null, "services/billing/invoicing-service").Should().Be("Billing");
        WorkspaceConventions.CanonicalizeDomain(null, null, "src/identity/auth-service").Should().Be("Identity");
        WorkspaceConventions.CanonicalizeDomain(null, null, "services/orders/order-processor").Should().Be("Orders");

        // 3. Technical paths and ontology roles
        WorkspaceConventions.CanonicalizeDomain(null, "button", "packages/ui/button").Should().Be("UserInterface");
        WorkspaceConventions.CanonicalizeDomain(null, "common-utils", role: "SharedLibrary").Should().Be("SharedKernel");
        WorkspaceConventions.CanonicalizeDomain(null, "cli-tool", role: "CliTool").Should().Be("DeveloperTooling");
        WorkspaceConventions.CanonicalizeDomain(null, "unit-tests", role: "Test").Should().Be("TestingInfrastructure");

        // 4. Universal Display formatting
        WorkspaceConventions.FormatDomainDisplayName("OperationsAndWorkflows").Should().Be("Operations & Workflows");
        WorkspaceConventions.FormatDomainDisplayName("BillingAndPayments").Should().Be("Billing & Payments");
        WorkspaceConventions.FormatDomainDisplayName("OrderManagement").Should().Be("Order Management");
        WorkspaceConventions.FormatDomainDisplayName("IdentityAndAccess").Should().Be("Identity & Access");
    }

    [Test]
    public void CanonicalizeDomain_RejectsRunawayHallucinatedDomain_FallsBackToHeuristicsOrTruncatedWords()
    {
        // 1. Runaway string with directory path available -> falls back to directory heuristic
        var runawayString = "Performance & Scalability Tests For ECommerce Platform With Automated Load Generation";
        var res1 = WorkspaceConventions.CanonicalizeDomain(runawayString, null, "services/billing/invoicing-service");
        res1.Should().Be("Billing");

        // 2. Runaway string with project name available -> falls back to project heuristic
        var res2 = WorkspaceConventions.CanonicalizeDomain(runawayString, "order-service");
        res2.Should().Be("OrderService");

        // 3. Runaway string with NO project or directory -> extracts first 3 words
        var res3 = WorkspaceConventions.CanonicalizeDomain(runawayString);
        res3.Should().Be("PerformanceScalabilityTests");

        // 4. Valid domain under 40 chars and <= 5 words is accepted
        var res4 = WorkspaceConventions.CanonicalizeDomain("BillingAndPayments");
        res4.Should().Be("BillingAndPayments");
    }

    [Test]
    public void NativeIntentPredictor_SanitizePredictedDomainName_CleansHallucinationsAndSentences()
    {
        // 1. Strips '&' and handles 2 words
        NativeIntentPredictor.SanitizePredictedDomainName("Billing & Payments").Should().Be("BillingPayments");

        // 2. Deduplicates consecutive words
        NativeIntentPredictor.SanitizePredictedDomainName("Ordering Ordering System").Should().Be("OrderingSystem");

        // 3. Rejects sentences
        NativeIntentPredictor.SanitizePredictedDomainName("This microservice is responsible for processing payments.").Should().Be("Core");
        NativeIntentPredictor.SanitizePredictedDomainName("The service handles all user authentication workflows.").Should().Be("Core");

        // 4. Truncates long runaway strings (> 4 words or > 40 chars) to at most 3 words
        var runaway = "Performance & Scalability Tests For ECommerce Platform That Runs Daily";
        NativeIntentPredictor.SanitizePredictedDomainName(runaway).Should().Be("PerformanceScalabilityTests");

        // 5. Handles prefixes 'domain:' and 'dom:'
        NativeIntentPredictor.SanitizePredictedDomainName("dom:telemetry").Should().Be("Telemetry");
        NativeIntentPredictor.SanitizePredictedDomainName("domain:identity_access").Should().Be("IdentityAccess");
    }
}

