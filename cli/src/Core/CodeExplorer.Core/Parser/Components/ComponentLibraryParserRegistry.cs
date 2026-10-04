using CodeExplorer.Core.Parser.Components.Parsers;

namespace CodeExplorer.Core.Parser.Components;

/// <summary>
/// Extensible, thread-safe registry of component and library parsers.
/// Executes framework-specific and library-specific parsers to determine capabilities,
/// roles, and concrete attributes without path-matching heuristics.
/// </summary>
public static class ComponentLibraryParserRegistry
{
    private static readonly List<IComponentLibraryParser> _parsers = [];

    static ComponentLibraryParserRegistry()
    {
        // TypeScript / JavaScript Ecosystem
        Register(new AngularComponentParser());
        Register(new ReactNextComponentParser());
        Register(new NestJsComponentParser());
        Register(new ExpressFastifyComponentParser());
        Register(new NodeWorkerSchedulerComponentParser());
        Register(new JavaScriptTestRunnerComponentParser());

        // .NET Ecosystem
        Register(new AspNetCoreComponentParser());
        Register(new YarpGatewayComponentParser());
        Register(new DotNetSchedulerComponentParser());
        Register(new DotNetWorkerComponentParser());
        Register(new DotNetTestComponentParser());
        Register(new DotNetLibraryComponentParser());

        // Python Ecosystem
        Register(new PythonTestComponentParser());
        Register(new FastApiComponentParser());

        // Go Ecosystem
        Register(new GoTestComponentParser());

        // Java Ecosystem
        Register(new JavaTestComponentParser());
    }

    public static List<IComponentLibraryParser> GetParsers(ProjectContext context)
    {
        var matched = new List<IComponentLibraryParser>();
        var parsers = Parsers;
        for (int i = 0; i < parsers.Count; i++)
        {
            if (parsers[i].CanHandle(context))
            {
                matched.Add(parsers[i]);
            }
        }
        return matched;
    }

    public static void Register(IComponentLibraryParser parser)
    {
        lock (_parsers)
        {
            if (!_parsers.Any(p => p.Id.Equals(parser.Id, StringComparison.OrdinalIgnoreCase)))
            {
                _parsers.Add(parser);
            }
        }
    }

    public static IReadOnlyList<IComponentLibraryParser> Parsers
    {
        get
        {
            lock (_parsers)
            {
                return [.. _parsers];
            }
        }
    }

    /// <summary>
    /// Analyzes the project at manifest/boundary level (Layer 2) across all registered component parsers.
    /// </summary>
    public static ProjectComponentProfile AnalyzeProject(ProjectContext context)
    {
        var matchedResults = new List<ComponentAnalysisResult>();
        var parsers = Parsers;

        for (int i = 0; i < parsers.Count; i++)
        {
            var p = parsers[i];
            if (p.CanHandle(context))
            {
                var result = p.AnalyzeManifest(context);
                if (result != null)
                {
                    matchedResults.Add(result);
                }
            }
        }

        return ProjectComponentProfile.Aggregate(matchedResults, context);
    }
}
