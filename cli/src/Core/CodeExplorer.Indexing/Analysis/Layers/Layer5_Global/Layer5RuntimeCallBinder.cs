using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Emits verified runtime <c>SERVICE_CALL</c> relationships (never conflated with build-time
/// <c>Project -[:DEPENDS_ON]-&gt; Project</c>) and filters generic/low-specificity cross-repo HTTP matches.
/// </summary>
internal static class Layer5RuntimeCallBinder
{
    private const string DepTypeServiceCall = "service_call";
    private const string ReasonRuntimeHttp = "runtime_http";
    private const string ReasonRuntimeEntrypoint = "runtime_entrypoint";
    private const string ReasonRuntimeHost = "runtime_host";
    private const string SchemeSep = "://";

    private static readonly string[] ExcludedTargetSuffixes =
    [
        ".data", ".logic", ".contracts", ".client", "-client",
        ".common", "-common", ".shared", "-shared"
    ];

    private static readonly HashSet<string> GenericSingleRouteSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "health", "healthz", "ready", "readyz", "live", "livez", "ping", "status",
        "metrics", "info", "version", "actuator", "token", "tokens", "login", "logout",
        "authorize", "userinfo", "realms", "realm", "users", "user", "roles", "groups",
        "permissions", "audit", "events", "jobs", "tasks", "config", "settings",
        "graphql", "webhook", "webhooks", "callback", "callbacks", "search", "query"
    };

    private static readonly HashSet<string> AmbiguousBareHostStems = new(StringComparer.OrdinalIgnoreCase)
    {
        "realm", "realms", "auth", "admin", "portal", "agent", "agents", "registry",
        "common", "commons", "core", "audit", "setup", "dashboard", "gateway", "proxy",
        "ui", "web", "bff", "api", "service", "services", "client", "external", "internal",
        "prod", "dev", "stage", "staging", "qa", "uat", "roles", "role", "helm",
        "login", "logout", "key_vault", "keyvault", "config", "configs", "test", "tests",
        "actions", "github-actions", "workflows", "scripts", "branches", "infra", "terraform", "modules"
    };

    private static readonly HashSet<string> NonCallableProjectLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "terraform", "hcl", "shell", "bash", "sh", "powershell", "pwsh", "batch", "script", "sql"
    };

    public static bool IsGenericSingleWordServiceTarget(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return true;
        var clean = key.Trim();
        return AmbiguousBareHostStems.Contains(clean);
    }

    public static bool IsCallableServiceTarget(ProjectNode? targetProj)
    {
        if (targetProj == null || targetProj.IsLibrary || targetProj.IsTest)
        {
            return false;
        }
        if (targetProj.EntityKind is ProjectEntityKind.Library or ProjectEntityKind.Test or ProjectEntityKind.ScriptProject or ProjectEntityKind.CliTool)
        {
            return false;
        }
        var projectType = targetProj.ProjectType.ToCanonicalString();
        if (!string.IsNullOrWhiteSpace(projectType) && NonCallableProjectLanguages.Contains(projectType.Trim()))
        {
            return false;
        }
        var normPath = (targetProj.Path ?? string.Empty).Replace('\\', '/');
        if (normPath.Contains("/tf/modules/", StringComparison.OrdinalIgnoreCase) ||
            normPath.StartsWith("tf/modules/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    public static bool CanEmitRuntimeServiceCall(ProjectNode? callerProj, ProjectNode? targetProj)
    {
        if (callerProj == null || targetProj == null || callerProj.Id == targetProj.Id)
        {
            return false;
        }
        if (callerProj.IsTest || callerProj.EntityKind == ProjectEntityKind.Test)
        {
            return false;
        }
        return IsCallableServiceTarget(targetProj);
    }

    private static readonly HashSet<string> SingleRepoTierFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "web", "frontend", "backend", "client", "server", "apps", "services", "packages"
    };

    public static bool HasSufficientRouteOrHostSpecificity(
        Layer5AnalysisParser.PreparedExternalService extService,
        string[] targetSegments,
        ProjectNode? callerProj,
        ProjectNode? targetProj,
        Func<ProjectNode, string, bool> projectMatcher,
        string? protocol = null)
    {
        const string protoGraphQl = "graphql", protoGrpc = "grpc", protoWcf = "wcf";
        if (!CanEmitRuntimeServiceCall(callerProj, targetProj))
        {
            return false;
        }

        var hasExplicitHostMatch = !extService.IsGenericHostOrService &&
                                   !extService.IsPathSegment &&
                                   extService.Service.IsRelativePath != true &&
                                   projectMatcher(targetProj!, extService.Service.DomainOrService);
        if (hasExplicitHostMatch)
        {
            return true;
        }

        // Cross-repo calls without an explicit host/service name require >= 2 meaningful static REST segments
        // (or >= 1 non-generic operation name for typed RPC protocols like GraphQL / gRPC / WCF)
        var sameTopRoot = ShareTopLevelRoot(callerProj!, targetProj!);
        if (!sameTopRoot)
        {
            var isTypedRpc = !string.IsNullOrEmpty(protocol) &&
                             (protocol.Equals(protoGraphQl, StringComparison.OrdinalIgnoreCase) ||
                              protocol.Equals(protoGrpc, StringComparison.OrdinalIgnoreCase) ||
                              protocol.Equals(protoWcf, StringComparison.OrdinalIgnoreCase));
            var minMeaningful = isTypedRpc ? 1 : 2;
            var meaningfulStatic = CountMeaningfulStaticSegments(targetSegments);
            if (meaningfulStatic < minMeaningful)
            {
                return false;
            }
        }

        return true;
    }

    public static void EmitServiceCallViaEntryPoint(
        Layer5Context ctx,
        ProjectNode? callerProj,
        ProjectNode? targetProj,
        string entryPointName,
        string? sourceDomain,
        HashSet<(string From, string To)> addedServiceCalls,
        List<Relationship> lateBoundRels)
    {
        if (!CanEmitRuntimeServiceCall(callerProj, targetProj))
        {
            return;
        }

        if (addedServiceCalls.Add((callerProj!.Id, targetProj!.Id)))
        {
            ctx.Layer5Log.SynthesizedDependencyViaEntryPoint(callerProj.Name, targetProj.Name, entryPointName);
            lateBoundRels.Add(Relationship.FromRelationship(new ServiceCallRelationship(callerProj.Id, targetProj.Id)
            {
                DependencyType = DepTypeServiceCall,
                Reason = ReasonRuntimeEntrypoint,
                MatchedEndpoint = entryPointName,
                SourceDep = sourceDomain
            }));
        }
    }

    public static void EmitServiceCallViaEndpoint(
        Layer5Context ctx,
        ProjectNode? callerProj,
        ProjectNode? targetProj,
        string? routeTemplate,
        string? sourceDomain,
        Relationship contributingRel,
        HashSet<(string From, string To)> addedServiceCalls,
        List<Relationship> lateBoundRels)
    {
        if (!CanEmitRuntimeServiceCall(callerProj, targetProj))
        {
            return;
        }

        if (addedServiceCalls.Add((callerProj!.Id, targetProj!.Id)))
        {
            ctx.Layer5Log.SynthesizedDependencyViaEndpoint(callerProj.Name, targetProj.Name, routeTemplate);
            var svcCall = Relationship.FromRelationship(new ServiceCallRelationship(callerProj.Id, targetProj.Id)
            {
                DependencyType = DepTypeServiceCall,
                Reason = ReasonRuntimeHttp,
                MatchedEndpoint = routeTemplate,
                SourceDep = sourceDomain
            });
            EvidenceProvenanceHelper.RecordContributingEdge(svcCall, contributingRel);
            lateBoundRels.Add(svcCall);
        }
        else
        {
            var existing = lateBoundRels.FirstOrDefault(r =>
                r.From == callerProj.Id && r.To == targetProj.Id && r.Kind == RelationshipKind.ServiceCall);
            if (existing != null)
            {
                EvidenceProvenanceHelper.RecordContributingEdge(existing, contributingRel);
            }
        }
    }

    public static void TryInferServiceCallViaHost(
        Layer5Context ctx,
        Layer5AnalysisParser.PreparedExternalService extService,
        IReadOnlyList<ProjectNode> allProjects,
        IReadOnlySet<string> callableProjectIds,
        Func<ProjectNode, string, bool> checkProjectMatch,
        HashSet<(string From, string To)> addedServiceCalls,
        List<Relationship> lateBoundRels)
    {
        var domain = extService.Service.DomainOrService;
        if (string.IsNullOrWhiteSpace(domain) ||
            domain is "*" or "unknown-service" ||
            extService.Service.InferredFromIdentifier == true ||
            (extService.Service.Path.StartsWith('/') && !extService.Service.Path.Contains(SchemeSep, StringComparison.Ordinal)))
        {
            return;
        }

        var cleanStem = domain.Trim().ToLowerInvariant();
        if (cleanStem.Length < 5 || AmbiguousBareHostStems.Contains(cleanStem))
        {
            return;
        }

        var callerProj = extService.CallerProj;
        if (callerProj == null || callerProj.IsTest || callerProj.EntityKind == ProjectEntityKind.Test)
        {
            return;
        }

        if (checkProjectMatch(callerProj, domain))
        {
            return;
        }

        var matches = new List<ProjectNode>();
        foreach (var proj in allProjects)
        {
            if (!CanEmitRuntimeServiceCall(callerProj, proj) || !callableProjectIds.Contains(proj.Id))
            {
                continue;
            }

            var pName = proj.Name.ToLowerInvariant();
            if (ExcludedTargetSuffixes.Any(s => pName.EndsWith(s, StringComparison.Ordinal)))
            {
                continue;
            }

            if (checkProjectMatch(proj, domain))
            {
                matches.Add(proj);
            }
        }

        // Only emit when host unambiguously identifies a single runnable service
        if (matches.Count == 1)
        {
            var target = matches[0];
            if (addedServiceCalls.Add((callerProj.Id, target.Id)))
            {
                ctx.Layer5Log.InferredDependencyViaDomain(callerProj.Name, target.Name, domain);
                lateBoundRels.Add(Relationship.FromRelationship(new ServiceCallRelationship(callerProj.Id, target.Id)
                {
                    DependencyType = DepTypeServiceCall,
                    Reason = ReasonRuntimeHost,
                    SourceDep = domain
                }));
            }
        }
    }

    private static int CountMeaningfulStaticSegments(string[] segments)
    {
        var count = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (string.IsNullOrWhiteSpace(seg)) continue;
            if (seg == "*" || seg.StartsWith(':') || (seg.StartsWith('{') && seg.EndsWith('}'))) continue;
            if (seg.ToLowerInvariant() is "api" or "rest" ||
                (seg.StartsWith('v') && seg.Length <= 4 && seg.Skip(1).All(char.IsDigit)))
            {
                continue;
            }
            if (segments.Length == 1 && GenericSingleRouteSegments.Contains(seg))
            {
                continue;
            }
            if (!GenericSingleRouteSegments.Contains(seg))
            {
                count++;
            }
        }
        return count;
    }

    private static bool ShareTopLevelRoot(ProjectNode a, ProjectNode b)
    {
        var topA = ExtractTopSegment(a.Path);
        var topB = ExtractTopSegment(b.Path);
        if (string.IsNullOrEmpty(topA) || string.IsNullOrEmpty(topB))
        {
            return true;
        }
        if (SingleRepoTierFolders.Contains(topA) && SingleRepoTierFolders.Contains(topB))
        {
            return true;
        }
        return string.Equals(topA, topB, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractTopSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var clean = path.Replace('\\', '/').Trim('/');
        var slash = clean.IndexOf('/');
        return slash > 0 ? clean[..slash] : clean;
    }
}
