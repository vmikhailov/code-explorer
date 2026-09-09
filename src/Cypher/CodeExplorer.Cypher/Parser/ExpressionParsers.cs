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
        .Or(Token.EqualTo(CypherToken.Unwind))
        .Or(Token.EqualTo(CypherToken.Xor))
        .Or(Token.EqualTo(CypherToken.Union))
        .Or(Token.EqualTo(CypherToken.Call))
        .Or(Token.EqualTo(CypherToken.Yield))
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
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var hexVal = Convert.ToInt64(str.Substring(2), 16);
                return (Expression)new NumberLiteralExpression(hexVal, true);
            }
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                return (Expression)new NumberLiteralExpression(intVal, true);
            return (Expression)new NumberLiteralExpression(double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture), false);
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

    public static TokenListParser<CypherToken, Expression> ListComprehensionOrListLiteral { get; } =
        (from open in Token.EqualTo(CypherToken.LBracket)
         from varName in PropertyNameText
         from inTok in Token.EqualTo(CypherToken.In)
         from listExpr in Parse.Ref(() => ExpressionParser!)
         from whereExpr in (
             from whereTok in Token.EqualTo(CypherToken.Where)
             from p in Parse.Ref(() => ExpressionParser!)
             select p
         ).OptionalOrDefault()
         from pipe in (
             from p in Token.EqualTo(CypherToken.Pipe)
             from proj in Parse.Ref(() => ExpressionParser!)
             select proj
         ).OptionalOrDefault()
         from close in Token.EqualTo(CypherToken.RBracket)
         select (Expression)new ListComprehensionExpression(varName, listExpr, whereExpr, pipe))
        .Try()
        .Or(
         from open in Token.EqualTo(CypherToken.LBracket)
         from items in Parse.Ref(() => ExpressionParser!).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
         from close in Token.EqualTo(CypherToken.RBracket)
         select (Expression)new ListExpression(items.ToList()));

    public static TokenListParser<CypherToken, Expression> MapLiteral { get; } =
        from open in Token.EqualTo(CypherToken.LBrace)
        from pairs in (
            from key in PropertyNameText
            from colon in Token.EqualTo(CypherToken.Colon)
            from val in Parse.Ref(() => ExpressionParser!)
            select (Key: key, Value: val)
        ).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
        from close in Token.EqualTo(CypherToken.RBrace)
        select (Expression)new MapLiteralExpression(pairs.ToDictionary(p => p.Key, p => p.Value));

    public static TokenListParser<CypherToken, Expression> QuantifierPredicate { get; } =
        from quant in PropertyNameText
        where quant.Equals("any", StringComparison.OrdinalIgnoreCase) ||
              quant.Equals("all", StringComparison.OrdinalIgnoreCase) ||
              quant.Equals("single", StringComparison.OrdinalIgnoreCase) ||
              quant.Equals("none", StringComparison.OrdinalIgnoreCase)
        from open in Token.EqualTo(CypherToken.LParen)
        from varName in PropertyNameText
        from inTok in Token.EqualTo(CypherToken.In)
        from listExpr in Parse.Ref(() => ExpressionParser!)
        from whereTok in Token.EqualTo(CypherToken.Where)
        from pred in Parse.Ref(() => ExpressionParser!)
        from close in Token.EqualTo(CypherToken.RParen)
        select (Expression)new ListPredicateExpression(quant.ToLowerInvariant(), varName, listExpr, pred);

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

    public static TokenListParser<CypherToken, Expression> GraphPatternExpression { get; } =
        (from path in PatternParsers.Path
         where path.Chain.Count > 0
         select (Expression)new PatternExpression(path)).Try();

    public static TokenListParser<CypherToken, Expression> PatternComprehension { get; } =
        (from open in Token.EqualTo(CypherToken.LBracket)
         from path in PatternParsers.Path
         where path.Chain.Count > 0
         from whereExpr in (
             from whereTok in Token.EqualTo(CypherToken.Where)
             from p in Parse.Ref(() => ExpressionParser!)
             select p
         ).OptionalOrDefault()
         from pipe in Token.EqualTo(CypherToken.Pipe)
         from proj in Parse.Ref(() => ExpressionParser!)
         from close in Token.EqualTo(CypherToken.RBracket)
         select (Expression)new PatternComprehensionExpression(path, whereExpr, proj)).Try();

    public static TokenListParser<CypherToken, Expression> ReduceExpressionParser { get; } =
        (from reduceName in PropertyNameText
         where reduceName.Equals("reduce", StringComparison.OrdinalIgnoreCase)
         from open in Token.EqualTo(CypherToken.LParen)
         from acc in PropertyNameText
         from eq in Token.EqualTo(CypherToken.Equal)
         from init in Parse.Ref(() => ExpressionParser!)
         from comma in Token.EqualTo(CypherToken.Comma)
         from varName in PropertyNameText
         from inTok in Token.EqualTo(CypherToken.In)
         from list in Parse.Ref(() => ExpressionParser!)
         from pipe in Token.EqualTo(CypherToken.Pipe)
         from step in Parse.Ref(() => ExpressionParser!)
         from close in Token.EqualTo(CypherToken.RParen)
         select (Expression)new ReduceExpression(acc, init, varName, list, step)).Try();

    // Atom: Parenthesized / Pattern, Case, Literals, List, Parameter, FunctionCall, or Identifier
    public static TokenListParser<CypherToken, Expression> Atom { get; } =
        GraphPatternExpression
        .Or(ParenthesizedExpression)
        .Or(CaseExpression)
        .Or(ReduceExpressionParser)
        .Or(QuantifierPredicate.Try())
        .Or(PatternComprehension)
        .Or(ListComprehensionOrListLiteral)
        .Or(MapLiteral)
        .Or(StringLiteral)
        .Or(NumberLiteral)
        .Or(BooleanLiteral)
        .Or(NullLiteral)
        .Or(Token.EqualTo(CypherToken.Asterisk).Value((Expression)new WildcardExpression()))
        .Or(Parameter)
        .Or(FunctionCallOrIdentifier);

    private static TokenListParser<CypherToken, MapProjectionElement> MapProjectionElementParser { get; } =
        (from dot in Token.EqualTo(CypherToken.Dot)
         from star in Token.EqualTo(CypherToken.Asterisk)
         select new MapProjectionElement("", null, true)).Try()
        .Or(
         from dot in Token.EqualTo(CypherToken.Dot)
         from prop in PropertyNameText
         select new MapProjectionElement(prop, null, false)).Try()
        .Or(
         from prop in PropertyNameText
         from colon in Token.EqualTo(CypherToken.Colon)
         from expr in Parse.Ref(() => ExpressionParser!)
         select new MapProjectionElement(prop, expr, false));

    // Postfix operations: property access (.prop), index/slice access ([0], [1..3]), label predicate (:Label), or map projection ({...})
    public static TokenListParser<CypherToken, Expression> PostfixExpression { get; } =
        from baseExpr in Atom
        from suffixes in (
            (from dot in Token.EqualTo(CypherToken.Dot)
             from prop in PropertyNameText
             select (Kind: "prop", Property: prop, IndexExpr: (Expression?)null, SliceFrom: (Expression?)null, SliceTo: (Expression?)null, ProjElements: (List<MapProjectionElement>?)null))
            .Or(
             from lbracket in Token.EqualTo(CypherToken.LBracket)
             from slice in (
                 from fromExpr in Parse.Ref(() => ExpressionParser!).OptionalOrDefault()
                 from dotdot in Token.EqualTo(CypherToken.DotDot)
                 from toExpr in Parse.Ref(() => ExpressionParser!).OptionalOrDefault()
                 select (IsSlice: true, From: fromExpr, To: toExpr, Single: (Expression?)null)
             ).Try().Or(
                 from idx in Parse.Ref(() => ExpressionParser!)
                 select (IsSlice: false, From: (Expression?)null, To: (Expression?)null, Single: (Expression?)idx)
             )
             from rbracket in Token.EqualTo(CypherToken.RBracket)
             select (Kind: slice.IsSlice ? "slice" : "index", Property: "", IndexExpr: slice.Single, SliceFrom: slice.From, SliceTo: slice.To, ProjElements: (List<MapProjectionElement>?)null))
            .Or(
             from colon in Token.EqualTo(CypherToken.Colon)
             from label in PropertyNameText
             select (Kind: "label", Property: label, IndexExpr: (Expression?)null, SliceFrom: (Expression?)null, SliceTo: (Expression?)null, ProjElements: (List<MapProjectionElement>?)null))
            .Or(
             (from open in Token.EqualTo(CypherToken.LBrace)
              from elements in MapProjectionElementParser.ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
              from close in Token.EqualTo(CypherToken.RBrace)
              select (Kind: "projection", Property: "", IndexExpr: (Expression?)null, SliceFrom: (Expression?)null, SliceTo: (Expression?)null, ProjElements: (List<MapProjectionElement>?)elements.ToList())).Try())
        ).Many()
        select suffixes.Aggregate(baseExpr, (current, suffix) =>
        {
            if (suffix.Kind == "projection")
            {
                return new MapProjectionExpression(current, suffix.ProjElements!);
            }
            if (suffix.Kind == "label")
            {
                return new HasLabelExpression(current, suffix.Property);
            }
            if (suffix.Kind == "slice")
            {
                return new ListSliceExpression(current, suffix.SliceFrom, suffix.SliceTo);
            }
            if (suffix.Kind == "index")
            {
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

    // Unary prefix: NOT, -, +
    public static TokenListParser<CypherToken, Expression> UnaryExpression { get; } =
        (from not in Token.EqualTo(CypherToken.Not)
         from expr in Parse.Ref(() => UnaryExpression!)
         select (Expression)new UnaryExpression(UnaryOperator.Not, expr))
        .Or(from dash in Token.EqualTo(CypherToken.Dash)
            from expr in Parse.Ref(() => UnaryExpression!)
            select (Expression)new UnaryExpression(UnaryOperator.Minus, expr))
        .Or(from plus in Token.EqualTo(CypherToken.Plus)
            from expr in Parse.Ref(() => UnaryExpression!)
            select (Expression)new UnaryExpression(UnaryOperator.Plus, expr))
        .Or(PostfixExpression);

    // Power (^)
    public static TokenListParser<CypherToken, Expression> PowerExpression { get; } =
        from first in UnaryExpression
        from rest in (
            from caret in Token.EqualTo(CypherToken.Caret)
            from next in UnaryExpression
            select next
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, BinaryOperator.Power, r));

    // Multiplicative operations (*, /, %)
    public static TokenListParser<CypherToken, Expression> MultiplicativeExpression { get; } =
        from first in PowerExpression
        from rest in (
            from op in (
                Token.EqualTo(CypherToken.Asterisk).Value(BinaryOperator.Multiply)
                .Or(Token.EqualTo(CypherToken.Slash).Value(BinaryOperator.Divide))
                .Or(Token.EqualTo(CypherToken.Percent).Value(BinaryOperator.Modulo))
            )
            from next in PowerExpression
            select (Op: op, Expr: next)
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, r.Op, r.Expr));

    // Additive operations (+, -)
    public static TokenListParser<CypherToken, Expression> AdditiveExpression { get; } =
        from first in MultiplicativeExpression
        from rest in (
            from op in (
                Token.EqualTo(CypherToken.Plus).Value(BinaryOperator.Add)
                .Or(Token.EqualTo(CypherToken.Dash).Value(BinaryOperator.Subtract))
            )
            from next in MultiplicativeExpression
            select (Op: op, Expr: next)
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, r.Op, r.Expr));

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
            // REGEX MATCH (=~)
            .Or(from regex in Token.EqualTo(CypherToken.RegexMatch)
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, BinaryOperator.RegexMatch, r)))
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

    // Logical XOR
    public static TokenListParser<CypherToken, Expression> XorExpression { get; } =
        from first in AndExpression
        from rest in (
            from xor in Token.EqualTo(CypherToken.Xor)
            from next in AndExpression
            select next
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, BinaryOperator.Xor, r));

    // Logical OR
    public static TokenListParser<CypherToken, Expression> OrExpression { get; } =
        from first in XorExpression
        from rest in (
            from or in Token.EqualTo(CypherToken.Or)
            from next in XorExpression
            select next
        ).Many()
        select rest.Aggregate(first, (l, r) => new BinaryExpression(l, BinaryOperator.Or, r));

    public static TokenListParser<CypherToken, Expression> ExpressionParser { get; } =
        OrExpression;
}
