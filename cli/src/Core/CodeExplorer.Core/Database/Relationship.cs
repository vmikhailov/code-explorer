using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Database;

public record Relationship(string From, string To, string Kind, Dictionary<string, object> Properties)
{
    public Relationship(string from, string to, string kind)
        : this(from, to, kind, new Dictionary<string, object>())
    {
    }

    public static Relationship FromRelationship(IOntologyRelationship rel)
    {
        var properties = new Dictionary<string, object>();
        if (rel.Extensions != null)
        {
            foreach (var kvp in rel.Extensions)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        return new Relationship(rel.From, rel.To, rel.Kind, properties);
    }
}
