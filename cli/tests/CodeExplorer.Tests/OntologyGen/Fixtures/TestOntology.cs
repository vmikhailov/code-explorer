// Test fixture: a self-contained mini ontology used by OntologyGenTests.
// Intentionally mirrors the real attribute/interface structure without touching
// any real node or relationship classes.
//
// IMPORTANT: This file defines its OWN attribute stubs so the fixture compiles
// independently of the phase-2 OntologyNodeAttribute signature changes (which
// haven't been applied to the real nodes yet).

using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using System.Text.Json.Serialization;

namespace CodeExplorer.Tests.OntologyGen.Fixtures;

// ── Test relationship records ────────────────────────────────────────────────

[OntologyRelationship("OWNS", "Links a Zoo to an Animal it owns.")]
public record OwnsRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore] public string Kind => "OWNS";
}

[OntologyRelationship("LIVES_IN", "Links an Animal to the Habitat it lives in.")]
public record LivesInRelationship(
    [property: JsonIgnore] string From,
    [property: JsonIgnore] string To,
    [property: JsonIgnore] Dictionary<string, string>? Extensions = null
) : IOntologyRelationship
{
    [JsonIgnore] public string Kind => "LIVES_IN";
}

// ── Test node records ────────────────────────────────────────────────────────

[OntologyNode(
    label: "Zoo",
    idScheme: "{workspaceId}:zoo:{name}",
    purpose: "Represents a zoo that contains animals.")]
[OntologyEdge<AnimalNode>("OWNS")]
public record ZooNode(
    string Id,
    [property: OntologyProperty("The name of the zoo.")] string Name,
    [property: OntologyProperty("The path of the zoo relative to its parent.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore] public override string Kind => "Zoo";
}

[OntologyNode(
    label: "Animal",
    idScheme: "{workspaceId}:animal:{name}",
    purpose: "Represents an animal living in a zoo.")]
[OntologyEdge<HabitatNode>("LIVES_IN")]
public record AnimalNode(
    string Id,
    [property: OntologyProperty("The name of the animal.")] string Name,
    [property: OntologyProperty("The species of the animal.")] string Species,
    [property: OntologyProperty("The path of the animal relative to its parent.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore] public override string Kind => "Animal";
}

// Pure leaf — receives edges, declares none
[OntologyNode(
    label: "Habitat",
    idScheme: "{workspaceId}:habitat:{name}",
    purpose: "Represents a physical habitat environment.")]
public record HabitatNode(
    string Id,
    [property: OntologyProperty("The name of the habitat.")] string Name,
    [property: OntologyProperty("The climate type (tropical, arctic, etc.).")] string Climate,
    [property: OntologyProperty("The path of the habitat relative to its parent.")] string Path,
    Dictionary<string, string>? Extensions = null
) : CompositeNode(Id, Extensions)
{
    [JsonIgnore] public override string Kind => "Habitat";
}
