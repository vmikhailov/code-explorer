using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private static readonly HashSet<string> AllowedApiPrefixSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "v1", "v2", "v3", "v4", "v5", "v6", "rest", "internal", "public", "private"
    };

    private static readonly System.Text.RegularExpressions.Regex ServiceDomainEnvRegex = new(
        @"^(environment\.|env\.|config\.|base_url_)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex ServiceSuffixRegex = new(
        @"(_service|service)$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string CleanServiceDomain(string serviceDomain)
    {
        return ServiceDomainEnvRegex.Replace(serviceDomain, "")
            .Replace("-", "").Replace("_", "").ToLowerInvariant();
    }

    private static bool IsApiPrefixOnly(string[] segs, string? serviceDomain = null)
    {
        if (segs.Length == 0) return false;
        foreach (var seg in segs)
        {
            if (AllowedApiPrefixSegments.Contains(seg)) continue;
            if (!string.IsNullOrEmpty(serviceDomain))
            {
                var cleanDomain = CleanServiceDomain(serviceDomain);
                var cleanSeg = seg.Replace("-", "").Replace("_", "").ToLowerInvariant();
                if (cleanDomain == cleanSeg || cleanDomain.TrimEnd('s') == cleanSeg.TrimEnd('s')) continue;
            }
            return false;
        }
        return true;
    }

    private static bool IsApiPrefixOnly(string prefix, string? serviceDomain = null)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return false;
        var segs = prefix.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return IsApiPrefixOnly(segs, serviceDomain);
    }

    private static readonly HashSet<string> GenericHostOrEnvNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "*", "unknown-service", "localhost", "127.0.0.1", "0.0.0.0",
        "prod", "production", "stage", "staging", "dev", "development", "test", "testing", "local", "environment", "env",
        "url", "http-call", "v1", "v2", "event", "response", "service", "api", "request", "httprequest", "pageurl", "graphql"
    };

    private static bool IsRootOrGenericPath(string? pathNorm)
    {
        if (string.IsNullOrWhiteSpace(pathNorm)) return true;
        var p = pathNorm.Trim();
        var clean = p.Trim('/');
        return p is "/" or "*" || string.IsNullOrEmpty(clean) || clean is "*";
    }

    private static bool IsGenericHostOrService(string? domainOrService)
    {
        if (string.IsNullOrWhiteSpace(domainOrService))
            return true;

        var d = domainOrService.Trim().ToLowerInvariant();
        var protoIdx = d.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0) d = d[(protoIdx + 3)..];
        var slashIdx = d.IndexOf('/');
        if (slashIdx >= 0) d = d[..slashIdx];
        var colonIdx = d.IndexOf(':');
        if (colonIdx >= 0) d = d[..colonIdx];

        return string.IsNullOrWhiteSpace(d) || GenericHostOrEnvNames.Contains(d);
    }

    private static bool IsInferredPathSegmentDomain(ExternalServiceNode extService)
    {
        if (string.IsNullOrEmpty(extService.DomainOrService) || extService.DomainOrService.Contains('.'))
            return false;

        if (string.IsNullOrEmpty(extService.Path) || !extService.Path.StartsWith('/'))
            return false;

        var firstSeg = extService.Path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.Equals(firstSeg, extService.DomainOrService, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoesProjectMatchServiceDomain(ProjectNode proj, string domainOrService)
    {
        if (string.IsNullOrWhiteSpace(domainOrService))
            return false;

        var d = domainOrService.Trim().ToLowerInvariant();
        var protoIdx = d.IndexOf("://", StringComparison.Ordinal);
        if (protoIdx >= 0) d = d[(protoIdx + 3)..];
        var slashIdx = d.IndexOf('/');
        if (slashIdx >= 0) d = d[..slashIdx];
        var colonIdx = d.IndexOf(':');
        if (colonIdx >= 0) d = d[..colonIdx];

        if (string.IsNullOrWhiteSpace(d) || GenericHostOrEnvNames.Contains(d))
            return false;

        // 1. Direct matching: check if project name, directory name, or repo matches full host or subdomain
        var pName = proj.Name.ToLowerInvariant();
        var pFolder = Path.GetFileName(proj.Path.TrimEnd('/', '\\')).ToLowerInvariant();
        var gitRepo = proj.Extensions?.TryGetValue("git_repo", out var gr) == true ? gr.ToLowerInvariant() : "";

        var cand = new ProjectMatchCandidate(proj.Id, proj.Name, proj.Path, gitRepo, proj.IsLibrary);
        var score = EndpointScoringEngine.ScoreProjectMatch(d, cand);
        if (score >= 40.0)
        {
            return true;
        }

        // Check full domain (e.g. "rule-configurator")
        if (MatchesProject(d, pName, pFolder, gitRepo))
        {
            return true;
        }

        // Check subdomain / first host segment (e.g. "rule-configurator" in "rule-configurator.at-systems.biz")
        if (d.Contains('.'))
        {
            var subdomain = d.Split('.', 2)[0];
            if (!string.IsNullOrEmpty(subdomain) && (EndpointScoringEngine.ScoreProjectMatch(subdomain, cand) >= 40.0 || MatchesProject(subdomain, pName, pFolder, gitRepo)))
            {
                return true;
            }
        }

        // Third-party external domains (e.g. google.com, telegram.org, cloudflare.com) should NEVER match internal projects
        // ONLY reject if the domain is not an internal service match tested above.
        if (d.EndsWith(".com") || d.EndsWith(".org") || d.EndsWith(".net") || d.EndsWith(".biz") ||
            d.EndsWith(".io") || d.EndsWith(".pro") || d.EndsWith(".ru") || d.EndsWith(".dev"))
        {
            return false;
        }

        var normD = WorkspaceConventions.NormalizeServiceName(d);
        var cleanDomain = ServiceDomainEnvRegex.Replace(normD, "")
            .Replace("-", "").Replace("_", "");
        cleanDomain = ServiceSuffixRegex.Replace(cleanDomain, "");

        var normP = WorkspaceConventions.NormalizeServiceName(pName);
        var cleanPName = normP.Replace("-", "").Replace("_", "");
        cleanPName = ServiceSuffixRegex.Replace(cleanPName, "");

        if (cleanDomain.Length >= 4 && (cleanDomain == cleanPName || cleanDomain.TrimEnd('s') == cleanPName.TrimEnd('s')))
            return true;

        if (pName == d || pName.Replace("-", "") == d.Replace("-", "") || normP == normD)
            return true;

        if (d.Length >= 4 && (pName.EndsWith("." + d) || pName.EndsWith("." + d + "s")))
            return true;

        // Common namespace/service prefix stripped match (e.g. id-helper-nest or id_helper_nest vs helper-nest)
        var dWithoutId = d.StartsWith("id-") || d.StartsWith("id_") ? d[3..] : null;
        if (dWithoutId != null)
        {
            var cleanDWithoutId = dWithoutId.Replace("-", "").Replace("_", "");
            if (pName == dWithoutId || normP == dWithoutId || pFolder == dWithoutId ||
                cleanPName == cleanDWithoutId || MatchesProject(dWithoutId, pName, pFolder, gitRepo))
            {
                return true;
            }
        }
        var pWithoutId = pName.StartsWith("id-") || pName.StartsWith("id_") ? pName[3..] : null;
        if (pWithoutId != null)
        {
            var cleanPWithoutId = pWithoutId.Replace("-", "").Replace("_", "");
            if (d == pWithoutId || normD == pWithoutId || cleanDomain == cleanPWithoutId || MatchesProject(pWithoutId, d, d, ""))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly HashSet<string> GenericProjectFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "src", "lib", "libs", "main", "core", "api", "client", "service"
    };

    private static bool MatchesProject(string token, string pName, string pFolder, string gitRepo)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var cleanToken = token.Replace("-", "").Replace("_", "");
        var canMatchFolder = !GenericProjectFolderNames.Contains(pFolder);

        if (pName == token || (canMatchFolder && pFolder == token) || gitRepo == token)
            return true;

        var cleanPName = pName.Replace("-", "").Replace("_", "");
        var cleanPFolder = pFolder.Replace("-", "").Replace("_", "");
        var cleanGitRepo = gitRepo.Replace("-", "").Replace("_", "");

        if (cleanPName == cleanToken || (canMatchFolder && cleanPFolder == cleanToken) || (!string.IsNullOrEmpty(cleanGitRepo) && cleanGitRepo == cleanToken))
            return true;

        if (cleanPName.TrimEnd('s') == cleanToken.TrimEnd('s') || (canMatchFolder && cleanPFolder.TrimEnd('s') == cleanToken.TrimEnd('s')))
            return true;

        if (pName.EndsWith("." + token, StringComparison.OrdinalIgnoreCase) ||
            pName.EndsWith("." + token + "s", StringComparison.OrdinalIgnoreCase) ||
            pName.EndsWith($".services.{token}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    internal static List<T> FilterAmbiguousCandidates<T>(
        PreparedExternalService extService,
        List<T> candidates,
        Func<T, ProjectNode?> getTargetProj,
        Func<T, string> getNormPath)
    {
        if (!extService.IsGenericHostOrService || candidates.Count <= 1)
            return candidates;

        if (string.Equals(extService.Service.Protocol, "graphql", StringComparison.OrdinalIgnoreCase))
        {
            var gqlExact = candidates
                .Where(e => string.Equals(extService.ServicePathNorm, getNormPath(e), StringComparison.OrdinalIgnoreCase))
                .ToList();
            return gqlExact.Count > 0 ? gqlExact : candidates;
        }

        var distinctTargetProjects = candidates
            .Select(getTargetProj)
            .Where(p => p != null)
            .DistinctBy(p => p!.Id)
            .ToList();

        if (distinctTargetProjects.Count <= 1)
            return candidates;

        if (extService.CallerProj != null && distinctTargetProjects.Any(p => p!.Id == extService.CallerProj.Id))
        {
            return candidates.Where(e => getTargetProj(e)?.Id == extService.CallerProj.Id).ToList();
        }

        var exactMatches = candidates
            .Where(e => string.Equals(extService.ServicePathNorm, getNormPath(e), StringComparison.OrdinalIgnoreCase))
            .ToList();
        var exactProjects = exactMatches
            .Select(getTargetProj)
            .Where(p => p != null)
            .DistinctBy(p => p!.Id)
            .ToList();

        if (exactProjects.Count == 1)
        {
            return exactMatches;
        }

        return [];
    }

    private static bool MatchParts(string[] partsA, string[] partsB, string? serviceDomain)
    {
        if (partsA.Length == 0 || partsB.Length == 0)
        {
            return false;
        }

        var len = Math.Min(partsA.Length, partsB.Length);
        var lastA = partsA[^1];
        var lastB = partsB[^1];
        var isParamLastA = lastA == "*" || lastA.StartsWith(':') || (lastA.StartsWith('{') && lastA.EndsWith('}'));
        var isParamLastB = lastB == "*" || lastB.StartsWith(':') || (lastB.StartsWith('{') && lastB.EndsWith('}'));

        // Fast early rejection on last segment
        if (!isParamLastA && !isParamLastB && !string.Equals(lastA, lastB, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasExactMatch = false;

        for (int i = 1; i <= len; i++)
        {
            var a = partsA[^i];
            var b = partsB[^i];

            var isParamA = a == "*" || a.StartsWith(':') || (a.StartsWith('{') && a.EndsWith('}'));
            var isParamB = b == "*" || b.StartsWith(':') || (b.StartsWith('{') && b.EndsWith('}'));

            if (isParamA && isParamB)
            {
                continue;
            }

            if (isParamA != isParamB)
            {
                if (a == "*" || b == "*") return false;
                continue;
            }

            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                var cleanA = a.Replace("-", "").Replace("_", "").ToLowerInvariant();
                var cleanB = b.Replace("-", "").Replace("_", "").ToLowerInvariant();
                var cleanAWithoutService = cleanA.EndsWith("service", StringComparison.Ordinal) ? cleanA[..^7] : cleanA;
                var cleanBWithoutService = cleanB.EndsWith("service", StringComparison.Ordinal) ? cleanB[..^7] : cleanB;
                if (cleanAWithoutService != cleanBWithoutService && cleanAWithoutService.TrimEnd('s') != cleanBWithoutService.TrimEnd('s'))
                {
                    return false;
                }
            }

            hasExactMatch = true;
        }

        if (!hasExactMatch) return false;

        var longerParts = partsA.Length > partsB.Length ? partsA : partsB;
        var prefixCount = longerParts.Length - len;
        if (len == 1 && prefixCount > 0)
        {
            for (int i = 0; i < prefixCount; i++)
            {
                var seg = longerParts[i];
                if (AllowedApiPrefixSegments.Contains(seg)) continue;
                if (!string.IsNullOrEmpty(serviceDomain))
                {
                    var cleanDomain = CleanServiceDomain(serviceDomain);
                    var cleanSeg = seg.Replace("-", "").Replace("_", "").ToLowerInvariant();
                    if (cleanDomain == cleanSeg || cleanDomain.TrimEnd('s') == cleanSeg.TrimEnd('s')) continue;
                    var cleanSegWithoutService = cleanSeg.EndsWith("service", StringComparison.Ordinal) ? cleanSeg[..^7] : cleanSeg;
                    var cleanDomainWithoutService = cleanDomain.EndsWith("service", StringComparison.Ordinal) ? cleanDomain[..^7] : cleanDomain;
                    if (cleanDomainWithoutService == cleanSegWithoutService || cleanDomainWithoutService.TrimEnd('s') == cleanSegWithoutService.TrimEnd('s')) continue;
                }
                return false;
            }
        }

        return true;
    }

    private bool MatchPaths(string pathA, string pathB) => MatchPaths(pathA, pathB, null);

    private bool MatchPaths(string pathA, string pathB, string? serviceDomain)
    {
        var partsA = pathA.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var partsB = pathB.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return MatchParts(partsA, partsB, serviceDomain);
    }

    private bool IsMatch(PreparedExternalService ext, PreparedEntryPoint ep, Func<ProjectNode, string, bool> projectMatcher)
    {
        if (string.Equals(ext.Service.Protocol, "graphql", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ep.TargetProj != null && !ext.IsPathSegment && !ext.IsGenericHostOrService)
        {
            if (!projectMatcher(ep.TargetProj, ext.Service.DomainOrService))
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(ep.EntryNorm))
        {
            return false;
        }

        if (ep.IsApiPrefixOnly)
        {
            return false;
        }

        if ((ext.RawDomain is "*" or "unknown-service" or "" || ext.IsGenericHostOrService) && ext.IsRootOrGenericPath)
        {
            return false;
        }

        if (ext.IsRootOrGenericPath)
        {
            if (ep.TargetProj == null || !projectMatcher(ep.TargetProj, ext.Service.DomainOrService ?? ""))
            {
                return false;
            }
        }

        if (string.Equals(ext.ServicePathNorm, ep.EntryNorm, StringComparison.OrdinalIgnoreCase) ||
            MatchParts(ext.Segments, ep.Segments, ext.Service.DomainOrService))
        {
            return true;
        }

        if (ext.CleanPath != "//" && ep.CleanPath != "//")
        {
            if (ep.CleanPath.EndsWith(ext.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ep.CleanPath[..^ext.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }
            else if (ext.CleanPath.EndsWith(ep.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ext.CleanPath[..^ep.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }
        }

        if (!string.IsNullOrEmpty(ext.ServiceDomainNorm) && ext.ServiceDomainNorm != "*" && ext.ServiceDomainNorm != "unknown-service")
        {
            if (string.Equals(ext.ServiceDomainNorm, ep.EntryNorm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ext.ServiceDomainNorm.StartsWith('/') && MatchPaths(ext.ServiceDomainNorm, ep.EntryNorm, ext.Service.DomainOrService))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMatch(PreparedExternalService ext, PreparedEndpoint ep, Func<ProjectNode, string, bool> projectMatcher)
    {
        var isExtGraphQL = string.Equals(ext.Service.Protocol, "graphql", StringComparison.OrdinalIgnoreCase);
        var isEpGraphQL = string.Equals(ep.Endpoint.Protocol, "GraphQL", StringComparison.OrdinalIgnoreCase);

        if (isExtGraphQL != isEpGraphQL)
        {
            return false;
        }

        if (isExtGraphQL)
        {
            if (string.IsNullOrEmpty(ext.ServicePathNorm) || string.IsNullOrEmpty(ep.RouteNorm))
            {
                return false;
            }

            if (ext.Service.Extensions != null &&
                ext.Service.Extensions.TryGetValue("operation_type", out var extOp) &&
                !string.IsNullOrEmpty(extOp) &&
                !string.IsNullOrEmpty(ep.Endpoint.OperationType) &&
                !string.Equals(extOp, ep.Endpoint.OperationType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(ext.ServicePathNorm, ep.RouteNorm, StringComparison.OrdinalIgnoreCase);
        }

        if (ep.TargetProj != null && !ext.IsPathSegment && !ext.IsGenericHostOrService)
        {
            if (!projectMatcher(ep.TargetProj, ext.Service.DomainOrService))
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(ep.RouteNorm))
        {
            return false;
        }

        if (ep.IsApiPrefixOnly)
        {
            return false;
        }

        if ((ext.RawDomain is "*" or "unknown-service" or "" || ext.IsGenericHostOrService) && ext.IsRootOrGenericPath)
        {
            return false;
        }

        if (ext.IsRootOrGenericPath)
        {
            if (ep.TargetProj == null || !projectMatcher(ep.TargetProj, ext.Service.DomainOrService ?? ""))
            {
                return false;
            }
        }

        if (string.Equals(ext.ServicePathNorm, ep.RouteNorm, StringComparison.OrdinalIgnoreCase) ||
            MatchParts(ext.Segments, ep.Segments, ext.Service.DomainOrService))
        {
            return true;
        }

        if ((string.Equals(ext.Service.Protocol, "grpc", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(ep.Endpoint.Protocol, "gRPC", StringComparison.OrdinalIgnoreCase)) &&
            ext.Segments.Length > 0 && ep.Segments.Length > 0)
        {
            var extMethod = ext.Segments[^1];
            var epMethod = ep.Segments[^1];
            if (string.Equals(extMethod, epMethod, StringComparison.OrdinalIgnoreCase))
            {
                var extServiceStem = ext.Segments.Length > 1 ? ext.Segments[^2] : ext.RawDomain;
                var epServiceStem = ep.Segments.Length > 1 ? ep.Segments[^2] : "";

                var cleanExtStem = extServiceStem.Replace("-", "").Replace("_", "").ToLowerInvariant();
                var cleanEpStem = epServiceStem.Replace("-", "").Replace("_", "").ToLowerInvariant();
                if (cleanExtStem.EndsWith("service", StringComparison.Ordinal)) cleanExtStem = cleanExtStem[..^7];
                if (cleanEpStem.EndsWith("service", StringComparison.Ordinal)) cleanEpStem = cleanEpStem[..^7];

                if (!string.IsNullOrEmpty(cleanExtStem) && !string.IsNullOrEmpty(cleanEpStem) &&
                    (cleanExtStem == cleanEpStem || cleanExtStem.TrimEnd('s') == cleanEpStem.TrimEnd('s')))
                {
                    return true;
                }

                if (string.IsNullOrEmpty(epServiceStem) && ep.TargetProj != null && projectMatcher(ep.TargetProj, ext.Service.DomainOrService ?? ""))
                {
                    return true;
                }
            }
        }

        if (ext.CleanPath != "//" && ep.CleanPath != "//")
        {
            if (ep.CleanPath.EndsWith(ext.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ep.CleanPath[..^ext.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }
            else if (ext.CleanPath.EndsWith(ep.CleanPath, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = ext.CleanPath[..^ep.CleanPath.Length].Trim('/');
                if (IsApiPrefixOnly(prefix, ext.Service.DomainOrService))
                {
                    return true;
                }
            }

            if ((ep.CleanPath.StartsWith(ext.CleanPath, StringComparison.OrdinalIgnoreCase) && ext.Segments.Length >= 2 && !ep.IsApiPrefixOnly) ||
                (ext.CleanPath.StartsWith(ep.CleanPath, StringComparison.OrdinalIgnoreCase) && ep.Segments.Length >= 2 && !ep.IsApiPrefixOnly))
            {
                return true;
            }

            if (ext.Segments.Length >= 3)
            {
                var extWithoutFirst = "/" + string.Join("/", ext.Segments.Skip(1)) + "/";
                if (ep.CleanPath.StartsWith(extWithoutFirst, StringComparison.OrdinalIgnoreCase) && !ep.IsApiPrefixOnly)
                {
                    return true;
                }
            }
        }

        if (!string.IsNullOrEmpty(ext.ServiceDomainNorm) && ext.ServiceDomainNorm != "*" && ext.ServiceDomainNorm != "unknown-service" && ext.ServiceDomainNorm.StartsWith('/'))
        {
            if (string.Equals(ext.ServiceDomainNorm, ep.RouteNorm, StringComparison.OrdinalIgnoreCase) ||
                MatchPaths(ext.ServiceDomainNorm, ep.RouteNorm, ext.Service.DomainOrService))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractFilePathFromSymbolId(string scopeSymbolId)
    {
        if (Urn.TryParse(scopeSymbolId, out var urn) && !string.IsNullOrEmpty(urn.Path))
        {
            var p = urn.Path;
            var colonIdx = p.IndexOf(':');
            if (colonIdx > 1) // preserve Windows drive letters like C:/
            {
                p = p[..colonIdx];
            }
            return p.Replace('\\', '/');
        }
        string[] markers =
        [
            $":{OntologyConstants.IdPrefixes.Symbol}:", ":symbol:", ":sym:",
            ":function:", ":fn:",
            $":{OntologyConstants.IdPrefixes.Procedure}:", ":procedure:", ":proc:",
            ":m:", ":method:", ":type:"
        ];
        foreach (var marker in markers)
        {
            var markerIdx = scopeSymbolId.IndexOf(marker, StringComparison.Ordinal);
            if (markerIdx >= 0)
            {
                var start = markerIdx + marker.Length;
                var end = scopeSymbolId.IndexOf(':', start);
                if (end > start)
                {
                    return scopeSymbolId[start..end].Replace('\\', '/');
                }
            }
        }
        return null;
    }

    private static string NormalizeRelativePath(string path)
    {
        path = path.Replace('\\', '/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>();
        foreach (var p in parts)
        {
            if (p == ".") continue;
            if (p == "..")
            {
                if (stack.Count > 0 && stack[^1] != "..")
                {
                    stack.RemoveAt(stack.Count - 1);
                }
                else
                {
                    stack.Add("..");
                }
            }
            else
            {
                stack.Add(p);
            }
        }
        return string.Join("/", stack);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var normalized = path.Replace('\\', '/').ToLowerInvariant();

        var protocolIdx = normalized.IndexOf("://");

        if (protocolIdx != -1)
        {
            normalized = normalized[(protocolIdx + 3)..];
        }

        var colonIdx = normalized.IndexOf(':');
        var slashIdx = normalized.IndexOf('/');
        if (colonIdx != -1 && (slashIdx == -1 || colonIdx < slashIdx))
        {
            var prefix = normalized[..colonIdx];
            if (prefix is "get" or "post" or "put" or "delete" or "patch" or "options" or "head" or "all" or "ws" or "rpc" or "grpc" or "graphql")
            {
                normalized = normalized[(colonIdx + 1)..];
            }
        }

        return normalized.Trim('/');
    }
}
