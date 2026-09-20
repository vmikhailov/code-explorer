using CodeExplorer.Cypher.Parser;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class CypherSecurityValidatorTests
{
    [Test]
    [TestCase("MATCH (d:Database) RETURN d.type AS databaseType")]
    [TestCase("MATCH (n:File) RETURN n.offset AS offset")]
    [TestCase("MATCH (n) WHERE n.name = 'Preset' RETURN n")]
    [TestCase("MATCH (n) WHERE n.action = 'SET' RETURN n")]
    [TestCase("MATCH (n) WHERE n.status = 'DELETED' RETURN n")]
    [TestCase("// Please set up the database\nMATCH (n) RETURN n")]
    [TestCase("/* multi-line create comment */ MATCH (n) RETURN n")]
    [TestCase("MATCH (n) RETURN n.set")]
    [TestCase("MATCH (n) RETURN n.`set`")]
    [TestCase("MATCH (n:Set) RETURN n")]
    [TestCase("MATCH (p:Project)-[:DEPENDS_ON]->(dep:Project) RETURN p.name, dep.name")]
    public void ValidateReadOnly_ValidReadQueriesWithSubstrings_DoesNotThrow(string query)
    {
        Assert.DoesNotThrow(() => CypherSecurityValidator.ValidateReadOnly(query));
    }

    [Test]
    [TestCase("MATCH (n) SET n.name = 'test'")]
    [TestCase("MATCH (n) sEt n.name = 'test'")]
    [TestCase("SET n.name = 'test'")]
    [TestCase("CREATE (n:Node {name: 'foo'})")]
    [TestCase("cReAtE (n:Node)")]
    [TestCase("MATCH (n) DETACH DELETE n")]
    [TestCase("MATCH (n) DELETE n")]
    [TestCase("MERGE (n:Node {id: 1})")]
    [TestCase("MATCH (n) REMOVE n.prop")]
    [TestCase("DROP INDEX test_idx")]
    [TestCase("ALTER TABLE users")]
    [TestCase("TRUNCATE TABLE logs")]
    public void ValidateReadOnly_MutatingQueries_ThrowsSecurityViolation(string query)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CypherSecurityValidator.ValidateReadOnly(query));
        Assert.That(ex!.Message, Does.Contain("Security violation: Mutating queries are not allowed."));
    }
}
