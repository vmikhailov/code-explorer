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
    public string DefaultRegion {{ get; set; }} = ""eu-west-1"";
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
        var res = await _client.ExecuteCypherAsync(query, new { projectName = "ProjectB" });

        res.Count.Should().BeGreaterThan(0, "get_project_entry_points returned 0 rows for ProjectB!");
        var names = res.Rows.Select(x => x.GetStringProp("entryPoint")).ToList();
        names.Any(n => n is "chargeOrder" or "getOrderStatus").Should().BeTrue();
    }

    [Test]
    public async Task Test_FindRefactorGodObjects_ReturnsRows()
    {
        var query = Queries.Get("find_refactor_god_objects");
        var res = await _client.ExecuteCypherAsync(query, new { projectName = "ProjectA" });

        res.Count.Should().BeGreaterThan(0, "find_refactor_god_objects returned 0 rows for ProjectA!");
        var firstName = res.Rows[0].GetStringProp("name");
        firstName.Should().Be("OrderGodObject");
    }

    [Test]
    public async Task Test_FindRefactorDeadCode_ReturnsRows()
    {
        var query = Queries.Get("find_refactor_dead_code");
        var res = await _client.ExecuteCypherAsync(query, new { projectName = "ProjectA" });

        res.Count.Should().BeGreaterThan(0, "find_refactor_dead_code returned 0 rows for ProjectA!");
        var names = res.Rows.Select(x => x.GetStringProp("name")).ToList();
        names.Any(n => n is "DeadClass" or "UnusedMethod").Should().BeTrue();
    }

    [Test]
    public async Task Test_GetFileOutline_ReturnsRows()
    {
        var query = Queries.Get("get_file_outline");
        var res = await _client.ExecuteCypherAsync(query, new { filePath = "OrdersController.ts" });

        res.Count.Should().BeGreaterThan(0, "get_file_outline returned 0 rows for OrdersController.ts!");
        var names = res.Rows.Select(x => x.GetStringProp("name")).ToList();
        names.Should().Contain("OrdersController");
    }

    [Test]
    public async Task Test_FindSymbol_Queries_ReturnMatches()
    {
        var qClass = Queries.Get("find_symbol_class");
        var resClass = await _client.ExecuteCypherAsync(qClass, new { name = "OrdersController" });
        resClass.Count.Should().BeGreaterThan(0, "find_symbol_class failed");

        var qInterface = Queries.Get("find_symbol_interface");
        var resInterface = await _client.ExecuteCypherAsync(qInterface, new { name = "IOrderService" });
        resInterface.Count.Should().BeGreaterThan(0, "find_symbol_interface failed");

        var qFunc = Queries.Get("find_symbol_function");
        var resFunc = await _client.ExecuteCypherAsync(qFunc, new { name = "chargeOrder" });
        resFunc.Count.Should().BeGreaterThan(0, "find_symbol_function failed");
    }

    [Test]
    public async Task Test_GetArchitectureMapWorkspace_ReturnsProjects()
    {
        var query = Queries.Get("get_architecture_map_workspace");
        var res = await _client.ExecuteCypherAsync(query);

        res.Count.Should().BeGreaterThan(0);
        var projects = res.Rows[0]["projects"].AsArray();
        projects.Count.Should().BeGreaterThanOrEqualTo(2);
        var projectNames = projects.Select(p => p.GetStringProp("name")).ToList();
        projectNames.Should().Contain("ProjectA");
        projectNames.Should().Contain("ProjectB");
    }

    [Test]
    public async Task Test_GetArchitectureOverview_ReturnsProjects()
    {
        var query = Queries.Get("get_architecture_overview");
        var res = await _client.ExecuteCypherAsync(query);

        res.Count.Should().BeGreaterThan(0);
        var totalProjects = res.Rows[0].GetIntProp("totalProjects");
        totalProjects.Should().BeGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Test_InspectDataLineage_EmptyOrNullTableName_ReturnsWithoutError()
    {
        var query = Queries.Get("inspect_data_lineage");
        var resEmpty = await _client.ExecuteCypherAsync(query, new { tableName = "" });
        resEmpty.Rows.Should().NotBeNull();

        var resNull = await _client.ExecuteCypherAsync(query, new { tableName = (string?)null });
        resNull.Rows.Should().NotBeNull();
    }

    [Test]
    public async Task Test_Repository_SymbolSearchAndDirectoryOutline()
    {
        var repo = new CodeExplorerRepository(_client, defaultWorkspacePath: _tempWorkspace);

        var classYaml = await repo.FindSymbolAsync("OrdersController");
        classYaml.Should().Contain("ProjectB/OrdersController.ts");
        classYaml.Should().NotContain(_tempWorkspace);

        var memberYaml = await repo.FindSymbolAsync("DefaultRegion", symbolType: "Variable");
        memberYaml.Should().Contain("DefaultRegion");
        memberYaml.Should().Contain("ProjectA/OrderGodObject.cs");
        memberYaml.Should().NotContain(":sym:");

        var missingYaml = await repo.FindSymbolAsync("TerraformNonExistentProvisioner");
        missingYaml.Trim().Should().Be("[]");

        var absFilePath = Path.Combine(_tempWorkspace, "ProjectA", "OrderGodObject.cs");
        var absFileOutline = await repo.GetFileOutlineAsync(absFilePath);
        absFileOutline.Should().Contain("OrderGodObject");
        absFileOutline.Should().Contain("IOrderService");

        var dirOutline = await repo.GetFileOutlineAsync("ProjectA");
        dirOutline.Should().Contain("ProjectA/OrderGodObject.cs");
        dirOutline.Should().Contain("OrderGodObject");

        var rootOutline = await repo.GetFileOutlineAsync(".");
        rootOutline.Should().Contain("ProjectA");
        rootOutline.Should().Contain("ProjectB");
    }
}
