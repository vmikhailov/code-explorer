using System.Collections.Frozen;
using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler : ICypherVisitor<string>
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
