using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Common.Nodes;

[AttributeUsage(AttributeTargets.Class)]
public class OntologyNodeAttribute(
    string label,
    string idScheme,
    string purpose,
    string layer = OntologyConstants.Layers.Semantic) : Attribute
{
    public string Label { get; } = label;
    public string IdScheme { get; } = idScheme;
    public string Purpose { get; } = purpose;
    public string Layer { get; } = layer;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class OntologyPropertyAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

/// <summary>
/// Declares one outbound edge from the annotated node class to <typeparamref name="TTo"/>.
/// Declare only outbound edges; the generator derives inbound edges via reverse lookup.
/// </summary>
/// <typeparam name="TTo">Target node type.</typeparam>
/// <param name="rel">Relationship label constant from <see cref="OntologyConstants.Relationships"/>.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class OntologyEdgeAttribute<TTo>(string rel) : Attribute
    where TTo : IOntologyNode
{
    public string Rel { get; } = rel;
    public Type To { get; } = typeof(TTo);
}

/// <summary>
/// Annotates a relationship record with its label and a human-readable description.
/// Applied to classes implementing <see cref="IOntologyRelationship"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class OntologyRelationshipAttribute(string label, string description) : Attribute
{
    public string Label { get; } = label;
    public string Description { get; } = description;
}
