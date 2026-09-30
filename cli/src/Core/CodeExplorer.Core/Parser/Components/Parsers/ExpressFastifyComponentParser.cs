namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for Express, Fastify, Koa, and Hono web frameworks.
/// </summary>
public class ExpressFastifyComponentParser : IComponentLibraryParser
{
    public string Id => "express-fastify";
    public string Name => "Express/Fastify";

    private static readonly HashSet<string> WebFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "express", "fastify", "koa", "hono", "@hapi/hapi"
    };

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => WebFrameworks.Contains(d));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        var matched = context.Dependencies.FirstOrDefault(d => WebFrameworks.Contains(d)) ?? "express";

        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = matched,
            Role = LibraryRole.WebService,
            Capabilities = ComponentCapabilities.HttpEndpoints,
            Metadata = new Dictionary<string, string>
            {
                ["web_framework"] = matched
            }
        };
    }
}
