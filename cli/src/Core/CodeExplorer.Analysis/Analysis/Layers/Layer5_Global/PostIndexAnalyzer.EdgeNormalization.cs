using System.Text.Json;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    public static (DependencyCategory Category, string DependencyType, string NormalizedKind) NormalizeEdgeCategory(
        string? rawKind,
        NodeKind sourceKind,
        NodeKind targetKind,
        string? existingCategory = null,
        string? existingDepType = null,
        bool isTargetLibrary = false)
    {
        return NormalizeEdgeCategory(rawKind, sourceKind.ToCypherLabel(), targetKind.ToCypherLabel(), existingCategory, existingDepType, isTargetLibrary);
    }

    public static (DependencyCategory Category, string DependencyType, string NormalizedKind) NormalizeEdgeCategory(
        string? rawKind,
        NodeKind sourceKind,
        NodeKind targetKind,
        DependencyCategory? existingCategory,
        string? existingDepType = null,
        bool isTargetLibrary = false)
    {
        return NormalizeEdgeCategory(rawKind, sourceKind.ToCypherLabel(), targetKind.ToCypherLabel(), existingCategory?.ToCategoryString(), existingDepType, isTargetLibrary);
    }

    public static (DependencyCategory Category, string DependencyType, string NormalizedKind) NormalizeEdgeCategory(
        string? rawKind,
        string? sourceKind,
        string? targetKind,
        string? existingCategory = null,
        string? existingDepType = null,
        bool isTargetLibrary = false)
    {
        var kind = (rawKind ?? "").ToUpperInvariant();

        if (kind == OntologyConstants.Relationships.CoordinatesWith)
        {
            return (DependencyCategory.ServiceCall, "peer_mesh", OntologyConstants.Relationships.CoordinatesWith);
        }
        if (targetKind == OntologyConstants.NodeLabels.CloudService &&
            kind is OntologyConstants.Relationships.UsesCloud or OntologyConstants.Relationships.ReadsFrom or OntologyConstants.Relationships.WritesTo)
        {
            return (DependencyCategory.ServiceCall, "external_resource", kind);
        }

        if (kind is OntologyConstants.Relationships.Tests or "TESTS")
        {
            return (DependencyCategory.UnidentifiedDependency, "tests", OntologyConstants.Relationships.Tests);
        }

        DependencyCategory category;
        if (isTargetLibrary ||
            targetKind == OntologyConstants.NodeLabels.Package ||
            targetKind == OntologyConstants.NodeLabels.Library ||
            string.Equals(existingDepType, DependencyCategoryExtensions.Library, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existingDepType, "package", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existingCategory, DependencyCategoryExtensions.Library, StringComparison.OrdinalIgnoreCase))
        {
            category = DependencyCategory.Library;
        }
        else if (!string.IsNullOrEmpty(existingCategory) &&
                 DependencyCategoryExtensions.ParseCategory(existingCategory) is var parsedCat &&
                 parsedCat != DependencyCategory.UnidentifiedDependency)
        {
            category = parsedCat;
        }
        else if (!string.IsNullOrEmpty(existingDepType) &&
                 DependencyCategoryExtensions.ParseCategory(existingDepType) is var parsedDep &&
                 parsedDep != DependencyCategory.UnidentifiedDependency)
        {
            category = parsedDep;
        }
        else if (kind == OntologyConstants.Relationships.UsesDb ||
                 targetKind == OntologyConstants.NodeLabels.Database ||
                 sourceKind == OntologyConstants.NodeLabels.Database)
        {
            category = DependencyCategory.Database;
        }
        else if (targetKind == OntologyConstants.NodeLabels.Topic ||
                 sourceKind == OntologyConstants.NodeLabels.Topic)
        {
            category = DependencyCategory.Messaging;
        }
        else if (kind is OntologyConstants.Relationships.Triggers or OntologyConstants.Relationships.PublishesTo or OntologyConstants.Relationships.SubscribesTo or "PUBLISHES" or "SUBSCRIBED_BY" or "PUBLISHED_BY")
        {
            category = (sourceKind == OntologyConstants.NodeLabels.Project && targetKind == OntologyConstants.NodeLabels.Project)
                ? DependencyCategory.UnidentifiedDependency
                : DependencyCategory.Messaging;
        }
        else if (kind is OntologyConstants.Relationships.ServiceCall or OntologyConstants.Relationships.CallsEndpoint ||
                 targetKind == OntologyConstants.NodeLabels.ExternalService ||
                 sourceKind == OntologyConstants.NodeLabels.ExternalService)
        {
            category = DependencyCategory.ServiceCall;
        }
        else if (isTargetLibrary || targetKind == OntologyConstants.NodeLabels.Package)
        {
            category = DependencyCategory.Library;
        }
        else
        {
            category = DependencyCategory.UnidentifiedDependency;
        }

        string normalizedKind;
        if (category == DependencyCategory.Database)
        {
            normalizedKind = OntologyConstants.Relationships.UsesDb;
        }
        else if (kind == OntologyConstants.Relationships.Triggers && targetKind != OntologyConstants.NodeLabels.Topic && sourceKind != OntologyConstants.NodeLabels.Topic)
        {
            normalizedKind = OntologyConstants.Relationships.Triggers;
        }
        else if (category == DependencyCategory.Messaging)
        {
            if (sourceKind == OntologyConstants.NodeLabels.Topic)
            {
                normalizedKind = (kind is "PUBLISHED_BY") ? "PUBLISHED_BY" : "SUBSCRIBED_BY";
            }
            else
            {
                normalizedKind = (kind is "PUBLISHES" or "PUBLISHES_TO" or "PUBLISHED_BY") ? "PUBLISHES_TO" : "SUBSCRIBES_TO";
            }
        }
        else if (kind is "DEPENDS_ON" or "DependsOn")
        {
            normalizedKind = OntologyConstants.Relationships.DependsOn;
        }
        else if (category == DependencyCategory.ServiceCall)
        {
            normalizedKind = kind == OntologyConstants.Relationships.CallsEndpoint ? OntologyConstants.Relationships.CallsEndpoint : OntologyConstants.Relationships.ServiceCall;
        }
        else if (category == DependencyCategory.Library)
        {
            normalizedKind = kind == "LIBRARY" ? "LIBRARY" : OntologyConstants.Relationships.DependsOn;
        }
        else
        {
            normalizedKind = OntologyConstants.Relationships.DependsOn;
        }

        var depType = category.ToCategoryString();
        return (category, depType, normalizedKind);
    }

    public static List<EdgeNormalizationUpdate> ComputeEdgeNormalizationUpdates(EdgeNormalizationSnapshot snapshot)
    {
        var updates = new List<EdgeNormalizationUpdate>();
        const string PropCategory = "category";
        const string PropDependencyType = "dependency_type";
        var nodeMap = snapshot.NodeMap;

        foreach (var edge in snapshot.Edges)
        {
            var src = edge.Source;
            var tgt = edge.Target;
            var rawKind = edge.Kind;
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            nodeMap.TryGetValue(src, out var srcInfo);
            nodeMap.TryGetValue(tgt, out var tgtInfo);

            if (tgtInfo.Kind == default)
            {
                if (tgt.Contains(":pkg:") || tgt.Contains(":package:"))
                {
                    tgtInfo = (NodeKind.Package, true);
                }
                else if (tgt.Contains(":res:db:") || tgt.Contains(":db:") || tgt.Contains(":database:"))
                {
                    tgtInfo = (NodeKind.Database, false);
                }
                else if (tgt.Contains(":top:") || tgt.Contains(":topic:"))
                {
                    tgtInfo = (NodeKind.Topic, false);
                }
            }

            var props = new Dictionary<string, string>(edge.Properties, StringComparer.Ordinal);
            var existingCategory = props.GetValueOrDefault(PropCategory, string.Empty);
            var existingDepType = props.GetValueOrDefault(PropDependencyType, string.Empty);

            var (category, depType, normalizedKindStr) = NormalizeEdgeCategory(rawKind.ToCypherType(), srcInfo.Kind, tgtInfo.Kind,
                existingCategory, existingDepType, tgtInfo.IsLibrary);
            var normalizedKind = EnumExtensions.ParseRelationshipKind(normalizedKindStr);

            var categoryStr = category.ToCategoryString();
            var needsUpdate = !props.ContainsKey(PropDependencyType) ||
                              !string.Equals(props.GetValueOrDefault(PropDependencyType), depType,
                                  StringComparison.Ordinal) ||
                              !string.Equals(props.GetValueOrDefault(PropCategory), categoryStr,
                                  StringComparison.Ordinal) || rawKind != normalizedKind;

            if (needsUpdate)
            {
                props["dependency_type"] = depType;
                props["category"] = categoryStr;

                var propsJson = JsonSerializer.Serialize(props);
                updates.Add(new EdgeNormalizationUpdate(normalizedKind, propsJson, src, tgt, rawKind));
            }
        }

        return updates;
    }

    public static async Task<int> NormalizeEdgesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await db.LoadEdgeNormalizationSnapshotAsync(cancellationToken);
        var updates = ComputeEdgeNormalizationUpdates(snapshot);
        return await db.ApplyEdgeNormalizationUpdatesAsync(updates, cancellationToken);
    }
    public static ProjectNode? FindMatchingServiceProject(string key, IEnumerable<ProjectNode> projects)
    {
        if (string.IsNullOrWhiteSpace(key) || key is "*" or "unknown-service" or "app" or "api") return null;

        var cleanKey = key.Trim().ToLowerInvariant();
        var protoIdx = cleanKey.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0) cleanKey = cleanKey[(protoIdx + 3)..];
        var slashIdx = cleanKey.IndexOf('/');
        if (slashIdx >= 0) cleanKey = cleanKey[..slashIdx];
        var colonIdx = cleanKey.IndexOf(':');
        if (colonIdx >= 0) cleanKey = cleanKey[..colonIdx];

        if (cleanKey is "app" or "api" or "service" or "services" || IsGarbageExternalService(cleanKey))
        {
            return null;
        }

        cleanKey = ConfigurationIndex.StripServiceOptionsSuffixes(cleanKey);

        if (WorkspaceConventions.TryGetServiceAlias(cleanKey, out var aliased))
        {
            cleanKey = aliased.ToLowerInvariant();
        }

        if (cleanKey.StartWithIC("environment."))
        {
            cleanKey = cleanKey[12..];
        }
        else if (cleanKey.StartWithIC("env."))
        {
            cleanKey = cleanKey[4..];
        }
        else if (cleanKey.StartWithIC("config."))
        {
            cleanKey = cleanKey[7..];
        }
        else if (cleanKey.Contains('.'))
        {
            var dotParts = cleanKey.Split('.');
            if (dotParts.Length == 2)
            {
                var candidatePrefixMatch = FindMatchingServiceProject(dotParts[0], projects);
                if (candidatePrefixMatch != null) return candidatePrefixMatch;
            }
        }

        if (WorkspaceConventions.TryGetServiceAlias(cleanKey, out aliased))
        {
            cleanKey = aliased.ToLowerInvariant();
        }

        var normKey = cleanKey.Replace("-", "").Replace("_", "").Replace("service", "");
        if (string.IsNullOrWhiteSpace(normKey)) return null;

        var projList = projects.ToList();

        // 0. Direct suffix/segment match (e.g. 'spa-bff' -> FoodDelivery.Spa.Bff, 'gateway' -> FoodDelivery.ApiGateway)
        var dotNorm = cleanKey.Replace("-", ".").Replace("_", ".");
        var directMatch = projList.FirstOrDefault(p =>
            !p.IsLibrary &&
            (p.Name.EndsWith("." + dotNorm, StringComparison.OrdinalIgnoreCase) ||
             p.Name.EndsWith("." + cleanKey, StringComparison.OrdinalIgnoreCase) ||
             (normKey is "gateway" or "apigateway" && (p.Name.EndsWith("Gateway", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("ApiGateway", StringComparison.OrdinalIgnoreCase))) ||
             p.Name.Replace(".", "").Replace("-", "").EndsWith(normKey, StringComparison.OrdinalIgnoreCase)));
        if (directMatch != null) return directMatch;

        // 1. Exact domain match on non-library project
        var exactNonLib = projList.FirstOrDefault(p =>
            !p.IsLibrary &&
            (ResourceSemanticAnalyzer.CleanProjectNameToDomain(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
             EndpointScoringEngine.NormalizeAlphanumeric(Path.GetFileName(p.Path.TrimEnd('/', '\\'))).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
             EndpointScoringEngine.NormalizeAlphanumeric(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase)));
        if (exactNonLib != null) return exactNonLib;

        // 2. Exact domain match on any project
        var exactAny = projList.FirstOrDefault(p =>
            ResourceSemanticAnalyzer.CleanProjectNameToDomain(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
            EndpointScoringEngine.NormalizeAlphanumeric(Path.GetFileName(p.Path.TrimEnd('/', '\\'))).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
            EndpointScoringEngine.NormalizeAlphanumeric(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase));
        if (exactAny != null) return exactAny;

        // 3. Name ends with .Services.{key} or .Gateways.{key}
        var nameMatch = projList.FirstOrDefault(p =>
            !p.IsLibrary &&
            (p.Name.EndsWith($".Services.{key}", StringComparison.OrdinalIgnoreCase) ||
             p.Name.EndsWith($".Gateways.{key}", StringComparison.OrdinalIgnoreCase)));
        if (nameMatch != null) return nameMatch;

        // 4. EndpointScoringEngine matching (excluding workers/schedulers without exposed APIs)
        var candidates = projList.Where(p =>
        {
            var isWorker = p.Role == OntologyConstants.ProjectRoles.Worker ||
                           p.PrimaryRole is "WorkerService" or "Scheduler" or "Worker";
            return !isWorker;
        }).Select(p => new ProjectMatchCandidate(
            p.Id,
            p.Name,
            p.Path,
            p.TryGetSignals()?.GitRepo,
            p.IsLibrary)).ToList();

        var match = EndpointScoringEngine.MatchCall(cleanKey, null, null, candidates);
        if (match.IsInternal && match.Project != null && match.ProjectScore >= 50.0)
        {
            var matchedProj = projList.FirstOrDefault(p => p.Id == match.Project.ProjectId);
            if (matchedProj != null) return matchedProj;
        }

        // 6. Typo-tolerant / Levenshtein matching (e.g. 'analitics' vs 'analytics')
        if (normKey.Length >= 6)
        {
            foreach (var p in projList.Where(p => !p.IsLibrary))
            {
                var isWorker = p.Role == OntologyConstants.ProjectRoles.Worker ||
                               p.PrimaryRole is "WorkerService" or "Scheduler" or "Worker";
                if (isWorker) continue;

                var candDomain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(p.Name).ToLowerInvariant();
                var candFolder = EndpointScoringEngine.NormalizeAlphanumeric(Path.GetFileName(p.Path.TrimEnd('/', '\\'))).ToLowerInvariant();
                if ((candDomain.Length >= 6 && Math.Abs(candDomain.Length - normKey.Length) <= 1 && ComputeLevenshteinDistance(normKey, candDomain) <= 1) ||
                    (candFolder.Length >= 6 && Math.Abs(candFolder.Length - normKey.Length) <= 1 && ComputeLevenshteinDistance(normKey, candFolder) <= 1))
                {
                    return p;
                }
            }
        }

        return null;
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];
        for (var i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= t.Length; j++) d[0, j] = j;

        for (var i = 1; i <= s.Length; i++)
        {
            for (var j = 1; j <= t.Length; j++)
            {
                var cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }

    public static string ExtractDomainFromExternalServiceId(string extId)
    {
        if (extId.Contains(":es:graphql:", StringComparison.OrdinalIgnoreCase) ||
            extId.Contains(":externalservice:graphql:", StringComparison.OrdinalIgnoreCase))
        {
            return "graphql";
        }
        var lastColon = extId.LastIndexOf(':');
        if (lastColon >= 0 && lastColon < extId.Length - 1)
        {
            return extId[(lastColon + 1)..];
        }
        return extId;
    }

    public static bool IsGarbageExternalService(string domain) => WorkspaceConventions.IsGarbageExternalService(domain);

    public static List<string> IdentifyPhantomExternalServicesToPurge(ExternalServicePurgeSnapshot snapshot)
    {
        var projectList = new List<ProjectNode>();
        foreach (var (pId, pName, pPath) in snapshot.Projects)
        {
            if (!string.IsNullOrEmpty(pId))
            {
                projectList.Add(new ProjectNode(pId, pName, pPath ?? "", ""));
            }
        }

        var projectCandidates = projectList.Select(p => new ProjectMatchCandidate(p.Id, p.Name, p.Path, null, p.IsLibrary)).ToList();

        var endpointCandidates = new List<EndpointMatchCandidate>();
        foreach (var ep in snapshot.Endpoints)
        {
            var epRoute = ep.Route;
            var epMethod = ep.Method;

            if (string.IsNullOrEmpty(epRoute) && !string.IsNullOrEmpty(ep.Name) && ep.Name.Contains(':'))
            {
                var parts = ep.Name.Split(':', 2);
                if (string.IsNullOrEmpty(epMethod)) epMethod = parts[0];
                epRoute = parts[1];
            }

            if (!string.IsNullOrEmpty(ep.Id) && !string.IsNullOrEmpty(epRoute))
            {
                var ownerProj = !string.IsNullOrEmpty(ep.Path) ? Layer2ProjectParser.FindProjectForFilePath(ep.Path, projectList) : null;
                endpointCandidates.Add(new EndpointMatchCandidate(ep.Id, epRoute, epMethod, ownerProj?.Id));
            }
        }

        var servicesCallingEndpoints = snapshot.ServicesCallingEndpoints;
        var idsToDelete = new List<string>();
        var matchingProjCache = new Dictionary<string, ProjectNode?>(StringComparer.OrdinalIgnoreCase);

        foreach (var ext in snapshot.ExternalServices)
        {
            var id = ext.Id;
            var name = ext.Name;
            var domain = ext.Domain;
            var path = ext.Path;
            var inferredFromPath = ext.InferredFromPath;
            var isRelativePath = ext.IsRelativePath;
            var isExt = ext.IsExternal;
            var inferredFromIdent = ext.InferredFromIdentifier;

            var cand = !string.IsNullOrWhiteSpace(domain) ? domain : name;
            if (string.IsNullOrWhiteSpace(cand)) cand = ExtractDomainFromExternalServiceId(id);

            // Purge wildcard, query fragment, or invalid artifacts
            if (cand.StartsWith('*') ||
                cand.StartsWith('?') ||
                cand.StartsWith('&') ||
                cand.StartsWith('=') ||
                cand.Contains("api-version=") ||
                cand.Contains('=') ||
                cand.Contains('&') ||
                cand.Contains('?') ||
                (name != null && (name.StartsWith('*') || name.StartsWith('?') || name.StartsWith('&') || name.Contains("api-version=") || name.Contains('='))) ||
                (domain != null && (domain.StartsWith('*') || domain.StartsWith('?') || domain.StartsWith('&') || domain.Contains("api-version=") || domain.Contains('='))))
            {
                idsToDelete.Add(id);
                continue;
            }

            if (cand != "unknown-service" && (IsGarbageExternalService(cand) || WorkspaceConventions.IsGenericUrlStemOrVariable(cand)))
            {
                idsToDelete.Add(id);
                continue;
            }

            if (cand == "unknown-service" && !servicesCallingEndpoints.Contains(id))
            {
                idsToDelete.Add(id);
                continue;
            }

            // Check if this ExternalService matches an internal project with high confidence
            if (!matchingProjCache.TryGetValue(cand, out var matchingProj))
            {
                matchingProj = FindMatchingServiceProject(cand, projectList);
                matchingProjCache[cand] = matchingProj;
            }
            if (matchingProj != null)
            {
                idsToDelete.Add(id);
                continue;
            }

            if (inferredFromIdent == "true" && !servicesCallingEndpoints.Contains(id))
            {
                idsToDelete.Add(id);
                continue;
            }

            if (isExt is "true" or "1") continue;

            var isRelative = inferredFromPath == "true" ||
                             isRelativePath == "true" ||
                             cand.StartsWith('/') ||
                             (!string.IsNullOrEmpty(path) && path.StartsWith('/') &&
                              (cand.Equals(path.Trim('/').Split('/', 2)[0], StringComparison.OrdinalIgnoreCase) || cand == "unknown-service"));

            if (isRelative)
            {
                if (servicesCallingEndpoints.Contains(id))
                {
                    idsToDelete.Add(id);
                    continue;
                }

                if (endpointCandidates.Count > 0)
                {
                    var effectivePath = !string.IsNullOrEmpty(path) && path != "/" ? path : (cand.StartsWith('/') ? cand : null);
                    if (!string.IsNullOrEmpty(effectivePath))
                    {
                        var match = EndpointScoringEngine.MatchCall(effectivePath, null, null, projectCandidates, endpointCandidates);
                        if (match.IsInternal && match.EndpointScore >= 50.0)
                        {
                            idsToDelete.Add(id);
                            continue;
                        }
                    }
                }
            }
        }

        return idsToDelete;
    }

    public static async Task PurgePhantomExternalServicesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await db.LoadExternalServicePurgeSnapshotAsync(cancellationToken);
        var idsToDelete = IdentifyPhantomExternalServicesToPurge(snapshot);
        await db.DeleteNodesAndConnectedEdgesAsync(idsToDelete, cancellationToken);
    }

    public static List<string> IdentifyPlaceholderTopicsToPurge(IReadOnlyList<(string Id, string Name)> topicsRows)
    {
        var placeholderIds = new List<string>();
        var concreteExternalTopicCount = 0;

        foreach (var (id, name) in topicsRows)
        {
            if (string.IsNullOrEmpty(id)) continue;

            if (id.Contains(":mediatr:", StringComparison.OrdinalIgnoreCase) || id.Contains(":in-memory:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lowerName = (name ?? "").ToLowerInvariant();
            var isPlaceholder = lowerName is "eventbus" or "event-bus" or "rabbitmq"
                || id.Contains(":top:rabbitmq:eventbus", StringComparison.OrdinalIgnoreCase)
                || id.Contains(":top:eventbus:eventbus", StringComparison.OrdinalIgnoreCase);

            if (isPlaceholder)
            {
                placeholderIds.Add(id);
            }
            else
            {
                concreteExternalTopicCount++;
            }
        }

        return concreteExternalTopicCount > 0 ? placeholderIds : [];
    }

    public static async Task PurgePlaceholderTopicsAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var topicsRows = await db.LoadTopicsForPurgeAsync(cancellationToken);
        var placeholderIds = IdentifyPlaceholderTopicsToPurge(topicsRows);
        await db.DeleteNodesAndConnectedEdgesAsync(placeholderIds, cancellationToken);
    }

    public Task DeduplicateProjectNodesAsync(string widPrefix = "", CancellationToken cancellationToken = default)
        => DeduplicateProjectNodesAsync(db, widPrefix, cancellationToken);

    public static void DeduplicateProjectNodes(List<Node> nodes, List<Relationship> relationships)
    {
        var canonicalPaths = new HashSet<string>(
            nodes.Where(n => n.NodeKind == NodeKind.Project && n.Id.Contains(":p:"))
                 .Select(n => n.Properties.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "")
                 .Where(p => !string.IsNullOrEmpty(p))
        );

        var removedIds = new HashSet<string>(
            nodes.Where(n => n.NodeKind == NodeKind.Project && (n.Id.Contains(":project:") || n.Id.EndsWith(":project"))
                             && n.Properties.TryGetValue("path", out var p) && canonicalPaths.Contains(p?.ToString() ?? ""))
                 .Select(n => n.Id)
        );

        if (removedIds.Count > 0)
        {
            nodes.RemoveAll(n => removedIds.Contains(n.Id));
            relationships.RemoveAll(r => removedIds.Contains(r.From) || removedIds.Contains(r.To));
        }
    }

    public static Task DeduplicateProjectNodesAsync(
        IGraphClient db,
        string widPrefix = "",
        CancellationToken cancellationToken = default)
        => db.DeduplicateLegacyProjectNodesAsync(widPrefix, cancellationToken);
}
