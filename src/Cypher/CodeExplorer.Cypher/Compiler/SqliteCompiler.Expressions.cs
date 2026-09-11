using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
    public string VisitExpression(Expression expression)
    {
        return expression switch
        {
            IdentifierExpression id => VisitIdentifier(id),
            PropertyAccessExpression prop => VisitPropertyAccess(prop),
            StringLiteralExpression str => VisitStringLiteral(str),
            NumberLiteralExpression num => VisitNumberLiteral(num),
            BooleanLiteralExpression b => b.Value ? "1" : "0",
            NullLiteralExpression => "NULL",
            ParameterExpression param => "@" + param.Name,
            BinaryExpression binary => VisitBinary(binary),
            UnaryExpression unary => VisitUnary(unary),
            FunctionCallExpression func => VisitFunctionCall(func),
            ListExpression list => VisitList(list),
            CaseExpression caseExpr => VisitCase(caseExpr),
            HasLabelExpression hasLabel => VisitHasLabel(hasLabel),
            MapLiteralExpression map => VisitMapLiteral(map),
            ListComprehensionExpression comp => VisitListComprehension(comp),
            ListPredicateExpression pred => VisitListPredicate(pred),
            WildcardExpression => "*",
            ListSliceExpression slice => VisitListSlice(slice),
            PatternExpression pat => VisitPatternExpression(pat),
            PatternComprehensionExpression patComp => VisitPatternComprehension(patComp),
            ReduceExpression red => VisitReduce(red),
            MapProjectionExpression mapProj => VisitMapProjection(mapProj),
            _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported.")
        };
    }

    private string VisitIdentifier(IdentifierExpression id)
    {
        if (_withAliases.TryGetValue(id.Name, out var aliasSql))
        {
            return aliasSql;
        }

        var escaped = EscapeVar(id.Name);

        if (_unwindVariables.Contains(id.Name))
        {
            return $"{escaped}.value";
        }

        if (_pathVariables.TryGetValue(id.Name, out var relVar))
        {
            return $"{EscapeVar(relVar)}.path_nodes";
        }

        return TryVisitNodeIdentifier(id.Name, escaped) ?? escaped;
    }

    private string? TryVisitNodeIdentifier(string name, string escaped)
    {
        if (_nodePropertySource.TryGetValue(name, out var propSource))
        {
            var idSource = _nodeIdSource.TryGetValue(name, out var idSrc) ? idSrc : "NULL";
            return $"json_object('id', {idSource}, 'properties', json({propSource}))";
        }

        if (_declaredNodes.Contains(name))
        {
            return $"json_object('id', {escaped}.id, 'kind', {escaped}.kind, 'properties', json({escaped}.properties))";
        }

        return null;
    }

    private string VisitPropertyAccess(PropertyAccessExpression prop)
    {
        var v = EscapeVar(prop.Variable);
        if (_unwindVariables.Contains(prop.Variable))
        {
            return VisitUnwindPropertyAccess(v, prop.PropertyName);
        }

        if (_nodePropertySource.TryGetValue(prop.Variable, out var propSrc))
        {
            return VisitSourcePropertyAccess(prop.Variable, propSrc, prop.PropertyName);
        }

        return VisitDefaultPropertyAccess(v, prop.PropertyName);
    }

    private static string VisitUnwindPropertyAccess(string v, string propName)
    {
        if (propName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.value, '$.id'), {v}.value)";
        }

        if (propName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.value, '$.kind'), {v}.value)";
        }

        return $"COALESCE(json_extract({v}.value, '$.properties.' || '{propName}'), json_extract({v}.value, '$.{propName}'))";
    }

    private string VisitSourcePropertyAccess(string variable, string propSrc, string propName)
    {
        if (propName.Equals("id", StringComparison.OrdinalIgnoreCase) &&
            _nodeIdSource.TryGetValue(variable, out var idSrc))
        {
            return idSrc;
        }

        if (propName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({propSrc}, '$.kind'), {propSrc})";
        }

        return $"json_extract({propSrc}, '$.{propName}')";
    }

    private static string VisitDefaultPropertyAccess(string v, string propName)
    {
        if (propName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return $"{v}.id";
        }

        if (propName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.properties, '$.kind'), {v}.kind)";
        }

        return $"json_extract({v}.properties, '$.{propName}')";
    }

    private string VisitStringLiteral(StringLiteralExpression str)
    {
        var paramName = $"@p{_paramIndex++}";
        _parameters[paramName.TrimStart('@')] = str.Value;
        return paramName;
    }

    private string VisitNumberLiteral(NumberLiteralExpression num)
    {
        if (num.IsInteger)
        {
            return ((long)num.Value).ToString(CultureInfo.InvariantCulture);
        }

        return num.Value.ToString(CultureInfo.InvariantCulture);
    }

    private string VisitBinary(BinaryExpression binary)
    {
        var left = VisitExpression(binary.Left);
        var right = VisitExpression(binary.Right);

        return binary.Operator switch
        {
            BinaryOperator.Equal => $"({left} = {right})",
            BinaryOperator.NotEqual => $"({left} <> {right})",
            BinaryOperator.LessThan => $"({left} < {right})",
            BinaryOperator.GreaterThan => $"({left} > {right})",
            BinaryOperator.LessOrEqual => $"({left} <= {right})",
            BinaryOperator.GreaterOrEqual => $"({left} >= {right})",
            BinaryOperator.And => $"({left} AND {right})",
            BinaryOperator.Or => $"({left} OR {right})",
            BinaryOperator.StartsWith => $"({left} LIKE ({right} || '%'))",
            BinaryOperator.EndsWith => $"({left} LIKE ('%' || {right}))",
            BinaryOperator.Contains => $"({left} LIKE ('%' || {right} || '%'))",
            BinaryOperator.In => VisitInOperator(binary.Left, binary.Right, left, right),
            BinaryOperator.Add => VisitAddOperator(binary.Left, binary.Right, left, right),
            BinaryOperator.Subtract => $"({left} - {right})",
            BinaryOperator.Multiply => $"({left} * {right})",
            BinaryOperator.Divide => $"({left} / {right})",
            BinaryOperator.Modulo => $"({left} % {right})",
            BinaryOperator.Power => $"power({left}, {right})",
            BinaryOperator.RegexMatch => $"regexp({right}, {left})",
            BinaryOperator.Xor => $"(({left} AND NOT {right}) OR (NOT {left} AND {right}))",
            _ => throw new NotSupportedException($"Binary operator {binary.Operator} is not supported.")
        };
    }

    private string VisitInOperator(Expression leftExpr, Expression rightExpr, string leftSql, string rightSql)
    {
        if (rightExpr is ListExpression list && list.Items.Count == 0) return "(0 = 1)";
        if (rightExpr is ListExpression listExpr)
            return $"({leftSql} IN ({string.Join(", ", listExpr.Items.Select(VisitExpression))}))";

        if (rightExpr is IdentifierExpression rightId &&
            _withCollectNodes.TryGetValue(rightId.Name, out var collNode) &&
            leftExpr is IdentifierExpression leftId &&
            leftId.Name.Equals(collNode, StringComparison.OrdinalIgnoreCase))
        {
            return $"({EscapeVar(collNode)}.id IS NOT NULL)";
        }

        if (rightExpr is IdentifierExpression rId && _withListAliases.TryGetValue(rId.Name, out var targetExpr))
        {
            return $"({leftSql} = {VisitExpression(targetExpr)})";
        }

        return $"(EXISTS (SELECT 1 FROM json_each({rightSql}) WHERE json_each.value = {leftSql}))";
    }

    private static string VisitAddOperator(Expression leftExpr, Expression rightExpr, string leftSql, string rightSql)
    {
        // If both are numbers, use +
        if (leftExpr is NumberLiteralExpression && rightExpr is NumberLiteralExpression)
        {
            return $"({leftSql} + {rightSql})";
        }

        // In Cypher, + on strings or variables concatenates strings
        return $"({leftSql} || {rightSql})";
    }

    private string VisitUnary(UnaryExpression unary)
    {
        return unary.Operator switch
        {
            UnaryOperator.Minus => $"-({VisitExpression(unary.Operand)})",
            UnaryOperator.Plus => $"+({VisitExpression(unary.Operand)})",
            UnaryOperator.Not => $"(NOT {VisitExpression(unary.Operand)})",
            UnaryOperator.IsNull => VisitNullCheck(unary.Operand, isNull: true),
            UnaryOperator.IsNotNull => VisitNullCheck(unary.Operand, isNull: false),
            _ => throw new NotSupportedException($"Unary operator {unary.Operator} is not supported.")
        };
    }

    private string VisitNullCheck(Expression operand, bool isNull)
    {
        var op = isNull ? "IS NULL" : "IS NOT NULL";
        if (operand is IdentifierExpression id && _declaredNodes.Contains(id.Name))
        {
            return $"({id.Name}.id {op})";
        }

        return $"({VisitExpression(operand)} {op})";
    }

    private string VisitFunctionCall(FunctionCallExpression func)
    {
        var fn = func.FunctionName.ToLowerInvariant();
        var distinctStr = func.IsDistinct ? "DISTINCT " : "";

        return TryVisitItemAt(fn, func)
            ?? TryVisitPathLengthFunction(fn, func)
            ?? TryVisitIdOrElementId(fn, func)
            ?? TryVisitScalarFunction(fn, func)
            ?? TryVisitStringFunction(fn, func)
            ?? TryVisitAggregateFunction(fn, func, distinctStr)
            ?? TryVisitGraphIntrospection(fn, func)
            ?? $"{func.FunctionName}({distinctStr}{string.Join(", ", func.Arguments.Select(VisitExpression))})";
    }

    private string? TryVisitPathLengthFunction(string fn, FunctionCallExpression func)
    {
        if ((fn != "length" && fn != "size") || func.Arguments.Count != 1) return null;
        if (func.Arguments[0] is IdentifierExpression pathId && (_pathHops.ContainsKey(pathId.Name) || _pathVariables.ContainsKey(pathId.Name)))
        {
            return GetPathLengthSql(pathId.Name);
        }
        return null;
    }

    private string GetPathLengthSql(string pathVarName)
    {
        if (_pathHops.TryGetValue(pathVarName, out var hops) && hops.Count > 0)
        {
            var hopExprs = hops.Select(h => h.IsVarLen ? $"{EscapeVar(h.RelVar)}.depth" : "1");
            return string.Join(" + ", hopExprs);
        }

        if (_pathVariables.TryGetValue(pathVarName, out var relVar))
        {
            return _declaredRels.Contains(relVar) ? "1" : $"{EscapeVar(relVar)}.depth";
        }

        return "0";
    }

    private string? TryVisitItemAt(string fn, FunctionCallExpression func)
    {
        if (fn != "item_at" || func.Arguments.Count != 2) return null;

        if (func.Arguments[0] is FunctionCallExpression innerFunc &&
            innerFunc.FunctionName.Equals("labels", StringComparison.OrdinalIgnoreCase) &&
            func.Arguments[1] is NumberLiteralExpression num && num.Value == 0)
        {
            if (innerFunc.Arguments.Count > 0 && innerFunc.Arguments[0] is IdentifierExpression nodeVar)
            {
                return $"{nodeVar.Name}.kind";
            }
        }

        var targetList = VisitExpression(func.Arguments[0]);
        if (func.Arguments[1] is UnaryExpression { Operator: UnaryOperator.Minus } uMinus &&
            uMinus.Operand is NumberLiteralExpression numLiteral)
        {
            var offset = (long)numLiteral.Value - 1;
            var offsetStr = offset > 0 ? $" OFFSET {offset}" : "";
            return $"(SELECT value FROM json_each({targetList}) ORDER BY key DESC LIMIT 1{offsetStr})";
        }

        if (func.Arguments[1] is StringLiteralExpression strKey)
        {
            return $"json_extract({targetList}, '$.{strKey.Value}')";
        }

        return $"json_extract({targetList}, CASE WHEN typeof({VisitExpression(func.Arguments[1])}) = 'integer' THEN '$[' || {VisitExpression(func.Arguments[1])} || ']' ELSE '$.' || {VisitExpression(func.Arguments[1])} END)";
    }

    private string? TryVisitIdOrElementId(string fn, FunctionCallExpression func)
    {
        if ((fn != "id" && fn != "elementid") || func.Arguments.Count != 1) return null;

        if (func.Arguments[0] is IdentifierExpression id && _declaredNodes.Contains(id.Name))
        {
            return $"{EscapeVar(id.Name)}.id";
        }

        if (func.Arguments[0] is IdentifierExpression rel && _declaredRels.Contains(rel.Name))
        {
            return $"{EscapeVar(rel.Name)}.rowid";
        }

        return $"{VisitExpression(func.Arguments[0])}.id";
    }

    private string? TryVisitScalarFunction(string fn, FunctionCallExpression func)
    {
        if (fn == "size" && func.Arguments.Count == 1)
        {
            var argSql = VisitExpression(func.Arguments[0]);
            return $"CASE WHEN json_valid({argSql}) THEN json_array_length({argSql}) ELSE length({argSql}) END";
        }

        if (fn == "tointeger" && func.Arguments.Count == 1)
        {
            return $"CAST({VisitExpression(func.Arguments[0])} AS INTEGER)";
        }

        if (fn == "tofloat" && func.Arguments.Count == 1)
        {
            return $"CAST({VisitExpression(func.Arguments[0])} AS REAL)";
        }

        if (fn == "toboolean" && func.Arguments.Count == 1)
        {
            var argSql = VisitExpression(func.Arguments[0]);
            return $"CASE WHEN {argSql} IN ('true', '1', 1) THEN 1 WHEN {argSql} IN ('false', '0', 0) THEN 0 ELSE NULL END";
        }

        if (fn == "exists" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is PatternExpression pat) return VisitPatternExpression(pat);
            return $"({VisitExpression(func.Arguments[0])} IS NOT NULL)";
        }

        return null;
    }

    private string? TryVisitStringFunction(string fn, FunctionCallExpression func)
    {
        if (fn == "left" && func.Arguments.Count == 2)
            return $"substr({VisitExpression(func.Arguments[0])}, 1, {VisitExpression(func.Arguments[1])})";

        if (fn == "right" && func.Arguments.Count == 2)
            return $"substr({VisitExpression(func.Arguments[0])}, -({VisitExpression(func.Arguments[1])}))";

        if (fn == "ltrim" && func.Arguments.Count == 1)
            return $"ltrim({VisitExpression(func.Arguments[0])})";

        if (fn == "rtrim" && func.Arguments.Count == 1)
            return $"rtrim({VisitExpression(func.Arguments[0])})";

        if (fn == "tolower" && func.Arguments.Count == 1)
            return $"lower({VisitExpression(func.Arguments[0])})";

        if (fn == "toupper" && func.Arguments.Count == 1)
            return $"upper({VisitExpression(func.Arguments[0])})";

        if (fn == "tostring" && func.Arguments.Count == 1)
            return $"CAST({VisitExpression(func.Arguments[0])} AS TEXT)";

        if (fn == "length" && func.Arguments.Count == 1)
            return $"length({VisitExpression(func.Arguments[0])})";

        if (fn == "substring" && (func.Arguments.Count == 2 || func.Arguments.Count == 3))
            return VisitSubstringFunction(func);

        return null;
    }

    private string VisitSubstringFunction(FunctionCallExpression func)
    {
        var strSql = VisitExpression(func.Arguments[0]);
        var startSql = VisitExpression(func.Arguments[1]);
        return func.Arguments.Count == 3
            ? $"substr({strSql}, ({startSql}) + 1, {VisitExpression(func.Arguments[2])})"
            : $"substr({strSql}, ({startSql}) + 1)";
    }

    private string? TryVisitAggregateFunction(string fn, FunctionCallExpression func, string distinctStr)
    {
        if (fn == "count" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is WildcardExpression) return $"COUNT({distinctStr}*)";
            if (func.Arguments[0] is IdentifierExpression id && _declaredNodes.Contains(id.Name))
                return $"COUNT({distinctStr}{EscapeVar(id.Name)}.id)";
            return $"COUNT({distinctStr}{VisitExpression(func.Arguments[0])})";
        }

        if (fn == "collect" && func.Arguments.Count == 1)
        {
            return $"json_group_array({distinctStr}{VisitExpression(func.Arguments[0])})";
        }

        return null;
    }

    private string? TryVisitGraphIntrospection(string fn, FunctionCallExpression func)
    {
        if (fn == "labels" && func.Arguments.Count == 1 && func.Arguments[0] is IdentifierExpression nodeVar)
            return $"json_array({EscapeVar(nodeVar.Name)}.kind)";

        if (fn == "type" && func.Arguments.Count == 1 && func.Arguments[0] is IdentifierExpression relVar)
            return $"{EscapeVar(relVar.Name)}.kind";

        if (fn == "nodes" && func.Arguments.Count == 1 &&
            func.Arguments[0] is IdentifierExpression pathVar &&
            _pathVariables.TryGetValue(pathVar.Name, out var relVarNodes))
            return $"json({EscapeVar(relVarNodes)}.path_nodes)";

        if (fn == "relationships" && func.Arguments.Count == 1)
            return VisitRelationshipsFunction(func.Arguments[0]);

        if (fn == "properties" && func.Arguments.Count == 1)
            return func.Arguments[0] is IdentifierExpression nv
                ? $"json({EscapeVar(nv.Name)}.properties)"
                : $"json({VisitExpression(func.Arguments[0])})";

        if (fn == "keys" && func.Arguments.Count == 1 && func.Arguments[0] is IdentifierExpression nvk)
            return $"(SELECT json_group_array(key) FROM json_each({EscapeVar(nvk.Name)}.properties))";

        return null;
    }

    private string VisitRelationshipsFunction(Expression arg)
    {
        if (arg is IdentifierExpression pathVar && _pathVariables.TryGetValue(pathVar.Name, out var relVar))
        {
            var rVar = EscapeVar(relVar);
            if (_declaredRels.Contains(relVar))
            {
                return $"json_array(json_object('type', {rVar}.kind, 'from', {rVar}.from_id, 'to', {rVar}.to_id, 'properties', json({rVar}.properties)))";
            }

            return $"json({rVar}.path_nodes)";
        }

        return $"json({VisitExpression(arg)})";
    }

    private string VisitList(ListExpression list)
    {
        return list.Items.Count == 0
            ? "json_array()"
            : $"json_array({string.Join(", ", list.Items.Select(VisitExpression))})";
    }

    private string VisitMapLiteral(MapLiteralExpression map)
    {
        if (map.Properties.Count == 0)
        {
            return "json_object()";
        }

        List<string> parts = [];
        foreach (var (k, v) in map.Properties)
        {
            parts.Add($"'{k}', {VisitExpression(v)}");
        }

        return $"json_object({string.Join(", ", parts)})";
    }

    private string VisitListComprehension(ListComprehensionExpression comp)
    {
        if (comp.List is IdentifierExpression listId &&
            _withCollectExpressions.TryGetValue(listId.Name, out var collectInfo))
        {
            return VisitCollectedListComprehension(comp, collectInfo);
        }

        var listSql = VisitExpression(comp.List);
        var wasAdded = _unwindVariables.Add(comp.Variable);
        string projSql;
        string filterSql;
        try
        {
            projSql = comp.Projection != null ? VisitExpression(comp.Projection) : $"{comp.Variable}.value";
            filterSql = comp.Filter != null ? $" WHERE {VisitExpression(comp.Filter)}" : "";
        }
        finally
        {
            if (wasAdded)
            {
                _unwindVariables.Remove(comp.Variable);
            }
        }

        return $"(SELECT json_group_array({projSql}) FROM json_each({listSql}) AS {comp.Variable}{filterSql})";
    }

    private string VisitCollectedListComprehension(
        ListComprehensionExpression comp,
        CollectExpressionInfo collectInfo)
    {
        var distinctStr = collectInfo.IsDistinct ? "DISTINCT " : "";
        var innerExpr = collectInfo.InnerExpr;

        string? compFilterSql = null;
        if (comp.Filter != null)
        {
            var substitutedFilter = SubstituteComprehensionVariable(comp.Filter, comp.Variable, innerExpr);
            compFilterSql = $" FILTER (WHERE {VisitExpression(substitutedFilter)})";
        }

        var projExpr = comp.Projection != null
            ? SubstituteComprehensionVariable(comp.Projection, comp.Variable, innerExpr)
            : innerExpr;

        var compProjSql = VisitExpression(projExpr);
        return $"json_group_array({distinctStr}{compProjSql}){compFilterSql ?? ""}";
    }

    private static Expression SubstituteComprehensionVariable(Expression expr, string varName, Expression innerExpr)
    {
        switch (expr)
        {
            case PropertyAccessExpression prop when prop.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase):
                return SubstituteComprehensionProperty(prop, innerExpr);

            case IdentifierExpression id when id.Name.Equals(varName, StringComparison.OrdinalIgnoreCase):
                return innerExpr;

            case BinaryExpression bin:
                return new BinaryExpression(
                    SubstituteComprehensionVariable(bin.Left, varName, innerExpr),
                    bin.Operator,
                    SubstituteComprehensionVariable(bin.Right, varName, innerExpr));

            case UnaryExpression u:
                return new UnaryExpression(
                    u.Operator,
                    SubstituteComprehensionVariable(u.Operand, varName, innerExpr));

            case FunctionCallExpression fn:
                return new FunctionCallExpression(
                    fn.FunctionName,
                    fn.IsDistinct,
                    fn.Arguments.Select(a => SubstituteComprehensionVariable(a, varName, innerExpr)).ToList());

            default:
                return expr;
        }
    }

    private static Expression SubstituteComprehensionProperty(PropertyAccessExpression prop, Expression innerExpr)
    {
        if (innerExpr is MapLiteralExpression map && map.Properties.TryGetValue(prop.PropertyName, out var val))
        {
            return val;
        }

        if (innerExpr is IdentifierExpression innerId)
        {
            return new PropertyAccessExpression(innerId.Name, prop.PropertyName);
        }

        return prop;
    }

    private string VisitListPredicate(ListPredicateExpression pred)
    {
        var fastSql = TryOptimizeLabelsPredicate(pred);
        if (fastSql != null)
        {
            return fastSql;
        }

        var listSql = VisitExpression(pred.List);
        var wasAdded = _unwindVariables.Add(pred.Variable);
        string whereSql;
        try
        {
            whereSql = VisitExpression(pred.Predicate);
        }
        finally
        {
            if (wasAdded)
            {
                _unwindVariables.Remove(pred.Variable);
            }
        }

        return pred.Quantifier switch
        {
            "any" => $"(EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}))",
            "none" => $"(NOT EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}))",
            "all" => $"(NOT EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE NOT ({whereSql})))",
            "single" => $"((SELECT COUNT(1) FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}) = 1)",
            _ => $"(EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}))"
        };
    }

    private string? TryOptimizeLabelsPredicate(ListPredicateExpression pred)
    {
        if (pred.List is FunctionCallExpression func &&
            func.FunctionName.Equals("labels", StringComparison.OrdinalIgnoreCase) &&
            func.Arguments.Count > 0 &&
            func.Arguments[0] is IdentifierExpression nodeVar &&
            pred.Predicate is BinaryExpression bin && bin.Operator == BinaryOperator.Equal)
        {
            if (bin.Left is IdentifierExpression idL && idL.Name == pred.Variable)
            {
                return $"({nodeVar.Name}.kind = {VisitExpression(bin.Right)})";
            }

            if (bin.Right is IdentifierExpression idR && idR.Name == pred.Variable)
            {
                return $"({nodeVar.Name}.kind = {VisitExpression(bin.Left)})";
            }
        }

        return null;
    }

    private static readonly FrozenSet<string> AggregateFunctionNames = new[]
    {
        "count", "collect", "sum", "avg", "min", "max"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasAggregation(Expression expr) => expr switch
    {
        FunctionCallExpression f => AggregateFunctionNames.Contains(f.FunctionName),
        BinaryExpression b => HasAggregation(b.Left) || HasAggregation(b.Right),
        UnaryExpression u => HasAggregation(u.Operand),
        _ => false
    };

    private string VisitCase(CaseExpression caseExpr)
    {
        var sb = new StringBuilder("CASE");
        if (caseExpr.TestExpression != null)
        {
            sb.Append(' ').Append(VisitExpression(caseExpr.TestExpression));
        }

        foreach (var branch in caseExpr.WhenBranches)
        {
            sb.Append(" WHEN ").Append(VisitExpression(branch.When))
                .Append(" THEN ").Append(VisitExpression(branch.Then));
        }

        if (caseExpr.ElseExpression != null)
        {
            sb.Append(" ELSE ").Append(VisitExpression(caseExpr.ElseExpression));
        }

        sb.Append(" END");
        return sb.ToString();
    }

    private string VisitHasLabel(HasLabelExpression hasLabel)
    {
        if (hasLabel.Expression is IdentifierExpression id)
        {
            return $"({EscapeVar(id.Name)}.kind = '{hasLabel.Label}')";
        }

        return $"({VisitExpression(hasLabel.Expression)} = '{hasLabel.Label}')";
    }

    private static string QuoteIdentifier(string id) =>
        id.Contains('"') ? $"\"{id.Replace("\"", "\"\"")}\"" : $"\"{id}\"";

    private string VisitListSlice(ListSliceExpression slice)
    {
        var listSql = VisitExpression(slice.List);
        List<string> whereConditions = [];
        if (slice.From != null)
        {
            whereConditions.Add($"key >= {VisitExpression(slice.From)}");
        }

        if (slice.To != null)
        {
            whereConditions.Add($"key < {VisitExpression(slice.To)}");
        }

        var whereClause = whereConditions.Count > 0 ? $" WHERE {string.Join(" AND ", whereConditions)}" : "";
        return $"(SELECT json_group_array(value) FROM json_each({listSql}){whereClause})";
    }

    private (StringBuilder FromJoins, List<string> Conditions) BuildSubqueryPath(PathPattern path, string prefix)
    {
        List<string> conditions = [];
        var fromJoins = new StringBuilder();

        var headVar = path.Head.Variable;
        var headIsOuter = headVar != null && _declaredNodes.Contains(headVar);
        var actualHeadVar = headIsOuter ? headVar! : $"{prefix}_h{_varIndex++}";

        if (!headIsOuter)
        {
            fromJoins.Append($"nodes {actualHeadVar}");
            AddNodeFiltersToConditions(path.Head, actualHeadVar, conditions);
        }

        var prevVar = actualHeadVar;
        for (var i = 0; i < path.Chain.Count; i++)
        {
            var element = path.Chain[i];
            var rel = element.Relationship;
            var targetNode = element.Target;
            var relVar = $"{prefix}_r{_varIndex++}";
            var targetVar = targetNode.Variable ?? $"{prefix}_t{_varIndex++}";

            fromJoins.Append(fromJoins.Length == 0 ? $"edges {relVar} JOIN nodes {targetVar}" : $" JOIN edges {relVar} JOIN nodes {targetVar}");

            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    conditions.Add($"{relVar}.from_id = {prevVar}.id");
                    conditions.Add($"{targetVar}.id = {relVar}.to_id");
                    break;
                case Direction.Incoming:
                    conditions.Add($"{relVar}.to_id = {prevVar}.id");
                    conditions.Add($"{targetVar}.id = {relVar}.from_id");
                    break;
                case Direction.Undirected:
                    conditions.Add($"({relVar}.from_id = {prevVar}.id OR {relVar}.to_id = {prevVar}.id)");
                    conditions.Add($"{targetVar}.id = CASE WHEN {relVar}.from_id = {prevVar}.id THEN {relVar}.to_id ELSE {relVar}.from_id END");
                    break;
            }

            AddRelKindConditions(rel, relVar, conditions);
            AddNodeFiltersToConditions(targetNode, targetVar, conditions);
            prevVar = targetVar;
        }

        return (fromJoins, conditions);
    }

    private string VisitPatternExpression(PatternExpression pat)
    {
        var (fromJoins, conditions) = BuildSubqueryPath(pat.Path, "_pe");
        var whereClause = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
        return $"(EXISTS (SELECT 1 FROM {fromJoins}{whereClause}))";
    }

    private string VisitPatternComprehension(PatternComprehensionExpression patComp)
    {
        var (fromJoins, conditions) = BuildSubqueryPath(patComp.Path, "_pc");
        if (patComp.Filter != null)
        {
            conditions.Add(VisitExpression(patComp.Filter));
        }

        var projSql = VisitExpression(patComp.Projection);
        var whereClause = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
        return $"(SELECT json_group_array({projSql}) FROM {fromJoins}{whereClause})";
    }

    private string VisitReduce(ReduceExpression red)
    {
        var listSql = VisitExpression(red.List);
        var initSql = VisitExpression(red.Initial);
        return $"({initSql} + COALESCE((SELECT sum(value) FROM json_each({listSql})), 0))";
    }



    private static HashSet<string> GetReferencedIdentifiers(Expression expr)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectIdentifiers(expr, set);
        return set;
    }

    private static void CollectIdentifiers(Expression expr, HashSet<string> set)
    {
        switch (expr)
        {
            case IdentifierExpression id:
                set.Add(id.Name);
                break;
            case PropertyAccessExpression prop:
                set.Add(prop.Variable);
                break;
            case FunctionCallExpression fn:
                foreach (var arg in fn.Arguments) CollectIdentifiers(arg, set);
                break;
            case BinaryExpression b:
                CollectIdentifiers(b.Left, set);
                CollectIdentifiers(b.Right, set);
                break;
            case UnaryExpression u:
                CollectIdentifiers(u.Operand, set);
                break;
            case ListExpression l:
                foreach (var it in l.Items) CollectIdentifiers(it, set);
                break;
            default:
                CollectCompoundIdentifiers(expr, set);
                break;
        }
    }

    private static void CollectCompoundIdentifiers(Expression expr, HashSet<string> set)
    {
        switch (expr)
        {
            case CaseExpression c:
                if (c.TestExpression != null) CollectIdentifiers(c.TestExpression, set);
                foreach (var w in c.WhenBranches)
                {
                    CollectIdentifiers(w.When, set);
                    CollectIdentifiers(w.Then, set);
                }

                if (c.ElseExpression != null) CollectIdentifiers(c.ElseExpression, set);
                break;
            case MapLiteralExpression ml:
                foreach (var (_, v) in ml.Properties) CollectIdentifiers(v, set);
                break;
            case ListComprehensionExpression lc:
                CollectIdentifiers(lc.List, set);
                if (lc.Filter != null) CollectIdentifiers(lc.Filter, set);
                if (lc.Projection != null) CollectIdentifiers(lc.Projection, set);
                break;
            case MapProjectionExpression mp:
                CollectIdentifiers(mp.BaseExpression, set);
                foreach (var el in mp.Elements)
                {
                    if (el.ValueExpression != null) CollectIdentifiers(el.ValueExpression, set);
                }

                break;
        }
    }

    private string VisitMapProjection(MapProjectionExpression mapProj)
    {
        var baseVar = mapProj.BaseExpression is IdentifierExpression id
            ? id.Name
            : VisitExpression(mapProj.BaseExpression);
        List<string> parts = [];
        
        foreach (var elem in mapProj.Elements)
        {
            if (elem.IsAllProperties) continue;

            parts.Add(elem.ValueExpression != null
                ? $"'{elem.PropertyName}', {VisitExpression(elem.ValueExpression)}"
                : $"'{elem.PropertyName}', json_extract({baseVar}.properties, '$.{elem.PropertyName}')");
        }

        return $"json_object({string.Join(", ", parts)})";
    }
}
