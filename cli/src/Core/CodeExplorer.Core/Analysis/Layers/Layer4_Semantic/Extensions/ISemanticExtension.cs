using System.Collections.Immutable;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Dedicated contract for semantic AST behavior analysis and ontology node mapping.
/// Replaces the legacy ILibraryParser by isolating Tree-sitter AST traversal from package manifests.
/// </summary>
public interface ISemanticExtension
{
    /// <summary>
    /// Unique identifier of the semantic extension (e.g., "aspnetcore", "spring-mvc", "express").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Friendly human-readable name of the extension (e.g., "ASP.NET Core", "Spring MVC").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of the extension (e.g., "framework", "db:relational", "messaging", "cloud").
    /// </summary>
    string Type => "framework";

    /// <summary>
    /// The architectural role of this extension when present in code.
    /// </summary>
    LibraryRole LibraryRole => LibraryRole.General;
    LibraryRole Role => LibraryRole;

    /// <summary>
    /// Optional custom matching predicate for project context (e.g. SDK or manifest properties check).
    /// </summary>
    Func<ProjectContext, bool>? CustomMatch => null;

    /// <summary>
    /// Gets the declarative package descriptors provided by this semantic extension.
    /// By default, yields a single descriptor representing this extension.
    /// Extensions can override this to yield zero, one, or multiple package descriptors.
    /// </summary>
    IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(Id, Name, LibraryRole, Type, SupportedPatterns, CustomMatch);
    }

    /// <summary>
    /// Converts this semantic extension into a declarative package descriptor for Layer 2 ontology classification.
    /// </summary>
    [Obsolete("Use GetPackageDescriptors() instead")]
    PackageDescriptor ToPackageDescriptor(string ecosystem = "library") =>
        new(Id, Name, LibraryRole, Type, SupportedPatterns, CustomMatch);

    /// <summary>
    /// Indicates whether this extension is built into the language standard library.
    /// </summary>
    bool IsBuiltIn => false;

    /// <summary>
    /// Indicates whether AST behavior mapping is implemented.
    /// </summary>
    bool IsImplemented => false;

    /// <summary>
    /// The import or namespace patterns that activate this semantic extension during AST traversal (e.g., ["Microsoft.AspNetCore.*", "express"]).
    /// </summary>
    IReadOnlyList<string> SupportedPatterns => [];

    /// <summary>
    /// Performs a part-based matching of library names, splitting by '.' and '/' and comparing segments.
    /// </summary>
    public static bool IsLibraryMatch(string a, string b) => PackageDescriptor.IsLibraryMatch(a, b);

    /// <summary>
    /// Declarative selectors mapping ontological kinds to their corresponding matcher selectors.
    /// </summary>
    IReadOnlyDictionary<string, NodeSelector> Selectors => ImmutableDictionary<string, NodeSelector>.Empty;

    /// <summary>
    /// Maps a Tree-sitter AST node to a CodeExplorer ontological kind (Class, Interface, Function, Variable, Query, EntryPoint, ExternalService, or null).
    /// </summary>
    string? MapNodeType(Node node, ParsingContext ctx) => null;

    /// <summary>
    /// Extracts the identifier/name of the matched behavior node.
    /// </summary>
    string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    /// <summary>
    /// Extracts the identifier/name of the matched behavior node with optional project context.
    /// </summary>
    string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx);

    /// <summary>
    /// Extracts the identifier/name of the matched behavior node with project and relative file path context.
    /// </summary>
    string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath) => ExtractIdentifier(node, ctx, projectName);

    /// <summary>
    /// Collects references inside a scope for this extension's nodes.
    /// </summary>
    void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    /// <summary>
    /// Enriches a synthesized syntactic symbol with domain-specific metadata (e.g., security roles, authorization, protocol).
    /// </summary>
    void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx) { }

    /// <summary>
    /// Optional early-phase discovery hook called during Phase 0 candidate file discovery
    /// before AST synthesis. Allows framework extensions to discover router mounts, global prefixes, or controller mappings.
    /// </summary>
    void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx) { }

    /// <summary>
    /// Optional AST-level discovery hook called during Pass 1 AST traversal.
    /// </summary>
    void DiscoverDeclarations(Node rootNode, string relativePath, string? projectName, ParsingContext? ctx) { }
}
