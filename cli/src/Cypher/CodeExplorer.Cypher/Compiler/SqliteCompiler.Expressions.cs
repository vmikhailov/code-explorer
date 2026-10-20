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

        return TryVisitNodeIdentifier(id.Name, escaped) ?? TryVisitRelIdentifier(id.Name, escaped) ?? escaped;
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

    private string? TryVisitRelIdentifier(string name, string escaped)
    {
        if (_declaredRels.Contains(name))
        {
            return $"json_object('type', {escaped}.kind, 'from', {escaped}.from_id, 'to', {escaped}.to_id, 'properties', json({escaped}.properties))";
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

        if (_withAliases.TryGetValue(prop.Variable, out var withSql))
        {
            return $"COALESCE(json_extract({withSql}, '$.properties.' || '{prop.PropertyName}'), json_extract({withSql}, '$.{prop.PropertyName}'))";
        }

        return VisitDefaultPropertyAccess(v, prop.PropertyName, prop.Variable);
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

        if (propName.Equals("properties", StringComparison.OrdinalIgnoreCase))
        {
            return propSrc;
        }

        if (propName.Equals("language", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({propSrc}, '$.language'), json_extract({propSrc}, '$.project_type'))";
        }

        if (propName.Equals("project_type", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({propSrc}, '$.project_type'), json_extract({propSrc}, '$.language'))";
        }

        if (propName.Equals("is_library", StringComparison.OrdinalIgnoreCase))
        {
            return $"(COALESCE(json_extract({propSrc}, '$.is_library'), 0) IN (1, 'true', 'True'))";
        }

        return $"json_extract({propSrc}, '$.{propName}')";
    }

    private string VisitDefaultPropertyAccess(string v, string propName, string originalVar)
    {
        if (_declaredRels.Contains(originalVar))
        {
            if (propName.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return $"{v}.rowid";
            }

            if (propName.Equals("kind", StringComparison.OrdinalIgnoreCase) || propName.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                return $"{v}.kind";
            }

            if (propName.Equals("from", StringComparison.OrdinalIgnoreCase) || propName.Equals("from_id", StringComparison.OrdinalIgnoreCase))
            {
                return $"{v}.from_id";
            }

            if (propName.Equals("to", StringComparison.OrdinalIgnoreCase) || propName.Equals("to_id", StringComparison.OrdinalIgnoreCase))
            {
                return $"{v}.to_id";
            }

            if (propName.Equals("properties", StringComparison.OrdinalIgnoreCase))
            {
                return $"{v}.properties";
            }

            return $"json_extract({v}.properties, '$.{propName}')";
        }

        if (propName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return $"{v}.id";
        }

        if (propName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.properties, '$.kind'), {v}.kind)";
        }

        if (propName.Equals("properties", StringComparison.OrdinalIgnoreCase))
        {
            return $"{v}.properties";
        }

        if (propName.Equals("language", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.properties, '$.language'), json_extract({v}.properties, '$.project_type'))";
        }

        if (propName.Equals("project_type", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.properties, '$.project_type'), json_extract({v}.properties, '$.language'))";
        }

        if (propName.Equals("is_library", StringComparison.OrdinalIgnoreCase))
        {
            return $"(COALESCE(json_extract({v}.properties, '$.is_library'), 0) IN (1, 'true', 'True'))";
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
        if (IsDefinitelyString(leftExpr) || IsDefinitelyString(rightExpr))
        {
            return $"({leftSql} || {rightSql})";
        }

        if (IsDefinitelyNumeric(leftExpr) || IsDefinitelyNumeric(rightExpr))
        {
            return $"({leftSql} + {rightSql})";
        }

        // In Cypher, + on strings or variables defaults to string concatenation
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
            return $"{EscapeVar(id.Name)}.kind = '{hasLabel.Label}'";
        }

        return $"({VisitExpression(hasLabel.Expression)} = '{hasLabel.Label}')";
    }

    private static string QuoteIdentifier(string id) =>
        id.Contains('"') ? $"\"{id.Replace("\"", "\"\"")}\"" : $"\"{id}\"";

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
            case PatternComprehensionExpression pc:
                if (pc.Path.Head.Variable != null) set.Add(pc.Path.Head.Variable);
                if (pc.Filter != null) CollectIdentifiers(pc.Filter, set);
                CollectIdentifiers(pc.Projection, set);
                break;
            case PatternExpression pe:
                if (pe.Path.Head.Variable != null) set.Add(pe.Path.Head.Variable);
                break;
            case HasLabelExpression hl:
                CollectIdentifiers(hl.Expression, set);
                break;
            case ListPredicateExpression lp:
                CollectIdentifiers(lp.List, set);
                CollectIdentifiers(lp.Predicate, set);
                break;
            case ListSliceExpression ls:
                CollectIdentifiers(ls.List, set);
                if (ls.From != null) CollectIdentifiers(ls.From, set);
                if (ls.To != null) CollectIdentifiers(ls.To, set);
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

            if (elem.ValueExpression != null)
            {
                var exprSql = VisitExpression(elem.ValueExpression);
                if (elem.ValueExpression is IdentifierExpression valId && _withCollectExpressions.ContainsKey(valId.Name))
                {
                    exprSql = $"json(coalesce({exprSql}, '[]'))";
                }
                parts.Add($"'{elem.PropertyName}', {exprSql}");
            }
            else
            {
                parts.Add($"'{elem.PropertyName}', json_extract({baseVar}.properties, '$.{elem.PropertyName}')");
            }
        }

        return $"json_object({string.Join(", ", parts)})";
    }
}
