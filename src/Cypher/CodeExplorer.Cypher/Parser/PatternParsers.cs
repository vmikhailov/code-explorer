using CodeExplorer.Cypher.Ast;
using Superpower;
using Superpower.Parsers;

namespace CodeExplorer.Cypher.Parser;

public static class PatternParsers
{
    // Property map inside node/relationship: { key: value, ... }
    public static TokenListParser<CypherToken, Dictionary<string, Expression>> PropertyMap { get; } =
        from open in Token.EqualTo(CypherToken.LBrace)
        from pairs in (
            from key in ExpressionParsers.PropertyNameText
            from colon in Token.EqualTo(CypherToken.Colon)
            from val in ExpressionParsers.ExpressionParser
            select (Key: key, Value: val)
        ).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
        from close in Token.EqualTo(CypherToken.RBrace)
        select pairs.ToDictionary(p => p.Key, p => p.Value);

    // Node: (var:Label1:Label2 {prop: val})
    public static TokenListParser<CypherToken, NodePattern> Node { get; } =
        from open in Token.EqualTo(CypherToken.LParen)
        from varName in ExpressionParsers.PropertyNameText.OptionalOrDefault()
        from labels in (
            from colon in Token.EqualTo(CypherToken.Colon)
            from label in ExpressionParsers.PropertyNameText
            select label
        ).Many()
        from props in PropertyMap.OptionalOrDefault()
        from close in Token.EqualTo(CypherToken.RParen)
        select new NodePattern(
            varName,
            labels.ToList(),
            props
        );

    // Range length: * or *1..5 or *..5 or *1.. or *3
    public static TokenListParser<CypherToken, RangeLength> RangeLength { get; } =
        from star in Token.EqualTo(CypherToken.Asterisk)
        from range in (
            from min in Token.EqualTo(CypherToken.Number).Select(t => int.Parse(t.ToStringValue())).Optional()
            from dotdot in Token.EqualTo(CypherToken.DotDot).Optional()
            from max in Token.EqualTo(CypherToken.Number).Select(t => int.Parse(t.ToStringValue())).Optional()
            select new RangeLength(
                min.HasValue ? min.Value : (int?)null,
                dotdot.HasValue ? (max.HasValue ? max.Value : (int?)null) : (min.HasValue ? min.Value : (int?)null)
            )
        ).Optional()
        select range.GetValueOrDefault(new RangeLength(null, null));

    // Relationship types: :CALLS|IMPLEMENTS
    public static TokenListParser<CypherToken, List<string>> RelTypes { get; } =
        from colon in Token.EqualTo(CypherToken.Colon)
        from types in ExpressionParsers.PropertyNameText.ManyDelimitedBy(Token.EqualTo(CypherToken.Pipe))
        select types.ToList();

    // Bracketed relationship body: [var:TYPE*1..5 {props}]
    private static TokenListParser<CypherToken, (string? Variable, List<string> Types, RangeLength? Range, Dictionary<string, Expression>? Properties)> BracketedRelBody { get; } =
        from open in Token.EqualTo(CypherToken.LBracket)
        from varName in ExpressionParsers.PropertyNameText.OptionalOrDefault()
        from types in RelTypes.OptionalOrDefault()
        from range in RangeLength.Optional()
        from props in PropertyMap.OptionalOrDefault()
        from close in Token.EqualTo(CypherToken.RBracket)
        select (
            varName,
            types ?? new List<string>(),
            range,
            props
        );

    // Directed or undirected relationship patterns
    // 1. -[...] -> (Outgoing)
    private static TokenListParser<CypherToken, RelationshipPattern> OutgoingBracketedRel { get; } =
        from dash in Token.EqualTo(CypherToken.Dash)
        from body in BracketedRelBody
        from arrow in Token.EqualTo(CypherToken.ArrowRight)
        select new RelationshipPattern(body.Variable, body.Types, body.Range, Direction.Outgoing, body.Properties);

    // 2. <- [...] - (Incoming)
    private static TokenListParser<CypherToken, RelationshipPattern> IncomingBracketedRel { get; } =
        from arrow in Token.EqualTo(CypherToken.ArrowLeft)
        from body in BracketedRelBody
        from dash in Token.EqualTo(CypherToken.Dash)
        select new RelationshipPattern(body.Variable, body.Types, body.Range, Direction.Incoming, body.Properties);

    // 3. - [...] - (Undirected)
    private static TokenListParser<CypherToken, RelationshipPattern> UndirectedBracketedRel { get; } =
        from dash1 in Token.EqualTo(CypherToken.Dash)
        from body in BracketedRelBody
        from dash2 in Token.EqualTo(CypherToken.Dash)
        select new RelationshipPattern(body.Variable, body.Types, body.Range, Direction.Undirected, body.Properties);

    // 4. -> without brackets
    // 4. --> or -> without brackets
    private static TokenListParser<CypherToken, RelationshipPattern> SimpleOutgoingRel { get; } =
        from dash in Token.EqualTo(CypherToken.Dash).Optional()
        from arrow in Token.EqualTo(CypherToken.ArrowRight)
        select new RelationshipPattern(null, new List<string>(), null, Direction.Outgoing, null);

    // 5. <- without brackets
    // 5. <-- or <- without brackets
    private static TokenListParser<CypherToken, RelationshipPattern> SimpleIncomingRel { get; } =
        from arrow in Token.EqualTo(CypherToken.ArrowLeft)
        from dash in Token.EqualTo(CypherToken.Dash).Optional()
        select new RelationshipPattern(null, new List<string>(), null, Direction.Incoming, null);

    // 6. -- without brackets
    private static TokenListParser<CypherToken, RelationshipPattern> SimpleUndirectedRel { get; } =
        from dash1 in Token.EqualTo(CypherToken.Dash)
        from dash2 in Token.EqualTo(CypherToken.Dash)
        select new RelationshipPattern(null, new List<string>(), null, Direction.Undirected, null);

    public static TokenListParser<CypherToken, RelationshipPattern> Relationship { get; } =
        OutgoingBracketedRel.Try()
        .Or(IncomingBracketedRel.Try())
        .Or(UndirectedBracketedRel.Try())
        .Or(SimpleOutgoingRel.Try())
        .Or(SimpleIncomingRel.Try())
        .Or(SimpleUndirectedRel);

    // Full Path Pattern: [pathVar =] (a)-[r]->(b)...
    public static TokenListParser<CypherToken, PathPattern> Path { get; } =
        from pathVar in (
            from id in ExpressionParsers.PropertyNameText
            from eq in Token.EqualTo(CypherToken.Equal)
            select id
        ).OptionalOrDefault()
        from head in Node
        from chain in (
            from rel in Relationship
            from target in Node
            select new PathElement(rel, target)
        ).Many()
        select new PathPattern(head, chain.ToList(), pathVar);
}
