using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
    private const string CollectFuncName = "collect";

    private sealed class DecomposedOptionalBranch
    {
        public required MatchClause Match { get; init; }
        public required PathPattern Path { get; init; }
        public required HashSet<string> IntroducedVars { get; init; }
        public bool IsCompiled { get; set; }
    }

    private readonly Dictionary<string, DecomposedOptionalBranch> _decomposedBranches =
        new(StringComparer.OrdinalIgnoreCase);

    private void DetectDecomposedOptionalMatches(CypherQuery query)
    {
        if (query.Matches.Count(static m => m.IsOptional) < 2 || !HasQueryAggregation(query))
        {
            return;
        }

        var declaredSoFar = CollectRequiredDeclaredVars(query);
        List<DecomposedOptionalBranch> candidateBranches = [];
        var undecomposedOptionalVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in query.Matches.Where(static m => m.IsOptional))
        {
            var candidate = TryCreateCandidateOptionalBranch(query, match, declaredSoFar);
            if (candidate != null)
            {
                candidateBranches.Add(candidate);
            }
            else
            {
                CollectMatchIntroducedVars(match, declaredSoFar, undecomposedOptionalVars);
            }
        }

        FilterCrossBranchAggregatedCandidates(query, candidateBranches, undecomposedOptionalVars);

        if (candidateBranches.Count >= 1)
        {
            foreach (var branch in candidateBranches)
            {
                foreach (var v in branch.IntroducedVars)
                {
                    _decomposedBranches[v] = branch;
                }
            }
        }
    }

    private static bool HasQueryAggregation(CypherQuery query) =>
        query.Return.Items.Any(static i => HasAggregation(i.Expression)) ||
        (query.WithClauses?.Any(static w => w.Items.Any(static i => HasAggregation(i.Expression))) ?? false);

    private static HashSet<string> CollectRequiredDeclaredVars(CypherQuery query)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in query.Matches.Where(static m => !m.IsOptional))
        {
            foreach (var path in match.Paths)
            {
                if (path.Head.Variable != null) declared.Add(path.Head.Variable);
                foreach (var elem in path.Chain)
                {
                    if (elem.Relationship.Variable != null) declared.Add(elem.Relationship.Variable);
                    if (elem.Target.Variable != null) declared.Add(elem.Target.Variable);
                }
            }
        }

        return declared;
    }

    private static void CollectMatchIntroducedVars(
        MatchClause match,
        HashSet<string> declaredSoFar,
        HashSet<string> target)
    {
        foreach (var path in match.Paths)
        {
            if (path.PathVariable != null && !declaredSoFar.Contains(path.PathVariable))
            {
                target.Add(path.PathVariable);
            }

            if (path.Head.Variable != null && !declaredSoFar.Contains(path.Head.Variable))
            {
                target.Add(path.Head.Variable);
            }

            foreach (var elem in path.Chain)
            {
                if (elem.Relationship.Variable != null && !declaredSoFar.Contains(elem.Relationship.Variable))
                {
                    target.Add(elem.Relationship.Variable);
                }

                if (elem.Target.Variable != null && !declaredSoFar.Contains(elem.Target.Variable))
                {
                    target.Add(elem.Target.Variable);
                }
            }
        }
    }

    private static DecomposedOptionalBranch? TryCreateCandidateOptionalBranch(
        CypherQuery query,
        MatchClause match,
        HashSet<string> declaredSoFar)
    {
        if (match.Paths.Count != 1) return null;
        var path = match.Paths[0];
        if (path.Chain.Count == 0 ||
            path.PathVariable != null ||
            path.IsShortestPath ||
            path.IsAllShortestPaths ||
            path.Chain.Any(static e => e.Relationship.Range.HasValue))
        {
            return null;
        }

        var anchorCount = 0;
        var introduced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (path.Head.Variable != null)
        {
            if (declaredSoFar.Contains(path.Head.Variable)) anchorCount++;
            else introduced.Add(path.Head.Variable);
        }

        foreach (var elem in path.Chain)
        {
            if (elem.Relationship.Variable != null)
            {
                if (declaredSoFar.Contains(elem.Relationship.Variable)) return null;
                introduced.Add(elem.Relationship.Variable);
            }

            if (elem.Target.Variable != null)
            {
                if (declaredSoFar.Contains(elem.Target.Variable)) anchorCount++;
                else introduced.Add(elem.Target.Variable);
            }
        }

        if (anchorCount == 0 || introduced.Count == 0) return null;
        if (match.Where != null &&
            GetReferencedIdentifiers(match.Where.Predicate).Any(id => !declaredSoFar.Contains(id) && !introduced.Contains(id)))
        {
            return null;
        }

        var referencedAnywhere = introduced.Any(v => IsVariableReferencedInQuery(query, match, v));
        var allAggregated = introduced.All(v => !HasVariableOutsideAggregation(query, match, v));
        if (!referencedAnywhere || !allAggregated) return null;

        return new DecomposedOptionalBranch
        {
            Match = match,
            Path = path,
            IntroducedVars = introduced
        };
    }

    private static void FilterCrossBranchAggregatedCandidates(
        CypherQuery query,
        List<DecomposedOptionalBranch> candidates,
        HashSet<string> undecomposedOptionalVars)
    {
        if (candidates.Count == 0) return;

        List<FunctionCallExpression> aggCalls = [];
        if (query.WithClauses != null)
        {
            foreach (var with in query.WithClauses)
            {
                foreach (var item in with.Items) CollectAggregationFunctions(item.Expression, aggCalls);
            }
        }

        foreach (var item in query.Return.Items) CollectAggregationFunctions(item.Expression, aggCalls);
        if (query.OrderBy != null)
        {
            foreach (var item in query.OrderBy.Items) CollectAggregationFunctions(item.Expression, aggCalls);
        }

        candidates.RemoveAll(branch => aggCalls.Any(fn =>
        {
            var refs = GetReferencedIdentifiers(fn);
            if (!refs.Overlaps(branch.IntroducedVars)) return false;
            return refs.Overlaps(undecomposedOptionalVars) ||
                   candidates.Any(other => !ReferenceEquals(other, branch) && refs.Overlaps(other.IntroducedVars));
        }));
    }

    private static void CollectAggregationFunctions(Expression expr, List<FunctionCallExpression> target)
    {
        switch (expr)
        {
            case FunctionCallExpression fn:
                if (AggregateFunctionNames.Contains(fn.FunctionName)) target.Add(fn);
                foreach (var arg in fn.Arguments) CollectAggregationFunctions(arg, target);
                break;
            case BinaryExpression b:
                CollectAggregationFunctions(b.Left, target);
                CollectAggregationFunctions(b.Right, target);
                break;
            case UnaryExpression u:
                CollectAggregationFunctions(u.Operand, target);
                break;
            case ListSliceExpression ls:
                CollectAggregationFunctions(ls.List, target);
                if (ls.From != null) CollectAggregationFunctions(ls.From, target);
                if (ls.To != null) CollectAggregationFunctions(ls.To, target);
                break;
            case CaseExpression c:
                if (c.TestExpression != null) CollectAggregationFunctions(c.TestExpression, target);
                foreach (var w in c.WhenBranches)
                {
                    CollectAggregationFunctions(w.When, target);
                    CollectAggregationFunctions(w.Then, target);
                }

                if (c.ElseExpression != null) CollectAggregationFunctions(c.ElseExpression, target);
                break;
            case ListComprehensionExpression lc:
                CollectAggregationFunctions(lc.List, target);
                if (lc.Filter != null) CollectAggregationFunctions(lc.Filter, target);
                if (lc.Projection != null) CollectAggregationFunctions(lc.Projection, target);
                break;
            case ListPredicateExpression lp:
                CollectAggregationFunctions(lp.List, target);
                CollectAggregationFunctions(lp.Predicate, target);
                break;
            case ReduceExpression red:
                CollectAggregationFunctions(red.Initial, target);
                CollectAggregationFunctions(red.List, target);
                CollectAggregationFunctions(red.Expression, target);
                break;
        }
    }

    private static bool IsVariableReferencedInQuery(CypherQuery query, MatchClause currentMatch, string varName)
    {
        if (query.Where != null && HasVariable(query.Where.Predicate, varName)) return true;
        if (query.WithClauses != null && query.WithClauses.Any(w =>
                w.Items.Any(i => HasVariable(i.Expression, varName)) ||
                (w.Where != null && HasVariable(w.Where.Predicate, varName))))
        {
            return true;
        }

        if (query.Return.Items.Any(i => HasVariable(i.Expression, varName))) return true;
        if (query.OrderBy != null && query.OrderBy.Items.Any(i => HasVariable(i.Expression, varName))) return true;

        return query.Matches.Any(m => !ReferenceEquals(m, currentMatch) &&
                                      m.Where != null &&
                                      HasVariable(m.Where.Predicate, varName));
    }

    private static bool HasVariableOutsideAggregation(CypherQuery query, MatchClause currentMatch, string varName)
    {
        // 1. Check all other MATCH clauses
        foreach (var match in query.Matches)
        {
            if (ReferenceEquals(match, currentMatch)) continue;

            if (match.Where != null && HasVariable(match.Where.Predicate, varName))
                return true;

            foreach (var p in match.Paths)
            {
                if (p.Head.Variable != null && p.Head.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase))
                    return true;
                foreach (var elem in p.Chain)
                {
                    if (elem.Relationship.Variable != null && elem.Relationship.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (elem.Target.Variable != null && elem.Target.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        // 2. Check main WHERE clause
        if (query.Where != null && HasVariableOutsideAggregation(query.Where.Predicate, varName, false))
            return true;

        // 3. Check WITH clauses and track aliases that carry varName
        var carriedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (query.WithClauses != null)
        {
            foreach (var with in query.WithClauses)
            {
                if (with.Where != null)
                {
                    if (HasVariableOutsideAggregation(with.Where.Predicate, varName, false))
                        return true;
                    if (carriedAliases.Any(alias => HasVariable(with.Where.Predicate, alias)))
                        return true;
                }

                foreach (var item in with.Items)
                {
                    if (HasVariableOutsideAggregation(item.Expression, varName, false))
                        return true;

                    if (carriedAliases.Any(alias => HasVariable(item.Expression, alias)))
                        return true;

                    if (item.Alias != null && HasVariable(item.Expression, varName))
                    {
                        carriedAliases.Add(item.Alias);
                    }
                }
            }
        }

        // If an alias carried this variable, and we have subsequent operations using that alias
        if (carriedAliases.Count > 0)
        {
            // Any carried alias used in Return or OrderBy
            foreach (var alias in carriedAliases)
            {
                foreach (var item in query.Return.Items)
                {
                    if (HasVariableOutsideAggregation(item.Expression, alias, false))
                        return true;
                }
            }
        }

        // 4. Check RETURN clause
        foreach (var item in query.Return.Items)
        {
            if (HasVariableOutsideAggregation(item.Expression, varName, false))
                return true;
        }

        // 5. Check ORDER BY clause
        if (query.OrderBy != null)
        {
            foreach (var item in query.OrderBy.Items)
            {
                if (HasVariableOutsideAggregation(item.Expression, varName, false))
                    return true;
            }
        }

        return false;
    }

    private static bool HasVariable(Expression expr, string varName) =>
        GetReferencedIdentifiers(expr).Contains(varName);

    private static bool HasVariableOutsideAggregation(Expression expr, string varName, bool insideAgg)
    {
        switch (expr)
        {
            case FunctionCallExpression fn when fn.FunctionName.Equals(CollectFuncName, StringComparison.OrdinalIgnoreCase) ||
                                                fn.FunctionName.Equals(CountFuncName, StringComparison.OrdinalIgnoreCase):
                return fn.Arguments.Any(arg => HasVariableOutsideAggregation(arg, varName, true));

            case IdentifierExpression id when id.Name.Equals(varName, StringComparison.OrdinalIgnoreCase):
                return !insideAgg;

            case PropertyAccessExpression prop when prop.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase):
                return !insideAgg;

            case BinaryExpression b:
                return HasVariableOutsideAggregation(b.Left, varName, insideAgg) ||
                       HasVariableOutsideAggregation(b.Right, varName, insideAgg);

            case UnaryExpression u:
                return HasVariableOutsideAggregation(u.Operand, varName, insideAgg);

            case ListSliceExpression ls:
                return HasVariableOutsideAggregation(ls.List, varName, insideAgg) ||
                       (ls.From != null && HasVariableOutsideAggregation(ls.From, varName, insideAgg)) ||
                       (ls.To != null && HasVariableOutsideAggregation(ls.To, varName, insideAgg));

            case ListExpression l:
                return l.Items.Any(i => HasVariableOutsideAggregation(i, varName, insideAgg));

            case FunctionCallExpression fn:
                return fn.Arguments.Any(arg => HasVariableOutsideAggregation(arg, varName, insideAgg));

            case MapLiteralExpression m:
                return m.Properties.Values.Any(v => HasVariableOutsideAggregation(v, varName, insideAgg));

            case MapProjectionExpression mp:
                return HasVariableOutsideAggregation(mp.BaseExpression, varName, insideAgg) ||
                       mp.Elements.Any(e => e.ValueExpression != null && HasVariableOutsideAggregation(e.ValueExpression, varName, insideAgg));

            case HasLabelExpression hl:
                return HasVariableOutsideAggregation(hl.Expression, varName, insideAgg);

            case CaseExpression c:
                return (c.TestExpression != null && HasVariableOutsideAggregation(c.TestExpression, varName, insideAgg)) ||
                       c.WhenBranches.Any(w => HasVariableOutsideAggregation(w.When, varName, insideAgg) || HasVariableOutsideAggregation(w.Then, varName, insideAgg)) ||
                       (c.ElseExpression != null && HasVariableOutsideAggregation(c.ElseExpression, varName, insideAgg));

            case ListComprehensionExpression comp:
                return HasVariableOutsideAggregation(comp.List, varName, insideAgg) ||
                       (comp.Filter != null && HasVariableOutsideAggregation(comp.Filter, varName, insideAgg)) ||
                       (comp.Projection != null && HasVariableOutsideAggregation(comp.Projection, varName, insideAgg));

            case ListPredicateExpression pred:
                return HasVariableOutsideAggregation(pred.List, varName, insideAgg) ||
                       HasVariableOutsideAggregation(pred.Predicate, varName, insideAgg);

            case ReduceExpression red:
                return HasVariableOutsideAggregation(red.Initial, varName, insideAgg) ||
                       HasVariableOutsideAggregation(red.List, varName, insideAgg) ||
                       HasVariableOutsideAggregation(red.Expression, varName, insideAgg);

            default:
                return GetReferencedIdentifiers(expr).Contains(varName);
        }
    }

    private string? TryCompileDecomposedAggregation(FunctionCallExpression func, string distinctStr)
    {
        _ = distinctStr;
        if (func.Arguments.Count != 1) return null;

        var referenced = GetReferencedIdentifiers(func.Arguments[0]);
        var matchingBranches = referenced
            .Select(id => _decomposedBranches.GetValueOrDefault(id))
            .Where(static b => b != null)
            .Distinct()
            .ToList();

        if (matchingBranches.Count != 1) return null;
        var branch = matchingBranches[0]!;

        // Build subquery path for this branch
        var (fromJoins, conditions) = BuildSubqueryPath(branch.Path, "_dec");

        // Temporarily register branch variables so VisitExpression resolves them correctly
        List<string> addedNodes = [];
        List<string> addedRels = [];

        if (branch.Path.Head.Variable != null && _declaredNodes.Add(branch.Path.Head.Variable))
            addedNodes.Add(branch.Path.Head.Variable);

        foreach (var elem in branch.Path.Chain)
        {
            if (elem.Relationship.Variable != null && _declaredRels.Add(elem.Relationship.Variable))
                addedRels.Add(elem.Relationship.Variable);
            if (elem.Target.Variable != null && _declaredNodes.Add(elem.Target.Variable))
                addedNodes.Add(elem.Target.Variable);
        }

        string projSql;
        try
        {
            if (branch.Match.Where != null)
            {
                conditions.Add(VisitExpression(branch.Match.Where.Predicate));
            }

            projSql = VisitExpression(func.Arguments[0]);
        }
        finally
        {
            foreach (var n in addedNodes) _declaredNodes.Remove(n);
            foreach (var r in addedRels) _declaredRels.Remove(r);
        }

        var whereSql = conditions.Count > 0 ? string.Join(" AND ", conditions) : null;
        branch.IsCompiled = true;

        if (func.FunctionName.Equals(CollectFuncName, StringComparison.OrdinalIgnoreCase))
        {
            return SqlTemplates.RenderCollectSubquery(func.IsDistinct, projSql, fromJoins.ToString(), whereSql);
        }

        if (func.FunctionName.Equals(CountFuncName, StringComparison.OrdinalIgnoreCase))
        {
            const string wildcardStar = "*";
            var countTarget = func.Arguments[0] switch
            {
                WildcardExpression => wildcardStar,
                IdentifierExpression id when addedNodes.Contains(id.Name, StringComparer.OrdinalIgnoreCase) => $"{EscapeVar(id.Name)}.id",
                IdentifierExpression relId when addedRels.Contains(relId.Name, StringComparer.OrdinalIgnoreCase) => $"{EscapeVar(relId.Name)}.rowid",
                _ => projSql
            };
            return SqlTemplates.RenderCountSubquery(
                func.IsDistinct && func.Arguments[0] is not WildcardExpression,
                countTarget,
                fromJoins.ToString(),
                whereSql);
        }

        return null;
    }
}
