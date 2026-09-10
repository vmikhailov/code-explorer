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
    private readonly Dictionary<string, string> _withCollectNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Expression> _withListAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Expression InnerExpr, bool IsDistinct)> _withCollectExpressions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _aggregatedAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodePropertySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodeIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _ctes = new();

    private static readonly HashSet<string> ReservedSqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "in", "order", "group", "by", "where", "from", "select", "join", "table", "index", "as", "on", "case", "when", "then", "else", "end", "with", "limit", "offset", "union", "all", "distinct", "values", "into", "set", "update", "delete", "insert", "drop", "create", "alter", "not", "and", "or", "is", "null", "like", "glob", "between", "exists", "key", "check", "column", "primary"
    };

    private static string EscapeVar(string name) => ReservedSqlKeywords.Contains(name) ? $"\"{name}\"" : name;

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

        // Pre-scan WITH clauses for collect nodes and list comprehensions
        if (query.WithClauses != null)
        {
            foreach (var with in query.WithClauses)
            {
                foreach (var item in with.Items)
                {
                    if (item.Alias != null)
                    {
                        if (item.Expression is FunctionCallExpression f &&
                            f.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase) &&
                            f.Arguments.Count == 1)
                        {
                            if (f.Arguments[0] is IdentifierExpression collId)
                            {
                                _withCollectNodes[item.Alias] = collId.Name;
                            }
                            _withCollectExpressions[item.Alias] = (f.Arguments[0], f.IsDistinct);
                        }
                        else if (item.Expression is ListComprehensionExpression lcomp &&
                                 lcomp.List is IdentifierExpression listId &&
                                 _withCollectNodes.TryGetValue(listId.Name, out var origNode))
                        {
                            if (lcomp.Projection is PropertyAccessExpression propAcc &&
                                propAcc.Variable.Equals(lcomp.Variable, StringComparison.OrdinalIgnoreCase))
                            {
                                _withListAliases[item.Alias] = new PropertyAccessExpression(origNode, propAcc.PropertyName);
                            }
                        }
                    }
                }
            }
        }

        // Process all MATCH / OPTIONAL MATCH clauses
        foreach (var match in query.Matches)
        {
            ProcessMatchClause(match, fromAndJoins, whereConditions);
        }

        // Process WITH clauses if any
        var groupByColumns = new List<string>();
        var havingConditions = new List<string>();

        if (query.WithClauses != null)
        {
            for (int withIndex = 0; withIndex < query.WithClauses.Count; withIndex++)
            {
                var with = query.WithClauses[withIndex];

                // Check if this WITH clause represents a new aggregation stage
                var isStageSplit = with.Items.Any(item =>
                    HasAggregation(item.Expression) &&
                    GetReferencedIdentifiers(item.Expression).Any(id => _aggregatedAliases.Contains(id)));

                if (isStageSplit)
                {
                    var stage1Name = $"_stage_{_ctes.Count + 1}";

                    // Find all identifiers referenced in the remaining WITH clauses and RETURN
                    var remainingReferenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int i = withIndex; i < query.WithClauses.Count; i++)
                    {
                        foreach (var it in query.WithClauses[i].Items)
                        {
                            foreach (var refId in GetReferencedIdentifiers(it.Expression))
                            {
                                remainingReferenced.Add(refId);
                            }
                        }
                    }
                    foreach (var it in query.Return.Items)
                    {
                        foreach (var refId in GetReferencedIdentifiers(it.Expression))
                        {
                            remainingReferenced.Add(refId);
                        }
                    }

                    var stage1SelectColumns = new List<string>();

                    // Include nodes needed in later stages
                    foreach (var node in _declaredNodes)
                    {
                        if (remainingReferenced.Contains(node))
                        {
                            var esc = EscapeVar(node);
                            stage1SelectColumns.Add($"{esc}.id AS {node}_id");
                            stage1SelectColumns.Add($"{esc}.properties AS {node}_properties");
                            _nodePropertySource[node] = $"{stage1Name}.{node}_properties";
                            _nodeIdSource[node] = $"{stage1Name}.{node}_id";
                        }
                    }

                    // Include aliases created in stage 1 that are needed in later stages
                    foreach (var (alias, exprSql) in _withAliases)
                    {
                        if (remainingReferenced.Contains(alias))
                        {
                            stage1SelectColumns.Add($"{exprSql} AS {QuoteIdentifier(alias)}");
                        }
                    }

                    // For the grouping keys of Stage 1: use node identifiers of previous WITH clause if available
                    var stage1GroupingKeys = new List<string>();
                    if (withIndex > 0)
                    {
                        var prevWith = query.WithClauses[withIndex - 1];
                        foreach (var prevItem in prevWith.Items)
                        {
                            if (prevItem.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
                            {
                                stage1GroupingKeys.Add($"{EscapeVar(id.Name)}.id");
                            }
                        }
                    }
                    if (stage1GroupingKeys.Count == 0)
                    {
                        stage1GroupingKeys.AddRange(groupByColumns);
                    }

                    var stage1Where = whereConditions.Count > 0 ? $"\nWHERE {string.Join(" AND ", whereConditions)}" : "";
                    var stage1GroupBy = stage1GroupingKeys.Count > 0 ? $"\nGROUP BY {string.Join(", ", stage1GroupingKeys.Distinct())}" : "";
                    var stage1Having = havingConditions.Count > 0 ? $"\nHAVING {string.Join(" AND ", havingConditions)}" : "";

                    var stage1Cte = $"{stage1Name} AS (\nSELECT {string.Join(", ", stage1SelectColumns)}\n{fromAndJoins}{stage1Where}{stage1GroupBy}{stage1Having}\n)";
                    _ctes.Add(stage1Cte);

                    // Update _withAliases for later stages to point to the stage1 columns
                    foreach (var (alias, _) in _withAliases.ToList())
                    {
                        if (remainingReferenced.Contains(alias))
                        {
                            _withAliases[alias] = $"{stage1Name}.{QuoteIdentifier(alias)}";
                        }
                    }

                    // Reset for next stage
                    fromAndJoins.Clear();
                    fromAndJoins.Append($"FROM {stage1Name}");
                    whereConditions.Clear();
                    groupByColumns.Clear();
                    havingConditions.Clear();
                }

                foreach (var item in with.Items)
                {
                    if (item.Expression is WildcardExpression)
                    {
                        var aggregatedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var other in with.Items)
                        {
                            if (HasAggregation(other.Expression))
                            {
                                foreach (var refId in GetReferencedIdentifiers(other.Expression))
                                {
                                    aggregatedVars.Add(refId);
                                }
                            }
                        }
                        foreach (var declaredNode in _declaredNodes)
                        {
                            if (!aggregatedVars.Contains(declaredNode))
                            {
                                if (_nodeIdSource.TryGetValue(declaredNode, out var idSrc))
                                {
                                    groupByColumns.Add(idSrc);
                                }
                                else
                                {
                                    groupByColumns.Add($"{EscapeVar(declaredNode)}.id");
                                }
                            }
                        }
                        continue;
                    }

                    if (item.Alias != null)
                    {
                        _withAliases[item.Alias] = VisitExpression(item.Expression);
                        if (item.Expression is FunctionCallExpression f &&
                            f.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase) &&
                            f.Arguments.Count == 1)
                        {
                            _withCollectExpressions[item.Alias] = (f.Arguments[0], f.IsDistinct);
                        }
                        if (HasAggregation(item.Expression) ||
                            GetReferencedIdentifiers(item.Expression).Any(id => _aggregatedAliases.Contains(id)))
                        {
                            _aggregatedAliases.Add(item.Alias);
                        }
                    }
                    if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
                    {
                        if (_nodeIdSource.TryGetValue(id.Name, out var idSrc))
                        {
                            groupByColumns.Add(idSrc);
                        }
                        else
                        {
                            groupByColumns.Add($"{EscapeVar(id.Name)}.id");
                        }
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

        // Process UNWIND clauses if any (after WITH aliases are populated)
        if (query.UnwindClauses != null)
        {
            foreach (var unwind in query.UnwindClauses)
            {
                _unwindVariables.Add(unwind.Alias);
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"JOIN json_each({VisitExpression(unwind.Expression)}) {EscapeVar(unwind.Alias)}");
            }
        }

        // Process CALL subqueries if any
        if (query.Calls != null)
        {
            foreach (var call in query.Calls)
            {
                ProcessCallClause(call);
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
            if (item.Expression is WildcardExpression)
            {
                selectColumns.Add("*");
                continue;
            }

            var exprSql = VisitExpression(item.Expression);
            var alias = item.Alias;
            if (string.IsNullOrEmpty(alias) && item.Expression is IdentifierExpression id && _withAliases.ContainsKey(id.Name))
            {
                alias = id.Name;
            }

            if (!string.IsNullOrEmpty(alias))
            {
                selectColumns.Add($"{exprSql} AS {QuoteIdentifier(alias)}");
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
            var limitVal = query.Limit != null ? VisitExpression(query.Limit.Expression) : "-1";
            sb.Append($"LIMIT {limitVal}");
            if (query.Skip != null)
            {
                sb.Append($" OFFSET {VisitExpression(query.Skip.Expression)}");
            }
        }

        // Unions if any
        if (query.Unions != null)
        {
            foreach (var union in query.Unions)
            {
                var unionCompiler = new SqliteCompiler(union.Query, _initialParameters);
                var unionSql = unionCompiler.VisitQuery(union.Query);
                foreach (var (k, v) in unionCompiler._parameters)
                {
                    _parameters[k] = v;
                }
                sb.AppendLine();
                sb.AppendLine(union.IsAll ? "UNION ALL" : "UNION");
                sb.Append(unionSql);
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
                    fromAndJoins.Append($"FROM nodes {EscapeVar(headVar)}");
                }
                else
                {
                    fromAndJoins.AppendLine();
                    fromAndJoins.Append($"{joinKeyword} nodes {EscapeVar(headVar)} ON 1=1");
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
                fromAndJoins.Append($"FROM nodes {EscapeVar(headVar)}");
            }
            else
            {
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"{joinKeyword} nodes {EscapeVar(headVar)} ON 1=1");
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

            if (rel.Range.HasValue || path.IsShortestPath || path.IsAllShortestPaths)
            {
                // Variable-length path CTE
                ProcessVariableLengthRel(
                    rel, relVar, prevNode, prevVar, targetNode, targetVar,
                    prevDeclared, targetDeclared, isOptional, joinKeyword,
                    fromAndJoins, mainWhereConditions, optionalWhereExtra,
                    path.IsShortestPath);
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

        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        // Direction mapping
        if (prevDeclared && !targetDeclared)
        {
            // Traverse from prev to target
            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    relOnConditions.Add($"{rVar}.from_id = {pVar}.id");
                    nodeOnConditions.Add($"{tVar}.id = {rVar}.to_id");
                    break;
                case Direction.Incoming:
                    relOnConditions.Add($"{rVar}.to_id = {pVar}.id");
                    nodeOnConditions.Add($"{tVar}.id = {rVar}.from_id");
                    break;
                case Direction.Undirected:
                    relOnConditions.Add($"({rVar}.from_id = {pVar}.id OR {rVar}.to_id = {pVar}.id)");
                    nodeOnConditions.Add($"{tVar}.id = CASE WHEN {rVar}.from_id = {pVar}.id THEN {rVar}.to_id ELSE {rVar}.from_id END");
                    break;
            }

            AddRelKindConditions(rel, relVar, relOnConditions);
            AddNodeFiltersToConditions(targetNode, targetVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            if (fromAndJoins.Length == 0 && !isOptional)
            {
                fromAndJoins.Append($"FROM edges {rVar}");
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"JOIN nodes {tVar} ON {string.Join(" AND ", nodeOnConditions)}");
                mainWhereConditions.AddRange(relOnConditions);
            }
            else
            {
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"{joinKeyword} edges {rVar} ON {string.Join(" AND ", relOnConditions)}");
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"{joinKeyword} nodes {tVar} ON {string.Join(" AND ", nodeOnConditions)}");
            }

            _declaredRels.Add(relVar);
            _declaredNodes.Add(targetVar);
        }
        else if (!prevDeclared && targetDeclared)
        {
            // Target is already declared, bind prev node
            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    relOnConditions.Add($"{rVar}.to_id = {tVar}.id");
                    nodeOnConditions.Add($"{pVar}.id = {rVar}.from_id");
                    break;
                case Direction.Incoming:
                    relOnConditions.Add($"{rVar}.from_id = {tVar}.id");
                    nodeOnConditions.Add($"{pVar}.id = {rVar}.to_id");
                    break;
                case Direction.Undirected:
                    relOnConditions.Add($"({rVar}.from_id = {tVar}.id OR {rVar}.to_id = {tVar}.id)");
                    nodeOnConditions.Add($"{pVar}.id = CASE WHEN {rVar}.from_id = {tVar}.id THEN {rVar}.to_id ELSE {rVar}.from_id END");
                    break;
            }

            AddRelKindConditions(rel, relVar, relOnConditions);
            AddNodeFiltersToConditions(prevNode, prevVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            if (fromAndJoins.Length == 0 && !isOptional)
            {
                fromAndJoins.Append($"FROM edges {rVar}");
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"JOIN nodes {pVar} ON {string.Join(" AND ", nodeOnConditions)}");
                mainWhereConditions.AddRange(relOnConditions);
            }
            else
            {
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"{joinKeyword} edges {rVar} ON {string.Join(" AND ", relOnConditions)}");
                fromAndJoins.AppendLine();
                fromAndJoins.Append($"{joinKeyword} nodes {pVar} ON {string.Join(" AND ", nodeOnConditions)}");
            }

            _declaredRels.Add(relVar);
            _declaredNodes.Add(prevVar);
        }
        else if (prevDeclared && targetDeclared)
        {
            // Both are declared, connect them with edge
            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    relOnConditions.Add($"{rVar}.from_id = {pVar}.id AND {rVar}.to_id = {tVar}.id");
                    break;
                case Direction.Incoming:
                    relOnConditions.Add($"{rVar}.to_id = {pVar}.id AND {rVar}.from_id = {tVar}.id");
                    break;
                case Direction.Undirected:
                    relOnConditions.Add($"(({rVar}.from_id = {pVar}.id AND {rVar}.to_id = {tVar}.id) OR ({rVar}.to_id = {pVar}.id AND {rVar}.from_id = {tVar}.id))");
                    break;
            }

            AddRelKindConditions(rel, relVar, relOnConditions);

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} edges {rVar} ON {string.Join(" AND ", relOnConditions)}");
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
        List<string> optionalWhereExtra,
        bool isShortestPath = false)
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

        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        relOnConditions.Add($"{rVar}.depth >= {minDepth}");
        if (maxDepth.HasValue)
        {
            relOnConditions.Add($"{rVar}.depth <= {maxDepth.Value}");
        }
        if (isShortestPath)
        {
            relOnConditions.Add($"{rVar}.depth = (SELECT min(depth) FROM {cteName} WHERE start_id = {pVar}.id AND end_id = {tVar}.id)");
        }

        if (prevDeclared && !targetDeclared)
        {
            if (rel.Direction == Direction.Incoming)
            {
                relOnConditions.Add($"{rVar}.end_id = {pVar}.id");
                nodeOnConditions.Add($"{tVar}.id = {rVar}.start_id");
            }
            else
            {
                relOnConditions.Add($"{rVar}.start_id = {pVar}.id");
                nodeOnConditions.Add($"{tVar}.id = {rVar}.end_id");
            }

            AddNodeFiltersToConditions(targetNode, targetVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} {cteName} {rVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {tVar} ON {string.Join(" AND ", nodeOnConditions)}");

            _declaredRels.Add(relVar);
            _declaredNodes.Add(targetVar);
        }
        else if (!prevDeclared && targetDeclared)
        {
            if (rel.Direction == Direction.Incoming)
            {
                relOnConditions.Add($"{rVar}.start_id = {tVar}.id");
                nodeOnConditions.Add($"{pVar}.id = {rVar}.end_id");
            }
            else
            {
                relOnConditions.Add($"{rVar}.end_id = {tVar}.id");
                nodeOnConditions.Add($"{pVar}.id = {rVar}.start_id");
            }

            AddNodeFiltersToConditions(prevNode, prevVar, nodeOnConditions);

            if (isOptional && optionalWhereExtra.Count > 0)
            {
                nodeOnConditions.AddRange(optionalWhereExtra);
                optionalWhereExtra.Clear();
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} {cteName} {rVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {pVar} ON {string.Join(" AND ", nodeOnConditions)}");

            _declaredRels.Add(relVar);
            _declaredNodes.Add(prevVar);
        }
        else if (prevDeclared && targetDeclared)
        {
            if (rel.Direction == Direction.Incoming)
            {
                relOnConditions.Add($"{rVar}.end_id = {pVar}.id AND {rVar}.start_id = {tVar}.id");
            }
            else
            {
                relOnConditions.Add($"{rVar}.start_id = {pVar}.id AND {rVar}.end_id = {tVar}.id");
            }

            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} {cteName} {rVar} ON {string.Join(" AND ", relOnConditions)}");
            _declaredRels.Add(relVar);
        }
    }

    private void AddRelKindConditions(RelationshipPattern rel, string relVar, List<string> conditions)
    {
        var rVar = EscapeVar(relVar);
        if (rel.Types.Count == 1)
        {
            conditions.Add($"{rVar}.kind = '{rel.Types[0]}'");
        }
        else if (rel.Types.Count > 1)
        {
            var kinds = string.Join(", ", rel.Types.Select(t => $"'{t}'"));
            conditions.Add($"{rVar}.kind IN ({kinds})");
        }

        if (rel.Properties != null)
        {
            foreach (var (k, v) in rel.Properties)
            {
                conditions.Add($"json_extract({rVar}.properties, '$.{k}') = {VisitExpression(v)}");
            }
        }
    }

    private void AddNodeFiltersToConditions(NodePattern node, string nodeVar, List<string> conditions)
    {
        var nVar = EscapeVar(nodeVar);
        if (node.Labels.Count == 1)
        {
            conditions.Add($"{nVar}.kind = '{node.Labels[0]}'");
        }
        else if (node.Labels.Count > 1)
        {
            var kinds = string.Join(", ", node.Labels.Select(l => $"'{l}'"));
            conditions.Add($"{nVar}.kind IN ({kinds})");
        }

        if (node.Properties != null)
        {
            foreach (var (k, v) in node.Properties)
            {
                if (k.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add($"{nVar}.id = {VisitExpression(v)}");
                }
                else if (k.Equals("kind", StringComparison.OrdinalIgnoreCase))
                {
                    conditions.Add($"COALESCE(json_extract({nVar}.properties, '$.kind'), {nVar}.kind) = {VisitExpression(v)}");
                }
                else
                {
                    conditions.Add($"json_extract({nVar}.properties, '$.{k}') = {VisitExpression(v)}");
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
        // Check with aliases first
        if (_withAliases.TryGetValue(id.Name, out var aliasSql))
        {
            return aliasSql;
        }

        var escaped = EscapeVar(id.Name);

        // If it refers to an unwind variable
        if (_unwindVariables.Contains(id.Name))
        {
            return $"{escaped}.value";
        }

        // If it refers to a path variable
        if (_pathVariables.TryGetValue(id.Name, out var relVar))
        {
            return $"{EscapeVar(relVar)}.path_nodes";
        }

        if (_nodePropertySource.TryGetValue(id.Name, out var propSource))
        {
            var idSource = _nodeIdSource.TryGetValue(id.Name, out var idSrc) ? idSrc : "NULL";
            return $"json_object('id', {idSource}, 'properties', json({propSource}))";
        }

        // If it refers to a declared node variable
        if (_declaredNodes.Contains(id.Name))
        {
            return $"json_object('id', {escaped}.id, 'kind', {escaped}.kind, 'properties', json({escaped}.properties))";
        }

        return escaped;
    }

    private string VisitPropertyAccess(PropertyAccessExpression prop)
    {
        var v = EscapeVar(prop.Variable);
        if (_unwindVariables.Contains(prop.Variable))
        {
            if (prop.PropertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return $"COALESCE(json_extract({v}.value, '$.id'), {v}.value)";
            }
            if (prop.PropertyName.Equals("kind", StringComparison.OrdinalIgnoreCase))
            {
                return $"COALESCE(json_extract({v}.value, '$.kind'), {v}.value)";
            }
            return $"COALESCE(json_extract({v}.value, '$.properties.' || '{prop.PropertyName}'), json_extract({v}.value, '$.{prop.PropertyName}'))";
        }

        if (_nodePropertySource.TryGetValue(prop.Variable, out var propSrc))
        {
            if (prop.PropertyName.Equals("id", StringComparison.OrdinalIgnoreCase) && _nodeIdSource.TryGetValue(prop.Variable, out var idSrc))
            {
                return idSrc;
            }
            if (prop.PropertyName.Equals("kind", StringComparison.OrdinalIgnoreCase))
            {
                return $"COALESCE(json_extract({propSrc}, '$.kind'), {propSrc})";
            }
            return $"json_extract({propSrc}, '$.{prop.PropertyName}')";
        }

        if (prop.PropertyName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return $"{v}.id";
        }
        if (prop.PropertyName.Equals("kind", StringComparison.OrdinalIgnoreCase))
        {
            return $"COALESCE(json_extract({v}.properties, '$.kind'), {v}.kind)";
        }

        return $"json_extract({v}.properties, '$.{prop.PropertyName}')";
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
            BinaryOperator.In => binary.Right is ListExpression list && list.Items.Count == 0
                ? "(0 = 1)"
                : binary.Right is ListExpression listExpr
                    ? $"({left} IN ({string.Join(", ", listExpr.Items.Select(VisitExpression))}))"
                    : binary.Right is IdentifierExpression rightId && _withCollectNodes.TryGetValue(rightId.Name, out var collNode) && binary.Left is IdentifierExpression leftId && leftId.Name.Equals(collNode, StringComparison.OrdinalIgnoreCase)
                        ? $"({EscapeVar(collNode)}.id IS NOT NULL)"
                        : binary.Right is IdentifierExpression rId && _withListAliases.TryGetValue(rId.Name, out var targetExpr)
                            ? $"({left} = {VisitExpression(targetExpr)})"
                            : $"(EXISTS (SELECT 1 FROM json_each({right}) WHERE json_each.value = {left}))",
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
        if (unary.Operator == UnaryOperator.Minus)
        {
            return $"-({VisitExpression(unary.Operand)})";
        }

        if (unary.Operator == UnaryOperator.Plus)
        {
            return $"+({VisitExpression(unary.Operand)})";
        }

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

        if ((fn == "id" || fn == "elementid") && func.Arguments.Count == 1)
        {
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

        if (fn == "left" && func.Arguments.Count == 2)
        {
            return $"substr({VisitExpression(func.Arguments[0])}, 1, {VisitExpression(func.Arguments[1])})";
        }

        if (fn == "right" && func.Arguments.Count == 2)
        {
            return $"substr({VisitExpression(func.Arguments[0])}, -({VisitExpression(func.Arguments[1])}))";
        }

        if (fn == "ltrim" && func.Arguments.Count == 1)
        {
            return $"ltrim({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "rtrim" && func.Arguments.Count == 1)
        {
            return $"rtrim({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "exists" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is PatternExpression pat)
            {
                return VisitPatternExpression(pat);
            }
            return $"({VisitExpression(func.Arguments[0])} IS NOT NULL)";
        }

        if (fn == "labels" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression nodeVar)
            {
                return $"json_array({EscapeVar(nodeVar.Name)}.kind)";
            }
        }

        if (fn == "count" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is WildcardExpression)
            {
                return $"COUNT({distinctStr}*)";
            }
            if (func.Arguments[0] is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                return $"COUNT({distinctStr}{EscapeVar(id.Name)}.id)";
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

        if (fn == "substring" && (func.Arguments.Count == 2 || func.Arguments.Count == 3))
        {
            var strSql = VisitExpression(func.Arguments[0]);
            var startSql = VisitExpression(func.Arguments[1]);
            if (func.Arguments.Count == 3)
            {
                var lenSql = VisitExpression(func.Arguments[2]);
                return $"substr({strSql}, ({startSql}) + 1, {lenSql})";
            }
            return $"substr({strSql}, ({startSql}) + 1)";
        }

        if (fn == "type" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression relVar)
            {
                return $"{EscapeVar(relVar.Name)}.kind";
            }
        }

        if (fn == "nodes" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression pathVar && _pathVariables.TryGetValue(pathVar.Name, out var relVar))
            {
                return $"json({EscapeVar(relVar)}.path_nodes)";
            }
        }

        if (fn == "relationships" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression pathVar && _pathVariables.TryGetValue(pathVar.Name, out var relVar))
            {
                var rVar = EscapeVar(relVar);
                if (_declaredRels.Contains(relVar))
                {
                    return $"json_array(json_object('type', {rVar}.kind, 'from', {rVar}.from_id, 'to', {rVar}.to_id, 'properties', json({rVar}.properties)))";
                }
                return $"json({rVar}.path_nodes)";
            }
            return $"json({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "properties" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression nodeVar)
            {
                return $"json({EscapeVar(nodeVar.Name)}.properties)";
            }
            return $"json({VisitExpression(func.Arguments[0])})";
        }

        if (fn == "keys" && func.Arguments.Count == 1)
        {
            if (func.Arguments[0] is IdentifierExpression nodeVar)
            {
                return $"(SELECT json_group_array(key) FROM json_each({EscapeVar(nodeVar.Name)}.properties))";
            }
        }

        var args = string.Join(", ", func.Arguments.Select(VisitExpression));
        return $"{func.FunctionName}({distinctStr}{args})";
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
        var parts = new List<string>();
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

        var listSql = VisitExpression(comp.List);
        bool wasAdded = _unwindVariables.Add(comp.Variable);
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

    private static Expression SubstituteComprehensionVariable(Expression expr, string varName, Expression innerExpr)
    {
        switch (expr)
        {
            case PropertyAccessExpression prop when prop.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase):
                if (innerExpr is MapLiteralExpression map && map.Properties.TryGetValue(prop.PropertyName, out var val))
                {
                    return val;
                }
                if (innerExpr is IdentifierExpression innerId)
                {
                    return new PropertyAccessExpression(innerId.Name, prop.PropertyName);
                }
                return prop;

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
        bool wasAdded = _unwindVariables.Add(pred.Variable);
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
            return $"({EscapeVar(id.Name)}.kind = '{hasLabel.Label}')";
        }
        return $"({VisitExpression(hasLabel.Expression)} = '{hasLabel.Label}')";
    }

    private static string QuoteIdentifier(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

    private string VisitListSlice(ListSliceExpression slice)
    {
        var listSql = VisitExpression(slice.List);
        var whereConditions = new List<string>();
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

    private string VisitPatternExpression(PatternExpression pat)
    {
        var path = pat.Path;
        var conditions = new List<string>();
        var fromJoins = new StringBuilder();

        var headVar = path.Head.Variable;
        bool headIsOuter = headVar != null && _declaredNodes.Contains(headVar);
        var actualHeadVar = headIsOuter ? headVar! : $"_pe_h{_varIndex++}";

        if (!headIsOuter)
        {
            fromJoins.Append($"nodes {actualHeadVar}");
            AddNodeFiltersToConditions(path.Head, actualHeadVar, conditions);
        }

        var prevVar = actualHeadVar;

        for (int i = 0; i < path.Chain.Count; i++)
        {
            var element = path.Chain[i];
            var rel = element.Relationship;
            var targetNode = element.Target;
            var relVar = $"_pe_r{_varIndex++}";
            var targetVar = targetNode.Variable ?? $"_pe_t{_varIndex++}";

            if (fromJoins.Length == 0)
            {
                fromJoins.Append($"edges {relVar} JOIN nodes {targetVar}");
            }
            else
            {
                fromJoins.Append($" JOIN edges {relVar}");
                fromJoins.Append($" JOIN nodes {targetVar}");
            }

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

        var whereClause = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
        return $"(EXISTS (SELECT 1 FROM {fromJoins}{whereClause}))";
    }

    private string VisitPatternComprehension(PatternComprehensionExpression patComp)
    {
        var path = patComp.Path;
        var conditions = new List<string>();
        var fromJoins = new StringBuilder();

        var headVar = path.Head.Variable;
        bool headIsOuter = headVar != null && _declaredNodes.Contains(headVar);
        var actualHeadVar = headIsOuter ? headVar! : $"_pc_h{_varIndex++}";

        if (!headIsOuter)
        {
            fromJoins.Append($"nodes {actualHeadVar}");
            AddNodeFiltersToConditions(path.Head, actualHeadVar, conditions);
        }

        var prevVar = actualHeadVar;

        for (int i = 0; i < path.Chain.Count; i++)
        {
            var element = path.Chain[i];
            var rel = element.Relationship;
            var targetNode = element.Target;
            var relVar = $"_pc_r{_varIndex++}";
            var targetVar = targetNode.Variable ?? $"_pc_t{_varIndex++}";

            if (fromJoins.Length == 0)
            {
                fromJoins.Append($"edges {relVar} JOIN nodes {targetVar}");
            }
            else
            {
                fromJoins.Append($" JOIN edges {relVar}");
                fromJoins.Append($" JOIN nodes {targetVar}");
            }

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

    private void ProcessCallClause(CallClause call)
    {
        var subCompiler = new SqliteCompiler(call.Subquery, _initialParameters);
        foreach (var node in _declaredNodes) subCompiler._declaredNodes.Add(node);
        foreach (var rel in _declaredRels) subCompiler._declaredRels.Add(rel);
        foreach (var (k, v) in _pathVariables) subCompiler._pathVariables[k] = v;
        foreach (var (k, v) in _withAliases) subCompiler._withAliases[k] = v;

        var subSql = subCompiler.VisitQuery(call.Subquery);
        foreach (var (k, v) in subCompiler._parameters)
        {
            _parameters[k] = v;
        }
        _varIndex += subCompiler._varIndex;
        _paramIndex += subCompiler._paramIndex;

        foreach (var item in call.Subquery.Return.Items)
        {
            var alias = item.Alias ?? (item.Expression is IdentifierExpression id ? id.Name : null);
            if (alias != null)
            {
                _withAliases[alias] = $"({subSql})";
            }
        }
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
            case CaseExpression c:
                if (c.TestExpression != null) CollectIdentifiers(c.TestExpression, set);
                foreach (var w in c.WhenBranches) { CollectIdentifiers(w.When, set); CollectIdentifiers(w.Then, set); }
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
        var baseVar = mapProj.BaseExpression is IdentifierExpression id ? id.Name : VisitExpression(mapProj.BaseExpression);
        var parts = new List<string>();
        foreach (var elem in mapProj.Elements)
        {
            if (elem.IsAllProperties)
            {
                continue;
            }
            if (elem.ValueExpression != null)
            {
                parts.Add($"'{elem.PropertyName}', {VisitExpression(elem.ValueExpression)}");
            }
            else
            {
                // Property from base: .prop
                parts.Add($"'{elem.PropertyName}', json_extract({baseVar}.properties, '$.{elem.PropertyName}')");
            }
        }
        return $"json_object({string.Join(", ", parts)})";
    }

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
    public string VisitCallClause(CallClause callClause) => "";
    public string VisitUnionClause(UnionClause unionClause) => "";
    public string VisitPathPattern(PathPattern pathPattern) => "";
    public string VisitNodePattern(NodePattern nodePattern) => "";
    public string VisitRelationshipPattern(RelationshipPattern relationshipPattern) => "";

    #endregion
}
