using System.Diagnostics;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using NUnit.Framework;

namespace CodeExplorer.Tests.Benchmarks;

[TestFixture]
public class SyntheticBenchmarkGraphTests
{
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;
    private CodeExplorerRepository _repo = null!;
    private BenchmarkGraphStats _stats = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"synthetic_bench_100k_{Guid.NewGuid():N}.db");
        _client = new SqliteGraphClient(_dbPath);
        _repo = new CodeExplorerRepository(_client);

        TestContext.WriteLine("Generating synthetic 100k-node benchmark graph...");
        _stats = await SyntheticBenchmarkGraphGenerator.GenerateAsync(_client);
        TestContext.WriteLine($"Generated {_stats.TotalNodes:N0} nodes and {_stats.TotalRelationships:N0} relationships in {_stats.GenerationDurationMs}ms.");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }

    [Test]
    public void Test_01_BenchmarkGraphScale_MeetsSpecification()
    {
        Assert.That(_stats.FileCount, Is.EqualTo(10_000), "Specification requires 10,000 files.");
        Assert.That(_stats.ClassCount, Is.EqualTo(25_000), "Specification requires 25,000 classes.");
        Assert.That(_stats.MethodCount, Is.EqualTo(70_000), "Specification requires 70,000 methods.");
        Assert.That(_stats.TotalNodes, Is.GreaterThanOrEqualTo(100_000), "Specification requires >= 100k nodes.");
        Assert.That(_stats.TotalRelationships, Is.GreaterThanOrEqualTo(300_000), "Specification requires >= 300k relationships.");
    }

    [Test]
    public async Task Test_02_FindSymbol_MeetsSla_Under25ms()
    {
        const int slaMs = 25;

        // Warmup
        var warmup = await _repo.FindSymbolAsync(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol);
        Assert.That(warmup, Does.Contain(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol));

        // Measured runs
        const int iterations = 5;
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await _repo.FindSymbolAsync(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
            Assert.That(result, Does.Contain(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol));
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        TestContext.WriteLine($"find_symbol SLA check: min={minMs}ms, avg={avgMs:F2}ms (SLA < {slaMs}ms)");

        Assert.That(minMs, Is.LessThan(slaMs),
            $"find_symbol minimum execution time ({minMs}ms) exceeded SLA of {slaMs}ms");
    }

    [Test]
    public async Task Test_03_GetCallChain_MeetsSla_Under100ms()
    {
        const int slaMs = 100;

        // Warmup
        var warmup = await _repo.GetCallChainAsync(
            SyntheticBenchmarkGraphGenerator.WellKnownStartFunction,
            SyntheticBenchmarkGraphGenerator.WellKnownEndFunction,
            maxDepth: 5);
        Assert.That(warmup, Does.Contain("Step0"));
        Assert.That(warmup, Does.Contain("Step4"));

        // Measured runs
        const int iterations = 5;
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await _repo.GetCallChainAsync(
                SyntheticBenchmarkGraphGenerator.WellKnownStartFunction,
                SyntheticBenchmarkGraphGenerator.WellKnownEndFunction,
                maxDepth: 5);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
            Assert.That(result, Does.Contain("Step0"));
            Assert.That(result, Does.Contain("Step4"));
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        TestContext.WriteLine($"get_call_chain SLA check: min={minMs}ms, avg={avgMs:F2}ms (SLA < {slaMs}ms)");

        Assert.That(minMs, Is.LessThan(slaMs),
            $"get_call_chain minimum execution time ({minMs}ms) exceeded SLA of {slaMs}ms");
    }

    [Test]
    public async Task Test_04_InspectDataLineage_MeetsSla_Under50ms()
    {
        const int slaMs = 50;

        // Warmup
        var warmup = await _repo.InspectDataLineageAsync(SyntheticBenchmarkGraphGenerator.WellKnownTable);
        Assert.That(warmup, Does.Contain(SyntheticBenchmarkGraphGenerator.WellKnownTable));
        Assert.That(warmup, Does.Contain("OrderEntity"));
        Assert.That(warmup, Does.Contain(SyntheticBenchmarkGraphGenerator.WellKnownCaller));

        // Measured runs
        const int iterations = 5;
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await _repo.InspectDataLineageAsync(SyntheticBenchmarkGraphGenerator.WellKnownTable);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
            Assert.That(result, Does.Contain(SyntheticBenchmarkGraphGenerator.WellKnownTable));
            Assert.That(result, Does.Contain("OrderEntity"));
            Assert.That(result, Does.Contain(SyntheticBenchmarkGraphGenerator.WellKnownCaller));
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        TestContext.WriteLine($"inspect_data_lineage SLA check: min={minMs}ms, avg={avgMs:F2}ms (SLA < {slaMs}ms)");

        Assert.That(minMs, Is.LessThan(slaMs),
            $"inspect_data_lineage minimum execution time ({minMs}ms) exceeded SLA of {slaMs}ms");
    }
}
