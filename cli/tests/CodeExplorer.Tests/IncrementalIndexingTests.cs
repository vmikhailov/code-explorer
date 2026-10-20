using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Incremental;
using CodeExplorer.Options;
using CommandLine;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class IncrementalIndexingTests
{
    [Test]
    public void Test_HashUtility_Normalizes_Crlf_And_Lf()
    {
        var crlf = System.Text.Encoding.UTF8.GetBytes("function add(a, b) {\r\n    return a + b;\r\n}\r\n");
        var lf = System.Text.Encoding.UTF8.GetBytes("function add(a, b) {\n    return a + b;\n}\n");

        var hashCrlf = HashUtility.ComputeSha256(crlf);
        var hashLf = HashUtility.ComputeSha256(lf);

        hashCrlf.Should().Be(hashLf);
        hashCrlf.Length.Should().Be(64);
    }

    [Test]
    public void Test_LogicOnlyChange_2Plus2_ProducesZeroStructuralChanges()
    {
        // Symbol before: body has "return 1 + 1;"
        var oldSymbol = new SymbolFootprint(
            SymbolId: "ws:sym:test.cs:Function:Calculate",
            QualifiedName: "Calculator.Calculate",
            Kind: "Function",
            SignatureHash: "sig_hash_int_calculate_int_a",
            BodyHash: "body_hash_1_plus_1",
            OutgoingCalls: ["Logger.Log"],
            OutgoingTypes: ["int"],
            StartLine: 10,
            EndLine: 15
        );

        // Symbol after: body has "return 2 + 2;", but signature and calls are identical
        var newSymbol = new SymbolFootprint(
            SymbolId: "ws:sym:test.cs:Function:Calculate",
            QualifiedName: "Calculator.Calculate",
            Kind: "Function",
            SignatureHash: "sig_hash_int_calculate_int_a",
            BodyHash: "body_hash_2_plus_2",
            OutgoingCalls: ["Logger.Log"],
            OutgoingTypes: ["int"],
            StartLine: 10,
            EndLine: 15
        );

        var oldSnapshot = new FileGraphSnapshot("test.cs", "hash1", DateTime.UtcNow.AddMinutes(-5),
            new Dictionary<string, SymbolFootprint> { [oldSymbol.SymbolId] = oldSymbol });

        var newSnapshot = new FileGraphSnapshot("test.cs", "hash2", DateTime.UtcNow,
            new Dictionary<string, SymbolFootprint> { [newSymbol.SymbolId] = newSymbol });

        var patch = SemanticGraphDiffer.ComputeDiff(oldSnapshot, newSnapshot);

        // CRITICAL: There must be ZERO structural changes in the graph!
        patch.HasGraphStructuralChanges.Should().BeFalse("Logic-only edit (2+2) should NOT produce any graph structural changes!");
        patch.AddedSymbols.Should().BeEmpty();
        patch.RemovedSymbols.Should().BeEmpty();
        patch.OutgoingCallDiffs.Should().BeEmpty();
        patch.OutgoingTypeDiffs.Should().BeEmpty();
        patch.LogicOnlyChangedSymbols.Should().HaveCount(1);
        patch.LogicOnlyChangedSymbols[0].SymbolId.Should().Be(oldSymbol.SymbolId);
    }

    [Test]
    public void Test_AddedCall_ProducesOutgoingCallDiffOnly()
    {
        var oldSymbol = new SymbolFootprint(
            SymbolId: "ws:sym:test.cs:Function:Submit",
            QualifiedName: "OrderService.Submit",
            Kind: "Function",
            SignatureHash: "sig_submit",
            BodyHash: "body_submit_v1",
            OutgoingCalls: ["_repo.Save"],
            OutgoingTypes: [],
            StartLine: 20,
            EndLine: 30
        );

        // New version adds a call to "_emailSender.SendNotification"
        var newSymbol = new SymbolFootprint(
            SymbolId: "ws:sym:test.cs:Function:Submit",
            QualifiedName: "OrderService.Submit",
            Kind: "Function",
            SignatureHash: "sig_submit",
            BodyHash: "body_submit_v2",
            OutgoingCalls: ["_repo.Save", "_emailSender.SendNotification"],
            OutgoingTypes: [],
            StartLine: 20,
            EndLine: 31
        );

        var oldSnapshot = new FileGraphSnapshot("test.cs", "hash1", DateTime.UtcNow.AddMinutes(-5),
            new Dictionary<string, SymbolFootprint> { [oldSymbol.SymbolId] = oldSymbol });

        var newSnapshot = new FileGraphSnapshot("test.cs", "hash2", DateTime.UtcNow,
            new Dictionary<string, SymbolFootprint> { [newSymbol.SymbolId] = newSymbol });

        var patch = SemanticGraphDiffer.ComputeDiff(oldSnapshot, newSnapshot);

        patch.HasGraphStructuralChanges.Should().BeTrue();
        patch.AddedSymbols.Should().BeEmpty();
        patch.RemovedSymbols.Should().BeEmpty();
        patch.OutgoingCallDiffs.Should().ContainKey(oldSymbol.SymbolId);

        var (addedCalls, removedCalls) = patch.OutgoingCallDiffs[oldSymbol.SymbolId];
        addedCalls.Should().Contain("_emailSender.SendNotification");
        removedCalls.Should().BeEmpty();
    }

    [Test]
    public void Test_LineShift_ProducesZeroGraphStructuralChanges()
    {
        var oldSymbol = new SymbolFootprint(
            SymbolId: "ws:sym:test.cs:Function:Foo",
            QualifiedName: "FooClass.Foo",
            Kind: "Function",
            SignatureHash: "sig_foo",
            BodyHash: "body_foo",
            OutgoingCalls: [],
            OutgoingTypes: [],
            StartLine: 10,
            EndLine: 20
        );

        // Empty line inserted at top of file, so lines shifted from 10-20 to 11-21
        var newSymbol = new SymbolFootprint(
            SymbolId: "ws:sym:test.cs:Function:Foo",
            QualifiedName: "FooClass.Foo",
            Kind: "Function",
            SignatureHash: "sig_foo",
            BodyHash: "body_foo",
            OutgoingCalls: [],
            OutgoingTypes: [],
            StartLine: 11,
            EndLine: 21
        );

        var oldSnapshot = new FileGraphSnapshot("test.cs", "hash1", DateTime.UtcNow.AddMinutes(-5),
            new Dictionary<string, SymbolFootprint> { [oldSymbol.SymbolId] = oldSymbol });

        var newSnapshot = new FileGraphSnapshot("test.cs", "hash2", DateTime.UtcNow,
            new Dictionary<string, SymbolFootprint> { [newSymbol.SymbolId] = newSymbol });

        var patch = SemanticGraphDiffer.ComputeDiff(oldSnapshot, newSnapshot);

        patch.HasGraphStructuralChanges.Should().BeFalse();
        patch.LineShiftedSymbols.Should().HaveCount(1);
        patch.LineShiftedSymbols[0].StartLine.Should().Be(11);
    }

    [Test]
    public void Test_FileRegistry_ComputeChangeset_DetectsAddedModifiedDeleted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_reg_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var file1 = Path.Combine(tempDir, "file1.cs");
            var file2 = Path.Combine(tempDir, "file2.cs");
            File.WriteAllText(file1, "content1");
            File.WriteAllText(file2, "content2");

            var registry = new FileRegistry();
            var lastMod1 = File.GetLastWriteTimeUtc(file1);
            var lastMod2 = File.GetLastWriteTimeUtc(file2);

            registry.UpdateEntry("file1.cs", HashUtility.ComputeSha256("content1"), lastMod1, "");
            registry.UpdateEntry("file2.cs", HashUtility.ComputeSha256("content2"), lastMod2, "");
            registry.UpdateEntry("old_deleted.cs", "hash_old", DateTime.UtcNow, "");

            // Modify file2, add file3, delete old_deleted.cs
            File.WriteAllText(file2, "content2_modified");
            File.SetLastWriteTimeUtc(file2, lastMod2.AddSeconds(2));
            var file3 = Path.Combine(tempDir, "file3.cs");
            File.WriteAllText(file3, "content3");

            var currentFiles = new[] { "file1.cs", "file2.cs", "file3.cs" };
            var changeset = registry.ComputeChangeset(tempDir, currentFiles);

            changeset.Added.Should().Contain("file3.cs");
            changeset.Modified.Should().Contain("file2.cs");
            changeset.Deleted.Should().Contain("old_deleted.cs");
            changeset.Modified.Should().NotContain("file1.cs");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_FileWatcher_DebouncesMultipleRapidEvents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_watch_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var batchesReceived = 0;
            var tcs = new TaskCompletionSource<bool>();

            using var watcher = new FileWatcher(tempDir, batch =>
            {
                Interlocked.Increment(ref batchesReceived);
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            }, debounceMs: 150);

            watcher.Start();

            // Simulate rapid edits (5 files created within 20ms)
            for (int i = 0; i < 5; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"file_{i}.cs"), $"content {i}");
                await Task.Delay(10);
            }

            // Wait for debounce timer to fire once
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
            completed.Should().Be(tcs.Task, "Watcher debounce timer should fire.");

            // Allow any extra trailing timer delay
            await Task.Delay(200);

            // Should be debounced into 1 batch (or max 2 depending on OS filesystem delay)
            batchesReceived.Should().BeGreaterThanOrEqualTo(1);

            batchesReceived.Should().BeLessThanOrEqualTo(2, "Events should be debounced into a single or very few batches.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TryBuildSnapshot_ExtractsMethodsAndReferencesFromCSharpSource()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_snapshot_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var csFile = Path.Combine(tempDir, "SampleService.cs");
            var code = """
                namespace MyNamespace;

                public class SampleService
                {
                    public void Process()
                    {
                        System.Console.WriteLine("Hello");
                    }
                }
                """;
            await File.WriteAllTextAsync(csFile, code);

            var bytes = await File.ReadAllBytesAsync(csFile);
            var hash = HashUtility.ComputeSha256(bytes);
            var lastMod = File.GetLastWriteTimeUtc(csFile);

            var snapshot = await WorkspaceIndexer.TryBuildSnapshotAsync(
                csFile, "SampleService.cs", "ws_test", tempDir, hash, lastMod);

            snapshot.Should().NotBeNull();
            snapshot!.Symbols.Count.Should().BeGreaterThanOrEqualTo(2, "Should extract Class and Method symbols");

            var methodSym = snapshot.Symbols.Values.FirstOrDefault(s => s.QualifiedName == "Process");
            methodSym.Should().NotBeNull();
            methodSym!.Kind.Should().Be("Function");
            methodSym.StartLine.Should().BeGreaterThan(0);
            methodSym.OutgoingCalls.Any(c => c is "System.Console.WriteLine" or "Console.WriteLine").Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_EndToEnd_LogicOnlyEdit_SkipsGraphChanges()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_logic_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var csFile = Path.Combine(tempDir, "Calculator.cs");
            var codeV1 = """
                namespace MathLib;

                public class Calculator
                {
                    public int Add(int a, int b)
                    {
                        return a + b;
                    }
                }
                """;

            var codeV2 = """
                namespace MathLib;

                public class Calculator
                {
                    public int Add(int a, int b)
                    {
                        // 2+2 internal logic modification without changing any calls or signatures
                        return 2 + 2;
                    }
                }
                """;

            await File.WriteAllTextAsync(csFile, codeV1);
            var bytes1 = await File.ReadAllBytesAsync(csFile);
            var hash1 = HashUtility.ComputeSha256(bytes1);
            var mod1 = File.GetLastWriteTimeUtc(csFile);
            var snapshot1 = await WorkspaceIndexer.TryBuildSnapshotAsync(csFile, "Calculator.cs", "ws_test", tempDir, hash1, mod1);

            await File.WriteAllTextAsync(csFile, codeV2);
            var bytes2 = await File.ReadAllBytesAsync(csFile);
            var hash2 = HashUtility.ComputeSha256(bytes2);
            var mod2 = File.GetLastWriteTimeUtc(csFile);
            var snapshot2 = await WorkspaceIndexer.TryBuildSnapshotAsync(csFile, "Calculator.cs", "ws_test", tempDir, hash2, mod2);

            snapshot1.Should().NotBeNull();
            snapshot2.Should().NotBeNull();

            var patch = SemanticGraphDiffer.ComputeDiff(snapshot1!, snapshot2!);

            patch.HasGraphStructuralChanges.Should().BeFalse("A 2+2 internal logic change must produce zero graph structural changes!");
            patch.AddedSymbols.Should().BeEmpty();
            patch.RemovedSymbols.Should().BeEmpty();
            patch.OutgoingCallDiffs.Should().BeEmpty();
            patch.OutgoingTypeDiffs.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_ScanOptions_Watch_Flag_Supported()
    {
        var parser = new CommandLine.Parser(with => with.CaseInsensitiveEnumValues = true);

        var resultScan = parser.ParseArguments<ScanOptions, IndexOptions>(["scan", "--watch"]);
        resultScan.Errors.Should().BeEmpty();
        ((ScanOptions)resultScan.Value).Watch.Should().BeTrue();

        var resultIndex = parser.ParseArguments<ScanOptions, IndexOptions>(["index", "--watch"]);
        resultIndex.Errors.Should().BeEmpty();
        ((IndexOptions)resultIndex.Value).Watch.Should().BeTrue();
    }

    [Test]
    public void Test_ScanOptions_Init_And_AutoInit_Flags_Supported()
    {
        var parser = new CommandLine.Parser(with => with.CaseInsensitiveEnumValues = true);

        var resInit = parser.ParseArguments<ScanOptions, IndexOptions>(["scan", "--init"]);
        resInit.Errors.Should().BeEmpty();
        ((ScanOptions)resInit.Value).Init.Should().BeTrue();
        ((ScanOptions)resInit.Value).ShouldAutoInit.Should().BeTrue();

        var resAutoInit = parser.ParseArguments<ScanOptions, IndexOptions>(["scan", "--auto-init"]);
        resAutoInit.Errors.Should().BeEmpty();
        ((ScanOptions)resAutoInit.Value).AutoInit.Should().BeTrue();
        ((ScanOptions)resAutoInit.Value).ShouldAutoInit.Should().BeTrue();
    }

    [Test]
    public void Test_GateOptions_Init_Scan_Clear_Flags_Supported()
    {
        var parser = new CommandLine.Parser(with => with.CaseInsensitiveEnumValues = true);

        var res = parser.ParseArguments<GateOptions>(["gate", "check", "--init", "--scan", "--clear"]);
        res.Errors.Should().BeEmpty();
        res.Value.Init.Should().BeTrue();
        res.Value.ShouldAutoInit.Should().BeTrue();
        res.Value.Scan.Should().BeTrue();
        res.Value.ShouldAutoScan.Should().BeTrue();
        res.Value.Clear.Should().BeTrue();

        var resAuto = parser.ParseArguments<GateOptions>(["gate", "check", "--auto-init", "--auto-scan"]);
        resAuto.Errors.Should().BeEmpty();
        resAuto.Value.AutoInit.Should().BeTrue();
        resAuto.Value.ShouldAutoInit.Should().BeTrue();
        resAuto.Value.AutoScan.Should().BeTrue();
        resAuto.Value.ShouldAutoScan.Should().BeTrue();
    }

    [Test]
    public async Task Test_TypeScript_NewlineInsertion_ZeroStructuralChanges()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ts_newline_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var tsFile = Path.Combine(tempDir, "response.ts");
            var codeV1 = """
                export class GetZoneMarginResponse {
                    id: string;
                    name: string;
                    calculate() {
                        const a = 1;
                        const b = 2;
                        return a + b;
                    }
                }
                """;

            var codeV2 = """
                export class GetZoneMarginResponse {
                    id: string;
                    name: string;
                    calculate() {
                        const a = 1;

                        const b = 2;
                        return a + b;
                    }
                }
                """;

            await File.WriteAllTextAsync(tsFile, codeV1);
            var bytes1 = await File.ReadAllBytesAsync(tsFile);
            var hash1 = HashUtility.ComputeSha256(bytes1);
            var mod1 = File.GetLastWriteTimeUtc(tsFile);
            var snapshot1 = await WorkspaceIndexer.TryBuildSnapshotAsync(tsFile, "response.ts", "ws_test", tempDir, hash1, mod1);

            await File.WriteAllTextAsync(tsFile, codeV2);
            var bytes2 = await File.ReadAllBytesAsync(tsFile);
            var hash2 = HashUtility.ComputeSha256(bytes2);
            var mod2 = File.GetLastWriteTimeUtc(tsFile);
            var snapshot2 = await WorkspaceIndexer.TryBuildSnapshotAsync(tsFile, "response.ts", "ws_test", tempDir, hash2, mod2);

            snapshot1.Should().NotBeNull();
            snapshot2.Should().NotBeNull();

            var patch = SemanticGraphDiffer.ComputeDiff(snapshot1!, snapshot2!);

            patch.HasGraphStructuralChanges.Should().BeFalse("Inserting a newline must produce ZERO graph structural changes!");
            patch.AddedSymbols.Should().BeEmpty();
            patch.RemovedSymbols.Should().BeEmpty();
            patch.OutgoingCallDiffs.Should().BeEmpty();
            patch.OutgoingTypeDiffs.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_IsCandidateSourceFile_Preserves_test_env_js_And_test_calculation_ts()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        // Files that MUST be recognized as candidate source files
        WorkspaceFileFilter.Default.IsCandidateSourceFile("bq-routes-calculation/test_env.js").Should().BeTrue("test_env.js should be recognized as a valid JavaScript file, not pruned as a test!");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("stage-worker/src/models/test_calculation.ts").Should().BeTrue("test_calculation.ts should be recognized as a valid TypeScript file, not pruned as a test!");

        // Test files are now first-class candidate source files
        WorkspaceFileFilter.Default.IsCandidateSourceFile("tests/test_calculator.py").Should().BeTrue("test_*.py should be included for test indexing");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("src/services/order.test.ts").Should().BeTrue("*.test.ts should be included for test indexing");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("src/services/order.spec.js").Should().BeTrue("*.spec.js should be included for test indexing");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("pkg/calc/calc_test.go").Should().BeTrue("*_test.go should be included for test indexing");

        // Monorepo packages directory MUST be recognized as candidate source files
        WorkspaceFileFilter.Default.IsCandidateSourceFile("packages/text/src/index.ts").Should().BeTrue("packages/* files in monorepos must NOT be skipped!");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("packages/ui/src/button.tsx").Should().BeTrue();
        WorkspaceFileFilter.Default.IsCandidateSourceFile("packages/common/index.js").Should().BeTrue();

        // Non-source files and mocks that MUST be skipped
        WorkspaceFileFilter.Default.IsCandidateSourceFile("src/mocks/mock_service.ts").Should().BeFalse("mock files must be skipped");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("src/bundle.min.js").Should().BeFalse("minified files must be skipped");
        WorkspaceFileFilter.Default.IsCandidateSourceFile("src/types.d.ts").Should().BeFalse("d.ts files must be skipped");
    }

    [Test]
    public void Test_WorkspaceFileFilter_PackagesDirectory_NotExcluded_ForMonorepos_And_Excluded_ForLegacyNuGet()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        // 1. packages should not be in general ExcludedDirectoryNames
        WorkspaceFileFilter.ExcludedDirectoryNames.Contains("packages").Should().BeFalse("packages is standard in JS/TS monorepos");
        WorkspaceFileFilter.Default.IsExcludedDirectory("packages").Should().BeFalse();

        // 2. Monorepo folder scenario
        var tempMonorepo = Path.Combine(Path.GetTempPath(), "ce_monorepo_test_" + Guid.NewGuid().ToString("N"));
        var pkgDir = Path.Combine(tempMonorepo, "packages", "text", "src");
        Directory.CreateDirectory(pkgDir);

        try
        {
            File.WriteAllText(Path.Combine(tempMonorepo, "pnpm-workspace.yaml"), "packages:\n  - 'packages/*'");
            File.WriteAllText(Path.Combine(tempMonorepo, "package.json"), "{\"name\":\"root\",\"private\":true}");
            File.WriteAllText(Path.Combine(tempMonorepo, "packages", "text", "package.json"), "{\"name\":\"@test/text\"}");
            var indexFile = Path.Combine(pkgDir, "index.ts");
            File.WriteAllText(indexFile, "export const hello = 'world';");

            var gitignore = new GitIgnoreMatcher(tempMonorepo);
            var candidates = WorkspaceFileFilter.Default.EnumerateCandidateFiles(tempMonorepo, tempMonorepo, gitignore);

            var normalizedCandidates = candidates.Select(c => c.Replace('\\', '/')).ToList();
            normalizedCandidates.Any(c => c.EndsWith("packages/text/src/index.ts")).Should().BeTrue(
                "Files under packages/ in a monorepo must be found by EnumerateCandidateFiles");

            WorkspaceFileFilter.Default.IsExcludedDirectory("packages", Path.Combine(tempMonorepo, "packages")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempMonorepo)) Directory.Delete(tempMonorepo, true);
        }

        // 3. Legacy NuGet cache scenario
        var tempNuGet = Path.Combine(Path.GetTempPath(), "ce_nuget_test_" + Guid.NewGuid().ToString("N"));
        var nugetPkgDir = Path.Combine(tempNuGet, "packages");
        Directory.CreateDirectory(nugetPkgDir);

        try
        {
            File.WriteAllText(Path.Combine(nugetPkgDir, "repositories.config"), "<?xml version=\"1.0\" encoding=\"utf-8\"?><repositories />");
            File.WriteAllText(Path.Combine(tempNuGet, "packages.config"), "<?xml version=\"1.0\" encoding=\"utf-8\"?><packages />");

            WorkspaceFileFilter.Default.IsExcludedDirectory("packages", nugetPkgDir).Should().BeTrue(
                "Legacy NuGet packages folder with repositories.config/packages.config should be excluded");
        }
        finally
        {
            if (Directory.Exists(tempNuGet)) Directory.Delete(tempNuGet, true);
        }
    }

    [Test]
    public async Task Test_Sqlite_Persists_AstSnapshot_And_SeparateProcessIncrementalScan_PreservesGraph_OnNewline()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_sqlite_incremental_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbFile = Path.Combine(tempDir, "graph.db");

        try
        {
            // Create a small TypeScript project
            await File.WriteAllTextAsync(Path.Combine(tempDir, "package.json"), "{\"name\": \"test-app\"}");
            var srcDir = Path.Combine(tempDir, "src");
            Directory.CreateDirectory(srcDir);
            var tsFile = Path.Combine(srcDir, "calculator.ts");
            var initialCode = """
                export class Calculator {
                    compute(x: number): number {
                        const a = 1;
                        return x + a;
                    }
                }
                """;
            await File.WriteAllTextAsync(tsFile, initialCode);

            // Phase 1: Full index with Client 1 (Process 1)
            var client1 = new Core.Database.SqliteGraphClient(dbFile);
            var indexer1 = new WorkspaceIndexer(client1);
            await indexer1.IndexAsync(tempDir, clear: false);

            // Verify SQLite file_registry table has entry and snapshot_json
            var registry1 = await client1.LoadFileRegistryAsync();
            registry1.Should().ContainKey("src/calculator.ts");
            var entry1 = registry1["src/calculator.ts"];
            entry1.SnapshotJson.Should().NotBeNullOrEmpty("SnapshotJson must be persisted in SQLite!");

            await client1.DisposeAsync();

            // Phase 2: User adds a newline in the file
            var modifiedCode = """
                export class Calculator {
                    compute(x: number): number {
                        const a = 1;

                        return x + a;
                    }
                }
                """;
            // Ensure timestamp shifts
            await Task.Delay(50);
            await File.WriteAllTextAsync(tsFile, modifiedCode);

            // Phase 3: Separate CLI process (Client 2, Indexer 2) runs incremental scan
            var client2 = new Core.Database.SqliteGraphClient(dbFile);
            var indexer2 = new WorkspaceIndexer(client2);

            var countsBefore = await client2.GetGraphCountsAsync();
            var changed = await indexer2.IndexIncrementalAsync(tempDir);
            changed.Should().BeTrue("Incremental scan should detect and process the modified file.");
            var countsAfter = await client2.GetGraphCountsAsync();
            countsAfter.NodesCount.Should().Be(countsBefore.NodesCount, "Graph nodes count must be preserved for logic-only edit!");

            // Second incremental scan without file edits: must detect 0 changes
            var changedAgain = await indexer2.IndexIncrementalAsync(tempDir);
            changedAgain.Should().BeFalse("Workspace should be up to date on subsequent check.");

            // Verify registry in SQLite was updated with new hash and preserved/updated snapshot
            var registry2 = await client2.LoadFileRegistryAsync();
            var entry2 = registry2["src/calculator.ts"];
            entry2.ContentHash.Should().NotBe(entry1.ContentHash, "Content hash should be updated");
            entry2.SnapshotJson.Should().NotBeNullOrEmpty("SnapshotJson must remain populated");

            await client2.DisposeAsync();
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

