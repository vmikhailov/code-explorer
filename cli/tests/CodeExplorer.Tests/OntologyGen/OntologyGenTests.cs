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
        Assert.That(_nodes, Has.Count.EqualTo(3), "Zoo, Animal, Habitat");

    [Test]
    public void ExtractsZooNode()
    {
        var zoo = _nodes!.Single(n => n.Label == "Zoo");
        Assert.That(zoo.IdScheme, Is.EqualTo("{workspaceId}:zoo:{name}"));
        Assert.That(zoo.Purpose, Is.EqualTo("Represents a zoo that contains animals."));
    }

    [Test]
    public void ExtractsAnimalNode()
    {
        var animal = _nodes!.Single(n => n.Label == "Animal");
        Assert.That(animal.IdScheme, Is.EqualTo("{workspaceId}:animal:{name}"));
    }

    [Test]
    public void HabitatNodeHasNoOutboundEdges()
    {
        var habitat = _nodes!.Single(n => n.Label == "Habitat");
        Assert.That(habitat.OutEdges, Is.Empty, "Habitat is a pure leaf node");
    }

    // ── Edge extraction ──────────────────────────────────────────────────────

    [Test]
    public void ZooHasOwnsEdgeToAnimal()
    {
        var zoo = _nodes!.Single(n => n.Label == "Zoo");
        var edge = zoo.OutEdges.SingleOrDefault(e => e.Rel == "OWNS");

        Assert.That(edge, Is.Not.Null);
        Assert.That(edge!.ToTypeName, Is.EqualTo("AnimalNode"));
        Assert.That(edge.FromLabel, Is.EqualTo("Zoo"));
    }

    [Test]
    public void AnimalHasLivesInEdgeToHabitat()
    {
        var animal = _nodes!.Single(n => n.Label == "Animal");
        var edge = animal.OutEdges.SingleOrDefault(e => e.Rel == "LIVES_IN");

        Assert.That(edge, Is.Not.Null);
        Assert.That(edge!.ToTypeName, Is.EqualTo("HabitatNode"));
    }

    // ── Property extraction ──────────────────────────────────────────────────

    [Test]
    public void AnimalNodeHasSpeciesProperty()
    {
        var animal = _nodes!.Single(n => n.Label == "Animal");
        var speciesProp = animal.Properties.SingleOrDefault(p => p.Name == "Species");

        Assert.That(speciesProp, Is.Not.Null);
        Assert.That(speciesProp!.Description, Is.EqualTo("The species of the animal."));
        Assert.That(speciesProp.Type, Is.EqualTo("string"));
    }

    [Test]
    public void PropertiesWithoutAttributeAreExcluded()
    {
        var zoo = _nodes!.Single(n => n.Label == "Zoo");
        Assert.That(zoo.Properties.Any(p => p.Name == "Id"), Is.False);
    }

    // ── Relationship extraction ──────────────────────────────────────────────

    [Test]
    public void ExtractsExpectedRelationshipCount() =>
        Assert.That(_rels, Has.Count.EqualTo(2), "OWNS, LIVES_IN");

    [Test]
    public void ExtractsOwnsRelationship()
    {
        var owns = _rels!.SingleOrDefault(r => r.Label == "OWNS");
        Assert.That(owns, Is.Not.Null);
        Assert.That(owns!.Description, Is.EqualTo("Links a Zoo to an Animal it owns."));
    }

    // ── Markdown rendering ───────────────────────────────────────────────────

    [Test]
    public void RenderedMarkdownContainsNodeLabels()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        Assert.That(md, Does.Contain("### `Zoo`"));
        Assert.That(md, Does.Contain("### `Animal`"));
        Assert.That(md, Does.Contain("### `Habitat`"));
    }

    [Test]
    public void RenderedMarkdownContainsOutboundEdge()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        Assert.That(md, Does.Contain("OWNS"));
    }

    [Test]
    public void RenderedMarkdownContainsIncomingEdgeForAnimal()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        Assert.That(md, Does.Contain("Incoming edges"));
        Assert.That(md, Does.Contain("Zoo"));
    }

    [Test]
    public void RenderedMarkdownContainsIdSchemeTable()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        Assert.That(md, Does.Contain("Uniform Resource Name (URN) & ID Schemes"));
        Assert.That(md, Does.Contain("{workspaceId}:zoo:{name}"));
    }

    [Test]
    public void RenderedMarkdownContainsRelationshipsSection()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        Assert.That(md, Does.Contain("Layer 5: SystemBindings (Integration Links)"));
        Assert.That(md, Does.Contain("Links a Zoo to an Animal it owns."));
    }

    [Test]
    public void RenderedMarkdownContainsAutoGeneratedHeader()
    {
        var md = MarkdownRenderer.Render(_nodes!, _rels!);
        Assert.That(md, Does.Contain("AUTO-GENERATED"));
    }
}
