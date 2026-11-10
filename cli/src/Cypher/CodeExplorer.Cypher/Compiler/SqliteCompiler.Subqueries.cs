using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
    private const string RecursiveRelCtePrefix = "cte_rel_";
    private const string CountFuncName = "count";
    private const string PropId = "id";
    private const string PropKind = "kind";
    private const string PropType = "type";
    private const string PropFrom = "from";
    private const string PropFromId = "from_id";
    private const string PropTo = "to";
    private const string PropToId = "to_id";
    private const string PropProperties = "properties";

    private readonly HashSet<string> _outerNodes = new(StringComparer.OrdinalIgnoreCase);
    private List<string>? _liftedRelCtes;
    private readonly Dictionary<string, string> _relRowIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _relKindSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _relFromIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _relToIdSource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _relPropertySource = new(StringComparer.OrdinalIgnoreCase);

    private string GetNodeRowIdSql(string varName) =>
        _nodeRowIdSource.TryGetValue(varName, out var src) ? src : $"{EscapeVar(varName)}.rowid";

    private string GetRelRowIdSql(string varName) =>
        _relRowIdSource.TryGetValue(varName, out var src) ? src : $"{EscapeVar(varName)}.rowid";

    private string? TryVisitStageRelIdentifier(string name)
    {
        if (!_relPropertySource.TryGetValue(name, out var propSrc))
        {
            return null;
        }

        var kindSrc = _relKindSource.TryGetValue(name, out var k) ? $"ce_rel_kind({k})" : "NULL";
        var fromSrc = _relFromIdSource.TryGetValue(name, out var f) ? $"(SELECT id FROM nodes WHERE rowid = {f})" : "NULL";
        var toSrc = _relToIdSource.TryGetValue(name, out var t) ? $"(SELECT id FROM nodes WHERE rowid = {t})" : "NULL";
        return $"json_object('type', {kindSrc}, 'from', {fromSrc}, 'to', {toSrc}, 'properties', json({propSrc}))";
    }

    private string? TryVisitStageRelPropertyAccess(string variable, string propName)
    {
        if (!_relPropertySource.TryGetValue(variable, out var propSrc))
        {
            return null;
        }

        if (propName.Equals(PropId, StringComparison.OrdinalIgnoreCase))
        {
            return _relRowIdSource[variable];
        }

        if (propName.Equals(PropKind, StringComparison.OrdinalIgnoreCase) ||
            propName.Equals(PropType, StringComparison.OrdinalIgnoreCase))
        {
            return $"ce_rel_kind({_relKindSource[variable]})";
        }

        if (propName.Equals(PropFrom, StringComparison.OrdinalIgnoreCase) ||
            propName.Equals(PropFromId, StringComparison.OrdinalIgnoreCase))
        {
            return $"(SELECT id FROM nodes WHERE rowid = {_relFromIdSource[variable]})";
        }

        if (propName.Equals(PropTo, StringComparison.OrdinalIgnoreCase) ||
            propName.Equals(PropToId, StringComparison.OrdinalIgnoreCase))
        {
            return $"(SELECT id FROM nodes WHERE rowid = {_relToIdSource[variable]})";
        }

        if (propName.Equals(PropProperties, StringComparison.OrdinalIgnoreCase))
        {
            return $"json({propSrc})";
        }

        return $"json_extract({propSrc}, '$.{propName}')";
    }

    private void AppendProjectedStageRelationships(
        string stageName,
        HashSet<string> remainingReferenced,
        List<string> stageSelectColumns)
    {
        foreach (var rel in _declaredRels)
        {
            if (_varLenRels.Contains(rel) || !remainingReferenced.Contains(rel))
            {
                continue;
            }

            var esc = EscapeVar(rel);
            var rowIdExpr = _relRowIdSource.GetValueOrDefault(rel, $"{esc}.rowid");
            var kindExpr = _relKindSource.GetValueOrDefault(rel, $"{esc}.kind");
            var fromExpr = _relFromIdSource.GetValueOrDefault(rel, $"{esc}.from_id");
            var toExpr = _relToIdSource.GetValueOrDefault(rel, $"{esc}.to_id");
            var propExpr = _relPropertySource.GetValueOrDefault(rel, $"{esc}.properties");

            stageSelectColumns.Add($"{rowIdExpr} AS {rel}_rowid");
            stageSelectColumns.Add($"{kindExpr} AS {rel}_kind");
            stageSelectColumns.Add($"{fromExpr} AS {rel}_from_id");
            stageSelectColumns.Add($"{toExpr} AS {rel}_to_id");
            stageSelectColumns.Add($"{propExpr} AS {rel}_properties");

            _relRowIdSource[rel] = $"{stageName}.{rel}_rowid";
            _relKindSource[rel] = $"{stageName}.{rel}_kind";
            _relFromIdSource[rel] = $"{stageName}.{rel}_from_id";
            _relToIdSource[rel] = $"{stageName}.{rel}_to_id";
            _relPropertySource[rel] = $"{stageName}.{rel}_properties";
        }
    }

    private string VisitExistsSubquery(ExistsSubqueryExpression expr)
    {
        var subQuery = expr.FullQuery ?? new CypherQuery(
            Matches: expr.Matches,
            Where: expr.Where,
            Return: new ReturnClause(false, [new ProjectionItem(new NumberLiteralExpression(1, true), null)]));
        var sql = CompileCorrelatedSubquery(subQuery);
        return $"(EXISTS ({sql}))";
    }

    private string VisitCountSubquery(CountSubqueryExpression expr)
    {
        if (expr.FullQuery != null)
        {
            var innerSql = CompileCorrelatedSubquery(expr.FullQuery);
            return $"(SELECT COUNT(*) FROM ({innerSql}))";
        }

        var subQuery = new CypherQuery(
            Matches: expr.Matches,
            Where: expr.Where,
            Return: new ReturnClause(false, [new ProjectionItem(new FunctionCallExpression(CountFuncName, false, [new WildcardExpression()]), null)]));
        var sql = CompileCorrelatedSubquery(subQuery);
        return $"COALESCE(({sql}), 0)";
    }

    private string CompileCorrelatedSubquery(CypherQuery subQuery)
    {
        List<string> liftedCtes = [];
        var subCompiler = CreateCorrelatedSubCompiler(subQuery, liftedCtes);
        var bodySql = subCompiler.VisitQuery(subQuery);

        foreach (var (k, v) in subCompiler._parameters)
        {
            _parameters[k] = v;
        }

        _paramIndex = subCompiler._paramIndex;
        _varIndex = subCompiler._varIndex;
        _cteIndex = subCompiler._cteIndex;

        if (liftedCtes.Count > 0)
        {
            _ctes.AddRange(liftedCtes);
        }

        return bodySql;
    }

    private void FlushLiftedRelCtesIfEligible()
    {
        if (_liftedRelCtes == null || _ctes.Count == 0)
        {
            return;
        }

        if (_ctes.All(static c => c.StartsWith(RecursiveRelCtePrefix, StringComparison.Ordinal)))
        {
            _liftedRelCtes.AddRange(_ctes);
            _ctes.Clear();
        }
    }

    private SqliteCompiler CreateCorrelatedSubCompiler(CypherQuery subQuery, List<string> liftedCtes)
    {
        var subCompiler = new SqliteCompiler(subQuery, null, maxPathTraversals: _maxPathTraversals)
        {
            _paramIndex = _paramIndex,
            _varIndex = _varIndex,
            _cteIndex = _cteIndex,
            _liftedRelCtes = liftedCtes
        };

        foreach (var (k, v) in _parameters) subCompiler._parameters[k] = v;
        foreach (var node in _declaredNodes)
        {
            subCompiler._declaredNodes.Add(node);
            subCompiler._outerNodes.Add(node);
        }
        foreach (var rel in _declaredRels) subCompiler._declaredRels.Add(rel);
        foreach (var rel in _varLenRels) subCompiler._varLenRels.Add(rel);
        foreach (var (k, v) in _pathVariables) subCompiler._pathVariables[k] = v;
        foreach (var (k, v) in _pathHops) subCompiler._pathHops[k] = [.. v];
        foreach (var (k, v) in _withAliases) subCompiler._withAliases[k] = v;
        foreach (var (k, v) in _nodePropertySource) subCompiler._nodePropertySource[k] = v;
        foreach (var (k, v) in _nodeIdSource) subCompiler._nodeIdSource[k] = v;
        foreach (var (k, v) in _nodeRowIdSource) subCompiler._nodeRowIdSource[k] = v;
        foreach (var (k, v) in _nodeKindSource) subCompiler._nodeKindSource[k] = v;
        foreach (var (k, v) in _relRowIdSource) subCompiler._relRowIdSource[k] = v;
        foreach (var (k, v) in _relKindSource) subCompiler._relKindSource[k] = v;
        foreach (var (k, v) in _relFromIdSource) subCompiler._relFromIdSource[k] = v;
        foreach (var (k, v) in _relToIdSource) subCompiler._relToIdSource[k] = v;
        foreach (var (k, v) in _relPropertySource) subCompiler._relPropertySource[k] = v;

        return subCompiler;
    }

    private static bool ContainsSubqueryOverAliases(Expression expr, HashSet<string> aliases)
    {
        if (aliases.Count == 0) return false;

        return expr switch
        {
            ListPredicateExpression lp => GetReferencedIdentifiers(lp).Overlaps(aliases),
            ListComprehensionExpression lc => GetReferencedIdentifiers(lc).Overlaps(aliases),
            ListSliceExpression ls => GetReferencedIdentifiers(ls).Overlaps(aliases),
            ReduceExpression red => GetReferencedIdentifiers(red).Overlaps(aliases),
            ExistsSubqueryExpression ex => GetReferencedIdentifiers(ex).Overlaps(aliases),
            CountSubqueryExpression cnt => GetReferencedIdentifiers(cnt).Overlaps(aliases),
            PatternExpression pat => GetReferencedIdentifiers(pat).Overlaps(aliases),
            PatternComprehensionExpression pc => GetReferencedIdentifiers(pc).Overlaps(aliases),
            BinaryExpression { Operator: BinaryOperator.In, Right: not ListExpression } binIn =>
                GetReferencedIdentifiers(binIn.Right).Overlaps(aliases) || ContainsSubqueryOverAliases(binIn.Left, aliases),
            BinaryExpression bin =>
                ContainsSubqueryOverAliases(bin.Left, aliases) || ContainsSubqueryOverAliases(bin.Right, aliases),
            UnaryExpression u => ContainsSubqueryOverAliases(u.Operand, aliases),
            FunctionCallExpression fn => fn.Arguments.Any(a => ContainsSubqueryOverAliases(a, aliases)),
            CaseExpression c =>
                (c.TestExpression != null && ContainsSubqueryOverAliases(c.TestExpression, aliases)) ||
                c.WhenBranches.Any(w => ContainsSubqueryOverAliases(w.When, aliases) || ContainsSubqueryOverAliases(w.Then, aliases)) ||
                (c.ElseExpression != null && ContainsSubqueryOverAliases(c.ElseExpression, aliases)),
            _ => false
        };
    }

    private void MaterializeCurrentWithAggregationStage(
        CypherQuery query,
        int withIndex,
        StringBuilder fromAndJoins,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        var stageName = $"_stage_{_ctes.Count + 1}";
        var remainingReferenced = CollectRemainingReferencedIdentifiers(query, withIndex);
        var stageSelectColumns = BuildStage1SelectColumns(stageName, remainingReferenced);
        var with = query.WithClauses![withIndex];
        var stageDistinct = with.IsDistinct ? "DISTINCT " : string.Empty;
        var stageWhere = whereConditions.Count > 0 ? $"\nWHERE {string.Join(" AND ", whereConditions)}" : string.Empty;
        var stageGroupBy = groupByColumns.Count > 0 ? $"\nGROUP BY {string.Join(", ", groupByColumns.Distinct())}" : string.Empty;
        var stageHaving = havingConditions.Count > 0 ? $"\nHAVING {string.Join(" AND ", havingConditions)}" : string.Empty;

        var stageCte = $"{stageName} AS (\nSELECT {stageDistinct}{string.Join(", ", stageSelectColumns)}\n{fromAndJoins}{stageWhere}{stageGroupBy}{stageHaving}\n)";
        _ctes.Add(stageCte);

        RemapWithAliasesToStage(stageName, remainingReferenced);

        fromAndJoins.Clear();
        fromAndJoins.Append($"FROM {stageName}");
        whereConditions.Clear();
        groupByColumns.Clear();
        havingConditions.Clear();
        _aggregatedAliases.Clear();
    }

    private static void CollectSubqueryIdentifiers(Expression expr, HashSet<string> set)
    {
        switch (expr)
        {
            case ExistsSubqueryExpression ex:
                CollectQueryClausesIdentifiers(ex.FullQuery?.Matches ?? ex.Matches, ex.FullQuery?.Where ?? ex.Where, ex.FullQuery, set);
                break;
            case CountSubqueryExpression cnt:
                CollectQueryClausesIdentifiers(cnt.FullQuery?.Matches ?? cnt.Matches, cnt.FullQuery?.Where ?? cnt.Where, cnt.FullQuery, set);
                break;
        }
    }

    private static void CollectQueryClausesIdentifiers(
        List<MatchClause> matches,
        WhereClause? whereClause,
        CypherQuery? fullQuery,
        HashSet<string> set)
    {
        foreach (var match in matches)
        {
            foreach (var path in match.Paths)
            {
                if (path.Head.Variable != null) set.Add(path.Head.Variable);
                foreach (var step in path.Chain)
                {
                    if (step.Relationship.Variable != null) set.Add(step.Relationship.Variable);
                    if (step.Target.Variable != null) set.Add(step.Target.Variable);
                }
            }

            if (match.Where != null)
            {
                CollectIdentifiers(match.Where.Predicate, set);
            }
        }

        if (whereClause != null)
        {
            CollectIdentifiers(whereClause.Predicate, set);
        }

        if (fullQuery?.Return != null)
        {
            foreach (var item in fullQuery.Return.Items)
            {
                CollectIdentifiers(item.Expression, set);
            }
        }
    }

    private bool ContainsSubqueryOverReturnClauses(CypherQuery query, HashSet<string> aliases)
    {
        if (aliases.Count == 0) return false;
        if (query.Where != null && ContainsSubqueryOverAliases(query.Where.Predicate, aliases))
        {
            return true;
        }

        if (query.Return.Items.Any(i =>
                i.Expression is not ListComprehensionExpression { List: IdentifierExpression lid } || !_withCollectExpressions.ContainsKey(lid.Name)
                    ? ContainsSubqueryOverAliases(i.Expression, aliases)
                    : false))
        {
            return true;
        }

        return query.OrderBy != null &&
               query.OrderBy.Items.Any(o => ContainsSubqueryOverAliases(o.Expression, aliases));
    }

    private CypherQuery MaterializeReturnSubqueryAggregationsIfNeeded(
        CypherQuery query,
        StringBuilder fromAndJoins,
        List<string> whereConditions,
        List<string> groupByColumns,
        List<string> havingConditions)
    {
        var needsStage = query.Return.Items.Any(static i => ContainsAggregationInsideJsonSubquery(i.Expression)) ||
                         (query.OrderBy?.Items.Any(static o => ContainsAggregationInsideJsonSubquery(o.Expression)) ?? false);
        if (!needsStage)
        {
            return query;
        }

        var stageGroupingKeys = BuildReturnStageGroupingKeys(query.Return, groupByColumns);
        var extractedAggSqls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var rewrittenReturnItems = query.Return.Items
            .Select(item =>
            {
                var preservedAlias = !string.IsNullOrEmpty(item.Alias)
                    ? item.Alias
                    : FormatDefaultColumnAlias(item.Expression);
                var rewrittenExpr = ExtractDirectAggregations(item.Expression, extractedAggSqls);
                return new ProjectionItem(rewrittenExpr, preservedAlias);
            })
            .ToList();

        OrderByClause? rewrittenOrderBy = null;
        if (query.OrderBy != null)
        {
            var rewrittenOrderItems = query.OrderBy.Items
                .Select(o => new OrderByItem(ExtractDirectAggregations(o.Expression, extractedAggSqls), o.IsDescending))
                .ToList();
            rewrittenOrderBy = new OrderByClause(rewrittenOrderItems);
        }

        if (extractedAggSqls.Count == 0)
        {
            return query;
        }

        var remainingReferenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in rewrittenReturnItems)
        {
            remainingReferenced.UnionWith(GetReferencedIdentifiers(item.Expression));
        }

        if (rewrittenOrderBy != null)
        {
            foreach (var ord in rewrittenOrderBy.Items)
            {
                remainingReferenced.UnionWith(GetReferencedIdentifiers(ord.Expression));
            }
        }

        remainingReferenced.ExceptWith(extractedAggSqls.Keys);

        var stageName = $"_stage_{_ctes.Count + 1}";
        var stageSelectColumns = BuildStage1SelectColumns(stageName, remainingReferenced);
        foreach (var uw in _unwindVariables.ToList())
        {
            if (remainingReferenced.Contains(uw))
            {
                stageSelectColumns.Add($"{EscapeVar(uw)}.value AS {QuoteIdentifier(uw)}");
                _withAliases[uw] = $"{stageName}.{QuoteIdentifier(uw)}";
                _unwindVariables.Remove(uw);
            }
        }

        foreach (var (aggAlias, aggSql) in extractedAggSqls)
        {
            stageSelectColumns.Add($"{aggSql} AS {QuoteIdentifier(aggAlias)}");
        }

        var stageWhere = whereConditions.Count > 0 ? $"\nWHERE {string.Join(" AND ", whereConditions)}" : string.Empty;
        var stageGroupBy = stageGroupingKeys.Count > 0 ? $"\nGROUP BY {string.Join(", ", stageGroupingKeys.Distinct())}" : string.Empty;
        var stageHaving = havingConditions.Count > 0 ? $"\nHAVING {string.Join(" AND ", havingConditions)}" : string.Empty;

        var stageCte = $"{stageName} AS (\nSELECT {string.Join(", ", stageSelectColumns)}\n{fromAndJoins}{stageWhere}{stageGroupBy}{stageHaving}\n)";
        _ctes.Add(stageCte);

        RemapWithAliasesToStage(stageName, remainingReferenced);
        foreach (var aggAlias in extractedAggSqls.Keys)
        {
            _withAliases[aggAlias] = $"{stageName}.{QuoteIdentifier(aggAlias)}";
        }

        fromAndJoins.Clear();
        fromAndJoins.Append($"FROM {stageName}");
        whereConditions.Clear();
        groupByColumns.Clear();
        havingConditions.Clear();
        _aggregatedAliases.Clear();

        return query with
        {
            Return = new ReturnClause(query.Return.IsDistinct, rewrittenReturnItems),
            OrderBy = rewrittenOrderBy
        };
    }

    private List<string> BuildReturnStageGroupingKeys(ReturnClause returnClause, List<string> currentGroupByColumns)
    {
        if (currentGroupByColumns.Count > 0)
        {
            return [.. currentGroupByColumns];
        }

        List<string> keys = [];
        foreach (var item in returnClause.Items)
        {
            if (HasAggregation(item.Expression) || item.Expression is WildcardExpression) continue;
            if (GetReferencedIdentifiers(item.Expression).Overlaps(_aggregatedAliases)) continue;

            if (item.Expression is IdentifierExpression id && _declaredNodes.Contains(id.Name))
            {
                keys.Add(_nodeIdSource.GetValueOrDefault(id.Name, $"{EscapeVar(id.Name)}.id"));
            }
            else if (item.Expression is IdentifierExpression relId && _declaredRels.Contains(relId.Name))
            {
                keys.Add(GetRelRowIdSql(relId.Name));
            }
            else
            {
                keys.Add(VisitExpression(item.Expression));
            }
        }

        return keys;
    }

    private static bool ContainsAggregationInsideJsonSubquery(Expression expr) => expr switch
    {
        ListSliceExpression ls => HasAggregation(ls.List) ||
                                  (ls.From != null && HasAggregation(ls.From)) ||
                                  (ls.To != null && HasAggregation(ls.To)),
        ListComprehensionExpression lc => HasAggregation(lc.List) ||
                                          (lc.Filter != null && HasAggregation(lc.Filter)) ||
                                          (lc.Projection != null && HasAggregation(lc.Projection)),
        ListPredicateExpression lp => HasAggregation(lp.List) || HasAggregation(lp.Predicate),
        ReduceExpression red => HasAggregation(red.Initial) || HasAggregation(red.List) || HasAggregation(red.Expression),
        BinaryExpression { Operator: BinaryOperator.In, Right: not ListExpression } binIn =>
            HasAggregation(binIn.Right) || HasAggregation(binIn.Left),
        BinaryExpression bin =>
            ContainsAggregationInsideJsonSubquery(bin.Left) || ContainsAggregationInsideJsonSubquery(bin.Right),
        UnaryExpression u => ContainsAggregationInsideJsonSubquery(u.Operand),
        FunctionCallExpression fn => fn.Arguments.Any(ContainsAggregationInsideJsonSubquery),
        ListExpression l => l.Items.Any(ContainsAggregationInsideJsonSubquery),
        MapLiteralExpression m => m.Properties.Values.Any(ContainsAggregationInsideJsonSubquery),
        MapProjectionExpression mp => ContainsAggregationInsideJsonSubquery(mp.BaseExpression) ||
                                      mp.Elements.Any(static e => e.ValueExpression != null && ContainsAggregationInsideJsonSubquery(e.ValueExpression)),
        CaseExpression c => (c.TestExpression != null && ContainsAggregationInsideJsonSubquery(c.TestExpression)) ||
                            c.WhenBranches.Any(static w => ContainsAggregationInsideJsonSubquery(w.When) || ContainsAggregationInsideJsonSubquery(w.Then)) ||
                            (c.ElseExpression != null && ContainsAggregationInsideJsonSubquery(c.ElseExpression)),
        HasLabelExpression hl => ContainsAggregationInsideJsonSubquery(hl.Expression),
        _ => false
    };

    private Expression ExtractDirectAggregations(Expression expr, Dictionary<string, string> extractedAggSqls)
    {
        switch (expr)
        {
            case FunctionCallExpression fn when AggregateFunctionNames.Contains(fn.FunctionName):
            {
                var aggAlias = $"_ret_agg_{extractedAggSqls.Count}";
                extractedAggSqls[aggAlias] = VisitExpression(fn);
                return new IdentifierExpression(aggAlias);
            }
            case FunctionCallExpression fn:
                return fn with { Arguments = fn.Arguments.Select(a => ExtractDirectAggregations(a, extractedAggSqls)).ToList() };
            case BinaryExpression bin:
                return bin with
                {
                    Left = ExtractDirectAggregations(bin.Left, extractedAggSqls),
                    Right = ExtractDirectAggregations(bin.Right, extractedAggSqls)
                };
            case UnaryExpression u:
                return u with { Operand = ExtractDirectAggregations(u.Operand, extractedAggSqls) };
            case ListSliceExpression ls:
                return ls with
                {
                    List = ExtractDirectAggregations(ls.List, extractedAggSqls),
                    From = ls.From != null ? ExtractDirectAggregations(ls.From, extractedAggSqls) : null,
                    To = ls.To != null ? ExtractDirectAggregations(ls.To, extractedAggSqls) : null
                };
            case ListExpression l:
                return l with { Items = l.Items.Select(i => ExtractDirectAggregations(i, extractedAggSqls)).ToList() };
            case MapLiteralExpression m:
                return m with
                {
                    Properties = m.Properties.ToDictionary(
                        kv => kv.Key,
                        kv => ExtractDirectAggregations(kv.Value, extractedAggSqls),
                        m.Properties.Comparer)
                };
            case MapProjectionExpression mp:
                return mp with
                {
                    BaseExpression = ExtractDirectAggregations(mp.BaseExpression, extractedAggSqls),
                    Elements = mp.Elements.Select(e => e with
                    {
                        ValueExpression = e.ValueExpression != null ? ExtractDirectAggregations(e.ValueExpression, extractedAggSqls) : null
                    }).ToList()
                };
            case CaseExpression c:
                return c with
                {
                    TestExpression = c.TestExpression != null ? ExtractDirectAggregations(c.TestExpression, extractedAggSqls) : null,
                    WhenBranches = c.WhenBranches.Select(w => new CaseWhenItem(
                        ExtractDirectAggregations(w.When, extractedAggSqls),
                        ExtractDirectAggregations(w.Then, extractedAggSqls))).ToList(),
                    ElseExpression = c.ElseExpression != null ? ExtractDirectAggregations(c.ElseExpression, extractedAggSqls) : null
                };
            case ListComprehensionExpression lc:
                return lc with
                {
                    List = ExtractDirectAggregations(lc.List, extractedAggSqls),
                    Filter = lc.Filter != null ? ExtractDirectAggregations(lc.Filter, extractedAggSqls) : null,
                    Projection = lc.Projection != null ? ExtractDirectAggregations(lc.Projection, extractedAggSqls) : null
                };
            case ListPredicateExpression lp:
                return lp with
                {
                    List = ExtractDirectAggregations(lp.List, extractedAggSqls),
                    Predicate = ExtractDirectAggregations(lp.Predicate, extractedAggSqls)
                };
            case ReduceExpression red:
                return red with
                {
                    Initial = ExtractDirectAggregations(red.Initial, extractedAggSqls),
                    List = ExtractDirectAggregations(red.List, extractedAggSqls),
                    Expression = ExtractDirectAggregations(red.Expression, extractedAggSqls)
                };
            case HasLabelExpression hl:
                return hl with { Expression = ExtractDirectAggregations(hl.Expression, extractedAggSqls) };
            default:
                return expr;
        }
    }
}
