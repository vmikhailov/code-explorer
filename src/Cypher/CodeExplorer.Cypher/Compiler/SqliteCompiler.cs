using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public class SqliteCompiler : ICypherVisitor<string>
{
    private readonly record struct PathHop(string RelVar, bool IsVarLen);
    private readonly record struct CollectExpressionInfo(Expression InnerExpr, bool IsDistinct);

    private readonly CypherQuery _query;
    private readonly IReadOnlyDictionary<string, object?>? _initialParameters;
    private readonly Dictionary<string, object?> _parameters = new();
    private readonly HashSet<string> _declaredNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _declaredRels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _pathVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<PathHop>> _pathHops = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unwindVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _withAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _withCollectNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Expression> _withListAliases = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CollectExpressionInfo> _withCollectExpressions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _aggregatedAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodePropertySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodeIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _ctes = [];

    private static readonly FrozenSet<string> _reservedSqlKeywords = new[]
    {
        "in", "order", "group", "by", "where", "from", "select", "join", "table", "index", "as", "on", "case", "when",
        "then", "else", "end", "with", "limit", "offset", "union", "all", "distinct", "values", "into", "set", "update",
        "delete", "insert", "drop", "create", "alter", "not", "and", "or", "is", "null", "like", "glob", "between",
        "exists", "key", "check", "column", "primary"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static string EscapeVar(string name) => _reservedSqlKeywords.Contains(name) ? $"\"{name}\"" : name;

    private int _paramIndex;
    private int _varIndex;
    private int _cteIndex;

    private SqliteCompiler(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        _query = query;
        _initialParameters = parameters;
    }

    public static SqliteCompiledQuery Compile(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        return new SqliteCompiler(query, parameters).Compile();
    }

    public SqliteCompiledQuery Compile()
    {
        var sql = VisitQuery(_query);
        return new SqliteCompiledQuery(sql, _parameters);
    }

    public string VisitQuery(CypherQuery query)
    {
        CopyInitialParameters();
        PreScanWithClauses(query.WithClauses);

        var fromAndJoins = new StringBuilder();
        List<string> whereConditions = [];
        List<string> groupByColumns = [];
        List<string> havingConditions = [];

        foreach (var match in query.Matches)
        {
            ProcessMatchClause(match, fromAndJoins, whereConditions);
        }

        ProcessWithClauses(query, fromAndJoins, whereConditions, groupByColumns, havingConditions);
        ProcessUnwinds(query.UnwindClauses, fromAndJoins);
        ProcessCalls(query.Calls);

        if (query.Where != null)
        {
            whereConditions.Add(VisitExpression(query.Where.Predicate));
        }

        PopulateReturnGroupBy(query.Return, groupByColumns);

        var selectColumns = BuildSelectColumns(query.Return);
        var sql = AssembleQuerySql(query, selectColumns, fromAndJoins, whereConditions, groupByColumns, havingConditions);
        return AppendUnions(sql, query.Unions);
    }

    private void PopulateReturnGroupBy(ReturnClause returnClause, List<string> groupByColumns)
    {
        if (groupByColumns.Count > 0 || !returnClause.Items.Any(i => HasAggregation(i.Expression))) return;

        foreach (var item in returnClause.Items)
        {
            if (HasAggregation(item.Expression) || item.Expression is WildcardExpression) continue;

            if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                groupByColumns.Add(_nodeIdSource.TryGetValue(id.Name, out var idSrc) ? idSrc : $"{EscapeVar(id.Name)}.id");
            }
            else if (!string.IsNullOrEmpty(item.Alias))
            {
                groupByColumns.Add(QuoteIdentifier(item.Alias));
            }
            else
            {
                groupByColumns.Add(VisitExpression(item.Expression));
            }
        }
    }

    private void CopyInitialParameters()
    {
        if (_initialParameters == null) return;
        foreach (var (k, v) in _initialParameters)
        {
            _parameters[k] = v;
        }
    }

    private void PreScanWithClauses(List<WithClause>? withClauses)
    {
        if (withClauses == null) return;

        foreach (var with in withClauses)
        {
            foreach (var item in with.Items.Where(x => x.Alias != null))
            {
                PreScanWithItem(item.Alias!, item.Expression);
            }
        }
    }

    private void PreScanWithItem(string alias, Expression expression)
    {
        switch (expression)
        {
            case FunctionCallExpression f when
                f.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase) &&
                f.Arguments.Count == 1:
            {
                if (f.Arguments[0] is IdentifierExpression collId)
                {
                    _withCollectNodes[alias] = collId.Name;
                }

                _withCollectExpressions[alias] = new CollectExpressionInfo(f.Arguments[0], f.IsDistinct);
                break;
            }
            case ListComprehensionExpression { List: IdentifierExpression listId } lcomp when
                _withCollectNodes.TryGetValue(listId.Name, out var origNode):
            {
                if (lcomp.Projection is PropertyAccessExpression propAcc &&
                    propAcc.Variable.Equals(lcomp.Variable, StringComparison.OrdinalIgnoreCase))
                {
                    _withListAliases[alias] =
                        new PropertyAccessExpression(origNode, propAcc.PropertyName);
                }

                break;
            }
        }
    }

    private void ProcessWithClauses(
        CypherQuery query,
        StringBuilder fromAndJoins,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        if (query.WithClauses == null) return;

        for (var withIndex = 0; withIndex < query.WithClauses.Count; withIndex++)
        {
            var with = query.WithClauses[withIndex];
            var isStageSplit = with.Items.Any(item =>
                HasAggregation(item.Expression) &&
                GetReferencedIdentifiers(item.Expression).Any(id => _aggregatedAliases.Contains(id)));

            if (isStageSplit)
            {
                ProcessStageSplitCte(query, withIndex, fromAndJoins, whereConditions, groupByColumns, havingConditions);
            }

            ProcessWithClauseItems(with, groupByColumns);
            ProcessWithClauseWhere(with, whereConditions, havingConditions);
        }
    }

    private void ProcessStageSplitCte(
        CypherQuery query,
        int withIndex,
        StringBuilder fromAndJoins,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        var stage1Name = $"_stage_{_ctes.Count + 1}";
        var remainingReferenced = CollectRemainingReferencedIdentifiers(query, withIndex);
        var stage1SelectColumns = BuildStage1SelectColumns(stage1Name, remainingReferenced);
        var stage1GroupingKeys = DetermineStage1GroupingKeys(query, withIndex, groupByColumns);

        var stage1Where = whereConditions.Count > 0 ? $"\nWHERE {string.Join(" AND ", whereConditions)}" : "";
        var stage1GroupBy = stage1GroupingKeys.Count > 0 ? $"\nGROUP BY {string.Join(", ", stage1GroupingKeys.Distinct())}" : "";
        var stage1Having = havingConditions.Count > 0 ? $"\nHAVING {string.Join(" AND ", havingConditions)}" : "";

        var stage1Cte = $"{stage1Name} AS (\nSELECT {string.Join(", ", stage1SelectColumns)}\n{fromAndJoins}{stage1Where}{stage1GroupBy}{stage1Having}\n)";
        _ctes.Add(stage1Cte);

        RemapWithAliasesToStage(stage1Name, remainingReferenced);

        fromAndJoins.Clear();
        fromAndJoins.Append($"FROM {stage1Name}");
        whereConditions.Clear();
        groupByColumns.Clear();
        havingConditions.Clear();
    }

    private void RemapWithAliasesToStage(string stageName, HashSet<string> remainingReferenced)
    {
        foreach (var (alias, _) in _withAliases.ToList())
        {
            if (remainingReferenced.Contains(alias))
            {
                _withAliases[alias] = $"{stageName}.{QuoteIdentifier(alias)}";
            }
        }
    }

    private static HashSet<string> CollectRemainingReferencedIdentifiers(CypherQuery query, int withIndex)
    {
        var remainingReferenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = withIndex; i < query.WithClauses!.Count; i++)
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

        return remainingReferenced;
    }

    private List<string> BuildStage1SelectColumns(string stage1Name, HashSet<string> remainingReferenced)
    {
        List<string> stage1SelectColumns = [];
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

        foreach (var (alias, exprSql) in _withAliases)
        {
            if (remainingReferenced.Contains(alias))
            {
                stage1SelectColumns.Add($"{exprSql} AS {QuoteIdentifier(alias)}");
            }
        }

        return stage1SelectColumns;
    }

    private List<string> DetermineStage1GroupingKeys(CypherQuery query, int withIndex, List<string> currentGroupByColumns)
    {
        List<string> stage1GroupingKeys = [];
        if (withIndex > 0)
        {
            var prevWith = query.WithClauses![withIndex - 1];
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
            stage1GroupingKeys.AddRange(currentGroupByColumns);
        }

        return stage1GroupingKeys;
    }

    private void ProcessWithClauseItems(WithClause with, List<string> groupByColumns)
    {
        foreach (var item in with.Items)
        {
            if (item.Expression is WildcardExpression)
            {
                ProcessWildcardWithItem(with, groupByColumns);
                continue;
            }

            if (item.Alias != null)
            {
                ProcessAliasedWithItem(item.Alias, item.Expression);
            }

            if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                groupByColumns.Add(_nodeIdSource.TryGetValue(id.Name, out var idSrc) ? idSrc : $"{EscapeVar(id.Name)}.id");
            }
        }
    }

    private void ProcessWildcardWithItem(WithClause with, List<string> groupByColumns)
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
                groupByColumns.Add(_nodeIdSource.TryGetValue(declaredNode, out var idSrc) ? idSrc : $"{EscapeVar(declaredNode)}.id");
            }
        }
    }

    private void ProcessAliasedWithItem(string alias, Expression expression)
    {
        _withAliases[alias] = VisitExpression(expression);
        if (expression is FunctionCallExpression f &&
            f.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase) &&
            f.Arguments.Count == 1)
        {
            _withCollectExpressions[alias] = new CollectExpressionInfo(f.Arguments[0], f.IsDistinct);
        }

        if (HasAggregation(expression) ||
            GetReferencedIdentifiers(expression).Any(id => _aggregatedAliases.Contains(id)))
        {
            _aggregatedAliases.Add(alias);
        }
    }

    private void ProcessWithClauseWhere(WithClause with, List<string> whereConditions, List<string> havingConditions)
    {
        if (with.Where == null) return;

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

    private void ProcessUnwinds(List<UnwindClause>? unwinds, StringBuilder fromAndJoins)
    {
        if (unwinds == null) return;
        foreach (var unwind in unwinds)
        {
            _unwindVariables.Add(unwind.Alias);
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"JOIN json_each({VisitExpression(unwind.Expression)}) {EscapeVar(unwind.Alias)}");
        }
    }

    private void ProcessCalls(List<CallClause>? calls)
    {
        if (calls == null) return;
        foreach (var call in calls)
        {
            ProcessCallClause(call);
        }
    }

    private List<string> BuildSelectColumns(ReturnClause returnClause)
    {
        List<string> selectColumns = [];
        foreach (var item in returnClause.Items)
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

            selectColumns.Add(!string.IsNullOrEmpty(alias) ? $"{exprSql} AS {QuoteIdentifier(alias)}" : exprSql);
        }
        return selectColumns;
    }

    private string AssembleQuerySql(
        CypherQuery query,
        List<string> selectColumns,
        StringBuilder fromAndJoins,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        var sb = new StringBuilder();
        if (_ctes.Count > 0)
        {
            sb.Append("WITH RECURSIVE ");
            sb.Append(string.Join(",\n", _ctes));
            sb.AppendLine();
        }

        var distinctStr = query.Return.IsDistinct ? "DISTINCT " : "";
        sb.Append("SELECT ").Append(distinctStr).AppendLine(string.Join(", ", selectColumns));
        sb.Append(fromAndJoins);

        AppendFilterAndGrouping(sb, whereConditions, groupByColumns, havingConditions);
        AppendOrderByAndPagination(sb, query);
        return sb.ToString().TrimEnd();
    }

    private static void AppendFilterAndGrouping(
        StringBuilder sb,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        if (whereConditions.Count > 0)
        {
            sb.AppendLine().Append("WHERE ").Append(string.Join(" AND ", whereConditions));
        }

        if (groupByColumns.Count > 0)
        {
            sb.AppendLine().Append("GROUP BY ").Append(string.Join(", ", groupByColumns.Distinct()));
        }

        if (havingConditions.Count > 0)
        {
            sb.AppendLine().Append("HAVING ").Append(string.Join(" AND ", havingConditions));
        }
    }

    private void AppendOrderByAndPagination(StringBuilder sb, CypherQuery query)
    {
        if (query.OrderBy is { Items.Count: > 0 })
        {
            sb.AppendLine();
            sb.Append("ORDER BY ");
            var orderItems = query.OrderBy.Items.Select(item =>
                $"{VisitExpression(item.Expression)} {(item.IsDescending ? "DESC" : "ASC")}");
            sb.Append(string.Join(", ", orderItems));
        }

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
    }

    private string AppendUnions(string baseSql, List<UnionClause>? unions)
    {
        if (unions == null || unions.Count == 0) return baseSql;

        var sb = new StringBuilder(baseSql);
        foreach (var union in unions)
        {
            var unionCompiled = new SqliteCompiler(union.Query, _initialParameters).Compile();
            foreach (var (k, v) in unionCompiled.Parameters)
            {
                _parameters[k] = v;
            }

            sb.AppendLine();
            sb.AppendLine(union.IsAll ? "UNION ALL" : "UNION");
            sb.Append(unionCompiled.Sql);
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
        List<string> optionalWhereExtra = [];

        if (match.Where != null)
        {
            var targetConditions = isOptional ? optionalWhereExtra : mainWhereConditions;
            targetConditions.Add(VisitExpression(match.Where.Predicate));
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
        if (path.Chain.Count == 0)
        {
            if (!_declaredNodes.Contains(headVar))
            {
                BindHeadNode(path.Head, headVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions);
            }
            return;
        }

        var anyDeclared = _declaredNodes.Contains(headVar) ||
                          path.Chain.Any(c => c.Target.Variable != null && _declaredNodes.Contains(c.Target.Variable));
        if (!anyDeclared)
        {
            BindHeadNode(path.Head, headVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions);
        }

        ProcessPathChain(path, headVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
    }

    private void BindHeadNode(
        NodePattern headNode,
        string headVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions)
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
        ApplyNodeConditions(headNode, headVar, isOptional, fromAndJoins, mainWhereConditions);
    }

    private void ProcessPathChain(
        PathPattern path,
        string headVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var prevNode = path.Head;
        var prevVar = headVar;

        foreach (var element in path.Chain)
        {
            ProcessPathElement(element, path, ref prevNode, ref prevVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
        }
    }

    private void ProcessPathElement(
        PathElement element,
        PathPattern path,
        ref NodePattern prevNode,
        ref string prevVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var rel = element.Relationship;
        var targetNode = element.Target;
        var targetVar = targetNode.Variable ?? $"_n{_varIndex++}";
        var relVar = rel.Variable ?? $"_r{_varIndex++}";

        var isVarLen = rel.Range.HasValue || path.IsShortestPath || path.IsAllShortestPaths;

        if (path.PathVariable != null)
        {
            _pathVariables[path.PathVariable] = relVar;
            if (!_pathHops.TryGetValue(path.PathVariable, out var hopsList))
            {
                hopsList = [];
                _pathHops[path.PathVariable] = hopsList;
            }
            hopsList.Add(new PathHop(relVar, isVarLen));
        }
        ProcessPathHop(
            rel, relVar, prevNode, prevVar, targetNode, targetVar,
            isOptional, isVarLen, path.IsShortestPath, joinKeyword,
            fromAndJoins, mainWhereConditions, optionalWhereExtra);

        prevNode = targetNode;
        prevVar = targetVar;
    }

    private void ProcessPathHop(
        RelationshipPattern rel,
        string relVar,
        NodePattern prevNode,
        string prevVar,
        NodePattern targetNode,
        string targetVar,
        bool isOptional,
        bool isVarLen,
        bool isShortestPath,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var prevDeclared = _declaredNodes.Contains(prevVar);
        var targetDeclared = _declaredNodes.Contains(targetVar);

        if (isVarLen)
        {
            ProcessVariableLengthRel(
                rel, relVar, prevNode, prevVar, targetNode, targetVar,
                prevDeclared, targetDeclared, isOptional, joinKeyword,
                fromAndJoins, mainWhereConditions, optionalWhereExtra,
                isShortestPath);
        }
        else
        {
            ProcessSingleHopRel(
                rel, relVar, prevNode, prevVar, targetNode, targetVar,
                prevDeclared, targetDeclared, isOptional, joinKeyword,
                fromAndJoins, mainWhereConditions, optionalWhereExtra);
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
        List<string> relOnConditions = [];
        List<string> nodeOnConditions = [];
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        if (prevDeclared && !targetDeclared)
        {
            ProcessForwardSingleHop(rel, relVar, rVar, pVar, tVar, targetNode, targetVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra, relOnConditions, nodeOnConditions);
        }
        else if (!prevDeclared && targetDeclared)
        {
            ProcessBackwardSingleHop(rel, relVar, rVar, pVar, tVar, prevNode, prevVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra, relOnConditions, nodeOnConditions);
        }
        else if (prevDeclared && targetDeclared)
        {
            ProcessConnectingSingleHop(rel, relVar, rVar, pVar, tVar, joinKeyword, fromAndJoins, relOnConditions);
        }
    }

    private void ProcessForwardSingleHop(
        RelationshipPattern rel,
        string relVar,
        string rVar,
        string pVar,
        string tVar,
        NodePattern targetNode,
        string targetVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        AddSingleHopEndpoints(rel.Direction, isForward: true, rVar, pVar, tVar, relOnConditions, nodeOnConditions);
        FinishSingleHop(rel, relVar, rVar, targetVar, targetNode, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra, relOnConditions, nodeOnConditions);
    }

    private void ProcessBackwardSingleHop(
        RelationshipPattern rel,
        string relVar,
        string rVar,
        string pVar,
        string tVar,
        NodePattern prevNode,
        string prevVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        AddSingleHopEndpoints(rel.Direction, isForward: false, rVar, pVar, tVar, relOnConditions, nodeOnConditions);
        FinishSingleHop(rel, relVar, rVar, prevVar, prevNode, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra, relOnConditions, nodeOnConditions);
    }

    private static void AddSingleHopEndpoints(
        Direction direction,
        bool isForward,
        string rVar,
        string pVar,
        string tVar,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        var fixedVar = isForward ? pVar : tVar;
        var freeVar = isForward ? tVar : pVar;

        switch (direction)
        {
            case Direction.Outgoing:
                relOnConditions.Add(isForward ? $"{rVar}.from_id = {fixedVar}.id" : $"{rVar}.to_id = {fixedVar}.id");
                nodeOnConditions.Add(isForward ? $"{freeVar}.id = {rVar}.to_id" : $"{freeVar}.id = {rVar}.from_id");
                break;
            case Direction.Incoming:
                relOnConditions.Add(isForward ? $"{rVar}.to_id = {fixedVar}.id" : $"{rVar}.from_id = {fixedVar}.id");
                nodeOnConditions.Add(isForward ? $"{freeVar}.id = {rVar}.from_id" : $"{freeVar}.id = {rVar}.to_id");
                break;
            case Direction.Undirected:
                relOnConditions.Add($"({rVar}.from_id = {fixedVar}.id OR {rVar}.to_id = {fixedVar}.id)");
                nodeOnConditions.Add($"{freeVar}.id = CASE WHEN {rVar}.from_id = {fixedVar}.id THEN {rVar}.to_id ELSE {rVar}.from_id END");
                break;
        }
    }

    private void FinishSingleHop(
        RelationshipPattern rel,
        string relVar,
        string rVar,
        string boundVar,
        NodePattern boundNode,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        AddRelKindConditions(rel, relVar, relOnConditions);
        AddNodeFiltersToConditions(boundNode, boundVar, nodeOnConditions);
        ApplyOptionalWhereExtra(isOptional, optionalWhereExtra, nodeOnConditions);

        EmitRelAndNodeJoin(rVar, EscapeVar(boundVar), relOnConditions, nodeOnConditions, isOptional, joinKeyword, fromAndJoins, mainWhereConditions);
        _declaredRels.Add(relVar);
        _declaredNodes.Add(boundVar);
    }

    private void ProcessConnectingSingleHop(
        RelationshipPattern rel,
        string relVar,
        string rVar,
        string pVar,
        string tVar,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> relOnConditions)
    {
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

    private static void ApplyOptionalWhereExtra(bool isOptional, List<string> optionalWhereExtra, List<string> targetConditions)
    {
        if (isOptional && optionalWhereExtra.Count > 0)
        {
            targetConditions.AddRange(optionalWhereExtra);
            optionalWhereExtra.Clear();
        }
    }

    private static void EmitRelAndNodeJoin(
        string rVar,
        string nodeVar,
        List<string> relOnConditions,
        List<string> nodeOnConditions,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions)
    {
        if (fromAndJoins.Length == 0 && !isOptional)
        {
            fromAndJoins.Append($"FROM edges {rVar}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"JOIN nodes {nodeVar} ON {string.Join(" AND ", nodeOnConditions)}");
            mainWhereConditions.AddRange(relOnConditions);
        }
        else
        {
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} edges {rVar} ON {string.Join(" AND ", relOnConditions)}");
            fromAndJoins.AppendLine();
            fromAndJoins.Append($"{joinKeyword} nodes {nodeVar} ON {string.Join(" AND ", nodeOnConditions)}");
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

        BuildVarLenRecursiveCte(cteName, rel, minDepth, maxDepth);

        var relOnConditions = BuildVarLenRelConditions(cteName, relVar, prevVar, targetVar, minDepth, maxDepth, isShortestPath);
        List<string> nodeOnConditions = [];

        DispatchVarLenHop(
            rel, cteName, relVar, prevNode, prevVar, targetNode, targetVar,
            prevDeclared, targetDeclared, isOptional, joinKeyword,
            fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions);
    }

    private void DispatchVarLenHop(
        RelationshipPattern rel,
        string cteName,
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
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        if (prevDeclared && !targetDeclared)
        {
            ProcessVarLenForwardHop(rel, cteName, relVar, prevVar, targetNode, targetVar, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions);
        }
        else if (!prevDeclared && targetDeclared)
        {
            ProcessVarLenBackwardHop(rel, cteName, relVar, prevNode, prevVar, targetVar, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions);
        }
        else if (prevDeclared && targetDeclared)
        {
            ProcessVarLenConnectingHop(rel, cteName, relVar, prevVar, targetVar, joinKeyword, fromAndJoins, relOnConditions);
        }
    }

    private void BuildVarLenRecursiveCte(string cteName, RelationshipPattern rel, int minDepth, int? maxDepth)
    {
        var edgeKindPred = "1=1";
        if (rel.Types.Count == 1)
        {
            edgeKindPred = $"e.kind = '{rel.Types[0]}'";
        }
        else if (rel.Types.Count > 1)
        {
            var kinds = string.Join(", ", rel.Types.Select(t => $"'{t}'"));
            edgeKindPred = $"e.kind IN ({kinds})";
        }

        var anchorSb = new StringBuilder();
        if (minDepth == 0)
        {
            anchorSb.AppendLine("    SELECT id AS start_id, id AS end_id, 0 AS depth, '/' || id || '/' AS path_visited, json_array(id) AS path_nodes FROM nodes");
            anchorSb.AppendLine("    UNION ALL");
        }
        anchorSb.Append($"    SELECT e.from_id AS start_id, e.to_id AS end_id, 1 AS depth, '/' || e.from_id || '/' || e.to_id || '/' AS path_visited, json_array(e.from_id, e.to_id) AS path_nodes FROM edges e WHERE {edgeKindPred}");

        var maxDepthCond = maxDepth.HasValue ? $" AND c.depth < {maxDepth.Value}" : "";
        var recursiveSql = $@"    SELECT c.start_id, e.to_id, c.depth + 1, c.path_visited || e.to_id || '/', json_insert(c.path_nodes, '$[#]', e.to_id)
    FROM {cteName} c
    JOIN edges e ON e.from_id = c.end_id
    WHERE {edgeKindPred}{maxDepthCond} AND instr(c.path_visited, '/' || e.to_id || '/') = 0";

        var cteSql = $"{cteName}(start_id, end_id, depth, path_visited, path_nodes) AS (\n{anchorSb}\n    UNION ALL\n{recursiveSql}\n)";
        _ctes.Add(cteSql);
    }

    private List<string> BuildVarLenRelConditions(
        string cteName,
        string relVar,
        string prevVar,
        string targetVar,
        int minDepth,
        int? maxDepth,
        bool isShortestPath)
    {
        var rVar = EscapeVar(relVar);
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);

        var conditions = new List<string> { $"{rVar}.depth >= {minDepth}" };
        if (maxDepth.HasValue)
        {
            conditions.Add($"{rVar}.depth <= {maxDepth.Value}");
        }

        if (isShortestPath)
        {
            conditions.Add($"{rVar}.depth = (SELECT min(depth) FROM {cteName} WHERE start_id = {pVar}.id AND end_id = {tVar}.id)");
        }

        return conditions;
    }

    private void ProcessVarLenForwardHop(
        RelationshipPattern rel,
        string cteName,
        string relVar,
        string prevVar,
        NodePattern targetNode,
        string targetVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        AddVarLenHopEndpoints(rel.Direction, isForward: true, rVar, pVar, tVar, relOnConditions, nodeOnConditions);
        FinishVarLenHop(cteName, relVar, rVar, targetVar, targetNode, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions);
    }

    private void ProcessVarLenBackwardHop(
        RelationshipPattern rel,
        string cteName,
        string relVar,
        NodePattern prevNode,
        string prevVar,
        string targetVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        AddVarLenHopEndpoints(rel.Direction, isForward: false, rVar, pVar, tVar, relOnConditions, nodeOnConditions);
        FinishVarLenHop(cteName, relVar, rVar, prevVar, prevNode, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions);
    }

    private static void AddVarLenHopEndpoints(
        Direction direction,
        bool isForward,
        string rVar,
        string pVar,
        string tVar,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        var fixedVar = isForward ? pVar : tVar;
        var freeVar = isForward ? tVar : pVar;
        var useStartForFixed = isForward ? direction != Direction.Incoming : direction == Direction.Incoming;

        if (useStartForFixed)
        {
            relOnConditions.Add($"{rVar}.start_id = {fixedVar}.id");
            nodeOnConditions.Add($"{freeVar}.id = {rVar}.end_id");
        }
        else
        {
            relOnConditions.Add($"{rVar}.end_id = {fixedVar}.id");
            nodeOnConditions.Add($"{freeVar}.id = {rVar}.start_id");
        }
    }

    private void FinishVarLenHop(
        string cteName,
        string relVar,
        string rVar,
        string boundVar,
        NodePattern boundNode,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> optionalWhereExtra,
        List<string> relOnConditions,
        List<string> nodeOnConditions)
    {
        AddNodeFiltersToConditions(boundNode, boundVar, nodeOnConditions);
        ApplyOptionalWhereExtra(isOptional, optionalWhereExtra, nodeOnConditions);
        EmitVarLenRelAndNodeJoin(cteName, rVar, EscapeVar(boundVar), relOnConditions, nodeOnConditions, joinKeyword, fromAndJoins);

        _declaredRels.Add(relVar);
        _declaredNodes.Add(boundVar);
    }

    private void ProcessVarLenConnectingHop(
        RelationshipPattern rel,
        string cteName,
        string relVar,
        string prevVar,
        string targetVar,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> relOnConditions)
    {
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

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

    private static void EmitVarLenRelAndNodeJoin(
        string cteName,
        string rVar,
        string nodeVar,
        List<string> relOnConditions,
        List<string> nodeOnConditions,
        string joinKeyword,
        StringBuilder fromAndJoins)
    {
        fromAndJoins.AppendLine();
        fromAndJoins.Append($"{joinKeyword} {cteName} {rVar} ON {string.Join(" AND ", relOnConditions)}");
        fromAndJoins.AppendLine();
        fromAndJoins.Append($"{joinKeyword} nodes {nodeVar} ON {string.Join(" AND ", nodeOnConditions)}");
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
            AddNodePropertiesConditions(node.Properties, nVar, conditions);
        }
    }

    private void AddNodePropertiesConditions(Dictionary<string, Expression> properties, string nVar, List<string> conditions)
    {
        foreach (var (k, v) in properties)
        {
            if (k.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                conditions.Add($"{nVar}.id = {VisitExpression(v)}");
            }
            else if (k.Equals("kind", StringComparison.OrdinalIgnoreCase))
            {
                conditions.Add(
                    $"COALESCE(json_extract({nVar}.properties, '$.kind'), {nVar}.kind) = {VisitExpression(v)}");
            }
            else
            {
                conditions.Add($"json_extract({nVar}.properties, '$.{k}') = {VisitExpression(v)}");
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
        List<string> conditions = [];
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

    private void ProcessCallClause(CallClause call)
    {
        var subCompiler = new SqliteCompiler(call.Subquery, _initialParameters);
        foreach (var node in _declaredNodes) subCompiler._declaredNodes.Add(node);
        foreach (var rel in _declaredRels) subCompiler._declaredRels.Add(rel);
        foreach (var (k, v) in _pathVariables) subCompiler._pathVariables[k] = v;
        foreach (var (k, v) in _pathHops) subCompiler._pathHops[k] = [..v];
        foreach (var (k, v) in _withAliases) subCompiler._withAliases[k] = v;

        var subCompiled = subCompiler.Compile();
        foreach (var (k, v) in subCompiled.Parameters)
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
                _withAliases[alias] = $"({subCompiled.Sql})";
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
