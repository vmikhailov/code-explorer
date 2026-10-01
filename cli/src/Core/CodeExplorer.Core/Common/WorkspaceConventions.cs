using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Configurable workspace naming conventions and mapping rules (topics, routing patterns, domains).
/// Loads optional custom rules from .codeexplorer/conventions.json if present in the workspace root.
/// </summary>
public static class WorkspaceConventions
{
    private static readonly ConcurrentDictionary<string, string> TopicAliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> ProjectToDomain = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> ServiceOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> DomainIcons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentBag<(Regex Regex, string Domain)> PatternToDomain = new();
    private static readonly ConcurrentBag<string> CustomRouteFunctions = new();
    private static readonly ConcurrentBag<string> CustomServicePrefixes = new();
    private static readonly ConcurrentDictionary<string, string> DatabaseAliases = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex DefaultRouteFunctionRegex = new(
        @"(?:resolveRoute|routeFor|serviceRoute|getRoute)\s*\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    private static readonly string[] GenericServicePrefixes =
    [
        "internal-service-",
        "integration-service-",
        "external-service-",
        "internal--",
        "integration--",
        "external--",
        "internal-",
        "integration-",
        "external-",
        "service-",
        "srv-"
    ];

    /// <summary>
    /// Loads custom conventions from .codeexplorer/conventions.json and .codeexplorer/domains.json if present.
    /// </summary>
    public static void LoadFromWorkspace(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot)) return;

        var configPath = Path.Combine(workspaceRoot, ".codeexplorer", "conventions.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);

                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("topics", out var topicsEl) &&
                        topicsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in topicsEl.EnumerateObject())
                        {
                            TopicAliases[prop.Name] = prop.Value.GetString() ?? prop.Name;
                        }
                    }

                    if (doc.RootElement.TryGetProperty("domains", out var domainsEl) &&
                        domainsEl.ValueKind == JsonValueKind.Object)
                    {
                        ParseDomainMappings(domainsEl);
                    }

                    if (doc.RootElement.TryGetProperty("overrides", out var ovrEl) &&
                        ovrEl.ValueKind == JsonValueKind.Object)
                    {
                        ParseOverrides(ovrEl);
                    }

                    if (doc.RootElement.TryGetProperty("service_prefixes", out var prefixesEl) &&
                        prefixesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in prefixesEl.EnumerateArray())
                        {
                            var prefix = p.GetString();
                            if (!string.IsNullOrWhiteSpace(prefix))
                            {
                                CustomServicePrefixes.Add(prefix.Trim().ToLowerInvariant());
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("route_functions", out var routesEl) &&
                        routesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var r in routesEl.EnumerateArray())
                        {
                            var fn = r.GetString();
                            if (!string.IsNullOrWhiteSpace(fn))
                            {
                                CustomRouteFunctions.Add(fn.Trim());
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("database_aliases", out var dbAliasesEl) &&
                        dbAliasesEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in dbAliasesEl.EnumerateObject())
                        {
                            var target = prop.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(target))
                            {
                                DatabaseAliases[prop.Name.Trim()] = target.Trim();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed custom convention files
            }
        }

        var domainsPath = Path.Combine(workspaceRoot, ".codeexplorer", "domains.json");
        if (File.Exists(domainsPath))
        {
            try
            {
                var json = File.ReadAllText(domainsPath);

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("domains", out var domainsEl))
                    {
                        if (domainsEl.ValueKind == JsonValueKind.Array)
                        {
                            ParseDomainDefinitionsArray(domainsEl);
                        }
                        else if (domainsEl.ValueKind == JsonValueKind.Object)
                        {
                            ParseDomainMappings(domainsEl);
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        ParseDomainDefinitionsArray(root);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        ParseDomainMappings(root);
                    }

                    if (root.TryGetProperty("overrides", out var ovrEl) &&
                        ovrEl.ValueKind == JsonValueKind.Object)
                    {
                        ParseOverrides(ovrEl);
                    }
                }
            }
            catch
            {
                // Ignore malformed custom domains files
            }
        }
    }

    private static void ParseOverrides(JsonElement overridesEl)
    {
        foreach (var prop in overridesEl.EnumerateObject())
        {
            var serviceName = prop.Name.Trim();
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var targetDomain = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(targetDomain))
                {
                    ServiceOverrides[serviceName] = targetDomain.Trim();
                    ProjectToDomain[serviceName] = targetDomain.Trim();
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                if (prop.Value.TryGetProperty("domain", out var domProp) && domProp.ValueKind == JsonValueKind.String)
                {
                    var targetDomain = domProp.GetString();
                    if (!string.IsNullOrWhiteSpace(targetDomain))
                    {
                        ServiceOverrides[serviceName] = targetDomain.Trim();
                        ProjectToDomain[serviceName] = targetDomain.Trim();
                    }
                }
            }
        }
    }

    public static void AddPattern(string globPattern, string domainName)
    {
        if (string.IsNullOrWhiteSpace(globPattern) || string.IsNullOrWhiteSpace(domainName)) return;
        try
        {
            var pattern = "^" + Regex.Escape(globPattern.Trim().Replace('\\', '/'))
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", @"[^/]*")
                .Replace(@"\?", ".") + "$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            PatternToDomain.Add((regex, domainName.Trim()));
        }
        catch
        {
            // Ignore invalid glob regexes
        }
    }

    private static void ParseDomainDefinitionsArray(JsonElement root)
    {
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            if (item.TryGetProperty("name", out var nameProp))
            {
                var domainName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(domainName)) continue;

                if (item.TryGetProperty("icon", out var iconProp))
                {
                    var icon = iconProp.GetString();
                    if (!string.IsNullOrWhiteSpace(icon))
                    {
                        DomainIcons[domainName.Trim()] = icon.Trim();
                    }
                }

                if (item.TryGetProperty("projects", out var projsProp) && projsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in projsProp.EnumerateArray())
                    {
                        var proj = p.GetString();
                        if (!string.IsNullOrWhiteSpace(proj))
                        {
                            ProjectToDomain[proj.Trim()] = domainName.Trim();
                        }
                    }
                }

                if (item.TryGetProperty("services", out var srvsProp) && srvsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in srvsProp.EnumerateArray())
                    {
                        var srv = s.GetString();
                        if (!string.IsNullOrWhiteSpace(srv))
                        {
                            ProjectToDomain[srv.Trim()] = domainName.Trim();
                        }
                    }
                }

                if (item.TryGetProperty("patterns", out var patsProp) && patsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pat in patsProp.EnumerateArray())
                    {
                        var patternStr = pat.GetString();
                        if (!string.IsNullOrWhiteSpace(patternStr))
                        {
                            AddPattern(patternStr, domainName.Trim());
                        }
                    }
                }
            }
        }
    }

    private static void ParseDomainMappings(JsonElement root)
    {
        foreach (var prop in root.EnumerateObject())
        {
            var domainName = prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                {
                    var proj = item.GetString();
                    if (!string.IsNullOrWhiteSpace(proj))
                    {
                        ProjectToDomain[proj.Trim()] = domainName;
                    }
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    ProjectToDomain[domainName] = val.Trim();
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                if (prop.Name.Equals("projects", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var projProp in prop.Value.EnumerateObject())
                    {
                        var assignedDomain = projProp.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(assignedDomain))
                        {
                            ProjectToDomain[projProp.Name.Trim()] = assignedDomain.Trim();
                        }
                    }
                }
                else
                {
                    if (prop.Value.TryGetProperty("patterns", out var pats) && pats.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pat in pats.EnumerateArray())
                        {
                            var patStr = pat.GetString();
                            if (!string.IsNullOrWhiteSpace(patStr))
                            {
                                AddPattern(patStr, domainName.Trim());
                            }
                        }
                    }
                    if (prop.Value.TryGetProperty("services", out var srvs) && srvs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var srv in srvs.EnumerateArray())
                        {
                            var srvStr = srv.GetString();
                            if (!string.IsNullOrWhiteSpace(srvStr))
                            {
                                ProjectToDomain[srvStr.Trim()] = domainName.Trim();
                            }
                        }
                    }
                    if (prop.Value.TryGetProperty("icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String)
                    {
                        var iconStr = iconProp.GetString();
                        if (!string.IsNullOrWhiteSpace(iconStr))
                        {
                            DomainIcons[domainName.Trim()] = iconStr.Trim();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resets all loaded convention aliases.
    /// </summary>
    public static void Clear()
    {
        TopicAliases.Clear();
        ProjectToDomain.Clear();
        ServiceOverrides.Clear();
        DomainIcons.Clear();
        PatternToDomain.Clear();
        CustomRouteFunctions.Clear();
        CustomServicePrefixes.Clear();
        DatabaseAliases.Clear();
    }

    /// <summary>
    /// Attempts to resolve a database dataset or table alias configured in conventions.json.
    /// </summary>
    public static bool TryGetDatabaseAlias(string name, out string alias)
    {
        return DatabaseAliases.TryGetValue(name, out alias!);
    }

    /// <summary>
    /// Attempts to resolve an aliased topic name configured in conventions.json.
    /// </summary>
    public static bool TryGetTopicAlias(string key, out string alias)
    {
        return TopicAliases.TryGetValue(key, out alias!);
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain icon from domains.json.
    /// </summary>
    public static bool TryGetDomainIcon(string domainName, out string icon)
    {
        return DomainIcons.TryGetValue(domainName, out icon!);
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain for a project name or path from domains.json or conventions.json.
    /// </summary>
    public static bool TryGetConfiguredDomain(string? projectName, string? filePath, out string domain)
    {
        domain = string.Empty;

        // 1. Explicit service override takes precedence
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var p = projectName.Trim();
            if (ServiceOverrides.TryGetValue(p, out var ovr))
            {
                domain = ovr;
                return true;
            }
            var clean = NormalizeServiceName(p);
            if (ServiceOverrides.TryGetValue(clean, out ovr))
            {
                domain = ovr;
                return true;
            }
        }

        // 2. Exact project name mapping
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var p = projectName.Trim();
            if (ProjectToDomain.TryGetValue(p, out var d))
            {
                domain = d;
                return true;
            }
            var clean = NormalizeServiceName(p);
            if (ProjectToDomain.TryGetValue(clean, out d))
            {
                domain = d;
                return true;
            }
        }

        // 3. Glob patterns against project name or file path
        var normPath = filePath?.Replace('\\', '/');
        if (!PatternToDomain.IsEmpty)
        {
            foreach (var (regex, targetDomain) in PatternToDomain)
            {
                if (!string.IsNullOrWhiteSpace(projectName) && regex.IsMatch(projectName))
                {
                    domain = targetDomain;
                    return true;
                }
                if (!string.IsNullOrEmpty(normPath) && regex.IsMatch(normPath))
                {
                    domain = targetDomain;
                    return true;
                }
            }
        }

        // 4. File path substring match in configured projects
        if (!string.IsNullOrEmpty(normPath))
        {
            foreach (var (key, d) in ProjectToDomain)
            {
                if (normPath.Contains("/" + key + "/", StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key + ".csproj", StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key + ".fsproj", StringComparison.OrdinalIgnoreCase))
                {
                    domain = d;
                    return true;
                }
            }
        }

        return false;
    }

    private static readonly Regex ValidTopicNameRegex = new(
        @"^[A-Za-z0-9][A-Za-z0-9_\-\.:/]*[A-Za-z0-9_]$|^[A-Za-z0-9]$",
        RegexOptions.Compiled);

    /// <summary>
    /// Checks whether an identifier, token, or string is a placeholder dummy name (e.g. QUEUE_NAME, TOPIC_NAME, etc.).
    /// </summary>
    public static bool IsPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var lower = name.Trim().Trim('\'', '"', '`').ToLowerInvariant().Replace('_', '-').Replace('.', '-');
        return lower is "topic" or "topic-name" or "topicname" or "topicid" or "topic-id" or "topic-name-or-id" or "topicnameorid"
            or "queue" or "queue-name" or "queuename" or "queueid" or "queue-id"
            or "sub" or "subid" or "sub-id" or "subname" or "sub-name" or "subscription" or "subscription-name" or "subscriptionname"
            or "subscriber" or "subscriber-name" or "subscribername" or "event-subscriber-name"
            or "default-topic" or "default-queue" or "default-sub-id" or "default-subscription-name"
            or "target" or "target-name" or "destination"
            or "message" or "msg" or "send-data" or "senddata" or "payload" or "data" or "body"
            or "exchange" or "exchangename" or "exchange-name" or "exchangekey" or "exchange-key" or "routingkey" or "routing-key"
            or "worker-name" or "workername"
            or "placeholder" or "dummy" or "test"
            or "undefined" or "null" or "string" or "void" or "any" or "unknown" or "never" or "object" or "boolean" or "number";
    }

    /// <summary>
    /// Strictly validates whether a string represents a valid messaging topic or queue identifier.
    /// Rejects code expressions, ternary statements, function calls, URLs, emojis, and placeholders.
    /// </summary>
    public static bool IsValidTopicOrQueueName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().Trim('\'', '"', '`');

        if (t.Length is < 2 or > 120) return false;

        // Disallow leading / trailing separators
        if (t.StartsWith(':') || t.StartsWith('/') || t.StartsWith('.') || t.StartsWith('-') || t.StartsWith('_'))
            return false;

        // Disallow URLs
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        // Disallow code syntax / operators / delimiters / whitespace
        if (t.IndexOfAny(['(', ')', '[', ']', '{', '}', ';', ',', '?', '=', '<', '>', '!', '|', '&', '+', '*', '%', '^', '~', '\\', '\'', '"', '`', ' ', '\t', '\r', '\n']) >= 0)
            return false;

        // Disallow placeholder and type names
        if (IsPlaceholderName(t)) return false;

        // Disallow code keywords and member prefix calls
        if (t.StartsWith("options.", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("config.", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("function", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("return", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("const", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("let", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("var", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must match valid topic/queue regex (alphanumeric, dot, underscore, hyphen, colon, slash)
        if (!ValidTopicNameRegex.IsMatch(t)) return false;

        return true;
    }

    /// <summary>
    /// Normalizes raw topic names or constants using configured aliases or algorithmic kebab-casing.
    /// </summary>
    public static string NormalizeTopicName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var t = raw.Trim().Trim('\'', '"', '`');

        if (!IsValidTopicOrQueueName(t))
        {
            return string.Empty;
        }

        if (TopicAliases.TryGetValue(t, out var mapped))
        {
            return mapped;
        }

        // Algorithmic fallback: convert SCREAMING_SNAKE_CASE or camelCase to kebab-case
        var normalized = AlgorithmicTopicKebabCase(t);
        return IsValidTopicOrQueueName(normalized) ? normalized : string.Empty;
    }

    /// <summary>
    /// Matches route resolution function calls (e.g. resolveRoute('billing'), routeFor('orders'),
    /// or custom route functions configured in conventions.json).
    /// </summary>
    public static bool TryMatchRouteFunction(string text, out string routeKey)
    {
        routeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = DefaultRouteFunctionRegex.Match(text);
        if (match.Success)
        {
            routeKey = match.Groups[1].Value;
            return true;
        }

        if (!CustomRouteFunctions.IsEmpty)
        {
            foreach (var fn in CustomRouteFunctions)
            {
                var m = Regex.Match(text, Regex.Escape(fn) + @"\s*\(\s*['""]([^'""]+)['""]");
                if (m.Success)
                {
                    routeKey = m.Groups[1].Value;
                    return true;
                }
            }
        }

        return false;
    }

    private static string AlgorithmicTopicKebabCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // If it already contains hyphens and lowercase letters, assume it's already a normalized topic
        if (name.Contains('-') && name.Any(char.IsLower)) return name;

        // Replace underscores with hyphens
        var clean = name.Replace('_', '-');

        // Split camelCase into words
        clean = Regex.Replace(clean, @"([a-z0-9])([A-Z])", "$1-$2");

        var lower = clean.ToLowerInvariant();

        // Strip trailing "-name" if it ends in "-topic-name" or "-sub-name"
        if (lower.EndsWith("-topic-name")) lower = lower[..^5];
        else if (lower.EndsWith("-sub-name")) lower = lower[..^5];

        return lower;
    }

    /// <summary>
    /// Normalizes service and project names: converts to lowercase and strictly strips prefixes
    /// such as "internal--", "integration--", "service-", "internal-service-", etc.
    /// </summary>
    public static string NormalizeServiceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = name.Trim().ToLowerInvariant();

        bool changed;
        do
        {
            changed = false;
            if (!CustomServicePrefixes.IsEmpty)
            {
                foreach (var p in CustomServicePrefixes)
                {
                    if (clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    {
                        clean = clean[p.Length..];
                        changed = true;
                        break;
                    }
                }
                if (changed) continue;
            }

            foreach (var p in GenericServicePrefixes)
            {
                if (clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[p.Length..];
                    changed = true;
                    break;
                }
            }
        } while (changed);

        clean = clean.Trim('-', '_');
        return string.IsNullOrEmpty(clean) ? name.Trim().ToLowerInvariant() : clean;
    }

    /// <summary>
    /// Converts a delimited or mixed-case string into PascalCase.
    /// </summary>
    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Default";
        var parts = text.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p[1..]);
            }
        }
        return sb.Length > 0 ? sb.ToString() : text;
    }

    /// <summary>
    /// Canonicalizes any raw domain, project name, or file path into a canonical DDD Problem Space (Macro-Domain).
    /// Uses user configuration, ontology roles, directory structure, or normalized service names.
    /// </summary>
    public static string CanonicalizeDomain(
        string? rawDomain,
        string? serviceOrProjectName = null,
        string? filePath = null,
        string? role = null)
    {
        // 1. Explicit user configuration (.codeexplorer/domains.json) takes absolute precedence
        if (TryGetConfiguredDomain(serviceOrProjectName ?? rawDomain, filePath, out var userConfigured))
        {
            return ToPascalCase(userConfigured);
        }

        // 2. Ontology role classification
        if (role is "SharedLibrary" or "Library") return "SharedKernel";
        if (role is "CliTool") return "DeveloperTooling";
        if (role is "Test") return "TestingInfrastructure";

        // 3. Technical file path heuristics
        var normPath = (filePath ?? "").Replace('\\', '/');
        if (!string.IsNullOrEmpty(normPath))
        {
            if (normPath.StartsWith("ui/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("client/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("web/", StringComparison.OrdinalIgnoreCase) ||
                normPath.Contains("/ui/") ||
                normPath.Contains("/components/") ||
                normPath.Contains("/packages/ui"))
            {
                return "UserInterface";
            }

            if (normPath.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("cli/", StringComparison.OrdinalIgnoreCase) ||
                normPath.Contains("/tools/") ||
                normPath.Contains("/cli/"))
            {
                return "DeveloperTooling";
            }

            if (normPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
                normPath.Contains("/tests/"))
            {
                return "TestingInfrastructure";
            }
        }

        // 4. If rawDomain is already provided and not a placeholder/generic word, use it
        var cleanRaw = (rawDomain ?? "").Trim();
        if (cleanRaw.StartsWith("domain:", StringComparison.OrdinalIgnoreCase))
            cleanRaw = cleanRaw["domain:".Length..];
        if (cleanRaw.StartsWith("dom:", StringComparison.OrdinalIgnoreCase))
            cleanRaw = cleanRaw["dom:".Length..];

        if (!string.IsNullOrWhiteSpace(cleanRaw))
        {
            var genericDomainWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "core", "service", "services", "app", "application", "default", "internal", "microservice",
                "table", "tables", "database", "sql", "boundedcontext", "context"
            };
            if (!genericDomainWords.Contains(cleanRaw))
            {
                return ToPascalCase(cleanRaw);
            }
        }

        // 5. Extract domain from directory structure only when an enclosing domain directory exists
        if (!string.IsNullOrEmpty(normPath))
        {
            var sanitizedPath = Regex.Replace(normPath, @"^[a-zA-Z]:[/]", "");
            var segments = sanitizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var skipRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "src", "services", "microservices", "apps", "packages", "libs", "modules", "projects", "cmd", "home", "users"
            };

            var meaningful = segments.Take(segments.Length - 1).Where(s => !skipRoots.Contains(s)).ToList();
            if (meaningful.Count >= 1)
            {
                var cleanDir = NormalizeServiceName(meaningful[0]);
                var dirPascal = ToPascalCase(cleanDir);
                if (dirPascal.Length > 2)
                {
                    return dirPascal;
                }
            }
        }

        // 6. Infer domain from normalized service or project name
        if (!string.IsNullOrWhiteSpace(serviceOrProjectName))
        {
            var cleanSvc = NormalizeServiceName(serviceOrProjectName);
            var genericDomainWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "core", "service", "services", "app", "application", "default", "internal", "microservice",
                "table", "tables", "database", "sql", "boundedcontext", "context"
            };
            if (!genericDomainWords.Contains(cleanSvc))
            {
                var svcPascal = ToPascalCase(cleanSvc);
                if (svcPascal.Length > 2)
                {
                    return svcPascal;
                }
            }
        }

        return "CoreDomain";
    }

    /// <summary>
    /// Formats a canonical PascalCase domain name into a clean, human-readable display name.
    /// E.g. "BillingAndPayments" -&gt; "Billing &amp; Payments", "OrderManagement" -&gt; "Order Management"
    /// </summary>
    public static string FormatDomainDisplayName(string canonicalDomain)
    {
        if (string.IsNullOrWhiteSpace(canonicalDomain)) return "Core Domain";
        var withSpaces = Regex.Replace(canonicalDomain, "([a-z])([A-Z])", "$1 $2");
        return withSpaces.Replace(" And ", " & ");
    }
}
