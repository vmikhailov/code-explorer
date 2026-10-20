using System.Collections.Concurrent;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Registry mapping TreeSitter language names (e.g. "c-sharp", "typescript", "go", "python", "java")
/// to their respective LanguageSyntaxProfile.
/// Enables universal, language-agnostic AST analysis and expression evaluation without switch statements.
/// </summary>
public static class LanguageSyntaxProfileRegistry
{
    private static readonly ConcurrentDictionary<string, LanguageSyntaxProfile> _profiles =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string languageName, LanguageSyntaxProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(languageName) && profile != null)
        {
            _profiles[languageName.Trim()] = profile;
        }
    }

    public static LanguageSyntaxProfile Get(string? languageName)
    {
        if (string.IsNullOrWhiteSpace(languageName)) return LanguageSyntaxProfile.Empty;
        return _profiles.TryGetValue(languageName.Trim(), out var p) ? p : LanguageSyntaxProfile.Empty;
    }
}
