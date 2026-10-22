namespace CodeExplorer.Core.Analysis.ProjectMap;

/// <summary>
/// Assigns each module to a ring using graph evidence first and folder names only as a tie-breaker:
/// <list type="bullet">
/// <item>Ingress (0): owns Endpoint/EntryPoint evidence (unless mostly depended upon), or nothing inside the project depends on it while it depends on others.</item>
/// <item>Data (3): a meaningful share of its files access Table/Database/Query resources.</item>
/// <item>Foundation (4): depended on by most other modules while depending on almost nothing.</item>
/// <item>Application (1) / Core (2): split by instability I = Ce / (Ca + Ce).</item>
/// </list>
/// </summary>
public static class ProjectMapClassifier
{
    public const double FoundationFanInShare = 0.5;
    public const double DataFileShare = 0.15;
    private const double TieBandLow = 0.35;
    private const double TieBandHigh = 0.65;

    private static readonly HashSet<string> IngressNames = Names("bin", "cmd", "cli", "api", "apis", "controllers", "controller", "routes", "router", "endpoints", "mcp", "ui", "views", "pages", "server", "web", "grpc", "graphql", "commands", "handlers");
    private static readonly HashSet<string> ApplicationNames = Names("services", "service", "application", "app", "usecases", "use-cases", "sync", "jobs", "workers", "workflows", "orchestration", "features");
    private static readonly HashSet<string> CoreNames = Names("domain", "core", "model", "models", "engine", "entities", "graph", "logic");
    private static readonly HashSet<string> DataNames = Names("db", "data", "database", "persistence", "repositories", "repository", "repos", "storage", "dao", "migrations", "store");
    private static readonly HashSet<string> FoundationNames = Names("utils", "util", "common", "shared", "helpers", "helper", "errors", "types", "constants", "extensions", "logging", "config", "lib");

    public static void Classify(ProjectMapResult result)
    {
        ComputeMetrics(result);
        var moduleCount = result.Modules.Count;

        foreach (var m in result.Modules)
        {
            (m.Tier, m.TierReason) = ClassifyModule(m, moduleCount);
        }
    }

    private static void ComputeMetrics(ProjectMapResult result)
    {
        var moduleEdges = result.Edges.Where(e => !e.IsResourceLink).ToList();
        foreach (var m in result.Modules)
        {
            m.FanOut = moduleEdges.Where(e => e.SourceId == m.Id).Select(e => e.TargetId).Distinct().Count();
            m.FanIn = moduleEdges.Where(e => e.TargetId == m.Id).Select(e => e.SourceId).Distinct().Count();
            var total = m.FanIn + m.FanOut;
            m.Instability = total == 0 ? 0.5 : (double)m.FanOut / total;
        }
    }

    private static (ProjectMapTier, string) ClassifyModule(ProjectMapModule m, int moduleCount)
    {
        var isDependedOn = m.FanIn >= 2 && m.FanIn > m.FanOut;
        if (m.EntryPointCount > 0 && !isDependedOn)
            return (ProjectMapTier.Ingress, $"Exposes {m.EntryPointCount} endpoint/entry point(s)");

        var others = Math.Max(1, moduleCount - 1);
        var foundationThreshold = Math.Max(3, (int)Math.Ceiling(others * FoundationFanInShare));
        if (moduleCount >= 4 && m.FanIn >= foundationThreshold && m.FanOut <= 1 && !m.TouchesData)
            return (ProjectMapTier.Foundation, $"Used by {m.FanIn}/{others} modules, depends on {m.FanOut}");

        if (m.FanIn == 0 && m.FanOut >= 2)
            return (ProjectMapTier.Ingress, $"No internal dependents; depends on {m.FanOut} module(s)");

        var dataShare = m.Files.Count == 0 ? 0 : (double)m.DataFileCount / m.Files.Count;
        if (m.TouchesData && dataShare >= DataFileShare)
            return (ProjectMapTier.Data, $"{m.DataFileCount}/{m.Files.Count} files access tables / databases");

        if (m.FanIn == 0 && m.FanOut > 0)
            return (ProjectMapTier.Ingress, $"No internal dependents; depends on {m.FanOut} module(s)");

        var isolated = m.FanIn == 0 && m.FanOut == 0;
        if (isolated || (m.Instability >= TieBandLow && m.Instability <= TieBandHigh))
        {
            var byName = ClassifyByName(m.Name);
            if (byName.HasValue)
                return (byName.Value, isolated ? "Isolated module; tier inferred from folder name" : $"Instability {m.Instability:0.00} (tie); tier inferred from folder name");
        }

        if (isolated)
            return (ProjectMapTier.Core, "Isolated module (no internal dependencies)");

        return m.Instability > 0.5
            ? (ProjectMapTier.Application, $"Instability {m.Instability:0.00}: depends on more modules than depend on it")
            : (ProjectMapTier.Core, $"Instability {m.Instability:0.00}: stable, depended on by other modules");
    }

    internal static ProjectMapTier? ClassifyByName(string moduleName)
    {
        var leaf = moduleName.Split('/').Last().Trim().ToLowerInvariant();
        if (IngressNames.Contains(leaf)) return ProjectMapTier.Ingress;
        if (DataNames.Contains(leaf)) return ProjectMapTier.Data;
        if (FoundationNames.Contains(leaf)) return ProjectMapTier.Foundation;
        if (CoreNames.Contains(leaf)) return ProjectMapTier.Core;
        if (ApplicationNames.Contains(leaf)) return ProjectMapTier.Application;
        return null;
    }

    private static HashSet<string> Names(params string[] names) => new(names, StringComparer.OrdinalIgnoreCase);
}
