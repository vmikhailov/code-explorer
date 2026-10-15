using System.Text.Json;
using AwesomeAssertions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class BuiltInQueriesTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_builtin_queries_test_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(_tempWorkspace);

        // Project A: C# project with a large GodObject and an unused function
        var projADir = Path.Combine(_tempWorkspace, "ProjectA").Replace('\\', '/');
        Directory.CreateDirectory(projADir);
        await File.WriteAllTextAsync(Path.Combine(projADir, "ProjectA.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var sbMethods = new System.Text.StringBuilder();
        for (int i = 1; i <= 20; i++)
        {
            sbMethods.AppendLine($"    public void Method{i}() {{ }}");
        }

        var projACode = $@"
namespace ProjectA;

public interface IOrderService
{{
    void ProcessOrder();
}}

public class OrderGodObject : IOrderService
{{
    public void ProcessOrder() {{ }}
{sbMethods}
}}

public class DeadClass
{{
    public void UnusedMethod() {{ }}
}}
";
        await File.WriteAllTextAsync(Path.Combine(projADir, "OrderGodObject.cs"), projACode);

        // Project B: TypeScript project with a Controller
        var projBDir = Path.Combine(_tempWorkspace, "ProjectB").Replace('\\', '/');
        Directory.CreateDirectory(projBDir);
        await File.WriteAllTextAsync(Path.Combine(projBDir, "package.json"), "{}");

        var projBCode = @"
import { Controller, Post, Get } from '@nestjs/common';

@Controller('orders')
export class OrdersController {
    @Post('charge')
    async chargeOrder() {}

    @Get('status')
    async getOrderStatus() {}
}
";
        await File.WriteAllTextAsync(Path.Combine(projBDir, "OrdersController.ts"), projBCode);

        _dbPath = Path.Combine(_tempWorkspace, "test_queries.db");
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.AutoRegisterParsers();

        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task Test_GetProjectEntryPoints_ReturnsRows()
    {
        var query = Queries.Get("get_project_entry_points");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["projectName"] = "ProjectB" });

        using var doc = JsonDocument.Parse(res);

        var array = doc.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "get_project_entry_points returned 0 rows for ProjectB!");
        var names = array.Select(x => x.GetProperty("entryPoint").GetString()).ToList();
        names.Any(n => n is "chargeOrder" or "getOrderStatus").Should().BeTrue();
    }

    [Test]
    public async Task Test_FindRefactorGodObjects_ReturnsRows()
    {
        var query = Queries.Get("find_refactor_god_objects");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["projectName"] = "ProjectA" });

        using var doc = JsonDocument.Parse(res);

        var array = doc.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "find_refactor_god_objects returned 0 rows for ProjectA!");
        var firstName = array[0].GetProperty("name").GetString();
        firstName.Should().Be("OrderGodObject");
    }

    [Test]
    public async Task Test_FindRefactorDeadCode_ReturnsRows()
    {
        var query = Queries.Get("find_refactor_dead_code");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["projectName"] = "ProjectA" });

        using var doc = JsonDocument.Parse(res);

        var array = doc.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "find_refactor_dead_code returned 0 rows for ProjectA!");
        var names = array.Select(x => x.GetProperty("name").GetString()).ToList();
        names.Any(n => n is "DeadClass" or "UnusedMethod").Should().BeTrue();
    }

    [Test]
    public async Task Test_GetFileOutline_ReturnsRows()
    {
        var query = Queries.Get("get_file_outline");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["filePath"] = "OrdersController.ts" });

        using var doc = JsonDocument.Parse(res);

        var array = doc.RootElement.EnumerateArray().ToList();

        array.Count.Should().BeGreaterThan(0, "get_file_outline returned 0 rows for OrdersController.ts!");
        var names = array.Select(x => x.GetProperty("name").GetString()).ToList();
        names.Should().Contain("OrdersController");
    }

    [Test]
    public async Task Test_FindSymbol_Queries_ReturnMatches()
    {
        var qClass = Queries.Get("find_symbol_class");
        var resClass = await _client.ExecuteQueryAsync(qClass, new Dictionary<string, object?> { ["name"] = "OrdersController" });
        using (var doc = JsonDocument.Parse(resClass))
        {
            doc.RootElement.GetArrayLength().Should().BeGreaterThan(0, "find_symbol_class failed");
        }

        var qInterface = Queries.Get("find_symbol_interface");
        var resInterface = await _client.ExecuteQueryAsync(qInterface, new Dictionary<string, object?> { ["name"] = "IOrderService" });
        using (var doc = JsonDocument.Parse(resInterface))
        {
            doc.RootElement.GetArrayLength().Should().BeGreaterThan(0, "find_symbol_interface failed");
        }

        var qFunc = Queries.Get("find_symbol_function");
        var resFunc = await _client.ExecuteQueryAsync(qFunc, new Dictionary<string, object?> { ["name"] = "chargeOrder" });
        using (var doc = JsonDocument.Parse(resFunc))
        {
            doc.RootElement.GetArrayLength().Should().BeGreaterThan(0, "find_symbol_function failed");
        }
    }

    [Test]
    public async Task Test_GetArchitectureMapWorkspace_ReturnsProjects()
    {
        var query = Queries.Get("get_architecture_map_workspace");
        var res = await _client.ExecuteQueryAsync(query);

        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();
        array.Count.Should().BeGreaterThan(0);
        var projects = array[0].GetProperty("projects").EnumerateArray().ToList();
        projects.Count.Should().BeGreaterThanOrEqualTo(2);
        var projectNames = projects.Select(p => p.GetProperty("name").GetString()).ToList();
        projectNames.Should().Contain("ProjectA");
        projectNames.Should().Contain("ProjectB");
    }

    [Test]
    public async Task Test_GetArchitectureOverview_ReturnsProjects()
    {
        var query = Queries.Get("get_architecture_overview");
        var res = await _client.ExecuteQueryAsync(query);

        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();
        array.Count.Should().BeGreaterThan(0);
        var totalProjects = array[0].GetProperty("totalProjects").GetInt32();
        totalProjects.Should().BeGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Test_InspectDataLineage_EmptyOrNullTableName_ReturnsWithoutError()
    {
        var query = Queries.Get("inspect_data_lineage");
        var resEmpty = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["tableName"] = "" });
        using (var docEmpty = JsonDocument.Parse(resEmpty))
        {
            docEmpty.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        }

        var resNull = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["tableName"] = null });
        using (var docNull = JsonDocument.Parse(resNull))
        {
            docNull.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }
}
