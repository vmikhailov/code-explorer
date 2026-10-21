using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Shared;

internal static partial class KeyValueSyntax
{
    [GeneratedRegex(@"^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_.]*)\s*=\s*(.*)$")]
    public static partial Regex EnvLine();

    [GeneratedRegex(@"^\s*([A-Za-z0-9_.\-]+)\s*[:=]\s*(.*)$")]
    public static partial Regex PropertyLine();

    public static string Unquote(string raw) => raw.Trim().Trim('"', '\'', '`');
}

/// <summary>.env, .env.local, .env.production, dev.env, ...</summary>
public sealed class EnvFileParser : ISharedFileParser
{
    public string Name => "dotenv";
    public IReadOnlyCollection<string> Extensions { get; } = [".env"];
    public IReadOnlyCollection<string> FileNamePrefixes { get; } = [".env"];

    public IProjectEnrichment? Parse(SharedFileContext file)
    {
        var values = new List<ConfigValue>();
        foreach (var line in file.Content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var match = KeyValueSyntax.EnvLine().Match(trimmed);
            if (!match.Success) continue;

            values.Add(new ConfigValue(match.Groups[1].Value.Trim(), UnquoteValue(match.Groups[2].Value)));
        }

        return new EnvironmentInformation(file.FullPath, values);
    }

    internal static string UnquoteValue(string raw)
    {
        var v = raw.Trim();
        if (v.Length >= 2 && (v[0] == '"' || v[0] == '\''))
        {
            var end = v.IndexOf(v[0], 1);
            return end > 0 ? v[1..end] : v[1..];
        }

        var hash = v.IndexOf(" #", StringComparison.Ordinal);
        return (hash >= 0 ? v[..hash] : v).Trim();
    }
}

/// <summary>appsettings*.json and any other JSON configuration, flattened to Section:Key paths.</summary>
public sealed class JsonConfigurationParser : ISharedFileParser
{
    private static readonly System.Text.Json.JsonDocumentOptions Options = new()
    {
        CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string Name => "json-configuration";
    public IReadOnlyCollection<string> Extensions { get; } = [".json"];

    public bool CanParse(string fileName) =>
        fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase);

    public IProjectEnrichment? Parse(SharedFileContext file)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(file.Content, Options);
        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;

        var values = new List<ConfigValue>();
        Traverse(doc.RootElement, "", values);
        return new ConfigurationFileInformation(file.FullPath, "json", values);
    }

    private static void Traverse(System.Text.Json.JsonElement element, string prefix, List<ConfigValue> values)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    Traverse(prop.Value, string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}", values);
                }
                break;
            case System.Text.Json.JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Traverse(item, $"{prefix}:{index++}", values);
                }
                break;
            case System.Text.Json.JsonValueKind.String:
            case System.Text.Json.JsonValueKind.Number:
            case System.Text.Json.JsonValueKind.True:
            case System.Text.Json.JsonValueKind.False:
                values.Add(new ConfigValue(prefix, element.ToString() ?? ""));
                break;
        }
    }
}

/// <summary>Spring-style application.properties and TOML key/value files.</summary>
public sealed class PropertiesConfigurationParser : ISharedFileParser
{
    public string Name => "properties-configuration";
    public IReadOnlyCollection<string> Extensions { get; } = [".properties", ".toml"];

    // Cargo / tooling manifests are project manifests, not application configuration.
    private static readonly HashSet<string> ExcludedToml = new(StringComparer.OrdinalIgnoreCase)
    {
        "cargo.toml", "pyproject.toml", "rustfmt.toml", "clippy.toml", "deny.toml",
        "release.toml", "cross.toml", "taplo.toml"
    };

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            ? !ExcludedToml.Contains(fileName)
            : fileName.StartsWith("application", StringComparison.OrdinalIgnoreCase);

    public IProjectEnrichment? Parse(SharedFileContext file)
    {
        var values = new List<ConfigValue>();
        foreach (var line in file.Content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('!')) continue;

            var match = KeyValueSyntax.PropertyLine().Match(trimmed);
            if (!match.Success) continue;

            values.Add(new ConfigValue(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim().Trim('"', '\'')));
        }

        return new ConfigurationFileInformation(file.FullPath, "properties", values);
    }
}

/// <summary>Indentation-based YAML configuration (application.yml, ...).</summary>
public sealed class YamlConfigurationParser : ISharedFileParser
{
    public string Name => "yaml-configuration";
    public IReadOnlyCollection<string> Extensions { get; } = [".yml", ".yaml"];

    public bool CanParse(string fileName) =>
        fileName.StartsWith("application", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase);

    public IProjectEnrichment? Parse(SharedFileContext file)
    {
        var values = new List<ConfigValue>();
        var stack = new List<(int Indent, string Key)>();

        foreach (var rawLine in file.Content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            var trimmedStart = line.TrimStart();
            if (trimmedStart.StartsWith('#')) continue;

            if (trimmedStart.StartsWith("- "))
            {
                var itemText = trimmedStart[2..].Trim().Trim('"', '\'');
                var envMatch = KeyValueSyntax.EnvLine().Match(itemText);
                if (envMatch.Success)
                {
                    values.Add(new ConfigValue(envMatch.Groups[1].Value.Trim(), envMatch.Groups[2].Value.Trim().Trim('"', '\'')));
                    continue;
                }
            }

            var indent = line.Length - trimmedStart.Length;
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
                stack.Add((indent, key));
            }
            else
            {
                var fullKey = stack.Count > 0 ? string.Join(":", stack.Select(s => s.Key)) + ":" + key : key;
                values.Add(new ConfigValue(fullKey, afterColon.Trim('"', '\'')));
            }
        }

        return new ConfigurationFileInformation(file.FullPath, "yaml", values);
    }
}
