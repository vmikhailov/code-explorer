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
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry
    );

    /// <summary>
    /// Resolves the import type (Internal/External) for the given import path in a file.
    /// </summary>
    ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath);

    /// <summary>
    /// The semantic AST extensions registered for this language.
    /// </summary>
    IReadOnlyList<ISemanticExtension> SemanticExtensions { get; }

    /// <summary>
    /// The language syntax classification profile for AST traversal.
    /// </summary>
    LanguageSyntaxProfile SyntaxProfile => LanguageSyntaxProfile.Empty;

    /// <summary>
    /// Traverses the AST root node to discover symbol, constant, and enum declarations
    /// for early-phase constant propagation into ConstantRegistry.
    /// </summary>
    void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
    }
}
