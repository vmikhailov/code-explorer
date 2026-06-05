using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public interface ILibraryParser
{
    /// <summary>
    /// The friendly name of the parser (e.g., "MongooseLibraryParser").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The category of behavior this library addresses (e.g., "database", "api").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// The canonical library/package names that trigger this parser (e.g., ["mongoose", "mongodb"]).
    /// </summary>
    IEnumerable<string> SupportedLibraries { get; }

    /// <summary>
    /// Maps a Tree-sitter AST node to a CodeExplorer ontological kind (Class, Interface, Function, Variable, Query, EntryPoint, ExternalService, or null).
    /// </summary>
    string? MapNodeType(Node node, ParsingContext ctx);

    /// <summary>
    /// Extracts the identifier/name of the matched behavior node.
    /// </summary>
    string? ExtractIdentifier(Node node, ParsingContext ctx);

    /// <summary>
    /// Collects references inside a scope for this library's nodes.
    /// </summary>
    void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx);
}
