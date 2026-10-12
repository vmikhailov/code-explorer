using AwesomeAssertions;
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

        query.Matches.Should().HaveCount(1);
        var match = query.Matches[0];
        match.IsOptional.Should().BeFalse();
        match.Paths.Should().HaveCount(1);

        var head = match.Paths[0].Head;
        head.Variable.Should().Be("n");
        head.Labels.Should().Contain("File");

        query.Return.Items.Should().HaveCount(1);
        var ret = query.Return.Items[0];
        ret.Alias.Should().Be("filePath");
        ret.Expression.Should().BeOfType<PropertyAccessExpression>();
        var prop = (PropertyAccessExpression)ret.Expression;
        prop.Variable.Should().Be("n");
        prop.PropertyName.Should().Be("path");
    }

    [Test]
    public void Parse_RelationshipPattern_WithPropertiesAndMultipleTypes()
    {
        var cypher = "MATCH (f:File)-[:DEFINES|DECLARES]->(c:Type {kind: 'class'}) RETURN c.name";
        var query = CypherQueryParser.Parse(cypher);

        var path = query.Matches[0].Paths[0];
        path.Head.Variable.Should().Be("f");
        path.Chain.Should().HaveCount(1);

        var link = path.Chain[0];
        link.Relationship.Types.Should().Equal(new[] { "DEFINES", "DECLARES" });
        link.Relationship.Direction.Should().Be(Direction.Outgoing);

        link.Target.Variable.Should().Be("c");
        link.Target.Labels.Should().Contain("Type");
        link.Target.Properties.Should().NotBeNull();
        link.Target.Properties!["kind"].Should().BeOfType<StringLiteralExpression>();
    }

    [Test]
    public void Parse_VariableLengthRelationship()
    {
        var cypher = "MATCH path = (src:Function {symbol: $startFunction})-[:CALLS*1..5]->(tgt:Function) RETURN nodes(path)";
        var query = CypherQueryParser.Parse(cypher);

        var path = query.Matches[0].Paths[0];
        path.PathVariable.Should().Be("path");
        path.Chain.Should().HaveCount(1);

        var rel = path.Chain[0].Relationship;
        rel.Types.Should().Contain("CALLS");
        rel.Range.HasValue.Should().BeTrue();
        rel.Range!.Value.Min.Should().Be(1);
        rel.Range!.Value.Max.Should().Be(5);
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

        query.Matches[0].Where.Should().NotBeNull();
        var pred = query.Matches[0].Where!.Predicate;
        pred.Should().BeOfType<BinaryExpression>();
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

        query.Return.Items.Should().HaveCount(1);
        query.Return.Items[0].Expression.Should().BeOfType<CaseExpression>();
        query.OrderBy.Should().NotBeNull();
        query.OrderBy!.Items[0].IsDescending.Should().BeFalse();
        query.Limit.Should().NotBeNull();
        query.Limit!.Count.Should().Be(50);
    }

    [Test]
    public void Parse_NotPrecedence_BindsLooserThanContains()
    {
        var cypher = "MATCH (es:ExternalService) WHERE NOT es.file_path CONTAINS 'test' RETURN es";
        var query = CypherQueryParser.Parse(cypher);

        var pred = query.Matches[0].Where!.Predicate;
        pred.Should().BeOfType<UnaryExpression>();
        var unary = (UnaryExpression)pred;
        unary.Operator.Should().Be(UnaryOperator.Not);
        unary.Operand.Should().BeOfType<BinaryExpression>();
        var bin = (BinaryExpression)unary.Operand;
        bin.Operator.Should().Be(BinaryOperator.Contains);
    }

    [Test]
    public void Parse_NotPrecedence_BindsLooserThanComparison()
    {
        var cypher = "MATCH (n) WHERE NOT n.id = 5 RETURN n";
        var query = CypherQueryParser.Parse(cypher);

        var pred = query.Matches[0].Where!.Predicate;
        pred.Should().BeOfType<UnaryExpression>();
        var unary = (UnaryExpression)pred;
        unary.Operator.Should().Be(UnaryOperator.Not);
        unary.Operand.Should().BeOfType<BinaryExpression>();
        var bin = (BinaryExpression)unary.Operand;
        bin.Operator.Should().Be(BinaryOperator.Equal);
    }

    [Test]
    public void Parse_NotPrecedence_BindsTighterThanAnd()
    {
        var cypher = "MATCH (n) WHERE NOT n.a = 1 AND n.b = 2 RETURN n";
        var query = CypherQueryParser.Parse(cypher);

        var pred = query.Matches[0].Where!.Predicate;
        pred.Should().BeOfType<BinaryExpression>();
        var andExpr = (BinaryExpression)pred;
        andExpr.Operator.Should().Be(BinaryOperator.And);
        andExpr.Left.Should().BeOfType<UnaryExpression>();
        var unary = (UnaryExpression)andExpr.Left;
        unary.Operator.Should().Be(UnaryOperator.Not);
    }
}
