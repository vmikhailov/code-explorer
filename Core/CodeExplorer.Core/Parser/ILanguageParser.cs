using System.Collections.Generic;
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
    /// Checks if the given directory contains a project for this language, based on files in it.
    /// </summary>
    bool IsProjectDirectory(string directoryPath, string[] filesInDirectory);

    /// <summary>
    /// The project type/language identifier (e.g., "csharp", "go", "python", "typescript").
    /// </summary>
    string ProjectType { get; }

    /// <summary>
    /// The directory names that should be excluded when this language's project type is active.
    /// </summary>
    IReadOnlyCollection<string> ExcludedFolders { get; }

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
