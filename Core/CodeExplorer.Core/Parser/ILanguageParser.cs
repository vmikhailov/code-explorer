using TreeSitter;

namespace CodeExplorer.Parser;

public interface ILanguageParser
{
    /// <summary>
    /// The exact Tree-sitter language binding name (e.g., "c-sharp", "go", "python", "typescript").
    /// </summary>
    string LanguageName { get; }

    /// <summary>
    /// Determines if this parser handles the given file extension.
    /// </summary>
    bool CanParse(string fileExtension);

    /// <summary>
    /// Maps a Tree-sitter AST node type to a CodeExplorer ontological kind (Class, Function, Variable, or null).
    /// </summary>
    string? MapNodeType(string nodeType);

    /// <summary>
    /// Extracts the identifier/name of the symbol node, resolving any language-specific syntax or field quirks.
    /// </summary>
    string? ExtractIdentifier(Node node);

    /// <summary>
    /// Analyzes an AST node inside a containing scope and extracts any referenced symbols (calls, type uses, base classes).
    /// </summary>
    void CollectReferences(Node node, string scopeSymbolId, List<Reference> references);
}
