using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Incremental;
using CodeExplorer.Options;
using CodeExplorer.Parser.CSharp;
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

        Assert.That(hashCrlf, Is.EqualTo(hashLf));
        Assert.That(hashCrlf.Length, Is.EqualTo(64));
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
        Assert.That(patch.HasGraphStructuralChanges, Is.False, "Logic-only edit (2+2) should NOT produce any graph structural changes!");
        Assert.That(patch.AddedSymbols, Is.Empty);
        Assert.That(patch.RemovedSymbols, Is.Empty);
        Assert.That(patch.OutgoingCallDiffs, Is.Empty);
        Assert.That(patch.OutgoingTypeDiffs, Is.Empty);
        Assert.That(patch.LogicOnlyChangedSymbols, Has.Count.EqualTo(1));
        Assert.That(patch.LogicOnlyChangedSymbols[0].SymbolId, Is.EqualTo(oldSymbol.SymbolId));
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

        Assert.That(patch.HasGraphStructuralChanges, Is.True);
        Assert.That(patch.AddedSymbols, Is.Empty);
        Assert.That(patch.RemovedSymbols, Is.Empty);
        Assert.That(patch.OutgoingCallDiffs, Contains.Key(oldSymbol.SymbolId));

        var (addedCalls, removedCalls) = patch.OutgoingCallDiffs[oldSymbol.SymbolId];
        Assert.That(addedCalls, Does.Contain("_emailSender.SendNotification"));
        Assert.That(removedCalls, Is.Empty);
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

        Assert.That(patch.HasGraphStructuralChanges, Is.False);
        Assert.That(patch.LineShiftedSymbols, Has.Count.EqualTo(1));
        Assert.That(patch.LineShiftedSymbols[0].StartLine, Is.EqualTo(11));
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

            Assert.That(changeset.Added, Does.Contain("file3.cs"));
            Assert.That(changeset.Modified, Does.Contain("file2.cs"));
            Assert.That(changeset.Deleted, Does.Contain("old_deleted.cs"));
            Assert.That(changeset.Modified, Does.Not.Contain("file1.cs"));
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

            using var watcher = new CodeExplorer.Core.Parser.FileWatcher(tempDir, batch =>
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
            Assert.That(completed, Is.EqualTo(tcs.Task), "Watcher debounce timer should fire.");

            // Allow any extra trailing timer delay
            await Task.Delay(200);

            // Should be debounced into 1 batch (or max 2 depending on OS filesystem delay)
            Assert.That(batchesReceived, Is.GreaterThanOrEqualTo(1));

            Assert.That(batchesReceived, Is.LessThanOrEqualTo(2),
                "Events should be debounced into a single or very few batches.");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_TryBuildSnapshot_ExtractsMethodsAndReferencesFromCSharpSource()
    {
        WorkspaceIndexer.Register(new CSharpParser());

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

            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.Symbols.Count, Is.GreaterThanOrEqualTo(2), "Should extract Class and Method symbols");

            var methodSym = snapshot.Symbols.Values.FirstOrDefault(s => s.QualifiedName == "Process");
            Assert.That(methodSym, Is.Not.Null);
            Assert.That(methodSym!.Kind, Is.EqualTo("Function"));
            Assert.That(methodSym.StartLine, Is.GreaterThan(0));
            Assert.That(methodSym.OutgoingCalls, Does.Contain("System.Console.WriteLine").Or.Contain("Console.WriteLine"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_EndToEnd_LogicOnlyEdit_SkipsGraphChanges()
    {
        WorkspaceIndexer.Register(new CSharpParser());

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

            Assert.That(snapshot1, Is.Not.Null);
            Assert.That(snapshot2, Is.Not.Null);

            var patch = SemanticGraphDiffer.ComputeDiff(snapshot1!, snapshot2!);

            Assert.That(patch.HasGraphStructuralChanges, Is.False,
                "A 2+2 internal logic change must produce zero graph structural changes!");
            Assert.That(patch.AddedSymbols, Is.Empty);
            Assert.That(patch.RemovedSymbols, Is.Empty);
            Assert.That(patch.OutgoingCallDiffs, Is.Empty);
            Assert.That(patch.OutgoingTypeDiffs, Is.Empty);
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
        Assert.That(resultScan.Errors, Is.Empty);
        Assert.That(((ScanOptions)resultScan.Value).Watch, Is.True);

        var resultIndex = parser.ParseArguments<ScanOptions, IndexOptions>(["index", "--watch"]);
        Assert.That(resultIndex.Errors, Is.Empty);
        Assert.That(((IndexOptions)resultIndex.Value).Watch, Is.True);
    }

    [Test]
    public async Task Test_TypeScript_NewlineInsertion_ZeroStructuralChanges()
    {
        WorkspaceIndexer.Register(new CodeExplorer.Parser.TypeScript.TypeScriptParser());

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

            Assert.That(snapshot1, Is.Not.Null);
            Assert.That(snapshot2, Is.Not.Null);

            var patch = SemanticGraphDiffer.ComputeDiff(snapshot1!, snapshot2!);

            Assert.That(patch.HasGraphStructuralChanges, Is.False,
                "Inserting a newline must produce ZERO graph structural changes!");
            Assert.That(patch.AddedSymbols, Is.Empty);
            Assert.That(patch.RemovedSymbols, Is.Empty);
            Assert.That(patch.OutgoingCallDiffs, Is.Empty);
            Assert.That(patch.OutgoingTypeDiffs, Is.Empty);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_IsCandidateSourceFile_Preserves_test_env_js_And_test_calculation_ts()
    {
        WorkspaceIndexer.Register(new CodeExplorer.Parser.TypeScript.TypeScriptParser());
        WorkspaceIndexer.Register(new CodeExplorer.Parser.TypeScript.JavaScriptParser());
        WorkspaceIndexer.Register(new CodeExplorer.Parser.Python.PythonParser());
        WorkspaceIndexer.Register(new CodeExplorer.Parser.Go.GoParser());

        // Files that MUST be recognized as candidate source files
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("bq-routes-calculation/test_env.js"), Is.True,
            "test_env.js should be recognized as a valid JavaScript file, not pruned as a test!");
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("stage-worker/src/models/test_calculation.ts"), Is.True,
            "test_calculation.ts should be recognized as a valid TypeScript file, not pruned as a test!");

        // Test files are now first-class candidate source files
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("tests/test_calculator.py"), Is.True,
            "test_*.py should be included for test indexing");
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("src/services/order.test.ts"), Is.True,
            "*.test.ts should be included for test indexing");
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("src/services/order.spec.js"), Is.True,
            "*.spec.js should be included for test indexing");
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("pkg/calc/calc_test.go"), Is.True,
            "*_test.go should be included for test indexing");

        // Non-source files and mocks that MUST be skipped
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("src/mocks/mock_service.ts"), Is.False,
            "mock files must be skipped");
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("src/bundle.min.js"), Is.False,
            "minified files must be skipped");
        Assert.That(WorkspaceIndexer.IsCandidateSourceFile("src/types.d.ts"), Is.False,
            "d.ts files must be skipped");
    }

    [Test]
    public async Task Test_Sqlite_Persists_AstSnapshot_And_SeparateProcessIncrementalScan_PreservesGraph_OnNewline()
    {
        WorkspaceIndexer.Register(new CodeExplorer.Parser.TypeScript.TypeScriptParser());

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
            var client1 = new CodeExplorer.Core.Database.SqliteGraphClient(dbFile);
            var indexer1 = new WorkspaceIndexer(client1);
            await indexer1.IndexAsync(tempDir, clear: false);

            // Verify SQLite file_registry table has entry and snapshot_json
            var registry1 = await client1.LoadFileRegistryAsync();
            Assert.That(registry1, Contains.Key("src/calculator.ts"));
            var entry1 = registry1["src/calculator.ts"];
            Assert.That(entry1.SnapshotJson, Is.Not.Null.And.Not.Empty, "SnapshotJson must be persisted in SQLite!");

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
            var client2 = new CodeExplorer.Core.Database.SqliteGraphClient(dbFile);
            var indexer2 = new WorkspaceIndexer(client2);

            var countsBefore = await client2.GetGraphCountsAsync();
            var changed = await indexer2.IndexIncrementalAsync(tempDir);
            Assert.That(changed, Is.True, "Incremental scan should detect and process the modified file.");
            var countsAfter = await client2.GetGraphCountsAsync();
            Assert.That(countsAfter.NodesCount, Is.EqualTo(countsBefore.NodesCount), "Graph nodes count must be preserved for logic-only edit!");

            // Second incremental scan without file edits: must detect 0 changes
            var changedAgain = await indexer2.IndexIncrementalAsync(tempDir);
            Assert.That(changedAgain, Is.False, "Workspace should be up to date on subsequent check.");

            // Verify registry in SQLite was updated with new hash and preserved/updated snapshot
            var registry2 = await client2.LoadFileRegistryAsync();
            var entry2 = registry2["src/calculator.ts"];
            Assert.That(entry2.ContentHash, Is.Not.EqualTo(entry1.ContentHash), "Content hash should be updated");
            Assert.That(entry2.SnapshotJson, Is.Not.Null.And.Not.Empty, "SnapshotJson must remain populated");

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

