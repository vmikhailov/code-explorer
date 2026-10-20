using System.Collections.Frozen;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
    private static bool IsDefinitelyString(Expression expr)
    {
        return expr switch
        {
            StringLiteralExpression => true,
            FunctionCallExpression func when func.FunctionName.Equals("toString", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("substring", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("trim", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("toLower", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("toUpper", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("replace", StringComparison.OrdinalIgnoreCase) => true,
            BinaryExpression bin when bin.Operator == BinaryOperator.Add && (IsDefinitelyString(bin.Left) || IsDefinitelyString(bin.Right)) => true,
            _ => false
        };
    }

    private static bool IsDefinitelyNumeric(Expression expr)
    {
        return expr switch
        {
            NumberLiteralExpression => true,
            UnaryExpression unary when unary.Operator is UnaryOperator.Minus or UnaryOperator.Plus => true,
            BinaryExpression bin when bin.Operator is BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Modulo or BinaryOperator.Power => true,
            BinaryExpression bin when bin.Operator == BinaryOperator.Add && (IsDefinitelyNumeric(bin.Left) || IsDefinitelyNumeric(bin.Right)) => true,
            FunctionCallExpression func when func.FunctionName.Equals("size", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("length", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("count", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("toInteger", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("toFloat", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("abs", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("round", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("ceil", StringComparison.OrdinalIgnoreCase) ||
                                                   func.FunctionName.Equals("floor", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
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

        var innerSql = VisitExpression(func.Arguments[0]);
        if (innerSql.EndsWith(".from_id", StringComparison.OrdinalIgnoreCase) ||
            innerSql.EndsWith(".to_id", StringComparison.OrdinalIgnoreCase) ||
            innerSql.EndsWith(".id", StringComparison.OrdinalIgnoreCase) ||
            innerSql.EndsWith(".rowid", StringComparison.OrdinalIgnoreCase))
        {
            return innerSql;
        }

        return $"{innerSql}.id";
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

        if (fn == "tostring" && func.Arguments.Count == 1)
            return $"CAST({VisitExpression(func.Arguments[0])} AS TEXT)";

        if (fn == "length" && func.Arguments.Count == 1)
            return $"length({VisitExpression(func.Arguments[0])})";

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
        {
            var strSql = VisitExpression(func.Arguments[0]);
            var startSql = VisitExpression(func.Arguments[1]);
            return func.Arguments.Count == 3
                ? $"substr({strSql}, ({startSql}) + 1, {VisitExpression(func.Arguments[2])})"
                : $"substr({strSql}, ({startSql}) + 1)";
        }

        return null;
    }

    private string? TryVisitAggregateFunction(string fn, FunctionCallExpression func, string distinctStr)
    {
        var decomposed = TryCompileDecomposedAggregation(func, distinctStr);
        if (decomposed != null) return decomposed;

        if (fn == "count" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is WildcardExpression) return $"COUNT({distinctStr}*)";
            if (func.Arguments[0] is IdentifierExpression id)
            {
                if (_declaredNodes.Contains(id.Name))
                    return $"COUNT({distinctStr}{EscapeVar(id.Name)}.id)";
                if (_declaredRels.Contains(id.Name))
                    return $"COUNT({distinctStr}{EscapeVar(id.Name)}.rowid)";
            }
            return $"COUNT({distinctStr}{VisitExpression(func.Arguments[0])})";
        }

        if (fn == "collect" && func.Arguments.Count == 1)
        {
            var inner = VisitExpression(func.Arguments[0]);
            return $"json_group_array({distinctStr}{inner}) FILTER (WHERE {inner} IS NOT NULL)";
        }

        return null;
    }

    private string? TryVisitGraphIntrospection(string fn, FunctionCallExpression func)
    {
        if (fn == "labels" && func.Arguments.Count == 1 && func.Arguments[0] is IdentifierExpression nodeVar)
        {
            var v = EscapeVar(nodeVar.Name);
            return $"CASE " +
                   $"WHEN {v}.kind IN ('Service', 'App', 'FrontendApp', 'Library', 'SharedLibrary', 'Worker', 'CliTool', 'DatabaseMigration', 'FunctionApp', 'Test', 'TestSuite') " +
                   $"THEN json_array({v}.kind, 'Project') " +
                   $"WHEN {v}.kind = 'Project' AND json_extract({v}.properties, '$.kind') IS NOT NULL " +
                   $"THEN json_array({v}.kind, json_extract({v}.properties, '$.kind')) " +
                   $"WHEN {v}.kind = 'Project' AND json_extract({v}.properties, '$.role') IS NOT NULL " +
                   $"THEN json_array({v}.kind, json_extract({v}.properties, '$.role')) " +
                   $"ELSE json_array({v}.kind) END";
        }

        if (fn == "type" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression relVar)
            {
                if (_declaredRels.Contains(relVar.Name))
                    return $"{EscapeVar(relVar.Name)}.kind";
                if (_withAliases.TryGetValue(relVar.Name, out var aliasSql))
                    return $"COALESCE(json_extract({aliasSql}, '$.type'), json_extract({aliasSql}, '$.kind'))";
            }
            return $"json_extract({VisitExpression(func.Arguments[0])}, '$.type')";
        }

        if ((fn == "startnode" || fn == "start_node") && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression startRelVar)
            {
                if (_declaredRels.Contains(startRelVar.Name))
                    return $"{EscapeVar(startRelVar.Name)}.from_id";
                if (_withAliases.TryGetValue(startRelVar.Name, out var aliasSql))
                    return $"COALESCE(json_extract({aliasSql}, '$.from'), json_extract({aliasSql}, '$.from_id'))";
            }
            return $"COALESCE(json_extract({VisitExpression(func.Arguments[0])}, '$.from'), json_extract({VisitExpression(func.Arguments[0])}, '$.from_id'))";
        }

        if ((fn == "endnode" || fn == "end_node") && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression endRelVar)
            {
                if (_declaredRels.Contains(endRelVar.Name))
                    return $"{EscapeVar(endRelVar.Name)}.to_id";
                if (_withAliases.TryGetValue(endRelVar.Name, out var aliasSql))
                    return $"COALESCE(json_extract({aliasSql}, '$.to'), json_extract({aliasSql}, '$.to_id'))";
            }
            return $"COALESCE(json_extract({VisitExpression(func.Arguments[0])}, '$.to'), json_extract({VisitExpression(func.Arguments[0])}, '$.to_id'))";
        }

        if (fn == "nodes" && func.Arguments.Count == 1 &&
            func.Arguments[0] is IdentifierExpression pathVar &&
            _pathVariables.TryGetValue(pathVar.Name, out var relVarNodes))
            return $"json({EscapeVar(relVarNodes)}.path_nodes)";

        if (fn == "relationships" && func.Arguments.Count == 1)
            return VisitRelationshipsFunction(func.Arguments[0]);

        if (fn == "properties" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression nv)
            {
                if (_declaredNodes.Contains(nv.Name))
                    return $"json({EscapeVar(nv.Name)}.properties)";
                if (_declaredRels.Contains(nv.Name))
                    return $"json({EscapeVar(nv.Name)}.properties)";
                if (_withAliases.TryGetValue(nv.Name, out var aliasSql))
                    return $"COALESCE(json_extract({aliasSql}, '$.properties'), json({aliasSql}))";
            }
            return $"json({VisitExpression(func.Arguments[0])})";
        }

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
            var exprSql = VisitExpression(v);
            if (v is IdentifierExpression id && _withCollectExpressions.ContainsKey(id.Name))
            {
                exprSql = $"json(coalesce({exprSql}, '[]'))";
            }
            parts.Add($"'{k}', {exprSql}");
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
        var safeListSql = $"CASE WHEN json_valid({listSql}) THEN {listSql} ELSE '[]' END";
        var wasAdded = _unwindVariables.Add(comp.Variable);
        string projSql;
        string? filterSql;
        try
        {
            projSql = comp.Projection != null ? VisitExpression(comp.Projection) : $"{comp.Variable}.value";
            filterSql = comp.Filter != null ? VisitExpression(comp.Filter) : null;
        }
        finally
        {
            if (wasAdded)
            {
                _unwindVariables.Remove(comp.Variable);
            }
        }

        return SqlTemplates.RenderListComprehension(safeListSql, comp.Variable, filterSql, projSql);
    }

    private string VisitCollectedListComprehension(
        ListComprehensionExpression comp,
        CollectExpressionInfo collectInfo)
    {
        var innerExpr = collectInfo.InnerExpr;

        string? compFilterSql = null;
        if (comp.Filter != null)
        {
            var substitutedFilter = SubstituteComprehensionVariable(comp.Filter, comp.Variable, innerExpr);
            compFilterSql = VisitExpression(substitutedFilter);
        }

        var projExpr = comp.Projection != null
            ? SubstituteComprehensionVariable(comp.Projection, comp.Variable, innerExpr)
            : innerExpr;

        var compProjSql = VisitExpression(projExpr);
        return SqlTemplates.RenderCollectedArray(collectInfo.IsDistinct, compProjSql, compFilterSql);
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
                    [.. fn.Arguments.Select(a => SubstituteComprehensionVariable(a, varName, innerExpr))]);

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
        var safeListSql = $"CASE WHEN json_valid({listSql}) THEN {listSql} ELSE '[]' END";
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

        return SqlTemplates.RenderQuantifier(pred.Quantifier, safeListSql, pred.Variable, whereSql);
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

    private string VisitReduce(ReduceExpression red)
    {
        var listSql = VisitExpression(red.List);
        var initSql = VisitExpression(red.Initial);
        return $"({initSql} + COALESCE((SELECT sum(value) FROM json_each({listSql})), 0))";
    }
}
