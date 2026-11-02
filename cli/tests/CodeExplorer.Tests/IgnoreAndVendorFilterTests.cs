using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class IgnoreAndVendorFilterTests
{
    [Test]
    public void Test_GitIgnoreMatcher_LoadsCodeExplorerIgnore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ignore_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".codeexplorerignore"), """
# Custom ignore
Scripts/vendor/
*.generated.cs
""");

            var matcher = new GitIgnoreMatcher(tempDir);

            matcher.IsIgnored("Scripts/vendor/bundle.js", false).Should().BeTrue();
            matcher.IsIgnored("Scripts/vendor", true).Should().BeTrue();
            matcher.IsIgnored("Foo.generated.cs", false).Should().BeTrue();
            matcher.IsIgnored("Scripts/app.js", false).Should().BeFalse();
            matcher.IsIgnored("Foo.cs", false).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_LibManAndDeclarationFiltering_InLayer1Scan()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_libman_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var scriptsDir = Path.Combine(tempDir, "Scripts");
            var monacoDir = Path.Combine(scriptsDir, "monaco-editor");
            var customDir = Path.Combine(scriptsDir, "ViewControllers");
            Directory.CreateDirectory(monacoDir);
            Directory.CreateDirectory(customDir);

            // libman.json registering monaco-editor as library destination
            await File.WriteAllTextAsync(Path.Combine(tempDir, "libman.json"), """
{
  "libraries": [
    {
      "library": "monaco-editor@0.55.1",
      "destination": "Scripts/monaco-editor"
    }
  ]
}
""");

            // Vendor file in monaco-editor (should be skipped by libman)
            await File.WriteAllTextAsync(Path.Combine(monacoDir, "editor.js"), "function monacoInternal() {}");
            // Declaration file (should be skipped by .d.ts rule)
            await File.WriteAllTextAsync(Path.Combine(customDir, "app.d.ts"), "declare module 'foo' {}");
            // Custom controller file (should be scanned)
            await File.WriteAllTextAsync(Path.Combine(customDir, "MyController.js"), "class MyController {}");

            var dbPath = Path.Combine(tempDir, "test_graph.db");

            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();

            var indexer = new WorkspaceIndexer(client);
            var results = await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var filesQuery = "MATCH (f:File) RETURN f.path AS path";
            var queryResult = await client.ExecuteQueryAsync(filesQuery);

            queryResult.Should().Contain("MyController.js");
            queryResult.Should().NotContain("editor.js");
            queryResult.Should().NotContain("app.d.ts");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_GitIgnoreMatcher_BuiltInSafeDefaults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_defaults_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // No .gitignore or .codeexplorerignore created
            var matcher = new GitIgnoreMatcher(tempDir);

            matcher.IsIgnored("node_modules/express/index.js", false).Should().BeTrue();
            matcher.IsIgnored("bin/Debug/net10.0/app.dll", false).Should().BeTrue();
            matcher.IsIgnored("obj/Release/app.pdb", false).Should().BeTrue();
            matcher.IsIgnored(".next/cache/turbopack.js", false).Should().BeTrue();
            matcher.IsIgnored(".turbo/build.log", false).Should().BeTrue();
            matcher.IsIgnored("dist/bundle.js", false).Should().BeTrue();
            matcher.IsIgnored("src/app.ts", false).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_GitIgnoreMatcher_NestedScopedIgnoreFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_nested_ignore_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var nestedDir = Path.Combine(tempDir, "services", "auth");
            Directory.CreateDirectory(nestedDir);

            // Nested .gitignore inside services/auth
            var nestedIgnore = Path.Combine(nestedDir, ".gitignore");
            File.WriteAllText(nestedIgnore, "local_cache/\n*.tmp");

            var matcher = new GitIgnoreMatcher(tempDir);
            matcher.LoadScopedFile(nestedIgnore, "services/auth");

            // Should be ignored inside services/auth
            matcher.IsIgnored("services/auth/local_cache/data.bin", false).Should().BeTrue();
            matcher.IsIgnored("services/auth/test.tmp", false).Should().BeTrue();

            // Should NOT be ignored outside services/auth
            matcher.IsIgnored("services/billing/local_cache/data.bin", false).Should().BeFalse();
            matcher.IsIgnored("root.tmp", false).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_GitIgnoreMatcher_NegationPattern_ReincludesDirectoryAndFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_negation_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gitignore"), """
**/lib/
!packages/ui/src/lib/
""");

            var matcher = new GitIgnoreMatcher(tempDir);

            // Re-included directory and child file
            matcher.IsIgnored("packages/ui/src/lib", true).Should().BeFalse();
            matcher.IsIgnored("packages/ui/src/lib/utils.ts", false).Should().BeFalse();

            // Non-negated directory matching **/lib/ and child file
            matcher.IsIgnored("other/package/lib", true).Should().BeTrue();
            matcher.IsIgnored("other/package/lib/utils.ts", false).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_GitIgnoreMatcher_RuleOrdering_LaterRuleOverridesEarlier()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ordering_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gitignore"), """
*.log
!important.log
!overridden.txt
overridden.txt
""");

            var matcher = new GitIgnoreMatcher(tempDir);

            // Later negation un-ignores
            matcher.IsIgnored("debug.log", false).Should().BeTrue();
            matcher.IsIgnored("important.log", false).Should().BeFalse();

            // Later positive pattern re-ignores
            matcher.IsIgnored("overridden.txt", false).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_GitIgnoreMatcher_AncestorDirectoryWithDescendantNegation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ancestor_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gitignore"), """
packages/
!packages/ui/src/lib/
""");

            var matcher = new GitIgnoreMatcher(tempDir);

            // Ancestor path must not be pruned because a later rule negates a path underneath it
            matcher.IsIgnored("packages", true).Should().BeFalse();
            matcher.IsIgnored("packages/ui", true).Should().BeFalse();
            matcher.IsIgnored("packages/ui/src", true).Should().BeFalse();
            matcher.IsIgnored("packages/ui/src/lib", true).Should().BeFalse();
            matcher.IsIgnored("packages/ui/src/lib/utils.ts", false).Should().BeFalse();

            // Another folder in packages/ without negation MUST be ignored
            matcher.IsIgnored("packages/other", true).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_Layer1Scan_NegatedDirectory_IndexesReincludedFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_scan_negation_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Minimal reproduction setup from Issue #5
            await File.WriteAllTextAsync(Path.Combine(tempDir, ".gitignore"), """
**/lib/
!packages/ui/src/lib/
""");

            var reincludedDir = Path.Combine(tempDir, "packages", "ui", "src", "lib");
            var excludedLibDir = Path.Combine(tempDir, "apps", "web", "src", "lib");
            Directory.CreateDirectory(reincludedDir);
            Directory.CreateDirectory(excludedLibDir);

            await File.WriteAllTextAsync(Path.Combine(reincludedDir, "utils.ts"), "export const cn = (...xs: string[]) => xs.join(' ');");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "packages", "ui", "src", "index.ts"), "export { cn } from './lib/utils';");
            await File.WriteAllTextAsync(Path.Combine(excludedLibDir, "internal.ts"), "export const internalHelper = 1;");

            var dbPath = Path.Combine(tempDir, "test_graph.db");
            await using var client = new SqliteGraphClient(dbPath);

            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var queryResult = await client.ExecuteQueryAsync("MATCH (f:File) RETURN f.path AS path");

            // packages/ui/src/lib/utils.ts must be indexed
            queryResult.Should().Contain("packages/ui/src/lib/utils.ts");
            queryResult.Should().Contain("packages/ui/src/index.ts");

            // apps/web/src/lib/internal.ts must be skipped
            queryResult.Should().NotContain("apps/web/src/lib/internal.ts");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_WorkspaceFileFilter_DynamicExclusions_FromConnectedParsers_WithDeduplication()
    {
        var csParser = new Parser.CSharp.CSharpParser();
        var tsParser = new Parser.TypeScript.TypeScriptParser();
        var pyParser = new Parser.Python.PythonParser();

        // 1. C# only filter
        var csOnlyFilter = new WorkspaceFileFilter([csParser], []);
        csOnlyFilter.ActiveExcludedDirectoryNames.Should().Contain("bin");
        csOnlyFilter.ActiveExcludedDirectoryNames.Should().Contain("obj");
        csOnlyFilter.ActiveExcludedDirectoryNames.Should().Contain(".git");
        csOnlyFilter.ActiveExcludedDirectoryNames.Should().NotContain("node_modules");
        csOnlyFilter.ActiveExcludedDirectoryNames.Should().NotContain("__pycache__");
        csOnlyFilter.IsManifestFile("app.csproj").Should().BeTrue();
        csOnlyFilter.IsManifestFile("package.json").Should().BeFalse();

        // 2. TypeScript only filter
        var tsOnlyFilter = new WorkspaceFileFilter([tsParser], []);
        tsOnlyFilter.ActiveExcludedDirectoryNames.Should().Contain("node_modules");
        tsOnlyFilter.ActiveExcludedDirectoryNames.Should().Contain("dist");
        tsOnlyFilter.ActiveExcludedDirectoryNames.Should().Contain(".git");
        tsOnlyFilter.ActiveExcludedDirectoryNames.Should().NotContain("bin");
        tsOnlyFilter.ActiveExcludedDirectoryNames.Should().NotContain("obj");
        tsOnlyFilter.IsManifestFile("package.json").Should().BeTrue();
        tsOnlyFilter.IsManifestFile("app.csproj").Should().BeFalse();

        // 3. Combined C#, TS, and Python filter - verifying deduplication & combined exclusions
        var combinedFilter = new WorkspaceFileFilter([csParser, tsParser, pyParser], []);
        combinedFilter.ActiveExcludedDirectoryNames.Should().Contain("bin");
        combinedFilter.ActiveExcludedDirectoryNames.Should().Contain("obj");
        combinedFilter.ActiveExcludedDirectoryNames.Should().Contain("node_modules");
        combinedFilter.ActiveExcludedDirectoryNames.Should().Contain("__pycache__");
        combinedFilter.ActiveExcludedDirectoryNames.Should().Contain("venv");
        combinedFilter.ActiveExcludedDirectoryNames.Should().Contain(".git");

        // Deduplication check: every item in ActiveExcludedDirectoryNames is unique (case-insensitive)
        var count = combinedFilter.ActiveExcludedDirectoryNames.Count;
        var distinctCount = combinedFilter.ActiveExcludedDirectoryNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        count.Should().Be(distinctCount);

        combinedFilter.IsManifestFile("app.csproj").Should().BeTrue();
        combinedFilter.IsManifestFile("package.json").Should().BeTrue();
        combinedFilter.IsManifestFile("requirements.txt").Should().BeTrue();
        combinedFilter.IsManifestFile("unknown.txt").Should().BeFalse();
    }
}
