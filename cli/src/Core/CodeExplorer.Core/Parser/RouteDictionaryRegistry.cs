using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

public static class RouteDictionaryRegistry
{
    private static readonly ConcurrentDictionary<string, (string Path, string? Service)> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> _serviceDomains =
        new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterServiceDomain(string key, string domain)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(domain))
        {
            _serviceDomains[key] = domain;
        }
    }

    public static void Register(string key, string path, string? service)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(path))
        {
            _routes[key] = (path, service);
            if (!string.IsNullOrEmpty(service))
            {
                RegisterServiceDomain(service, service);
            }
        }
    }

    public static bool TryResolve(string key, out string path, out string? service)
    {
        if (_routes.TryGetValue(key, out var tuple))
        {
            path = tuple.Path;
            service = tuple.Service;
            return true;
        }
        path = null!;
        service = null;
        return false;
    }

    public static IReadOnlyCollection<string> GetAllKnownServices() => _serviceDomains.Values.ToList();

    public static void Clear()
    {
        _routes.Clear();
        _serviceDomains.Clear();
    }

    public static string NormalizeResolvedUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var routeMatch = Regex.Match(raw, @"getServiceDomainByRoute\s*\(\s*['""]([^'""]+)['""]");
        if (routeMatch.Success)
        {
            var routeKey = routeMatch.Groups[1].Value;
            if (TryResolve(routeKey, out var rPath, out var rService))
            {
                var cleanPath = rPath.Split('?')[0];
                cleanPath = Regex.Replace(cleanPath, @"\{[a-zA-Z0-9_]+\}", "*");
                return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
            }
        }

        // Replace template placeholders: ${...}, {var}, and format specifiers: %s, %d, %v, {0}
        var clean = Regex.Replace(raw, @"\$\{[^}]+\}", "*");
        clean = Regex.Replace(clean, @"\{[a-zA-Z0-9_]+\}", "*");
        clean = Regex.Replace(clean, @"%[sdvf]", "*");
        clean = clean.Trim('\'', '"', '`', ' ');

        // Check if route registry has this key directly (e.g. SINGLE_STAGE, BUY_DOMAIN, NETWORKS)
        if (TryResolve(clean, out var rp, out var rs))
        {
            var cleanPath = rp.Split('?')[0];
            cleanPath = Regex.Replace(cleanPath, @"\{[a-zA-Z0-9_]+\}", "*");
            return !string.IsNullOrEmpty(rs) ? $"{rs}{cleanPath}" : cleanPath;
        }

        if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return clean;
        }

        // If it starts with '*/' (e.g. ${baseUrl}/api/v1/foo -> */api/v1/foo)
        if (clean.StartsWith("*/"))
        {
            return clean;
        }

        // If it contains a slash, extract the path part starting with '/' unless prefix is known service domain
        var slashIdx = clean.IndexOf('/');
        if (slashIdx > 0)
        {
            var prefix = clean[..slashIdx];
            if (prefix.Contains('-') || prefix.Contains('_') ||
                prefix.EndsWith("service", StringComparison.OrdinalIgnoreCase) ||
                prefix.EndsWith("client", StringComparison.OrdinalIgnoreCase) ||
                GetAllKnownServices().Any(s => string.Equals(s, prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return clean;
            }
            return clean[slashIdx..];
        }

        return clean;
    }

    public static void ScanAndRegister(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // 1. Extract SERVICE_DOMAINS / SERVICES mapping: KEY: 'service-name' or 'http://service-name'
        var serviceDomainsMatches = Regex.Matches(content, @"(?:SERVICE_DOMAINS|SERVICES|Services)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?;", RegexOptions.Multiline);
        foreach (Match sm in serviceDomainsMatches)
        {
            var body = sm.Groups[1].Value;
            var domainMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*['""]([^'""]+)['""]");
            foreach (Match dm in domainMatches)
            {
                var sKey = dm.Groups[1].Value;
                var sVal = dm.Groups[2].Value.Trim();
                if (sVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(sVal);
                        sVal = uri.Host;
                    }
                    catch
                    {
                        sVal = sVal.Replace("http://", "").Replace("https://", "").Trim('/');
                    }
                }
                _serviceDomains[sKey] = sVal;
            }
        }

        // JSON format for Services: "Services": { "Bundle": "http://bundle-service", ... }
        var jsonServicesMatch = Regex.Match(content, @"""Services""\s*:\s*\{([\s\S]*?)\}", RegexOptions.Multiline);
        if (jsonServicesMatch.Success)
        {
            var body = jsonServicesMatch.Groups[1].Value;
            var domainMatches = Regex.Matches(body, @"""([A-Za-z0-9_\-]+)""\s*:\s*""([^""]+)""");
            foreach (Match dm in domainMatches)
            {
                var sKey = dm.Groups[1].Value;
                var sVal = dm.Groups[2].Value.Trim();
                if (sVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(sVal);
                        sVal = uri.Host;
                    }
                    catch
                    {
                        sVal = sVal.Replace("http://", "").Replace("https://", "").Trim('/');
                    }
                }
                _serviceDomains[sKey] = sVal;
            }
        }

        // 2. Extract API_ROUTES / ROUTES blocks: SERVICE_KEY: [ { route: 'KEY', path: 'PATH' } ]
        var apiRoutesMatch = Regex.Match(content, @"(?:API_ROUTES|ROUTES)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?;", RegexOptions.Multiline);
        if (apiRoutesMatch.Success)
        {
            var body = apiRoutesMatch.Groups[1].Value;
            var serviceBlocks = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*\[([\s\S]*?)\]", RegexOptions.Multiline);
            foreach (Match sb in serviceBlocks)
            {
                var sKey = sb.Groups[1].Value;
                var innerRoutes = sb.Groups[2].Value;
                _serviceDomains.TryGetValue(sKey, out var domain);

                var routeMatches = Regex.Matches(innerRoutes, @"(?:route\s*:\s*['""]([^'""]+)['""],\s*path\s*:\s*['""]([^'""]+)['""]|path\s*:\s*['""]([^'""]+)['""],\s*route\s*:\s*['""]([^'""]+)['""])");
                foreach (Match rm in routeMatches)
                {
                    var rKey = rm.Groups[1].Success ? rm.Groups[1].Value : rm.Groups[4].Value;
                    var rPath = rm.Groups[1].Success ? rm.Groups[2].Value : rm.Groups[3].Value;
                    Register(rKey, rPath, domain);
                }
            }

            // Also simple dictionary mapping in ROUTES / API_ROUTES: 'GET_ORDERS': '/api/v1/orders'
            var simpleDictMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*['""]([^'""]+)['""]");
            foreach (Match sm in simpleDictMatches)
            {
                var rKey = sm.Groups[1].Value;
                var rPath = sm.Groups[2].Value;
                if (rPath.Contains('/'))
                {
                    Register(rKey, rPath, null);
                }
            }
        }

        // 3. Fallback: Standalone route definitions: { route: 'KEY', path: 'PATH' }
        var standaloneMatches = Regex.Matches(content, @"(?:route\s*:\s*['""]([^'""]+)['""],\s*path\s*:\s*['""]([^'""]+)['""]|path\s*:\s*['""]([^'""]+)['""],\s*route\s*:\s*['""]([^'""]+)['""])");
        foreach (Match m in standaloneMatches)
        {
            var rKey = m.Groups[1].Success ? m.Groups[1].Value : rmKey(m);
            var rPath = m.Groups[1].Success ? m.Groups[2].Value : rmPath(m);
            if (!_routes.ContainsKey(rKey))
            {
                Register(rKey, rPath, null);
            }
        }

        // 4. Constants across languages (Python, Go, C#, Java):
        // e.g. BUNDLE_ENDPOINT = "/api/v1/bundles" or const BundleEndpoint = "/api/v1/bundles"
        var constMatches = Regex.Matches(content, @"(?:const\s+|readonly\s+|final\s+)?(?:string\s+)?([A-Za-z0-9_]+(?:Endpoint|Route|Url|Path|URL|_ENDPOINT|_ROUTE|_URL|_PATH))\s*=\s*['""]([^'""]+)['""]");
        foreach (Match cm in constMatches)
        {
            var rKey = cm.Groups[1].Value;
            var rVal = cm.Groups[2].Value;
            if (rVal.Contains('/'))
            {
                Register(rKey, rVal, null);
            }
        }

        // 5. Java / Spring properties: services.bundle.url=http://bundle-service
        var propMatches = Regex.Matches(content, @"services\.([a-zA-Z0-9_\-]+)\.url\s*[:=]\s*([^\r\n]+)");
        foreach (Match pm in propMatches)
        {
            var sKey = pm.Groups[1].Value;
            var sVal = pm.Groups[2].Value.Trim();
            if (sVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri(sVal);
                    sVal = uri.Host;
                }
                catch { }
            }
            _serviceDomains[sKey] = sVal;
        }
    }

    private static string rmKey(Match m) => m.Groups[4].Value;
    private static string rmPath(Match m) => m.Groups[3].Value;
}
