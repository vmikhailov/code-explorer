using AwesomeAssertions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class DiagramExportTests
{
    private string _tempDir = null!;
    private SqliteGraphClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_diagram_test_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(_tempDir);

        var projDir = Path.Combine(_tempDir, "OrderService").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderService.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        var code = """
        using System.ComponentModel.DataAnnotations.Schema;

        [Table("orders")]
        public class Order
        {
            public int Id { get; set; }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(projDir, "Order.cs"), code);

        var appsettings = """
        {
          "ConnectionStrings": {
            "Postgres": "Host=localhost;Database=orders_db;Username=postgres;Password=secret;"
          },
          "Stripe": {
            "ApiKey": "sk_test_123"
          }
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(projDir, "appsettings.json"), appsettings);

        var libDir = Path.Combine(_tempDir, "CommonUtils").Replace('\\', '/');
        Directory.CreateDirectory(libDir);
        await File.WriteAllTextAsync(Path.Combine(libDir, "CommonUtils.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(libDir, "Utils.cs"), "public class Utils { }");

        var dbPath = Path.Combine(_tempDir, "diagram_test.db").Replace('\\', '/');
        _client = new SqliteGraphClient(dbPath);

        WorkspaceIndexer.AutoRegisterParsers();
        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempDir, _tempDir, clear: true);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task Test_ExportMermaidArchitecture()
    {
        var mermaid = await DiagramExporter.ExportAsync(_client, format: "mermaid", type: "architecture");
        mermaid.Should().StartWith("flowchart TD");
        mermaid.Should().Contain("OrderService");
        mermaid.Should().NotContain("CommonUtils");
        mermaid.Should().ContainEquivalentOf("Postgres");
        mermaid.Should().Contain("Stripe");
    }

    [Test]
    public async Task Test_ExportMermaidArchitecture_WithLibraries_IncludesLibrariesSubgraph()
    {
        var mermaid = await DiagramExporter.ExportAsync(_client, format: "mermaid", type: "architecture", includeLibraries: true);
        mermaid.Should().StartWith("flowchart TD");
        mermaid.Should().Contain("OrderService");
        mermaid.Should().Contain("CommonUtils");
        mermaid.Should().Contain("subgraph Libraries [Shared Libraries & SDKs]");
    }

    [Test]
    public async Task Test_ExportC4Architecture()
    {
        var c4 = await DiagramExporter.ExportAsync(_client, format: "c4", type: "architecture");
        c4.Should().StartWith("C4Container");
        c4.Should().Contain("Container(");
        c4.Should().NotContain("CommonUtils");
        c4.Should().Contain("ContainerDb(");
    }

    [Test]
    public async Task Test_ExportC4Architecture_WithLibraries()
    {
        var c4 = await DiagramExporter.ExportAsync(_client, format: "c4", type: "architecture", includeLibraries: true);
        c4.Should().StartWith("C4Container");
        c4.Should().Contain("Container(");
        c4.Should().Contain("CommonUtils");
        c4.Should().Contain("Shared Library");
    }

    [Test]
    public async Task Test_ExportDataLineage()
    {
        var lineage = await DiagramExporter.ExportAsync(_client, format: "mermaid", type: "lineage");
        lineage.Should().StartWith("flowchart LR");
        lineage.Should().Contain("Order");
        lineage.Should().Contain("orders");
        lineage.Should().Contain("PERSISTED_IN");
    }

    [Test]
    public async Task Test_ExportDomainMermaid()
    {
        var domain = await DiagramExporter.ExportAsync(_client, format: "mermaid", type: "domain");
        domain.Should().StartWith("flowchart TD");
        domain.Should().Contain("Databases");
        domain.Should().ContainEquivalentOf("OrderService");
    }
}
