using AwesomeAssertions;
using CodeExplorer.Core.Analysis.GraphProjection;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GraphProjectionPipelineTests
{
    [Test]
    public void UniversalCypherEdgeLifter_LiftsInMemoryEdges_AndAggregatesWeights()
    {
        var rawEdges = new List<(string SourceLeafId, string TargetLeafId, string Kind, string? SourceFile, string? TargetFile, int? Line)>
        {
            ("fn:OrdersController.Create", "fn:OrderService.PlaceOrder", "CALLS", "OrdersController.cs", "OrderService.cs", 25),
            ("fn:OrdersController.Get", "fn:OrderService.GetOrder", "CALLS", "OrdersController.cs", "OrderService.cs", 40),
            ("fn:OrderService.PlaceOrder", "tbl:orders", "WRITES_TO", "OrderService.cs", null, 60),
            ("fn:OrdersController.Create", "fn:OrdersController.ValidateInternal", "CALLS", "OrdersController.cs", "OrdersController.cs", 10), // intra-container
        };

        var containerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fn:OrdersController.Create"] = "type:OrdersController",
            ["fn:OrdersController.Get"] = "type:OrdersController",
            ["fn:OrdersController.ValidateInternal"] = "type:OrdersController",
            ["fn:OrderService.PlaceOrder"] = "type:OrderService",
            ["fn:OrderService.GetOrder"] = "type:OrderService",
            ["tbl:orders"] = "tbl:orders"
        };

        var lifted = UniversalCypherEdgeLifter.LiftInMemory(rawEdges, leafId => containerMap.GetValueOrDefault(leafId));

        // Intra-container call must be filtered out
        lifted.Should().HaveCount(2);

        var controllerToService = lifted.Single(e => e.Source == "type:OrdersController" && e.Target == "type:OrderService");
        controllerToService.Kind.Should().Be("CALLS");
        controllerToService.Properties!["count"].Should().Be("2");
        controllerToService.Properties["distinct_files"].Should().Be("1");

        var serviceToTable = lifted.Single(e => e.Source == "type:OrderService" && e.Target == "tbl:orders");
        serviceToTable.Kind.Should().Be("WRITES_TO");
        serviceToTable.Properties!["count"].Should().Be("1");
    }

    [Test]
    public void TopologyAndCycleAnalyzer_DetectsCycles_AndMarksEdges()
    {
        var nodes = new List<GraphNodeDto>
        {
            new() { Id = "type:A", Name = "A", Kind = NodeKind.Type },
            new() { Id = "type:B", Name = "B", Kind = NodeKind.Type },
            new() { Id = "type:C", Name = "C", Kind = NodeKind.Type },
            new() { Id = "type:D", Name = "D", Kind = NodeKind.Type }
        };

        var edges = new List<GraphEdgeDto>
        {
            new() { Id = "e1", Source = "type:A", Target = "type:B", Kind = "CALLS" },
            new() { Id = "e2", Source = "type:B", Target = "type:C", Kind = "CALLS" },
            new() { Id = "e3", Source = "type:C", Target = "type:A", Kind = "CALLS" },
            new() { Id = "e4", Source = "type:D", Target = "type:A", Kind = "CALLS" }
        };

        var result = TopologyAndCycleAnalyzer.Analyze(nodes, edges);

        result.Cycles.Should().HaveCount(1);
        result.Cycles[0].Should().BeEquivalentTo(["type:A", "type:B", "type:C"]);

        result.CircularEdgeIds.Should().Contain(["e1", "e2", "e3"]);
        result.CircularEdgeIds.Should().NotContain("e4");
    }

    [Test]
    public void TopologyAndCycleAnalyzer_AssignsStandardTiers_FromIngressToStorage()
    {
        var nodes = new List<GraphNodeDto>
        {
            new() { Id = "ep:post", Name = "POST /orders", Kind = NodeKind.Endpoint },
            new() { Id = "type:Controller", Name = "OrdersController", Kind = NodeKind.Type },
            new() { Id = "type:Service", Name = "OrderService", Kind = NodeKind.Type },
            new() { Id = "type:Repo", Name = "OrderRepository", Kind = NodeKind.Type },
            new() { Id = "tbl:orders", Name = "orders", Kind = NodeKind.Table }
        };

        var edges = new List<GraphEdgeDto>
        {
            new() { Id = "e1", Source = "ep:post", Target = "type:Controller", Kind = "CALLS" },
            new() { Id = "e2", Source = "type:Controller", Target = "type:Service", Kind = "CALLS" },
            new() { Id = "e3", Source = "type:Service", Target = "type:Repo", Kind = "CALLS" },
            new() { Id = "e4", Source = "type:Repo", Target = "tbl:orders", Kind = "WRITES_TO" }
        };

        var result = TopologyAndCycleAnalyzer.Analyze(nodes, edges);

        result.NodeTiers["ep:post"].Should().Be(ArchitecturalTier.Ingress);
        result.NodeTiers["type:Controller"].Should().Be(ArchitecturalTier.Application);
        result.NodeTiers["type:Service"].Should().Be(ArchitecturalTier.Core);
        result.NodeTiers["type:Repo"].Should().Be(ArchitecturalTier.DataAccess);
        result.NodeTiers["tbl:orders"].Should().Be(ArchitecturalTier.DatabasesAndStorage);
    }
}
