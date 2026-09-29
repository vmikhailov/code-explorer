using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
    private sealed class DecomposedOptionalBranch
    {
        public required MatchClause Match { get; init; }
        public required PathPattern Path { get; init; }
        public required string RootVar { get; init; }
        public required HashSet<string> IntroducedVars { get; init; }
        public bool IsCompiled { get; set; }
    }

    private readonly Dictionary<string, DecomposedOptionalBranch> _decomposedBranches =
        new(StringComparer.OrdinalIgnoreCase);

    private void DetectDecomposedOptionalMatches(CypherQuery query)
    {
        if (query.Matches.Count(m => m.IsOptional) < 2) return;

        var declaredSoFar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in query.Matches.Where(m => !m.IsOptional))
        {
            foreach (var path in match.Paths)
            {
                if (path.Head.Variable != null) declaredSoFar.Add(path.Head.Variable);
                foreach (var elem in path.Chain)
                {
                    if (elem.Relationship.Variable != null) declaredSoFar.Add(elem.Relationship.Variable);
                    if (elem.Target.Variable != null) declaredSoFar.Add(elem.Target.Variable);
                }
            }
        }

        var candidateBranches = new List<DecomposedOptionalBranch>();
        foreach (var match in query.Matches.Where(m => m.IsOptional))
        {
            if (match.Paths.Count != 1) continue;
            var path = match.Paths[0];
            var headVar = path.Head.Variable;
            if (headVar == null || !declaredSoFar.Contains(headVar) || path.Chain.Count == 0) continue;

            var introduced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var referencesOtherOptional = false;

            foreach (var elem in path.Chain)
            {
                if (elem.Relationship.Variable != null)
                {
                    if (declaredSoFar.Contains(elem.Relationship.Variable))
                        referencesOtherOptional = true;
                    else
                        introduced.Add(elem.Relationship.Variable);
                }

                if (elem.Target.Variable != null)
                {
                    if (declaredSoFar.Contains(elem.Target.Variable))
                        referencesOtherOptional = true;
                    else
                        introduced.Add(elem.Target.Variable);
                }
            }

            if (referencesOtherOptional || introduced.Count == 0) continue;

            // Check that all introduced variables are ONLY used inside aggregations (collect, count)
            // and never used in subsequent MATCH clauses, list comprehensions, or non-agg expressions
            var allAggregated = introduced.All(v =>
                !HasVariableOutsideAggregation(query, match, v));

            if (allAggregated)
            {
                candidateBranches.Add(new DecomposedOptionalBranch
                {
                    Match = match,
                    Path = path,
                    RootVar = headVar,
                    IntroducedVars = introduced
                });
            }
        }

        // Only decompose if we have 2 or more independent optional branches
        if (candidateBranches.Count >= 2)
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

    private static bool HasVariable(Expression expr, string varName)
    {
        switch (expr)
        {
            case IdentifierExpression id:
                return id.Name.Equals(varName, StringComparison.OrdinalIgnoreCase);
            case PropertyAccessExpression prop:
                return prop.Variable.Equals(varName, StringComparison.OrdinalIgnoreCase);
            case BinaryExpression b:
                return HasVariable(b.Left, varName) || HasVariable(b.Right, varName);
            case UnaryExpression u:
                return HasVariable(u.Operand, varName);
            case FunctionCallExpression fn:
                return fn.Arguments.Any(a => HasVariable(a, varName));
            case ListExpression l:
                return l.Items.Any(i => HasVariable(i, varName));
            case MapLiteralExpression m:
                return m.Properties.Values.Any(v => HasVariable(v, varName));
            case CaseExpression c:
                return (c.TestExpression != null && HasVariable(c.TestExpression, varName)) ||
                       c.WhenBranches.Any(w => HasVariable(w.When, varName) || HasVariable(w.Then, varName)) ||
                       (c.ElseExpression != null && HasVariable(c.ElseExpression, varName));
            case ListComprehensionExpression comp:
                return HasVariable(comp.List, varName) ||
                       (comp.Filter != null && HasVariable(comp.Filter, varName)) ||
                       (comp.Projection != null && HasVariable(comp.Projection, varName));
            case ListPredicateExpression pred:
                return HasVariable(pred.List, varName) || HasVariable(pred.Predicate, varName);
            default:
                return false;
        }
    }

    private static bool HasVariableOutsideAggregation(Expression expr, string varName, bool insideAgg)
    {
        switch (expr)
        {
            case FunctionCallExpression fn when fn.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase) ||
                                                fn.FunctionName.Equals("count", StringComparison.OrdinalIgnoreCase):
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

            case ListExpression l:
                return l.Items.Any(i => HasVariableOutsideAggregation(i, varName, insideAgg));

            case FunctionCallExpression fn:
                return fn.Arguments.Any(arg => HasVariableOutsideAggregation(arg, varName, insideAgg));

            case MapLiteralExpression m:
                return m.Properties.Values.Any(v => HasVariableOutsideAggregation(v, varName, insideAgg));

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

            default:
                return false;
        }
    }

    private string? TryCompileDecomposedAggregation(FunctionCallExpression func, string distinctStr)
    {
        if (func.Arguments.Count != 1) return null;

        var referenced = GetReferencedIdentifiers(func.Arguments[0]);
        var branch = referenced.Select(id => _decomposedBranches.TryGetValue(id, out var b) ? b : null)
                               .FirstOrDefault(b => b != null);

        if (branch == null) return null;

        // Build subquery path for this branch
        var (fromJoins, conditions) = BuildSubqueryPath(branch.Path, "_dec");

        // Temporarily register branch variables so VisitExpression resolves them correctly
        var addedNodes = new List<string>();
        var addedRels = new List<string>();

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

        if (func.FunctionName.Equals("collect", StringComparison.OrdinalIgnoreCase))
        {
            return SqlTemplates.RenderCollectSubquery(func.IsDistinct, projSql, fromJoins.ToString(), whereSql);
        }

        if (func.FunctionName.Equals("count", StringComparison.OrdinalIgnoreCase))
        {
            var countTarget = func.Arguments[0] is WildcardExpression ? "*" : "1";
            return SqlTemplates.RenderCountSubquery(func.IsDistinct, countTarget, fromJoins.ToString(), whereSql);
        }

        return null;
    }
}
