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
            var queryResult = (await client.ExecuteCypherAsync(filesQuery)).ToJson();

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

            var queryResult = (await client.ExecuteCypherAsync("MATCH (f:File) RETURN f.path AS path")).ToJson();

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
        var csParser = new Parsers.CSharp.CSharpProjectParser();
        var tsParser = new Parsers.TypeScript.TypeScriptProjectParser();
        var pyParser = new Parsers.Python.PythonProjectParser();

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

    [Test]
    public async Task Test_ProjectScopedLayer1Scan_SkipsGitWorktrees_PrunesEmptyFolders_AndDiscoversNestedProjects()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_proj_scoped_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // 1. Unowned structural folders outside all projects (should collect 0 JSON/YAML dumps and prune empty folders)
            var docsPerfDir = Path.Combine(tempDir, "docs", "perf");
            Directory.CreateDirectory(docsPerfDir);
            await File.WriteAllTextAsync(Path.Combine(docsPerfDir, "lighthouse-report.json"), "{\"score\": 99}");
            await File.WriteAllTextAsync(Path.Combine(docsPerfDir, "notes.md"), "# Perf notes");

            // 2. Parent project (vscode-extension) with a nested project (webview) + an empty docs folder inside it
            var extDir = Path.Combine(tempDir, "vscode-extension");
            var extSrcDir = Path.Combine(extDir, "src");
            var extDocsDir = Path.Combine(extDir, "docs");
            var webviewDir = Path.Combine(extDir, "webview");
            var webviewSrcDir = Path.Combine(webviewDir, "src");
            Directory.CreateDirectory(extSrcDir);
            Directory.CreateDirectory(extDocsDir);
            Directory.CreateDirectory(webviewSrcDir);

            await File.WriteAllTextAsync(Path.Combine(extDir, "package.json"), """{"name": "my-vscode-ext", "workspaces": ["webview"]}""");
            await File.WriteAllTextAsync(Path.Combine(extSrcDir, "extension.ts"), "export function activate() {}");
            await File.WriteAllTextAsync(Path.Combine(extDocsDir, "fixture-dump.json"), "{\"items\": [1, 2, 3]}");
            await File.WriteAllTextAsync(Path.Combine(extDir, "pnpm-lock.yaml"), "lockfileVersion: '9.0'\n");

            await File.WriteAllTextAsync(Path.Combine(webviewDir, "package.json"), """{"name": "my-webview", "dependencies": {"react": "^19.0.0"}}""");
            await File.WriteAllTextAsync(Path.Combine(webviewSrcDir, "App.tsx"), "export function App() { return null; }");

            // 3. Nested linked Git worktree inside workspace (must be completely skipped)
            var worktreeDir = Path.Combine(tempDir, "tmp-sandboxes", "0d5542783c1d652905121d814a33609f");
            Directory.CreateDirectory(Path.Combine(worktreeDir, "src"));
            await File.WriteAllTextAsync(Path.Combine(worktreeDir, ".git"), "gitdir: /repo/.git/worktrees/0d5542783c1d652905121d814a33609f\n");
            await File.WriteAllTextAsync(Path.Combine(worktreeDir, "package.json"), """{"name": "cloned-worktree"}""");
            await File.WriteAllTextAsync(Path.Combine(worktreeDir, "src", "ClonedClone.ts"), "export class ClonedClone {}");

            var dbPath = Path.Combine(tempDir, "test_graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            WorkspaceIndexer.AutoRegisterParsers();

            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var projectsJson = (await client.ExecuteCypherAsync("MATCH (p:Project) RETURN p.name AS name")).ToJson();
            projectsJson.Should().Contain("vscode-extension");
            projectsJson.Should().Contain("webview");
            projectsJson.Should().NotContain("cloned-worktree");
            projectsJson.Should().NotContain("0d5542783c1d652905121d814a33609f");

            var filesJson = (await client.ExecuteCypherAsync("MATCH (f:File) RETURN f.path AS path")).ToJson();
            filesJson.Should().Contain("vscode-extension/src/extension.ts");
            filesJson.Should().Contain("vscode-extension/webview/src/App.tsx");
            filesJson.Should().NotContain("lighthouse-report.json");
            filesJson.Should().NotContain("fixture-dump.json");
            filesJson.Should().NotContain("pnpm-lock.yaml");
            filesJson.Should().NotContain("ClonedClone.ts");

            var foldersJson = (await client.ExecuteCypherAsync("MATCH (fl:Folder) RETURN fl.name AS name")).ToJson();
            foldersJson.Should().NotContain("perf");
            foldersJson.Should().NotContain("docs");
            foldersJson.Should().NotContain("tmp-sandboxes");

            // Immediate incremental scan should see 0 changes (file_registry populated in single pass)
            var changed = await indexer.IndexIncrementalAsync(tempDir, tempDir);
            changed.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
