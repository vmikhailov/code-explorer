namespace CodeExplorer.Core.Common.Relationships;

public interface IOntologyRelationship
{
    string From { get; }
    string To { get; }
    string Kind { get; }
    Dictionary<string, string>? Extensions { get; }
}
