using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Core.Semantics;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ParameterNatureProvenanceTests
{
    [Test]
    public void Catalog_Resolves_Database_Methods_As_SqlQuery()
    {
        PlatformParameterCatalog.ResolveParameterNature("Query", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("QueryAsync", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("Execute", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("ExecuteAsync", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("ExecuteSqlRaw", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("FromSqlRaw", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("FromSql", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("conn.ExecuteReader", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("db.raw", 0).Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveParameterNature("client.prepareStatement", 0).Should().Be(ParameterNature.SqlQuery);
    }

    [Test]
    public void Catalog_Resolves_Http_Methods_As_UrlOrRoute()
    {
        PlatformParameterCatalog.ResolveParameterNature("GetAsync", 0).Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveParameterNature("PostAsync", 0).Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveParameterNature("fetch", 0).Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveParameterNature("axios.get", 0).Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveParameterNature("client.GetStringAsync", 0).Should().Be(ParameterNature.UrlOrRoute);
    }

    [Test]
    public void Catalog_Resolves_Connection_Methods_As_ConnectionString()
    {
        PlatformParameterCatalog.ResolveParameterNature("SqlConnection", 0).Should().Be(ParameterNature.ConnectionString);
        PlatformParameterCatalog.ResolveParameterNature("NpgsqlConnection", 0).Should().Be(ParameterNature.ConnectionString);
        PlatformParameterCatalog.ResolveParameterNature("UseSqlServer", 0).Should().Be(ParameterNature.ConnectionString);
        PlatformParameterCatalog.ResolveParameterNature("createPool", 0).Should().Be(ParameterNature.ConnectionString);
    }

    [Test]
    public void Catalog_Resolves_File_Methods_As_FilePath()
    {
        PlatformParameterCatalog.ResolveParameterNature("ReadAllText", 0).Should().Be(ParameterNature.FilePath);
        PlatformParameterCatalog.ResolveParameterNature("File.WriteAllLines", 0).Should().Be(ParameterNature.FilePath);
        PlatformParameterCatalog.ResolveParameterNature("readFile", 0).Should().Be(ParameterNature.FilePath);
    }

    [Test]
    public void ClassifyByName_Detects_Nature_From_Identifier()
    {
        PlatformParameterCatalog.ClassifyByName("sqlQuery").Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ClassifyByName("dbQuery").Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ClassifyByName("selectStmt").Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ClassifyByName("connectionString").Should().Be(ParameterNature.ConnectionString);
        PlatformParameterCatalog.ClassifyByName("apiUrl").Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ClassifyByName("userProfileRoute").Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ClassifyByName("configFilePath").Should().Be(ParameterNature.FilePath);
        PlatformParameterCatalog.ClassifyByName("eventTopic").Should().Be(ParameterNature.TopicOrQueue);
        PlatformParameterCatalog.ClassifyByName("authSecret").Should().Be(ParameterNature.SecretOrHeader);
        PlatformParameterCatalog.ClassifyByName("userName").Should().Be(ParameterNature.Unspecified);
    }

    [Test]
    public void BackwardParameterSlicer_Traces_Literal_CallArgument()
    {
        var call = new CallExprSyntax
        {
            MethodName = "Query",
            Arguments =
            [
                new ArgumentSyntax
                {
                    Value = new LiteralExprSyntax
                    {
                        LiteralKind = LiteralSyntaxKind.String,
                        ValueText = "SELECT id, name FROM users"
                    }
                }
            ]
        };

        var slice = BackwardParameterSlicer.SliceArgument(call, 0);

        slice.Nature.Should().Be(ParameterNature.SqlQuery);
        slice.IsResolved.Should().BeTrue();
        slice.ResolvedValue.Should().Be("SELECT id, name FROM users");
        slice.Origins.Should().ContainSingle();
    }

    [Test]
    public void BackwardParameterSlicer_Traces_ReachingDefinition_Variable()
    {
        var varDecl = new VariableDeclSyntax
        {
            Name = "mySql",
            Initializer = new LiteralExprSyntax
            {
                LiteralKind = LiteralSyntaxKind.String,
                ValueText = "SELECT COUNT(*) FROM orders"
            },
            Span = new TextSpan(0, 40, 1, 0, 1, 40)
        };

        var ident = new IdentifierExprSyntax
        {
            Name = "mySql",
            Span = new TextSpan(50, 55, 2, 10, 2, 15)
        };

        var call = new CallExprSyntax
        {
            MethodName = "ExecuteAsync",
            Arguments = [new ArgumentSyntax { Value = ident }],
            Span = new TextSpan(45, 60, 2, 0, 2, 15)
        };

        var block = new BlockStmtSyntax
        {
            Children = [varDecl, call],
            Span = new TextSpan(0, 100, 1, 0, 3, 0)
        };

        varDecl.Parent = block;
        call.Parent = block;
        ident.Parent = call.Arguments[0];
        call.Arguments[0].Parent = call;

        var slice = BackwardParameterSlicer.SliceArgument(call, 0);

        slice.Nature.Should().Be(ParameterNature.SqlQuery);
        slice.Origins.Should().Contain(varDecl);
    }

    [Test]
    public void CallLiteralNode_Maintains_Assigned_Nature()
    {
        var node = new CallLiteralNode(
            "ws:lit:file.cs:10:5",
            "sql",
            "SELECT * FROM accounts",
            "QueryAsync",
            0,
            "Services/AccountService.cs",
            10,
            5,
            false,
            null,
            null,
            "Business",
            false,
            ParameterNature.SqlQuery);

        node.Nature.Should().Be(ParameterNature.SqlQuery);
        node.Callee.Should().Be("QueryAsync");
        node.ArgIndex.Should().Be(0);
    }

    [Test]
    public void Catalog_ObjectPropertyRoles_Distinguishes_DbQuery_From_UiText()
    {
        PlatformParameterCatalog.ResolveObjectPropertyNature("client.query", "text").Should().Be(ParameterNature.SqlQuery);
        PlatformParameterCatalog.ResolveObjectPropertyNature("button.setOptions", "text").Should().Be(ParameterNature.Unspecified);
        PlatformParameterCatalog.ResolveObjectPropertyNature("axios.request", "url").Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveObjectPropertyNature("axios.create", "baseURL").Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveObjectPropertyNature("svg.render", "path").Should().Be(ParameterNature.Unspecified);
        PlatformParameterCatalog.ResolveObjectPropertyNature("http.request", "path").Should().Be(ParameterNature.UrlOrRoute);
        PlatformParameterCatalog.ResolveObjectPropertyNature("broker.send", "routingKey").Should().Be(ParameterNature.TopicOrQueue);
    }

    [Test]
    public void ConstantValueClassifier_Classifies_4D_SemanticValues()
    {
        ConstantValueClassifier.Classify(ParameterNature.SqlQuery, "/* comment */ SELECT id FROM orders").Should().Be(ConstantValueKind.SqlDml);
        ConstantValueClassifier.Classify(ParameterNature.SqlQuery, "CREATE TABLE audit_log (id INT)").Should().Be(ConstantValueKind.SqlDdl);
        ConstantValueClassifier.Classify(ParameterNature.SqlQuery, "EXEC dbo.usp_ProcessBatch").Should().Be(ConstantValueKind.SqlExec);
        ConstantValueClassifier.Classify(ParameterNature.UrlOrRoute, "/v1/orders/charge").Should().Be(ConstantValueKind.UrlRelativeRoute);
        ConstantValueClassifier.Classify(ParameterNature.UrlOrRoute, "https://api.stripe.com/v1/charges").Should().Be(ConstantValueKind.UrlPublicDomain);
        ConstantValueClassifier.Classify(ParameterNature.UrlOrRoute, "http://localhost:8080/health").Should().Be(ConstantValueKind.UrlLoopback);
        ConstantValueClassifier.Classify(ParameterNature.ConnectionString, "Host=db.internal;Database=billing").Should().Be(ConstantValueKind.ConnStrRelational);
    }

    [Test]
    public async Task GraphIndexing_InterproceduralSqlWrappers_ColorsParameters_FlowsTo_And_GroundsTablesDownward()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_sem_args_cs_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(tempDir);

        try
        {
            var projDir = Path.Combine(tempDir, "BillingService").Replace('\\', '/');
            Directory.CreateDirectory(projDir);

            await File.WriteAllTextAsync(Path.Combine(projDir, "BillingService.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Dapper" Version="2.1.35" />
                    <PackageReference Include="Npgsql" Version="8.0.0" />
                  </ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(Path.Combine(projDir, "OrderRepository.cs"), """
                using System.Data;
                using System.Threading.Tasks;
                using Dapper;

                namespace BillingService;

                public interface IOrderRepository
                {
                    Task QueryReadOnlyAsync(string rawSql);
                    Task FindAllFromTableAsync(string tableName);
                }

                public class OrderRepository : IOrderRepository
                {
                    private readonly IDbConnection _conn;

                    public OrderRepository(IDbConnection conn) => _conn = conn;

                    public async Task QueryReadOnlyAsync(string rawSql)
                    {
                        await _conn.QueryAsync(rawSql);
                    }

                    public async Task FindAllFromTableAsync(string tableName)
                    {
                        await _conn.QueryAsync("SELECT * FROM " + tableName);
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(projDir, "OrderAppService.cs"), """
                using System.Threading.Tasks;
                using Microsoft.Extensions.Logging;

                namespace BillingService;

                public class OrderAppService
                {
                    private const string OpenOrdersSql = "SELECT id, total FROM customer_orders WHERE status = 'OPEN'";
                    private readonly IOrderRepository _repo;
                    private readonly ILogger<OrderAppService> _logger;

                    public OrderAppService(IOrderRepository repo, ILogger<OrderAppService> logger)
                    {
                        _repo = repo;
                        _logger = logger;
                    }

                    public async Task LoadDashboardAsync()
                    {
                        _logger.LogInformation("Select item from menu to update account");
                        await _repo.QueryReadOnlyAsync(OpenOrdersSql);
                        await _repo.FindAllFromTableAsync("billing_invoices");
                    }
                }
                """);

            var dbPath = Path.Combine(tempDir, "graph.db").Replace('\\', '/');
            await using var client = new SqliteGraphClient(dbPath);
            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            // 1. Verify colored :Parameter nodes exist for rawSql & tableName, and NOT for logger/conn
            var paramRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[:HAS_PARAMETER]->(p:Parameter) RETURN fn.name AS fnName, p.name AS paramName, p.nature AS nature");
            paramRes.Count.Should().BeGreaterThanOrEqualTo(2);
            paramRes.Rows.Should().Contain(r => r.GetStringProp("paramName") == "rawSql" && r.GetStringProp("nature") == "SqlQuery");
            paramRes.Rows.Should().Contain(r => r.GetStringProp("paramName") == "tableName" && r.GetStringProp("nature") == "SqlQuery");

            // 2. Verify zero :CallArgument noise on LogInformation("Select item from menu to update account")
            var noiseRes = await client.ExecuteCypherAsync(
                "MATCH (arg:CallArgument) WHERE arg.callee CONTAINS 'LogInformation' RETURN arg.id AS id");
            noiseRes.Count.Should().Be(0, "Non-SQL logging calls must never materialize semantic CallArgument nodes");

            // 3. Verify interprocedural FLOWS_TO chain from LoadDashboardAsync's CallArgument to QueryReadOnlyAsync's Parameter
            var flowRes = await client.ExecuteCypherAsync("""
                MATCH (caller:Function {name: 'LoadDashboardAsync'})-[:PASSES_ARGUMENT]->(arg:CallArgument)-[:FLOWS_TO*1..3]->(sinkArg:CallArgument)
                WHERE arg.resolved_value CONTAINS 'customer_orders'
                RETURN arg.provenance AS prov, arg.value_kind AS vkind, sinkArg.callee AS sinkCallee
                """);
            flowRes.Count.Should().BeGreaterThan(0);
            flowRes.Rows[0].GetStringProp("prov").Should().Be("InternalConstant");
            flowRes.Rows[0].GetStringProp("vkind").Should().Be("SqlDml");

            // 4. Verify downward Layer 4 -> Layer 3 TARGETED_BY edges for BOTH customer_orders AND top-down template billing_invoices!
            var tableTargets = await client.ExecuteCypherAsync("""
                MATCH (tbl:Table)-[:TARGETED_BY]->(arg:CallArgument)<-[:PASSES_ARGUMENT]-(fn:Function {name: 'LoadDashboardAsync'})
                RETURN tbl.name AS tableName, arg.callee AS callee, arg.resolved_value AS resolvedVal
                """);
            tableTargets.Rows.Select(r => r.GetStringProp("tableName")).Should().Contain(["customer_orders", "billing_invoices"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task GraphIndexing_TypeScriptOptionsObject_And_CompositeEnvUrl_PreservesProvenanceAndLayerHierarchy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_sem_args_ts_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), """
                {
                  "name": "ledger-service",
                  "dependencies": {
                    "pg": "^8.11.0",
                    "axios": "^1.6.0"
                  }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "ledgerClient.ts"), """
                import axios from 'axios';
                import { Pool } from 'pg';

                const pool = new Pool();

                export async function syncLedgerEntries() {
                    const baseUrl = process.env.PAYMENTS_API_URL;
                    await axios.get(baseUrl + '/v1/charges/settle');
                    await pool.query({ text: 'SELECT id, amount FROM ledger_entries WHERE settled = false' });
                }
                """);

            var dbPath = Path.Combine(tempDir, "graph_ts.db").Replace('\\', '/');
            await using var client = new SqliteGraphClient(dbPath);
            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            // 1. Verify options object property_key = 'text' grounded to TableNode('ledger_entries')
            var optRes = await client.ExecuteCypherAsync("""
                MATCH (tbl:Table {name: 'ledger_entries'})-[:TARGETED_BY]->(arg:CallArgument)
                RETURN arg.property_key AS propKey, arg.nature AS nature, arg.value_kind AS vkind
                """);
            optRes.Count.Should().Be(1);
            optRes.Rows[0].GetStringProp("propKey").Should().Be("text");
            optRes.Rows[0].GetStringProp("nature").Should().Be("SqlQuery");
            optRes.Rows[0].GetStringProp("vkind").Should().Be("SqlDml");

            // 2. Verify composite URL slices: CompositeBase (ConfigOrEnv) + CompositeSuffix (InternalConstant, UrlRelativeRoute)
            var urlRes = await client.ExecuteCypherAsync("""
                MATCH (fn:Function {name: 'syncLedgerEntries'})-[:PASSES_ARGUMENT]->(arg:CallArgument {nature: 'UrlOrRoute'})
                RETURN arg.expression_role AS role, arg.provenance AS prov, arg.origin_symbol AS sym, arg.resolved_value AS val, arg.value_kind AS vkind
                """);
            urlRes.Rows.Should().Contain(r =>
                r.GetStringProp("role") == "CompositeBase" &&
                r.GetStringProp("prov") == "ConfigOrEnv" &&
                r.GetStringProp("sym") == "PAYMENTS_API_URL");
            urlRes.Rows.Should().Contain(r =>
                r.GetStringProp("role") == "CompositeSuffix" &&
                r.GetStringProp("prov") == "InternalConstant" &&
                r.GetStringProp("val") == "/v1/charges/settle" &&
                r.GetStringProp("vkind") == "UrlRelativeRoute");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
