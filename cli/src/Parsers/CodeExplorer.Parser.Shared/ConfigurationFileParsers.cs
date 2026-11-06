using System.Text.RegularExpressions;

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
    private static readonly System.Text.Json.JsonReaderOptions Options = new()
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
        var bytes = System.Text.Encoding.UTF8.GetBytes(file.Content);
        var reader = new System.Text.Json.Utf8JsonReader(bytes, Options);
        if (!reader.Read() || reader.TokenType != System.Text.Json.JsonTokenType.StartObject) return null;

        var values = new List<ConfigValue>();
        TraverseObject(ref reader, "", values);
        return new ConfigurationFileInformation(file.FullPath, "json", values);
    }

    private static void TraverseObject(ref System.Text.Json.Utf8JsonReader reader, string prefix, List<ConfigValue> values)
    {
        while (reader.Read())
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject) return;
            if (reader.TokenType != System.Text.Json.JsonTokenType.PropertyName) continue;

            var propName = reader.GetString() ?? "";
            var nextPrefix = string.IsNullOrEmpty(prefix) ? propName : $"{prefix}:{propName}";
            if (!reader.Read()) return;
            TraverseValue(ref reader, nextPrefix, values);
        }
    }

    private static void TraverseArray(ref System.Text.Json.Utf8JsonReader reader, string prefix, List<ConfigValue> values)
    {
        var index = 0;
        while (reader.Read())
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.EndArray) return;
            TraverseValue(ref reader, $"{prefix}:{index++}", values);
        }
    }

    private static void TraverseValue(ref System.Text.Json.Utf8JsonReader reader, string prefix, List<ConfigValue> values)
    {
        switch (reader.TokenType)
        {
            case System.Text.Json.JsonTokenType.StartObject:
                TraverseObject(ref reader, prefix, values);
                break;
            case System.Text.Json.JsonTokenType.StartArray:
                TraverseArray(ref reader, prefix, values);
                break;
            case System.Text.Json.JsonTokenType.String:
                values.Add(new ConfigValue(prefix, reader.GetString() ?? ""));
                break;
            case System.Text.Json.JsonTokenType.Number:
                values.Add(new ConfigValue(prefix, System.Text.Encoding.UTF8.GetString(reader.ValueSpan)));
                break;
            case System.Text.Json.JsonTokenType.True:
            {
                const string trueConfigVal = "True";
                values.Add(new ConfigValue(prefix, trueConfigVal));
                break;
            }
            case System.Text.Json.JsonTokenType.False:
            {
                const string falseConfigVal = "False";
                values.Add(new ConfigValue(prefix, falseConfigVal));
                break;
            }
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
            : fileName.EndsWith(".properties", StringComparison.OrdinalIgnoreCase) &&
              !fileName.StartsWith("gradle", StringComparison.OrdinalIgnoreCase);

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

    private static readonly HashSet<string> ExcludedYaml = new(StringComparer.OrdinalIgnoreCase)
    {
        ".travis.yml", ".travis.yaml",
        ".gitlab-ci.yml", ".gitlab-ci.yaml",
        "azure-pipelines.yml", "azure-pipelines.yaml",
        "dependabot.yml", "dependabot.yaml",
        ".codecov.yml", ".codecov.yaml",
        ".pre-commit-config.yaml", ".pre-commit-config.yml"
    };

    public bool CanParse(string fileName) =>
        (fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
         fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)) &&
        !ExcludedYaml.Contains(fileName);

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
