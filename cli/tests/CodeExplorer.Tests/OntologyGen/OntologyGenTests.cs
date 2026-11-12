using NUnit.Framework;
using OntologyGen;

namespace CodeExplorer.Tests.OntologyGen;

/// <summary>
/// Tests for the OntologyGen extractor and renderer using an isolated test ontology.
/// Does NOT touch any real node/relationship classes.
/// The fixture source is an inline string — immune to working-directory issues in CI.
/// </summary>
[TestFixture]
public class OntologyGenTests
{
    // ── Inline fixture ───────────────────────────────────────────────────────
    // A self-contained mini ontology: Zoo → Animal → Habitat.
    // Uses local (file-scoped) attribute stubs with the phase-2 signatures.
    private const string FixtureSource = """
        namespace OntologyGenFixture;

        [System.AttributeUsage(System.AttributeTargets.Class)]
        sealed class OntologyNodeAttribute(string label, string idScheme, string purpose) : System.Attribute;
        [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
        sealed class OntologyEdgeAttribute<TTo>(string rel) : System.Attribute where TTo : class;
        [System.AttributeUsage(System.AttributeTargets.Class)]
        sealed class OntologyRelationshipAttribute(string label, string description) : System.Attribute;
        [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field | System.AttributeTargets.Parameter)]
        sealed class OntologyPropertyAttribute(string description) : System.Attribute;

        [OntologyRelationship(label: "OWNS", description: "Links a Zoo to an Animal it owns.")]
        public record OwnsRelationship(string From, string To);

        [OntologyRelationship(label: "LIVES_IN", description: "Links an Animal to the Habitat it lives in.")]
        public record LivesInRelationship(string From, string To);

        [OntologyNode(label: "Zoo", idScheme: "{workspaceId}:zoo:{name}", purpose: "Represents a zoo that contains animals.")]
        [OntologyEdge<AnimalNode>("OWNS")]
        public record ZooNode(
            string Id,
            [property: OntologyProperty("The name of the zoo.")] string Name,
            [property: OntologyProperty("The path of the zoo relative to its parent.")] string Path
        );

        [OntologyNode(label: "Animal", idScheme: "{workspaceId}:animal:{name}", purpose: "Represents an animal living in a zoo.")]
        [OntologyEdge<HabitatNode>("LIVES_IN")]
        public record AnimalNode(
            string Id,
            [property: OntologyProperty("The name of the animal.")] string Name,
            [property: OntologyProperty("The species of the animal.")] string Species,
            [property: OntologyProperty("The path of the animal relative to its parent.")] string Path
        );

        [OntologyNode(label: "Habitat", idScheme: "{workspaceId}:habitat:{name}", purpose: "Represents a physical habitat environment.")]
        public record HabitatNode(
            string Id,
            [property: OntologyProperty("The name of the habitat.")] string Name,
            [property: OntologyProperty("The climate type (tropical, arctic, etc.).")] string Climate,
            [property: OntologyProperty("The path of the habitat relative to its parent.")] string Path
        );
        """;

    private List<NodeInfo>? _nodes;
    private List<RelInfo>? _rels;

    [OneTimeSetUp]
    public async Task ExtractFixtures()
    {
        // Write fixture to a temp file — extractor works against file paths
        var tmpFile = Path.Combine(Path.GetTempPath(), $"OntologyGenFixture_{Guid.NewGuid():N}.cs");
        try
        {
            await File.WriteAllTextAsync(tmpFile, FixtureSource);
            var extractor = new OntologyExtractor();
            var files = new[] { tmpFile };
            _nodes = await extractor.ExtractNodesAsync(files);
            _rels = await extractor.ExtractRelationshipsAsync(files);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    // ── Node extraction ──────────────────────────────────────────────────────

    [Test]
    public void ExtractsExpectedNodeCount() =>
        _nodes.Should().HaveCount(3, "Zoo, Animal, Habitat");

    [Test]
    public void ExtractsZooNode()
    {
        var zoo = _nodes!.Single(n => n.Label == "Zoo");
        zoo.IdScheme.Should().Be("{workspaceId}:zoo:{name}");
        zoo.Purpose.Should().Be("Represents a zoo that contains animals.");
    }

    [Test]
    public void ExtractsAnimalNode()
    {
        var animal = _nodes!.Single(n => n.Label == "Animal");
        animal.IdScheme.Should().Be("{workspaceId}:animal:{name}");
    }

    [Test]
    public void HabitatNodeHasNoOutboundEdges()
    {
        var habitat = _nodes!.Single(n => n.Label == "Habitat");
        habitat.OutEdges.Should().BeEmpty("Habitat is a pure leaf node");
    }

    // ── Edge extraction ──────────────────────────────────────────────────────

    [Test]
    public void ZooHasOwnsEdgeToAnimal()
    {
        var zoo = _nodes!.Single(n => n.Label == "Zoo");
        var edge = zoo.OutEdges.SingleOrDefault(e => e.Rel == "OWNS");

        edge.Should().NotBeNull();
        edge!.ToTypeName.Should().Be("AnimalNode");
        edge.FromLabel.Should().Be("Zoo");
    }

    [Test]
    public void AnimalHasLivesInEdgeToHabitat()
    {
        var animal = _nodes!.Single(n => n.Label == "Animal");
        var edge = animal.OutEdges.SingleOrDefault(e => e.Rel == "LIVES_IN");

        edge.Should().NotBeNull();
        edge!.ToTypeName.Should().Be("HabitatNode");
    }

    // ── Property extraction ──────────────────────────────────────────────────

    [Test]
    public void AnimalNodeHasSpeciesProperty()
    {
        var animal = _nodes!.Single(n => n.Label == "Animal");
        var speciesProp = animal.Properties.SingleOrDefault(p => p.Name == "Species");

        speciesProp.Should().NotBeNull();
        speciesProp!.Description.Should().Be("The species of the animal.");
        speciesProp.Type.Should().Be("string");
    }

    [Test]
    public void PropertiesWithoutAttributeAreExcluded()
    {
        var zoo = _nodes!.Single(n => n.Label == "Zoo");
        (zoo.Properties.Any(p => p.Name == "Id")).Should().BeFalse();
    }

    // ── Relationship extraction ──────────────────────────────────────────────

    [Test]
    public void ExtractsExpectedRelationshipCount() =>
        _rels.Should().HaveCount(2, "OWNS, LIVES_IN");

    [Test]
    public void ExtractsOwnsRelationship()
    {
        var owns = _rels!.SingleOrDefault(r => r.Label == "OWNS");
        owns.Should().NotBeNull();
        owns!.Description.Should().Be("Links a Zoo to an Animal it owns.");
    }

    // ── Markdown rendering ───────────────────────────────────────────────────

    [Test]
    public void RenderedMarkdownContainsNodeLabels()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        md.Should().Contain("### `Zoo`");
        md.Should().Contain("### `Animal`");
        md.Should().Contain("### `Habitat`");
    }

    [Test]
    public void RenderedMarkdownContainsOutboundEdge()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        md.Should().Contain("OWNS");
    }

    [Test]
    public void RenderedMarkdownContainsIncomingEdgeForAnimal()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        md.Should().Contain("Incoming edges");
        md.Should().Contain("Zoo");
    }

    [Test]
    public void RenderedMarkdownContainsIdSchemeTable()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        md.Should().Contain("Uniform Resource Name (URN) & ID Schemes");
        md.Should().Contain("{workspaceId}:zoo:{name}");
    }

    [Test]
    public void RenderedMarkdownContainsRelationshipsSection()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        md.Should().Contain("Layer 5: SystemBindings (Integration Links)");
        md.Should().Contain("Links a Zoo to an Animal it owns.");
    }

    [Test]
    public void RenderedMarkdownContainsAutoGeneratedHeader()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        md.Should().Contain("AUTO-GENERATED");
    }

    [Test]
    public void JsonSchemaRenderer_ResolvesTargetLabelsInboundEdgesAndIncludesLayer5LogicalDomains()
    {
        var nodesWithLayer5 = new List<NodeInfo>(_nodes!)
        {
            new(
                ClassName: "DomainNode",
                Label: "Domain",
                IdScheme: "{workspaceId}:dom:{name}",
                Purpose: "Logical business domain entity.",
                Layer: "Layer 5: Logical Structure",
                OutEdges:
                [
                    new EdgeInfo("Domain", "CONTAINS", "ZooNode"),
                    new EdgeInfo("Domain", "BELONGS_TO_DOMAIN", "ZooNode")
                ],
                Properties: []),
            new(
                ClassName: "SyntaxStructureNode",
                Label: "SyntaxStructure",
                IdScheme: "{workspaceId}:ss",
                Purpose: "Internal root container.",
                Layer: "Layer 3: Syntax Structure",
                OutEdges: [],
                Properties: [],
                IsSystemNode: true)
        };
        var relsWithLayer5 = new List<RelInfo>(_rels!)
        {
            new("CONTAINS", "Contains child entity."),
            new("BELONGS_TO_DOMAIN", "Legacy hidden upward edge.")
        };

        var json = JsonSchemaRenderer.Render(nodesWithLayer5, relsWithLayer5);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("2.0");
        root.GetProperty("layers").GetArrayLength().Should().Be(5);

        var nodeKinds = root.GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("kind").GetString()!)
            .ToList();

        nodeKinds.Should().Contain(["Domain", "Zoo", "Animal", "Habitat"]);
        nodeKinds.Should().NotContain("SyntaxStructure");

        var domainNode = root.GetProperty("nodes").EnumerateArray()
            .Single(n => n.GetProperty("kind").GetString() == "Domain");
        domainNode.GetProperty("layerId").GetInt32().Should().Be(5);
        domainNode.GetProperty("layerKey").GetString().Should().Be("L5_logical");
        domainNode.GetProperty("outEdges")[0].GetProperty("rel").GetString().Should().Be("CONTAINS");

        var animalNode = root.GetProperty("nodes").EnumerateArray()
            .Single(n => n.GetProperty("kind").GetString() == "Animal");
        animalNode.GetProperty("outEdges")[0].GetProperty("toKind").GetString().Should().Be("Habitat");
        animalNode.GetProperty("inEdges")[0].GetProperty("fromKind").GetString().Should().Be("Zoo");
        animalNode.GetProperty("sampleCypher").GetString().Should().Contain("MATCH");

        json.Should().NotContain("BELONGS_TO_DOMAIN");
    }

    [Test]
    public async Task MultiLevelInheritance_ExtractsAbstractAncestorsSubtypesAndInheritedEdges()
    {
        const string hierarchySource = """
            namespace OntologyHierarchyFixture;

            [System.AttributeUsage(System.AttributeTargets.Class)]
            sealed class OntologyNodeAttribute(string label, string idScheme, string purpose, bool isAbstract = false) : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
            sealed class OntologyEdgeAttribute<TTo>(string rel) : System.Attribute where TTo : class;
            [System.AttributeUsage(System.AttributeTargets.Class)]
            sealed class OntologyRelationshipAttribute(string label, string description) : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Parameter)]
            sealed class OntologyPropertyAttribute(string description) : System.Attribute;

            [OntologyRelationship(label: "IMPLEMENTED_BY", description: "Links module to project.")]
            public record ImplementedByRel(string From, string To);
            [OntologyRelationship(label: "EXPOSES", description: "Links workload to endpoint.")]
            public record ExposesRel(string From, string To);
            [OntologyRelationship(label: "TESTS", description: "Links test suite to module.")]
            public record TestsRel(string From, string To);

            [OntologyNode(label: "Project", idScheme: "{ws}:proj:{name}", purpose: "Project.")]
            public record ProjectNode(string Id, [property: OntologyProperty("Project name.")] string Name);

            [OntologyNode(label: "Endpoint", idScheme: "{ws}:ep:{name}", purpose: "Endpoint.")]
            public record EndpointNode(string Id, [property: OntologyProperty("Route.")] string Name);

            [OntologyNode(label: "Module", idScheme: "{ws}:mod:{name}", purpose: "Abstract module.", isAbstract: true)]
            [OntologyEdge<ProjectNode>("IMPLEMENTED_BY")]
            public abstract record ModuleNode(string Id, [property: OntologyProperty("Module name.")] string Name)
            {
                [OntologyProperty("Framework name.")]
                public string? Framework { get; set; }
            }

            [OntologyNode(label: "Workload", idScheme: "{ws}:wl:{name}", purpose: "Abstract workload.", isAbstract: true)]
            [OntologyEdge<EndpointNode>("EXPOSES")]
            public abstract record WorkloadNode(string Id, string Name) : ModuleNode(Id, Name);

            [OntologyNode(label: "Service", idScheme: "{ws}:svc:{name}", purpose: "Concrete service.")]
            public record ServiceNode(string Id, string Name) : WorkloadNode(Id, Name);

            [OntologyNode(label: "TestSuite", idScheme: "{ws}:test:{name}", purpose: "Concrete test suite.")]
            [OntologyEdge<ModuleNode>("TESTS")]
            public record TestSuiteNode(string Id, string Name) : ModuleNode(Id, Name);
            """;

        var tmpFile = Path.Combine(Path.GetTempPath(), $"OntologyHierarchy_{Guid.NewGuid():N}.cs");
        try
        {
            await File.WriteAllTextAsync(tmpFile, hierarchySource);
            var extractor = new OntologyExtractor();
            var nodes = await extractor.ExtractNodesAsync([tmpFile]);
            var rels = await extractor.ExtractRelationshipsAsync([tmpFile]);

            var json = JsonSchemaRenderer.Render(nodes, rels);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var nodesArr = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();

            var moduleDto = nodesArr.Single(n => n.GetProperty("kind").GetString() == "Module");
            moduleDto.GetProperty("isAbstract").GetBoolean().Should().BeTrue();
            var modSubtypes = moduleDto.GetProperty("subtypes").EnumerateArray().Select(e => e.GetString()!).ToList();
            modSubtypes.Should().Contain(["Workload", "TestSuite"]);

            var workloadDto = nodesArr.Single(n => n.GetProperty("kind").GetString() == "Workload");
            workloadDto.GetProperty("isAbstract").GetBoolean().Should().BeTrue();
            workloadDto.GetProperty("extends").GetString().Should().Be("Module");

            var serviceDto = nodesArr.Single(n => n.GetProperty("kind").GetString() == "Service");
            serviceDto.TryGetProperty("isAbstract", out _).Should().BeFalse();
            serviceDto.GetProperty("extends").GetString().Should().Be("Workload");
            var ancestors = serviceDto.GetProperty("ancestors").EnumerateArray().Select(e => e.GetString()!).ToList();
            ancestors.Should().Equal("Workload", "Module");

            // Service has 0 duplicate own edges; shared edges live on Workload & Module and appear in inheritedOutEdges/inheritedInEdges
            serviceDto.GetProperty("outEdges").GetArrayLength().Should().Be(0);
            var inheritedOut = serviceDto.GetProperty("inheritedOutEdges").EnumerateArray().ToList();
            inheritedOut.Select(e => e.GetProperty("rel").GetString()!).Should().Contain(["EXPOSES", "IMPLEMENTED_BY"]);

            var inheritedIn = serviceDto.GetProperty("inheritedInEdges").EnumerateArray().ToList();
            inheritedIn.Select(e => e.GetProperty("rel").GetString()!).Should().Contain("TESTS");

            // Service inherits properties transitively from Module
            var svcProps = serviceDto.GetProperty("properties").EnumerateArray().ToList();
            svcProps.Any(p => p.GetProperty("name").GetString() == "framework" && p.GetProperty("inheritedFrom").GetString() == "Module").Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }
}
