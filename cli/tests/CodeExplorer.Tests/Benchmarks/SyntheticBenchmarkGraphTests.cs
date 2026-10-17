using System.Diagnostics;
using AwesomeAssertions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using NUnit.Framework;

namespace CodeExplorer.Tests.Benchmarks;

[TestFixture]
public class SyntheticBenchmarkGraphTests
{
    private static readonly int CiMultiplier = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ? 3 : 1;

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
        _stats.FileCount.Should().Be(10_000, "Specification requires 10,000 files.");
        _stats.ClassCount.Should().Be(25_000, "Specification requires 25,000 classes.");
        _stats.MethodCount.Should().Be(70_000, "Specification requires 70,000 methods.");
        _stats.TotalNodes.Should().BeGreaterThanOrEqualTo(100_000, "Specification requires >= 100k nodes.");
        _stats.TotalRelationships.Should().BeGreaterThanOrEqualTo(300_000, "Specification requires >= 300k relationships.");
    }

    [Test]
    public async Task Test_02_FindSymbol_MeetsSla_Under25ms()
    {
        int slaMs = 25 * CiMultiplier;

        // Warmup
        var warmup = await _repo.FindSymbolAsync(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol);
        warmup.Should().Contain(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol);

        // Measured runs
        const int iterations = 5;
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await _repo.FindSymbolAsync(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
            result.Should().Contain(SyntheticBenchmarkGraphGenerator.WellKnownSearchSymbol);
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        TestContext.WriteLine($"find_symbol SLA check: min={minMs}ms, avg={avgMs:F2}ms (SLA < {slaMs}ms)");

        minMs.Should().BeLessThan(slaMs, $"find_symbol minimum execution time ({minMs}ms) exceeded SLA of {slaMs}ms");
    }

    [Test]
    public async Task Test_03_GetCallChain_MeetsSla_Under100ms()
    {
        int slaMs = 100 * CiMultiplier;

        // Warmup
        var warmup = await _repo.GetCallChainAsync(
            SyntheticBenchmarkGraphGenerator.WellKnownStartFunction,
            SyntheticBenchmarkGraphGenerator.WellKnownEndFunction,
            maxDepth: 5);
        warmup.Should().Contain("Step0");
        warmup.Should().Contain("Step4");

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
            result.Should().Contain("Step0");
            result.Should().Contain("Step4");
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        TestContext.WriteLine($"get_call_chain SLA check: min={minMs}ms, avg={avgMs:F2}ms (SLA < {slaMs}ms)");

        minMs.Should().BeLessThan(slaMs, $"get_call_chain minimum execution time ({minMs}ms) exceeded SLA of {slaMs}ms");
    }

    [Test]
    public async Task Test_04_InspectDataLineage_MeetsSla_Under50ms()
    {
        int slaMs = 50 * CiMultiplier;

        // Warmup
        var warmup = await _repo.InspectDataLineageAsync(SyntheticBenchmarkGraphGenerator.WellKnownTable);
        warmup.Should().Contain(SyntheticBenchmarkGraphGenerator.WellKnownTable);
        warmup.Should().Contain("OrderEntity");
        warmup.Should().Contain(SyntheticBenchmarkGraphGenerator.WellKnownCaller);

        // Measured runs
        const int iterations = 5;
        var times = new List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = await _repo.InspectDataLineageAsync(SyntheticBenchmarkGraphGenerator.WellKnownTable);
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
            result.Should().Contain(SyntheticBenchmarkGraphGenerator.WellKnownTable);
            result.Should().Contain("OrderEntity");
            result.Should().Contain(SyntheticBenchmarkGraphGenerator.WellKnownCaller);
        }

        var avgMs = times.Average();
        var minMs = times.Min();
        TestContext.WriteLine($"inspect_data_lineage SLA check: min={minMs}ms, avg={avgMs:F2}ms (SLA < {slaMs}ms)");

        minMs.Should().BeLessThan(slaMs, $"inspect_data_lineage minimum execution time ({minMs}ms) exceeded SLA of {slaMs}ms");
    }
}
