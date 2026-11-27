using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public sealed class PolynomialComplexityGateTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ce_poly_gates_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp directory cleanup on Windows
            }
        }
    }

    [Test]
    public async Task ComplexityMetricsAndGates_CatchLinearScansAndInterProceduralQuadraticLoops_WithoutFlaggingHashSetOrTreeDfs()
    {
        var projDir = Path.Combine(_tempDir, "SampleService");
        Directory.CreateDirectory(projDir);

        await File.WriteAllTextAsync(Path.Combine(projDir, "SampleService.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(Path.Combine(projDir, "OrderProcessor.cs"), """
            namespace SampleService;

            public sealed class TreeNode
            {
                public List<TreeNode> Children { get; } = [];
            }

            public sealed class OrderContext
            {
                public List<string> AllFiles { get; } = [];
                public Dictionary<string, string> BindingsLookup { get; } = new();
                public Dictionary<string, List<string>> ItemsByGroup { get; } = new();
            }

            public sealed class OrderProcessor
            {
                private static readonly string[] AllowedSuffixes = [".cs", ".ts", ".go"];

                // BAD 1 (SIG-M-R-003): List.Contains + LINQ FirstOrDefault inside a loop
                public List<string> CollectQuadraticDuplicates(List<string> rawItems, List<string> catalog)
                {
                    var seenList = new List<string>();
                    foreach (var item in rawItems)
                    {
                        if (!seenList.Contains(item) && catalog.FirstOrDefault(c => c == item) != null)
                        {
                            seenList.Add(item);
                        }
                    }
                    return seenList;
                }

                // BAD 2 (SIG-M-R-004): Outer loop -> intermediate helper -> inner loop over unpartitioned AllFiles / BindingsLookup
                public int ProcessProjectsQuadratically(List<string> projects, OrderContext ctx)
                {
                    var count = 0;
                    foreach (var proj in projects)
                    {
                        count += EnrichSingleProject(proj, ctx);
                    }
                    return count;
                }

                private int EnrichSingleProject(string proj, OrderContext ctx)
                {
                    return ScanGlobalFilesForProject(proj, ctx);
                }

                private int ScanGlobalFilesForProject(string proj, OrderContext ctx)
                {
                    var matched = 0;
                    foreach (var file in ctx.AllFiles)
                    {
                        if (file.StartsWith(proj)) matched++;
                    }
                    return matched;
                }

                // GOOD 1: HashSet.Contains + constant-bounded suffix array + partitioned TryGetValue lookup
                public int ProcessCleanLinear(List<string> items, OrderContext ctx)
                {
                    var seenSet = new HashSet<string>();
                    var total = 0;
                    foreach (var item in items)
                    {
                        if (seenSet.Contains(item)) continue;
                        seenSet.Add(item);
                        for (var i = 0; i < AllowedSuffixes.Length; i++)
                        {
                            if (item.EndsWith(AllowedSuffixes[i])) total++;
                        }
                        if (ctx.ItemsByGroup.TryGetValue(item, out var groupBucket))
                        {
                            foreach (var member in groupBucket)
                            {
                                total += member.Length;
                            }
                        }
                    }
                    return total;
                }

                // GOOD 2: Recursive tree DFS must not accumulate infinite caller_loop_depth across self-recursion
                public int CountTreeNodesDfs(TreeNode node)
                {
                    var count = 1;
                    foreach (var child in node.Children)
                    {
                        count += CountTreeNodesDfs(child);
                    }
                    return count;
                }
            }
            """);

        var dbDir = Path.Combine(_tempDir, ".codeexplorer");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "graph.db");

        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();
        var indexer = new WorkspaceIndexer(client, NullLogger<WorkspaceIndexer>.Instance);
        await indexer.IndexAsync(_tempDir, clear: true);

        // 1. Verify intra-procedural linear scan metrics
        var badLinearScans = await client.QueryScalarAsync<long>(
            "MATCH (f:Function {name: 'CollectQuadraticDuplicates'}) RETURN coalesce(f.loop_linear_scan_count, 0)");
        Assert.That(badLinearScans, Is.GreaterThanOrEqualTo(2));

        var cleanLinearScans = await client.QueryScalarAsync<long>(
            "MATCH (f:Function {name: 'ProcessCleanLinear'}) RETURN coalesce(f.loop_linear_scan_count, 0)");
        Assert.That(cleanLinearScans, Is.EqualTo(0));

        // 2. Verify inter-procedural loop amplification & Tarjan SCC recursion immunity
        var leafCallerDepth = await client.QueryScalarAsync<long>(
            "MATCH (f:Function {name: 'ScanGlobalFilesForProject'}) RETURN coalesce(f.caller_loop_depth, 0)");
        var leafHasUnpartitioned = await client.QueryScalarAsync<bool>(
            "MATCH (f:Function {name: 'ScanGlobalFilesForProject'}) RETURN coalesce(f.has_unpartitioned_loop, false)");
        Assert.That(leafCallerDepth, Is.EqualTo(1));
        Assert.That(leafHasUnpartitioned, Is.True);

        var dfsCallerDepth = await client.QueryScalarAsync<long>(
            "MATCH (f:Function {name: 'CountTreeNodesDfs'}) RETURN coalesce(f.caller_loop_depth, 0)");
        Assert.That(dfsCallerDepth, Is.EqualTo(0), "Self-recursive tree DFS must not amplify caller_loop_depth.");

        // 3. Verify built-in gates SIG-M-R-003 and SIG-M-R-004 catch the exact bad methods and spare the clean ones
        var engine = new GateExecutionEngine(client, new FileGateRepository(_tempDir));
        var r003 = await engine.CheckGateAsync(BuiltInGateCatalog.NoLinearScansInLoopsGate with
        {
            Ratchet = new() { Enabled = false }
        });
        Assert.That(r003.Violations, Is.Not.Empty);
        Assert.That(r003.Violations.Select(v => v.Symbol), Does.Contain("CollectQuadraticDuplicates"));
        Assert.That(r003.Violations.Select(v => v.Symbol), Does.Not.Contain("ProcessCleanLinear"));

        var r004 = await engine.CheckGateAsync(BuiltInGateCatalog.InterProceduralPolynomialCeilingGate with
        {
            Ratchet = new() { Enabled = false }
        });
        Assert.That(r004.Violations, Is.Not.Empty);
        Assert.That(r004.Violations.Any(v => (v.Symbol ?? string.Empty).Contains("EnrichSingleProject") && (v.Target ?? string.Empty).Contains("ScanGlobalFilesForProject")), Is.True);
        Assert.That(r004.Violations.Any(v => (v.Symbol ?? string.Empty).Contains("CountTreeNodesDfs")), Is.False);
    }
}
