namespace CodeExplorer.Cypher.Ast;

public abstract record Expression;

public record IdentifierExpression(string Name) : Expression;

public record PropertyAccessExpression(string Variable, string PropertyName) : Expression;

public record StringLiteralExpression(string Value) : Expression;

public record NumberLiteralExpression(double Value, bool IsInteger) : Expression;

public record BooleanLiteralExpression(bool Value) : Expression;

public record NullLiteralExpression() : Expression;

public record ParameterExpression(string Name) : Expression;

public record BinaryExpression(Expression Left, BinaryOperator Operator, Expression Right) : Expression;

public record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;

public record FunctionCallExpression(string FunctionName, bool IsDistinct, List<Expression> Arguments) : Expression;

public record ListExpression(List<Expression> Items) : Expression;

public record CaseWhenItem(Expression When, Expression Then);

public record CaseExpression(Expression? TestExpression, List<CaseWhenItem> WhenBranches, Expression? ElseExpression) : Expression;

public record HasLabelExpression(Expression Expression, string Label) : Expression;

public record ListPredicateExpression(string Quantifier, string Variable, Expression List, Expression Predicate) : Expression;

public record ListComprehensionExpression(string Variable, Expression List, Expression? Filter, Expression? Projection) : Expression;

public record MapLiteralExpression(Dictionary<string, Expression> Properties) : Expression;

public record WildcardExpression() : Expression;

public record ListSliceExpression(Expression List, Expression? From, Expression? To) : Expression;

public record PatternExpression(PathPattern Path) : Expression;

public record PatternComprehensionExpression(PathPattern Path, Expression? Filter, Expression Projection) : Expression;

public record ReduceExpression(string Accumulator, Expression Initial, string Variable, Expression List, Expression Expression) : Expression;

public record MapProjectionElement(string PropertyName, Expression? ValueExpression, bool IsAllProperties = false);

public record MapProjectionExpression(Expression BaseExpression, List<MapProjectionElement> Elements) : Expression;
