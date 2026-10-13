using System.Text.Json;
using AwesomeAssertions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TypeScriptImportCallResolutionTests
{
    [Test]
    public async Task Test_TypeScriptCalls_ResolvedThroughImports_NotAmbiguousFunctionName()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var tempDir = Path.Combine(Path.GetTempPath(), "ts_calls_import_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbFile = Path.Combine(tempDir, "graph.db");

        try
        {
            // Root package.json and pnpm-workspace.yaml
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), "{\"name\": \"root\", \"private\": true}");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "pnpm-workspace.yaml"), "packages:\n  - 'libs/*'\n  - 'apps/*'");

            // 1. libs/text: package "@repro/text"
            var textDir = Path.Combine(tempDir, "libs", "text", "src");
            Directory.CreateDirectory(textDir);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "libs", "text", "package.json"),
                "{\"name\": \"@repro/text\", \"version\": \"1.0.0\", \"main\": \"src/index.ts\"}");
            await File.WriteAllTextAsync(
                Path.Combine(textDir, "index.ts"),
                "export function pluralize(n: number, one: string, many: string): string { return n === 1 ? one : many; }");

            // 2. apps/admin: local function and caller
            var adminDir = Path.Combine(tempDir, "apps", "admin", "src");
            Directory.CreateDirectory(adminDir);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "apps", "admin", "package.json"),
                "{\"name\": \"admin\", \"version\": \"1.0.0\"}");
            await File.WriteAllTextAsync(
                Path.Combine(adminDir, "plural.ts"),
                "export function pluralize(n: number, one: string, many: string): string { return n === 1 ? one : many; }");
            await File.WriteAllTextAsync(
                Path.Combine(adminDir, "local.ts"),
                "import { pluralize } from \"./plural\";\nexport function usersLabel(n: number) { return pluralize(n, \"user\", \"users\"); }");

            // 3. apps/web: caller importing from @repro/text
            var webDir = Path.Combine(tempDir, "apps", "web", "src");
            Directory.CreateDirectory(webDir);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "apps", "web", "package.json"),
                "{\"name\": \"web\", \"version\": \"1.0.0\", \"dependencies\": { \"@repro/text\": \"workspace:*\" }}");
            await File.WriteAllTextAsync(
                Path.Combine(webDir, "page.ts"),
                "import { pluralize } from \"@repro/text\";\nexport function itemsLabel(n: number) { return pluralize(n, \"item\", \"items\"); }");

            // Run indexer
            await using var client = new SqliteGraphClient(dbFile);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            // Execute Cypher query:
            // MATCH (t:Function)<-[:CALLS]-(d) WHERE t.name = 'pluralize'
            // RETURN t.symbol AS def, d.name AS caller
            var query = "MATCH (t:Function)<-[:CALLS]-(d) WHERE t.name = 'pluralize' RETURN t.symbol AS def, d.name AS caller";
            var json = await client.ExecuteQueryAsync(query);

            using var doc = JsonDocument.Parse(json);
            var rows = doc.RootElement.EnumerateArray().ToList();

            rows.Count.Should().Be(2, "Both usersLabel and itemsLabel must have CALLS relationships to pluralize");

            var usersRow = rows.FirstOrDefault(r => r.GetProperty("caller").GetString() == "usersLabel");
            usersRow.ValueKind.Should().NotBe(JsonValueKind.Undefined, "usersLabel should call pluralize");
            usersRow.GetProperty("def").GetString().Should().Contain("apps/admin/src/plural.ts", "usersLabel must resolve to apps/admin/src/plural.ts");

            var itemsRow = rows.FirstOrDefault(r => r.GetProperty("caller").GetString() == "itemsLabel");
            itemsRow.ValueKind.Should().NotBe(JsonValueKind.Undefined, "itemsLabel should call pluralize");
            itemsRow.GetProperty("def").GetString().Should().Contain("libs/text/src/index.ts", "itemsLabel must resolve to libs/text/src/index.ts");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
