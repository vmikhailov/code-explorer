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
    /// The canonical library/package names that trigger this parser (e.g., ["mongoose", "mongodb"]).
    /// </summary>
    IEnumerable<string> SupportedLibraries { get; }

    /// <summary>
    /// Gets a value indicating whether this library parser is implemented.
    /// Defaults to false.
    /// </summary>
    bool IsImplemented => false;

    /// <summary>
    /// Gets a value indicating whether this library is a built-in/standard library of the language.
    /// Built-in libraries are always active regardless of project dependencies.
    /// </summary>
    bool IsBuiltIn => false;

    /// <summary>
    /// The type of the library (e.g., "db:relational", "api", "cloud", "framework", "tool").
    /// </summary>
    string LibraryType { get; }

    /// <summary>
    /// The logical name of the library (e.g., "PostgreSQL", "Dapper", "Stripe", "NestJS").
    /// </summary>
    string LibraryName { get; }

    /// <summary>
    /// A unique identifier for the library (e.g., "postgres", "dapper", "stripe", "nestjs").
    /// </summary>
    string LibraryId { get; }

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
