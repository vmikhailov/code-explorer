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

    [Test]
    public void Test_GitIgnoreTrieMatcher_100PercentParityWithLegacyMatcher()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_trie_parity_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".gitignore"), """
# Root ignore rules
Scripts/vendor/
*.generated.cs
*.log
!important.log
!overridden.txt
overridden.txt
**/lib/
!packages/ui/src/lib/
packages/legacy/
!packages/legacy/keep/
npm-debug.log*
.env.*
build-*/*.tmp
""");
            var nestedAuthDir = Path.Combine(tempDir, "services", "auth");
            Directory.CreateDirectory(nestedAuthDir);
            var nestedIgnore = Path.Combine(nestedAuthDir, ".gitignore");
            File.WriteAllText(nestedIgnore, """
local_cache/
*.secret
!public.secret
snapshots/*.snap
""");

            var legacy = new GitIgnoreMatcher(tempDir);
            legacy.LoadScopedFile(nestedIgnore, "services/auth");

            var trie = new GitIgnoreTrieMatcher(tempDir);
            trie.LoadScopedFile(nestedIgnore, "services/auth");

            (string Path, bool IsDir)[] testCases =
            [
                ("Scripts/vendor", true),
                ("Scripts/vendor/bundle.js", false),
                ("Scripts/app.js", false),
                ("Foo.generated.cs", false),
                ("Foo.cs", false),
                ("node_modules/express/index.js", false),
                ("bin/Debug/net10.0/app.dll", false),
                ("obj/Release/app.pdb", false),
                ("dist/bundle.js", false),
                ("src/app.ts", false),
                ("debug.log", false),
                ("important.log", false),
                ("overridden.txt", false),
                ("packages/ui/src/lib", true),
                ("packages/ui/src/lib/utils.ts", false),
                ("other/package/lib", true),
                ("other/package/lib/utils.ts", false),
                ("packages/legacy", true),
                ("packages/legacy/keep", true),
                ("packages/legacy/keep/file.ts", false),
                ("packages/legacy/drop", true),
                ("npm-debug.log.1", false),
                (".env.local", false),
                ("build-prod/out.tmp", false),
                ("services/auth/local_cache", true),
                ("services/auth/local_cache/data.bin", false),
                ("services/billing/local_cache/data.bin", false),
                ("services/auth/db.secret", false),
                ("services/auth/public.secret", false),
                ("services/auth/snapshots/login.snap", false),
                ("services/billing/snapshots/login.snap", false)
            ];

            foreach (var (path, isDir) in testCases)
            {
                if (isDir)
                {
                    var legacyIgnored = legacy.IsIgnoredDirectory(path, out var legacyKept);
                    var trieIgnored = trie.IsIgnoredDirectory(path, out var trieKept);
                    trieIgnored.Should().Be(legacyIgnored, $"directory '{path}' IsIgnoredDirectory mismatch");
                    trieKept.Should().Be(legacyKept, $"directory '{path}' keptForLaterNegation mismatch");
                }
                else
                {
                    var legacyIgnoredFull = legacy.IsIgnoredFile(path, skipDirectoryOnlyRules: false);
                    var trieIgnoredFull = trie.IsIgnoredFile(path, skipDirectoryOnlyRules: false);
                    trieIgnoredFull.Should().Be(legacyIgnoredFull, $"file '{path}' (skipDirOnly=false) mismatch");

                    var legacyIgnoredSkipDir = legacy.IsIgnoredFile(path, skipDirectoryOnlyRules: true);
                    var trieIgnoredSkipDir = trie.IsIgnoredFile(path, skipDirectoryOnlyRules: true);
                    trieIgnoredSkipDir.Should().Be(legacyIgnoredSkipDir, $"file '{path}' (skipDirOnly=true) mismatch");
                }
            }

            // Also verify incremental GitIgnoreScopeChain compilation parity for subtree paths
            var authChain = trie.CompileChildScope(trie.RootScope, "services/auth", nestedIgnore, null);
            trie.IsIgnoredDirectoryInScope(authChain, "services/auth/local_cache", out _).Should().BeTrue();
            trie.IsIgnoredFileInScope(authChain, "services/auth/db.secret", skipDirectoryOnlyRules: true).Should().BeTrue();
            trie.IsIgnoredFileInScope(authChain, "services/auth/public.secret", skipDirectoryOnlyRules: true).Should().BeFalse();
            trie.IsIgnoredFileInScope(trie.RootScope, "services/billing/db.secret", skipDirectoryOnlyRules: true).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Benchmark_GitIgnoreTrieMatcher_Vs_LegacyRegexMatcher_On700Projects44kFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_trie_bench_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            var legacy = new GitIgnoreMatcher(tempDir);
            var trie = new GitIgnoreTrieMatcher(tempDir);

            // Root .gitignore with 40 realistic rules
            string[] rootRules =
            [
                "*.log", "*.tmp", "*.bak", "*.swp", "*.pyc", "*.pyo", "*.pyd", "*.so", "*.dylib", "*.dll",
                "*.exe", "*.o", "*.a", "*.class", "*.jar", "*.war", "*.ear", "*.zip", "*.tar.gz", "*.rar",
                "*.generated.cs", "*.g.cs", "*.g.i.cs", "*.designer.cs", "*.min.js", "*.bundle.js",
                "npm-debug.log*", "yarn-debug.log*", "yarn-error.log*", "pnpm-debug.log*", ".env.*",
                "coverage/", ".nyc_output/", ".cache/", ".parcel-cache/", ".turbo/", ".vercel/",
                "docs/internal/drafts/", "scripts/local/tmp/", "!scripts/local/tmp/keep.sh"
            ];
            foreach (var r in rootRules)
            {
                legacy.AddPattern(r);
                trie.AddPattern(r);
            }

            // 100 scoped project .gitignore files across 700 projects (15 rules each = 1,500 scoped rules)
            for (var p = 0; p < 100; p++)
            {
                var scope = $"repos/project-{p:D3}";
                string[] scopedRules =
                [
                    "dist/", "build/", "out/", ".next/", "coverage/", "temp/",
                    "*.tsbuildinfo", "*.local", "*.snap", "*.orig",
                    "src/generated/", "!src/generated/schema.ts",
                    "test-results/", "playwright-report/", "cypress/videos/"
                ];
                foreach (var sr in scopedRules)
                {
                    legacy.AddPattern(sr, scope);
                    trie.AddPattern(sr, scope);
                }
            }

            // Generate 44,000 realistic file paths and 7,000 directory paths across 700 projects
            var filePaths = new string[44000];
            for (var i = 0; i < filePaths.Length; i++)
            {
                var projIdx = i % 700;
                var modIdx = (i / 10) % 20;
                filePaths[i] = (i % 12) switch
                {
                    0 => $"repos/project-{projIdx:D3}/src/modules/mod-{modIdx}/service-{i}.ts",
                    1 => $"repos/project-{projIdx:D3}/src/modules/mod-{modIdx}/controller-{i}.cs",
                    2 => $"repos/project-{projIdx:D3}/src/components/View-{i}.tsx",
                    3 => $"repos/project-{projIdx:D3}/src/domain/Entity-{i}.go",
                    4 => $"repos/project-{projIdx:D3}/src/generated/Client-{i}.generated.cs",
                    5 => $"repos/project-{projIdx:D3}/dist/assets/app-{i}.min.js",
                    6 => $"repos/project-{projIdx:D3}/logs/server-{i}.log",
                    7 => $"repos/project-{projIdx:D3}/tests/unit/spec-{i}.spec.ts",
                    8 => $"repos/project-{projIdx:D3}/src/repositories/Repo-{i}.py",
                    9 => $"repos/project-{projIdx:D3}/src/generated/schema.ts",
                    10 => $"repos/project-{projIdx:D3}/.env.development",
                    _ => $"repos/project-{projIdx:D3}/src/handlers/Handler-{i}.java"
                };
            }

            var dirPaths = new string[7000];
            for (var i = 0; i < dirPaths.Length; i++)
            {
                var projIdx = i % 700;
                dirPaths[i] = (i % 7) switch
                {
                    0 => $"repos/project-{projIdx:D3}/src",
                    1 => $"repos/project-{projIdx:D3}/src/modules",
                    2 => $"repos/project-{projIdx:D3}/node_modules",
                    3 => $"repos/project-{projIdx:D3}/dist",
                    4 => $"repos/project-{projIdx:D3}/src/generated",
                    5 => $"repos/project-{projIdx:D3}/tests/unit",
                    _ => $"repos/project-{projIdx:D3}/src/components"
                };
            }

            // Warmup & verify parity on sample
            for (var i = 0; i < 500; i++)
            {
                var lRes = legacy.IsIgnoredFile(filePaths[i], skipDirectoryOnlyRules: true);
                var tRes = trie.IsIgnoredFile(filePaths[i], skipDirectoryOnlyRules: true);
                tRes.Should().Be(lRes, $"file '{filePaths[i]}' parity mismatch");
            }

            var swLegacy = System.Diagnostics.Stopwatch.StartNew();
            var legacyIgnoredCount = 0;
            for (var i = 0; i < dirPaths.Length; i++)
            {
                if (legacy.IsIgnoredDirectory(dirPaths[i], out _)) legacyIgnoredCount++;
            }
            for (var i = 0; i < filePaths.Length; i++)
            {
                if (legacy.IsIgnoredFile(filePaths[i], skipDirectoryOnlyRules: true)) legacyIgnoredCount++;
            }
            swLegacy.Stop();

            var swTrie = System.Diagnostics.Stopwatch.StartNew();
            var trieIgnoredCount = 0;
            for (var i = 0; i < dirPaths.Length; i++)
            {
                if (trie.IsIgnoredDirectory(dirPaths[i], out _)) trieIgnoredCount++;
            }
            for (var i = 0; i < filePaths.Length; i++)
            {
                if (trie.IsIgnoredFile(filePaths[i], skipDirectoryOnlyRules: true)) trieIgnoredCount++;
            }
            swTrie.Stop();

            var speedup = swLegacy.Elapsed.TotalMilliseconds / Math.Max(0.01, swTrie.Elapsed.TotalMilliseconds);
            TestContext.Out.WriteLine(
                $"[GitIgnore Benchmark: 700 projects, 100 scoped .gitignore files (~1,580 rules), 7,000 dirs + 44,000 files]\n" +
                $"  Legacy Regex GitIgnoreMatcher : {swLegacy.Elapsed.TotalMilliseconds:F2} ms (ignored={legacyIgnoredCount})\n" +
                $"  Zero-Regex GitIgnoreTrieMatcher: {swTrie.Elapsed.TotalMilliseconds:F2} ms (ignored={trieIgnoredCount})\n" +
                $"  Speedup                        : {speedup:F2}x faster");

            trieIgnoredCount.Should().Be(legacyIgnoredCount);
            swTrie.Elapsed.Should().BeLessThan(swLegacy.Elapsed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
