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

    [Test]
    public async Task Test_FindRefactorGodFunctions_EmptyCatches_And_NoiseFiltering()
    {
        var psRes = await _client.ExecuteCypherAsync("MATCH (p:Project {name: 'ProjectA'})<-[:BELONGS_TO]-(ps:ProjectSyntax) RETURN ps.id AS id");
        psRes.Rows.Should().NotBeEmpty();
        var psId = psRes.Rows[0].GetStringProp("id");

        var nodes = new List<Node>
        {
            new("ws:file:ProjectA:BigComponent.tsx", "File", new Dictionary<string, object> { ["path"] = "ProjectA/BigComponent.tsx" }),
            new("ws:file:ProjectA:app.config.ts", "File", new Dictionary<string, object> { ["path"] = "ProjectA/app.config.ts" }),
            new("ws:fn:ProjectA:BigComponent", "Function", new Dictionary<string, object>
            {
                ["name"] = "BigComponent",
                ["file_path"] = "ProjectA/BigComponent.tsx",
                ["start_line"] = 1,
                ["end_line"] = 240,
                ["is_test"] = false,
                ["symbol"] = "ws:sym:ProjectA/BigComponent.tsx:Function:BigComponent"
            }),
            new("ws:fn:ProjectA:module_init", "Function", new Dictionary<string, object>
            {
                ["name"] = "<module_init>",
                ["file_path"] = "ProjectA/app.config.ts",
                ["start_line"] = 1,
                ["end_line"] = 300,
                ["is_test"] = false
            }),
            new("ws:fn:ProjectA:JestSuite", "Function", new Dictionary<string, object>
            {
                ["name"] = "Jest:Suite:BigComponent",
                ["file_path"] = "ProjectA/BigComponent.test.tsx",
                ["start_line"] = 1,
                ["end_line"] = 280,
                ["is_test"] = true
            }),
            new("ws:cc:ProjectA:BigComponent:42", "CatchClause", new Dictionary<string, object>
            {
                ["name"] = "catch",
                ["file_path"] = "ProjectA/BigComponent.tsx",
                ["line"] = 42,
                ["is_empty"] = true,
                ["is_test"] = false,
                ["project_name"] = "ProjectA"
            }),
            new("ws:cc:ProjectA:TestCatch:12", "CatchClause", new Dictionary<string, object>
            {
                ["name"] = "catch",
                ["file_path"] = "ProjectA/BigComponent.test.tsx",
                ["line"] = 12,
                ["is_empty"] = true,
                ["is_test"] = true,
                ["project_name"] = "ProjectA"
            })
        };
        var rels = new List<Relationship>
        {
            new(psId, "ws:fn:ProjectA:BigComponent", "CONTAINS"),
            new(psId, "ws:fn:ProjectA:module_init", "CONTAINS"),
            new(psId, "ws:fn:ProjectA:JestSuite", "CONTAINS"),
            new("ws:fn:ProjectA:BigComponent", "ws:file:ProjectA:BigComponent.tsx", "DECLARED_IN"),
            new("ws:fn:ProjectA:module_init", "ws:file:ProjectA:app.config.ts", "DECLARED_IN")
        };
        await _client.UploadNodesAsync(nodes);
        await _client.UploadRelationshipsAsync(rels);

        var godFnRes = await _client.ExecuteCypherAsync(Queries.Get("find_refactor_god_functions"), new { projectName = "ProjectA" });
        var godFnNames = godFnRes.Rows.Select(r => r.GetStringProp("name")).ToList();
        godFnNames.Should().Contain("BigComponent");
        godFnNames.Should().NotContain("<module_init>");
        godFnNames.Should().NotContain("Jest:Suite:BigComponent");

        var catchRes = await _client.ExecuteCypherAsync(Queries.Get("find_refactor_empty_catches"), new { projectName = "ProjectA" });
        catchRes.Rows.Should().ContainSingle(r => r.GetStringProp("filePath") == "ProjectA/BigComponent.tsx");

        var deadRes = await _client.ExecuteCypherAsync(Queries.Get("find_refactor_dead_code"), new { projectName = "ProjectA" });
        var deadNames = deadRes.Rows.Select(r => r.GetStringProp("name")).ToList();
        deadNames.Should().NotContain("<module_init>");
        deadNames.Should().NotContain("Jest:Suite:BigComponent");
    }

    [Test]
    public async Task Test_FindRefactorDeadCode_ExcludesNextJsAppRouterConventions()
    {
        var psId = "ws:ps:NextApp";
        var nodes = new List<Node>
        {
            new("ws:proj:NextApp", "Project", new Dictionary<string, object> { ["name"] = "NextApp" }),
            new(psId, "ProjectSyntax", new Dictionary<string, object> { ["name"] = "NextApp" }),
            new("ws:file:NextApp:route.ts", "File", new Dictionary<string, object> { ["path"] = "apps/web/src/app/api/health/route.ts", ["artifact_role"] = "source" }),
            new("ws:file:NextApp:page.tsx", "File", new Dictionary<string, object> { ["path"] = "apps/web/src/app/[lang]/page.tsx", ["artifact_role"] = "source" }),
            new("ws:file:NextApp:not-found.tsx", "File", new Dictionary<string, object> { ["path"] = "apps/web/src/app/not-found.tsx", ["artifact_role"] = "source" }),
            new("ws:file:NextApp:robots.ts", "File", new Dictionary<string, object> { ["path"] = "apps/web/src/app/robots.ts", ["artifact_role"] = "source" }),
            new("ws:file:NextApp:dead.ts", "File", new Dictionary<string, object> { ["path"] = "apps/web/src/lib/dead.ts", ["artifact_role"] = "source" }),
            new("ws:fn:NextApp:GET", "Function", new Dictionary<string, object> { ["name"] = "GET", ["is_exported"] = true, ["is_test"] = false }),
            new("ws:fn:NextApp:POST", "Function", new Dictionary<string, object> { ["name"] = "POST", ["is_exported"] = true, ["is_test"] = false }),
            new("ws:fn:NextApp:HomePage", "Function", new Dictionary<string, object> { ["name"] = "HomePage", ["is_exported"] = true, ["is_test"] = false }),
            new("ws:fn:NextApp:generateMetadata", "Function", new Dictionary<string, object> { ["name"] = "generateMetadata", ["is_exported"] = true, ["is_test"] = false }),
            new("ws:fn:NextApp:NotFound", "Function", new Dictionary<string, object> { ["name"] = "NotFound", ["is_exported"] = true, ["is_test"] = false }),
            new("ws:fn:NextApp:unusedPageHelper", "Function", new Dictionary<string, object> { ["name"] = "unusedPageHelper", ["is_exported"] = false, ["is_test"] = false }),
            new("ws:fn:NextApp:robots", "Function", new Dictionary<string, object> { ["name"] = "robots", ["is_exported"] = true, ["is_test"] = false }),
            new("ws:fn:NextApp:trulyDeadFn", "Function", new Dictionary<string, object> { ["name"] = "trulyDeadFn", ["is_exported"] = true, ["is_test"] = false })
        };
        var rels = new List<Relationship>
        {
            new(psId, "ws:proj:NextApp", "BELONGS_TO"),
            new(psId, "ws:fn:NextApp:GET", "CONTAINS"),
            new(psId, "ws:fn:NextApp:POST", "CONTAINS"),
            new(psId, "ws:fn:NextApp:HomePage", "CONTAINS"),
            new(psId, "ws:fn:NextApp:generateMetadata", "CONTAINS"),
            new(psId, "ws:fn:NextApp:NotFound", "CONTAINS"),
            new(psId, "ws:fn:NextApp:unusedPageHelper", "CONTAINS"),
            new(psId, "ws:fn:NextApp:robots", "CONTAINS"),
            new(psId, "ws:fn:NextApp:trulyDeadFn", "CONTAINS"),
            new("ws:fn:NextApp:GET", "ws:file:NextApp:route.ts", "DECLARED_IN"),
            new("ws:fn:NextApp:POST", "ws:file:NextApp:route.ts", "DECLARED_IN"),
            new("ws:fn:NextApp:HomePage", "ws:file:NextApp:page.tsx", "DECLARED_IN"),
            new("ws:fn:NextApp:generateMetadata", "ws:file:NextApp:page.tsx", "DECLARED_IN"),
            new("ws:fn:NextApp:NotFound", "ws:file:NextApp:not-found.tsx", "DECLARED_IN"),
            new("ws:fn:NextApp:unusedPageHelper", "ws:file:NextApp:not-found.tsx", "DECLARED_IN"),
            new("ws:fn:NextApp:robots", "ws:file:NextApp:robots.ts", "DECLARED_IN"),
            new("ws:fn:NextApp:trulyDeadFn", "ws:file:NextApp:dead.ts", "DECLARED_IN")
        };
        await _client.UploadNodesAsync(nodes);
        await _client.UploadRelationshipsAsync(rels);

        var deadRes = await _client.ExecuteCypherAsync(Queries.Get("find_refactor_dead_code"), new { projectName = "NextApp" });
        var deadNames = deadRes.Rows.Select(r => r.GetStringProp("name")).ToList();

        deadNames.Should().NotContain("GET");
        deadNames.Should().NotContain("POST");
        deadNames.Should().NotContain("HomePage");
        deadNames.Should().NotContain("generateMetadata");
        deadNames.Should().NotContain("NotFound");
        deadNames.Should().NotContain("robots");
        deadNames.Should().Contain("unusedPageHelper");
        deadNames.Should().Contain("trulyDeadFn");
    }
}
