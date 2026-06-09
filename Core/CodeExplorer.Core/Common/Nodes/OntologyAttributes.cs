namespace CodeExplorer.Core.Common.Nodes;

[AttributeUsage(AttributeTargets.Class)]
public class OntologyNodeAttribute(string purpose, params string[] relationships) : Attribute
{
    public string Purpose { get; } = purpose;
    public string[] Relationships { get; } = relationships;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class OntologyPropertyAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}
