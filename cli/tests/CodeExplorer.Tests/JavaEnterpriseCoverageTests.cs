using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parsers.Java;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class JavaEnterpriseCoverageTests
{
    private const string TempPrefix = "ce-java-ent-";
    private const string GuidFormat = "N";
    private const string ParentId = "parent-id";

    private static List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();
        foreach (var node in nodes)
        {
            if (node is EndpointNode ep) result.Add(ep);
            result.AddRange(FindEndpointNodes(node.Children));
        }
        return result;
    }

    private static List<EntryPointNode> FindEntryPointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EntryPointNode>();
        foreach (var node in nodes)
        {
            if (node is EntryPointNode ep) result.Add(ep);
            result.AddRange(FindEntryPointNodes(node.Children));
        }
        return result;
    }

    private static List<ExternalServiceNode> FindExternalServiceNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<ExternalServiceNode>();
        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode ext) result.Add(ext);
            result.AddRange(FindExternalServiceNodes(node.Children));
        }
        return result;
    }

    private static List<Reference> CollectAllReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            if (node is CompositeNode cn && cn.References != null)
            {
                result.AddRange(cn.References);
            }
            result.AddRange(CollectAllReferences(node.Children));
        }
        return result;
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), TempPrefix + Guid.NewGuid().ToString(GuidFormat));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public async Task SpringDataJpaRepository_MapsEntityTableAndDerivedQueryLineage()
    {
        var tempDir = CreateTempDir();
        try
        {
            var entityPath = Path.Combine(tempDir, "OrderEntity.java");
            var repoPath = Path.Combine(tempDir, "OrderRepository.java");

            const string entityCode = """
                package com.enterprise.orders;

                import jakarta.persistence.Entity;
                import jakarta.persistence.Table;

                @Entity
                @Table(name = "orders")
                public class OrderEntity {
                    private Long id;
                    private String status;
                }
                """;

            const string repoCode = """
                package com.enterprise.orders;

                import org.springframework.data.jpa.repository.JpaRepository;
                import java.util.List;

                public interface OrderRepository extends JpaRepository<OrderEntity, Long> {
                    List<OrderEntity> findByStatus(String status);
                    void deleteByCustomerId(Long customerId);
                }
                """;

            await File.WriteAllTextAsync(entityPath, entityCode);
            await File.WriteAllTextAsync(repoPath, repoCode);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            // Simulate Layer 3 early declaration discovery across both files
            ((IFileParser)parser).DiscoverDeclarations(entityPath, entityCode, "orders-service", ctx);
            ((IFileParser)parser).DiscoverDeclarations(repoPath, repoCode, "orders-service", ctx);

            using var repoTree = await parser.ParseAsync(repoPath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(repoTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx);

            var refs = CollectAllReferences(repoTree.FileNode.Children);
            refs.Should().Contain(r => r.Kind == RelationshipKind.AccessedBy && r.TargetName == "OrderEntity");
            refs.Should().Contain(r => r.ScopeSymbolId == "findByStatus" && r.Kind == RelationshipKind.ReadsFrom && r.TargetName == "orders");
            refs.Should().Contain(r => r.ScopeSymbolId == "deleteByCustomerId" && r.Kind == RelationshipKind.WritesTo && r.TargetName == "orders");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SpringDataMongoRepository_MapsCollectionAndDerivedMethodLineage()
    {
        var tempDir = CreateTempDir();
        try
        {
            var docPath = Path.Combine(tempDir, "InventoryDoc.java");
            var repoPath = Path.Combine(tempDir, "InventoryRepository.java");

            const string docCode = """
                package com.enterprise.inventory;

                import org.springframework.data.mongodb.core.mapping.Document;

                @Document(collection = "inventory_items")
                public class InventoryDoc {
                    private String sku;
                }
                """;

            const string repoCode = """
                package com.enterprise.inventory;

                import org.springframework.data.mongodb.repository.MongoRepository;
                import java.util.List;

                public interface InventoryRepository extends MongoRepository<InventoryDoc, String> {
                    List<InventoryDoc> findBySku(String sku);
                    InventoryDoc saveInventory(InventoryDoc doc);
                }
                """;

            await File.WriteAllTextAsync(docPath, docCode);
            await File.WriteAllTextAsync(repoPath, repoCode);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            ((IFileParser)parser).DiscoverDeclarations(docPath, docCode, "inventory-service", ctx);
            ((IFileParser)parser).DiscoverDeclarations(repoPath, repoCode, "inventory-service", ctx);

            using var repoTree = await parser.ParseAsync(repoPath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(repoTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx);

            var refs = CollectAllReferences(repoTree.FileNode.Children);
            refs.Should().Contain(r => r.ScopeSymbolId == "findBySku" && r.Kind == RelationshipKind.ReadsFrom && r.TargetName == "inventory_items");
            refs.Should().Contain(r => r.ScopeSymbolId == "saveInventory" && r.Kind == RelationshipKind.WritesTo && r.TargetName == "inventory_items");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SpringJms_ListenerAndJmsTemplate_ExtractsEntryPointAndPubSub()
    {
        var tempDir = CreateTempDir();
        try
        {
            var filePath = Path.Combine(tempDir, "WarehouseJmsHandler.java");
            const string code = """
                package com.enterprise.jms;

                import org.springframework.jms.annotation.JmsListener;
                import org.springframework.jms.core.JmsTemplate;

                public class WarehouseJmsHandler {
                    private final JmsTemplate jmsTemplate;

                    @JmsListener(destination = "orders.inbound.queue")
                    public void onOrderReceived(String payload) {
                        jmsTemplate.convertAndSend("orders.outbound.queue", payload);
                    }
                }
                """;

            await File.WriteAllTextAsync(filePath, code);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            using var syntaxTree = await parser.ParseAsync(filePath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var entryPoints = FindEndpointNodes(syntaxTree.FileNode.Children);
            entryPoints.Should().Contain(ep => ep.Name.Contains("orders.inbound.queue"));

            var refs = CollectAllReferences(syntaxTree.FileNode.Children);
            refs.Should().Contain(r => r.Kind == RelationshipKind.SubscribesTo && r.TargetName.Contains("orders.inbound.queue"));
            refs.Should().Contain(r => r.Kind == RelationshipKind.PublishesTo && r.TargetName.Contains("orders.outbound.queue"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SoapJaxWsAndSpringWs_ExtractsEndpointsAndOutboundClient()
    {
        var tempDir = CreateTempDir();
        try
        {
            var filePath = Path.Combine(tempDir, "ShipmentSoapEndpoint.java");
            const string code = """
                package com.enterprise.soap;

                import jakarta.jws.WebMethod;
                import jakarta.jws.WebService;
                import org.springframework.ws.client.core.WebServiceTemplate;
                import org.springframework.ws.server.endpoint.annotation.Endpoint;
                import org.springframework.ws.server.endpoint.annotation.PayloadRoot;

                @Endpoint
                @WebService(name = "ShipmentService")
                public class ShipmentSoapEndpoint {
                    private WebServiceTemplate webServiceTemplate;

                    @WebMethod(operationName = "getShipment")
                    public String getShipment(String id) {
                        return (String) webServiceTemplate.marshalSendAndReceive("http://erp.internal/ws/shipment", id);
                    }

                    @PayloadRoot(namespace = "http://enterprise.com/ws", localPart = "CreatePlanRequest")
                    public void createPlan(Object req) {
                    }
                }
                """;

            await File.WriteAllTextAsync(filePath, code);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            using var syntaxTree = await parser.ParseAsync(filePath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var entryPoints = FindEndpointNodes(syntaxTree.FileNode.Children);
            entryPoints.Should().Contain(ep => ep.Name.Contains("getShipment"));
            entryPoints.Should().Contain(ep => ep.Name.Contains("CreatePlanRequest"));

            var externalServices = FindExternalServiceNodes(syntaxTree.FileNode.Children);
            externalServices.Should().Contain(es => es.Name.Contains("erp.internal"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SpringRuntime_ScheduledAsyncAndCacheAnnotations_ExtractsEntryPointsAndSinks()
    {
        var tempDir = CreateTempDir();
        try
        {
            var filePath = Path.Combine(tempDir, "PlannerSyncJob.java");
            const string code = """
                package com.enterprise.jobs;

                import org.springframework.cache.annotation.CacheEvict;
                import org.springframework.cache.annotation.Cacheable;
                import org.springframework.scheduling.annotation.Async;
                import org.springframework.scheduling.annotation.Scheduled;

                public class PlannerSyncJob {
                    @Scheduled(cron = "0 */5 * * * *")
                    public void reconcilePlans() {
                    }

                    @Scheduled(fixedRate = 30000)
                    public void refreshMetrics() {
                    }

                    @Async
                    @Cacheable("planner-grid")
                    public String loadGrid(String id) {
                        return id;
                    }

                    @CacheEvict("planner-grid")
                    public void clearGrid() {
                    }
                }
                """;

            await File.WriteAllTextAsync(filePath, code);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            using var syntaxTree = await parser.ParseAsync(filePath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var entryPoints = FindEntryPointNodes(syntaxTree.FileNode.Children);
            entryPoints.Should().Contain(ep => ep.Name.Contains("reconcilePlans") && ep.Name.Contains("0 */5 * * * *"));
            entryPoints.Should().Contain(ep => ep.Name.Contains("refreshMetrics"));

            var refs = CollectAllReferences(syntaxTree.FileNode.Children);
            refs.Should().Contain(r => r.Kind == RelationshipKind.Calls && r.TargetName == "spring-async:Async");
            refs.Should().Contain(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "spring-cache:Cacheable");
            refs.Should().Contain(r => r.Kind == RelationshipKind.UsesDb && r.TargetName == "spring-cache:CacheEvict");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task LombokFieldDiAndRecordParameters_EmitRawTypeBindings()
    {
        var tempDir = CreateTempDir();
        try
        {
            var filePath = Path.Combine(tempDir, "OrderOrchestrator.java");
            const string code = """
                package com.enterprise.orders;

                import lombok.RequiredArgsConstructor;
                import org.springframework.beans.factory.annotation.Autowired;

                @RequiredArgsConstructor
                public class OrderOrchestrator {
                    private final OrderRepository orderRepository;

                    @Autowired
                    private PaymentClient paymentClient;

                    public void process(PricingEngine pricingEngine) {
                        orderRepository.findByStatus("OPEN");
                        paymentClient.charge();
                        pricingEngine.calculate();
                    }
                }
                """;

            await File.WriteAllTextAsync(filePath, code);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            using var syntaxTree = await parser.ParseAsync(filePath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            syntaxTree.RawTypeBindings.Should().Contain(b => b.VariableName == "orderRepository" && b.TypeName == "OrderRepository");
            syntaxTree.RawTypeBindings.Should().Contain(b => b.VariableName == "paymentClient" && b.TypeName == "PaymentClient");
            syntaxTree.RawTypeBindings.Should().Contain(b => b.VariableName == "pricingEngine" && b.TypeName == "PricingEngine");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Spring6HttpInterfacesAndWebFluxRouterFunctions_ExtractEndpointsAndClients()
    {
        var tempDir = CreateTempDir();
        try
        {
            var routerPath = Path.Combine(tempDir, "PlannerRouter.java");
            const string routerCode = """
                package com.enterprise.web;

                import org.springframework.web.reactive.function.server.RouterFunction;
                import org.springframework.web.reactive.function.server.RouterFunctions;
                import org.springframework.web.reactive.function.server.ServerResponse;
                import org.springframework.web.client.RestClient;

                public class PlannerRouter {
                    private RestClient restClient;

                    public RouterFunction<ServerResponse> routes(PlannerHandler handler) {
                        return RouterFunctions.route()
                            .GET("/api/plans", handler::list)
                            .POST("/api/plans", handler::create)
                            .build();
                    }

                    public String fetchWarehouse() {
                        return restClient.get()
                            .uri("https://warehouse.internal/v2/stock")
                            .retrieve()
                            .toEntity(String.class)
                            .getBody();
                    }
                }
                """;

            await File.WriteAllTextAsync(routerPath, routerCode);

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);
            var parser = new JavaFileParser();

            using var syntaxTree = await parser.ParseAsync(routerPath, ParentId, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var entryPoints = FindEndpointNodes(syntaxTree.FileNode.Children);
            entryPoints.Should().Contain(ep => ep.Name == "GET:/api/plans");
            entryPoints.Should().Contain(ep => ep.Name == "POST:/api/plans");

            var externalServices = FindExternalServiceNodes(syntaxTree.FileNode.Children);
            externalServices.Should().Contain(es => es.Name.Contains("warehouse.internal"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task MavenParentPropertiesAndGradleVersionCatalog_ResolveDependenciesAccurately()
    {
        var tempDir = CreateTempDir();
        try
        {
            // 1. Multi-module Maven with parent POM properties & ${project.groupId}
            var mavenRoot = Path.Combine(tempDir, "maven-app");
            var mavenService = Path.Combine(mavenRoot, "planner-service");
            Directory.CreateDirectory(mavenService);

            await File.WriteAllTextAsync(Path.Combine(mavenRoot, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                    <groupId>com.enterprise</groupId>
                    <artifactId>planner-parent</artifactId>
                    <version>2.4.0</version>
                    <packaging>pom</packaging>
                    <properties>
                        <spring.boot.version>3.3.1</spring.boot.version>
                    </properties>
                    <modules>
                        <module>planner-service</module>
                    </modules>
                </project>
                """);

            await File.WriteAllTextAsync(Path.Combine(mavenService, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                    <parent>
                        <groupId>com.enterprise</groupId>
                        <artifactId>planner-parent</artifactId>
                        <version>2.4.0</version>
                    </parent>
                    <artifactId>planner-service</artifactId>
                    <dependencies>
                        <dependency>
                            <groupId>${project.groupId}</groupId>
                            <artifactId>planner-domain</artifactId>
                            <version>${project.version}</version>
                        </dependency>
                        <dependency>
                            <groupId>org.springframework.boot</groupId>
                            <artifactId>spring-boot-starter-web</artifactId>
                            <version>${spring.boot.version}</version>
                        </dependency>
                    </dependencies>
                </project>
                """);

            var projectParser = new JavaProjectParser();
            var mavenDeps = await projectParser.ParseDependenciesAsync(mavenService);
            mavenDeps.ExternalPackages.Should().Contain(p => p.Name == "com.enterprise:planner-domain" && p.Version == "2.4.0");
            mavenDeps.ExternalPackages.Should().Contain(p => p.Name == "org.springframework.boot:spring-boot-starter-web" && p.Version == "3.3.1");

            // 2. Multi-module Gradle with nested :services:orders, sibling project(":common"), and libs.versions.toml
            var gradleRoot = Path.Combine(tempDir, "gradle-app");
            var gradleCommon = Path.Combine(gradleRoot, "common");
            var gradleOrders = Path.Combine(gradleRoot, "services", "orders");
            var gradleMeta = Path.Combine(gradleRoot, "gradle");
            Directory.CreateDirectory(gradleCommon);
            Directory.CreateDirectory(gradleOrders);
            Directory.CreateDirectory(gradleMeta);

            await File.WriteAllTextAsync(Path.Combine(gradleRoot, "settings.gradle.kts"), """
                rootProject.name = "gradle-app"
                include(":common", ":services:orders")
                """);

            await File.WriteAllTextAsync(Path.Combine(gradleMeta, "libs.versions.toml"), """
                [versions]
                springBoot = "3.3.2"

                [libraries]
                spring-boot-web = { module = "org.springframework.boot:spring-boot-starter-web", version.ref = "springBoot" }
                """);

            await File.WriteAllTextAsync(Path.Combine(gradleOrders, "build.gradle.kts"), """
                dependencies {
                    implementation(project(":common"))
                    implementation(libs.spring.boot.web)
                }
                """);

            var rootDeps = await projectParser.ParseDependenciesAsync(gradleRoot);
            rootDeps.LocalProjectPaths.Should().Contain(p => p.EndsWith("/common", StringComparison.OrdinalIgnoreCase));
            rootDeps.LocalProjectPaths.Should().Contain(p => p.EndsWith("/services/orders", StringComparison.OrdinalIgnoreCase));

            var ordersDeps = await projectParser.ParseDependenciesAsync(gradleOrders);
            ordersDeps.LocalProjectPaths.Should().Contain(p => p.EndsWith("/common", StringComparison.OrdinalIgnoreCase));
            ordersDeps.ExternalPackages.Should().Contain(p => p.Name == "org.springframework.boot:spring-boot-starter-web" && p.Version == "3.3.2");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task EndToEnd_SpringDataJpa_IndexesFullLayer1To5TableLineageInSqlite()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        var srcDir = Path.Combine(tempDir, "src", "main", "java", "com", "enterprise", "orders");
        Directory.CreateDirectory(srcDir);
        var dbPath = Path.Combine(tempDir, "graph.db");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                    <groupId>com.enterprise</groupId>
                    <artifactId>orders-service</artifactId>
                    <version>1.0.0</version>
                    <dependencies>
                        <dependency>
                            <groupId>org.springframework.boot</groupId>
                            <artifactId>spring-boot-starter-data-jpa</artifactId>
                            <version>3.3.0</version>
                        </dependency>
                    </dependencies>
                </project>
                """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderEntity.java"), """
                package com.enterprise.orders;

                import jakarta.persistence.Entity;
                import jakarta.persistence.Table;

                @Entity
                @Table(name = "orders")
                public class OrderEntity {
                    private Long id;
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderRepository.java"), """
                package com.enterprise.orders;

                import org.springframework.data.jpa.repository.JpaRepository;
                import java.util.List;

                public interface OrderRepository extends JpaRepository<OrderEntity, Long> {
                    List<OrderEntity> findByStatus(String status);
                    void deleteByCustomerId(Long customerId);
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "OrderService.java"), """
                package com.enterprise.orders;

                import java.util.List;

                public class OrderService {
                    private final OrderRepository orderRepository;

                    public OrderService(OrderRepository orderRepository) {
                        this.orderRepository = orderRepository;
                    }

                    public List<OrderEntity> loadOpenOrders() {
                        return orderRepository.findByStatus("OPEN");
                    }

                    public void purgeCustomer(Long id) {
                        orderRepository.deleteByCustomerId(id);
                    }
                }
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var readRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[:READS_FROM]->(t:Table {name: 'orders'}) RETURN fn.name AS name");
            var readNames = readRes.Rows.Select(r => r.GetStringProp("name")).ToList();
            readNames.Should().Contain("loadOpenOrders");

            var writeRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[:WRITES_TO]->(t:Table {name: 'orders'}) RETURN fn.name AS name");
            var writeNames = writeRes.Rows.Select(r => r.GetStringProp("name")).ToList();
            writeNames.Should().Contain("purgeCustomer");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* SQLite file release race on Windows */ }
            }
        }
    }

    [Test]
    public async Task EndToEnd_RedisReceiverScoping_CSharpRepoWrites_AndMutableStaticPrecision()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        var tsDir = Path.Combine(tempDir, "ts-service");
        var csDir = Path.Combine(tempDir, "cs-service");
        Directory.CreateDirectory(Path.Combine(tsDir, "src"));
        Directory.CreateDirectory(csDir);
        var dbPath = Path.Combine(tempDir, "graph.db");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tsDir, "package.json"), """
                {
                  "name": "ts-service",
                  "dependencies": { "redis": "^4.0.0" }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tsDir, "src", "postSlug.ts"), """
                export interface IPostRepo {
                  exists(slug: string): Promise<boolean>;
                }
                export async function checkSlug(postRepo: IPostRepo, redisClient: any, slug: string) {
                  const inRedis = await redisClient.exists(slug);
                  if (inRedis) return true;
                  return await postRepo.exists(slug);
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(csDir, "Service.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup><PackageReference Include="Ardalis.Specification" Version="8.0.0" /></ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(Path.Combine(csDir, "Contributor.cs"), """
                using System.Collections.Concurrent;

                namespace Sample;

                public class Contributor { public int Id { get; set; } }
                public interface IRepository<T> { Task UpdateAsync(T entity); }

                public class GetContributorHandler(IRepository<Contributor> _repository)
                {
                    private static readonly ConcurrentDictionary<int, int> _fetchCounts = new();
                    public static string HandlerName => "GetContributor";

                    public async Task Handle(Contributor entity)
                    {
                        await _repository.UpdateAsync(entity);
                    }
                }
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            // 1. Redis TS: redisClient.exists IS a Redis query, postRepo.exists is NOT
            var queries = await client.ExecuteCypherAsync("MATCH (q:Query) RETURN q.name AS name");
            var queryNames = queries.Rows.Select(r => r.GetStringProp("name")).ToList();
            queryNames.Should().NotContain(n => n.Contains("postRepo.exists"));
            queryNames.Should().Contain(n => n.Contains("redisClient.exists"));

            // 2. C# static readonly ConcurrentDictionary is mutable (is_constant = false), get-only property is constant (is_constant = true)
            var members = await client.ExecuteCypherAsync(
                "MATCH (m:Member) WHERE m.name IN ['_fetchCounts', 'HandlerName'] RETURN m.name AS name, m.is_static AS isStatic, m.is_constant AS isConst");
            var fetchRow = members.Rows.First(r => r.GetStringProp("name") == "_fetchCounts");
            fetchRow.GetBoolProp("isStatic").Should().BeTrue();
            fetchRow.GetBoolProp("isConst").Should().BeFalse();

            var propRow = members.Rows.First(r => r.GetStringProp("name") == "HandlerName");
            propRow.GetBoolProp("isStatic").Should().BeTrue();
            propRow.GetBoolProp("isConst").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* SQLite file release race on Windows */ }
            }
        }
    }
}
