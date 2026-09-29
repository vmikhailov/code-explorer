using CodeExplorer.Core.Parser.Incremental;
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
}
