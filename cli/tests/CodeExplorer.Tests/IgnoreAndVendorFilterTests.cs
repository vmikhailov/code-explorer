using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.TypeScript;
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

            Assert.That(matcher.IsIgnored("Scripts/vendor/bundle.js", false), Is.True);
            Assert.That(matcher.IsIgnored("Scripts/vendor", true), Is.True);
            Assert.That(matcher.IsIgnored("Foo.generated.cs", false), Is.True);
            Assert.That(matcher.IsIgnored("Scripts/app.js", false), Is.False);
            Assert.That(matcher.IsIgnored("Foo.cs", false), Is.False);
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
            WorkspaceIndexer.Register(new JavaScriptParser());

            var indexer = new WorkspaceIndexer(client);
            var results = await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var filesQuery = "MATCH (f:File) RETURN f.path AS path";
            var queryResult = await client.ExecuteQueryAsync(filesQuery);

            Assert.That(queryResult, Contains.Substring("MyController.js"));
            Assert.That(queryResult, Does.Not.Contain("editor.js"));
            Assert.That(queryResult, Does.Not.Contain("app.d.ts"));
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

            Assert.That(matcher.IsIgnored("node_modules/express/index.js", false), Is.True);
            Assert.That(matcher.IsIgnored("bin/Debug/net10.0/app.dll", false), Is.True);
            Assert.That(matcher.IsIgnored("obj/Release/app.pdb", false), Is.True);
            Assert.That(matcher.IsIgnored(".next/cache/turbopack.js", false), Is.True);
            Assert.That(matcher.IsIgnored(".turbo/build.log", false), Is.True);
            Assert.That(matcher.IsIgnored("dist/bundle.js", false), Is.True);
            Assert.That(matcher.IsIgnored("src/app.ts", false), Is.False);
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
            Assert.That(matcher.IsIgnored("services/auth/local_cache/data.bin", false), Is.True);
            Assert.That(matcher.IsIgnored("services/auth/test.tmp", false), Is.True);

            // Should NOT be ignored outside services/auth
            Assert.That(matcher.IsIgnored("services/billing/local_cache/data.bin", false), Is.False);
            Assert.That(matcher.IsIgnored("root.tmp", false), Is.False);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
