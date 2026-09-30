using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TestImpactAnalysisTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_tia_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);

        // 1. Service Project
        var serviceDir = Path.Combine(_tempWorkspace, "OrderService");
        Directory.CreateDirectory(serviceDir);

        var serviceCsproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(Path.Combine(serviceDir, "OrderService.csproj"), serviceCsproj);

        var serviceCode = @"namespace OrderService;

public class OrderProcessor
{
    public void ProcessPayment()
    {
        System.Console.WriteLine(""Processing..."");
    }
}";
        await File.WriteAllTextAsync(Path.Combine(serviceDir, "OrderProcessor.cs"), serviceCode);

        // 2. Test Project referencing Service
        var testDir = Path.Combine(_tempWorkspace, "OrderService.Tests");
        Directory.CreateDirectory(testDir);

        var testCsproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""xunit"" Version=""2.9.0"" />
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.11.0"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""..\OrderService\OrderService.csproj"" />
  </ItemGroup>
</Project>";
        await File.WriteAllTextAsync(Path.Combine(testDir, "OrderService.Tests.csproj"), testCsproj);

        var testCode = @"namespace OrderService.Tests;

using OrderService;
using Xunit;

public class OrderProcessorTests
{
    [Fact]
    public void Test_ProcessPayment_Success()
    {
        var proc = new OrderProcessor();
        proc.ProcessPayment();
    }
}";
        await File.WriteAllTextAsync(Path.Combine(testDir, "OrderProcessorTests.cs"), testCode);

        _dbPath = Path.Combine(_tempWorkspace, "test_tia.db");
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.Register(new CSharpParser());

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
    public async Task Test_TestSuiteNode_IsMaterialized_And_HasTestsRelationship()
    {
        // Check that TestSuite node exists in the graph
        var res = await _client.ExecuteQueryAsync("MATCH (ts:TestSuite) RETURN ts.name AS name, ts.kind AS kind");
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "No TestSuite node materialized!");
        var suiteName = array[0].GetProperty("name").GetString();
        Assert.That(suiteName, Does.Contain("OrderService.Tests").IgnoreCase);

        // Check TESTS relationship from TestSuite to target
        var testsRelRes = await _client.ExecuteQueryAsync("MATCH (ts:TestSuite)-[r:TESTS]->(target) RETURN ts.name AS suite, target.name AS target");
        using var relDoc = JsonDocument.Parse(testsRelRes);
        var relArray = relDoc.RootElement.EnumerateArray().ToList();
        Assert.That(relArray.Count, Is.GreaterThan(0), "TestSuite does not have a TESTS relationship!");
    }

    [Test]
    public async Task Test_FunctionNode_IsTaggedAsTest()
    {
        var res = await _client.ExecuteQueryAsync("MATCH (f:Function) WHERE f.name = 'Test_ProcessPayment_Success' RETURN f.name AS name, f.is_test AS isTest, f.test_framework AS framework");
        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.EqualTo(1), "Expected 1 Test_ProcessPayment_Success function node");
        var isTest = array[0].GetProperty("isTest").GetString();
        Assert.That(isTest, Is.EqualTo("true"));
        var framework = array[0].GetProperty("framework").GetString();
        Assert.That(framework, Is.EqualTo("xunit"));
    }

    [Test]
    public async Task Test_GetAffectedTests_TracesChangedSourceFile_ToTest()
    {
        var query = Queries.Get("get_affected_tests");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?>
        {
            ["filePath"] = "OrderProcessor.cs",
            ["symbolName"] = null
        });

        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "Expected at least 1 affected test for OrderProcessor.cs");
        var testNames = array.Select(x => x.GetProperty("testName").GetString()).ToList();
        Assert.That(testNames, Does.Contain("Test_ProcessPayment_Success"));
    }

    [Test]
    public async Task Test_GetAffectedTests_TracesChangedTestFile_ToSelf()
    {
        var query = Queries.Get("get_affected_tests");
        var res = await _client.ExecuteQueryAsync(query, new Dictionary<string, object?>
        {
            ["filePath"] = "OrderProcessorTests.cs",
            ["symbolName"] = null
        });

        using var doc = JsonDocument.Parse(res);
        var array = doc.RootElement.EnumerateArray().ToList();

        Assert.That(array.Count, Is.GreaterThan(0), "Expected at least 1 affected test for OrderProcessorTests.cs");
        var testNames = array.Select(x => x.GetProperty("testName").GetString()).ToList();
        Assert.That(testNames, Does.Contain("Test_ProcessPayment_Success"));
    }
}
