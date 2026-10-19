using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
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

    public static async Task<int> NormalizeEdgesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        // 1. Query architectural/macro nodes to determine kind and whether project is library
        var nodeMap = new Dictionary<string, (string Kind, bool IsLibrary)>(StringComparer.OrdinalIgnoreCase);
        var nodesJson = await db.ExecuteQueryAsync(
            "MATCH (n) WHERE n.kind IN ['Project', 'Service', 'App', 'Worker', 'Library', 'Package', 'Database', 'Topic', 'Endpoint', 'EntryPoint', 'ExternalService', 'CloudService'] RETURN n.id AS id, labels(n) AS lbl, n.is_library AS is_lib, n.role AS role",
            null, cancellationToken);
        using (var nodesDoc = JsonDocument.Parse(nodesJson))
        {
            foreach (var row in nodesDoc.RootElement.EnumerateArray())
            {
                var id = GetStringProp(row, "id");
                if (string.IsNullOrEmpty(id)) continue;
                var kind = "Node";
                if (row.TryGetProperty("lbl", out var lblProp) && lblProp.ValueKind == JsonValueKind.Array)
                {
                    kind = lblProp.EnumerateArray().FirstOrDefault().GetString() ?? "Node";
                }
                var isLib = kind is "Package" or "Library";
                if (row.TryGetProperty("is_lib", out var il))
                {
                    isLib = il.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.String => string.Equals(il.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                        JsonValueKind.Number => il.GetInt32() == 1,
                        _ => isLib
                    };
                }
                var role = GetStringProp(row, "role");
                if (role is "SharedLibrary" or "Test") isLib = true;

                nodeMap[id] = (kind, isLib);
            }
        }

        // 2. Query architectural/macro edges
        var edgeQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'SERVICE_CALL', 'INTEGRATES_WITH', 'CALLS_ENDPOINT', 'PUBLISHES_TO', 'TRIGGERS', 'SUBSCRIBES_TO', 'LIBRARY'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties";
        var edgesJson = await db.ExecuteQueryAsync(edgeQuery, null, cancellationToken);

        using (var edgesDoc = JsonDocument.Parse(edgesJson))
        {
            var updatedCount = 0;
            var updates = new List<(string NormalizedKind, string PropsJson, string Src, string Tgt, string RawKind)>();

            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                var rawKind = GetStringProp(row, "kind");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                nodeMap.TryGetValue(src, out var srcInfo);
                nodeMap.TryGetValue(tgt, out var tgtInfo);

                if (string.IsNullOrEmpty(tgtInfo.Kind))
                {
                    if (tgt.Contains(":pkg:") || tgt.Contains(":package:"))
                    {
                        tgtInfo = ("Package", true);
                    }
                    else if (tgt.Contains(":res:db:") || tgt.Contains(":db:") || tgt.Contains(":database:"))
                    {
                        tgtInfo = ("Database", false);
                    }
                    else if (tgt.Contains(":top:") || tgt.Contains(":topic:"))
                    {
                        tgtInfo = ("Topic", false);
                    }
                }

                var existingCategory = "";
                var existingDepType = "";
                var props = new Dictionary<string, object>();

                if (row.TryGetProperty("properties", out var pElem))
                {
                    if (pElem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in pElem.EnumerateObject())
                        {
                            var valStr = prop.Value.ToString();
                            props[prop.Name] = valStr;
                            if (prop.NameEquals("category")) existingCategory = valStr;
                            if (prop.NameEquals("dependency_type")) existingDepType = valStr;
                        }
                    }
                    else if (pElem.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(pElem.GetString()))
                    {
                        try
                        {
                            using var innerDoc = JsonDocument.Parse(pElem.GetString()!);

                            if (innerDoc.RootElement.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in innerDoc.RootElement.EnumerateObject())
                                {
                                    var valStr = prop.Value.ToString();
                                    props[prop.Name] = valStr;
                                    if (prop.NameEquals("category")) existingCategory = valStr;
                                    if (prop.NameEquals("dependency_type")) existingDepType = valStr;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                var (category, depType, normalizedKind) = NormalizeEdgeCategory(rawKind, srcInfo.Kind, tgtInfo.Kind,
                    existingCategory, existingDepType, tgtInfo.IsLibrary);

                var categoryStr = category.ToCategoryString();
                var needsUpdate = !props.ContainsKey("dependency_type") ||
                                  !string.Equals(props.GetValueOrDefault("dependency_type")?.ToString(), depType,
                                      StringComparison.Ordinal) ||
                                  !string.Equals(props.GetValueOrDefault("category")?.ToString(), categoryStr,
                                      StringComparison.Ordinal) || !string.Equals(rawKind, normalizedKind,
                                      StringComparison.Ordinal);

                if (needsUpdate)
                {
                    props["dependency_type"] = depType;
                    props["category"] = categoryStr;

                    var propsJson = JsonSerializer.Serialize(props);
                    updates.Add((normalizedKind, propsJson, src, tgt, rawKind));
                }
            }

            const int updateBatchSize = 100;
            for (int i = 0; i < updates.Count; i += updateBatchSize)
            {
                var chunk = updates.Skip(i).Take(updateBatchSize).ToList();
                var sb = new System.Text.StringBuilder(chunk.Count * 120);
                var paramDict = new Dictionary<string, object>();
                for (int j = 0; j < chunk.Count; j++)
                {
                    var u = chunk[j];
                    sb.AppendLine($"UPDATE OR IGNORE edges SET kind = @k{j}, properties = @p{j} WHERE from_id = @s{j} AND to_id = @t{j} AND kind = @rk{j};");
                    paramDict[$"k{j}"] = u.NormalizedKind;
                    paramDict[$"p{j}"] = u.PropsJson;
                    paramDict[$"s{j}"] = u.Src;
                    paramDict[$"t{j}"] = u.Tgt;
                    paramDict[$"rk{j}"] = u.RawKind;
                }
                await db.ExecuteWriteAsync(sb.ToString(), paramDict, cancellationToken);
                updatedCount += chunk.Count;
            }

            return updatedCount;
        }
    }

    private static string GetStringProp(JsonElement elem, string prop, string fallback = "")
    {
        return elem.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? (val.GetString() ?? fallback)
            : fallback;
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

        cleanKey = ConfigStore.StripServiceOptionsSuffixes(cleanKey);

        if (WorkspaceConventions.TryGetServiceAlias(cleanKey, out var aliased))
        {
            cleanKey = aliased.ToLowerInvariant();
        }

        if (cleanKey.StartsWith("environment.", StringComparison.OrdinalIgnoreCase))
        {
            cleanKey = cleanKey[12..];
        }
        else if (cleanKey.StartsWith("env.", StringComparison.OrdinalIgnoreCase))
        {
            cleanKey = cleanKey[4..];
        }
        else if (cleanKey.StartsWith("config.", StringComparison.OrdinalIgnoreCase))
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
            (SyntaxEnricher.CleanProjectNameToDomain(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
             EndpointScoringEngine.NormalizeAlphanumeric(Path.GetFileName(p.Path.TrimEnd('/', '\\'))).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
             EndpointScoringEngine.NormalizeAlphanumeric(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase)));
        if (exactNonLib != null) return exactNonLib;

        // 2. Exact domain match on any project
        var exactAny = projList.FirstOrDefault(p =>
            SyntaxEnricher.CleanProjectNameToDomain(p.Name).Equals(normKey, StringComparison.OrdinalIgnoreCase) ||
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
                           p.Extensions?.GetValueOrDefault("primary_role") is "WorkerService" or "Scheduler" or "Worker";
            return !isWorker;
        }).Select(p => new ProjectMatchCandidate(
            p.Id,
            p.Name,
            p.Path,
            p.Extensions?.TryGetValue("git_repo", out var gr) == true ? gr : null,
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
                               p.Extensions?.GetValueOrDefault("primary_role") is "WorkerService" or "Scheduler" or "Worker";
                if (isWorker) continue;

                var candDomain = SyntaxEnricher.CleanProjectNameToDomain(p.Name).ToLowerInvariant();
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
        var lastColon = extId.LastIndexOf(':');
        if (lastColon >= 0 && lastColon < extId.Length - 1)
        {
            return extId[(lastColon + 1)..];
        }
        return extId;
    }

    public static bool IsGarbageExternalService(string domain) => WorkspaceConventions.IsGarbageExternalService(domain);

    public static async Task PurgePhantomExternalServicesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path";
        var projsJson = await db.ExecuteQueryAsync(projQuery, null, cancellationToken);

        using var projsDoc = JsonDocument.Parse(projsJson);

        var projectList = new List<ProjectNode>();

        foreach (var row in projsDoc.RootElement.EnumerateArray())
        {
            var pId = GetStringProp(row, "id");
            var pName = GetStringProp(row, "name");

            var pPath = row.TryGetProperty("path", out var pt) && pt.ValueKind == JsonValueKind.String
                ? pt.GetString()
                : null;

            if (!string.IsNullOrEmpty(pId))
            {
                projectList.Add(new ProjectNode(pId, pName, pPath ?? "", ""));
            }
        }

        var projectCandidates = projectList.Select(p => new ProjectMatchCandidate(p.Id, p.Name, p.Path, null, p.IsLibrary)).ToList();

        var epQuery =
            "MATCH (ep:Endpoint) RETURN ep.id AS id, ep.name AS name, json_extract(ep.properties, '$.route_template') AS route, json_extract(ep.properties, '$.http_method') AS method, json_extract(ep.properties, '$.path') AS path";
        var epJson = await db.ExecuteQueryAsync(epQuery, null, cancellationToken);
        using var epDoc = JsonDocument.Parse(epJson);
        var endpointCandidates = new List<EndpointMatchCandidate>();
        foreach (var epRow in epDoc.RootElement.EnumerateArray())
        {
            var epId = GetStringProp(epRow, "id");
            var epName = GetStringProp(epRow, "name");
            var epRoute = GetStringProp(epRow, "route");
            var epMethod = GetStringProp(epRow, "method");
            var epPath = GetStringProp(epRow, "path");

            if (string.IsNullOrEmpty(epRoute) && !string.IsNullOrEmpty(epName) && epName.Contains(':'))
            {
                var parts = epName.Split(':', 2);
                epMethod ??= parts[0];
                epRoute = parts[1];
            }

            if (!string.IsNullOrEmpty(epId) && !string.IsNullOrEmpty(epRoute))
            {
                var ownerProj = !string.IsNullOrEmpty(epPath) ? Layer2ProjectParser.FindProjectForFilePath(epPath, projectList) : null;
                endpointCandidates.Add(new EndpointMatchCandidate(epId, epRoute, epMethod, ownerProj?.Id));
            }
        }

        var callsEpQuery = "MATCH (es:ExternalService)-[:CALLS_ENDPOINT]->(ep:Endpoint) RETURN es.id AS es_id";
        var callsEpJson = await db.ExecuteQueryAsync(callsEpQuery, null, cancellationToken);
        using var callsEpDoc = JsonDocument.Parse(callsEpJson);
        var servicesCallingEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edgeRow in callsEpDoc.RootElement.EnumerateArray())
        {
            var esId = GetStringProp(edgeRow, "es_id");
            if (!string.IsNullOrEmpty(esId)) servicesCallingEndpoints.Add(esId);
        }

        var extQuery =
            "MATCH (es:ExternalService) RETURN es.id AS id, es.name AS name, json_extract(es.properties, '$.domain_or_service') AS domain, json_extract(es.properties, '$.path') AS path, json_extract(es.properties, '$.inferred_from_path') AS inferred_from_path, json_extract(es.properties, '$.is_relative_path') AS is_relative_path, json_extract(es.properties, '$.is_external') AS is_ext";
        var extJson = await db.ExecuteQueryAsync(extQuery, null, cancellationToken);

        using var extDoc = JsonDocument.Parse(extJson);

        var idsToDelete = new List<string>();

        foreach (var row in extDoc.RootElement.EnumerateArray())
        {
            var id = GetStringProp(row, "id");
            var name = GetStringProp(row, "name");
            var domain = GetStringProp(row, "domain");
            var path = GetStringProp(row, "path");
            var inferredFromPath = GetStringProp(row, "inferred_from_path");
            var isRelativePath = GetStringProp(row, "is_relative_path");
            var isExt = GetStringProp(row, "is_ext");

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
            var matchingProj = FindMatchingServiceProject(cand, projectList);
            if (matchingProj != null)
            {
                idsToDelete.Add(id);
                continue;
            }

            if (isExt == "true") continue;

            // A candidate is synthesized from a relative path if explicitly flagged, starting with '/',
            // or if the candidate service name is simply the first segment of the path without an explicit network host.
            var isRelative = inferredFromPath == "true" ||
                             isRelativePath == "true" ||
                             cand.StartsWith('/') ||
                             (!string.IsNullOrEmpty(path) && path.StartsWith('/') &&
                              (cand.Equals(path.Trim('/').Split('/', 2)[0], StringComparison.OrdinalIgnoreCase) || cand == "unknown-service"));

            if (isRelative)
            {
                // If it was already late-bound to an internal endpoint, it's an internal invocation
                if (servicesCallingEndpoints.Contains(id))
                {
                    idsToDelete.Add(id);
                    continue;
                }

                // Or check via EndpointScoringEngine if the path or route matches an internal endpoint with score >= 50.0
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

        const int deleteBatchSize = 100;
        for (int i = 0; i < idsToDelete.Count; i += deleteBatchSize)
        {
            var chunk = idsToDelete.Skip(i).Take(deleteBatchSize).ToList();
            var paramDict = new Dictionary<string, object>();
            var inParams = string.Join(", ", chunk.Select((id, idx) =>
            {
                var p = $"@id{idx}";
                paramDict[$"id{idx}"] = id;
                return p;
            }));

            var sql = $"DELETE FROM edges WHERE from_id IN ({inParams}) OR to_id IN ({inParams}); DELETE FROM nodes WHERE id IN ({inParams});";
            await db.ExecuteWriteAsync(sql, paramDict, cancellationToken);
        }
    }

    public Task DeduplicateProjectNodesAsync(string widPrefix = "", CancellationToken cancellationToken = default)
        => DeduplicateProjectNodesAsync(db, widPrefix, cancellationToken);

    public static void DeduplicateProjectNodes(List<Node> nodes, List<Relationship> relationships)
    {
        var canonicalPaths = new HashSet<string>(
            nodes.Where(n => n.Kind == "Project" && n.Id.Contains(":p:"))
                 .Select(n => n.Properties.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "")
                 .Where(p => !string.IsNullOrEmpty(p))
        );

        var removedIds = new HashSet<string>(
            nodes.Where(n => n.Kind == "Project" && (n.Id.Contains(":project:") || n.Id.EndsWith(":project"))
                             && n.Properties.TryGetValue("path", out var p) && canonicalPaths.Contains(p?.ToString() ?? ""))
                 .Select(n => n.Id)
        );

        if (removedIds.Count > 0)
        {
            nodes.RemoveAll(n => removedIds.Contains(n.Id));
            relationships.RemoveAll(r => removedIds.Contains(r.From) || removedIds.Contains(r.To));
        }
    }

    public static async Task DeduplicateProjectNodesAsync(
        IGraphClient db,
        string widPrefix = "",
        CancellationToken cancellationToken = default)
    {
        // When both modern :p: project nodes and legacy :project: / :project_semantic / :project_syntax nodes exist,
        // purge the legacy duplicates and their connected edges.
        await db.ExecuteWriteAsync("""
            DELETE FROM edges WHERE from_id IN (
                SELECT p1.id FROM nodes p1
                WHERE p1.kind = 'Project' AND (p1.id LIKE '%:project:%' OR p1.id LIKE '%:project')
                AND EXISTS (
                    SELECT 1 FROM nodes p2 
                    WHERE p2.kind = 'Project' AND p2.id LIKE '%:p:%'
                    AND json_extract(p2.properties, '$.path') = json_extract(p1.properties, '$.path')
                )
            );
            DELETE FROM edges WHERE to_id IN (
                SELECT p1.id FROM nodes p1
                WHERE p1.kind = 'Project' AND (p1.id LIKE '%:project:%' OR p1.id LIKE '%:project')
                AND EXISTS (
                    SELECT 1 FROM nodes p2 
                    WHERE p2.kind = 'Project' AND p2.id LIKE '%:p:%'
                    AND json_extract(p2.properties, '$.path') = json_extract(p1.properties, '$.path')
                )
            );
            DELETE FROM nodes 
            WHERE kind = 'Project' AND (id LIKE '%:project:%' OR id LIKE '%:project')
            AND EXISTS (
                SELECT 1 FROM nodes p2 
                WHERE p2.kind = 'Project' AND p2.id LIKE '%:p:%'
                AND json_extract(p2.properties, '$.path') = json_extract(nodes.properties, '$.path')
            );
            DELETE FROM edges WHERE from_id IN (
                SELECT id FROM nodes WHERE kind IN ('ProjectSemantic', 'ProjectSyntax') AND id LIKE '%:project:%'
            );
            DELETE FROM edges WHERE to_id IN (
                SELECT id FROM nodes WHERE kind IN ('ProjectSemantic', 'ProjectSyntax') AND id LIKE '%:project:%'
            );
            DELETE FROM nodes WHERE kind IN ('ProjectSemantic', 'ProjectSyntax') AND id LIKE '%:project:%';
            DELETE FROM edges WHERE NOT EXISTS (SELECT 1 FROM nodes WHERE id = edges.from_id) OR NOT EXISTS (SELECT 1 FROM nodes WHERE id = edges.to_id);
            """, null, cancellationToken);
    }
}
