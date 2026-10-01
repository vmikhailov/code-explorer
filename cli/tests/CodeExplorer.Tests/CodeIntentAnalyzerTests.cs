using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
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

        Assert.That(candidates.Any(c => c.Name == "ScheduledActionService"), Is.True);
        Assert.That(candidates.Any(c => c.Name == "OrderController"), Is.True);
        Assert.That(candidates.Any(c => c.Name == "SomeHelperUtilsSpec"), Is.False);
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
        Assert.That(queryResult.Contains("Billing"), Is.True);
        Assert.That(queryResult.Contains("Application"), Is.True);
        Assert.That(queryResult.Contains("Handles invoices and payment execution."), Is.True);

        // Verify Domain node creation and relationship
        var domainResult = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.id AS id, d.name AS name");
        Assert.That(domainResult.Contains("Billing"), Is.True);

        var relResult =
            await client.ExecuteQueryAsync(
                "MATCH (n:Type)-[r:BELONGS_TO_DOMAIN]->(d:Domain) RETURN n.id AS from_id, d.id AS to_id");
        Assert.That(relResult.Contains("ws:type:svc"), Is.True);
        Assert.That(domainResult.ToLowerInvariant().Contains("ws:dom:billing"), Is.True);
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
            Assert.That(path, Is.Not.Null);
            Assert.That(File.Exists(path), Is.True);
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

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Domain, Is.Not.Null.Or.Empty);
        Assert.That(result.IntentSummary, Is.Not.Null.Or.Empty);

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
        Assert.That(existing.Count, Is.EqualTo(1));
        Assert.That(existing[0].Domain, Is.EqualTo("Payments"));

        // Apply to graph
        var applied = await client.ApplyCachedIntentsToGraphAsync("ws");
        Assert.That(applied, Is.GreaterThanOrEqualTo(1));

        var domainQuery = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.id AS id, d.name AS name");
        Assert.That(domainQuery.Contains("Payments"), Is.True);

        // Clear database (e.g. ce scan --clear)
        await client.ClearDatabaseAsync();

        // Node should be deleted
        var candidatesAfterClear = await client.LoadIntentCandidatesAsync("ws");
        Assert.That(candidatesAfterClear.Count, Is.EqualTo(0));

        // BUT Intent record should STILL BE PRESERVED
        var intentsAfterClear = await client.LoadExistingIntentsAsync("ws");
        Assert.That(intentsAfterClear.Count, Is.EqualTo(1));
        Assert.That(intentsAfterClear[0].ContentHash, Is.EqualTo("hash123"));

        // Test error increment & reset
        await client.IncrementIntentErrorAsync("src/service.ts", "ws", "ws:f:src/service.ts", "hash123",
            DateTime.UtcNow, "LLM timeout");
        var withError = await client.LoadExistingIntentsAsync("ws");
        Assert.That(withError[0].ErrorCount, Is.EqualTo(1));
        Assert.That(withError[0].LastError, Is.EqualTo("LLM timeout"));

        await client.ResetIntentErrorsAsync("ws");
        var resetErrors = await client.LoadExistingIntentsAsync("ws");
        Assert.That(resetErrors[0].ErrorCount, Is.EqualTo(0));
        Assert.That(resetErrors[0].LastError, Is.Null);

        // Test explicit ClearIntents
        await client.ClearIntentsAsync("ws");
        var emptyIntents = await client.LoadExistingIntentsAsync("ws");
        Assert.That(emptyIntents.Count, Is.EqualTo(0));
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
        Assert.That(cExplicit, Is.EqualTo(3));
        Assert.That(rExplicit, Does.Contain("explicitly requested"));

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
            Assert.That(cM2, Is.EqualTo(1));
            Assert.That(rM2, Does.Contain("single stream prevents command queue contention"));
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
            Assert.That(cMax, Is.EqualTo(2));
            Assert.That(rMax, Does.Contain("Max/Ultra"));
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
            Assert.That(cCudaHigh, Is.EqualTo(4));
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
        Assert.That(cCpuLow, Is.EqualTo(1));

        // 6. CPU-only with high resources (16 cores, 32 GB RAM) -> 3
        var (cCpuHigh, _) = NativeIntentPredictor.CalculateOptimalConcurrency(
            explicitConcurrency: null,
            isGpu: false,
            executionDevice: "CPU",
            gpuLayers: 0,
            cpuCoreCount: 16,
            memoryBytes: 32UL * 1024 * 1024 * 1024
        );
        Assert.That(cCpuHigh, Is.EqualTo(3));
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

        Assert.That(crlfHash, Is.EqualTo(lfHash));
        Assert.That(crlfHash, Is.Not.EqualTo(diffHash));
        Assert.That(crlfHash.Length, Is.EqualTo(64));
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
        Assert.That(integrations, Is.Not.Null);
        Assert.That(integrations!.Services, Does.Contain("IntegrationServiceNrt"));

        var ops = result.Domains.FirstOrDefault(d => d.Name == "Ops");
        Assert.That(ops, Is.Not.Null);
        Assert.That(ops!.Services, Does.Contain("InternalServiceApproval"));
        Assert.That(ops.Services, Does.Contain("InternalServiceJournal"));

        var edge = result.Domains.FirstOrDefault(d => d.Name == "Edge");
        Assert.That(edge, Is.Not.Null);
        Assert.That(edge!.Services, Does.Contain("InternalServiceKvV2"));

        var auctions = result.Domains.FirstOrDefault(d => d.Name == "Auctions");
        Assert.That(auctions, Is.Not.Null);
        Assert.That(auctions!.Services, Does.Contain("AuctionService"));

        // 2. UI widgets collapsed into UserInterface
        var ui = result.Domains.FirstOrDefault(d => d.Name == "UserInterface");
        Assert.That(ui, Is.Not.Null);
        Assert.That(ui!.Services, Does.Contain("Button"));
        Assert.That(ui.Services, Does.Contain("Modal"));
        Assert.That(ui.Services, Does.Contain("SelectButton"));
        Assert.That(ui.Services, Does.Contain("ContextMenu"));
        Assert.That(ui.Services, Does.Contain("ProgressBar"));
        Assert.That(ui.Services, Does.Contain("Toast"));
        Assert.That(ui.Services, Does.Contain("Table"));

        // 3. Ensure NO micro-domains created for individual UI widgets
        Assert.That(domainNames, Does.Not.Contain("Button"));
        Assert.That(domainNames, Does.Not.Contain("Modal"));
        Assert.That(domainNames, Does.Not.Contain("SelectButton"));
        Assert.That(domainNames, Does.Not.Contain("ProgressBar"));
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
        Assert.That(ui, Is.Not.Null);
        Assert.That(ui!.Services, Does.Contain("Button"));
        Assert.That(ui.Services, Does.Contain("ModalDialog"));

        // 2. SharedKernel collapsed into SharedKernel
        var shared = result.Domains.FirstOrDefault(d => d.Name == "SharedKernel");
        Assert.That(shared, Is.Not.Null);
        Assert.That(shared!.Services, Does.Contain("SharedKernel"));

        // 3. DeveloperTooling collapsed into DeveloperTooling
        var tooling = result.Domains.FirstOrDefault(d => d.Name == "DeveloperTooling");
        Assert.That(tooling, Is.Not.Null);
        Assert.That(tooling!.Services, Does.Contain("DbMigrationTool"));

        // 4. Orders services clustered together via shared table & directory namespace
        var ordersDomain = result.Domains.FirstOrDefault(d => d.Services.Contains("OrderProcessingService"));
        Assert.That(ordersDomain, Is.Not.Null);
        Assert.That(ordersDomain!.Services, Does.Contain("OrderDispatchWorker"));

        // 5. Billing services clustered together via shared table & directory namespace
        var billingDomain = result.Domains.FirstOrDefault(d => d.Services.Contains("BillingService"));
        Assert.That(billingDomain, Is.Not.Null);
        Assert.That(billingDomain!.Services, Does.Contain("InvoiceGenerator"));
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
            Assert.That(config, Is.Not.Null);
            Assert.That(config!.Domains, Has.Count.EqualTo(1));
            Assert.That(config.Domains[0].Name, Is.EqualTo("Finance"));
            Assert.That(config.Domains[0].Description, Is.EqualTo("Accounting and ledger"));
            Assert.That(config.Domains[0].Keywords, Does.Contain("ledger"));
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

        Assert.That(candidates.Any(c => c.Name == "OrderQueryService"), Is.True);
        Assert.That(candidates.Any(c => c.Name == "get-bundles.query.sql"), Is.False);
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
        Assert.That(domainsBefore.Contains("GhostDomain"), Is.True);

        // Purge orphan intent
        await client.PurgeIntentsByPathsAsync(["scripts/orphan.js"]);

        // Apply cache again: orphan Domain node must be cleaned up
        await client.ApplyCachedIntentsToGraphAsync("ws");

        var domainsAfter = await client.ExecuteQueryAsync("MATCH (d:Domain) RETURN d.name AS name");
        Assert.That(domainsAfter.Contains("GhostDomain"), Is.False);
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
        Assert.That(domains.Contains("SqlDomain"), Is.False);

        var intents = await client.LoadExistingIntentsAsync("ws");
        Assert.That(intents.Any(i => i.FilePath.EndsWith(".sql")), Is.False);
    }

    [Test]
    public void CanonicalizeDomain_CorrectlyMapsMicroservicesAndTablesToProblemSpaces()
    {
        // 1. Service name normalization into clean PascalCase domain
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "internal-service-approval"), Is.EqualTo("Approval"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "internal-service-notifier"), Is.EqualTo("Notifier"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "internal-service-scheduler"), Is.EqualTo("Scheduler"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "internal-service-billing"), Is.EqualTo("Billing"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "integration-service-payment"), Is.EqualTo("Payment"));

        // 2. Directory structure extraction
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, null, "services/billing/invoicing-service"), Is.EqualTo("Billing"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, null, "src/identity/auth-service"), Is.EqualTo("Identity"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, null, "services/orders/order-processor"), Is.EqualTo("Orders"));

        // 3. Technical paths and ontology roles
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "button", "packages/ui/button"), Is.EqualTo("UserInterface"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "common-utils", role: "SharedLibrary"), Is.EqualTo("SharedKernel"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "cli-tool", role: "CliTool"), Is.EqualTo("DeveloperTooling"));
        Assert.That(WorkspaceConventions.CanonicalizeDomain(null, "unit-tests", role: "Test"), Is.EqualTo("TestingInfrastructure"));

        // 4. Universal Display formatting
        Assert.That(WorkspaceConventions.FormatDomainDisplayName("OperationsAndWorkflows"), Is.EqualTo("Operations & Workflows"));
        Assert.That(WorkspaceConventions.FormatDomainDisplayName("BillingAndPayments"), Is.EqualTo("Billing & Payments"));
        Assert.That(WorkspaceConventions.FormatDomainDisplayName("OrderManagement"), Is.EqualTo("Order Management"));
        Assert.That(WorkspaceConventions.FormatDomainDisplayName("IdentityAndAccess"), Is.EqualTo("Identity & Access"));
    }
}

