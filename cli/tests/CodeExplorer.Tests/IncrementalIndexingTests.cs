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

            using var watcher = new CodeExplorer.Core.Parser.FileWatcher(
                tempDir,
                batch =>
                {
                    Interlocked.Increment(ref batchesReceived);
                    tcs.TrySetResult(true);
                    return Task.CompletedTask;
                },
                debounceMs: 150);

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
            Assert.That(batchesReceived, Is.LessThanOrEqualTo(2), "Events should be debounced into a single or very few batches.");
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

        var resultScan = parser.ParseArguments<ScanOptions, IndexOptions>(new[] { "scan", "--watch" });
        Assert.That(resultScan.Errors, Is.Empty);
        Assert.That(((ScanOptions)resultScan.Value).Watch, Is.True);

        var resultIndex = parser.ParseArguments<ScanOptions, IndexOptions>(new[] { "index", "--watch" });
        Assert.That(resultIndex.Errors, Is.Empty);
        Assert.That(((IndexOptions)resultIndex.Value).Watch, Is.True);
    }
}
