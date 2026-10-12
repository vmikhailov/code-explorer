using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Conventions.Services;

/// <summary>
/// Normalizes and validates messaging topics, queue names, and subscription identifiers.
/// </summary>
public sealed class MessagingConventionService
{
    private static readonly Regex ValidTopicNameRegex = new(
        @"^[A-Za-z0-9][A-Za-z0-9_\-\.:/]*[A-Za-z0-9_]$|^[A-Za-z0-9]$",
        RegexOptions.Compiled);

    private readonly Dictionary<string, string> _topicAliases;
    private readonly HashSet<string> _customPlaceholders;

    public MessagingConventionService(
        IDictionary<string, string>? topicAliases = null,
        IEnumerable<string>? customPlaceholders = null)
    {
        _topicAliases = new Dictionary<string, string>(topicAliases ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _customPlaceholders = new HashSet<string>(customPlaceholders ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to resolve an aliased topic name configured in conventions.json.
    /// </summary>
    public bool TryGetTopicAlias(string key, out string alias)
    {
        return _topicAliases.TryGetValue(key, out alias!);
    }

    /// <summary>
    /// Checks whether an identifier, token, or string is a placeholder dummy name (e.g. QUEUE_NAME, TOPIC_NAME, etc.).
    /// </summary>
    public bool IsPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var lower = name.Trim().Trim('\'', '"', '`').ToLowerInvariant().Replace('_', '-').Replace('.', '-');

        if (_customPlaceholders.Contains(lower) || _customPlaceholders.Contains(name.Trim()))
        {
            return true;
        }

        return lower is "topic" or "topic-name" or "topicname" or "topicid" or "topic-id" or "topic-name-or-id" or "topicnameorid"
            or "queue" or "queue-name" or "queuename" or "queueid" or "queue-id"
            or "sub" or "subid" or "sub-id" or "subname" or "sub-name" or "subscription" or "subscription-name" or "subscriptionname"
            or "subscriber" or "subscriber-name" or "subscribername" or "event-subscriber-name"
            or "default-topic" or "default-queue" or "default-sub-id" or "default-subscription-name"
            or "target" or "target-name" or "destination"
            or "message" or "msg" or "send-data" or "senddata" or "payload" or "data" or "body"
            or "exchange" or "exchangename" or "exchange-name" or "exchangekey" or "exchange-key" or "routingkey" or "routing-key"
            or "worker-name" or "workername"
            or "network" or "network-topic" or "networktopic"
            or "placeholder" or "dummy" or "test"
            or "undefined" or "null" or "string" or "void" or "any" or "unknown" or "never" or "object" or "boolean" or "number";
    }

    /// <summary>
    /// Strictly validates whether a string represents a valid messaging topic or queue identifier.
    /// Rejects code expressions, ternary statements, function calls, URLs, emojis, and placeholders.
    /// </summary>
    public bool IsValidTopicOrQueueName(string? s)
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
    public string NormalizeTopicName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var t = raw.Trim().Trim('\'', '"', '`');

        if (!IsValidTopicOrQueueName(t))
        {
            return string.Empty;
        }

        if (_topicAliases.TryGetValue(t, out var mapped))
        {
            return mapped;
        }

        // Algorithmic fallback: convert SCREAMING_SNAKE_CASE or camelCase to kebab-case
        var normalized = AlgorithmicTopicKebabCase(t);
        return IsValidTopicOrQueueName(normalized) ? normalized : string.Empty;
    }

    /// <summary>
    /// Algorithmically converts SCREAMING_SNAKE_CASE or camelCase into kebab-case.
    /// </summary>
    public static string AlgorithmicTopicKebabCase(string name)
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
