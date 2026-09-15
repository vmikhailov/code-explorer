using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
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
        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderService.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

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

        var dbPath = Path.Combine(_tempDir, "diagram_test.db").Replace('\\', '/');
        _client = new SqliteGraphClient(dbPath);

        WorkspaceIndexer.Register(new CSharpParser());
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
        Assert.That(mermaid, Does.StartWith("flowchart TD"));
        Assert.That(mermaid, Does.Contain("OrderService"));
        Assert.That(mermaid, Does.Contain("Postgres"));
        Assert.That(mermaid, Does.Contain("Stripe"));
    }

    [Test]
    public async Task Test_ExportC4Architecture()
    {
        var c4 = await DiagramExporter.ExportAsync(_client, format: "c4", type: "architecture");
        Assert.That(c4, Does.StartWith("C4Container"));
        Assert.That(c4, Does.Contain("Container("));
        Assert.That(c4, Does.Contain("ContainerDb("));
    }

    [Test]
    public async Task Test_ExportDataLineage()
    {
        var lineage = await DiagramExporter.ExportAsync(_client, format: "mermaid", type: "lineage");
        Assert.That(lineage, Does.StartWith("flowchart LR"));
        Assert.That(lineage, Does.Contain("Order"));
        Assert.That(lineage, Does.Contain("orders"));
        Assert.That(lineage, Does.Contain("PERSISTED_IN"));
    }
}
