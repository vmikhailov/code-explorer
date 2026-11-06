using System.Text.Json;
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
            var res = await client.ExecuteCypherAsync(query);

            res.Count.Should().Be(2, "Both usersLabel and itemsLabel must have CALLS relationships to pluralize");

            var usersRow = res.Rows.FirstOrDefault(r => r.GetStringProp("caller") == "usersLabel");
            usersRow.FieldCount.Should().BeGreaterThan(0, "usersLabel should call pluralize");
            usersRow.GetStringProp("def").Should().Contain("apps/admin/src/plural.ts", "usersLabel must resolve to apps/admin/src/plural.ts");

            var itemsRow = res.Rows.FirstOrDefault(r => r.GetStringProp("caller") == "itemsLabel");
            itemsRow.FieldCount.Should().BeGreaterThan(0, "itemsLabel should call pluralize");
            itemsRow.GetStringProp("def").Should().Contain("libs/text/src/index.ts", "itemsLabel must resolve to libs/text/src/index.ts");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Test]
    public async Task Test_CommonJsRequire_BarrelReExports_Callbacks_And_EntryPointReachability()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_cjs_reachability_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbFile = Path.Combine(tempDir, "graph.db");

        try
        {
            var srcDir = Path.Combine(tempDir, "src");
            var utilsDir = Path.Combine(srcDir, "utils");
            Directory.CreateDirectory(utilsDir);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), """
            {
              "name": "reachability-pkg",
              "main": "src/index.js",
              "scripts": {
                "postinstall": "node src/setup.js"
              }
            }
            """);

            await File.WriteAllTextAsync(Path.Combine(utilsDir, "math.js"), """
            function internalCompute(x) {
                return x * 2;
            }
            function callbackWorker(item) {
                return internalCompute(item);
            }
            exports.publicCompute = internalCompute;
            exports.callbackWorker = callbackWorker;
            """);

            await File.WriteAllTextAsync(Path.Combine(utilsDir, "index.js"), """
            module.exports = require('./math');
            """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "shared.js"), """
            function safeHelper() {
                return 42;
            }
            function directHelper() {
                return 99;
            }
            module.exports = { safeHelper, directHelper };
            """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "unrelated.js"), """
            function onItem(x) {
                return x;
            }
            function unrelatedGlobalCb(x) {
                return x;
            }
            """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "index.js"), """
            const shared = require('./shared');
            const { publicCompute: calc, callbackWorker } = require('./utils');

            function runApp(items) {
                shared.safeHelper();
                require('./shared').directHelper();
                items.map(callbackWorker);
                items.forEach(unrelatedGlobalCb);
                return calc(10);
            }

            module.exports = { runApp };
            """);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "setup.js"), """
            const { runApp } = require('./index');

            function bootstrap() {
                return runApp([1, 2, 3]);
            }

            (() => {
                bootstrap();
            })();
            """);

            await using var client = new SqliteGraphClient(dbFile);
            var indexer = new WorkspaceIndexer(client);

            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            // 1. CommonJS namespace require (`shared.safeHelper()`) & direct require (`require('./shared').directHelper()`)
            var runAppCallees = await client.ExecuteCypherAsync(
                "MATCH (f:Function {name: 'runApp'})-[:CALLS]->(target:Function) RETURN target.name AS callee, target.file_path AS file");
            var calleeNames = runAppCallees.Rows.Select(r => r.GetStringProp("callee")).ToList();

            calleeNames.Should().Contain("safeHelper", "CommonJS `shared.safeHelper()` should resolve to safeHelper");
            calleeNames.Should().Contain("directHelper", "Chained `require('./shared').directHelper()` should resolve to directHelper");

            // 2. Transitive barrel re-export + CJS export alias (`exports.publicCompute = internalCompute` via `./utils/index.js`)
            calleeNames.Should().Contain("internalCompute", "Aliased destructuring + CJS barrel re-export + export alias should resolve to internalCompute");

            // 3. Higher-order callback argument (`items.map(callbackWorker)`) should resolve to imported `callbackWorker`,
            //    while `items.forEach(unrelatedGlobalCb)` (not in same file or explicit imports) must NOT false-positive resolve!
            calleeNames.Should().Contain("callbackWorker", "Higher-order callback argument imported from ./utils should be resolved");
            calleeNames.Should().NotContain("unrelatedGlobalCb", "Callback argument must not fall back to unimported functions in other files");

            // 4. Top-level IIFE in `setup.js` -> `<module_init>` -> `bootstrap` -> `runApp` -> `internalCompute`,
            //    and `EntryPoint` (`postinstall`) -[:TRIGGERS]-> `<module_init>`
            var epChain = await client.ExecuteCypherAsync(
                "MATCH (ep:EntryPoint)-[:TRIGGERS]->(m:Function {name: '<module_init>'})-[:CALLS]->(b:Function {name: 'bootstrap'}) RETURN ep.name AS epName, m.file_path AS modFile");
            epChain.Count.Should().BeGreaterThan(0, "EntryPoint must trigger <module_init> which calls bootstrap");
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
