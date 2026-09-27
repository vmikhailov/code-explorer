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

    private static readonly Regex RouteFunctionRegex = new(
        @"(?:get(?:ServiceDomainBy)?Route|resolveRoute|routeFor|serviceRoute)\s*\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    /// <summary>
    /// Loads custom conventions from .codeexplorer/conventions.json if present.
    /// </summary>
    public static void LoadFromWorkspace(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot)) return;
        var configPath = Path.Combine(workspaceRoot, ".codeexplorer", "conventions.json");
        if (!File.Exists(configPath)) return;

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
        }
        catch
        {
            // Ignore malformed custom convention files
        }
    }

    /// <summary>
    /// Resets all loaded convention aliases.
    /// </summary>
    public static void Clear()
    {
        TopicAliases.Clear();
    }

    /// <summary>
    /// Normalizes raw topic names or constants using configured aliases or algorithmic kebab-casing.
    /// </summary>
    public static string NormalizeTopicName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var t = raw.Trim().Trim('\'', '"', '`');

        if (t.StartsWith(':') || t.Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("string", StringComparison.OrdinalIgnoreCase) || t.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("null", StringComparison.OrdinalIgnoreCase) || t.Equals("void", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var lowerCheck = t.ToLowerInvariant().Replace('_', '-');
        if (lowerCheck is "topic" or "topic-name" or "topic-id" or "queue" or "queue-name" or "sub" or "sub-id" or "subscription" or "subscriber" or "event-subscriber-name" or "pub-sub-subscription" ||
            lowerCheck.EndsWith("-sub-id") || lowerCheck.EndsWith("-subscription-name"))
        {
            return string.Empty;
        }

        if (TopicAliases.TryGetValue(t, out var mapped))
        {
            return mapped;
        }

        // Algorithmic fallback: convert SCREAMING_SNAKE_CASE or camelCase to kebab-case
        return AlgorithmicTopicKebabCase(t);
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
}
