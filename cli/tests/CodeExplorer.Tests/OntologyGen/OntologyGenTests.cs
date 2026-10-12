using NUnit.Framework;
using OntologyGen;
using AwesomeAssertions;

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
}
