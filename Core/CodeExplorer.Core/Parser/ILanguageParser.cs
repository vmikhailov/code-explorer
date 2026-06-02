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

    /// <summary>
    /// Checks if the project in the given directory produces a package, and returns details if so.
    /// </summary>
    Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory);

    /// <summary>
    /// Parses the project dependencies (local project directory paths and external packages) in the given directory.
    /// </summary>
    Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory);

    /// <summary>
    /// Indicates whether this parser uses Tree-Sitter for AST-level parsing.
    /// </summary>
    bool UsesTreeSitter { get; }

    /// <summary>
    /// Executes custom non-Tree-Sitter parsing logic for this file.
    /// </summary>
    Task ParseCustomAsync(string filePath, string parentNodeId, ParsingContext ctx);
}
