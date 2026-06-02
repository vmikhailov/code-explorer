using TreeSitter;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public interface IFileParser
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
    /// Indicates whether this parser uses Tree-Sitter for AST-level parsing.
    /// </summary>
    bool UsesTreeSitter { get; }

    /// <summary>
    /// Parses the file and returns a rich FileNode with all child symbols nested.
    /// </summary>
    Task<FileNode> ParseAsync(string filePath, string parentNodeId, ParsingContext ctx);

    /// <summary>
    /// Maps a Tree-sitter AST node to a CodeExplorer ontological kind (Class, Interface, Function, Variable, or null).
    /// </summary>
    string? MapNodeType(Node node);

    /// <summary>
    /// Extracts the identifier/name of the symbol node, resolving any language-specific syntax or field quirks.
    /// </summary>
    string? ExtractIdentifier(Node node);

    /// <summary>
    /// Analyzes an AST node inside a containing scope and extracts any referenced symbols (calls, type uses, base classes).
    /// </summary>
    void CollectReferences(Node node, string scopeSymbolId, List<Reference> references);
}
