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
    /// The library/package patterns that trigger this parser (e.g., ["mongoose", "@nestjs/*"]).
    /// </summary>
    IReadOnlyList<string> SupportedPatterns { get; }

    /// <summary>
    /// Performs a part-based matching of library names, splitting by '.' and '/' and comparing segments.
    /// </summary>
    public static bool IsLibraryMatch(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;

        var aParts = a.Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries);
        var bParts = b.Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries);

        var minLen = Math.Min(aParts.Length, bParts.Length);
        if (minLen == 0) return false;

        for (var i = 0; i < minLen; i++)
        {
            if (!aParts[i].Equals(bParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

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
    string Type { get; }

    /// <summary>
    /// A unique identifier for the library (e.g., "postgres", "dapper", "stripe", "nestjs").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Declarative selectors mapping ontological kinds to their corresponding matcher selectors.
    /// </summary>
    IReadOnlyDictionary<string, NodeSelector> Selectors => System.Collections.Immutable.ImmutableDictionary<string, NodeSelector>.Empty;

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
