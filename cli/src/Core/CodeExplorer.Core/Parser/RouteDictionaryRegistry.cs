using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

public static class RouteDictionaryRegistry
{
    private static readonly ConcurrentDictionary<string, (string Path, string? Service)> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> _serviceDomains =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> _aliases =
        new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterServiceDomain(string key, string domain)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(domain))
        {
            _serviceDomains[key] = domain;
        }
    }

    public static void RegisterAlias(string aliasKey, string targetKey)
    {
        if (!string.IsNullOrEmpty(aliasKey) && !string.IsNullOrEmpty(targetKey))
        {
            _aliases[aliasKey] = targetKey;
            if (_routes.TryGetValue(targetKey, out var tuple))
            {
                _routes[aliasKey] = tuple;
            }
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

            foreach (var (alias, target) in _aliases)
            {
                if (string.Equals(target, key, StringComparison.OrdinalIgnoreCase))
                {
                    _routes[alias] = (path, service);
                }
            }
        }
    }

    public static bool TryResolve(string key, out string path, out string? service)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            path = null!;
            service = null;
            return false;
        }

        // 1. Direct lookup
        if (_routes.TryGetValue(key, out var tuple))
        {
            path = tuple.Path;
            service = tuple.Service;
            return true;
        }

        // 2. Alias lookup
        if (_aliases.TryGetValue(key, out var targetKey) && _routes.TryGetValue(targetKey, out tuple))
        {
            path = tuple.Path;
            service = tuple.Service;
            return true;
        }

        // 3. Strip parentheses if passed as "funcName()"
        var cleanKey = key.Trim();
        if (cleanKey.EndsWith("()"))
        {
            cleanKey = cleanKey[..^2].Trim();
            if (_routes.TryGetValue(cleanKey, out tuple) ||
                (_aliases.TryGetValue(cleanKey, out targetKey) && _routes.TryGetValue(targetKey, out tuple)))
            {
                path = tuple.Path;
                service = tuple.Service;
                return true;
            }
        }

        // 4. Heuristic inference for getter functions: e.g. getGamesListUrl, getPlayerUrl, getUsersFromIdentityUrl
        var getterMatch = Regex.Match(cleanKey, @"^get([A-Za-z0-9_]+?)(?:From[A-Za-z0-9_]+)?(?:List)?Url$", RegexOptions.IgnoreCase);
        if (getterMatch.Success)
        {
            var entityName = getterMatch.Groups[1].Value.ToLowerInvariant();
            var candidates = new[] { entityName, entityName + "s", entityName.TrimEnd('s') };
            foreach (var cand in candidates)
            {
                if (_routes.TryGetValue(cand, out tuple) ||
                    (_aliases.TryGetValue(cand, out var candTarget) && _routes.TryGetValue(candTarget, out tuple)))
                {
                    path = tuple.Path;
                    service = tuple.Service;
                    return true;
                }
            }
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
        _aliases.Clear();
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

        // 1b. Environment, URL map, and configuration dictionaries:
        // export const environment = { ... }, const urlMap = { ... }, const Endpoints = { ... }
        var envDictMatches = Regex.Matches(content,
            @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?\s*(?:;|\n|$)",
            RegexOptions.Multiline);
        foreach (Match m in envDictMatches)
        {
            var varName = m.Groups[1].Value;
            var isCandidate = Regex.IsMatch(varName,
                @"(?:env|environment|url|endpoint|route|service|api|config)",
                RegexOptions.IgnoreCase);
            if (!isCandidate) continue;

            var body = m.Groups[2].Value;
            var entryMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*['""`]([^'""`\r\n]+)['""`]");
            foreach (Match em in entryMatches)
            {
                var key = em.Groups[1].Value;
                var val = em.Groups[2].Value.Trim();

                if (val.Contains('/') || val.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || val.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Register(key, val, null);
                    Register($"{varName}.{key}", val, null);
                    if (varName.StartsWith("env", StringComparison.OrdinalIgnoreCase))
                    {
                        Register($"env.{key}", val, null);
                        Register($"environment.{key}", val, null);
                    }
                }
            }
        }

        // 1c. URL getter functions:
        // export const getGamesListUrl = () => env.games;
        // const getPlayerUrl = () => 'http://localhost:8050/api/v1/profiles';
        var getterMatches = Regex.Matches(content,
            @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*=\s*\([^)]*\)\s*=>\s*(?:['""`]([^'""`\r\n]+)['""`]|([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)|([A-Za-z0-9_]+)\(\))",
            RegexOptions.Multiline);
        foreach (Match gm in getterMatches)
        {
            var fnName = gm.Groups[1].Value;
            if (gm.Groups[2].Success)
            {
                var rawUrl = gm.Groups[2].Value.Trim();
                var tplMatch = Regex.Match(rawUrl, @"\$\{([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)\}");
                if (tplMatch.Success && TryResolve(tplMatch.Groups[2].Value, out var baseP, out _))
                {
                    rawUrl = rawUrl.Replace(tplMatch.Value, baseP);
                }

                if (rawUrl.Contains('/') || rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Register(fnName, rawUrl, null);
                }
            }
            else if (gm.Groups[3].Success && gm.Groups[4].Success)
            {
                var objName = gm.Groups[3].Value;
                var propName = gm.Groups[4].Value;

                RegisterAlias(fnName, propName);
                RegisterAlias(fnName, $"{objName}.{propName}");

                if (TryResolve(propName, out var p, out var s))
                {
                    Register(fnName, p, s);
                }
                else if (TryResolve($"{objName}.{propName}", out p, out s))
                {
                    Register(fnName, p, s);
                }
            }
        }

        // 1d. Traditional function declarations:
        // export function getGamesListUrl() { return env.games; }
        var funcDeclMatches = Regex.Matches(content,
            @"(?:export\s+)?function\s+([A-Za-z0-9_]+)\s*\([^)]*\)\s*(?::\s*[^{]+)?\s*\{\s*return\s+(?:['""`]([^'""`\r\n]+)['""`]|([A-Za-z0-9_]+)\.([A-Za-z0-9_]+));?\s*\}",
            RegexOptions.Multiline);
        foreach (Match fm in funcDeclMatches)
        {
            var fnName = fm.Groups[1].Value;
            if (fm.Groups[2].Success)
            {
                var rawUrl = fm.Groups[2].Value.Trim();
                var tplMatch = Regex.Match(rawUrl, @"\$\{([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)\}");
                if (tplMatch.Success && TryResolve(tplMatch.Groups[2].Value, out var baseP, out _))
                {
                    rawUrl = rawUrl.Replace(tplMatch.Value, baseP);
                }

                if (rawUrl.Contains('/') || rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Register(fnName, rawUrl, null);
                }
            }
            else if (fm.Groups[3].Success && fm.Groups[4].Success)
            {
                var objName = fm.Groups[3].Value;
                var propName = fm.Groups[4].Value;

                RegisterAlias(fnName, propName);
                RegisterAlias(fnName, $"{objName}.{propName}");

                if (TryResolve(propName, out var p, out var s))
                {
                    Register(fnName, p, s);
                }
                else if (TryResolve($"{objName}.{propName}", out p, out s))
                {
                    Register(fnName, p, s);
                }
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
