using System.Globalization;
using CodeExplorer.Cypher.Ast;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace CodeExplorer.Cypher.Parser;

public static class ExpressionParsers
{
    public static TokenListParser<CypherToken, Expression> ExpressionParser { get; } =
        input => OrExpression!(input);
    public static TokenListParser<CypherToken, string> PropertyNameText { get; } =
        CypherTokenizer.IdentifierCompatibleKeywords
            .Prepend(CypherToken.Identifier)
            .Select(Token.EqualTo)
            .Aggregate((a, b) => a.Or(b))
            .Select(t =>
            {
                var str = t.ToStringValue();
                if (str.StartsWith('`') && str.EndsWith('`') && str.Length >= 2)
                    return str[1..^1];
                return str;
            });

    public static TokenListParser<CypherToken, Expression> StringLiteral { get; } =
        Token.EqualTo(CypherToken.StringLiteral).Select(Expression (t) =>
        {
            var raw = t.ToStringValue();
            var content = raw.Length >= 2 ? raw[1..^1] : raw;
            var unescaped = content
                .Replace("\\'", "'")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t");
            return new StringLiteralExpression(unescaped);
        });

    public static TokenListParser<CypherToken, Expression> NumberLiteral { get; } =
        Token.EqualTo(CypherToken.Number).Select(Expression (t) =>
        {
            var str = t.ToStringValue();
            if (str.StartWithIC("0x"))
            {
                var hexVal = Convert.ToInt64(str[2..], 16);
                return new NumberLiteralExpression(hexVal, true);
            }
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                return new NumberLiteralExpression(intVal, true);
            return new NumberLiteralExpression(double.Parse(str, NumberStyles.Float, CultureInfo.InvariantCulture), false);
        });

    public static TokenListParser<CypherToken, Expression> BooleanLiteral { get; } =
        Token.EqualTo(CypherToken.True).Value((Expression)new BooleanLiteralExpression(true))
        .Or(Token.EqualTo(CypherToken.False).Value((Expression)new BooleanLiteralExpression(false)));

    public static TokenListParser<CypherToken, Expression> NullLiteral { get; } =
        Token.EqualTo(CypherToken.Null).Value((Expression)new NullLiteralExpression());

    public static TokenListParser<CypherToken, Expression> Parameter { get; } =
        Token.EqualTo(CypherToken.Parameter).Select(Expression (t) =>
        {
            var str = t.ToStringValue();
            return new ParameterExpression(str.TrimStart('$'));
        });

    private static TokenListParser<CypherToken, Expression?> CaseTestExpression { get; } =
        input =>
        {
            var next = input.ConsumeToken();
            if (next is { HasValue: true, Value.Kind: CypherToken.When })
            {
                return TokenListParserResult.Value<CypherToken, Expression?>(null, input, input);
            }
            var res = ExpressionParser(input);
            if (res.HasValue)
            {
                return TokenListParserResult.Value<CypherToken, Expression?>(res.Value, input, res.Remainder);
            }
            return TokenListParserResult.CastEmpty<CypherToken, Expression, Expression?>(res);
        };

    public static TokenListParser<CypherToken, Expression> CaseExpression { get; } =
        from caseTok in Token.EqualTo(CypherToken.Case)
        from testExpr in Parse.Ref(() => CaseTestExpression)
        from branches in (
            from whenTok in Token.EqualTo(CypherToken.When)
            from whenExpr in Parse.Ref(() => ExpressionParser)
            from thenTok in Token.EqualTo(CypherToken.Then)
            from thenExpr in Parse.Ref(() => ExpressionParser)
            select new CaseWhenItem(whenExpr, thenExpr)
        ).AtLeastOnce()
        from elseExpr in (
            from elseTok in Token.EqualTo(CypherToken.Else)
            from e in Parse.Ref(() => ExpressionParser)
            select e
        ).OptionalOrDefault()
        from endTok in Token.EqualTo(CypherToken.End)
        select (Expression)new CaseExpression(testExpr, [.. branches], elseExpr);

    public static TokenListParser<CypherToken, Expression> ListComprehensionOrListLiteral { get; } =
        (from open in Token.EqualTo(CypherToken.LBracket)
         from varName in PropertyNameText
         from inTok in Token.EqualTo(CypherToken.In)
         from listExpr in Parse.Ref(() => ExpressionParser)
         from whereExpr in (
             from whereTok in Token.EqualTo(CypherToken.Where)
             from p in Parse.Ref(() => ExpressionParser)
             select p
         ).OptionalOrDefault()
         from pipe in (
             from p in Token.EqualTo(CypherToken.Pipe)
             from proj in Parse.Ref(() => ExpressionParser)
             select proj
         ).OptionalOrDefault()
         from close in Token.EqualTo(CypherToken.RBracket)
         select (Expression)new ListComprehensionExpression(varName, listExpr, whereExpr, pipe))
        .Try()
        .Or(
         from open in Token.EqualTo(CypherToken.LBracket)
         from items in Parse.Ref(() => ExpressionParser).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
         from close in Token.EqualTo(CypherToken.RBracket)
         select (Expression)new ListExpression([.. items]));

    public static TokenListParser<CypherToken, Expression> MapLiteral { get; } =
        from open in Token.EqualTo(CypherToken.LBrace)
        from pairs in (
            from key in PropertyNameText
            from colon in Token.EqualTo(CypherToken.Colon)
            from val in Parse.Ref(() => ExpressionParser)
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
        from listExpr in Parse.Ref(() => ExpressionParser)
        from whereTok in Token.EqualTo(CypherToken.Where)
        from pred in Parse.Ref(() => ExpressionParser)
        from close in Token.EqualTo(CypherToken.RParen)
        select (Expression)new ListPredicateExpression(quant.ToLowerInvariant(), varName, listExpr, pred);

    public static TokenListParser<CypherToken, Expression> ParenthesizedExpression { get; } =
        from open in Token.EqualTo(CypherToken.LParen)
        from expr in Parse.Ref(() => ExpressionParser)
        from close in Token.EqualTo(CypherToken.RParen)
        select expr;

    public static TokenListParser<CypherToken, Expression> FunctionCallOrIdentifier { get; } =
        from name in PropertyNameText
        from call in (
            from open in Token.EqualTo(CypherToken.LParen)
            from distinct in Token.EqualTo(CypherToken.Distinct).Optional()
            from args in Parse.Ref(() => ExpressionParser).ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
            from close in Token.EqualTo(CypherToken.RParen)
            select (IsFunction: true, Distinct: distinct.HasValue, Arguments: args.ToList())
        ).Optional()
        select call is { IsFunction: true }
            ? new FunctionCallExpression(name, call.Value.Distinct, call.Value.Arguments)
            : (Expression)new IdentifierExpression(name);

    public static TokenListParser<CypherToken, Expression> GraphPatternExpression { get; } =
        (from path in PatternParsers.Path
         where path.Chain.Count > 0 || path.Head.Properties != null || (path.Head.Variable == null && path.Head.Labels.Count > 0)
         select (Expression)new PatternExpression(path)).Try();

    public static TokenListParser<CypherToken, Expression> PatternComprehension { get; } =
        (from open in Token.EqualTo(CypherToken.LBracket)
         from path in PatternParsers.Path
         where path.Chain.Count > 0
         from whereExpr in (
             from whereTok in Token.EqualTo(CypherToken.Where)
             from p in Parse.Ref(() => ExpressionParser)
             select p
         ).OptionalOrDefault()
         from pipe in Token.EqualTo(CypherToken.Pipe)
         from proj in Parse.Ref(() => ExpressionParser)
         from close in Token.EqualTo(CypherToken.RBracket)
         select (Expression)new PatternComprehensionExpression(path, whereExpr, proj)).Try();

    public static TokenListParser<CypherToken, Expression> ReduceExpressionParser { get; } =
        (from reduceName in PropertyNameText
         where reduceName.Equals("reduce", StringComparison.OrdinalIgnoreCase)
         from open in Token.EqualTo(CypherToken.LParen)
         from acc in PropertyNameText
         from eq in Token.EqualTo(CypherToken.Equal)
         from init in Parse.Ref(() => ExpressionParser)
         from comma in Token.EqualTo(CypherToken.Comma)
         from varName in PropertyNameText
         from inTok in Token.EqualTo(CypherToken.In)
         from list in Parse.Ref(() => ExpressionParser)
         from pipe in Token.EqualTo(CypherToken.Pipe)
         from step in Parse.Ref(() => ExpressionParser)
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
         from expr in Parse.Ref(() => ExpressionParser)
         select new MapProjectionElement(prop, expr, false));

    private const string FuncItemAt = "item_at";

    private static Expression BuildPropertyAccess(Expression current, string property) =>
        current switch
        {
            IdentifierExpression idExpr => new PropertyAccessExpression(idExpr.Name, property),
            PropertyAccessExpression propExpr => new PropertyAccessExpression($"{propExpr.Variable}.{propExpr.PropertyName}", property),
            _ => new FunctionCallExpression(FuncItemAt, false, [current, new StringLiteralExpression(property)])
        };

    private static TokenListParser<CypherToken, Func<Expression, Expression>> PropertyAccessContinuation { get; } =
        from dot in Token.EqualTo(CypherToken.Dot)
        from prop in PropertyNameText
        select (Func<Expression, Expression>)(current => BuildPropertyAccess(current, prop));

    private static TokenListParser<CypherToken, Func<Expression, Expression>> IndexOrSliceContinuation { get; } =
        from lbracket in Token.EqualTo(CypherToken.LBracket)
        from continuation in (
            from fromExpr in Parse.Ref(() => ExpressionParser).OptionalOrDefault()
            from dotdot in Token.EqualTo(CypherToken.DotDot)
            from toExpr in Parse.Ref(() => ExpressionParser).OptionalOrDefault()
            select (Func<Expression, Expression>)(current => new ListSliceExpression(current, fromExpr, toExpr))
        ).Try().Or(
            from idx in Parse.Ref(() => ExpressionParser)
            select (Func<Expression, Expression>)(current => new FunctionCallExpression(FuncItemAt, false, [current, idx]))
        )
        from rbracket in Token.EqualTo(CypherToken.RBracket)
        select continuation;

    private static TokenListParser<CypherToken, Func<Expression, Expression>> LabelPredicateContinuation { get; } =
        from colon in Token.EqualTo(CypherToken.Colon)
        from label in PropertyNameText
        select (Func<Expression, Expression>)(current => new HasLabelExpression(current, label));

    private static TokenListParser<CypherToken, Func<Expression, Expression>> MapProjectionContinuation { get; } =
        (from open in Token.EqualTo(CypherToken.LBrace)
         from elements in MapProjectionElementParser.ManyDelimitedBy(Token.EqualTo(CypherToken.Comma))
         from close in Token.EqualTo(CypherToken.RBrace)
         select (Func<Expression, Expression>)(current => new MapProjectionExpression(current, [.. elements]))).Try();

    // Postfix operations: property access (.prop), index/slice access ([0], [1..3]), label predicate (:Label), or map projection ({...})
    public static TokenListParser<CypherToken, Expression> PostfixExpression { get; } =
        from baseExpr in Atom
        from continuations in PropertyAccessContinuation
            .Or(IndexOrSliceContinuation)
            .Or(LabelPredicateContinuation)
            .Or(MapProjectionContinuation)
            .Many()
        select continuations.Aggregate(baseExpr, (current, apply) => apply(current));

    private static TokenListParser<CypherToken, BinaryOperator> BinaryOp(
        params (CypherToken Token, BinaryOperator Operator)[] mappings) =>
        mappings
            .Select(m => Token.EqualTo(m.Token).Value(m.Operator))
            .Aggregate((a, b) => a.Or(b));

    private static TokenListParser<CypherToken, Expression> ChainBinary(
        TokenListParser<CypherToken, Expression> operand,
        params (CypherToken Token, BinaryOperator Operator)[] mappings) =>
        Parse.Chain(
            BinaryOp(mappings),
            operand,
            (op, left, right) => new BinaryExpression(left, op, right));

    // Unary prefix: -, +
    public static TokenListParser<CypherToken, Expression> UnaryExpression { get; } =
        (from dash in Token.EqualTo(CypherToken.Dash)
         from expr in Parse.Ref(() => UnaryExpression!)
         select (Expression)new UnaryExpression(UnaryOperator.Minus, expr))
        .Or(from plus in Token.EqualTo(CypherToken.Plus)
            from expr in Parse.Ref(() => UnaryExpression!)
            select (Expression)new UnaryExpression(UnaryOperator.Plus, expr))
        .Or(PostfixExpression);

    // Power (^)
    public static TokenListParser<CypherToken, Expression> PowerExpression { get; } =
        ChainBinary(UnaryExpression, (CypherToken.Caret, BinaryOperator.Power));

    // Multiplicative operations (*, /, %)
    public static TokenListParser<CypherToken, Expression> MultiplicativeExpression { get; } =
        ChainBinary(
            PowerExpression,
            (CypherToken.Asterisk, BinaryOperator.Multiply),
            (CypherToken.Slash, BinaryOperator.Divide),
            (CypherToken.Percent, BinaryOperator.Modulo));

    // Additive operations (+, -)
    public static TokenListParser<CypherToken, Expression> AdditiveExpression { get; } =
        ChainBinary(
            MultiplicativeExpression,
            (CypherToken.Plus, BinaryOperator.Add),
            (CypherToken.Dash, BinaryOperator.Subtract));

    private static TokenListParser<CypherToken, BinaryOperator> StandardComparisonOperator { get; } =
        BinaryOp(
            (CypherToken.Equal, BinaryOperator.Equal),
            (CypherToken.NotEqual, BinaryOperator.NotEqual),
            (CypherToken.LessOrEqual, BinaryOperator.LessOrEqual),
            (CypherToken.GreaterOrEqual, BinaryOperator.GreaterOrEqual),
            (CypherToken.LessThan, BinaryOperator.LessThan),
            (CypherToken.GreaterThan, BinaryOperator.GreaterThan));

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
            // CONTAINS / IN / REGEX MATCH / Standard comparisons
            .Or(from op in BinaryOp(
                    (CypherToken.Contains, BinaryOperator.Contains),
                    (CypherToken.In, BinaryOperator.In),
                    (CypherToken.RegexMatch, BinaryOperator.RegexMatch))
                .Or(StandardComparisonOperator)
                from r in AdditiveExpression
                select (Func<Expression, Expression>)(l => new BinaryExpression(l, op, r)))
        ).OptionalOrDefault()
        select right != null ? right(left) : left;

    // Logical NOT (binds looser than comparisons/CONTAINS, tighter than AND)
    public static TokenListParser<CypherToken, Expression> NotExpression { get; } =
        (from not in Token.EqualTo(CypherToken.Not)
         from expr in Parse.Ref(() => NotExpression!)
         select (Expression)new UnaryExpression(UnaryOperator.Not, expr))
        .Or(ComparisonExpression);

    // Logical AND
    public static TokenListParser<CypherToken, Expression> AndExpression { get; } =
        ChainBinary(NotExpression, (CypherToken.And, BinaryOperator.And));

    // Logical XOR
    public static TokenListParser<CypherToken, Expression> XorExpression { get; } =
        ChainBinary(AndExpression, (CypherToken.Xor, BinaryOperator.Xor));

    // Logical OR
    public static TokenListParser<CypherToken, Expression> OrExpression { get; } =
        ChainBinary(XorExpression, (CypherToken.Or, BinaryOperator.Or));
}
