using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Parser;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class ParserTests
{
    [Test]
    public void Parse_SimpleNodeQuery()
    {
        var cypher = "MATCH (n:File) RETURN n.path AS filePath";
        var query = CypherQueryParser.Parse(cypher);

        Assert.That(query.Matches, Has.Count.EqualTo(1));
        var match = query.Matches[0];
        Assert.That(match.IsOptional, Is.False);
        Assert.That(match.Paths, Has.Count.EqualTo(1));

        var head = match.Paths[0].Head;
        Assert.That(head.Variable, Is.EqualTo("n"));
        Assert.That(head.Labels, Contains.Item("File"));

        Assert.That(query.Return.Items, Has.Count.EqualTo(1));
        var ret = query.Return.Items[0];
        Assert.That(ret.Alias, Is.EqualTo("filePath"));
        Assert.That(ret.Expression, Is.InstanceOf<PropertyAccessExpression>());
        var prop = (PropertyAccessExpression)ret.Expression;
        Assert.That(prop.Variable, Is.EqualTo("n"));
        Assert.That(prop.PropertyName, Is.EqualTo("path"));
    }

    [Test]
    public void Parse_RelationshipPattern_WithPropertiesAndMultipleTypes()
    {
        var cypher = "MATCH (f:File)-[:DEFINES|DECLARES]->(c:Type {kind: 'class'}) RETURN c.name";
        var query = CypherQueryParser.Parse(cypher);

        var path = query.Matches[0].Paths[0];
        Assert.That(path.Head.Variable, Is.EqualTo("f"));
        Assert.That(path.Chain, Has.Count.EqualTo(1));

        var link = path.Chain[0];
        Assert.That(link.Relationship.Types, Is.EqualTo(new[] { "DEFINES", "DECLARES" }));
        Assert.That(link.Relationship.Direction, Is.EqualTo(Direction.Outgoing));

        Assert.That(link.Target.Variable, Is.EqualTo("c"));
        Assert.That(link.Target.Labels, Contains.Item("Type"));
        Assert.That(link.Target.Properties, Is.Not.Null);
        Assert.That(link.Target.Properties!["kind"], Is.InstanceOf<StringLiteralExpression>());
    }

    [Test]
    public void Parse_VariableLengthRelationship()
    {
        var cypher = "MATCH path = (src:Function {symbol: $startFunction})-[:CALLS*1..5]->(tgt:Function) RETURN nodes(path)";
        var query = CypherQueryParser.Parse(cypher);

        var path = query.Matches[0].Paths[0];
        Assert.That(path.PathVariable, Is.EqualTo("path"));
        Assert.That(path.Chain, Has.Count.EqualTo(1));

        var rel = path.Chain[0].Relationship;
        Assert.That(rel.Types, Contains.Item("CALLS"));
        Assert.That(rel.Range.HasValue, Is.True);
        Assert.That(rel.Range!.Value.Min, Is.EqualTo(1));
        Assert.That(rel.Range!.Value.Max, Is.EqualTo(5));
    }

    [Test]
    public void Parse_WhereWithLabelPredicatesAndStringOperators()
    {
        var cypher = @"
            MATCH (n)
            WHERE (n:Function OR n:Type) AND n.name CONTAINS $name AND n.id STARTS WITH $prefix
            RETURN n.name AS name
        ";
        var query = CypherQueryParser.Parse(cypher);

        Assert.That(query.Matches[0].Where, Is.Not.Null);
        var pred = query.Matches[0].Where!.Predicate;
        Assert.That(pred, Is.InstanceOf<BinaryExpression>());
    }

    [Test]
    public void Parse_CaseExpressionAndAddition()
    {
        var cypher = @"
            MATCH (w:Workspace)-[:CONTAINS]->(f:File)
            RETURN CASE WHEN f IS NOT NULL THEN w.path + '/' + f.path ELSE null END AS fullPath
            ORDER BY fullPath ASC
            LIMIT 50
        ";
        var query = CypherQueryParser.Parse(cypher);

        Assert.That(query.Return.Items, Has.Count.EqualTo(1));
        Assert.That(query.Return.Items[0].Expression, Is.InstanceOf<CaseExpression>());
        Assert.That(query.OrderBy, Is.Not.Null);
        Assert.That(query.OrderBy!.Items[0].IsDescending, Is.False);
        Assert.That(query.Limit, Is.Not.Null);
        Assert.That(query.Limit!.Count, Is.EqualTo(50));
    }
}
