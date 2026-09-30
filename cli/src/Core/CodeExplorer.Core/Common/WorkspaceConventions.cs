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

    private static readonly Regex RouteFunctionRegex = new(
        @"(?:get(?:ServiceDomainBy)?Route|resolveRoute|routeFor|serviceRoute)\s*\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled);

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
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("topics", out var topicsEl) && topicsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in topicsEl.EnumerateObject())
                    {
                        TopicAliases[prop.Name] = prop.Value.GetString() ?? prop.Name;
                    }
                }

                if (doc.RootElement.TryGetProperty("domains", out var domainsEl) && domainsEl.ValueKind == JsonValueKind.Object)
                {
                    ParseDomainMappings(domainsEl);
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
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("domains", out var nestedDomains) && nestedDomains.ValueKind == JsonValueKind.Object)
                {
                    ParseDomainMappings(nestedDomains);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    ParseDomainMappings(root);
                }
            }
            catch
            {
                // Ignore malformed custom domains files
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
            else if (prop.Value.ValueKind == JsonValueKind.Object && prop.Name.Equals("projects", StringComparison.OrdinalIgnoreCase))
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
        }
    }

    /// <summary>
    /// Resets all loaded convention aliases.
    /// </summary>
    public static void Clear()
    {
        TopicAliases.Clear();
        ProjectToDomain.Clear();
    }

    /// <summary>
    /// Attempts to resolve an aliased topic name configured in conventions.json.
    /// </summary>
    public static bool TryGetTopicAlias(string key, out string alias)
    {
        return TopicAliases.TryGetValue(key, out alias!);
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain for a project name or path from domains.json or conventions.json.
    /// </summary>
    public static bool TryGetConfiguredDomain(string? projectName, string? filePath, out string domain)
    {
        domain = string.Empty;
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

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var normPath = filePath.Replace('\\', '/');
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
            or "undefined" or "null" or "string" or "void" or "any" or "unknown" or "never" or "object" or "boolean" or "number"
            or "appmodule" or "app-module" or "other" or "broken";
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
    /// Matches route resolution function calls (e.g., getServiceDomainByRoute('auth'), resolveRoute('billing')).
    /// </summary>
    public static bool TryMatchRouteFunction(string text, out string routeKey)
    {
        routeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = RouteFunctionRegex.Match(text);
        if (match.Success)
        {
            routeKey = match.Groups[1].Value;
            return true;
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
            if (clean.StartsWith("internal--"))
            {
                clean = clean["internal--".Length..];
                changed = true;
            }
            else if (clean.StartsWith("integration--"))
            {
                clean = clean["integration--".Length..];
                changed = true;
            }
            else if (clean.StartsWith("external--"))
            {
                clean = clean["external--".Length..];
                changed = true;
            }
            else if (clean.StartsWith("internal-service-"))
            {
                clean = clean["internal-service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("integration-service-"))
            {
                clean = clean["integration-service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("external-service-"))
            {
                clean = clean["external-service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("service-"))
            {
                clean = clean["service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("internal-"))
            {
                clean = clean["internal-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("integration-"))
            {
                clean = clean["integration-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("external-"))
            {
                clean = clean["external-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("srv-"))
            {
                clean = clean["srv-".Length..];
                changed = true;
            }
        } while (changed);

        clean = clean.Trim('-', '_');
        return string.IsNullOrEmpty(clean) ? name.Trim().ToLowerInvariant() : clean;
    }
}
