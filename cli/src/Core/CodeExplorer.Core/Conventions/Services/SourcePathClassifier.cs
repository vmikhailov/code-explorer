namespace CodeExplorer.Core.Conventions.Services;

/// <summary>
/// Source path topology and categorization service (test files, spec files, conventions).
/// </summary>
public static class SourcePathClassifier
{
    /// <summary>
    /// Determines whether a source file path represents a unit test, integration test, or spec file.
    /// </summary>
    public static bool IsTestFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        return normalized.Contains("/test/") ||
               normalized.Contains("/tests/") ||
               normalized.Contains("/__tests__/") ||
               normalized.Contains("/testing/") ||
               normalized.Contains("/specs/") ||
               normalized.Contains("/spec/") ||
               normalized.EndsWith(".spec.ts") ||
               normalized.EndsWith(".spec.js") ||
               normalized.EndsWith(".spec.jsx") ||
               normalized.EndsWith(".spec.tsx") ||
               normalized.EndsWith(".test.ts") ||
               normalized.EndsWith(".test.js") ||
               normalized.EndsWith(".test.jsx") ||
               normalized.EndsWith(".test.tsx") ||
               normalized.EndsWith("_test.go") ||
               normalized.EndsWith(".tests.cs") ||
               normalized.EndsWith("tests.cs") ||
               normalized.EndsWith("_test.py") ||
               normalized.EndsWith("test_.py");
    }
}
