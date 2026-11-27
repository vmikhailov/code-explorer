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

    private static readonly ConcurrentDictionary<string, string> _globalPrefixes =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> _mountPrefixes =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string> _rawTemplatesByNormalized =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MaxRawTemplatesCapacity = 65536;
    private const char QuestionMarkChar = '?';
    private const char AmpersandChar = '&';
    private const char OpenBraceSlotChar = '{';
    private const char CloseBraceSlotChar = '}';
    private const char PercentSpecChar = '%';
    private const char SlashPathChar = '/';
    private const char StarWildcardChar = '*';
    private const char SingleQuoteTrimChar = '\'';
    private const char DoubleQuoteTrimChar = '"';
    private const char BacktickTrimChar = '`';
    private const char SpaceTrimChar = ' ';
    private const char SemicolonTrimChar = ';';
    private const string SchemeSeparator = "://";
    private const string WildcardSlashPrefix = "*/";
    private const string SchemeHttpPrefix = "http://";
    private const string SchemeHttpsPrefix = "https://";
    private const string SchemeWsPrefix = "ws://";
    private const string SchemeWssPrefix = "wss://";
    private const string ServiceSuffixToken = "service";
    private const string ClientSuffixToken = "client";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static void RegisterMountPrefix(string filePath, string mountPrefix)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(mountPrefix)) return;
        var cleanPath = filePath.Replace('\\', '/').Trim('/');
        var cleanPrefix = mountPrefix.Trim().Trim('\'', '"', '`').Trim('/');
        if (!string.IsNullOrEmpty(cleanPrefix))
        {
            _mountPrefixes[cleanPath] = cleanPrefix;
            var fileName = Path.GetFileName(cleanPath);
            if (!string.IsNullOrEmpty(fileName))
            {
                _mountPrefixes[fileName] = cleanPrefix;
            }
            var nameWithoutExt = Path.GetFileNameWithoutExtension(cleanPath);
            if (!string.IsNullOrEmpty(nameWithoutExt))
            {
                _mountPrefixes[nameWithoutExt] = cleanPrefix;
            }
        }
    }

    public static bool TryGetMountPrefix(string? filePath, out string mountPrefix)
    {
        mountPrefix = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var cleanPath = filePath.Replace('\\', '/').Trim('/');
        if (_mountPrefixes.TryGetValue(cleanPath, out var p))
        {
            mountPrefix = p;
            return true;
        }
        var fileName = Path.GetFileName(cleanPath);
        if (!string.IsNullOrEmpty(fileName) && _mountPrefixes.TryGetValue(fileName, out p))
        {
            mountPrefix = p;
            return true;
        }
        var nameWithoutExt = Path.GetFileNameWithoutExtension(cleanPath);
        if (!string.IsNullOrEmpty(nameWithoutExt) && _mountPrefixes.TryGetValue(nameWithoutExt, out p))
        {
            mountPrefix = p;
            return true;
        }
        return false;
    }

    private static readonly HashSet<string> _genericVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "url", "baseUrl", "apiUrl", "uri", "path", "href", "link", "endpoint", "endpoints", "req", "res", "request", "response",
        "data", "body", "query", "params", "param", "header", "headers", "options", "config", "result",
        "item", "file", "dir", "str", "text", "val", "value", "key", "cmd", "args", "output", "input",
        "err", "error", "msg", "message", "event", "evt", "target", "src", "dest", "base", "temp", "tmp"
    };

    public static void RegisterGlobalPrefix(string? projectName, string prefix)
    {
        if (!string.IsNullOrWhiteSpace(projectName) && !string.IsNullOrWhiteSpace(prefix))
        {
            var clean = prefix.Trim().Trim('\'', '"', '`').Trim('/');
            if (!string.IsNullOrEmpty(clean) && !clean.Contains("${"))
            {
                _globalPrefixes[projectName.Trim()] = clean;
                var normProj = WorkspaceConventions.NormalizeServiceName(projectName);
                if (!string.IsNullOrEmpty(normProj))
                {
                    _globalPrefixes[normProj] = clean;
                }
            }
        }
    }

    public static bool TryGetGlobalPrefix(string? projectName, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(projectName)) return false;
        var p = projectName.Trim();
        if (_globalPrefixes.TryGetValue(p, out var pref))
        {
            prefix = pref;
            return true;
        }
        var normProj = WorkspaceConventions.NormalizeServiceName(projectName);
        if (!string.IsNullOrEmpty(normProj) && _globalPrefixes.TryGetValue(normProj, out pref))
        {
            prefix = pref;
            return true;
        }
        return false;
    }

    public static void RegisterServiceDomain(string key, string domain)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(domain))
        {
            var normDomain = WorkspaceConventions.NormalizeServiceName(domain);
            if (string.IsNullOrEmpty(normDomain)) normDomain = domain;
            _serviceDomains[key] = normDomain;
            var normKey = WorkspaceConventions.NormalizeServiceName(key);
            if (!string.IsNullOrEmpty(normKey))
            {
                _serviceDomains[normKey] = normDomain;
            }
        }
    }

    public static bool TryResolveServiceDomain(string key, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (_serviceDomains.TryGetValue(key, out var d))
        {
            domain = d;
            return true;
        }
        var normKey = WorkspaceConventions.NormalizeServiceName(key);
        if (!string.IsNullOrEmpty(normKey) && _serviceDomains.TryGetValue(normKey, out d))
        {
            domain = d;
            return true;
        }
        return false;
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
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(path) && !_genericVariableNames.Contains(key.Trim()))
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
        if (string.IsNullOrWhiteSpace(key) || _genericVariableNames.Contains(key.Trim()))
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

    public static IReadOnlyCollection<string> GetAllKnownServices() => [.. _serviceDomains.Values];

    public static void Clear()
    {
        _routes.Clear();
        _serviceDomains.Clear();
        _aliases.Clear();
        _globalPrefixes.Clear();
        _mountPrefixes.Clear();
        _rawTemplatesByNormalized.Clear();
    }

    public static string ResolveTemplatePath(string? rawServiceName, string normalizedPath)
    {
        if (!string.IsNullOrWhiteSpace(rawServiceName))
        {
            var trimmed = rawServiceName.Trim(SingleQuoteTrimChar, DoubleQuoteTrimChar, BacktickTrimChar, SpaceTrimChar, SemicolonTrimChar);
            var qIdx = trimmed.IndexOf(QuestionMarkChar);
            if (qIdx >= 0) trimmed = trimmed[..qIdx];
            var ampIdx = trimmed.IndexOf(AmpersandChar);
            if (ampIdx >= 0) trimmed = trimmed[..ampIdx];

            if (_rawTemplatesByNormalized.TryGetValue(trimmed, out var tplFromFull) &&
                !string.IsNullOrEmpty(tplFromFull))
            {
                return tplFromFull;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedPath) &&
            _rawTemplatesByNormalized.TryGetValue(normalizedPath, out var tplFromPath) &&
            !string.IsNullOrEmpty(tplFromPath))
        {
            return tplFromPath;
        }

        return normalizedPath;
    }

    private static void RecordRawTemplate(string normalizedFull, string rawWithSlots)
    {
        var tplPath = ExtractTemplatePathPortion(rawWithSlots);
        if (string.IsNullOrEmpty(tplPath) || (!tplPath.Contains(OpenBraceSlotChar) && !tplPath.Contains(PercentSpecChar)))
        {
            return;
        }

        if (_rawTemplatesByNormalized.Count >= MaxRawTemplatesCapacity)
        {
            _rawTemplatesByNormalized.Clear();
        }

        if (!string.IsNullOrEmpty(normalizedFull))
        {
            _rawTemplatesByNormalized[normalizedFull] = tplPath;
        }

        var normPath = ExtractTemplatePathPortion(normalizedFull);
        if (!string.IsNullOrEmpty(normPath))
        {
            _rawTemplatesByNormalized[normPath] = tplPath;
        }
    }

    private static string ExtractTemplatePathPortion(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var s = url.Trim(SingleQuoteTrimChar, DoubleQuoteTrimChar, BacktickTrimChar, SpaceTrimChar);
        var schemeIdx = s.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (schemeIdx >= 0)
        {
            s = s[(schemeIdx + 3)..];
        }
        else if (s.StartsWith(WildcardSlashPrefix, StringComparison.Ordinal))
        {
            s = s[1..];
        }

        if (s.StartsWith(SlashPathChar))
        {
            return s;
        }

        var slashIdx = s.IndexOf(SlashPathChar);
        if (slashIdx <= 0)
        {
            return string.Empty;
        }

        var firstSeg = s[..slashIdx].ToLowerInvariant();
        if (firstSeg is "api" or "rest" or "v1" or "v2" or "v3" or "v4" or "v5" or "graphql")
        {
            return "/" + s;
        }

        return s[slashIdx..];
    }

    private static string PreserveWholeSegmentPlaceholders(string raw)
    {
        var s = raw.Trim(SingleQuoteTrimChar, DoubleQuoteTrimChar, BacktickTrimChar, SpaceTrimChar);
        if (!s.Contains(OpenBraceSlotChar) && !s.Contains(PercentSpecChar)) return s;

        var sb = new System.Text.StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var isAfterSlash = i > 0 && s[i - 1] == SlashPathChar;
            if (s[i] == '$' && i + 1 < s.Length && s[i + 1] == OpenBraceSlotChar)
            {
                var close = s.IndexOf(CloseBraceSlotChar, i + 2);
                if (close > i + 2)
                {
                    var isBeforeSlashOrEnd = close + 1 == s.Length || s[close + 1] == SlashPathChar;
                    var inner = s.AsSpan(i + 2, close - i - 2).Trim();
                    if (isAfterSlash && isBeforeSlashOrEnd && IsValidSlotIdentifier(inner))
                    {
                        sb.Append(OpenBraceSlotChar).Append(inner).Append(CloseBraceSlotChar);
                    }
                    else
                    {
                        sb.Append(StarWildcardChar);
                    }
                    i = close + 1;
                    continue;
                }
            }
            else if (s[i] == OpenBraceSlotChar)
            {
                var close = s.IndexOf(CloseBraceSlotChar, i + 1);
                if (close > i + 1)
                {
                    var isBeforeSlashOrEnd = close + 1 == s.Length || s[close + 1] == SlashPathChar;
                    var inner = s.AsSpan(i + 1, close - i - 1).Trim();
                    if (isAfterSlash && isBeforeSlashOrEnd && IsValidSlotIdentifier(inner))
                    {
                        sb.Append(OpenBraceSlotChar).Append(inner).Append(CloseBraceSlotChar);
                    }
                    else
                    {
                        sb.Append(StarWildcardChar);
                    }
                    i = close + 1;
                    continue;
                }
            }
            else if (s[i] == PercentSpecChar && i + 1 < s.Length && s[i + 1] is 's' or 'd' or 'v' or 'f')
            {
                var isBeforeSlashOrEnd = i + 2 == s.Length || s[i + 2] == SlashPathChar;
                if (isAfterSlash && isBeforeSlashOrEnd)
                {
                    sb.Append(s[i]).Append(s[i + 1]);
                }
                else
                {
                    sb.Append(StarWildcardChar);
                }
                i += 2;
                continue;
            }

            sb.Append(s[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsValidSlotIdentifier(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return false;
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (!char.IsLetterOrDigit(c) && c is not '_' and not ':' and not '.' and not '-')
            {
                return false;
            }
        }
        return true;
    }

    public static string NormalizeResolvedUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Strip query string if present (? or &)
        var qIdxEarly = raw.IndexOf('?');
        if (qIdxEarly >= 0) raw = raw[..qIdxEarly];
        var ampIdxEarly = raw.IndexOf('&');
        if (ampIdxEarly >= 0) raw = raw[..ampIdxEarly];

        if (WorkspaceConventions.TryMatchRouteFunction(raw, out var routeKey))
        {
            if (TryResolve(routeKey, out var rPath, out var rService))
            {
                var rawCleanPath = rPath.Split('?')[0];
                var cleanPath = Regex.Replace(rawCleanPath, @"\{[a-zA-Z0-9_]+\}", "*");
                var res = !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
                if (rawCleanPath != cleanPath)
                {
                    RecordRawTemplate(res, !string.IsNullOrEmpty(rService) ? $"{rService}{rawCleanPath}" : rawCleanPath);
                }
                return res;
            }
        }

        var preservedSlots = PreserveWholeSegmentPlaceholders(raw);

        // Replace template placeholders: ${...}, {var}, and format specifiers: %s, %d, %v, {0}
        var clean = Regex.Replace(raw, @"\$\{[^}]+\}", "*");
        clean = Regex.Replace(clean, @"\{[^}]+\}", "*");
        clean = Regex.Replace(clean, @"%[sdvf]", "*");
        clean = clean.Trim('\'', '"', '`', ' ');

        // Check if route registry has this key directly (e.g. SINGLE_STAGE, BUY_DOMAIN, NETWORKS)
        if (TryResolve(clean, out var rp, out var rs))
        {
            var rawCleanPath = rp.Split('?')[0];
            var cleanPath = Regex.Replace(rawCleanPath, @"\{[^}]+\}", "*");
            var res = !string.IsNullOrEmpty(rs) ? $"{rs}{cleanPath}" : cleanPath;
            if (rawCleanPath != cleanPath)
            {
                RecordRawTemplate(res, !string.IsNullOrEmpty(rs) ? $"{rs}{rawCleanPath}" : rawCleanPath);
            }
            return res;
        }

        var normalizedResult = FinalizeNormalizedUrl(clean);
        if (preservedSlots != clean)
        {
            RecordRawTemplate(normalizedResult, preservedSlots);
        }
        return normalizedResult;
    }

    private static string FinalizeNormalizedUrl(string clean)
    {
        if (clean.StartWithIC(SchemeHttpPrefix) ||
            clean.StartWithIC(SchemeHttpsPrefix) ||
            clean.StartWithIC(SchemeWsPrefix) ||
            clean.StartWithIC(SchemeWssPrefix))
        {
            return clean;
        }

        // If it starts with '*/' (e.g. ${baseUrl}/api/v1/foo -> */api/v1/foo)
        if (clean.StartsWith(WildcardSlashPrefix, StringComparison.Ordinal))
        {
            return clean;
        }

        // If it contains a slash, extract the path part starting with '/' only if prefix is an authority/host (contains '.' or ':')
        var slashIdx = clean.IndexOf(SlashPathChar);
        if (slashIdx > 0)
        {
            var prefix = clean[..slashIdx];
            if (prefix.Contains('-') || prefix.Contains('_') ||
                prefix.EndsWith(ServiceSuffixToken, StringComparison.OrdinalIgnoreCase) ||
                prefix.EndsWith(ClientSuffixToken, StringComparison.OrdinalIgnoreCase) ||
                GetAllKnownServices().Any(s => string.Equals(s, prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return clean;
            }
            if (prefix.Contains('.') || prefix.Contains(':'))
            {
                return clean[slashIdx..];
            }
            return clean;
        }

        return clean;
    }

    public static void ScanServiceDomains(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            // 1. Extract SERVICE_DOMAINS / SERVICES mapping: KEY: 'service-name' or 'http://service-name'
            var serviceDomainsMatches = Regex.Matches(content, @"(?:SERVICE_DOMAINS|SERVICES|Services)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?;", RegexOptions.Multiline, RegexTimeout);
            foreach (Match sm in serviceDomainsMatches)
            {
                var body = sm.Groups[1].Value;
                var domainMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*['""]([^'""]+)['""]", RegexOptions.None, RegexTimeout);
                foreach (Match dm in domainMatches)
                {
                    var sKey = dm.Groups[1].Value;
                    var sVal = dm.Groups[2].Value.Trim();
                    if (sVal.StartWithIC("http://") || sVal.StartWithIC("https://"))
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
                    var normSVal = WorkspaceConventions.NormalizeServiceName(sVal);
                    if (string.IsNullOrEmpty(normSVal)) normSVal = sVal;
                    _serviceDomains[sKey] = normSVal;
                    var normSKey = WorkspaceConventions.NormalizeServiceName(sKey);
                    if (!string.IsNullOrEmpty(normSKey))
                    {
                        _serviceDomains[normSKey] = normSVal;
                    }
                }
            }

            // JSON format for Services: "Services": { "Bundle": "http://bundle-service", ... }
            var jsonServicesMatch = Regex.Match(content, @"""Services""\s*:\s*\{([\s\S]*?)\}", RegexOptions.Multiline, RegexTimeout);
            if (jsonServicesMatch.Success)
            {
                var body = jsonServicesMatch.Groups[1].Value;
                var domainMatches = Regex.Matches(body, @"""([A-Za-z0-9_\-]+)""\s*:\s*""([^""]+)""", RegexOptions.None, RegexTimeout);
                foreach (Match dm in domainMatches)
                {
                    var sKey = dm.Groups[1].Value;
                    var sVal = dm.Groups[2].Value.Trim();
                    if (sVal.StartWithIC("http://") || sVal.StartWithIC("https://"))
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
                    var normSVal = WorkspaceConventions.NormalizeServiceName(sVal);
                    if (string.IsNullOrEmpty(normSVal)) normSVal = sVal;
                    _serviceDomains[sKey] = normSVal;
                    var normSKey = WorkspaceConventions.NormalizeServiceName(sKey);
                    if (!string.IsNullOrEmpty(normSKey))
                    {
                        _serviceDomains[normSKey] = normSVal;
                    }
                }
            }

            // 3. ServiceUrls / ServiceUrl patterns in structs or config: ServiceUrls: []string{"translate.google.com"} or ServiceUrl: "api.foo.com"
            var serviceUrlsMatches = Regex.Matches(content, @"(?:ServiceUrls|ServiceUrl|BaseUrls|BaseUrl)\s*[:=]\s*(?:\[\]string\s*\{\s*)?['""]([^'""]+)['""]", RegexOptions.Multiline, RegexTimeout);
            foreach (Match sm in serviceUrlsMatches)
            {
                var sVal = sm.Groups[1].Value.Trim();
                if (sVal.Contains('.') && !sVal.Contains(' '))
                {
                    if (sVal.StartWithIC("http://") || sVal.StartWithIC("https://"))
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
                    var normSVal = WorkspaceConventions.NormalizeServiceName(sVal);
                    if (string.IsNullOrEmpty(normSVal)) normSVal = sVal;
                    _serviceDomains["translator"] = normSVal;
                    _serviceDomains["webapi"] = normSVal;
                    _serviceDomains[normSVal] = normSVal;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Safeguard against catastrophic backtracking
        }
    }

    public static void ScanAndRegister(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            ScanServiceDomains(content);
            var knownServices = _serviceDomains.Count > 0
                ? _serviceDomains.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : [];

        // 1a-1. Top-level string/URL constant declarations:
        // const apiRoot = 'https://api.HOSTNAME/api/v1';
        // const identityRoot = 'https://api.HOSTNAME/identity';
        var topConstMatches = Regex.Matches(content,
            @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*['""`]([^'""`\r\n]+)['""`]\s*;?",
            RegexOptions.Multiline);
        foreach (Match tcm in topConstMatches)
        {
            var cName = tcm.Groups[1].Value;
            if (_genericVariableNames.Contains(cName)) continue;
            var cVal = tcm.Groups[2].Value.Trim();
            if (cVal.Contains('/') || cVal.StartWithIC("http://") || cVal.StartWithIC("https://"))
            {
                Register(cName, cVal, InferService(cName, cVal, knownServices));
            }
        }

        // 1b. Environment, URL map, and configuration dictionaries:
        // export const environment = { ... }, const urlMap = { ... }, const Endpoints = { ... }
        var envDictMatches = Regex.Matches(content,
            @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?\s*(?:;|\n|$)",
            RegexOptions.Multiline, RegexTimeout);
        foreach (Match m in envDictMatches)
        {
            var varName = m.Groups[1].Value;
            var isCandidate = Regex.IsMatch(varName,
                @"(?:env|environment|url|endpoint|route|service|api|config)",
                RegexOptions.IgnoreCase);
            if (!isCandidate) continue;

            var body = m.Groups[2].Value;

            // Direct string literal entries: key: 'url'
            var entryMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*['""`]([^'""`\r\n]+)['""`]");
            foreach (Match em in entryMatches)
            {
                var key = em.Groups[1].Value;
                var val = em.Groups[2].Value.Trim();

                if (val.Contains('/') || val.StartWithIC("http://") || val.StartWithIC("https://"))
                {
                    var inferredSvc = InferService(key, val, knownServices);
                    Register(key, val, inferredSvc);
                    Register($"{varName}.{key}", val, inferredSvc);
                    if (varName.StartWithIC("env"))
                    {
                        Register($"env.{key}", val, inferredSvc);
                        Register($"environment.{key}", val, inferredSvc);
                    }
                }
            }

            // Binary concatenation entries: key: apiRoot + '/profiles' or key: 'http://foo' + port
            var concatMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*([A-Za-z0-9_]+)\s*\+\s*['""`]([^'""`\r\n]+)['""`]");
            foreach (Match cm in concatMatches)
            {
                var key = cm.Groups[1].Value;
                var baseVar = cm.Groups[2].Value;
                var suffix = cm.Groups[3].Value.Trim();

                if (TryResolve(baseVar, out var baseVal, out var baseSvc))
                {
                    var combined = baseVal.TrimEnd('/') + "/" + suffix.TrimStart('/');
                    var inferredSvc = baseSvc ?? InferService(key, combined, knownServices);
                    Register(key, combined, inferredSvc);
                    Register($"{varName}.{key}", combined, inferredSvc);
                    if (varName.StartWithIC("env"))
                    {
                        Register($"env.{key}", combined, inferredSvc);
                        Register($"environment.{key}", combined, inferredSvc);
                    }
                }
            }

            // Identifier alias entries: identity: identityRoot or allowedUrls: ...
            var identMatches = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*([A-Za-z0-9_]+)(?:\s*,\s*|\s*$)");
            foreach (Match im in identMatches)
            {
                var key = im.Groups[1].Value;
                var target = im.Groups[2].Value;
                if (!string.Equals(key, target, StringComparison.OrdinalIgnoreCase) &&
                    !target.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                    !target.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                    !target.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                    !target.Equals("undefined", StringComparison.OrdinalIgnoreCase))
                {
                    RegisterAlias(key, target);
                    RegisterAlias($"{varName}.{key}", target);
                }
            }

            // Nested dictionary endpoints: gameList: { endpoint: env.games, ... }
            var nestedMatches = Regex.Matches(body, @"([A-Za-z0-9_]+)\s*:\s*\{[\s\S]*?endpoint\s*:\s*(?:([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)|['""`]([^'""`\r\n]+)['""`])[\s\S]*?\}", RegexOptions.None, RegexTimeout);
            foreach (Match nm in nestedMatches)
            {
                var secName = nm.Groups[1].Value;
                if (nm.Groups[2].Success && nm.Groups[3].Success)
                {
                    var targetKey = nm.Groups[3].Value;
                    RegisterAlias($"{varName}.{secName}.endpoint", targetKey);
                    RegisterAlias($"{secName}.endpoint", targetKey);
                    RegisterAlias(secName, targetKey);
                }
                else if (nm.Groups[4].Success)
                {
                    var rawVal = nm.Groups[4].Value.Trim();
                    var inferredSvc = InferService(secName, rawVal, knownServices);
                    Register($"{varName}.{secName}.endpoint", rawVal, inferredSvc);
                    Register($"{secName}.endpoint", rawVal, inferredSvc);
                    Register(secName, rawVal, inferredSvc);
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

                if (rawUrl.Contains('/') || rawUrl.StartWithIC("http://") || rawUrl.StartWithIC("https://"))
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

                if (rawUrl.Contains('/') || rawUrl.StartWithIC("http://") || rawUrl.StartWithIC("https://"))
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
        var apiRoutesMatch = Regex.Match(content, @"(?:API_ROUTES|ROUTES)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?;", RegexOptions.Multiline, RegexTimeout);
        if (apiRoutesMatch.Success)
        {
            var body = apiRoutesMatch.Groups[1].Value;
            var serviceBlocks = Regex.Matches(body, @"['""]?([A-Za-z0-9_\-]+)['""]?\s*:\s*\[([\s\S]*?)\]", RegexOptions.Multiline, RegexTimeout);
            foreach (Match sb in serviceBlocks)
            {
                var sKey = sb.Groups[1].Value;
                var innerRoutes = sb.Groups[2].Value;
                _serviceDomains.TryGetValue(sKey, out var domain);
                if (string.IsNullOrEmpty(domain))
                {
                    domain = WorkspaceConventions.NormalizeServiceName(sKey);
                }

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
            if (sVal.StartWithIC("http://") || sVal.StartWithIC("https://"))
            {
                try
                {
                    var uri = new Uri(sVal);
                    sVal = uri.Host;
                }
                catch { }
            }
            var normVal = WorkspaceConventions.NormalizeServiceName(sVal);
            if (string.IsNullOrEmpty(normVal)) normVal = sVal;
            _serviceDomains[sKey] = normVal;
            var normK = WorkspaceConventions.NormalizeServiceName(sKey);
            if (!string.IsNullOrEmpty(normK))
            {
                _serviceDomains[normK] = normVal;
            }
        }
        }
        catch (RegexMatchTimeoutException)
        {
            // Safeguard against catastrophic backtracking
        }
    }

    private static string? InferService(string key, string val, string[] knownServices)
    {
        if (Uri.TryCreate(val, UriKind.Absolute, out var parsedUri))
        {
            var host = parsedUri.Host;
            if (!string.IsNullOrEmpty(host) &&
                !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                !host.Contains("hostname", StringComparison.OrdinalIgnoreCase) &&
                !host.Contains("example", StringComparison.OrdinalIgnoreCase))
            {
                var normHost = WorkspaceConventions.NormalizeServiceName(host);
                return !string.IsNullOrEmpty(normHost) ? normHost : host;
            }
        }

        if (_serviceDomains.TryGetValue(key, out var exactSvc))
        {
            return exactSvc;
        }
        var normKey = WorkspaceConventions.NormalizeServiceName(key);
        if (!string.IsNullOrEmpty(normKey) && _serviceDomains.TryGetValue(normKey, out var normSvc))
        {
            return normSvc;
        }
        for (var i = 0; i < knownServices.Length; i++)
        {
            var svc = knownServices[i];
            if (!string.IsNullOrEmpty(svc) && (key.Contains(svc, StringComparison.OrdinalIgnoreCase) || normKey.Contains(svc, StringComparison.OrdinalIgnoreCase)))
            {
                return svc;
            }
        }

        return null;
    }

    private static string rmKey(Match m) => m.Groups[4].Value;
    private static string rmPath(Match m) => m.Groups[3].Value;
}
