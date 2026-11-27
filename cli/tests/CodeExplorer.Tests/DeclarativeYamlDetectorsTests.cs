using CodeExplorer.Commands;
using CodeExplorer.Options;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class DeclarativeYamlDetectorsTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [Test]
    public async Task CustomYamlDetector_ExtractsEndpointsTopicsTables_InvalidatesCacheOnEdit_AndIsolatesWorkspaces()
    {
        var wsA = Path.Combine(Path.GetTempPath(), "ce_yaml_det_a_" + Guid.NewGuid().ToString("N"));
        var wsB = Path.Combine(Path.GetTempPath(), "ce_yaml_det_b_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(wsA, ".codeexplorer", "detectors"));
        Directory.CreateDirectory(Path.Combine(wsA, "src", "main", "java", "com", "acme", "app"));
        Directory.CreateDirectory(Path.Combine(wsB, ".codeexplorer"));
        Directory.CreateDirectory(Path.Combine(wsB, "src", "main", "java", "com", "acme", "app"));

        try
        {
            var pomXml = """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                  <modelVersion>4.0.0</modelVersion>
                  <groupId>com.acme</groupId>
                  <artifactId>order-service</artifactId>
                  <version>1.0.0</version>
                  <dependencies>
                    <dependency>
                      <groupId>com.acme.rpc</groupId>
                      <artifactId>acme-rpc-core</artifactId>
                      <version>2.0.0</version>
                    </dependency>
                  </dependencies>
                </project>
                """;

            var javaSource = """
                package com.acme.app;
                import com.acme.rpc.RpcService;
                import com.acme.rpc.RpcPost;
                import com.acme.rpc.RpcDelete;
                import com.acme.rpc.OnAcmeEvent;
                import com.acme.rpc.AcmeTable;
                import com.acme.rpc.AcmeEventBus;

                @AcmeTable(name = "acme_orders")
                public class OrderRecord {
                    private Long id;
                }

                @RpcService("/orders")
                public class OrderController {
                    private AcmeEventBus eventBus;

                    @RpcPost("/create")
                    public void createOrder() {
                        eventBus.emitAcme("order.created");
                    }

                    @RpcDelete("/remove")
                    public void removeOrder() {
                    }

                    @OnAcmeEvent("order.cancelled")
                    public void onCancelled() {
                    }
                }
                """;

            await File.WriteAllTextAsync(Path.Combine(wsA, "pom.xml"), pomXml);
            await File.WriteAllTextAsync(Path.Combine(wsA, "src", "main", "java", "com", "acme", "app", "OrderController.java"), javaSource);
            await File.WriteAllTextAsync(Path.Combine(wsB, "pom.xml"), pomXml);
            await File.WriteAllTextAsync(Path.Combine(wsB, "src", "main", "java", "com", "acme", "app", "OrderController.java"), javaSource);

            var detectorPath = Path.Combine(wsA, ".codeexplorer", "detectors", "acme-rpc.yaml");
            await File.WriteAllTextAsync(detectorPath, """
                schema_version: 1
                id: acme-internal-rpc
                name: Acme Internal RPC
                languages: [java]
                type: framework
                library_role: web_service
                supported_patterns:
                  - "com.acme.rpc*"
                  - "*acme-rpc-core*"

                annotation_endpoints:
                  - annotation_to_http_method:
                      RpcPost: POST
                    route_prefix_annotation_names: [RpcService]

                orm_tables:
                  - table_annotation_names: [AcmeTable]

                messaging_brokers:
                  - protocol: acme-bus
                    listener_annotation_names: [OnAcmeEvent]
                    publish_method_names: [emitAcme]
                    publish_receiver_substrings: [EventBus, eventBus]
                """);

            // 1. Scan Workspace A -> custom YAML detector extracts Endpoint, Table, Topics, and Service classification
            await using (var dbA = new SqliteGraphClient(Path.Combine(wsA, ".codeexplorer", "graph.db")))
            {
                var indexerA = new WorkspaceIndexer(dbA);
                await indexerA.IndexAsync(wsA, clear: true);

                var eps1 = (await dbA.ExecuteCypherAsync("MATCH (e:Endpoint) RETURN e.name AS name"))
                    .Rows.Select(r => r.GetStringProp("name")).ToList();
                eps1.Should().Contain("POST:/orders/create");
                eps1.Should().NotContain("DELETE:/orders/remove");

                var tables = (await dbA.ExecuteCypherAsync("MATCH (t:Table) RETURN t.name AS name"))
                    .Rows.Select(r => r.GetStringProp("name")).ToList();
                tables.Should().Contain("acme_orders");

                var topics = (await dbA.ExecuteCypherAsync("MATCH (t:Topic) RETURN t.name AS name"))
                    .Rows.Select(r => r.GetStringProp("name")).ToList();
                topics.Should().Contain(t => t.EndsWith("order.created", StringComparison.Ordinal));
                topics.Should().Contain(t => t.EndsWith("order.cancelled", StringComparison.Ordinal));

                var services = (await dbA.ExecuteCypherAsync("MATCH (s:Service) RETURN s.name AS name"))
                    .Rows.Select(r => r.GetStringProp("name")).ToList();
                services.Should().NotBeEmpty();

                // ce doctor recognizes com.acme.rpc:acme-rpc-core as matched
                var doctor = new WorkspaceDoctorService();
                var report = await doctor.AnalyzeAsync(dbA, wsA);
                report.Gaps!.UnmatchedPackages.Should().NotContain(p => p.Name.Contains("acme-rpc-core", StringComparison.OrdinalIgnoreCase));

                // 2. Edit YAML detector to add RpcDelete: DELETE and run INCREMENTAL scan (no --clear)
                await File.WriteAllTextAsync(detectorPath, """
                    schema_version: 1
                    id: acme-internal-rpc
                    name: Acme Internal RPC
                    languages: [java]
                    type: framework
                    library_role: web_service
                    supported_patterns:
                      - "com.acme.rpc*"
                      - "*acme-rpc-core*"

                    annotation_endpoints:
                      - annotation_to_http_method:
                          RpcPost: POST
                          RpcDelete: DELETE
                        route_prefix_annotation_names: [RpcService]
                    """);

                var reindexed = (await indexerA.IndexAsync(wsA)).HasChanges;
                reindexed.Should().BeTrue();

                var eps2 = (await dbA.ExecuteCypherAsync("MATCH (e:Endpoint) RETURN e.name AS name"))
                    .Rows.Select(r => r.GetStringProp("name")).ToList();
                eps2.Should().Contain("POST:/orders/create");
                eps2.Should().Contain("DELETE:/orders/remove");
            }

            // 3. Scan Workspace B (no custom detectors) -> zero leakage from Workspace A
            await using (var dbB = new SqliteGraphClient(Path.Combine(wsB, ".codeexplorer", "graph.db")))
            {
                var indexerB = new WorkspaceIndexer(dbB);
                await indexerB.IndexAsync(wsB, clear: true);

                var epsB = (await dbB.ExecuteCypherAsync("MATCH (e:Endpoint) RETURN e.name AS name")).Rows;
                epsB.Should().BeEmpty();
            }
        }
        finally
        {
            if (Directory.Exists(wsA)) Directory.Delete(wsA, recursive: true);
            if (Directory.Exists(wsB)) Directory.Delete(wsB, recursive: true);
        }
    }

    [Test]
    public async Task Validation_EnforcesOntologyRelations_BuiltInCollisionOverride_AndCliCommands()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_yaml_det_val_" + Guid.NewGuid().ToString("N"));
        var detDir = Path.Combine(tempDir, ".codeexplorer", "detectors");
        Directory.CreateDirectory(detDir);

        try
        {
            // 1. Colliding with built-in 'dubbo' without override: true fails validation & scan
            var filePath = Path.Combine(detDir, "bad.yaml");
            await File.WriteAllTextAsync(filePath, """
                schema_version: 1
                id: dubbo
                name: Accidental Shadow
                languages: [java]
                supported_patterns: ["org.apache.dubbo*"]
                annotation_sinks:
                  - annotation_names: [CustomAudit]
                    target_relation: INVALID_EDGE_NAME
                """);

            var act = () => WorkspaceDetectorLoader.LoadForWorkspace(tempDir, strict: true, globalDetectorsDirOverride: string.Empty);
            act.Should().Throw<DetectorValidationException>()
                .Which.Errors.Should().HaveCountGreaterThanOrEqualTo(2);

            var valExit = await DetectorCommandHandler.HandleAsync(new DetectorOptions
            {
                Action = "validate",
                Dir = tempDir
            });
            valExit.Should().Be(1);

            // 2. Setting override: true + valid relation succeeds, but warns about shadowing C# CustomAstRules
            await File.WriteAllTextAsync(filePath, """
                schema_version: 1
                id: dubbo
                name: Intentional Dubbo Override
                languages: [csharp, java]
                override: true
                supported_patterns: ["org.apache.dubbo*"]
                annotation_sinks:
                  - annotation_names: [CustomAudit]
                    target_relation: CALLS
                """);

            var (catalog, issues) = WorkspaceDetectorLoader.ValidateWorkspace(tempDir, globalDetectorsDirOverride: string.Empty);
            issues.Where(i => !i.IsWarning).Should().BeEmpty();
            issues.Where(i => i.IsWarning).Should().ContainSingle(w => w.Message.Contains("imperative C# AST rule", StringComparison.Ordinal));
            catalog.CustomDetectors.Should().ContainSingle(d => d.OverridesBuiltIn && d.ShadowsImperativeRules);
            catalog.CustomDetectors[0].TargetLanguages.Should().Contain("c-sharp");

            // 3. CLI schema & list commands succeed
            var schemaJson = DeclarativeDetectorSchemaGenerator.BuildJsonSchema();
            schemaJson.Should().Contain("annotation_endpoints");
            schemaJson.Should().Contain("class_hierarchy_sinks");

            var listExit = await DetectorCommandHandler.HandleAsync(new DetectorOptions
            {
                Action = "list",
                CustomOnly = true,
                Dir = tempDir
            });
            listExit.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
