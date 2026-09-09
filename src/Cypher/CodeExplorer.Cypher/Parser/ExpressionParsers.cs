using System.Globalization;
using CodeExplorer.Cypher.Ast;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace CodeExplorer.Cypher.Parser;

public static class ExpressionParsers
{
    public static TokenListParser<CypherToken, string> PropertyNameText { get; } =
        Token.EqualTo(CypherToken.Identifier)
        .Or(Token.EqualTo(CypherToken.Order))
        .Or(Token.EqualTo(CypherToken.By))
        .Or(Token.EqualTo(CypherToken.Match))
        .Or(Token.EqualTo(CypherToken.Where))
        .Or(Token.EqualTo(CypherToken.Return))
        .Or(Token.EqualTo(CypherToken.As))
        .Or(Token.EqualTo(CypherToken.Is))
        .Or(Token.EqualTo(CypherToken.In))
        .Or(Token.EqualTo(CypherToken.Distinct))
        .Or(Token.EqualTo(CypherToken.Asc))
        .Or(Token.EqualTo(CypherToken.Desc))
        .Or(Token.EqualTo(CypherToken.Skip))
        .Or(Token.EqualTo(CypherToken.Limit))
        .Or(Token.EqualTo(CypherToken.With))
        .Or(Token.EqualTo(CypherToken.Case))
        .Or(Token.EqualTo(CypherToken.When))
        .Or(Token.EqualTo(CypherToken.Then))
        .Or(Token.EqualTo(CypherToken.Else))
        .Or(Token.EqualTo(CypherToken.End))
        .Or(Token.EqualTo(CypherToken.Contains))
        .Or(Token.EqualTo(CypherToken.Starts))
        .Or(Token.EqualTo(CypherToken.Ends))
        .Or(Token.EqualTo(CypherToken.Not))
        .Or(Token.EqualTo(CypherToken.And))
        .Or(Token.EqualTo(CypherToken.Or))
        .Select(t =>
        {
            var str = t.ToStringValue();
            if (str.StartsWith("`") && str.EndsWith("`") && str.Length >= 2)
                return str.Substring(1, str.Length - 2);
            return str;
        });

    public static TokenListParser<CypherToken, Expression> StringLiteral { get; } =
        Token.EqualTo(CypherToken.StringLiteral).Select(t =>
        {
            var raw = t.ToStringValue();
            var content = raw.Length >= 2 ? raw.Substring(1, raw.Length - 2) : raw;
            var unescaped = content
                .Replace("\\'", "'")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
            return (Expression)new StringLiteralExpression(unescaped);
        });

    public static TokenListParser<CypherToken, Expression> NumberLiteral { get; } =
        Token.EqualTo(CypherToken.Number).Select(t =>
        {
            var str = t.ToStringValue();
            if (int.TryParse(str, out var intVal))
                return (Expression)new NumberLiteralExpression(intVal, true);
            return (Expression)new NumberLiteralExpression(double.Parse(str, CultureInfo.InvariantCulture), false);
        });

    public static TokenListParser<CypherToken, Expression> BooleanLiteral { get; } =
        Token.EqualTo(CypherToken.True).Value((Expression)new BooleanLiteralExpression(true))
        .Or(Token.EqualTo(CypherToken.False).Value((Expression)new BooleanLiteralExpression(false)));

    public static TokenListParser<CypherToken, Expression> NullLiteral { get; } =
        Token.EqualTo(CypherToken.Null).Value((Expression)new NullLiteralExpression());

    public static TokenListParser<CypherToken, Expression> Parameter { get; } =
        Token.EqualTo(CypherToken.Parameter).Select(t =>
        {
            var str = t.ToStringValue();
            return (Expression)new ParameterExpression(str.TrimStart('$'));
        });

    private static TokenListParser<CypherToken, Expression?> CaseTestExpression { get; } =
        input =>
        {
            var next = input.ConsumeToken();
            if (next.HasValue && next.Value.Kind == CypherToken.When)
            {
                return TokenListParserResult.Value<CypherToken, Expression?>(null, input, input);
            }
            var res = ExpressionParser!(input);
            if (res.HasValue)
            {
                return TokenListParserResult.Value<CypherToken, Expression?>(res.Value, input, res.Remainder);
            }
            return TokenListParserResult.CastEmpty<CypherToken, Expression, Expression?>(res);
        };

    public static TokenListParser<CypherToken, Expression> CaseExpression { get; } =
        from caseTok in Token.EqualTo(CypherToken.Case)
        from testExpr in Parse.Ref(() => CaseTestExpression!)
        from branches in (
            from whenTok in Token.EqualTo(CypherToken.When)
            from whenExpr in Parse.Ref(() => ExpressionParser!)
            from thenTok in Token.EqualTo(CypherToken.Then)
            from thenExpr in Parse.Ref(() => ExpressionParser!)
            select new CaseWhenItem(whenExpr, thenExpr)
        ).AtLeastOnce()
        from elseExpr in (
            from elseTok in Token.EqualTo(CypherToken.Else)
            from e in Parse.Ref(() => ExpressionParser!)
            select e
        ).OptionalOrDefault()
        from endTok in Token.EqualTo(CypherToken.End)
        select (Expression)new CaseExpression(testExpr, branches.ToList(), elseExpr);

    public static TokenListParser<CypherToken, Expression> ListLiteral { get; } =
        from open in Token.EqualTo(CypherToken.LBracket)
        from items in Parse.Ref(() => ExpressionParser!).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
        from close in Token.EqualTo(CypherToken.RBracket)
        select (Expression)new ListExpression(items.ToList());

    public static TokenListParser<CypherToken, Expression> ParenthesizedExpression { get; } =
        from open in Token.EqualTo(CypherToken.LParen)
        from expr in Parse.Ref(() => ExpressionParser!)
        from close in Token.EqualTo(CypherToken.RParen)
        select expr;

    public static TokenListParser<CypherToken, Expression> FunctionCallOrIdentifier { get; } =
        from name in PropertyNameText
        from call in (
            from open in Token.EqualTo(CypherToken.LParen)
            from distinct in Token.EqualTo(CypherToken.Distinct).Optional()
            from args in Parse.Ref(() => ExpressionParser!).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
            from close in Token.EqualTo(CypherToken.RParen)
            select (IsFunction: true, Distinct: distinct.HasValue, Arguments: args.ToList())
        ).Optional()
        select call.HasValue && call.Value.IsFunction
            ? (Expression)new FunctionCallExpression(name, call.Value.Distinct, call.Value.Arguments)
            : (Expression)new IdentifierExpression(name);

    // Atom: Parenthesized, Case, Literals, List, Parameter, FunctionCall, or Identifier
    public static TokenListParser<CypherToken, Expression> Atom { get; } =
        ParenthesizedExpression
        .Or(CaseExpression)
        .Or(ListLiteral)
        .Or(StringLiteral)
        .Or(NumberLiteral)
        .Or(BooleanLiteral)
        .Or(NullLiteral)
        .Or(Parameter)
        .Or(FunctionCallOrIdentifier);

    // Postfix operations: property access (.prop), index access ([0]), or label predicate (:Label)
    public static TokenListParser<CypherToken, Expression> PostfixExpression { get; } =
        from baseExpr in Atom
        from suffixes in (
            (from dot in Token.EqualTo(CypherToken.Dot)
             from prop in PropertyNameText
             select (Kind: "prop", Property: prop, IndexExpr: (Expression?)null))
            .Or(
             from lbracket in Token.EqualTo(CypherToken.LBracket)
             from idx in Parse.Ref(() => ExpressionParser!)
             from rbracket in Token.EqualTo(CypherToken.RBracket)
             select (Kind: "index", Property: "", IndexExpr: (Expression?)idx))
            .Or(
             from colon in Token.EqualTo(CypherToken.Colon)
             from label in PropertyNameText
             select (Kind: "label", Property: label, IndexExpr: (Expression?)null))
        ).Many()
        select suffixes.Aggregate(baseExpr, (current, suffix) =>
        {
            if (suffix.Kind == "label")
            {
                return new HasLabelExpression(current, suffix.Property);
            }
            if (suffix.Kind == "index")
            {
                // represent index as a pseudo function or binary property access
                return new FunctionCallExpression("item_at", false, [current, suffix.IndexExpr!]);
            }
            if (current is IdentifierExpression idExpr)
            {
                return new PropertyAccessExpression(idExpr.Name, suffix.Property);
            }
            if (current is PropertyAccessExpression propExpr)
            {
                return new PropertyAccessExpression($"{propExpr.Variable}.{propExpr.PropertyName}", suffix.Property);
            }
            return new PropertyAccessExpression(current.ToString()!, suffix.Property);
        });

    // Unary prefix: NOT, -
    public static TokenListParser<CypherToken, Expression> UnaryExpression { get; } =
        (from not in Token.EqualTo(CypherToken.Not)
         from expr in Parse.Ref(() => UnaryExpression!)
         select (Expression)new UnaryExpression(UnaryOperator.Not, expr))
        .Or(PostfixExpression);

    // Additive operations (+ for addition and string concatenation)
    public static TokenListParser<CypherToken, Expression> AdditiveExpression { get; } =
        from first in UnaryExpression
        from rest in (
            from plus in Token.EqualTo(CypherToken.Plus)
            from next in UnaryExpression
            select next
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, BinaryOperator.Add, r));

    // Comparison operators and IS NULL / IS NOT NULL
    public static TokenListParser<CypherToken, Expression> ComparisonExpression { get; } =
        from left in AdditiveExpression
        from right in (
            // IS [NOT] NULL
            (from isTok in Token.EqualTo(CypherToken.Is)
             from notTok in Token.EqualTo(CypherToken.Not).Optional()
             from nullTok in Token.EqualTo(CypherToken.Null)
             select (Func<Expression, Expression>)(l => new UnaryExpression(notTok.HasValue ? UnaryOperator.IsNotNull : UnaryOperator.IsNull, l)))
            // STARTS WITH
            .Or(from starts in Token.EqualTo(CypherToken.Starts)
                from with in Token.EqualTo(CypherToken.With)
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, BinaryOperator.StartsWith, r)))
            // ENDS WITH
            .Or(from ends in Token.EqualTo(CypherToken.Ends)
                from with in Token.EqualTo(CypherToken.With)
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, BinaryOperator.EndsWith, r)))
            // CONTAINS
            .Or(from contains in Token.EqualTo(CypherToken.Contains)
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, BinaryOperator.Contains, r)))
            // IN
            .Or(from inTok in Token.EqualTo(CypherToken.In)
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, BinaryOperator.In, r)))
            // Standard comparisons
            .Or(from op in (
                    Token.EqualTo(CypherToken.Equal).Value(BinaryOperator.Equal)
                    .Or(Token.EqualTo(CypherToken.NotEqual).Value(BinaryOperator.NotEqual))
                    .Or(Token.EqualTo(CypherToken.LessOrEqual).Value(BinaryOperator.LessOrEqual))
                    .Or(Token.EqualTo(CypherToken.GreaterOrEqual).Value(BinaryOperator.GreaterOrEqual))
                    .Or(Token.EqualTo(CypherToken.LessThan).Value(BinaryOperator.LessThan))
                    .Or(Token.EqualTo(CypherToken.GreaterThan).Value(BinaryOperator.GreaterThan))
                )
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, op, r)))
        ).OptionalOrDefault()
        select right != null ? right(left) : left;

    // Logical AND
    public static TokenListParser<CypherToken, Expression> AndExpression { get; } =
        from first in ComparisonExpression
        from rest in (
            from and in Token.EqualTo(CypherToken.And)
            from next in ComparisonExpression
            select next
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, BinaryOperator.And, r));

    // Logical OR
    public static TokenListParser<CypherToken, Expression> OrExpression { get; } =
        from first in AndExpression
        from rest in (
            from or in Token.EqualTo(CypherToken.Or)
            from next in AndExpression
            select next
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, BinaryOperator.Or, r));

    public static TokenListParser<CypherToken, Expression> ExpressionParser { get; } =
        OrExpression;
}
