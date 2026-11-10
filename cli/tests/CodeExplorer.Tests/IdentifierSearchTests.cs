using CodeExplorer.Cypher.Common;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class IdentifierSearchTests
{
    [Test]
    public void VectorEncoder_ShouldProduceNormalizedVectors()
    {
        var vec = IdentifierVectorEncoder.Encode("GateCommandHandler.HandleAsync");
        vec.Length.Should().Be(IdentifierVectorEncoder.Dimension);

        double sumSq = 0;
        foreach (var v in vec) sumSq += v * v;
        Math.Sqrt(sumSq).Should().BeApproximately(1.0, 0.001);
    }

    [Test]
    public void VectorEncoder_ShouldScoreRelatedIdentifiersHigherThanUnrelated()
    {
        var v1 = IdentifierVectorEncoder.Encode("GateCommandHandler.HandleAsync");
        var vExactMethod = IdentifierVectorEncoder.Encode("HandleAsync");
        var vPartialGate = IdentifierVectorEncoder.Encode("gate command");
        var vUnrelated = IdentifierVectorEncoder.Encode("RandomUnrelatedCustomerPaymentService");

        var simExactMethod = IdentifierVectorEncoder.CosineSimilarity(v1, vExactMethod);
        var simPartialGate = IdentifierVectorEncoder.CosineSimilarity(v1, vPartialGate);
        var simUnrelated = IdentifierVectorEncoder.CosineSimilarity(v1, vUnrelated);

        simExactMethod.Should().BeGreaterThan(0.40f);
        simPartialGate.Should().BeGreaterThan(0.40f);
        simExactMethod.Should().BeGreaterThan(simUnrelated);
        simPartialGate.Should().BeGreaterThan(simUnrelated);
    }

    [Test]
    public async Task IdentifierSearchEngine_ShouldFindExactAndFuzzyMatches()
    {
        using var conn = new SqliteConnection($"Data Source=InMemory_{Guid.NewGuid():N};Mode=Memory;");
        conn.Open();

        using (var setup = conn.CreateCommand())
        {
            SqliteCypherFunctions.Register(conn);
            setup.CommandText = $$"""
                CREATE TABLE IF NOT EXISTS nodes (
                    id TEXT PRIMARY KEY,
                    kind INTEGER NOT NULL,
                    properties JSONB NOT NULL
                );
                CREATE TABLE IF NOT EXISTS edges (
                    from_id INTEGER NOT NULL,
                    to_id INTEGER NOT NULL,
                    kind INTEGER NOT NULL,
                    properties JSONB NOT NULL
                );
                INSERT INTO nodes VALUES
                  ('t1', {{GraphKindCodec.EncodeNodeKind("Type")}}, jsonb('{"name":"GateCommandHandler","path":"cli/Commands/GateCommandHandler.cs","start_line":10}')),
                  ('f1', {{GraphKindCodec.EncodeNodeKind("Function")}}, jsonb('{"name":"HandleAsync","path":"cli/Commands/GateCommandHandler.cs","start_line":17}')),
                  ('ep1', {{GraphKindCodec.EncodeNodeKind("Endpoint")}}, jsonb('{"name":"POST:/api/gates/check","route_template":"/api/gates/check","path":"Server.cs","start_line":330}')),
                  ('m1', {{GraphKindCodec.EncodeNodeKind("Member")}}, jsonb('{"name":"GetGatesRequest","path":"Protocol.cs","start_line":26}'));

                INSERT INTO edges VALUES
                  ((SELECT rowid FROM nodes WHERE id = 't1'), (SELECT rowid FROM nodes WHERE id = 'f1'), {{GraphKindCodec.EncodeRelKind("HAS_METHOD")}}, jsonb('{}')),
                  ((SELECT rowid FROM nodes WHERE id = 't1'), (SELECT rowid FROM nodes WHERE id = 'm1'), {{GraphKindCodec.EncodeRelKind("HAS_MEMBER")}}, jsonb('{}'));
                """;
            setup.ExecuteNonQuery();
        }

        var engine = new IdentifierSearchEngine();
        await engine.RebuildIndexAsync(conn);

        // 1. Search for "HandleAsync"
        var res1 = await engine.SearchAsync(conn, "HandleAsync");
        res1.Should().NotBeEmpty();
        res1[0].FullName.Should().Be("GateCommandHandler.HandleAsync");

        // 2. Search for route "/api/gates/check"
        var res2 = await engine.SearchAsync(conn, "gates check");
        res2.Should().NotBeEmpty();
        res2.Any(r => r.Name.Contains("gates/check")).Should().BeTrue();

        // 3. Resolve "GateCommandHandler.HandleAsync"
        var resolved = await engine.ResolveIdentifierAsync(conn, "GateCommandHandler.HandleAsync");
        resolved.Should().NotBeNull();
        resolved!.NodeId.Should().Be("f1");
    }
}
