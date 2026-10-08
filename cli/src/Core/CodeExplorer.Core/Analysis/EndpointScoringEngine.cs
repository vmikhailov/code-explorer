using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

public record ProjectMatchCandidate(
    string ProjectId,
    string ProjectName,
    string? Path = null,
    string? GitRepo = null,
    bool IsLibrary = false);

public record EndpointMatchCandidate(
    string EndpointId,
    string RouteTemplate,
    string? HttpMethod = null,
    string? ParentProjectId = null);

public record MatchResult(
    ProjectMatchCandidate? Project,
    EndpointMatchCandidate? Endpoint,
    double ProjectScore,
    double EndpointScore,
    bool IsInternal);

/// <summary>
/// Scores and matches service invocations (hosts, URLs, routes) against internal repository projects and exposed endpoints.
/// Uses tokenization, normalized edit distance (Levenshtein), and route template similarity.
/// </summary>
public static partial class EndpointScoringEngine
{
    private static readonly HashSet<string> PublicThirdPartyTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "com", "org", "net", "io", "dev", "ai", "app", "pro", "ru", "biz", "workers.dev", "pages.dev"
    };

    private static readonly HashSet<string> GenericHostPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "*", "unknown-service", "httprequest", "pageurl", "string", "undefined", "null", "localhost", "127.0.0.1", "0.0.0.0",
        "127.0.0.1:*", "localhost:*",
        "prod", "production", "stage", "staging", "dev", "development", "test", "testing", "local", "environment", "env",
        "url", "http-call", "v1", "v2", "event", "response", "service", "api", "request", "worker", "workers"
    };

    private static readonly string[] CommonPrefixes =
    [
        "internal-service-",
        "internal_service_",
        "integration-service-",
        "integration_service_",
        "backend-service-",
        "backend_service_",
        "core-service-",
        "core_service_",
        "service-",
        "service_",
        "svc-",
        "svc_",
        "app-",
        "app_",
        "id-",
        "id_"
    ];

    private static readonly string[] CommonSuffixes =
    [
        "-service",
        "_service",
        "service",
        "-svc",
        "_svc",
        "svc",
        "-app",
        "_app",
        "-cf-worker",
        "_cf_worker",
        "-worker",
        "_worker",
        "-workers",
        "_workers"
    ];

    [GeneratedRegex(@"^(https?://|grpc://)", RegexOptions.IgnoreCase)]
    private static partial Regex ProtocolRegex();

    [GeneratedRegex(@"^(environment\.|env\.|config\.|base_url_)", RegexOptions.IgnoreCase)]
    private static partial Regex EnvPrefixRegex();

    [GeneratedRegex(@"^(:[a-zA-Z0-9_]+|\{[a-zA-Z0-9_]+\})$")]
    private static partial Regex RouteParamRegex();

    [GeneratedRegex(@"[-_.]")]
    private static partial Regex DelimiterRegex();

    /// <summary>
    /// Calculates matching confidence between a call target and a set of internal projects and endpoints.
    /// </summary>
    public static MatchResult MatchCall(
        string? hostOrUrl,
        string? route = null,
        string? httpMethod = null,
        IEnumerable<ProjectMatchCandidate>? projects = null,
        IEnumerable<EndpointMatchCandidate>? endpoints = null)
    {
        var (cleanHost, pathFromHost) = ExtractHostAndPath(hostOrUrl);
        var effectiveRoute = !string.IsNullOrEmpty(route) ? route : pathFromHost;

        ProjectMatchCandidate? bestProject = null;
        double bestProjScore = 0.0;

        if (projects != null && !string.IsNullOrWhiteSpace(cleanHost) && !GenericHostPlaceholders.Contains(cleanHost))
        {
            foreach (var proj in projects)
            {
                var score = ScoreProjectMatch(cleanHost, proj);
                if (score > bestProjScore)
                {
                    bestProjScore = score;
                    bestProject = proj;
                }
            }
        }

        EndpointMatchCandidate? bestEndpoint = null;
        double bestEpScore = 0.0;

        if (endpoints != null && !string.IsNullOrWhiteSpace(effectiveRoute))
        {
            foreach (var ep in endpoints)
            {
                // If a project is matched with high confidence, prefer endpoints of that project
                var projectBias = (bestProject != null && ep.ParentProjectId == bestProject.ProjectId) ? 20.0 : 0.0;
                var score = ScoreEndpointMatch(effectiveRoute, httpMethod, ep) + projectBias;
                if (score > bestEpScore)
                {
                    bestEpScore = score;
                    bestEndpoint = ep;
                }
            }
        }

        // If an endpoint matched with high confidence, infer the owning project if not already resolved
        if (bestProject == null && bestEndpoint != null && projects != null && bestEpScore >= 50.0)
        {
            bestProject = projects.FirstOrDefault(p => p.ProjectId == bestEndpoint.ParentProjectId);
            if (bestProject != null && bestProjScore == 0.0)
            {
                bestProjScore = Math.Min(100.0, bestEpScore);
            }
        }

        // Confidence threshold: Score >= 40 indicates high confidence internal project match
        var isInternal = bestProjScore >= 40.0 || bestEpScore >= 50.0;

        return new MatchResult(bestProject, bestEndpoint, bestProjScore, bestEpScore, isInternal);
    }

    /// <summary>
    /// Checks whether the target URL or path is a relative path or local route without remote host/scheme.
    /// </summary>
    public static bool IsRelativeOrInternalPath(string? hostOrUrl)
    {
        if (string.IsNullOrWhiteSpace(hostOrUrl)) return false;
        var s = hostOrUrl.Trim();
        return s.StartsWith('/') && !s.StartsWith("//");
    }

    /// <summary>
    /// Determines whether a call or candidate service matches an internal endpoint with confidence >= 50.0.
    /// </summary>
    public static bool IsInternalEndpointMatch(
        string? pathOrRoute,
        string? httpMethod,
        IEnumerable<EndpointMatchCandidate> endpoints,
        out EndpointMatchCandidate? matchedEndpoint,
        out double score)
    {
        matchedEndpoint = null;
        score = 0.0;
        if (string.IsNullOrWhiteSpace(pathOrRoute) || endpoints == null) return false;

        foreach (var ep in endpoints)
        {
            var s = ScoreEndpointMatch(pathOrRoute, httpMethod, ep);
            if (s > score)
            {
                score = s;
                matchedEndpoint = ep;
            }
        }

        return score >= 50.0;
    }

    /// <summary>
    /// Scores candidate project against target host string.
    /// </summary>
    public static double ScoreProjectMatch(string host, ProjectMatchCandidate project)
    {
        if (string.IsNullOrWhiteSpace(host)) return 0.0;

        var cleanHost = EnvPrefixRegex().Replace(host.Trim().ToLowerInvariant(), "");
        if (GenericHostPlaceholders.Contains(cleanHost)) return 0.0;
        if (cleanHost.StartsWith('/')) return 0.0;

        var hostSegments = cleanHost.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var primaryHostToken = hostSegments.Length > 0 ? hostSegments[0] : cleanHost;
        if (GenericHostPlaceholders.Contains(primaryHostToken)) return 0.0;

        // If target host ends with a public TLD (e.g. stripe.com, app.workers.dev)
        var isPublicTld = hostSegments.Length > 1 &&
            (PublicThirdPartyTlds.Contains(hostSegments[^1]) ||
             PublicThirdPartyTlds.Any(tld => cleanHost.EndsWith("." + tld, StringComparison.OrdinalIgnoreCase)));

        var pName = project.ProjectName.Trim().ToLowerInvariant();
        var pFolder = !string.IsNullOrEmpty(project.Path)
            ? System.IO.Path.GetFileName(project.Path.TrimEnd('/', '\\')).ToLowerInvariant()
            : "";
        var gitRepo = !string.IsNullOrEmpty(project.GitRepo) ? project.GitRepo.Trim().ToLowerInvariant() : "";

        var normHost = NormalizeAlphanumeric(primaryHostToken);
        var normHostPluralTrimmed = normHost.TrimEnd('s');

        var candidates = new List<string> { pName, pFolder };
        if (!string.IsNullOrEmpty(gitRepo)) candidates.Add(gitRepo);

        // Also add stripped prefix/suffix variants
        foreach (var c in new[] { pName, pFolder })
        {
            var stripped = StripCommonAffixes(c);
            if (!string.IsNullOrEmpty(stripped) && !candidates.Contains(stripped))
            {
                candidates.Add(stripped);
            }
        }

        double maxScore = 0.0;

        foreach (var cand in candidates)
        {
            if (string.IsNullOrEmpty(cand)) continue;

            var normCand = NormalizeAlphanumeric(cand);
            var normCandPluralTrimmed = normCand.TrimEnd('s');

            // 1. Exact match on raw string
            if (cand == primaryHostToken || cand == cleanHost)
            {
                maxScore = Math.Max(maxScore, 100.0);
                continue;
            }

            // 2. Exact match on normalized alphanumeric
            if (normCand == normHost || normCandPluralTrimmed == normHostPluralTrimmed)
            {
                maxScore = Math.Max(maxScore, 90.0);
                continue;
            }

            // 3. Substring containment with token boundary verification (e.g. "traffic-types" in "internal-service-traffic-types")
            // Short host tokens (< 4 chars) MUST NOT match via loose substring containment (e.g. "rtb", "app", "api", "cf")
            if (normHost.Length >= 4 && (normCand.Contains(normHost) || (normCand.Length >= 4 && normHost.Contains(normCand))))
            {
                // Verify that the substring match aligns with a word boundary in the delimiter-separated original tokens
                // e.g. "traffic-types" inside "internal-service-traffic-types" matches full tokens, but "cf-worker" inside "adhub-cf-worker" is an affix of another project
                var hostTokens = primaryHostToken.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
                var candTokens = cand.ToLowerInvariant().Split(new[] { '-', '_', '.', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                bool hasTokenMatch = false;
                if (normHost.Length <= normCand.Length)
                {
                    // normHost is inside cand: cand should contain normHost or cand tokens should equal host tokens
                    hasTokenMatch = candTokens.Contains(primaryHostToken) ||
                                    candTokens.Any(t => NormalizeAlphanumeric(t) == normHost) ||
                                    normCand.StartsWith(normHost) || normCand.EndsWith(normHost);
                }
                else
                {
                    // normCand is inside host: host tokens should contain cand or match candidate token boundary
                    hasTokenMatch = hostTokens.Contains(cand.ToLowerInvariant()) ||
                                    hostTokens.Any(t => NormalizeAlphanumeric(t) == normCand) ||
                                    normHost.StartsWith(normCand) || normHost.EndsWith(normCand);
                }

                if (hasTokenMatch)
                {
                    var overlapRatio = (double)Math.Min(normCand.Length, normHost.Length) / Math.Max(normCand.Length, normHost.Length);
                    if (overlapRatio >= 0.5)
                    {
                        var subScore = 50.0 + (overlapRatio * 30.0);
                        maxScore = Math.Max(maxScore, subScore);
                        continue;
                    }
                }
            }

            // 4. Normalized Levenshtein distance on clean tokens
            // Disallow fuzzy Levenshtein for short tokens (< 4 chars)
            if (normHost.Length >= 4 && normCand.Length >= 4)
            {
                var dist = LevenshteinDistance(normHost, normCand);
                var maxLen = Math.Max(normHost.Length, normCand.Length);
                if (maxLen > 0)
                {
                    var similarity = 1.0 - ((double)dist / maxLen);
                    if (similarity >= 0.75)
                    {
                        var levScore = 40.0 + (similarity * 40.0);
                        maxScore = Math.Max(maxScore, levScore);
                    }
                }
            }
        }

        // Penalty for public domains when confidence is weak or only matched partially
        if (isPublicTld && maxScore < 80.0)
        {
            maxScore -= 50.0;
        }

        // Library penalty: standalone executable services should take precedence over class libraries
        if (project.IsLibrary)
        {
            maxScore -= 15.0;
        }

        return Math.Max(0.0, maxScore);
    }

    /// <summary>
    /// Scores candidate endpoint against route and HTTP method.
    /// </summary>
    public static double ScoreEndpointMatch(string route, string? httpMethod, EndpointMatchCandidate endpoint)
    {
        if (string.IsNullOrWhiteSpace(route) || string.IsNullOrWhiteSpace(endpoint.RouteTemplate))
            return 0.0;

        var cleanCallRoute = route.Trim().Split('?')[0].Trim('/');
        var cleanEpRoute = endpoint.RouteTemplate.Trim().Split('?')[0].Trim('/');

        var callSegs = cleanCallRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var epSegs = cleanEpRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (callSegs.Length == 0 || epSegs.Length == 0) return 0.0;

        // Compare segment by segment
        int matchCount = 0;
        int minLen = Math.Min(callSegs.Length, epSegs.Length);

        for (int i = 0; i < minLen; i++)
        {
            var cSeg = callSegs[i];
            var eSeg = epSegs[i];

            if (RouteParamRegex().IsMatch(eSeg) || RouteParamRegex().IsMatch(cSeg))
            {
                // Parameter wildcard match
                matchCount++;
            }
            else if (string.Equals(cSeg, eSeg, StringComparison.OrdinalIgnoreCase) ||
                     NormalizeAlphanumeric(cSeg) == NormalizeAlphanumeric(eSeg))
            {
                matchCount++;
            }
        }

        var jaccard = (double)matchCount / (callSegs.Length + epSegs.Length - matchCount);
        var score = jaccard * 60.0;

        if (cleanCallRoute.Equals(cleanEpRoute, StringComparison.OrdinalIgnoreCase))
        {
            score = 70.0;
        }

        // HTTP method match bonus
        if (!string.IsNullOrEmpty(httpMethod) && !string.IsNullOrEmpty(endpoint.HttpMethod))
        {
            if (string.Equals(httpMethod, endpoint.HttpMethod, StringComparison.OrdinalIgnoreCase))
            {
                score += 20.0;
            }
            else
            {
                score -= 20.0;
            }
        }

        return Math.Max(0.0, score);
    }

    public static (string CleanHost, string? Path) ExtractHostAndPath(string? hostOrUrl)
    {
        if (string.IsNullOrWhiteSpace(hostOrUrl)) return (string.Empty, null);

        var s = hostOrUrl.Trim();
        s = ProtocolRegex().Replace(s, "");

        var slashIdx = s.IndexOf('/');
        string host;
        string? path = null;

        if (slashIdx >= 0)
        {
            host = s[..slashIdx];
            path = s[slashIdx..];
        }
        else
        {
            host = s;
        }

        var colonIdx = host.IndexOf(':');
        if (colonIdx >= 0)
        {
            host = host[..colonIdx];
        }

        return (host.Trim(), path);
    }

    public static string NormalizeAlphanumeric(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return DelimiterRegex().Replace(s.ToLowerInvariant(), "");
    }

    public static string StripCommonAffixes(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var lower = name.ToLowerInvariant();
        foreach (var pre in CommonPrefixes)
        {
            if (lower.StartsWith(pre))
            {
                lower = lower[pre.Length..];
                break;
            }
        }

        foreach (var suf in CommonSuffixes)
        {
            if (lower.EndsWith(suf))
            {
                lower = lower[..^suf.Length];
                break;
            }
        }

        return lower.Trim('-', '_');
    }

    public static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
