using System.Linq.Expressions;
using System.Reflection;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Cypher.Linq.Entities;
using BinaryExpression = CodeExplorer.Cypher.Ast.BinaryExpression;
using Expression = CodeExplorer.Cypher.Ast.Expression;
using LinqBinaryExpression = System.Linq.Expressions.BinaryExpression;
using LinqExpression = System.Linq.Expressions.Expression;
using LinqParameterExpression = System.Linq.Expressions.ParameterExpression;
using LinqUnaryExpression = System.Linq.Expressions.UnaryExpression;
using ParameterExpression = CodeExplorer.Cypher.Ast.ParameterExpression;
using UnaryExpression = CodeExplorer.Cypher.Ast.UnaryExpression;

namespace CodeExplorer.Cypher.Linq;

public class CypherQueryModel
{
    public Type EntityType { get; set; } = typeof(GraphEntity);
    public string VariableName { get; set; } = "n";
    public string? Label { get; set; }
    public List<Expression> WherePredicates { get; } = [];
    public List<OrderByItem> OrderByItems { get; } = [];
    public int? SkipCount { get; set; }
    public int? LimitCount { get; set; }
    public bool IsCountQuery { get; set; }
    public bool IsAnyQuery { get; set; }
    public List<ProjectionItem>? Projections { get; set; }
    public Dictionary<string, object?> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public CypherQuery BuildAst()
    {
        var nodePattern = new NodePattern(
            VariableName,
            Label != null ? [Label] : [],
            null
        );

        var pathPattern = new PathPattern(nodePattern, []);
        var matchClause = new MatchClause(false, [pathPattern]);

        Expression? combinedWhere = null;
        if (WherePredicates.Count > 0)
        {
            combinedWhere = WherePredicates[0];
            for (var i = 1; i < WherePredicates.Count; i++)
            {
                combinedWhere = new BinaryExpression(combinedWhere, BinaryOperator.And, WherePredicates[i]);
            }
        }

        var whereClause = combinedWhere != null ? new WhereClause(combinedWhere) : null;

        ReturnClause returnClause;
        if (IsCountQuery)
        {
            var countFunc = new FunctionCallExpression("count", false, [new IdentifierExpression(VariableName)]);
            returnClause = new ReturnClause(false, [new ProjectionItem(countFunc, "count")]);
        }
        else if (IsAnyQuery)
        {
            var countFunc = new FunctionCallExpression("count", false, [new IdentifierExpression(VariableName)]);
            var gtZero = new BinaryExpression(countFunc, BinaryOperator.GreaterThan, new NumberLiteralExpression(0, true));
            returnClause = new ReturnClause(false, [new ProjectionItem(gtZero, "any")]);
        }
        else if (Projections != null && Projections.Count > 0)
        {
            returnClause = new ReturnClause(false, Projections);
        }
        else
        {
            returnClause = new ReturnClause(false, [new ProjectionItem(new IdentifierExpression(VariableName), null)]);
        }

        OrderByClause? orderByClause = OrderByItems.Count > 0 ? new OrderByClause(OrderByItems) : null;
        SkipClause? skipClause = SkipCount.HasValue ? new SkipClause(SkipCount.Value) : null;
        LimitClause? limitClause = LimitCount.HasValue ? new LimitClause(LimitCount.Value) : null;

        return new CypherQuery(
            Matches: [matchClause],
            Where: whereClause,
            Return: returnClause,
            OrderBy: orderByClause,
            Skip: skipClause,
            Limit: limitClause
        );
    }
}

public class CypherExpressionVisitor
{
    private readonly CypherQueryModel _model = new();
    private int _paramCounter;

    public CypherQueryModel Model => _model;

    public static CypherQueryModel Translate(LinqExpression expression, Type entityType)
    {
        var visitor = new CypherExpressionVisitor();
        visitor._model.EntityType = entityType;
        visitor._model.Label = ResolveLabel(entityType);
        visitor.VisitQuery(expression);
        return visitor._model;
    }

    private static string? ResolveLabel(Type entityType)
    {
        if (entityType == typeof(ServiceEntity)) return NodeKind.Service.ToCypherLabel();
        if (entityType == typeof(AppEntity)) return NodeKind.App.ToCypherLabel();
        if (entityType == typeof(WorkerEntity)) return NodeKind.Worker.ToCypherLabel();
        if (entityType == typeof(CliToolEntity)) return NodeKind.CliTool.ToCypherLabel();
        if (entityType == typeof(DatabaseEntity)) return NodeKind.Database.ToCypherLabel();
        if (entityType == typeof(TopicEntity)) return NodeKind.Topic.ToCypherLabel();
        if (entityType == typeof(ExternalServiceEntity)) return NodeKind.ExternalService.ToCypherLabel();
        if (entityType == typeof(LibraryEntity)) return NodeKind.Library.ToCypherLabel();
        if (entityType == typeof(EndpointEntity)) return NodeKind.Endpoint.ToCypherLabel();
        return null;
    }

    public void VisitQuery(LinqExpression expression)
    {
        // Unroll method call chain
        var stack = new Stack<MethodCallExpression>();
        var current = expression;

        while (current is MethodCallExpression mce)
        {
            stack.Push(mce);
            current = mce.Arguments.Count > 0 ? mce.Arguments[0] : null;
        }

        while (stack.Count > 0)
        {
            var call = stack.Pop();
            ProcessMethodCall(call);
        }
    }

    private void ProcessMethodCall(MethodCallExpression call)
    {
        var methodName = call.Method.Name;

        switch (methodName)
        {
            case nameof(Queryable.Where):
                if (call.Arguments.Count > 1)
                {
                    var lambda = StripQuotes(call.Arguments[1]) as LambdaExpression;
                    if (lambda != null)
                    {
                        var cypherExpr = TranslatePredicate(lambda.Body);
                        if (cypherExpr != null)
                        {
                            _model.WherePredicates.Add(cypherExpr);
                        }
                    }
                }
                break;

            case nameof(Queryable.OrderBy):
                AddOrderBy(call, isDescending: false);
                break;

            case nameof(Queryable.OrderByDescending):
                AddOrderBy(call, isDescending: true);
                break;

            case nameof(Queryable.ThenBy):
                AddOrderBy(call, isDescending: false);
                break;

            case nameof(Queryable.ThenByDescending):
                AddOrderBy(call, isDescending: true);
                break;

            case nameof(Queryable.Skip):
                if (call.Arguments.Count > 1)
                {
                    var count = EvaluateConstant<int>(call.Arguments[1]);
                    _model.SkipCount = count;
                }
                break;

            case nameof(Queryable.Take):
                if (call.Arguments.Count > 1)
                {
                    var count = EvaluateConstant<int>(call.Arguments[1]);
                    _model.LimitCount = count;
                }
                break;

            case nameof(Queryable.Count):
                _model.IsCountQuery = true;
                break;

            case nameof(Queryable.Any):
                _model.IsAnyQuery = true;
                break;
        }
    }

    private void AddOrderBy(MethodCallExpression call, bool isDescending)
    {
        if (call.Arguments.Count > 1)
        {
            var lambda = StripQuotes(call.Arguments[1]) as LambdaExpression;
            if (lambda != null)
            {
                var cypherExpr = TranslateExpression(lambda.Body);
                if (cypherExpr != null)
                {
                    _model.OrderByItems.Add(new OrderByItem(cypherExpr, isDescending));
                }
            }
        }
    }

    private Expression? TranslateExpression(LinqExpression expr)
    {
        switch (expr)
        {
            case LinqBinaryExpression bin:
                return TranslateBinary(bin);

            case LinqUnaryExpression un:
                return TranslateUnary(un);

            case MemberExpression member:
                return TranslateMember(member);

            case ConstantExpression constant:
                return TranslateConstant(constant.Value);

            case MethodCallExpression call:
                return TranslateMethodCall(call);

            default:
                var evaluated = EvaluateConstant<object?>(expr);
                return TranslateConstant(evaluated);
        }
    }

    private Expression? TranslatePredicate(LinqExpression expr)
    {
        var result = TranslateExpression(expr);
        if (result is PropertyAccessExpression prop && expr.Type == typeof(bool))
        {
            return new BinaryExpression(prop, BinaryOperator.Equal, new BooleanLiteralExpression(true));
        }
        return result;
    }

    private Expression? TranslateBinary(LinqBinaryExpression bin)
    {
        // Logical AND/OR
        if (bin.NodeType == ExpressionType.AndAlso || bin.NodeType == ExpressionType.OrElse)
        {
            var leftPred = TranslatePredicate(bin.Left);
            var rightPred = TranslatePredicate(bin.Right);
            if (leftPred == null || rightPred == null) return null;
            var logicalOp = bin.NodeType == ExpressionType.AndAlso ? BinaryOperator.And : BinaryOperator.Or;
            return new BinaryExpression(leftPred, logicalOp, rightPred);
        }

        // Check for null comparisons
        if (bin.NodeType == ExpressionType.Equal || bin.NodeType == ExpressionType.NotEqual)
        {
            var isLeftNull = IsNullConstant(bin.Left);
            var isRightNull = IsNullConstant(bin.Right);

            if (isLeftNull && !isRightNull)
            {
                var right = TranslateExpression(bin.Right);
                if (right == null) return null;
                return bin.NodeType == ExpressionType.Equal
                    ? new UnaryExpression(UnaryOperator.IsNull, right)
                    : new UnaryExpression(UnaryOperator.IsNotNull, right);
            }
            if (isRightNull && !isLeftNull)
            {
                var left = TranslateExpression(bin.Left);
                if (left == null) return null;
                return bin.NodeType == ExpressionType.Equal
                    ? new UnaryExpression(UnaryOperator.IsNull, left)
                    : new UnaryExpression(UnaryOperator.IsNotNull, left);
            }
        }

        var l = TranslateExpression(bin.Left);
        var r = TranslateExpression(bin.Right);

        if (l == null || r == null) return null;

        var op = bin.NodeType switch
        {
            ExpressionType.Equal => BinaryOperator.Equal,
            ExpressionType.NotEqual => BinaryOperator.NotEqual,
            ExpressionType.LessThan => BinaryOperator.LessThan,
            ExpressionType.LessThanOrEqual => BinaryOperator.LessOrEqual,
            ExpressionType.GreaterThan => BinaryOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => BinaryOperator.GreaterOrEqual,
            _ => BinaryOperator.Equal
        };

        return new BinaryExpression(l, op, r);
    }

    private Expression? TranslateUnary(LinqUnaryExpression un)
    {
        if (un.NodeType == ExpressionType.Not)
        {
            var inner = TranslateExpression(un.Operand);
            if (inner == null) return null;

            if (inner is PropertyAccessExpression prop)
            {
                var isNull = new UnaryExpression(UnaryOperator.IsNull, prop);
                var isFalse = new BinaryExpression(prop, BinaryOperator.Equal, new BooleanLiteralExpression(false));
                return new BinaryExpression(isNull, BinaryOperator.Or, isFalse);
            }

            return new UnaryExpression(UnaryOperator.Not, inner);
        }

        if (un.NodeType == ExpressionType.Convert)
        {
            return TranslateExpression(un.Operand);
        }

        return null;
    }

    private Expression? TranslateMember(MemberExpression member)
    {
        // If the member access is on the query lambda parameter: s.Name -> n.name
        if (IsParameterAccess(member))
        {
            var propName = MapPropertyName(member.Member.Name);
            return new PropertyAccessExpression(_model.VariableName, propName);
        }

        // Captured variable/closure
        var val = EvaluateConstant<object?>(member);
        return TranslateConstant(val);
    }

    private Expression? TranslateMethodCall(MethodCallExpression call)
    {
        // String.Contains, StartsWith, EndsWith, Equals
        if (call.Method.DeclaringType == typeof(string))
        {
            if (call.Object != null && call.Arguments.Count >= 1)
            {
                var target = TranslateExpression(call.Object);
                var arg = TranslateExpression(call.Arguments[0]);

                if (target != null && arg != null)
                {
                    return call.Method.Name switch
                    {
                        nameof(string.Contains) => new BinaryExpression(target, BinaryOperator.Contains, arg),
                        nameof(string.StartsWith) => new BinaryExpression(target, BinaryOperator.StartsWith, arg),
                        nameof(string.EndsWith) => new BinaryExpression(target, BinaryOperator.EndsWith, arg),
                        nameof(string.Equals) => new BinaryExpression(target, BinaryOperator.Equal, arg),
                        _ => null
                    };
                }
            }
        }

        // Static string.Equals(a, b)
        if (call.Method.DeclaringType == typeof(string) && call.Method.Name == nameof(string.Equals) && call.Arguments.Count >= 2)
        {
            var l = TranslateExpression(call.Arguments[0]);
            var r = TranslateExpression(call.Arguments[1]);
            if (l != null && r != null)
            {
                return new BinaryExpression(l, BinaryOperator.Equal, r);
            }
        }

        // Otherwise evaluate constant
        var val = EvaluateConstant<object?>(call);
        return TranslateConstant(val);
    }

    private Expression TranslateConstant(object? val)
    {
        if (val == null)
            return new NullLiteralExpression();

        if (val is string s)
        {
            var paramName = NextParamName();
            _model.Parameters[paramName] = s;
            return new ParameterExpression(paramName);
        }

        if (val is bool b)
            return new BooleanLiteralExpression(b);

        if (val is int i)
            return new NumberLiteralExpression(i, true);

        if (val is long l)
            return new NumberLiteralExpression(l, true);

        if (val is double d)
            return new NumberLiteralExpression(d, false);

        if (val is float f)
            return new NumberLiteralExpression(f, false);

        if (val is Enum e)
        {
            var paramName = NextParamName();
            _model.Parameters[paramName] = e.ToString();
            return new ParameterExpression(paramName);
        }

        var pName = NextParamName();
        _model.Parameters[pName] = val;
        return new ParameterExpression(pName);
    }

    private string NextParamName()
    {
        return $"p{_paramCounter++}";
    }

    private static bool IsParameterAccess(MemberExpression member)
    {
        var current = member.Expression;
        while (current != null)
        {
            if (current is LinqParameterExpression || current.NodeType == ExpressionType.Parameter)
                return true;
            if (current is MemberExpression innerMember)
                current = innerMember.Expression;
            else
                break;
        }
        return false;
    }

    private static bool IsNullConstant(LinqExpression expr)
    {
        if (expr is ConstantExpression ce && ce.Value == null) return true;
        return false;
    }

    private static string MapPropertyName(string memberName) => memberName switch
    {
        "Id" => "id",
        "Name" => "name",
        "DisplayName" => "display_name",
        "FilePath" => "file_path",
        "LineStart" => "line_start",
        "LineEnd" => "line_end",
        "Framework" => "framework",
        "Language" => "language",
        "Role" => "role",
        "Layer" => "layer",
        "IsLibrary" => "is_library",
        "DbType" => "db_type",
        "Engine" => "engine",
        "Schema" => "schema",
        "BrokerType" => "broker_type",
        "IsInternal" => "is_internal",
        "Protocol" => "protocol",
        "DomainOrService" => "domain_or_service",
        "BaseUrl" => "base_url",
        "IsExternal" => "is_external",
        "ProjectType" => "project_type",
        "RouteTemplate" => "route_template",
        "HttpMethod" => "http_method",
        _ => ToSnakeCase(memberName)
    };

    private static string ToSnakeCase(string str)
    {
        return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLowerInvariant();
    }

    private static LinqExpression StripQuotes(LinqExpression expression)
    {
        while (expression.NodeType == ExpressionType.Quote)
        {
            expression = ((LinqUnaryExpression)expression).Operand;
        }
        return expression;
    }

    private static T EvaluateConstant<T>(LinqExpression expression)
    {
        if (expression is ConstantExpression ce && ce.Value is T val)
            return val;

        var lambda = LinqExpression.Lambda(expression);
        var compiled = lambda.Compile();
        return (T)compiled.DynamicInvoke()!;
    }
}
