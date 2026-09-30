namespace CodeExplorer.Core.Parser.Components;

/// <summary>
/// Pluggable contract for a component/library parser that understands a specific technology or framework
/// (e.g., Angular, NestJS, Next.js, Express, ASP.NET Core, YARP, MassTransit, Jest, xUnit).
/// Emits architectural roles, capability flags, and concrete attributes without guessing paths.
/// </summary>
public interface IComponentLibraryParser
{
    /// <summary>
    /// Unique identifier of the component parser (e.g., "angular", "nestjs-schedule", "aspnetcore").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Friendly human-readable name of the component (e.g., "Angular", "NestJS Schedule", "ASP.NET Core").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates if this parser can handle the project based on dependencies, manifests, or configuration.
    /// </summary>
    bool CanHandle(ProjectContext context);

    /// <summary>
    /// Analyzes the project manifest, package dependencies, and files at indexing Layer 2.
    /// </summary>
    ComponentAnalysisResult AnalyzeManifest(ProjectContext context);

    /// <summary>
    /// Enriches the component analysis result with concrete AST artifacts (routes, schedules, tests)
    /// during syntactic and semantic parsing (Layers 3/4).
    /// </summary>
    void EnrichWithSyntax(ComponentAnalysisResult? result, SyntaxTree syntaxTree, ParsingContext ctx) { }
}
