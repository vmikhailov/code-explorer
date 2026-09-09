using System.Globalization;
using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public class SqliteCompiler : ICypherVisitor<string>
{
    private readonly CypherQuery _query;
    private readonly Dictionary<string, object?>? _initialParameters;
    private readonly Dictionary<string, object?> _parameters = new();
    private readonly HashSet<string> _declaredNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _declaredRels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _pathVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unwindVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _withAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _ctes = new();

    private int _paramIndex;
    private int _varIndex;
    private int _cteIndex;

    public SqliteCompiler(CypherQuery query, Dictionary<string, object?>? parameters = null)
    {
        _query = query;
        _initialParameters = parameters;
    }

    public static SqliteCompiledQuery Compile(CypherQuery query, Dictionary<string, object?>? parameters = null)
    {
        var compiler = new SqliteCompiler(query, parameters);
        var sql = compiler.VisitQuery(query);
        return new SqliteCompiledQuery(sql, compiler._parameters);
    }

    public string VisitQuery(CypherQuery query)
    {
        // Copy initial user parameters if any
        if (_initialParameters != null)
        {
            foreach (var (k, v) in _initialParameters)
            {
                _parameters[k] = v;
            }
        }

        var fromAndJoins = new StringBuilder();
        var whereConditions = new List<string>();

        // Process all MATCH / OPTIONAL MATCH clauses
        foreach (var match in query.Matches)
        {
            ProcessMatchClause(match, fromAndJoins, whereConditions);
        }

        // Process UNWIND clauses if any
        if (query.UnwindClauses != null)
        {
            foreach (var unwind in query.UnwindClauses)
            {
                _unwindVariables.Add(unwind.Alias);
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"JOIN json_each({VisitExpression(unwind.Expression)}) {unwind.Alias}");
            }
        }

        // Process WITH clauses if any
        var groupByColumns = new List<string>();
        var havingConditions = new List<string>();

        if (query.WithClauses != null)
        {
            foreach (var with in query.WithClauses)
            {
                foreach (var item in with.Items)
                {
                    if (item.Alias != null)
                    {
                        _withAliases[item.Alias] = VisitExpression(item.Expression);
                    }
                    if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
                    {
                        groupByColumns.Add($"{id.Name}.id");
                    }
                }

                if (with.Where != null)
                {
                    var whereSql = VisitExpression(with.Where.Predicate);
                    if (with.Items.Any(i => HasAggregation(i.Expression)))
                    {
                        havingConditions.Add(whereSql);
                    }
                    else
                    {
                        whereConditions.Add(whereSql);
                    }
                }
            }
        }

        // Process top-level WHERE clause if present
        if (query.Where != null)
        {
            whereConditions.Add(VisitExpression(query.Where.Predicate));
        }

        // Build SELECT columns
        var selectColumns = new List<string>();
        foreach (var item in query.Return.Items)
        {
            var exprSql = VisitExpression(item.Expression);
            if (!string.IsNullOrEmpty(item.Alias))
            {
                selectColumns.Add($"{exprSql} AS {QuoteIdentifier(item.Alias)}");
            }
            else
            {
                selectColumns.Add(exprSql);
            }
        }

        var distinctStr = query.Return.IsDistinct ? "DISTINCT " : "";
        var sb = new StringBuilder();

        // Emit CTEs if any
        if (_ctes.Count > 0)
        {
            sb.Append("WITH RECURSIVE ");
            sb.Append(string.Join(",\n", _ctes));
            sb.AppendLine();
        }

        sb.Append("SELECT ").Append(distinctStr).AppendLine(string.Join(", ", selectColumns));
        sb.Append(fromAndJoins);

        if (whereConditions.Count > 0)
        {
            sb.AppendLine();
            sb.Append("WHERE ").Append(string.Join(" AND ", whereConditions));
        }

        if (groupByColumns.Count > 0)
        {
            sb.AppendLine();
            sb.Append("GROUP BY ").Append(string.Join(", ", groupByColumns.Distinct()));
        }

        if (havingConditions.Count > 0)
        {
            sb.AppendLine();
            sb.Append("HAVING ").Append(string.Join(" AND ", havingConditions));
        }

        // ORDER BY
        if (query.OrderBy != null && query.OrderBy.Items.Count > 0)
        {
            sb.AppendLine();
            sb.Append("ORDER BY ");
            var orderItems = query.OrderBy.Items.Select(item =>
                $"{VisitExpression(item.Expression)} {(item.IsDescending ? "DESC" : "ASC")}");
            sb.Append(string.Join(", ", orderItems));
        }

        // LIMIT and SKIP (OFFSET)
        if (query.Limit != null || query.Skip != null)
        {
            sb.AppendLine();
            var limitVal = query.Limit?.Count ?? -1;
            sb.Append($"LIMIT {limitVal}");
            if (query.Skip != null)
            {
                sb.Append($" OFFSET {query.Skip.Count}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private void ProcessMatchClause(
        MatchClause match,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions)
    {
        var isOptional = match.IsOptional;
        var joinKeyword = isOptional ? "LEFT JOIN" : "JOIN";
        var optionalWhereExtra = new List<string>();

        if (isOptional && match.Where != null)
        {
            optionalWhereExtra.Add(VisitExpression(match.Where.Predicate));
        }
        else if (!isOptional && match.Where != null)
        {
            mainWhereConditions.Add(VisitExpression(match.Where.Predicate));
        }

        foreach (var path in match.Paths)
        {
            ProcessPathPattern(path, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
        }
    }

    private void ProcessPathPattern(
        PathPattern path,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var headVar = path.Head.Variable ?? $"_n{_varIndex++}";
        var pathVar = path.PathVariable;

        // If path has no chain, just (n)
        if (path.Chain.Count == 0)
        {
            if (!_declaredNodes.Contains(headVar))
            {
                if (fromAndJoins.Length == 0 && !isOptional)
                {
                    fromAndJoins.Append($"FROM nodes {headVar}");
                }
                else
                {
                    fromAndJoins.AppendLine();
                    fromAndJoins.Append($"{joinKeyword} nodes {headVar} ON 1=1");
                }
                _declaredNodes.Add(headVar);

                ApplyNodeConditions(path.Head, headVar, isOptional, fromAndJoins, mainWhereConditions);
            }
            return;
        }

        // There is a chain of relationships
        // Check which nodes are already bound
        var prevNode = path.Head;
        var prevVar = headVar;

        // If head node is not declared and no node in the chain is declared yet:
        bool anyDeclared = _declaredNodes.Contains(headVar) ||
                           path.Chain.Any(c => c.Target.Variable != null && _declaredNodes.Contains(c.Target.Variable));

        if (!anyDeclared)
        {
            if (fromAndJoins.Length == 0 && !isOptional)
            {
                fromAndJoins.Append($"FROM nodes {headVar}");
            }
            else
            {
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"{joinKeyword} nodes {headVar} ON 1=1");
            }
            _declaredNodes.Add(headVar);
            ApplyNodeConditions(path.Head, headVar, isOptional, fromAndJoins, mainWhereConditions);
        }

        // Iterate through each relationship link
        for (int i = 0; i < path.Chain.Count; i++)
        {
            var element = path.Chain[i];
            var rel = element.Relationship;
            var targetNode = element.Target;
            var targetVar = targetNode.Variable ?? $"_n{_varIndex++}";
            var relVar = rel.Variable ?? $"_r{_varIndex++}";

            if (pathVar != null)
            {
                _pathVariables[pathVar] = relVar;
            }

            bool prevDeclared = _declaredNodes.Contains(prevVar);
            bool targetDeclared = _declaredNodes.Contains(targetVar);

            if (rel.Range.HasValue)
            {
                // Variable-length path CTE
                ProcessVariableLengthRel(
                    rel, relVar, prevNode, prevVar, targetNode, targetVar,
                    prevDeclared, targetDeclared, isOptional, joinKeyword,
                    fromAndJoins, mainWhereConditions, optionalWhereExtra);
            }
            else
            {
                // Single-hop relationship
                ProcessSingleHopRel(
                    rel, relVar, prevNode, prevVar, targetNode, targetVar,
                    prevDeclared, targetDeclared, isOptional, joinKeyword,
                    fromAndJoins, mainWhereConditions, optionalWhereExtra);
            }

            prevNode = targetNode;
            prevVar = targetVar;
        }
    }

    private void ProcessSingleHopRel(
        RelationshipPattern rel,
        string relVar,
        NodePattern prevNode,
        string prevVar,
        NodePattern targetNode,
        string targetVar,
        bool prevDeclared,
        bool targetDeclared,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var relOnConditions = new List<string>();
        var nodeOnConditions = new List<string>();

        // Direction mapping
        if (prevDeclared && !targetDeclared)
        {
            // Traverse from prev to target
            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    relOnConditions.Add($"{relVar}.from_id = {prevVar}.id");
                    nodeOnConditions.Add($"{targetVar}.id = {relVar}.to_id");
                    break;
                case Direction.Incoming:
                    relOnConditions.Add($"{relVar}.to_id = {prevVar}.id");
                    nodeOnConditions.Add($"{targetVar}.id = {relVar}.from_id");
                    break;
                case Direction.Undirected:
                    relOnConditions.Add($"({relVar}.from_id = {prevVar}.id OR {relVar}.to_id = {prevVar}.id)");
                    nodeOnConditions.Add($"{targetVar}.id = CASE WHEN {relVar}.from_id = {prevVar}.id THEN {relVar}.to_id ELSE {relVar}.from_id END");
                    break;
            }

            AddRelKindConditions(rel, relVar, relOnConditions);
            AddNodeFiltersToConditions(targetNode, targetVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} edges {relVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {targetVar} ON {string.Join(" AND ", nodeOnConditions)}");

            _declaredRels.Add(relVar);
            _declaredNodes.Add(targetVar);
        }
        else if (!prevDeclared && targetDeclared)
        {
            // Target is already declared, bind prev node
            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    relOnConditions.Add($"{relVar}.to_id = {targetVar}.id");
                    nodeOnConditions.Add($"{prevVar}.id = {relVar}.from_id");
                    break;
                case Direction.Incoming:
                    relOnConditions.Add($"{relVar}.from_id = {targetVar}.id");
                    nodeOnConditions.Add($"{prevVar}.id = {relVar}.to_id");
                    break;
                case Direction.Undirected:
                    relOnConditions.Add($"({relVar}.from_id = {targetVar}.id OR {relVar}.to_id = {targetVar}.id)");
                    nodeOnConditions.Add($"{prevVar}.id = CASE WHEN {relVar}.from_id = {targetVar}.id THEN {relVar}.to_id ELSE {relVar}.from_id END");
                    break;
            }

            AddRelKindConditions(rel, relVar, relOnConditions);
            AddNodeFiltersToConditions(prevNode, prevVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} edges {relVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {prevVar} ON {string.Join(" AND ", nodeOnConditions)}");

            _declaredRels.Add(relVar);
            _declaredNodes.Add(prevVar);
        }
        else if (prevDeclared && targetDeclared)
        {
            // Both are declared, connect them with edge
            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    relOnConditions.Add($"{relVar}.from_id = {prevVar}.id AND {relVar}.to_id = {targetVar}.id");
                    break;
                case Direction.Incoming:
                    relOnConditions.Add($"{relVar}.to_id = {prevVar}.id AND {relVar}.from_id = {targetVar}.id");
                    break;
                case Direction.Undirected:
                    relOnConditions.Add($"(({relVar}.from_id = {prevVar}.id AND {relVar}.to_id = {targetVar}.id) OR ({relVar}.to_id = {prevVar}.id AND {relVar}.from_id = {targetVar}.id))");
                    break;
            }

            AddRelKindConditions(rel, relVar, relOnConditions);

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} edges {relVar} ON {string.Join(" AND ", relOnConditions)}");
            _declaredRels.Add(relVar);
        }
    }

    private void ProcessVariableLengthRel(
        RelationshipPattern rel,
        string relVar,
        NodePattern prevNode,
        string prevVar,
        NodePattern targetNode,
        string targetVar,
        bool prevDeclared,
        bool targetDeclared,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var cteName = $"cte_rel_{_cteIndex++}";
        var minDepth = rel.Range?.Min ?? 1;
        var maxDepth = rel.Range?.Max;

        // Build kind predicate for CTE edges
        string edgeKindPred = "1=1";
        if (rel.Types.Count == 1)
        {
            edgeKindPred = $"e.kind = '{rel.Types[0]}'";
        }
        else if (rel.Types.Count > 1)
        {
            var kinds = string.Join(", ", rel.Types.Select(t => $"'{t}'"));
            edgeKindPred = $"e.kind IN ({kinds})";
        }

        // Anchor member
        var anchorSb = new StringBuilder();
        if (minDepth == 0)
        {
            anchorSb.AppendLine($"    SELECT id AS start_id, id AS end_id, 0 AS depth, '/' || id || '/' AS path_visited, json_array(id) AS path_nodes FROM nodes");
            anchorSb.AppendLine("    UNION ALL");
            anchorSb.Append($"    SELECT e.from_id AS start_id, e.to_id AS end_id, 1 AS depth, '/' || e.from_id || '/' || e.to_id || '/' AS path_visited, json_array(e.from_id, e.to_id) AS path_nodes FROM edges e WHERE {edgeKindPred}");
        }
        else
        {
            anchorSb.Append($"    SELECT e.from_id AS start_id, e.to_id AS end_id, 1 AS depth, '/' || e.from_id || '/' || e.to_id || '/' AS path_visited, json_array(e.from_id, e.to_id) AS path_nodes FROM edges e WHERE {edgeKindPred}");
        }

        // Recursive member
        var maxDepthCond = maxDepth.HasValue ? $" AND c.depth < {maxDepth.Value}" : "";
        var recursiveSb = new StringBuilder();
        recursiveSb.Append($@"    SELECT c.start_id, e.to_id, c.depth + 1, c.path_visited || e.to_id || '/', json_insert(c.path_nodes, '$[#]', e.to_id)
    FROM {cteName} c
    JOIN edges e ON e.from_id = c.end_id
    WHERE {edgeKindPred}{maxDepthCond} AND instr(c.path_visited, '/' || e.to_id || '/') = 0");

        var cteSql = $"{cteName}(start_id, end_id, depth, path_visited, path_nodes) AS (\n{anchorSb}\n    UNION ALL\n{recursiveSb}\n)";
        _ctes.Add(cteSql);

        var relOnConditions = new List<string>();
        var nodeOnConditions = new List<string>();

        relOnConditions.Add($"{relVar}.depth >= {minDepth}");
        if (maxDepth.HasValue)
        {
            relOnConditions.Add($"{relVar}.depth <= {maxDepth.Value}");
        }

        if (prevDeclared && !targetDeclared)
        {
            if (rel.Direction == Direction.Incoming)
            {
                relOnConditions.Add($"{relVar}.end_id = {prevVar}.id");
                nodeOnConditions.Add($"{targetVar}.id = {relVar}.start_id");
            }
            else
            {
                relOnConditions.Add($"{relVar}.start_id = {prevVar}.id");
                nodeOnConditions.Add($"{targetVar}.id = {relVar}.end_id");
            }

            AddNodeFiltersToConditions(targetNode, targetVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} {cteName} {relVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {targetVar} ON {string.Join(" AND ", nodeOnConditions)}");

            _declaredRels.Add(relVar);
            _declaredNodes.Add(targetVar);
        }
        else if (!prevDeclared && targetDeclared)
        {
            if (rel.Direction == Direction.Incoming)
            {
                relOnConditions.Add($"{relVar}.start_id = {targetVar}.id");
                nodeOnConditions.Add($"{prevVar}.id = {relVar}.end_id");
            }
            else
            {
                relOnConditions.Add($"{relVar}.end_id = {targetVar}.id");
                nodeOnConditions.Add($"{prevVar}.id = {relVar}.start_id");
            }

            AddNodeFiltersToConditions(prevNode, prevVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} {cteName} {relVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {prevVar} ON {string.Join(" AND ", nodeOnConditions)}");

            _declaredRels.Add(relVar);
            _declaredNodes.Add(prevVar);
        }
        else if (prevDeclared && targetDeclared)
        {
            if (rel.Direction == Direction.Incoming)
            {
                relOnConditions.Add($"{relVar}.end_id = {prevVar}.id AND {relVar}.start_id = {targetVar}.id");
            }
            else
            {
                relOnConditions.Add($"{relVar}.start_id = {prevVar}.id AND {relVar}.end_id = {targetVar}.id");
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} {cteName} {relVar} ON {string.Join(" AND ", relOnConditions)}");
            _declaredRels.Add(relVar);
        }
    }

    private void AddRelKindConditions(RelationshipPattern rel, string relVar, List<string> conditions)
    {
        if (rel.Types.Count == 1)
        {
            conditions.Add($"{relVar}.kind = '{rel.Types[0]}'");
        }
        else if (rel.Types.Count > 1)
        {
            var kinds = string.Join(", ", rel.Types.Select(t => $"'{t}'"));
            conditions.Add($"{relVar}.kind IN ({kinds})");
        }

        if (rel.Properties != null)
        {
            foreach (var (k, v) in rel.Properties)
            {
                conditions.Add($"json_extract({relVar}.properties, '$.{k}') = {VisitExpression(v)}");
            }
        }
    }

    private void AddNodeFiltersToConditions(NodePattern node, string nodeVar, List<string> conditions)
    {
        if (node.Labels.Count == 1)
        {
            conditions.Add($"{nodeVar}.kind = '{node.Labels[0]}'");
        }
        else if (node.Labels.Count > 1)
        {
            var kinds = string.Join(", ", node.Labels.Select(l => $"'{l}'"));
            conditions.Add($"{nodeVar}.kind IN ({kinds})");
        }

        if (node.Properties != null)
        {
            foreach (var (k, v) in node.Properties)
            {
                if (k.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add($"{nodeVar}.id = {VisitExpression(v)}");
                }
                else if (k.Equals("kind", StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add($"COALESCE(json_extract({nodeVar}.properties, '$.kind'), {nodeVar}.kind) = {VisitExpression(v)}");
                }
                else
                {
                    conditions.Add($"json_extract({nodeVar}.properties, '$.{k}') = {VisitExpression(v)}");
                }
            }
        }
    }

    private void ApplyNodeConditions(
        NodePattern node,
        string nodeVar,
        bool isOptional,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions)
    {
        var conditions = new List<string>();
        AddNodeFiltersToConditions(node, nodeVar, conditions);

        if (conditions.Count > 0)
        {
            if (isOptional)
            {
                // Put into ON clause
                fromAndJoins.Append($" AND {string.Join(" AND ", conditions)}");
            }
            else
            {
                // Put into main WHERE clause
                mainWhereConditions.AddRange(conditions);
            }
        }
    }

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
            _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported.")
        };
    }

    private string VisitIdentifier(IdentifierExpression id)
    {
        // If it refers to an unwind variable
        if (_unwindVariables.Contains(id.Name))
        {
            return $"{id.Name}.value";
        }

        // If it refers to a path variable
        if (_pathVariables.TryGetValue(id.Name, out var relVar))
        {
            return $"{relVar}.path_nodes";
        }

        // If it refers to a declared node variable
        if (_declaredNodes.Contains(id.Name))
        {
            return $"json_object('id', {id.Name}.id, 'kind', {id.Name}.kind, 'properties', json({id.Name}.properties))";
        }

        return id.Name;
    }

    private string VisitPropertyAccess(PropertyAccessExpression prop)
    {
        if (prop.PropertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return $"{prop.Variable}.id";
        }
        if (prop.PropertyName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({prop.Variable}.properties, '$.kind'), {prop.Variable}.kind)";
        }

        return $"json_extract({prop.Variable}.properties, '$.{prop.PropertyName}')";
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
            BinaryOperator.In => binary.Right is ListExpression
                ? $"({left} IN {right})"
                : $"(EXISTS (SELECT 1 FROM json_each({right}) WHERE json_each.value = {left}))",
            BinaryOperator.Add => VisitAddOperator(binary.Left, binary.Right, left, right),
            _ => throw new NotSupportedException($"Binary operator {binary.Operator} is not supported.")
        };
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
        if (unary.Operator == UnaryOperator.IsNull)
        {
            if (unary.Operand is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                return $"({id.Name}.id IS NULL)";
            }
            return $"({VisitExpression(unary.Operand)} IS NULL)";
        }

        if (unary.Operator == UnaryOperator.IsNotNull)
        {
            if (unary.Operand is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                return $"({id.Name}.id IS NOT NULL)";
            }
            return $"({VisitExpression(unary.Operand)} IS NOT NULL)";
        }

        if (unary.Operator == UnaryOperator.Not)
        {
            return $"(NOT {VisitExpression(unary.Operand)})";
        }

        throw new NotSupportedException($"Unary operator {unary.Operator} is not supported.");
    }

    private string VisitFunctionCall(FunctionCallExpression func)
    {
        var fn = func.FunctionName.ToLowerInvariant();
        var distinctStr = func.IsDistinct ? "DISTINCT " : "";

        // labels(n)[0] pattern: func = item_at, args[0] = labels(n), args[1] = 0
        if (fn == "item_at" && func.Arguments.Count == 2)
        {
            if (func.Arguments[0] is FunctionCallExpression innerFunc &&
                innerFunc.FunctionName.Equals("labels", StringComparison.OrdinalIgnoreCase) &&
                func.Arguments[1] is NumberLiteralExpression num && num.Value == 0)
            {
                if (innerFunc.Arguments.Count > 0 && innerFunc.Arguments[0] is IdentifierExpression nodeVar)
                {
                    return $"{nodeVar.Name}.kind";
                }
            }
            return $"json_extract({VisitExpression(func.Arguments[0])}, '$[' || {VisitExpression(func.Arguments[1])} || ']')";
        }

        if (fn == "labels" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression nodeVar)
            {
                return $"json_array({nodeVar.Name}.kind)";
            }
        }

        if (fn == "count" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                return $"COUNT({distinctStr}{id.Name}.id)";
            }
            return $"COUNT({distinctStr}{VisitExpression(func.Arguments[0])})";
        }

        if (fn == "collect" && func.Arguments.Count == 1)
        {
            return $"json_group_array({distinctStr}{VisitExpression(func.Arguments[0])})";
        }

        if (fn == "tolower" && func.Arguments.Count == 1)
        {
            return $"lower({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "toupper" && func.Arguments.Count == 1)
        {
            return $"upper({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "tostring" && func.Arguments.Count == 1)
        {
            return $"CAST({VisitExpression(func.Arguments[0])} AS TEXT)";
        }

        if (fn == "length" && func.Arguments.Count == 1)
        {
            return $"length({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "type" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression relVar)
            {
                return $"{relVar.Name}.kind";
            }
        }

        if (fn == "nodes" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression pathVar && _pathVariables.TryGetValue(pathVar.Name, out var relVar))
            {
                return $"json({relVar}.path_nodes)";
            }
        }

        if (fn == "keys" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression nodeVar)
            {
                return $"(SELECT json_group_array(key) FROM json_each({nodeVar.Name}.properties))";
            }
        }

        var args = string.Join(", ", func.Arguments.Select(VisitExpression));
        return $"{func.FunctionName}({distinctStr}{args})";
    }

    private string VisitList(ListExpression list)
    {
        var items = string.Join(", ", list.Items.Select(VisitExpression));
        return $"({items})";
    }

    private string VisitMapLiteral(MapLiteralExpression map)
    {
        var parts = new List<string>();
        foreach (var (k, v) in map.Properties)
        {
            parts.Add($"'{k}', {VisitExpression(v)}");
        }
        return $"json_object({string.Join(", ", parts)})";
    }

    private string VisitListComprehension(ListComprehensionExpression comp)
    {
        var listSql = VisitExpression(comp.List);
        var projSql = comp.Projection != null ? VisitExpression(comp.Projection) : $"{comp.Variable}.value";
        var filterSql = comp.Filter != null ? $" WHERE {VisitExpression(comp.Filter)}" : "";

        return $"(SELECT json_group_array({projSql}) FROM json_each({listSql}) AS {comp.Variable}{filterSql})";
    }

    private string VisitListPredicate(ListPredicateExpression pred)
    {
        // Special case: any(lbl IN labels(n) WHERE lbl = $type)
        if (pred.List is FunctionCallExpression func &&
            func.FunctionName.Equals("labels", StringComparison.OrdinalIgnoreCase) &&
            func.Arguments.Count > 0 &&
            func.Arguments[0] is IdentifierExpression nodeVar)
        {
            if (pred.Predicate is BinaryExpression bin && bin.Operator == BinaryOperator.Equal)
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
        }

        var listSql = VisitExpression(pred.List);
        var whereSql = VisitExpression(pred.Predicate);

        return pred.Quantifier switch
        {
            "any" => $"(EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}))",
            "none" => $"(NOT EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}))",
            "all" => $"(NOT EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE NOT ({whereSql})))",
            _ => $"(EXISTS (SELECT 1 FROM json_each({listSql}) AS {pred.Variable} WHERE {whereSql}))"
        };
    }

    private static bool HasAggregation(Expression expr)
    {
        return expr switch
        {
            FunctionCallExpression f when f.FunctionName.Equals("count", StringComparison.OrdinalIgnoreCase) ||
                                          f.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase) ||
                                          f.FunctionName.Equals("sum", StringComparison.OrdinalIgnoreCase) ||
                                          f.FunctionName.Equals("avg", StringComparison.OrdinalIgnoreCase) ||
                                          f.FunctionName.Equals("min", StringComparison.OrdinalIgnoreCase) ||
                                          f.FunctionName.Equals("max", StringComparison.OrdinalIgnoreCase) => true,
            BinaryExpression b => HasAggregation(b.Left) || HasAggregation(b.Right),
            UnaryExpression u => HasAggregation(u.Operand),
            _ => false
        };
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
            return $"({id.Name}.kind = '{hasLabel.Label}')";
        }
        return $"({VisitExpression(hasLabel.Expression)} = '{hasLabel.Label}')";
    }

    private static string QuoteIdentifier(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

    #region ICypherVisitor boilerplates

    public string VisitMatchClause(MatchClause matchClause) => "";
    public string VisitWhereClause(WhereClause whereClause) => VisitExpression(whereClause.Predicate);
    public string VisitWithClause(WithClause withClause) => "";
    public string VisitUnwindClause(UnwindClause unwindClause) => "";
    public string VisitReturnClause(ReturnClause returnClause) => "";
    public string VisitProjectionItem(ProjectionItem projectionItem) => "";
    public string VisitOrderByClause(OrderByClause orderByClause) => "";
    public string VisitSkipClause(SkipClause skipClause) => "";
    public string VisitLimitClause(LimitClause limitClause) => "";
    public string VisitPathPattern(PathPattern pathPattern) => "";
    public string VisitNodePattern(NodePattern nodePattern) => "";
    public string VisitRelationshipPattern(RelationshipPattern relationshipPattern) => "";

    #endregion
}
