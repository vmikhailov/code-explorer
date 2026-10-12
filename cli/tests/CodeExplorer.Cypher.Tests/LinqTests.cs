using AwesomeAssertions;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Linq;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class LinqTests
{
    private class FakeQueryExecutor : ICypherQueryExecutor
    {
        public CypherQuery? LastQuery { get; private set; }
        public IReadOnlyDictionary<string, object?>? LastParameters { get; private set; }
        public string FakeResultJson { get; set; } = "[]";

        public Task<string> ExecuteQueryAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            LastQuery = query;
            LastParameters = parameters;
            return Task.FromResult(FakeResultJson);
        }

        public Task<string> ExecuteRawAsync(string cypher, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            return Task.FromResult(FakeResultJson);
        }
    }

    [Test]
    public void Test_Services_SimpleFilter()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework == "aspnetcore");
        var ast = query.ToCypherAst();

        ast.Matches.Should().HaveCount(1);
        var match = ast.Matches[0];
        match.Paths[0].Head.Labels.Should().Contain("Service");

        ast.Where.Should().NotBeNull();
        ast.Where!.Predicate.Should().BeOfType<BinaryExpression>();
        var bin = (BinaryExpression)ast.Where.Predicate;
        bin.Operator.Should().Be(BinaryOperator.Equal);
        bin.Left.Should().BeOfType<PropertyAccessExpression>();
        var leftProp = (PropertyAccessExpression)bin.Left;
        leftProp.PropertyName.Should().Be("framework");
        bin.Right.Should().BeOfType<ParameterExpression>();

        // Check SQL compilation
        var compiled = SqliteCompiler.Compile(ast);
        compiled.Sql.Should().Contain("SELECT");
        compiled.Sql.Should().Contain("framework");
    }

    [Test]
    public void Test_Services_MultiplePredicates_And()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework == "aspnetcore" && s.Language == "csharp");
        var ast = query.ToCypherAst();

        ast.Where.Should().NotBeNull();
        ast.Where!.Predicate.Should().BeOfType<BinaryExpression>();
        var bin = (BinaryExpression)ast.Where.Predicate;
        bin.Operator.Should().Be(BinaryOperator.And);
    }

    [Test]
    public void Test_Services_StringContains_StartsWith_EndsWith()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services.Where(s => s.Name.Contains("hub")).ToCypherAst();
        var bin1 = (BinaryExpression)q1.Where!.Predicate;
        bin1.Operator.Should().Be(BinaryOperator.Contains);

        var q2 = graph.Services.Where(s => s.Name.StartsWith("ad-")).ToCypherAst();
        var bin2 = (BinaryExpression)q2.Where!.Predicate;
        bin2.Operator.Should().Be(BinaryOperator.StartsWith);

        var q3 = graph.Services.Where(s => s.Name.EndsWith("-service")).ToCypherAst();
        var bin3 = (BinaryExpression)q3.Where!.Predicate;
        bin3.Operator.Should().Be(BinaryOperator.EndsWith);
    }

    [Test]
    public void Test_Services_OrderBy_Skip_Take()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services
            .Where(s => s.Name != null)
            .OrderBy(s => s.Name)
            .Skip(10)
            .Take(25);

        var ast = query.ToCypherAst();

        ast.OrderBy.Should().NotBeNull();
        ast.OrderBy!.Items.Should().HaveCount(1);
        ast.OrderBy.Items[0].IsDescending.Should().BeFalse();

        ast.Skip.Should().NotBeNull();
        ast.Skip!.Count.Should().Be(10);

        ast.Limit.Should().NotBeNull();
        ast.Limit!.Count.Should().Be(25);

        // Compile with SqliteCompiler
        var compiled = SqliteCompiler.Compile(ast);
        compiled.Sql.Should().Contain("LIMIT 25");
        compiled.Sql.Should().Contain("OFFSET 10");
    }

    [Test]
    public void Test_Services_OrderByDescending()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services
            .OrderByDescending(s => s.Name);

        var ast = query.ToCypherAst();

        ast.OrderBy.Should().NotBeNull();
        ast.OrderBy!.Items[0].IsDescending.Should().BeTrue();
    }

    [Test]
    public async Task Test_CountAsync_ExecutesCountAst()
    {
        var fake = new FakeQueryExecutor
        {
            FakeResultJson = "[{\"count\": 42}]"
        };
        var graph = new GraphContext(fake);

        var count = await graph.Services.Where(s => !s.IsLibrary).CountAsync();

        count.Should().Be(42);
        fake.LastQuery.Should().NotBeNull();
        fake.LastQuery!.Return.Items[0].Alias.Should().Be("count");
    }

    [Test]
    public async Task Test_AnyAsync_ExecutesAnyAst()
    {
        var fake = new FakeQueryExecutor
        {
            FakeResultJson = "[{\"any\": true}]"
        };
        var graph = new GraphContext(fake);

        var hasBff = await graph.Services.Where(s => s.Name == "bff").AnyAsync();

        hasBff.Should().BeTrue();
        fake.LastQuery.Should().NotBeNull();
        fake.LastQuery!.Return.Items[0].Alias.Should().Be("any");
    }

    [Test]
    public void Test_GraphContext_Entities_Labels()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        graph.Services.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("Service");
        graph.Apps.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("App");
        graph.Workers.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("Worker");
        graph.CliTools.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("CliTool");
        graph.Databases.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("Database");
        graph.Topics.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("Topic");
        graph.ExternalServices.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("ExternalService");
        graph.Libraries.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("Library");
        graph.Endpoints.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().Contain("Endpoint");
        graph.Nodes.ToCypherAst().Matches[0].Paths[0].Head.Labels.Should().BeEmpty();
    }

    [Test]
    public async Task Test_ExecuteWithMockExecutor_Deserialization()
    {
        var fake = new FakeQueryExecutor
        {
            FakeResultJson = """
            [
              {
                "id": "ws:s:bff",
                "name": "bff",
                "display_name": "BFF Service",
                "file_path": "bff/src/main.ts",
                "line_start": 1,
                "line_end": 100,
                "properties": {
                  "framework": "nestjs",
                  "language": "typescript",
                  "role": "Service",
                  "is_library": "false"
                }
              },
              {
                "id": "ws:s:billing",
                "name": "billing",
                "display_name": "Billing Service",
                "file_path": "billing/Program.cs",
                "properties": {
                  "framework": "aspnetcore",
                  "language": "csharp",
                  "role": "Service",
                  "is_library": false
                }
              }
            ]
            """
        };
        var graph = new GraphContext(fake);

        var services = await graph.Services.ToListAsync();

        services.Should().HaveCount(2);

        services[0].Id.Should().Be("ws:s:bff");
        services[0].Name.Should().Be("bff");
        services[0].DisplayName.Should().Be("BFF Service");
        services[0].Framework.Should().Be("nestjs");
        services[0].Language.Should().Be("typescript");
        services[0].IsLibrary.Should().BeFalse();

        services[1].Id.Should().Be("ws:s:billing");
        services[1].Name.Should().Be("billing");
        services[1].Framework.Should().Be("aspnetcore");
        services[1].Language.Should().Be("csharp");
        services[1].IsLibrary.Should().BeFalse();
    }

    [Test]
    public async Task Test_FirstOrDefaultAsync()
    {
        var fake = new FakeQueryExecutor
        {
            FakeResultJson = """
            [
              {
                "id": "ws:db:main",
                "name": "main_postgres",
                "properties": {
                  "db_type": "relational",
                  "engine": "postgresql"
                }
              }
            ]
            """
        };
        var graph = new GraphContext(fake);

        var db = await graph.Databases.Where(d => d.Name == "main_postgres").FirstOrDefaultAsync();

        db.Should().NotBeNull();
        db!.Id.Should().Be("ws:db:main");
        db.Name.Should().Be("main_postgres");
        db.DbType.Should().Be("relational");
        db.Engine.Should().Be("postgresql");
        fake.LastQuery!.Limit!.Count.Should().Be(1);
    }

    [Test]
    public void Test_Comparison_LessThan_LessOrEqual_GreaterThan_GreaterOrEqual()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var qLt = graph.Services.Where(s => s.Port < 8080).ToCypherAst();
        var binLt = (BinaryExpression)qLt.Where!.Predicate;
        binLt.Operator.Should().Be(BinaryOperator.LessThan);

        var qLte = graph.Services.Where(s => s.Port <= 8080).ToCypherAst();
        var binLte = (BinaryExpression)qLte.Where!.Predicate;
        binLte.Operator.Should().Be(BinaryOperator.LessOrEqual);

        var qGt = graph.Services.Where(s => s.Port > 8080).ToCypherAst();
        var binGt = (BinaryExpression)qGt.Where!.Predicate;
        binGt.Operator.Should().Be(BinaryOperator.GreaterThan);

        var qGte = graph.Services.Where(s => s.Port >= 8080).ToCypherAst();
        var binGte = (BinaryExpression)qGte.Where!.Predicate;
        binGte.Operator.Should().Be(BinaryOperator.GreaterOrEqual);
    }

    [Test]
    public void Test_Comparison_NotEqual()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework != "aspnetcore").ToCypherAst();
        var bin = (BinaryExpression)query.Where!.Predicate;
        bin.Operator.Should().Be(BinaryOperator.NotEqual);
    }

    [Test]
    public void Test_Logical_Or()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework == "aspnetcore" || s.Framework == "nestjs").ToCypherAst();
        var bin = (BinaryExpression)query.Where!.Predicate;
        bin.Operator.Should().Be(BinaryOperator.Or);
    }

    [Test]
    public void Test_Logical_ComplexNested()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => 
            (s.Framework == "aspnetcore" && s.Language == "csharp") || 
            (s.Framework == "nestjs" && s.Language == "typescript")
        ).ToCypherAst();

        var bin = (BinaryExpression)query.Where!.Predicate;
        bin.Operator.Should().Be(BinaryOperator.Or);
        ((BinaryExpression)bin.Left).Operator.Should().Be(BinaryOperator.And);
        ((BinaryExpression)bin.Right).Operator.Should().Be(BinaryOperator.And);
    }

    [Test]
    public void Test_String_Equals_InstanceAndStatic()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services.Where(s => s.Name.Equals("bff")).ToCypherAst();
        var bin1 = (BinaryExpression)q1.Where!.Predicate;
        bin1.Operator.Should().Be(BinaryOperator.Equal);

        var q2 = graph.Services.Where(s => string.Equals(s.Name, "bff")).ToCypherAst();
        var bin2 = (BinaryExpression)q2.Where!.Predicate;
        bin2.Operator.Should().Be(BinaryOperator.Equal);
    }

    private class SampleFilterConfig
    {
        public string ExpectedFramework { get; set; } = "aspnetcore";
        public int ExpectedPort { get; set; } = 5000;
    }

    private static int GetTargetPort() => 5001;

    [Test]
    public void Test_VariableClosure_LocalVariable_And_ObjectProperty()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var localName = "order-service";
        var config = new SampleFilterConfig { ExpectedFramework = "grpc-dotnet", ExpectedPort = 9090 };

        var queryable = graph.Services.Where(s => s.Name == localName && s.Framework == config.ExpectedFramework && s.Port == GetTargetPort());
        var ast = queryable.ToCypherAst();

        var where = ast.Where;
        where.Should().NotBeNull();

        var compiled = SqliteCompiler.Compile(ast, queryable.GetParameters());
        compiled.Parameters.Values.Should().Contain("order-service");
        compiled.Parameters.Values.Should().Contain("grpc-dotnet");
    }

    [Test]
    public void Test_NullComparison_EqualAndNotEqualNull()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services.Where(s => s.DisplayName == null).ToCypherAst();
        var un1 = (UnaryExpression)q1.Where!.Predicate;
        un1.Operator.Should().Be(UnaryOperator.IsNull);

        var q2 = graph.Services.Where(s => s.DisplayName != null).ToCypherAst();
        var un2 = (UnaryExpression)q2.Where!.Predicate;
        un2.Operator.Should().Be(UnaryOperator.IsNotNull);

        var q3 = graph.Services.Where(s => null == s.DisplayName).ToCypherAst();
        var un3 = (UnaryExpression)q3.Where!.Predicate;
        un3.Operator.Should().Be(UnaryOperator.IsNull);

        var q4 = graph.Services.Where(s => null != s.DisplayName).ToCypherAst();
        var un4 = (UnaryExpression)q4.Where!.Predicate;
        un4.Operator.Should().Be(UnaryOperator.IsNotNull);
    }

    [Test]
    public void Test_Chained_Where_CombinesWithAnd()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services
            .Where(s => s.Framework == "aspnetcore")
            .Where(s => s.Language == "csharp");

        var ast = query.ToCypherAst();
        ast.Where.Should().NotBeNull();
        var bin = (BinaryExpression)ast.Where!.Predicate;
        bin.Operator.Should().Be(BinaryOperator.And);
    }

    [Test]
    public void Test_OrderBy_ThenBy_And_ThenByDescending()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services
            .OrderBy(s => s.Framework)
            .ThenBy(s => s.Name)
            .ToCypherAst();

        q1.OrderBy.Should().NotBeNull();
        q1.OrderBy!.Items.Should().HaveCount(2);
        q1.OrderBy.Items[0].IsDescending.Should().BeFalse();
        q1.OrderBy.Items[1].IsDescending.Should().BeFalse();

        var q2 = graph.Services
            .OrderBy(s => s.Framework)
            .ThenByDescending(s => s.Name)
            .ToCypherAst();

        q2.OrderBy.Should().NotBeNull();
        q2.OrderBy!.Items.Should().HaveCount(2);
        q2.OrderBy.Items[0].IsDescending.Should().BeFalse();
        q2.OrderBy.Items[1].IsDescending.Should().BeTrue();
    }

    [Test]
    public void Test_SkipOnly_And_TakeOnly()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var qSkip = graph.Services.Skip(15).ToCypherAst();
        qSkip.Skip!.Count.Should().Be(15);
        qSkip.Limit.Should().BeNull();

        var qTake = graph.Services.Take(30).ToCypherAst();
        qTake.Limit!.Count.Should().Be(30);
        qTake.Skip.Should().BeNull();

        var compiledSkip = SqliteCompiler.Compile(qSkip);
        compiledSkip.Sql.Should().Contain("OFFSET 15");

        var compiledTake = SqliteCompiler.Compile(qTake);
        compiledTake.Sql.Should().Contain("LIMIT 30");
    }

    [Test]
    public void Test_BooleanProperty_DirectPredicate_True_And_False()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        // s => s.IsLibrary
        var qTrue = graph.Services.Where(s => s.IsLibrary).ToCypherAst();
        qTrue.Where.Should().NotBeNull();
        var binTrue = (BinaryExpression)qTrue.Where!.Predicate;
        binTrue.Operator.Should().Be(BinaryOperator.Equal);
        ((PropertyAccessExpression)binTrue.Left).PropertyName.Should().Be("is_library");
        ((BooleanLiteralExpression)binTrue.Right).Value.Should().BeTrue();

        // s => !s.IsLibrary
        var qFalse = graph.Services.Where(s => !s.IsLibrary).ToCypherAst();
        qFalse.Where.Should().NotBeNull();
        var binFalse = (BinaryExpression)qFalse.Where!.Predicate;
        binFalse.Operator.Should().Be(BinaryOperator.Or);
        binFalse.Left.Should().BeOfType<UnaryExpression>();
        ((UnaryExpression)binFalse.Left).Operator.Should().Be(UnaryOperator.IsNull);
        binFalse.Right.Should().BeOfType<BinaryExpression>();
        ((BinaryExpression)binFalse.Right).Operator.Should().Be(BinaryOperator.Equal);
    }

    [Test]
    public async Task Test_ToArrayAsync()
    {
        var fake = new FakeQueryExecutor
        {
            FakeResultJson = """
            [
              { "id": "s:1", "name": "service-one" },
              { "id": "s:2", "name": "service-two" }
            ]
            """
        };
        var graph = new GraphContext(fake);

        var arr = await graph.Services.ToArrayAsync();
        arr.Should().HaveCount(2);
        arr[0].Name.Should().Be("service-one");
        arr[1].Name.Should().Be("service-two");
    }

    [Test]
    public void Test_EntityPropertyMappings_AllTypes()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        // Apps
        var appAst = graph.Apps.Where(a => a.AppType == "spa" && a.Port == 3000).ToCypherAst();
        var appSql = SqliteCompiler.Compile(appAst).Sql;
        appSql.Should().Contain("app_type");
        appSql.Should().Contain("port");

        // Workers
        var workerAst = graph.Workers.Where(w => w.QueueType == "rabbitmq").ToCypherAst();
        var workerSql = SqliteCompiler.Compile(workerAst).Sql;
        workerSql.Should().Contain("queue_type");

        // CliTools
        var cliAst = graph.CliTools.Where(c => c.CommandName == "ce").ToCypherAst();
        var cliSql = SqliteCompiler.Compile(cliAst).Sql;
        cliSql.Should().Contain("command_name");

        // Databases
        var dbAst = graph.Databases.Where(d => d.DbType == "relational" && d.Engine == "postgres" && d.Port == 5432).ToCypherAst();
        var dbSql = SqliteCompiler.Compile(dbAst).Sql;
        dbSql.Should().Contain("db_type");
        dbSql.Should().Contain("engine");
        dbSql.Should().Contain("port");

        // Topics
        var topicAst = graph.Topics.Where(t => t.BrokerType == "kafka" && t.PartitionCount == 12).ToCypherAst();
        var topicSql = SqliteCompiler.Compile(topicAst).Sql;
        topicSql.Should().Contain("broker_type");
        topicSql.Should().Contain("partition_count");

        // ExternalServices
        var extAst = graph.ExternalServices.Where(e => e.Protocol == "https" && e.BaseUrl == "https://api.stripe.com" && e.Category == "Payment").ToCypherAst();
        var extSql = SqliteCompiler.Compile(extAst).Sql;
        extSql.Should().Contain("protocol");
        extSql.Should().Contain("base_url");
        extSql.Should().Contain("category");

        // Libraries
        var libAst = graph.Libraries.Where(l => l.PackageName == "Newtonsoft.Json" && l.Version == "13.0.3").ToCypherAst();
        var libSql = SqliteCompiler.Compile(libAst).Sql;
        libSql.Should().Contain("package_name");
        libSql.Should().Contain("version");

        // Endpoints
        var epAst = graph.Endpoints.Where(ep => ep.Route == "/api/v1/orders" && ep.HttpMethod == "POST" && ep.Protocol == "http").ToCypherAst();
        var epSql = SqliteCompiler.Compile(epAst).Sql;
        epSql.Should().Contain("route");
        epSql.Should().Contain("http_method");
        epSql.Should().Contain("protocol");

        // Generic Nodes
        var nodeAst = graph.Nodes.Where(n => n.FilePath == "src/index.ts" && n.LineStart == 10 && n.LineEnd == 50).ToCypherAst();
        var nodeSql = SqliteCompiler.Compile(nodeAst).Sql;
        nodeSql.Should().Contain("file_path");
        nodeSql.Should().Contain("line_start");
        nodeSql.Should().Contain("line_end");
    }
}
