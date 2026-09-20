using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public record ProjectLayerInfo(
    [property: JsonPropertyName("layerId")] string LayerId,
    [property: JsonPropertyName("layerName")] string LayerName,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("description")] string Description
);

public static class StandardLayers
{
    public static readonly ProjectLayerInfo Presentation = new(
        "layer_presentation",
        "Ingress & Presentation",
        0,
        "#fbbf24", // Amber
        "⚡",
        "Entry points, CLI commands, HTTP APIs, and Host executables"
    );

    public static readonly ProjectLayerInfo ApplicationCore = new(
        "layer_core",
        "Application & Domain Core",
        1,
        "#c084fc", // Purple
        "🏛️",
        "Core orchestration, domain models, and business logic"
    );

    public static readonly ProjectLayerInfo DomainServices = new(
        "layer_engines",
        "Domain Services & Specialized Engines",
        2,
        "#38bdf8", // Cyan
        "⚙️",
        "Parsers, query engines, algorithms, and domain handlers"
    );

    public static readonly ProjectLayerInfo Foundation = new(
        "layer_foundation",
        "Foundation & Storage",
        3,
        "#34d399", // Emerald
        "🗄️",
        "Shared utilities, database entities, and common abstractions"
    );

    public static readonly ProjectLayerInfo Tests = new(
        "layer_tests",
        "Tests & Verification",
        4,
        "#94a3b8", // Slate
        "🧪",
        "Unit tests, integration suites, benchmarks, and generator tools"
    );

    public static readonly IReadOnlyList<ProjectLayerInfo> All = new[]
    {
        Presentation,
        ApplicationCore,
        DomainServices,
        Foundation,
        Tests
    };
}

public class ProjectClassifierItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? FilePath { get; init; }
    public string? Framework { get; init; }
}

public class DependencyItem
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
}

public static class ProjectLayerClassifier
{
    public static Dictionary<string, ProjectLayerInfo> Classify(
        IEnumerable<ProjectClassifierItem> projects,
        IEnumerable<DependencyItem> dependencies)
    {
        var projList = projects.ToList();
        var depList = dependencies.ToList();
        var result = new Dictionary<string, ProjectLayerInfo>(StringComparer.OrdinalIgnoreCase);

        // Map incoming and outgoing dependencies
        var incomingCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outgoingCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in projList)
        {
            incomingCount[p.Id] = 0;
            outgoingCount[p.Id] = 0;
        }

        foreach (var d in depList)
        {
            if (incomingCount.ContainsKey(d.TargetId)) incomingCount[d.TargetId]++;
            if (outgoingCount.ContainsKey(d.SourceId)) outgoingCount[d.SourceId]++;
        }

        foreach (var p in projList)
        {
            var name = p.Name;
            var path = p.FilePath ?? "";

            // 1. Tests & Tools
            if (IsTestOrTool(name, path))
            {
                result[p.Id] = StandardLayers.Tests;
                continue;
            }

            // 2. Presentation / Entry Points
            if (IsPresentation(name, path, incomingCount.GetValueOrDefault(p.Id, 0)))
            {
                result[p.Id] = StandardLayers.Presentation;
                continue;
            }

            // 3. Application Core
            if (IsApplicationCore(name, path))
            {
                result[p.Id] = StandardLayers.ApplicationCore;
                continue;
            }

            // 4. Domain Services / Engines (Parsers, Cypher, Handlers)
            if (IsDomainService(name, path))
            {
                result[p.Id] = StandardLayers.DomainServices;
                continue;
            }

            // 5. Foundation / Leaf (Common, Shared, or low out-degree)
            if (IsFoundation(name, path, outgoingCount.GetValueOrDefault(p.Id, 0)))
            {
                result[p.Id] = StandardLayers.Foundation;
                continue;
            }

            // Fallback based on topology
            if (outgoingCount.GetValueOrDefault(p.Id, 0) == 0)
            {
                result[p.Id] = StandardLayers.Foundation;
            }
            else
            {
                result[p.Id] = StandardLayers.DomainServices;
            }
        }

        return result;
    }

    private static bool IsTestOrTool(string name, string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        return lowerName.EndsWith(".tests") ||
               lowerName.EndsWith(".test") ||
               lowerName.Contains("test.") ||
               lowerName.Contains("tests.") ||
               lowerName.Contains("ontologygen") ||
               lowerName.Contains("benchmark") ||
               normalizedPath.Contains("/tests/") ||
               normalizedPath.Contains("/test/");
    }

    private static bool IsPresentation(string name, string path, int inDegree)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        if (normalizedPath.Contains("/ui/") ||
            normalizedPath.Contains("/cli/") ||
            normalizedPath.Contains("/web/") ||
            normalizedPath.Contains("/api/") ||
            normalizedPath.Contains("/host/"))
        {
            return true;
        }

        if (lowerName.EndsWith(".cli") ||
            lowerName.EndsWith(".ui") ||
            lowerName.EndsWith(".web") ||
            lowerName.EndsWith(".api") ||
            lowerName.EndsWith(".host") ||
            lowerName.Equals("codeexplorer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsApplicationCore(string name, string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        return lowerName.EndsWith(".core") ||
               lowerName.EndsWith(".domain") ||
               lowerName.EndsWith(".application") ||
               lowerName.EndsWith(".app") ||
               normalizedPath.Contains("/core/") ||
               normalizedPath.Contains("/domain/");
    }

    private static bool IsDomainService(string name, string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        return normalizedPath.Contains("/parsers/") ||
               normalizedPath.Contains("/services/") ||
               normalizedPath.Contains("/handlers/") ||
               normalizedPath.Contains("/engine/") ||
               lowerName.Contains("parser") ||
               lowerName.Contains("cypher") ||
               lowerName.Contains("service") ||
               lowerName.Contains("handler");
    }

    private static bool IsFoundation(string name, string path, int outDegree)
    {
        var lowerName = name.ToLowerInvariant();
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();

        return lowerName.EndsWith(".common") ||
               lowerName.EndsWith(".shared") ||
               lowerName.EndsWith(".infrastructure") ||
               lowerName.EndsWith(".data") ||
               normalizedPath.Contains("/common/") ||
               normalizedPath.Contains("/shared/") ||
               outDegree == 0;
    }
}
