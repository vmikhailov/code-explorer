using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Discovered service endpoint or URL target from configuration.
/// </summary>
public record DiscoveredServiceUrl(
    string SourceFilePath,
    string? ProjectName,
    string ConfigKey,
    string ServiceName,
    string Url,
    string Scheme,
    string Host,
    int? Port,
    bool IsExternal
);

/// <summary>
/// Parsed configuration entry stored in memory.
/// </summary>
public record ConfigEntry(
    string Key,
    string Value,
    string SourceFilePath,
    string? ProjectName
);

/// <summary>
/// Unified early-stage configuration store.
/// Recursively parses and indexes appsettings*.json, .env*, application*.yml, and application*.properties
/// into flat key-path hierarchies and automatically registers them into <see cref="ConstantRegistry"/>.
/// Also discovers service-to-service URLs and external APIs generically without hardcoded vendor names.
/// </summary>
public static class ConfigStore
{
    private static readonly Regex KeyValEnvRegex = new(@"^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_.]*)\s*=\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex PropLineRegex = new(@"^\s*([A-Za-z0-9_.\-]+)\s*[:=]\s*(.*)$", RegexOptions.Compiled);

    // Project -> (NormalizedKey -> Value)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _projectConfigs =
        new(StringComparer.OrdinalIgnoreCase);

    // Global workspace configs
    private static readonly ConcurrentDictionary<string, string> _globalConfigs =
        new(StringComparer.OrdinalIgnoreCase);

    // Discovered service endpoints from config values
    private static readonly ConcurrentBag<DiscoveredServiceUrl> _discoveredUrls = [];

    // Parsed entries per file for single-pass reading
    private static readonly ConcurrentDictionary<string, List<ConfigEntry>> _fileConfigs =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Clear()
    {
        _projectConfigs.Clear();
        _globalConfigs.Clear();
        _fileConfigs.Clear();
        while (_discoveredUrls.TryTake(out _)) { }
    }

    public static IReadOnlyList<DiscoveredServiceUrl> GetDiscoveredUrls(string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return [.. _discoveredUrls];
        }

        var pName = projectName.Trim();
        var domain = ConstantRegistry.ExtractDomainNameFromProject(pName);

        return
        [
            .. _discoveredUrls.Where(u =>
                string.Equals(u.ProjectName, pName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(domain) &&
                 string.Equals(u.ProjectName, domain, StringComparison.OrdinalIgnoreCase)))
        ];
    }

    public static bool TryGetConfig(string? projectName, string key, out string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null!;
            return false;
        }

        var cleanKey = key.Trim();

        // 1. Try project-specific config
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var pName = projectName.Trim();
            if (_projectConfigs.TryGetValue(pName, out var pDict) && pDict.TryGetValue(cleanKey, out var pVal))
            {
                value = pVal;
                return true;
            }

            var domain = ConstantRegistry.ExtractDomainNameFromProject(pName);
            if (!string.IsNullOrWhiteSpace(domain) && _projectConfigs.TryGetValue(domain, out var dDict) && dDict.TryGetValue(cleanKey, out var dVal))
            {
                value = dVal;
                return true;
            }
        }

        // 2. Try global config
        if (_globalConfigs.TryGetValue(cleanKey, out var gVal))
        {
            value = gVal;
            return true;
        }

        value = null!;
        return false;
    }

    public static IReadOnlyList<ConfigEntry> GetFileEntries(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return [];

        var normPath = Path.GetFullPath(filePath).Replace('\\', '/');
        if (_fileConfigs.TryGetValue(normPath, out var entries))
        {
            lock (entries)
            {
                return [.. entries];
            }
        }

        var altKey = filePath.Replace('\\', '/');
        if (_fileConfigs.TryGetValue(altKey, out entries))
        {
            lock (entries)
            {
                return [.. entries];
            }
        }

        var match = _fileConfigs.FirstOrDefault(kvp => kvp.Key.EndsWith(altKey, StringComparison.OrdinalIgnoreCase));
        if (match.Value != null)
        {
            lock (match.Value)
            {
                return [.. match.Value];
            }
        }

        return [];
    }

    public static bool IsConfigurationFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var lower = fileName.ToLowerInvariant();
        if (lower.StartsWith(".env") ||
            (lower.StartsWith("docker-compose") && (lower.EndsWith(".yml") || lower.EndsWith(".yaml"))) ||
            (lower.EndsWith(".toml") && lower is not ("cargo.toml" or "pyproject.toml" or "rustfmt.toml" or "clippy.toml" or "deny.toml" or "release.toml" or "cross.toml" or "taplo.toml")))
        {
            return true;
        }

        var parsers = WorkspaceIndexer.GetAllProjectParsers();
        for (int i = 0; i < parsers.Count; i++)
        {
            if (parsers[i].IsConfigurationFile(fileName))
            {
                return true;
            }
        }

        return (lower.StartsWith("appsettings") && lower.EndsWith(".json")) ||
               (lower.StartsWith("application") && (lower.EndsWith(".properties") || lower.EndsWith(".yml") || lower.EndsWith(".yaml"))) ||
               lower is "web.config" or "app.config";
    }

    public static void LoadFile(string filePath, string? projectName, ParsingContext? ctx = null)
    {
        if (!File.Exists(filePath)) return;

        var fileName = Path.GetFileName(filePath);
        var lower = fileName.ToLowerInvariant();

        try
        {
            if (lower.EndsWith(".json"))
            {
                ParseAppSettingsJson(filePath, projectName);
            }
            else if (lower.StartsWith(".env"))
            {
                ParseDotEnv(filePath, projectName);
            }
            else if (lower.EndsWith(".properties") || lower.EndsWith(".toml"))
            {
                ParseApplicationProperties(filePath, projectName);
            }
            else if (lower.EndsWith(".yml") || lower.EndsWith(".yaml"))
            {
                ParseApplicationYaml(filePath, projectName);
            }
            else if (lower.EndsWith(".config"))
            {
                ParseDotNetXmlConfig(filePath, projectName);
            }
        }
        catch (Exception ex)
        {
            ctx?.LogWarning($"[ConfigStore] Failed to load config file '{filePath}': {ex.Message}");
        }
    }

    private static readonly JsonDocumentOptions JsonDocOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static void ParseAppSettingsJson(string filePath, string? projectName)
    {
        var content = File.ReadAllText(filePath);

        using var doc = JsonDocument.Parse(content, JsonDocOptions);

        if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

        TraverseJsonElement(doc.RootElement, "", filePath, projectName);
    }

    private static void TraverseJsonElement(JsonElement element, string currentPrefix, string filePath, string? projectName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var nextPrefix = string.IsNullOrEmpty(currentPrefix) ? prop.Name : $"{currentPrefix}:{prop.Name}";
                TraverseJsonElement(prop.Value, nextPrefix, filePath, projectName);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var nextPrefix = $"{currentPrefix}:{index}";
                TraverseJsonElement(item, nextPrefix, filePath, projectName);
                index++;
            }
        }
        else if (element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            var rawValue = element.ToString() ?? "";
            RegisterConfigValue(currentPrefix, rawValue, filePath, projectName);
        }
    }

    private static void ParseDotEnv(string filePath, string? projectName)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || string.IsNullOrWhiteSpace(trimmed)) continue;

            var match = KeyValEnvRegex.Match(trimmed);
            if (!match.Success) continue;

            var key = match.Groups[1].Value.Trim();
            var val = match.Groups[2].Value.Trim().Trim('"', '\'');

            RegisterConfigValue(key, val, filePath, projectName);
        }
    }

    private static void ParseApplicationProperties(string filePath, string? projectName)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.StartsWith('!') || string.IsNullOrWhiteSpace(trimmed)) continue;

            var match = PropLineRegex.Match(trimmed);
            if (!match.Success) continue;

            var key = match.Groups[1].Value.Trim();
            var val = match.Groups[2].Value.Trim().Trim('"', '\'');

            RegisterConfigValue(key, val, filePath, projectName);
        }
    }

    private static void ParseApplicationYaml(string filePath, string? projectName)
    {
        var lines = File.ReadAllLines(filePath);
        var stack = new List<(int Indent, string Key)>();

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            var trimmedStart = rawLine.TrimStart();
            if (trimmedStart.StartsWith('#')) continue;

            if (trimmedStart.StartsWith("- "))
            {
                var itemText = trimmedStart[2..].Trim().Trim('"', '\'');
                var envMatch = KeyValEnvRegex.Match(itemText);
                if (envMatch.Success)
                {
                    var eKey = envMatch.Groups[1].Value.Trim();
                    var eVal = envMatch.Groups[2].Value.Trim().Trim('"', '\'');
                    RegisterConfigValue(eKey, eVal, filePath, projectName);
                    continue;
                }
            }

            int indent = rawLine.Length - trimmedStart.Length;

            // Pop stack to current indent level
            while (stack.Count > 0 && stack[^1].Indent >= indent)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var colonIdx = trimmedStart.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = trimmedStart[..colonIdx].Trim();
            var afterColon = trimmedStart[(colonIdx + 1)..].Trim();

            if (string.IsNullOrEmpty(afterColon))
            {
                // Parent object
                stack.Add((indent, key));
            }
            else
            {
                // Leaf value
                var val = afterColon.Trim('"', '\'');
                var fullKey = stack.Count > 0
                    ? string.Join(":", stack.Select(s => s.Key)) + ":" + key
                    : key;

                RegisterConfigValue(fullKey, val, filePath, projectName);
            }
        }
    }

    private static void ParseDotNetXmlConfig(string filePath, string? projectName, string? logicalSourceFilePath = null, int depth = 0)
    {
        if (depth > 3 || !File.Exists(filePath)) return;

        var effectiveFilePath = logicalSourceFilePath ?? filePath;
        var content = File.ReadAllText(filePath);
        var doc = System.Xml.Linq.XDocument.Parse(content);
        if (doc.Root == null) return;

        var baseDir = Path.GetDirectoryName(filePath) ?? "";

        // 1. <connectionStrings>
        foreach (var csSection in doc.Root.Descendants().Where(e => e.Name.LocalName.Equals("connectionStrings", StringComparison.OrdinalIgnoreCase)))
        {
            var extSource = csSection.Attribute("configSource")?.Value ?? csSection.Attribute("file")?.Value;
            if (!string.IsNullOrWhiteSpace(extSource))
            {
                var linkedPath = ResolveLinkedXmlConfigPath(baseDir, extSource);
                if (linkedPath != null)
                {
                    ParseDotNetXmlConfig(linkedPath, projectName, effectiveFilePath, depth + 1);
                }
            }

            foreach (var addElem in csSection.Elements().Where(e => e.Name.LocalName.Equals("add", StringComparison.OrdinalIgnoreCase)))
            {
                var name = addElem.Attribute("name")?.Value?.Trim();
                var connStr = addElem.Attribute("connectionString")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(connStr))
                {
                    RegisterConfigValue($"ConnectionStrings:{name}", connStr, effectiveFilePath, projectName);
                }
            }
        }

        // 2. <appSettings>
        foreach (var appSection in doc.Root.Descendants().Where(e => e.Name.LocalName.Equals("appSettings", StringComparison.OrdinalIgnoreCase)))
        {
            var extSource = appSection.Attribute("file")?.Value ?? appSection.Attribute("configSource")?.Value;
            if (!string.IsNullOrWhiteSpace(extSource))
            {
                var linkedPath = ResolveLinkedXmlConfigPath(baseDir, extSource);
                if (linkedPath != null)
                {
                    ParseDotNetXmlConfig(linkedPath, projectName, effectiveFilePath, depth + 1);
                }
            }

            foreach (var addElem in appSection.Elements().Where(e => e.Name.LocalName.Equals("add", StringComparison.OrdinalIgnoreCase)))
            {
                var key = addElem.Attribute("key")?.Value?.Trim();
                var val = addElem.Attribute("value")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
                {
                    RegisterConfigValue(key, val, effectiveFilePath, projectName);
                }
            }
        }

        // 3. <system.serviceModel> -> <client> -> <endpoint>
        foreach (var clientElem in doc.Root.Descendants().Where(e => e.Name.LocalName.Equals("client", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var epElem in clientElem.Elements().Where(e => e.Name.LocalName.Equals("endpoint", StringComparison.OrdinalIgnoreCase)))
            {
                var address = epElem.Attribute("address")?.Value?.Trim();
                var contract = epElem.Attribute("contract")?.Value?.Trim() ?? epElem.Attribute("name")?.Value?.Trim() ?? "WcfEndpoint";
                if (!string.IsNullOrWhiteSpace(address))
                {
                    RegisterConfigValue($"WcfClient:{contract}:Address", address, effectiveFilePath, projectName);
                }
            }
        }
    }

    private static string? ResolveLinkedXmlConfigPath(string baseDir, string relativeConfigPath)
    {
        if (string.IsNullOrWhiteSpace(baseDir) || string.IsNullOrWhiteSpace(relativeConfigPath)) return null;
        var normRel = relativeConfigPath.Replace('\\', '/');
        var directCandidate = Path.GetFullPath(Path.Combine(baseDir, normRel));
        if (File.Exists(directCandidate))
        {
            return directCandidate;
        }

        // If a project references "../Web.config" (deployment layout where Web.config sits in a sibling web project in source control),
        // check only when the target filename is Web.config in the parent solution directory.
        var targetFileName = Path.GetFileName(normRel);
        if (targetFileName.Equals("web.config", StringComparison.OrdinalIgnoreCase) && normRel.StartsWith("../", StringComparison.Ordinal))
        {
            var parentDir = Path.GetDirectoryName(directCandidate);
            if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            {
                try
                {
                    var siblingWebConfigs = Directory.EnumerateDirectories(parentDir)
                        .SelectMany(d => Directory.EnumerateFiles(d, "Web.config", SearchOption.TopDirectoryOnly)
                            .Concat(Directory.EnumerateFiles(d, "web.config", SearchOption.TopDirectoryOnly)))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    // Prefer the primary UI / Web project's Web.config if one exists
                    var best = siblingWebConfigs.FirstOrDefault(f => Path.GetDirectoryName(f)!.EndsWith(".UI", StringComparison.OrdinalIgnoreCase) ||
                                                                     Path.GetDirectoryName(f)!.EndsWith(".Web", StringComparison.OrdinalIgnoreCase))
                               ?? siblingWebConfigs.FirstOrDefault();
                    if (best != null) return best;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        return null;
    }

    private static void RegisterConfigValue(string keyPath, string value, string filePath, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(value)) return;

        var cleanKey = keyPath.Trim();
        var cleanVal = value.Trim().Trim('"', '\'', '`');

        // Store in ConfigStore internal dictionaries
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var pDict = _projectConfigs.GetOrAdd(projectName.Trim(), _ => new(StringComparer.OrdinalIgnoreCase));
            pDict[cleanKey] = cleanVal;
        }
        _globalConfigs.TryAdd(cleanKey, cleanVal);

        // Record parsed entry for file
        var entry = new ConfigEntry(cleanKey, cleanVal, filePath, projectName);
        var normFile = Path.GetFullPath(filePath).Replace('\\', '/');
        var fileList = _fileConfigs.GetOrAdd(normFile, _ => []);
        lock (fileList)
        {
            fileList.Add(entry);
        }

        // Standard C# format: Section:SubSection:Key
        ConstantRegistry.Register(projectName, cleanKey, cleanVal);

        // Dot format: Section.SubSection.Key
        if (cleanKey.Contains(':'))
        {
            var dotKey = cleanKey.Replace(':', '.');
            ConstantRegistry.Register(projectName, dotKey, cleanVal);
        }
        else if (cleanKey.Contains('.'))
        {
            var colonKey = cleanKey.Replace('.', ':');
            ConstantRegistry.Register(projectName, colonKey, cleanVal);
        }

        // If key came from env var with double underscores (e.g. KAFKA__TOPICS__ORDER_SUBMITTED)
        if (cleanKey.Contains("__"))
        {
            var colonFromDunder = cleanKey.Replace("__", ":");
            ConstantRegistry.Register(projectName, colonFromDunder, cleanVal);
            ConstantRegistry.Register(projectName, colonFromDunder.Replace("_", ""), cleanVal);
            var dotFromDunder = cleanKey.Replace("__", ".");
            ConstantRegistry.Register(projectName, dotFromDunder, cleanVal);
            ConstantRegistry.Register(projectName, dotFromDunder.Replace("_", ""), cleanVal);

            // Also register leaf property
            var dunderParts = cleanKey.Split("__", StringSplitOptions.RemoveEmptyEntries);
            if (dunderParts.Length > 1)
            {
                var leaf = dunderParts[^1];
                ConstantRegistry.Register(projectName, leaf, cleanVal);
                ConstantRegistry.Register(projectName, leaf.Replace("_", ""), cleanVal);
            }
        }

        // Environment format: SECTION__SUBSECTION__KEY and SECTION_SUBSECTION_KEY
        var envKeyDouble = cleanKey.Replace(':', '_').Replace('.', '_').ToUpperInvariant();
        var envKeyDunder = cleanKey.Replace(":", "__").Replace(".", "__").ToUpperInvariant();
        ConstantRegistry.Register(projectName, envKeyDouble, cleanVal);
        ConstantRegistry.Register(projectName, envKeyDunder, cleanVal);

        // Leaf property registration for common patterns
        var lastColon = cleanKey.LastIndexOf(':');
        var lastDot = cleanKey.LastIndexOf('.');
        var sepIdx = Math.Max(lastColon, lastDot);
        if (sepIdx >= 0 && sepIdx < cleanKey.Length - 1)
        {
            var leafKey = cleanKey[(sepIdx + 1)..];
            var parentPath = cleanKey[..sepIdx];

            // 1. ConnectionStrings:DefaultConnection -> "DefaultConnection"
            if (parentPath.EndsWith("ConnectionStrings", StringComparison.OrdinalIgnoreCase) ||
                parentPath.EndsWith("Database", StringComparison.OrdinalIgnoreCase))
            {
                ConstantRegistry.Register(projectName, leafKey, cleanVal);
            }
            // 2. Kafka:Topics:OrderCreated or Topics:OrderCreated -> "OrderCreated"
            else if (parentPath.EndsWith("Topics", StringComparison.OrdinalIgnoreCase) ||
                     parentPath.EndsWith("Queues", StringComparison.OrdinalIgnoreCase))
            {
                ConstantRegistry.Register(projectName, leafKey, cleanVal);
            }
            // 3. Services:Payment:Url or Clients:Order:BaseUrl -> register "Payment:Url", "Payment", "Order"
            else if (leafKey.Equals("url", StringComparison.OrdinalIgnoreCase) ||
                     leafKey.Equals("baseurl", StringComparison.OrdinalIgnoreCase) ||
                     leafKey.Equals("uri", StringComparison.OrdinalIgnoreCase) ||
                     leafKey.Equals("endpoint", StringComparison.OrdinalIgnoreCase) ||
                     leafKey.Equals("address", StringComparison.OrdinalIgnoreCase))
            {
                var prevSep = Math.Max(parentPath.LastIndexOf(':'), parentPath.LastIndexOf('.'));
                if (prevSep >= 0 && prevSep < parentPath.Length - 1)
                {
                    var serviceKey = parentPath[(prevSep + 1)..];
                    ConstantRegistry.Register(projectName, $"{serviceKey}:{leafKey}", cleanVal);
                    ConstantRegistry.Register(projectName, serviceKey, cleanVal);
                }
                else
                {
                    ConstantRegistry.Register(projectName, parentPath, cleanVal);
                }
            }
        }

        // Generic URL & Service Target Discovery (Phase 0.3)
        if (cleanVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            cleanVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            InspectAndRegisterUrl(cleanKey, cleanVal, filePath, projectName);
        }
    }

    private static void InspectAndRegisterUrl(string keyPath, string url, string filePath, string? projectName)
    {
        if (IsInboundOrNonServiceConfigKey(keyPath)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host)) return;

        // Derive Service Name from keyPath
        var serviceName = ExtractServiceNameFromKeyPath(keyPath);
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            serviceName = host;
        }

        // Determine if external:
        // Internal if localhost, loopback, or single-segment hostname without dot (typical Docker/K8s service name)
        var isLocal = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                      host.Equals("127.0.0.1", StringComparison.Ordinal) ||
                      host.Equals("0.0.0.0", StringComparison.Ordinal) ||
                      host.Equals("[::1]", StringComparison.Ordinal);

        var hasDomainDot = host.Contains('.') && !isLocal;
        var isExternal = hasDomainDot;

        var endpoint = new DiscoveredServiceUrl(
            SourceFilePath: filePath,
            ProjectName: projectName,
            ConfigKey: keyPath,
            ServiceName: serviceName,
            Url: url,
            Scheme: uri.Scheme,
            Host: host,
            Port: uri.Port > 0 && uri.Port != 80 && uri.Port != 443 ? uri.Port : null,
            IsExternal: isExternal
        );

        _discoveredUrls.Add(endpoint);

        // If URL has path (e.g. https://localhost:3001/spa-bff), check if path points to a downstream service
        if (uri.AbsolutePath.Length > 1)
        {
            var firstSeg = uri.AbsolutePath.Trim('/').Split('/')[0];
            if (!string.IsNullOrWhiteSpace(firstSeg) &&
                !firstSeg.Equals("api", StringComparison.OrdinalIgnoreCase) &&
                !firstSeg.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                !firstSeg.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
            {
                var pathEndpoint = new DiscoveredServiceUrl(
                    SourceFilePath: filePath,
                    ProjectName: projectName,
                    ConfigKey: keyPath + "#" + firstSeg,
                    ServiceName: firstSeg,
                    Url: url,
                    Scheme: uri.Scheme,
                    Host: host,
                    Port: uri.Port > 0 && uri.Port != 80 && uri.Port != 443 ? uri.Port : null,
                    IsExternal: isExternal
                );
                _discoveredUrls.Add(pathEndpoint);
            }
        }
    }

    public static string StripServiceOptionsSuffixes(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = name.Trim();
        var suffixes = new[]
        {
            "RestClientOptions", "ClientOptions", "HttpOptions", "ApiOptions",
            "ServiceOptions", "GrpcOptions", "RestClient", "HttpClient", "Options"
        };
        foreach (var s in suffixes)
        {
            if (clean.EndsWith(s, StringComparison.OrdinalIgnoreCase) && clean.Length > s.Length)
            {
                clean = clean[..^s.Length];
                break;
            }
        }
        return clean.Trim('-', '_');
    }

    private static string ExtractServiceNameFromKeyPath(string keyPath)
    {
        var parts = keyPath.Split([':', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return string.Empty;

        // 1. Check for YARP / ReverseProxy clusters or routes (e.g. Yarp:Clusters:spa-bff:Destinations:... or ReverseProxy:Clusters:api-bff:...)
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("clusters", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Equals("routes", StringComparison.OrdinalIgnoreCase))
            {
                var clusterOrRoute = parts[i + 1];
                if (!string.IsNullOrWhiteSpace(clusterOrRoute))
                {
                    if (clusterOrRoute.Contains('/')) clusterOrRoute = clusterOrRoute.Split('/')[0];
                    return StripServiceOptionsSuffixes(clusterOrRoute);
                }
            }
        }

        // 2. Check for standard leaf URL/address properties
        var last = parts[^1];
        if (last.Equals("url", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("baseurl", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("uri", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("endpoint", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("address", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("baseaddress", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("host", StringComparison.OrdinalIgnoreCase))
        {
            var target = parts.Length > 1 ? parts[^2] : last;
            if (target.Contains('/')) target = target.Split('/')[0];
            return StripServiceOptionsSuffixes(target);
        }

        // 3. Check for env var formats like VITE_GATEWAY_API_BASE_URL or SERVICES__CATALOGS__HTTP__0
        if (keyPath.Contains('_'))
        {
            var envParts = keyPath.Split('_', StringSplitOptions.RemoveEmptyEntries);
            var meaningful = envParts.Where(p =>
                !p.Equals("VITE", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("NEXT", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("PUBLIC", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("REACT", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("APP", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("BASE", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("URL", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("URI", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("API", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("HOST", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("ENDPOINT", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("SERVICES", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("HTTP", StringComparison.OrdinalIgnoreCase) &&
                !p.Equals("HTTPS", StringComparison.OrdinalIgnoreCase) &&
                !int.TryParse(p, out _)).ToList();
            if (meaningful.Count > 0)
            {
                return StripServiceOptionsSuffixes(meaningful[^1]);
            }
        }

        return StripServiceOptionsSuffixes(last);
    }

    private static bool IsInboundOrNonServiceConfigKey(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath)) return true;
        var lower = keyPath.ToLowerInvariant();
        return lower.Contains("cors") ||
               lower.Contains("allowedorigins") ||
               lower.Contains("allowedhosts") ||
               lower.Contains("validissuers") ||
               lower.Contains("validaudiences") ||
               lower.Contains("$schema") ||
               lower.Contains("xmlns") ||
               lower.EndsWith("repository") ||
               lower.EndsWith("repository:url") ||
               lower.EndsWith("homepage") ||
               lower.EndsWith("bugs:url");
    }
}
