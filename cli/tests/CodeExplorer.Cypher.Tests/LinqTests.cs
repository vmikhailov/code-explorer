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

        Assert.That(ast.Matches, Has.Count.EqualTo(1));
        var match = ast.Matches[0];
        Assert.That(match.Paths[0].Head.Labels, Contains.Item("Service"));

        Assert.That(ast.Where, Is.Not.Null);
        Assert.That(ast.Where!.Predicate, Is.TypeOf<BinaryExpression>());
        var bin = (BinaryExpression)ast.Where.Predicate;
        Assert.That(bin.Operator, Is.EqualTo(BinaryOperator.Equal));
        Assert.That(bin.Left, Is.TypeOf<PropertyAccessExpression>());
        var leftProp = (PropertyAccessExpression)bin.Left;
        Assert.That(leftProp.PropertyName, Is.EqualTo("framework"));
        Assert.That(bin.Right, Is.TypeOf<ParameterExpression>());

        // Check SQL compilation
        var compiled = SqliteCompiler.Compile(ast);
        Assert.That(compiled.Sql, Does.Contain("SELECT"));
        Assert.That(compiled.Sql, Does.Contain("framework"));
    }

    [Test]
    public void Test_Services_MultiplePredicates_And()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework == "aspnetcore" && s.Language == "csharp");
        var ast = query.ToCypherAst();

        Assert.That(ast.Where, Is.Not.Null);
        Assert.That(ast.Where!.Predicate, Is.TypeOf<BinaryExpression>());
        var bin = (BinaryExpression)ast.Where.Predicate;
        Assert.That(bin.Operator, Is.EqualTo(BinaryOperator.And));
    }

    [Test]
    public void Test_Services_StringContains_StartsWith_EndsWith()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services.Where(s => s.Name.Contains("hub")).ToCypherAst();
        var bin1 = (BinaryExpression)q1.Where!.Predicate;
        Assert.That(bin1.Operator, Is.EqualTo(BinaryOperator.Contains));

        var q2 = graph.Services.Where(s => s.Name.StartsWith("ad-")).ToCypherAst();
        var bin2 = (BinaryExpression)q2.Where!.Predicate;
        Assert.That(bin2.Operator, Is.EqualTo(BinaryOperator.StartsWith));

        var q3 = graph.Services.Where(s => s.Name.EndsWith("-service")).ToCypherAst();
        var bin3 = (BinaryExpression)q3.Where!.Predicate;
        Assert.That(bin3.Operator, Is.EqualTo(BinaryOperator.EndsWith));
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

        Assert.That(ast.OrderBy, Is.Not.Null);
        Assert.That(ast.OrderBy!.Items, Has.Count.EqualTo(1));
        Assert.That(ast.OrderBy.Items[0].IsDescending, Is.False);

        Assert.That(ast.Skip, Is.Not.Null);
        Assert.That(ast.Skip!.Count, Is.EqualTo(10));

        Assert.That(ast.Limit, Is.Not.Null);
        Assert.That(ast.Limit!.Count, Is.EqualTo(25));

        // Compile with SqliteCompiler
        var compiled = SqliteCompiler.Compile(ast);
        Assert.That(compiled.Sql, Does.Contain("LIMIT 25"));
        Assert.That(compiled.Sql, Does.Contain("OFFSET 10"));
    }

    [Test]
    public void Test_Services_OrderByDescending()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services
            .OrderByDescending(s => s.Name);

        var ast = query.ToCypherAst();

        Assert.That(ast.OrderBy, Is.Not.Null);
        Assert.That(ast.OrderBy!.Items[0].IsDescending, Is.True);
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

        Assert.That(count, Is.EqualTo(42));
        Assert.That(fake.LastQuery, Is.Not.Null);
        Assert.That(fake.LastQuery!.Return.Items[0].Alias, Is.EqualTo("count"));
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

        Assert.That(hasBff, Is.True);
        Assert.That(fake.LastQuery, Is.Not.Null);
        Assert.That(fake.LastQuery!.Return.Items[0].Alias, Is.EqualTo("any"));
    }

    [Test]
    public void Test_GraphContext_Entities_Labels()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        Assert.That(graph.Services.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("Service"));
        Assert.That(graph.Apps.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("App"));
        Assert.That(graph.Workers.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("Worker"));
        Assert.That(graph.CliTools.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("CliTool"));
        Assert.That(graph.Databases.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("Database"));
        Assert.That(graph.Topics.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("Topic"));
        Assert.That(graph.ExternalServices.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("ExternalService"));
        Assert.That(graph.Libraries.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("Library"));
        Assert.That(graph.Endpoints.ToCypherAst().Matches[0].Paths[0].Head.Labels, Contains.Item("Endpoint"));
        Assert.That(graph.Nodes.ToCypherAst().Matches[0].Paths[0].Head.Labels, Is.Empty);
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

        Assert.That(services, Has.Count.EqualTo(2));

        Assert.That(services[0].Id, Is.EqualTo("ws:s:bff"));
        Assert.That(services[0].Name, Is.EqualTo("bff"));
        Assert.That(services[0].DisplayName, Is.EqualTo("BFF Service"));
        Assert.That(services[0].Framework, Is.EqualTo("nestjs"));
        Assert.That(services[0].Language, Is.EqualTo("typescript"));
        Assert.That(services[0].IsLibrary, Is.False);

        Assert.That(services[1].Id, Is.EqualTo("ws:s:billing"));
        Assert.That(services[1].Name, Is.EqualTo("billing"));
        Assert.That(services[1].Framework, Is.EqualTo("aspnetcore"));
        Assert.That(services[1].Language, Is.EqualTo("csharp"));
        Assert.That(services[1].IsLibrary, Is.False);
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

        Assert.That(db, Is.Not.Null);
        Assert.That(db!.Id, Is.EqualTo("ws:db:main"));
        Assert.That(db.Name, Is.EqualTo("main_postgres"));
        Assert.That(db.DbType, Is.EqualTo("relational"));
        Assert.That(db.Engine, Is.EqualTo("postgresql"));
        Assert.That(fake.LastQuery!.Limit!.Count, Is.EqualTo(1));
    }

    [Test]
    public void Test_Comparison_LessThan_LessOrEqual_GreaterThan_GreaterOrEqual()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var qLt = graph.Services.Where(s => s.Port < 8080).ToCypherAst();
        var binLt = (BinaryExpression)qLt.Where!.Predicate;
        Assert.That(binLt.Operator, Is.EqualTo(BinaryOperator.LessThan));

        var qLte = graph.Services.Where(s => s.Port <= 8080).ToCypherAst();
        var binLte = (BinaryExpression)qLte.Where!.Predicate;
        Assert.That(binLte.Operator, Is.EqualTo(BinaryOperator.LessOrEqual));

        var qGt = graph.Services.Where(s => s.Port > 8080).ToCypherAst();
        var binGt = (BinaryExpression)qGt.Where!.Predicate;
        Assert.That(binGt.Operator, Is.EqualTo(BinaryOperator.GreaterThan));

        var qGte = graph.Services.Where(s => s.Port >= 8080).ToCypherAst();
        var binGte = (BinaryExpression)qGte.Where!.Predicate;
        Assert.That(binGte.Operator, Is.EqualTo(BinaryOperator.GreaterOrEqual));
    }

    [Test]
    public void Test_Comparison_NotEqual()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework != "aspnetcore").ToCypherAst();
        var bin = (BinaryExpression)query.Where!.Predicate;
        Assert.That(bin.Operator, Is.EqualTo(BinaryOperator.NotEqual));
    }

    [Test]
    public void Test_Logical_Or()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var query = graph.Services.Where(s => s.Framework == "aspnetcore" || s.Framework == "nestjs").ToCypherAst();
        var bin = (BinaryExpression)query.Where!.Predicate;
        Assert.That(bin.Operator, Is.EqualTo(BinaryOperator.Or));
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
        Assert.That(bin.Operator, Is.EqualTo(BinaryOperator.Or));
        Assert.That(((BinaryExpression)bin.Left).Operator, Is.EqualTo(BinaryOperator.And));
        Assert.That(((BinaryExpression)bin.Right).Operator, Is.EqualTo(BinaryOperator.And));
    }

    [Test]
    public void Test_String_Equals_InstanceAndStatic()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services.Where(s => s.Name.Equals("bff")).ToCypherAst();
        var bin1 = (BinaryExpression)q1.Where!.Predicate;
        Assert.That(bin1.Operator, Is.EqualTo(BinaryOperator.Equal));

        var q2 = graph.Services.Where(s => string.Equals(s.Name, "bff")).ToCypherAst();
        var bin2 = (BinaryExpression)q2.Where!.Predicate;
        Assert.That(bin2.Operator, Is.EqualTo(BinaryOperator.Equal));
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
        Assert.That(where, Is.Not.Null);

        var compiled = SqliteCompiler.Compile(ast, queryable.GetParameters());
        Assert.That(compiled.Parameters.Values, Contains.Item("order-service"));
        Assert.That(compiled.Parameters.Values, Contains.Item("grpc-dotnet"));
    }

    [Test]
    public void Test_NullComparison_EqualAndNotEqualNull()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var q1 = graph.Services.Where(s => s.DisplayName == null).ToCypherAst();
        var un1 = (UnaryExpression)q1.Where!.Predicate;
        Assert.That(un1.Operator, Is.EqualTo(UnaryOperator.IsNull));

        var q2 = graph.Services.Where(s => s.DisplayName != null).ToCypherAst();
        var un2 = (UnaryExpression)q2.Where!.Predicate;
        Assert.That(un2.Operator, Is.EqualTo(UnaryOperator.IsNotNull));

        var q3 = graph.Services.Where(s => null == s.DisplayName).ToCypherAst();
        var un3 = (UnaryExpression)q3.Where!.Predicate;
        Assert.That(un3.Operator, Is.EqualTo(UnaryOperator.IsNull));

        var q4 = graph.Services.Where(s => null != s.DisplayName).ToCypherAst();
        var un4 = (UnaryExpression)q4.Where!.Predicate;
        Assert.That(un4.Operator, Is.EqualTo(UnaryOperator.IsNotNull));
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
        Assert.That(ast.Where, Is.Not.Null);
        var bin = (BinaryExpression)ast.Where!.Predicate;
        Assert.That(bin.Operator, Is.EqualTo(BinaryOperator.And));
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

        Assert.That(q1.OrderBy, Is.Not.Null);
        Assert.That(q1.OrderBy!.Items, Has.Count.EqualTo(2));
        Assert.That(q1.OrderBy.Items[0].IsDescending, Is.False);
        Assert.That(q1.OrderBy.Items[1].IsDescending, Is.False);

        var q2 = graph.Services
            .OrderBy(s => s.Framework)
            .ThenByDescending(s => s.Name)
            .ToCypherAst();

        Assert.That(q2.OrderBy, Is.Not.Null);
        Assert.That(q2.OrderBy!.Items, Has.Count.EqualTo(2));
        Assert.That(q2.OrderBy.Items[0].IsDescending, Is.False);
        Assert.That(q2.OrderBy.Items[1].IsDescending, Is.True);
    }

    [Test]
    public void Test_SkipOnly_And_TakeOnly()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        var qSkip = graph.Services.Skip(15).ToCypherAst();
        Assert.That(qSkip.Skip!.Count, Is.EqualTo(15));
        Assert.That(qSkip.Limit, Is.Null);

        var qTake = graph.Services.Take(30).ToCypherAst();
        Assert.That(qTake.Limit!.Count, Is.EqualTo(30));
        Assert.That(qTake.Skip, Is.Null);

        var compiledSkip = SqliteCompiler.Compile(qSkip);
        Assert.That(compiledSkip.Sql, Does.Contain("OFFSET 15"));

        var compiledTake = SqliteCompiler.Compile(qTake);
        Assert.That(compiledTake.Sql, Does.Contain("LIMIT 30"));
    }

    [Test]
    public void Test_BooleanProperty_DirectPredicate_True_And_False()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        // s => s.IsLibrary
        var qTrue = graph.Services.Where(s => s.IsLibrary).ToCypherAst();
        Assert.That(qTrue.Where, Is.Not.Null);
        var binTrue = (BinaryExpression)qTrue.Where!.Predicate;
        Assert.That(binTrue.Operator, Is.EqualTo(BinaryOperator.Equal));
        Assert.That(((PropertyAccessExpression)binTrue.Left).PropertyName, Is.EqualTo("is_library"));
        Assert.That(((BooleanLiteralExpression)binTrue.Right).Value, Is.True);

        // s => !s.IsLibrary
        var qFalse = graph.Services.Where(s => !s.IsLibrary).ToCypherAst();
        Assert.That(qFalse.Where, Is.Not.Null);
        var binFalse = (BinaryExpression)qFalse.Where!.Predicate;
        Assert.That(binFalse.Operator, Is.EqualTo(BinaryOperator.Or));
        Assert.That(binFalse.Left, Is.TypeOf<UnaryExpression>());
        Assert.That(((UnaryExpression)binFalse.Left).Operator, Is.EqualTo(UnaryOperator.IsNull));
        Assert.That(binFalse.Right, Is.TypeOf<BinaryExpression>());
        Assert.That(((BinaryExpression)binFalse.Right).Operator, Is.EqualTo(BinaryOperator.Equal));
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
        Assert.That(arr, Has.Length.EqualTo(2));
        Assert.That(arr[0].Name, Is.EqualTo("service-one"));
        Assert.That(arr[1].Name, Is.EqualTo("service-two"));
    }

    [Test]
    public void Test_EntityPropertyMappings_AllTypes()
    {
        var fake = new FakeQueryExecutor();
        var graph = new GraphContext(fake);

        // Apps
        var appAst = graph.Apps.Where(a => a.AppType == "spa" && a.Port == 3000).ToCypherAst();
        var appSql = SqliteCompiler.Compile(appAst).Sql;
        Assert.That(appSql, Does.Contain("app_type"));
        Assert.That(appSql, Does.Contain("port"));

        // Workers
        var workerAst = graph.Workers.Where(w => w.QueueType == "rabbitmq").ToCypherAst();
        var workerSql = SqliteCompiler.Compile(workerAst).Sql;
        Assert.That(workerSql, Does.Contain("queue_type"));

        // CliTools
        var cliAst = graph.CliTools.Where(c => c.CommandName == "ce").ToCypherAst();
        var cliSql = SqliteCompiler.Compile(cliAst).Sql;
        Assert.That(cliSql, Does.Contain("command_name"));

        // Databases
        var dbAst = graph.Databases.Where(d => d.DbType == "relational" && d.Engine == "postgres" && d.Port == 5432).ToCypherAst();
        var dbSql = SqliteCompiler.Compile(dbAst).Sql;
        Assert.That(dbSql, Does.Contain("db_type"));
        Assert.That(dbSql, Does.Contain("engine"));
        Assert.That(dbSql, Does.Contain("port"));

        // Topics
        var topicAst = graph.Topics.Where(t => t.BrokerType == "kafka" && t.PartitionCount == 12).ToCypherAst();
        var topicSql = SqliteCompiler.Compile(topicAst).Sql;
        Assert.That(topicSql, Does.Contain("broker_type"));
        Assert.That(topicSql, Does.Contain("partition_count"));

        // ExternalServices
        var extAst = graph.ExternalServices.Where(e => e.Protocol == "https" && e.BaseUrl == "https://api.stripe.com" && e.Category == "Payment").ToCypherAst();
        var extSql = SqliteCompiler.Compile(extAst).Sql;
        Assert.That(extSql, Does.Contain("protocol"));
        Assert.That(extSql, Does.Contain("base_url"));
        Assert.That(extSql, Does.Contain("category"));

        // Libraries
        var libAst = graph.Libraries.Where(l => l.PackageName == "Newtonsoft.Json" && l.Version == "13.0.3").ToCypherAst();
        var libSql = SqliteCompiler.Compile(libAst).Sql;
        Assert.That(libSql, Does.Contain("package_name"));
        Assert.That(libSql, Does.Contain("version"));

        // Endpoints
        var epAst = graph.Endpoints.Where(ep => ep.Route == "/api/v1/orders" && ep.HttpMethod == "POST" && ep.Protocol == "http").ToCypherAst();
        var epSql = SqliteCompiler.Compile(epAst).Sql;
        Assert.That(epSql, Does.Contain("route"));
        Assert.That(epSql, Does.Contain("http_method"));
        Assert.That(epSql, Does.Contain("protocol"));

        // Generic Nodes
        var nodeAst = graph.Nodes.Where(n => n.FilePath == "src/index.ts" && n.LineStart == 10 && n.LineEnd == 50).ToCypherAst();
        var nodeSql = SqliteCompiler.Compile(nodeAst).Sql;
        Assert.That(nodeSql, Does.Contain("file_path"));
        Assert.That(nodeSql, Does.Contain("line_start"));
        Assert.That(nodeSql, Does.Contain("line_end"));
    }
}
