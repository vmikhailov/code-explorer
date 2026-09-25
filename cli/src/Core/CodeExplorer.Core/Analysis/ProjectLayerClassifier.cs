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
    public static readonly ProjectLayerInfo Ingress = new(
        "layer_ingress",
        "Ingress",
        0,
        "#fbbf24", // Amber
        "⚡",
        "Entry points, HTTP APIs, CLI commands, UI/Web, BFF, and Gateways"
    );

    public static readonly ProjectLayerInfo Components = new(
        "layer_components",
        "Components",
        1,
        "#c084fc", // Purple
        "🧩",
        "Domain services, core business logic, engines, schedulers, and processors"
    );

    public static readonly ProjectLayerInfo Egress = new(
        "layer_egress",
        "Egress",
        2,
        "#38bdf8", // Cyan
        "📤",
        "External clients, outbound adapters, notifiers, publishers, and integrations"
    );

    public static readonly ProjectLayerInfo Foundation = new(
        "layer_foundation",
        "Storage & Foundation",
        3,
        "#34d399", // Emerald
        "🗄️",
        "Databases, persistence models, shared utilities, and common infrastructure"
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
        Ingress,
        Components,
        Egress,
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
    public string? Role { get; init; }
    public bool IsLibrary { get; init; }
    public int EndpointsCount { get; init; }
    public IReadOnlyList<CodeExplorer.Core.Common.Nodes.Layer4_Semantic.EntryPointNode> EntryPoints { get; init; } = [];
    public int ExternalServicesCount { get; init; }
    public int UsesDbCount { get; init; }
    public IReadOnlyDictionary<string, string>? Extensions { get; init; }
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
            var inDegree = incomingCount.GetValueOrDefault(p.Id, 0);
            var outDegree = outgoingCount.GetValueOrDefault(p.Id, 0);
            var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();

            // 1. Tests & Tools
            if (IsTestOrTool(name, path, p))
            {
                result[p.Id] = StandardLayers.Tests;
                continue;
            }

            // 2. Library Priority (is_library == true / role == SharedLibrary / manifest_type == "library" / Library path)
            // If project is a library (e.g., fe/projects/ui/src/lib/* or common-nest),
            // it immediately goes to Storage & Foundation, avoiding /ui/ or /integrations/.
            if (IsLibrary(p, name, path))
            {
                result[p.Id] = StandardLayers.Foundation;
                continue;
            }

            // 3. Worker Projects (role == "Worker" or manifest_type == "worker" or worker entry points)
            // Workers belong to Components (background business processors), NOT Ingress.
            if (IsWorker(p, name, path))
            {
                result[p.Id] = StandardLayers.Components;
                continue;
            }

            // 4. Ingress (Entry Points, APIs, UI, Web, Gateways, BFF, CLI)
            if (IsIngress(name, path, inDegree, p))
            {
                result[p.Id] = StandardLayers.Ingress;
                continue;
            }

            // 5. Egress (External clients, adapters, notifiers, publishers, webhooks, integrations)
            if (IsEgress(name, path, p))
            {
                result[p.Id] = StandardLayers.Egress;
                continue;
            }

            // 6. Storage & Foundation (Common, Shared, DB, KV, Infrastructure)
            if (IsExplicitFoundation(name, path))
            {
                result[p.Id] = StandardLayers.Foundation;
                continue;
            }

            // 7. Components (Domain, Core, Services, Engines, Processors)
            if (IsComponents(name, path))
            {
                result[p.Id] = StandardLayers.Components;
                continue;
            }

            // 8. Fallback based on topology:
            // Stricter inDegree == 0 rule: Do NOT treat a service as Ingress just because inDegree == 0.
            // If it's a backend service/workload, default to Components.
            if (inDegree > 0 && outDegree == 0 && p.EndpointsCount == 0 && p.EntryPoints.Count == 0)
            {
                var hasServiceFramework = !string.IsNullOrWhiteSpace(p.Framework) &&
                                          !p.Framework.Equals("Library", StringComparison.OrdinalIgnoreCase);
                var isServicePath = normalizedPath.Contains("/services/") ||
                                    normalizedPath.Contains("/apps/") ||
                                    normalizedPath.Contains("/microservices/");

                result[p.Id] = (hasServiceFramework || isServicePath)
                    ? StandardLayers.Components
                    : StandardLayers.Foundation;
            }
            else
            {
                // Default to Components for regular backend services and workloads
                result[p.Id] = StandardLayers.Components;
            }
        }

        return result;
    }

    private static bool IsTestOrTool(string name, string path, ProjectClassifierItem p)
    {
        if (string.Equals(p.Role, "Test", StringComparison.OrdinalIgnoreCase)) return true;

        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        return lowerName.EndsWith(".tests") ||
               lowerName.EndsWith(".test") ||
               lowerName.EndsWith("-tests") ||
               lowerName.EndsWith("-test") ||
               lowerName.EndsWith("_tests") ||
               lowerName.EndsWith("_test") ||
               lowerName.StartsWith("test-") ||
               lowerName.StartsWith("tests-") ||
               lowerName.Contains(".test.") ||
               lowerName.Contains(".tests.") ||
               lowerName.Contains("-test-") ||
               lowerName.Contains("-tests-") ||
               lowerName.Contains("ontologygen") ||
               lowerName.Contains("benchmark") ||
               lowerName.Contains(".spec") ||
               lowerName.Contains("-spec") ||
               normalizedPath.Contains("/tests/") ||
               normalizedPath.Contains("/test/");
    }

    private static bool IsLibrary(ProjectClassifierItem p, string name, string path)
    {
        if (p.IsLibrary) return true;

        if (string.Equals(p.Role, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Role, "Library", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Extensions != null)
        {
            var manifestType = p.Extensions.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
            if (manifestType == "library") return true;

            var outputType = p.Extensions.GetValueOrDefault("output_type");
            var sdk = p.Extensions.GetValueOrDefault("sdk");
            if (outputType == "Library" && sdk == "Microsoft.NET.Sdk") return true;
        }

        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        // Angular / React / Nest library paths, e.g. fe/projects/ui/src/lib/*, common-nest, etc.
        if (normalizedPath.Contains("/src/lib/") ||
            normalizedPath.Contains("/src/libs/") ||
            normalizedPath.Contains("/projects/ui/src/lib/") ||
            (normalizedPath.Contains("/fe/projects/") && normalizedPath.Contains("/lib/")) ||
            normalizedPath.Contains("/common-nest") ||
            lowerName.StartsWith("common-") ||
            lowerName.EndsWith(".lib") ||
            lowerName.EndsWith("-lib") ||
            lowerName.Contains("-lib-") ||
            lowerName.Contains(".lib."))
        {
            return true;
        }

        return false;
    }

    private static bool IsWorker(ProjectClassifierItem p, string name, string path)
    {
        if (string.Equals(p.Role, "Worker", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Extensions != null)
        {
            var manifestType = p.Extensions.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
            if (manifestType == "worker") return true;

            var frameworkType = p.Extensions.GetValueOrDefault("framework_type")?.ToLowerInvariant();
            if (frameworkType == "worker") return true;

            var sdk = p.Extensions.GetValueOrDefault("sdk");
            if (sdk == "Microsoft.NET.Sdk.Worker") return true;
        }

        if (p.EntryPoints.Any(ep => ep.EntryType is "queue-listener" or "cron" or "worker"))
        {
            return true;
        }

        var lowerName = name.ToLowerInvariant();
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();

        if (normalizedPath.Contains("/worker/") || normalizedPath.Contains("/workers/") ||
            normalizedPath.Contains("/consumer/") || normalizedPath.Contains("/consumers/") ||
            normalizedPath.Contains("/scheduler/"))
        {
            return true;
        }

        if (lowerName.EndsWith("-worker") ||
            lowerName.EndsWith(".worker") ||
            lowerName.EndsWith("_worker") ||
            lowerName.EndsWith("-consumer") ||
            lowerName.EndsWith("_consumer"))
        {
            // Do not treat gateways as background workers unless role is explicitly Worker
            if (!lowerName.Contains("gateway"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIngress(string name, string path, int inDegree, ProjectClassifierItem p)
    {
        if (IsWorker(p, name, path)) return false;
        if (IsLibrary(p, name, path)) return false;

        // Evidence 1: Role or Manifest Type
        if (string.Equals(p.Role, "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Role, "CliTool", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Extensions != null)
        {
            var manifestType = p.Extensions.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
            if (manifestType is "cli") return true;

            var hasCliBin = p.Extensions.GetValueOrDefault("has_cli_bin") == "true";
            if (hasCliBin) return true;

            var frameworkType = p.Extensions.GetValueOrDefault("framework_type")?.ToLowerInvariant();
            if (frameworkType is "frontend") return true;

            var sdk = p.Extensions.GetValueOrDefault("sdk");
            if (sdk == "Microsoft.NET.Sdk.Web" && (p.EndpointsCount > 0 || inDegree == 0))
            {
                return true;
            }
        }

        // Evidence 2: EntryPoints (e.g. CLI command entrypoint)
        if (p.EntryPoints.Any(ep => ep.EntryType == "cli"))
        {
            return true;
        }

        // Evidence 3: Path indicators for Ingress
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        if (normalizedPath.Contains("/ui/") ||
            normalizedPath.Contains("/cli/") ||
            normalizedPath.Contains("/web/") ||
            normalizedPath.Contains("/host/") ||
            normalizedPath.Contains("/bff/") ||
            normalizedPath.Contains("/gateway/") ||
            normalizedPath.Contains("/gateways/") ||
            normalizedPath.Contains("/fe/") ||
            normalizedPath.Contains("/frontend/") ||
            normalizedPath.Contains("/landing/") ||
            normalizedPath.Contains("/landers/") ||
            normalizedPath.Contains("/ingress/"))
        {
            return true;
        }

        // Evidence 4: Top-level API Gateway / BFF / Ingress by Endpoints + InDegree == 0
        if (p.EndpointsCount > 0 && inDegree == 0 && (lowerName.Contains("gateway") || lowerName.Contains("bff") || normalizedPath.Contains("/api/")))
        {
            return true;
        }

        if (lowerName.EndsWith(".cli") ||
            lowerName.EndsWith(".ui") ||
            lowerName.EndsWith(".web") ||
            lowerName.EndsWith(".api") ||
            lowerName.EndsWith(".host") ||
            lowerName.EndsWith(".bff") ||
            lowerName.EndsWith(".gateway") ||
            lowerName.EndsWith(".fe") ||
            lowerName.EndsWith(".frontend") ||
            lowerName.Equals("codeexplorer", StringComparison.OrdinalIgnoreCase) ||
            lowerName.Contains("gateway") ||
            lowerName.Contains("bff") ||
            lowerName.Contains("-fe") ||
            lowerName.Contains("landing") ||
            lowerName.Contains("landers"))
        {
            return true;
        }

        return false;
    }

    private static bool IsEgress(string name, string path, ProjectClassifierItem p)
    {
        // Evidence 1: External service calls without hosting server endpoints
        if (p.ExternalServicesCount > 0 && p.EndpointsCount == 0 && p.EntryPoints.Count == 0)
        {
            return true;
        }

        // Evidence 2: Path indicators
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        if (normalizedPath.Contains("/adapter/") ||
            normalizedPath.Contains("/adapters/") ||
            normalizedPath.Contains("/client/") ||
            normalizedPath.Contains("/clients/") ||
            normalizedPath.Contains("/notifier/") ||
            normalizedPath.Contains("/notifiers/") ||
            normalizedPath.Contains("/postback/") ||
            normalizedPath.Contains("/egress/") ||
            normalizedPath.Contains("/integration/") ||
            normalizedPath.Contains("/integrations/"))
        {
            return true;
        }

        return lowerName.EndsWith(".adapter") ||
               lowerName.EndsWith(".adapters") ||
               lowerName.EndsWith(".client") ||
               lowerName.EndsWith(".clients") ||
               lowerName.EndsWith(".notifier") ||
               lowerName.EndsWith(".integration") ||
               lowerName.EndsWith(".egress") ||
               lowerName.StartsWith("integration-") ||
               lowerName.StartsWith("integration_") ||
               lowerName.Contains("integration-service") ||
               lowerName.Contains("integration_service") ||
               lowerName.Contains("-integration") ||
               lowerName.Contains("_integration") ||
               lowerName.Contains("-adapter") ||
               lowerName.Contains("adapter") ||
               lowerName.Contains("notifier") ||
               lowerName.Contains("postback") ||
               lowerName.Contains("webhook") ||
               lowerName.Contains("publisher");
    }

    private static bool IsExplicitFoundation(string name, string path)
    {
        var lowerName = name.ToLowerInvariant();
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();

        if (lowerName.EndsWith(".common") ||
            lowerName.EndsWith(".shared") ||
            lowerName.EndsWith(".infrastructure") ||
            lowerName.EndsWith(".infra") ||
            lowerName.EndsWith(".data") ||
            lowerName.EndsWith(".db") ||
            lowerName.EndsWith(".models") ||
            lowerName.EndsWith("-models") ||
            lowerName.EndsWith(".model") ||
            lowerName.EndsWith("-model") ||
            lowerName.EndsWith(".entities") ||
            lowerName.EndsWith("-entities") ||
            lowerName.EndsWith(".contracts") ||
            lowerName.EndsWith("-contracts") ||
            lowerName.EndsWith(".dto") ||
            lowerName.EndsWith(".dtos") ||
            lowerName.EndsWith(".types") ||
            lowerName.EndsWith("-types") ||
            lowerName.Equals("library", StringComparison.OrdinalIgnoreCase) ||
            lowerName.Contains("library") ||
            lowerName.Contains("-lib") ||
            lowerName.EndsWith(".lib") ||
            lowerName.Contains("common") ||
            lowerName.Contains("shared") ||
            lowerName.Contains("kv") ||
            normalizedPath.Contains("/libs/") ||
            normalizedPath.Contains("/lib/") ||
            normalizedPath.Contains("/libraries/") ||
            normalizedPath.Contains("/common/") ||
            normalizedPath.Contains("/shared/") ||
            normalizedPath.Contains("/infrastructure/") ||
            normalizedPath.Contains("/infra/") ||
            normalizedPath.Contains("/data/") ||
            normalizedPath.Contains("/storage/") ||
            normalizedPath.Contains("/db/"))
        {
            return true;
        }

        return false;
    }

    private static bool IsComponents(string name, string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();

        return normalizedPath.Contains("/core/") ||
               normalizedPath.Contains("/domain/") ||
               normalizedPath.Contains("/parsers/") ||
               normalizedPath.Contains("/parser/") ||
               normalizedPath.Contains("/services/") ||
               normalizedPath.Contains("/service/") ||
               normalizedPath.Contains("/handlers/") ||
               normalizedPath.Contains("/handler/") ||
               normalizedPath.Contains("/engine/") ||
               normalizedPath.Contains("/scheduler/") ||
               normalizedPath.Contains("/aggregator/") ||
               lowerName.EndsWith(".core") ||
               lowerName.EndsWith(".domain") ||
               lowerName.EndsWith(".application") ||
               lowerName.EndsWith(".app") ||
               lowerName.Contains("parser") ||
               lowerName.Contains("cypher") ||
               lowerName.Contains("service") ||
               lowerName.Contains("handler") ||
               lowerName.Contains("scheduler") ||
               lowerName.Contains("aggregator") ||
               lowerName.Contains("decision") ||
               lowerName.Contains("rule-tree") ||
               lowerName.Contains("calculator") ||
               lowerName.Contains("calculation") ||
               lowerName.Contains("configurator") ||
               lowerName.Equals("sources", StringComparison.OrdinalIgnoreCase);
    }
}
