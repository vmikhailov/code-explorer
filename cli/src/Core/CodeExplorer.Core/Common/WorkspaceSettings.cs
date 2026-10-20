using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Represents workspace-level scan and analysis settings stored in <c>.codeexplorer/settings.json</c>.
/// </summary>
public sealed class WorkspaceSettings
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Universal developer tooling / VCS directories that are always excluded regardless of connected parsers.
    /// </summary>
    public static readonly HashSet<string> DefaultExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vscode", ".idea", ".vs",
        "scratch", "demo", "third_party", "thirdparty", "3rdparty",
        ".codeexplorer", ".packages", ".agents", ".gemini", ".claude", ".cursor"
    };

    /// <summary>
    /// Gitignore/glob-style patterns or relative directory/file paths to exclude from scanning.
    /// </summary>
    [JsonPropertyName("exclude")]
    public List<string> Exclude { get; set; } = [];

    public static string GetSettingsPath(string workspaceRoot)
    {
        var ceDir = Path.Combine(workspaceRoot, WorkspaceLocator.FolderName);
        return Path.Combine(ceDir, FileName);
    }

    /// <summary>
    /// Loads <c>.codeexplorer/settings.json</c> from the specified workspace root.
    /// Returns an empty <see cref="WorkspaceSettings"/> instance if the file does not exist or cannot be parsed.
    /// </summary>
    public static WorkspaceSettings Load(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            return new WorkspaceSettings();

        var settingsPath = GetSettingsPath(workspaceRoot);
        if (!File.Exists(settingsPath))
            return new WorkspaceSettings();

        try
        {
            var json = File.ReadAllText(settingsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new WorkspaceSettings();

            var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json, ReadOptions) ?? new WorkspaceSettings();
            settings.Exclude = (settings.Exclude ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return settings;
        }
        catch
        {
            return new WorkspaceSettings();
        }
    }

    /// <summary>
    /// Saves the settings to <c>.codeexplorer/settings.json</c>.
    /// </summary>
    public static void Save(string workspaceRoot, WorkspaceSettings settings)
    {
        var settingsPath = GetSettingsPath(workspaceRoot);
        var ceDir = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(ceDir))
        {
            Directory.CreateDirectory(ceDir);
        }

        settings.Exclude = (settings.Exclude ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(settings, WriteOptions);
        File.WriteAllText(settingsPath, json + Environment.NewLine);
    }

    /// <summary>
    /// Ensures a default <c>.codeexplorer/settings.json</c> file exists without overwriting existing settings.
    /// </summary>
    public static string EnsureDefaultSettingsFile(string workspaceRoot)
    {
        var settingsPath = GetSettingsPath(workspaceRoot);
        if (!File.Exists(settingsPath))
        {
            try
            {
                Save(workspaceRoot, new WorkspaceSettings());
            }
            catch
            {
                // Best effort if concurrent initialization occurs
            }
        }
        return settingsPath;
    }

    /// <summary>
    /// Adds a relative path or glob pattern to <c>exclude</c> in <c>.codeexplorer/settings.json</c>.
    /// Returns true if the pattern was newly added, or false if it was already present.
    /// </summary>
    public static bool AddExcludePattern(string workspaceRoot, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        var normalized = NormalizeExcludePattern(workspaceRoot, pattern);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var settings = Load(workspaceRoot);
        if (settings.Exclude.Any(p => string.Equals(p.TrimEnd('/'), normalized.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        settings.Exclude.Add(normalized);
        Save(workspaceRoot, settings);
        return true;
    }

    public static string NormalizeExcludePattern(string workspaceRoot, string rawPathOrPattern)
    {
        var trimmed = rawPathOrPattern.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        // If an absolute path inside workspaceRoot was passed, convert it to workspace-relative
        if (Path.IsPathRooted(trimmed) && !trimmed.StartsWith('/'))
        {
            var fullRoot = Path.GetFullPath(workspaceRoot).Replace('\\', '/').TrimEnd('/');
            var fullTarget = Path.GetFullPath(trimmed).Replace('\\', '/');
            if (fullTarget.StartWithIC(fullRoot + "/"))
            {
                var rel = fullTarget[(fullRoot.Length + 1)..];
                if (Directory.Exists(trimmed) && !rel.EndsWith('/'))
                {
                    rel += "/";
                }
                return rel;
            }
        }

        var normalized = trimmed.Replace('\\', '/');
        var candidateFullPath = Path.Combine(workspaceRoot, normalized.TrimStart('/'));
        if (Directory.Exists(candidateFullPath) && !normalized.EndsWith('/') && !normalized.Contains('*'))
        {
            normalized += "/";
        }

        return normalized;
    }
}
