using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Pluggable descriptor for framework-specific and library-specific configuration keys.
/// Decouples Core ConfigurationParser from individual language and library conventions (e.g. Spring, ASP.NET, NestJS).
/// </summary>
public interface ILibraryConfigurationDescriptor
{
    /// <summary>
    /// Evaluation order (lower values execute earlier). Framework/dialect-specific descriptors run before generic fallbacks.
    /// </summary>
    int Order => 100;

    /// <summary>
    /// Processes a configuration key-value pair if recognized by this framework/library.
    /// Returns true if handled, false otherwise.
    /// </summary>
    bool TryHandle(
        string key,
        string value,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx);
}
