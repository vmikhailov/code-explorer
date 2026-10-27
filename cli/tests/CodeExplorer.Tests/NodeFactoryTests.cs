using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes.Layer5_Logical;
using CodeExplorer.Core.Database;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class NodeFactoryTests
{
    private static readonly JsonSerializerOptions LegacySerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static Dictionary<string, object> LegacyFromNode(IOntologyNode node)
    {
        var json = JsonSerializer.Serialize(node, node.GetType(), LegacySerializerOptions);
        var rawProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(json, LegacySerializerOptions) 
            ?? new Dictionary<string, object>();

        var properties = new Dictionary<string, object>();
        foreach (var kvp in rawProperties)
        {
            var cleanedVal = JsonHelper.ConvertJsonElement(kvp.Value);
            if (cleanedVal != null)
            {
                properties[kvp.Key] = cleanedVal;
            }
        }

        if (node.Extensions != null)
        {
            foreach (var kvp in node.Extensions)
            {
                if (int.TryParse(kvp.Value, out var intVal) &&
                    (kvp.Key.EndsWith("_line") || kvp.Key.EndsWith("_col") || kvp.Key is "line" or "column" or "arg_index" or "start_line" or "end_line" or "stage_order" or "loc" or "line_count" or "statement_count"))
                {
                    properties[kvp.Key] = intVal;
                }
                else if (long.TryParse(kvp.Value, out var longVal) && kvp.Key is "size_bytes")
                {
                    properties[kvp.Key] = longVal;
                }
                else if (bool.TryParse(kvp.Value, out var boolVal) && kvp.Key is "is_test" or "is_library" or "is_empty" or "has_comment" or "is_decorator" or "is_internal" or "is_unsafe" or "is_async")
                {
                    properties[kvp.Key] = boolVal;
                }
                else
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
        }

        return properties;
    }

    [Test]
    public void NodeFactory_MatchesLegacySerialization_AcrossAllNodeTypes()
    {
        var testNodes = new List<IOntologyNode>
        {
            new TableNode("tbl:1", "users", "src/models/user.cs", new Dictionary<string, string> { ["line"] = "42", ["is_test"] = "true" }),
            new TypeNode("type:1", "UserService", "App.UserService", "src/user.cs", "user.cs", 10, 50, 1, 1, "class", false, "Service", "App.Core", 1, 40, new Dictionary<string, string> { ["custom_key"] = "custom_val" }),
            new FunctionNode("fn:1", "GetUser", "App.UserService.GetUser", "src/user.cs", "user.cs", 15, 25, 5, 1, false, "Service", "App.Core", 1, 10),
            new FileNode("f:1", "user.cs", "src/user.cs", "/workspace/src/user.cs", false, "Service", "App.Core", 100, 2048L),
            new EndpointNode("ep:1", "GET /api/users", "src/user.cs", "GET", "/api/users"),
            new ProjectNode("p:1", "App.Core", "src/App.Core.csproj", "CSharp", "Service"),
            new DomainNode("dom:1", "UserManagement", "User Management", "Core", "Manages users and auth", "user", "src/domains/user"),
            new BoundedContextNode("bc:1", "Identity", "Core identity", "dom:1", "Summary", "blue", "gray", 95.5, "src/identity"),
            new CatchClauseNode("catch:1", "catch", "ArgumentNullException", "src/user.cs", 30, 1, false, true, 2),
            new CallLiteralNode("call:1", "arg", "Hello", "App.Log", 0, "src/user.cs", 12, 5),
            new QueryNode("q:1", "SelectUsers", "SELECT * FROM users", "src/repo.cs", 10, 15)
        };

        foreach (var node in testNodes)
        {
            var legacyProps = LegacyFromNode(node);
            var modernNode = NodeFactory.Create(node);
            var modernProps = modernNode.Properties;

            modernNode.Id.Should().Be(node.Id);
            modernNode.Kind.Should().Be(node.Kind);

            var legacyKeys = legacyProps.Keys.OrderBy(k => k).ToList();
            var modernKeys = modernProps.Keys.OrderBy(k => k).ToList();

            modernKeys.Should().Equal(legacyKeys, $"Properties keys should be identical for {node.GetType().Name}");

            foreach (var key in legacyKeys)
            {
                var legacyVal = legacyProps[key]?.ToString();
                var modernVal = modernProps[key]?.ToString();
                modernVal.Should().Be(legacyVal, $"Value for property '{key}' should match on {node.GetType().Name}");
            }
        }
    }

    [Test]
    public void NodeFactory_PerformanceBenchmark_SignificantlyFasterThanLegacy()
    {
        var sampleNode = new TypeNode(
            "type:sample", "OrderProcessor", "App.OrderProcessor",
            "src/orders/processor.cs", "processor.cs", 10, 120, 1, 1,
            "class", false, "Service", "App.Orders", 2, 95,
            new Dictionary<string, string> { ["line"] = "10", ["is_test"] = "false", ["custom_tag"] = "important" }
        );

        // Warmup
        for (int i = 0; i < 500; i++)
        {
            LegacyFromNode(sampleNode);
            NodeFactory.Create(sampleNode);
        }

        const int iterations = 20_000;

        // Legacy benchmark
        var swLegacy = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            LegacyFromNode(sampleNode);
        }
        swLegacy.Stop();

        // NodeFactory benchmark
        var swModern = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            NodeFactory.Create(sampleNode);
        }
        swModern.Stop();

        TestContext.Out.WriteLine($"=== Benchmark Results ({iterations:N0} iterations) ===");
        TestContext.Out.WriteLine($"Legacy JsonSerializer: {swLegacy.ElapsedMilliseconds} ms ({swLegacy.Elapsed.TotalMicroseconds / iterations:F2} µs/op)");
        TestContext.Out.WriteLine($"NodeFactory IL-Emit:   {swModern.ElapsedMilliseconds} ms ({swModern.Elapsed.TotalMicroseconds / iterations:F2} µs/op)");
        TestContext.Out.WriteLine($"Speedup:               {(double)swLegacy.ElapsedMilliseconds / Math.Max(1, swModern.ElapsedMilliseconds):F1}x faster");

        swModern.ElapsedMilliseconds.Should().BeLessThan(swLegacy.ElapsedMilliseconds, "NodeFactory must be strictly faster than JSON serialization");
    }
}

