using System.Collections.Frozen;
using System.Globalization;
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
    private readonly HashSet<string> _varLenRels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _pathVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<PathHop>> _pathHops = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unwindVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _withAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _withCollectNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Expression> _withListAliases = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CollectExpressionInfo> _withCollectExpressions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _listValuedAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _aggregatedAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodePropertySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodeIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodeRowIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _nodeKindSource = new(StringComparer.OrdinalIgnoreCase);
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
    private readonly bool _rawNodeColumns;

    private SqliteCompiler(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, bool rawNodeColumns = false)
    {
        _query = query;
        _initialParameters = parameters;
        _rawNodeColumns = rawNodeColumns;
    }

    public static SqliteCompiledQuery Compile(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, bool strictLabels = true, bool rawNodeColumns = false)
    {
        return new SqliteCompiler(query, parameters, rawNodeColumns).Compile();
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
        DetectDecomposedOptionalMatches(query);

        var fromAndJoins = new StringBuilder();
        List<string> whereConditions = [];
        List<string> groupByColumns = [];
        List<string> havingConditions = [];

        foreach (var match in query.Matches)
        {
            if (IsMatchDecomposed(match)) continue;
            ProcessMatchClause(match, fromAndJoins, whereConditions);
        }

        ProcessWithClauses(query, fromAndJoins, whereConditions, groupByColumns, havingConditions);
        ProcessUnwinds(query.UnwindClauses, fromAndJoins);
        ProcessCalls(query.Calls);

        if (query.Where != null)
        {
            whereConditions.Add(VisitExpression(query.Where.Predicate));
        }

        var effectiveQuery = MaterializeReturnSubqueryAggregationsIfNeeded(
            query, fromAndJoins, whereConditions, groupByColumns, havingConditions);
        PopulateReturnGroupBy(effectiveQuery.Return, groupByColumns);

        var selectColumns = BuildSelectColumns(effectiveQuery.Return);
        var sql = AssembleQuerySql(effectiveQuery, selectColumns, fromAndJoins, whereConditions, groupByColumns, havingConditions);
        return AppendUnions(sql, effectiveQuery.Unions);
    }

    private bool IsMatchDecomposed(MatchClause match)
    {
        if (!match.IsOptional || _decomposedBranches.Count == 0) return false;
        foreach (var path in match.Paths)
        {
            if (path.Head.Variable != null && _decomposedBranches.ContainsKey(path.Head.Variable))
                return true;
            foreach (var elem in path.Chain)
            {
                if (elem.Target.Variable != null && _decomposedBranches.ContainsKey(elem.Target.Variable))
                    return true;
                if (elem.Relationship.Variable != null && _decomposedBranches.ContainsKey(elem.Relationship.Variable))
                    return true;
            }
        }
        return false;
    }

    private void PopulateReturnGroupBy(ReturnClause returnClause, List<string> groupByColumns)
    {
        if (groupByColumns.Count > 0 || !returnClause.Items.Any(i => HasAggregation(i.Expression))) return;

        foreach (var item in returnClause.Items)
        {
            if (HasAggregation(item.Expression) || item.Expression is WildcardExpression) continue;
            if (GetReferencedIdentifiers(item.Expression).Any(id => _aggregatedAliases.Contains(id))) continue;

            if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                groupByColumns.Add(_nodeIdSource.TryGetValue(id.Name, out var idSrc) ? idSrc : $"{EscapeVar(id.Name)}.id");
            }
            else if (item.Expression is IdentifierExpression relId && _declaredRels.Contains(relId.Name))
            {
                groupByColumns.Add(GetRelRowIdSql(relId.Name));
            }
            else if (!string.IsNullOrEmpty(item.Alias) &&
                     !item.Alias.Equals("id", StringComparison.OrdinalIgnoreCase) &&
                     !item.Alias.Equals("kind", StringComparison.OrdinalIgnoreCase) &&
                     !item.Alias.Equals("properties", StringComparison.OrdinalIgnoreCase) &&
                     !item.Alias.Equals("from_id", StringComparison.OrdinalIgnoreCase) &&
                     !item.Alias.Equals("to_id", StringComparison.OrdinalIgnoreCase) &&
                     !item.Alias.Equals("rowid", StringComparison.OrdinalIgnoreCase))
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
            foreach (var item in with.Items)
            {
                if (item.Alias != null)
                {
                    PreScanWithItem(item.Alias, item.Expression);
                }
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
            var prevWith = withIndex > 0 ? query.WithClauses[withIndex - 1] : null;
            var isStageSplit = with.Items.Any(item =>
                HasAggregation(item.Expression) &&
                (GetReferencedIdentifiers(item.Expression).Any(id => _aggregatedAliases.Contains(id)) ||
                 prevWith is { IsDistinct: true } or { OrderBy: not null } or { Skip: not null } or { Limit: not null }));

            if (isStageSplit)
            {
                ProcessStageSplitCte(query, withIndex, fromAndJoins, whereConditions, groupByColumns, havingConditions);
            }

            ProcessWithClauseItems(with, groupByColumns);
            if (with.Where != null &&
                _aggregatedAliases.Count > 0 &&
                ContainsSubqueryOverAliases(with.Where.Predicate, _aggregatedAliases))
            {
                MaterializeCurrentWithAggregationStage(query, withIndex, fromAndJoins, whereConditions, groupByColumns, havingConditions);
            }

            ProcessWithClauseWhere(with, whereConditions, havingConditions);
        }

        var unwindReferencesAggregatedAlias = query.UnwindClauses != null &&
            query.UnwindClauses.Any(u => GetReferencedIdentifiers(u.Expression).Overlaps(_aggregatedAliases));
        if (_aggregatedAliases.Count > 0 &&
            (query.Return.Items.Any(i => HasAggregation(i.Expression)) ||
             unwindReferencesAggregatedAlias ||
             ContainsSubqueryOverReturnClauses(query, _aggregatedAliases)))
        {
            ProcessStageSplitCte(query, query.WithClauses.Count, fromAndJoins, whereConditions, groupByColumns, havingConditions);
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
        var prevWith = withIndex > 0 ? query.WithClauses![withIndex - 1] : null;
        var stage1GroupingKeys = prevWith is { IsDistinct: true } && groupByColumns.Count == 0
            ? []
            : DetermineStage1GroupingKeys(query, withIndex, groupByColumns);

        var stage1Where = whereConditions.Count > 0 ? $"\nWHERE {string.Join(" AND ", whereConditions)}" : "";
        var stage1GroupBy = stage1GroupingKeys.Count > 0 ? $"\nGROUP BY {string.Join(", ", stage1GroupingKeys.Distinct())}" : "";
        var stage1Having = havingConditions.Count > 0 ? $"\nHAVING {string.Join(" AND ", havingConditions)}" : "";

        var with = withIndex < query.WithClauses!.Count ? query.WithClauses[withIndex] : null;
        var stage1Distinct = (prevWith?.IsDistinct == true || with?.IsDistinct == true) ? "DISTINCT " : "";
        var stage1OrderBy = "";
        if (prevWith?.OrderBy is { Items.Count: > 0 })
        {
            var ordItems = prevWith.OrderBy.Items.Select(item =>
                $"{VisitExpression(item.Expression)} {(item.IsDescending ? "DESC" : "ASC")}");
            stage1OrderBy = $"\nORDER BY {string.Join(", ", ordItems)}";
        }
        var stage1LimitOffset = "";
        if (prevWith?.Limit != null || prevWith?.Skip != null)
        {
            var lim = prevWith.Limit != null ? VisitExpression(prevWith.Limit.Expression) : "-1";
            stage1LimitOffset = $"\nLIMIT {lim}" + (prevWith.Skip != null ? $" OFFSET {VisitExpression(prevWith.Skip.Expression)}" : "");
        }
        var stage1Cte = $"{stage1Name} AS (\nSELECT {stage1Distinct}{string.Join(", ", stage1SelectColumns)}\n{fromAndJoins}{stage1Where}{stage1GroupBy}{stage1Having}{stage1OrderBy}{stage1LimitOffset}\n)";
        _ctes.Add(stage1Cte);

        RemapWithAliasesToStage(stage1Name, remainingReferenced);

        fromAndJoins.Clear();
        fromAndJoins.Append($"FROM {stage1Name}");
        whereConditions.Clear();
        groupByColumns.Clear();
        havingConditions.Clear();
        _aggregatedAliases.Clear();
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
            var w = query.WithClauses[i];
            foreach (var it in w.Items)
            {
                remainingReferenced.UnionWith(GetReferencedIdentifiers(it.Expression));
            }

            if (w.Where != null)
            {
                remainingReferenced.UnionWith(GetReferencedIdentifiers(w.Where.Predicate));
            }

            if (w.OrderBy != null)
            {
                foreach (var ord in w.OrderBy.Items)
                {
                    remainingReferenced.UnionWith(GetReferencedIdentifiers(ord.Expression));
                }
            }
        }

        if (query.UnwindClauses != null)
        {
            foreach (var unwind in query.UnwindClauses)
            {
                remainingReferenced.UnionWith(GetReferencedIdentifiers(unwind.Expression));
            }
        }

        if (query.Where != null)
        {
            remainingReferenced.UnionWith(GetReferencedIdentifiers(query.Where.Predicate));
        }

        foreach (var it in query.Return.Items)
        {
            remainingReferenced.UnionWith(GetReferencedIdentifiers(it.Expression));
        }

        if (query.OrderBy != null)
        {
            foreach (var ord in query.OrderBy.Items)
            {
                remainingReferenced.UnionWith(GetReferencedIdentifiers(ord.Expression));
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
                var rowIdExpr = _nodeRowIdSource.GetValueOrDefault(node, $"{esc}.rowid");
                var idExpr = _nodeIdSource.GetValueOrDefault(node, $"{esc}.id");
                var kindExpr = _nodeKindSource.GetValueOrDefault(node, $"{esc}.kind");
                var propExpr = _nodePropertySource.GetValueOrDefault(node, $"{esc}.properties");
                stage1SelectColumns.Add($"{rowIdExpr} AS {node}_rowid");
                stage1SelectColumns.Add($"{idExpr} AS {node}_id");
                stage1SelectColumns.Add($"{kindExpr} AS {node}_kind");
                stage1SelectColumns.Add($"{propExpr} AS {node}_properties");
                _nodeRowIdSource[node] = $"{stage1Name}.{node}_rowid";
                _nodeIdSource[node] = $"{stage1Name}.{node}_id";
                _nodeKindSource[node] = $"{stage1Name}.{node}_kind";
                _nodePropertySource[node] = $"{stage1Name}.{node}_properties";
            }
        }

        AppendProjectedStageRelationships(stage1Name, remainingReferenced, stage1SelectColumns);

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
        var hasAgg = with.Items.Any(i => HasAggregation(i.Expression));

        foreach (var item in with.Items)
        {
            if (item.Expression is WildcardExpression)
            {
                if (hasAgg)
                {
                    ProcessWildcardWithItem(with, groupByColumns);
                }

                continue;
            }

            if (item.Alias != null)
            {
                ProcessAliasedWithItem(item.Alias, item.Expression);
            }

            if (hasAgg)
            {
                if (HasAggregation(item.Expression)) continue;
                if (GetReferencedIdentifiers(item.Expression).Any(id => _aggregatedAliases.Contains(id))) continue;

                if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
                {
                    groupByColumns.Add(_nodeIdSource.TryGetValue(id.Name, out var idSrc) ? idSrc : $"{EscapeVar(id.Name)}.id");
                }
                else if (item.Expression is IdentifierExpression relId && _declaredRels.Contains(relId.Name))
                {
                    groupByColumns.Add(GetRelRowIdSql(relId.Name));
                }
                else
                {
                    groupByColumns.Add(VisitExpression(item.Expression));
                }
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

        foreach (var declaredRel in _declaredRels)
        {
            if (!_varLenRels.Contains(declaredRel) && !aggregatedVars.Contains(declaredRel))
            {
                groupByColumns.Add(GetRelRowIdSql(declaredRel));
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

        if (IsListExpression(expression))
        {
            _listValuedAliases.Add(alias);
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
        if (_aggregatedAliases.Count > 0 && with.Items.Any(i => HasAggregation(i.Expression)))
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
        foreach (var rel in _varLenRels) subCompiler._varLenRels.Add(rel);
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
                const string wildcardStar = "*";
                selectColumns.Add(wildcardStar);
                continue;
            }

            if (_rawNodeColumns && item.Expression is IdentifierExpression rawId && _declaredNodes.Contains(rawId.Name))
            {
                var esc = EscapeVar(rawId.Name);
                var idSrc = _nodeIdSource.GetValueOrDefault(rawId.Name, $"{esc}.id");
                var propSrc = _nodePropertySource.GetValueOrDefault(rawId.Name, $"{esc}.properties");
                selectColumns.Add($"{idSrc} AS id, {propSrc} AS properties");
                continue;
            }

            var exprSql = VisitExpression(item.Expression);
            var alias = !string.IsNullOrEmpty(item.Alias)
                ? item.Alias
                : FormatDefaultColumnAlias(item.Expression);

            selectColumns.Add(!string.IsNullOrEmpty(alias) ? $"{exprSql} AS {QuoteIdentifier(alias)}" : exprSql);
        }
        return selectColumns;
    }

    private static string? FormatDefaultColumnAlias(Expression expr)
    {
        const string distinctPrefix = "DISTINCT ";
        const string commaSep = ", ";
        return expr switch
        {
            IdentifierExpression id => id.Name,
            PropertyAccessExpression prop => $"{prop.Variable}.{prop.PropertyName}",
            FunctionCallExpression fn => fn.Arguments.Count == 0
                ? $"{fn.FunctionName}(*)"
                : $"{fn.FunctionName}({(fn.IsDistinct ? distinctPrefix : string.Empty)}{string.Join(commaSep, fn.Arguments.Select(static a => FormatDefaultColumnAlias(a) ?? "?"))})",
            ListSliceExpression ls => FormatDefaultColumnAlias(ls.List) is { } listAlias
                ? $"{listAlias}[{(ls.From != null ? FormatDefaultColumnAlias(ls.From) ?? string.Empty : string.Empty)}..{(ls.To != null ? FormatDefaultColumnAlias(ls.To) ?? string.Empty : string.Empty)}]"
                : null,
            HasLabelExpression lbl => $"{FormatDefaultColumnAlias(lbl.Expression) ?? "expr"}:{lbl.Label}",
            UnaryExpression u => FormatUnaryDefaultAlias(u),
            ParameterExpression p => $"${p.Name}",
            StringLiteralExpression s => $"'{s.Value}'",
            NumberLiteralExpression n => n.IsInteger
                ? ((long)n.Value).ToString(CultureInfo.InvariantCulture)
                : n.Value.ToString(CultureInfo.InvariantCulture),
            BooleanLiteralExpression b => b.Value ? "true" : "false",
            NullLiteralExpression => "null",
            _ => null
        };
    }

    private static string? FormatUnaryDefaultAlias(UnaryExpression unary)
    {
        var inner = FormatDefaultColumnAlias(unary.Operand);
        if (inner == null) return null;
        return unary.Operator switch
        {
            UnaryOperator.Not => $"NOT {inner}",
            UnaryOperator.IsNull => $"{inner} IS NULL",
            UnaryOperator.IsNotNull => $"{inner} IS NOT NULL",
            UnaryOperator.Minus => $"-{inner}",
            UnaryOperator.Plus => $"+{inner}",
            _ => null
        };
    }

    private string AssembleQuerySql(
        CypherQuery query,
        List<string> selectColumns,
        StringBuilder fromAndJoins,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        var lastWith = query.WithClauses?.LastOrDefault();
        var effectiveOrderBy = query.OrderBy ?? (query.WithClauses is { Count: 1 } ? lastWith?.OrderBy : null);
        List<string> orderItems = [];
        if (effectiveOrderBy is { Items.Count: > 0 })
        {
            orderItems =
            [
                .. effectiveOrderBy.Items.Select(item =>
                    $"{VisitExpression(item.Expression)} {(item.IsDescending ? "DESC" : "ASC")}")
            ];
        }

        var effectiveLimit = query.Limit ?? (query.WithClauses is { Count: 1 } ? lastWith?.Limit : null);
        var effectiveSkip = query.Skip ?? (query.WithClauses is { Count: 1 } ? lastWith?.Skip : null);
        string? limitVal = null;
        string? offsetVal = null;
        if (effectiveLimit != null || effectiveSkip != null)
        {
            limitVal = effectiveLimit != null ? VisitExpression(effectiveLimit.Expression) : "-1";
            if (effectiveSkip != null)
            {
                offsetVal = VisitExpression(effectiveSkip.Expression);
            }
        }

        var isDistinct = query.Return.IsDistinct || query.WithClauses?.LastOrDefault()?.IsDistinct == true;
        FlushLiftedRelCtesIfEligible();

        var model = new SqlQueryModel
        {
            Ctes = _ctes,
            IsDistinct = isDistinct,
            SelectColumns = selectColumns,
            FromAndJoins = fromAndJoins.ToString(),
            WhereConditions = whereConditions,
            GroupByColumns = groupByColumns,
            HavingConditions = havingConditions,
            OrderByItems = orderItems,
            Limit = limitVal,
            Offset = offsetVal,
        };

        return SqlTemplates.RenderQuery(model);
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

}
