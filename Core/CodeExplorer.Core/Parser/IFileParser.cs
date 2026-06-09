using TreeSitter;

namespace CodeExplorer.Core.Parser;

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
    /// Parses the file and returns a rich SyntaxTree with the AST and child symbols.
    /// </summary>
    Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath);

    /// <summary>
    /// Creates a language-specific AST visitor to traverse and extract symbols, references, and semantic data.
    /// </summary>
    BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry
    );

    /// <summary>
    /// Resolves the import type (Internal/External) for the given import path in a file.
    /// </summary>
    ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath);

    /// <summary>
    /// The library-specific parsers registered for this language.
    /// </summary>
    IReadOnlyList<ILibraryParser> LibraryParsers { get; }
}
