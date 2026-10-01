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

    public static readonly IReadOnlyList<ProjectLayerInfo> All =
    [
        Ingress,
        Components,
        Egress,
        Foundation,
        Tests
    ];
}

public class ProjectClassifierItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? FilePath { get; init; }
    public string? Framework { get; init; }
    public string? Role { get; set; }
    public bool IsLibrary { get; set; }
    public int EndpointsCount { get; init; }
    public IReadOnlyList<Common.Nodes.Layer4_Semantic.EntryPointNode> EntryPoints { get; init; } = [];
    public int ExternalServicesCount { get; init; }
    public int UsesDbCount { get; init; }
    public IReadOnlyDictionary<string, string>? Extensions { get; set; }
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

            if (string.IsNullOrEmpty(p.Role) && (p.Extensions == null || !p.Extensions.ContainsKey("entity_kind")))
            {
                var extDict = p.Extensions != null
                    ? new Dictionary<string, string>(p.Extensions)
                    : new Dictionary<string, string>();

                var (detectedRole, isLib, detectedClassification) = ProjectRoleDetector.Detect(
                    p.FilePath ?? "",
                    [],
                    p.FilePath ?? "",
                    p.Name,
                    p.Framework ?? "",
                    null,
                    extDict);

                p.Role = detectedRole.ToString();
                p.IsLibrary = isLib;
                extDict["entity_kind"] = detectedClassification.Kind.ToString();
                if (detectedClassification.SubKind != Common.ProjectEntitySubKind.None)
                {
                    extDict["sub_kind"] = detectedClassification.SubKind.ToString();
                }
                p.Extensions = extDict;
            }
        }

        var isTestMap = projList.ToDictionary(p => p.Id, p => IsTest(p), StringComparer.OrdinalIgnoreCase);

        foreach (var d in depList)
        {
            if (outgoingCount.ContainsKey(d.SourceId)) outgoingCount[d.SourceId]++;

            // Test projects referencing an application do not make the application an internal dependency
            if (!isTestMap.GetValueOrDefault(d.SourceId, false))
            {
                if (incomingCount.ContainsKey(d.TargetId)) incomingCount[d.TargetId]++;
            }
        }

        foreach (var p in projList)
        {
            var inDegree = incomingCount.GetValueOrDefault(p.Id, 0);
            var outDegree = outgoingCount.GetValueOrDefault(p.Id, 0);

            // 1. Tests Layer
            if (IsTest(p))
            {
                result[p.Id] = StandardLayers.Tests;
                continue;
            }

            // 2. Foundation Layer (Libraries, Models, Migrations)
            // Absolute priority: libraries (UI components, shared libs, contracts) NEVER belong to Ingress!
            if (IsFoundation(p))
            {
                result[p.Id] = StandardLayers.Foundation;
                continue;
            }

            // 3. Ingress Layer (End-user Frontend Apps, CLI Tools, and API Gateways/BFFs)
            if (IsIngress(p, inDegree, outDegree))
            {
                result[p.Id] = StandardLayers.Ingress;
                continue;
            }

            // 4. Egress Layer (External integration clients, outbound webhooks, third-party adapters)
            if (IsEgress(p))
            {
                result[p.Id] = StandardLayers.Egress;
                continue;
            }

            // 5. Components Layer (Domain services, Web services, Schedulers, Background workers)
            result[p.Id] = StandardLayers.Components;
        }

        return result;
    }

    public static bool IsTest(ProjectClassifierItem p)
    {
        var name = p.Name ?? "";
        return string.Equals(p.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "Test", StringComparison.OrdinalIgnoreCase) ||
               p.Extensions?.GetValueOrDefault("is_test_project") == "true" ||
               p.Extensions?.GetValueOrDefault("manifest_type") == "test" ||
               name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("-tests", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("-test", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFoundation(ProjectClassifierItem p)
    {
        var lowerName = (p.Name ?? "").ToLowerInvariant();

        // Domain, Core, Application, and Engine components belong to Components, not Foundation
        if (lowerName.EndsWith(".core") || lowerName.EndsWith("-core") ||
            lowerName.EndsWith(".domain") || lowerName.EndsWith("-domain") ||
            lowerName.EndsWith(".application") || lowerName.EndsWith("-application") ||
            lowerName.Contains("parser"))
        {
            return false;
        }

        // Explicit library role or flag
        if (p.IsLibrary) return true;

        if (string.Equals(p.Role, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Role, "Library", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Role, "DatabaseMigration", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "Library", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "DatabaseMigration", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Extensions != null)
        {
            if (p.Extensions.GetValueOrDefault("is_ui_library") == "true" ||
                p.Extensions.GetValueOrDefault("is_library") == "true" ||
                p.Extensions.GetValueOrDefault("manifest_type") == "library" ||
                p.Extensions.GetValueOrDefault("manifest_type") == "migration")
            {
                return true;
            }
        }

        return lowerName.Equals("library") ||
               lowerName.EndsWith(".common") ||
               lowerName.EndsWith(".shared") ||
               lowerName.EndsWith(".models") ||
               lowerName.EndsWith("-models") ||
               lowerName.EndsWith(".model") ||
               lowerName.EndsWith("-model") ||
               lowerName.EndsWith(".entities") ||
               lowerName.EndsWith("-entities") ||
               lowerName.EndsWith(".contracts") ||
               lowerName.EndsWith("-contracts") ||
               lowerName.EndsWith(".dto") ||
               lowerName.EndsWith(".dtos");
    }

    public static bool IsIngress(ProjectClassifierItem p, int inDegree = 0, int outDegree = 0)
    {
        // Libraries can never be Ingress
        if (IsFoundation(p)) return false;

        // Workers and Schedulers can never be Ingress (unless it's an explicit CLI tool)
        if (IsWorkerOrScheduler(p) && !IsCli(p)) return false;

        // 1. Frontend Apps (End-user UI Web Applications)
        if (string.Equals(p.Role, "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Role, "App", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "App", StringComparison.OrdinalIgnoreCase) ||
            p.Extensions?.GetValueOrDefault("is_frontend_app") == "true")
        {
            return true;
        }

        // 2. CLI Tools (Command-line applications)
        if (IsCli(p))
        {
            return true;
        }

        // 3. API Gateways & Reverse Proxies / BFFs
        if (p.Extensions?.GetValueOrDefault("is_api_gateway") == "true" ||
            p.Extensions?.GetValueOrDefault("gateway_type") == "reverse_proxy" ||
            p.Extensions?.GetValueOrDefault("has_ingress_contract") == "true")
        {
            return true;
        }

        var lowerName = (p.Name ?? "").ToLowerInvariant();
        if (lowerName.EndsWith(".gateway") || lowerName.EndsWith("-gateway") ||
            lowerName.EndsWith(".bff") || lowerName.EndsWith("-bff") ||
            lowerName.Equals("bff") || lowerName.Equals("gateway") ||
            lowerName.Contains("gateway") || lowerName.Contains("bff"))
        {
            return true;
        }

        // 4. Monolithic entrypoint project
        var normPath = (p.FilePath ?? "").Replace('\\', '/');
        if (inDegree == 0 && outDegree > 0 &&
            (lowerName.EndsWith(".cli") || lowerName.EndsWith(".app") || lowerName.EndsWith(".host") || lowerName.Equals("cli") ||
             normPath.Contains("/ui/", StringComparison.OrdinalIgnoreCase) || normPath.Contains("/apps/", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public static bool IsCli(ProjectClassifierItem p)
    {
        return string.Equals(p.Role, "CliTool", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(p.Role, "CliApp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "CliTool", StringComparison.OrdinalIgnoreCase) ||
               p.Extensions?.GetValueOrDefault("manifest_type") == "cli" ||
               p.Extensions?.GetValueOrDefault("has_cli_bin") == "true" ||
               p.EntryPoints.Any(ep => ep.EntryType == "cli");
    }

    public static bool IsWorkerOrScheduler(ProjectClassifierItem p)
    {
        var primaryRole = p.Extensions?.GetValueOrDefault("primary_role");
        if (!string.IsNullOrEmpty(primaryRole))
        {
            if (string.Equals(primaryRole, "Scheduler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(primaryRole, "WorkerService", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(primaryRole, "Worker", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(primaryRole, "WebService", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(primaryRole, "FrontendFramework", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(primaryRole, "ApiGateway", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (string.Equals(p.Role, "Worker", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Extensions?.GetValueOrDefault("entity_kind"), "Worker", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Extensions != null)
        {
            if (p.Extensions.GetValueOrDefault("manifest_type") == "worker" ||
                p.Extensions.GetValueOrDefault("framework_type") == "worker")
            {
                return true;
            }

            if (!string.Equals(p.Role, "Service", StringComparison.OrdinalIgnoreCase) &&
                (p.Extensions.GetValueOrDefault("is_scheduler") == "true" ||
                 p.Extensions.GetValueOrDefault("is_queue_worker") == "true" ||
                 p.Extensions.GetValueOrDefault("has_schedule") == "true"))
            {
                return true;
            }
        }

        if (p.EntryPoints.Any(ep => ep.EntryType is "queue-listener" or "cron" or "worker"))
        {
            return true;
        }

        var lowerName = (p.Name ?? "").ToLowerInvariant();
        return lowerName.EndsWith("-worker") ||
               lowerName.EndsWith(".worker") ||
               lowerName.EndsWith("_worker") ||
               lowerName.EndsWith("-scheduler") ||
               lowerName.EndsWith(".scheduler") ||
               lowerName.EndsWith("_scheduler") ||
               lowerName.EndsWith("-consumer") ||
               lowerName.EndsWith("_consumer");
    }

    public static bool IsEgress(ProjectClassifierItem p)
    {
        if (IsFoundation(p)) return false;
        if (IsWorkerOrScheduler(p)) return false;
        if (IsCli(p)) return false;

        if (p.Extensions?.GetValueOrDefault("is_egress") == "true")
        {
            return true;
        }

        var lowerName = (p.Name ?? "").ToLowerInvariant();
        if (lowerName.StartsWith("integration-") ||
            lowerName.StartsWith("integration_") ||
            lowerName.EndsWith("adapter") ||
            lowerName.EndsWith("client") ||
            lowerName.Contains("integration-service") ||
            lowerName.Contains("integration_service"))
        {
            return true;
        }

        // External services calls without hosting inbound endpoints (excluding workers/schedulers/cli/services)
        if (p.ExternalServicesCount > 0 && p.EndpointsCount == 0 && p.EntryPoints.Count == 0 &&
            !string.Equals(p.Role, "Service", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static bool HasProtocolTokens(string name, string path)
    {
        var lowerName = (name ?? "").ToLowerInvariant();
        var parts = lowerName.Split('.', '-', '_');
        var protocolTokens = new[] { "graphql", "grapql", "grpc", "gateway", "gateways", "bff", "mqtt", "endpoint", "endpoints" };
        return parts.Any(p => protocolTokens.Contains(p));
    }
}
