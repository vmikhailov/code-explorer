using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// Architectural invariant tests enforcing strict unidirectional hierarchy and model cohesion across ontology layers.
/// Executes on every build during the test suite.
/// </summary>
[TestFixture]
public class OntologyLayerArchitectureTests
{
    private static string FindCommonNodesDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "cli", "src", "Core", "CodeExplorer.Core", "Common", "Nodes");
            if (Directory.Exists(candidate))
                return Path.GetFullPath(candidate);

            candidate = Path.Combine(current, "src", "Core", "CodeExplorer.Core", "Common", "Nodes");
            if (Directory.Exists(candidate))
                return Path.GetFullPath(candidate);

            current = Path.GetDirectoryName(current);
        }

        throw new DirectoryNotFoundException("Could not locate Common/Nodes directory from " + AppContext.BaseDirectory);
    }

    [Test]
    public void BuiltInGateCatalog_RegistersOntologyGates()
    {
        var defaultGates = BuiltInGateCatalog.GetDefaultGates();

        defaultGates.Should().Contain(g => g.Id == "SIG-P-B-005", "Catalog must include SIG-P-B-005 Ontology Layer Strict Hierarchy Gate");
        defaultGates.Should().Contain(g => g.Id == "SIG-C-B-002", "Catalog must include SIG-C-B-002 Ontology Model Layer Cohesion Gate");

        BuiltInGateCatalog.OntologyLayerStrictHierarchyGate.Should().NotBeNull();
        BuiltInGateCatalog.OntologyModelCohesionGate.Should().NotBeNull();
    }

    [Test]
    public void OntologyLayer1_Physical_HasZeroReferencesToHigherLayers()
    {
        var nodesDir = FindCommonNodesDirectory();
        var layer1Dir = Path.Combine(nodesDir, "Layer1_Physical");
        Directory.Exists(layer1Dir).Should().BeTrue();

        var prohibitedNamespaces = new[]
        {
            "CodeExplorer.Core.Common.Nodes.Layer2_Boundaries",
            "CodeExplorer.Core.Common.Nodes.Layer3_Syntactic",
            "CodeExplorer.Core.Common.Nodes.Layer4_Semantic"
        };

        var prohibitedTokens = new[]
        {
            "ProjectNode", "PackageNode", "ProjectsStructureNode", "EntryPointNode",
            "TypeNode", "FunctionNode", "MemberNode", "CallLiteralNode", "CatchClauseNode", "ProjectSyntaxNode",
            "ServiceNode", "AppNode", "WorkerNode", "LibraryNode", "CliToolNode",
            "EndpointNode", "DatabaseNode", "TableNode", "TopicNode", "ExternalServiceNode"
        };

        foreach (var file in Directory.GetFiles(layer1Dir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            // Check using directives
            foreach (var usingDirective in root.Usings)
            {
                var ns = usingDirective.Name?.ToString() ?? "";
                prohibitedNamespaces.Should().NotContain(ns,
                    $"Layer 1 file '{Path.GetFileName(file)}' must not import higher ontology layer '{ns}'");
            }

            // Check generic attributes and type usages
            var identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => id.Identifier.Text)
                .ToHashSet();

            foreach (var prohibited in prohibitedTokens)
            {
                identifiers.Should().NotContain(prohibited,
                    $"Layer 1 file '{Path.GetFileName(file)}' must not reference higher-layer model '{prohibited}'");
            }
        }
    }

    [Test]
    public void OntologyLayer2_Boundaries_HasZeroReferencesToHigherLayers()
    {
        var nodesDir = FindCommonNodesDirectory();
        var layer2Dir = Path.Combine(nodesDir, "Layer2_Boundaries");
        Directory.Exists(layer2Dir).Should().BeTrue();

        var prohibitedNamespaces = new[]
        {
            "CodeExplorer.Core.Common.Nodes.Layer3_Syntactic",
            "CodeExplorer.Core.Common.Nodes.Layer4_Semantic"
        };

        var prohibitedTokens = new[]
        {
            "TypeNode", "FunctionNode", "MemberNode", "CallLiteralNode", "CatchClauseNode", "ProjectSyntaxNode",
            "ServiceNode", "AppNode", "WorkerNode", "LibraryNode", "CliToolNode",
            "EndpointNode", "DatabaseNode", "TableNode", "TopicNode", "ExternalServiceNode"
        };

        foreach (var file in Directory.GetFiles(layer2Dir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var usingDirective in root.Usings)
            {
                var ns = usingDirective.Name?.ToString() ?? "";
                prohibitedNamespaces.Should().NotContain(ns,
                    $"Layer 2 file '{Path.GetFileName(file)}' must not import higher ontology layer '{ns}'");
            }

            var identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => id.Identifier.Text)
                .ToHashSet();

            foreach (var prohibited in prohibitedTokens)
            {
                identifiers.Should().NotContain(prohibited,
                    $"Layer 2 file '{Path.GetFileName(file)}' must not reference higher-layer model '{prohibited}'");
            }
        }
    }

    [Test]
    public void OntologyLayer3_Syntactic_HasZeroReferencesToHigherLayers()
    {
        var nodesDir = FindCommonNodesDirectory();
        var layer3Dir = Path.Combine(nodesDir, "Layer3_Syntactic");
        Directory.Exists(layer3Dir).Should().BeTrue();

        var prohibitedNamespaces = new[]
        {
            "CodeExplorer.Core.Common.Nodes.Layer4_Semantic"
        };

        var prohibitedTokens = new[]
        {
            "ServiceNode", "AppNode", "WorkerNode", "LibraryNode", "CliToolNode",
            "EndpointNode", "DatabaseNode", "TableNode", "TopicNode", "ExternalServiceNode",
            "CloudServiceNode", "ApiInUseNode", "ProcedureNode", "QueryNode"
        };

        foreach (var file in Directory.GetFiles(layer3Dir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var usingDirective in root.Usings)
            {
                var ns = usingDirective.Name?.ToString() ?? "";
                prohibitedNamespaces.Should().NotContain(ns,
                    $"Layer 3 file '{Path.GetFileName(file)}' must not import higher ontology layer '{ns}'");
            }

            var identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => id.Identifier.Text)
                .ToHashSet();

            foreach (var prohibited in prohibitedTokens)
            {
                identifiers.Should().NotContain(prohibited,
                    $"Layer 3 file '{Path.GetFileName(file)}' must not reference higher-layer model '{prohibited}'");
            }
        }
    }

    [Test]
    public void OntologyModels_StrictCohesion_LayerMetadataMatchesDirectory()
    {
        var nodesDir = FindCommonNodesDirectory();

        var layerExpectations = new[]
        {
            ("Layer1_Physical", OntologyConstants.Layers.Physical),
            ("Layer2_Boundaries", OntologyConstants.Layers.ProjectBoundary),
            ("Layer3_Syntactic", OntologyConstants.Layers.Syntactic),
            ("Layer4_Semantic", OntologyConstants.Layers.Semantic),
        };

        foreach (var (folder, expectedLayer) in layerExpectations)
        {
            var dir = Path.Combine(nodesDir, folder);
            var files = Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly);
            files.Should().NotBeEmpty($"Ontology layer folder '{folder}' must contain model files.");

            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(source);
                var root = tree.GetCompilationUnitRoot();

                var records = root.DescendantNodes().OfType<RecordDeclarationSyntax>().ToList();
                records.Should().NotBeEmpty($"File '{Path.GetFileName(file)}' must declare at least one record node.");

                foreach (var record in records)
                {
                    // Must have [OntologyNode] attribute
                    var hasAttr = record.AttributeLists
                        .SelectMany(al => al.Attributes)
                        .Any(a => a.Name.ToString().Contains("OntologyNode"));

                    hasAttr.Should().BeTrue($"Record '{record.Identifier.Text}' in '{folder}' must be annotated with [OntologyNode].");
                }
            }
        }
    }

    [Test]
    public void OntologyModels_StrictCohesion_AllNodeTypesAreEncapsulatedInCoreCommonNodes()
    {
        // Enforce that in the entire CodeExplorer.Core assembly, only types under CodeExplorer.Core.Common.Nodes implement IOntologyNode
        var coreAssembly = typeof(IOntologyNode).Assembly;

        var ontologyNodeImplementations = coreAssembly.GetTypes()
            .Where(t => typeof(IOntologyNode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        ontologyNodeImplementations.Should().NotBeEmpty();

        foreach (var type in ontologyNodeImplementations)
        {
            type.Namespace.Should().StartWith("CodeExplorer.Core.Common.Nodes",
                $"Ontology model '{type.FullName}' must reside within CodeExplorer.Core.Common.Nodes namespace to preserve layer cohesion.");
        }
    }

    private static string? FindWorkspaceRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")) ||
                File.Exists(Path.Combine(current, "cli", "CodeExplorer.slnx")))
            {
                return Path.GetFullPath(current);
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    [Test]
    public async Task OntologyGates_EvaluateCleanlyOnWorkspaceGraph_WhenGraphExists()
    {
        var root = FindWorkspaceRoot();
        if (root == null)
        {
            Assert.Ignore("Workspace root not found.");
            return;
        }

        var dbPath = Path.Combine(root, ".codeexplorer", "graph.db");
        if (!File.Exists(dbPath))
        {
            Assert.Ignore("Local .codeexplorer/graph.db does not exist yet. Run 'ce scan' to evaluate on workspace graph.");
            return;
        }

        await using var client = new SqliteGraphClient(dbPath);
        var repo = new FileGateRepository(root);
        var engine = new GateExecutionEngine(client, repo);

        var hierarchyGate = BuiltInGateCatalog.OntologyLayerStrictHierarchyGate;
        var hierarchyResult = await engine.CheckGateAsync(hierarchyGate);
        hierarchyResult.Status.Should().Be(GateCheckStatus.Passed,
            $"OntologyLayerStrictHierarchyGate (SIG-P-B-005) must pass without violations: {string.Join("; ", hierarchyResult.Violations.Select(v => v.Message))}");

        var cohesionGate = BuiltInGateCatalog.OntologyModelCohesionGate;
        var cohesionResult = await engine.CheckGateAsync(cohesionGate);
        cohesionResult.Status.Should().Be(GateCheckStatus.Passed,
            $"OntologyModelCohesionGate (SIG-C-B-002) must pass without violations: {string.Join("; ", cohesionResult.Violations.Select(v => v.Message))}");
    }
}
