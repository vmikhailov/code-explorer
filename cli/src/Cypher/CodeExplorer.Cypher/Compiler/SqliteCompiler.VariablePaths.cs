using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
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
        bool isShortestPath = false,
        string? pathVariable = null)
    {
        var cteName = $"cte_rel_{_cteIndex++}";
        var minDepth = rel.Range?.Min ?? 1;
        var maxDepth = rel.Range?.Max;

        var isIncoming = rel.Direction == Direction.Incoming;
        var startNode = isIncoming ? targetNode : prevNode;
        var startVar = isIncoming ? targetVar : prevVar;
        var endNode = isIncoming ? prevNode : targetNode;
        var endVar = isIncoming ? prevVar : targetVar;

        var startFilterConditions = BuildAnchorFilterConditions(startNode, startVar, isOptional);
        var endFilterConditions = BuildAnchorFilterConditions(endNode, endVar, isOptional);

        var needsPathNodes = pathVariable != null;
        BuildVarLenRecursiveCte(cteName, rel, minDepth, maxDepth, startFilterConditions, endFilterConditions, needsPathNodes);

        var relOnConditions = BuildVarLenRelConditions(cteName, relVar, prevVar, targetVar, minDepth, maxDepth, isShortestPath);
        List<string> nodeOnConditions = [];

        DispatchVarLenHop(
            rel, cteName, relVar, prevNode, prevVar, targetNode, targetVar,
            prevDeclared, targetDeclared, isOptional, joinKeyword,
            fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions);
    }

    private List<string> BuildAnchorFilterConditions(NodePattern node, string nodeVar, bool isOptional)
    {
        List<string> conditions = [];
        if (node.Labels.Count > 0 || node.Properties != null)
        {
            AddNodeFiltersToConditions(node, "_src_anc", conditions);
        }

        if (!isOptional && !string.IsNullOrEmpty(nodeVar) && !nodeVar.StartsWith("_n", StringComparison.Ordinal))
        {
            List<Expression> conjuncts = [];
            foreach (var match in _query.Matches)
            {
                if (!match.IsOptional && match.Where?.Predicate != null)
                {
                    CollectTopLevelConjuncts(match.Where.Predicate, conjuncts);
                }
            }

            if (_query.Where?.Predicate != null)
            {
                CollectTopLevelConjuncts(_query.Where.Predicate, conjuncts);
            }

            foreach (var conj in conjuncts)
            {
                if (CanPushDownToAnchor(conj, nodeVar))
                {
                    var rewritten = RewriteVariable(conj, nodeVar, "_src_anc");
                    conditions.Add(VisitExpression(rewritten));
                }
            }
        }

        return conditions;
    }

    private static void CollectTopLevelConjuncts(Expression? expr, List<Expression> conjuncts)
    {
        if (expr == null) return;
        if (expr is BinaryExpression { Operator: BinaryOperator.And } bin)
        {
            CollectTopLevelConjuncts(bin.Left, conjuncts);
            CollectTopLevelConjuncts(bin.Right, conjuncts);
        }
        else
        {
            conjuncts.Add(expr);
        }
    }

    private bool CanPushDownToAnchor(Expression expr, string targetVar)
    {
        if (_withAliases.ContainsKey(targetVar) ||
            _unwindVariables.Contains(targetVar) ||
            _pathVariables.ContainsKey(targetVar) ||
            _declaredRels.Contains(targetVar) ||
            _nodePropertySource.ContainsKey(targetVar))
        {
            return false;
        }

        var refs = GetReferencedIdentifiers(expr);
        if (refs.Count != 1 || !refs.Contains(targetVar)) return false;
        return IsSimpleAnchorExpression(expr);
    }

    private static bool IsSimpleAnchorExpression(Expression expr)
    {
        return expr switch
        {
            IdentifierExpression or PropertyAccessExpression or StringLiteralExpression or
                NumberLiteralExpression or BooleanLiteralExpression or NullLiteralExpression or
                ParameterExpression => true,
            HasLabelExpression hl => IsSimpleAnchorExpression(hl.Expression),
            UnaryExpression u => IsSimpleAnchorExpression(u.Operand),
            BinaryExpression b => IsSimpleAnchorExpression(b.Left) && IsSimpleAnchorExpression(b.Right),
            ListExpression l => l.Items.All(IsSimpleAnchorExpression),
            FunctionCallExpression f => !HasAggregation(f) && f.Arguments.All(IsSimpleAnchorExpression),
            _ => false
        };
    }

    private static Expression RewriteVariable(Expression expr, string fromVar, string toVar)
    {
        return expr switch
        {
            IdentifierExpression id when id.Name.Equals(fromVar, StringComparison.OrdinalIgnoreCase) =>
                new IdentifierExpression(toVar),
            PropertyAccessExpression prop when prop.Variable.Equals(fromVar, StringComparison.OrdinalIgnoreCase) =>
                new PropertyAccessExpression(toVar, prop.PropertyName),
            HasLabelExpression hl =>
                new HasLabelExpression(RewriteVariable(hl.Expression, fromVar, toVar), hl.Label),
            BinaryExpression bin =>
                new BinaryExpression(RewriteVariable(bin.Left, fromVar, toVar), bin.Operator, RewriteVariable(bin.Right, fromVar, toVar)),
            UnaryExpression un =>
                new UnaryExpression(un.Operator, RewriteVariable(un.Operand, fromVar, toVar)),
            FunctionCallExpression fn =>
                new FunctionCallExpression(fn.FunctionName, fn.IsDistinct, fn.Arguments.Select(a => RewriteVariable(a, fromVar, toVar)).ToList()),
            ListExpression list =>
                new ListExpression(list.Items.Select(i => RewriteVariable(i, fromVar, toVar)).ToList()),
            _ => expr
        };
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
        var forceCrossJoin = !isOptional && (prevDeclared || targetDeclared);
        if (prevDeclared && !targetDeclared)
        {
            ProcessVarLenForwardHop(rel, cteName, relVar, prevVar, targetNode, targetVar, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions, forceCrossJoin);
        }
        else if (!prevDeclared && targetDeclared)
        {
            ProcessVarLenBackwardHop(rel, cteName, relVar, prevNode, prevVar, targetVar, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions, forceCrossJoin);
        }
        else if (prevDeclared && targetDeclared)
        {
            ProcessVarLenConnectingHop(rel, cteName, relVar, prevVar, targetVar, joinKeyword, fromAndJoins, relOnConditions);
        }
    }

    private void BuildVarLenRecursiveCte(
        string cteName,
        RelationshipPattern rel,
        int minDepth,
        int? maxDepth,
        List<string> startFilterConditions,
        List<string> endFilterConditions,
        bool needsPathNodes = false)
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

        var isUndirected = rel.Direction == Direction.Undirected;
        var walkBackwardsFromEnd = !isUndirected && !needsPathNodes && startFilterConditions.Count == 0 && endFilterConditions.Count > 0;
        var activeAnchorFilters = walkBackwardsFromEnd ? endFilterConditions : startFilterConditions;

        var edgeSourceTable = isUndirected
            ? $"(SELECT from_id, to_id FROM edges e WHERE {edgeKindPred} UNION ALL SELECT to_id AS from_id, from_id AS to_id FROM edges e WHERE {edgeKindPred} AND from_id != to_id) e"
            : "edges e";
        var edgeWherePred = isUndirected ? "1=1" : edgeKindPred;

        var anchorSb = new StringBuilder();
        var cols = needsPathNodes
            ? "start_id, end_id, depth, path_visited, path_nodes"
            : "start_id, end_id, depth, path_visited";
        var zeroNodes = needsPathNodes ? ", json_array(id) AS path_nodes" : "";
        var anchorNodes = needsPathNodes ? ", json_array(e.from_id, e.to_id) AS path_nodes" : "";
        var recNodes = needsPathNodes ? ", json_insert(c.path_nodes, '$[#]', e.to_id)" : "";

        if (minDepth == 0)
        {
            if (activeAnchorFilters.Count > 0)
            {
                anchorSb.AppendLine($"    SELECT id AS start_id, id AS end_id, 0 AS depth, '/' || id || '/' AS path_visited{zeroNodes} FROM nodes _src_anc WHERE {string.Join(" AND ", activeAnchorFilters)}");
            }
            else
            {
                anchorSb.AppendLine($"    SELECT id AS start_id, id AS end_id, 0 AS depth, '/' || id || '/' AS path_visited{zeroNodes} FROM nodes");
            }
            anchorSb.AppendLine("    UNION ALL");
        }

        if (walkBackwardsFromEnd)
        {
            var whereClause = activeAnchorFilters.Count > 0
                ? $"WHERE {string.Join(" AND ", activeAnchorFilters)} AND {edgeWherePred}"
                : $"WHERE {edgeWherePred}";
            anchorSb.Append($"    SELECT e.from_id AS start_id, e.to_id AS end_id, 1 AS depth, '/' || e.from_id || '/' || e.to_id || '/' AS path_visited FROM nodes _src_anc CROSS JOIN {edgeSourceTable} ON e.to_id = _src_anc.id {whereClause}");

            var maxDepthCond = maxDepth.HasValue ? $" AND c.depth < {maxDepth.Value}" : "";
            var recursiveSql = $@"    SELECT e.from_id, c.end_id, c.depth + 1, '/' || e.from_id || c.path_visited
    FROM {cteName} c
    CROSS JOIN {edgeSourceTable} ON e.to_id = c.start_id
    WHERE {edgeWherePred}{maxDepthCond} AND instr(c.path_visited, '/' || e.from_id || '/') = 0";

            var cteSql = $"{cteName}({cols}) AS (\n{anchorSb}\n    UNION ALL\n{recursiveSql}\n)";
            _ctes.Add(cteSql);
            return;
        }

        if (activeAnchorFilters.Count > 0)
        {
            var wherePart = isUndirected
                ? string.Join(" AND ", activeAnchorFilters)
                : $"{string.Join(" AND ", activeAnchorFilters)} AND {edgeWherePred}";
            anchorSb.Append($"    SELECT e.from_id AS start_id, e.to_id AS end_id, 1 AS depth, '/' || e.from_id || '/' || e.to_id || '/' AS path_visited{anchorNodes} FROM nodes _src_anc CROSS JOIN {edgeSourceTable} ON e.from_id = _src_anc.id WHERE {wherePart}");
        }
        else
        {
            var wherePart = isUndirected ? "" : $" WHERE {edgeWherePred}";
            anchorSb.Append($"    SELECT e.from_id AS start_id, e.to_id AS end_id, 1 AS depth, '/' || e.from_id || '/' || e.to_id || '/' AS path_visited{anchorNodes} FROM {edgeSourceTable}{wherePart}");
        }

        var fwdMaxDepthCond = maxDepth.HasValue ? $" AND c.depth < {maxDepth.Value}" : "";
        var fwdWherePrefix = isUndirected ? "" : $"{edgeWherePred} AND ";
        var fwdRecursiveSql = $@"    SELECT c.start_id, e.to_id, c.depth + 1, c.path_visited || e.to_id || '/'{recNodes}
    FROM {cteName} c
    CROSS JOIN {edgeSourceTable} ON e.from_id = c.end_id
    WHERE {fwdWherePrefix}instr(c.path_visited, '/' || e.to_id || '/') = 0{fwdMaxDepthCond}";

        var fwdCteSql = $"{cteName}({cols}) AS (\n{anchorSb}\n    UNION ALL\n{fwdRecursiveSql}\n)";
        _ctes.Add(fwdCteSql);
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
        List<string> nodeOnConditions,
        bool forceCrossJoin = false)
    {
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        AddVarLenHopEndpoints(rel.Direction, isForward: true, rVar, pVar, tVar, relOnConditions, nodeOnConditions);
        FinishVarLenHop(cteName, relVar, rVar, targetVar, targetNode, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions, forceCrossJoin);
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
        List<string> nodeOnConditions,
        bool forceCrossJoin = false)
    {
        var pVar = EscapeVar(prevVar);
        var tVar = EscapeVar(targetVar);
        var rVar = EscapeVar(relVar);

        AddVarLenHopEndpoints(rel.Direction, isForward: false, rVar, pVar, tVar, relOnConditions, nodeOnConditions);
        FinishVarLenHop(cteName, relVar, rVar, prevVar, prevNode, isOptional, joinKeyword, fromAndJoins, optionalWhereExtra, relOnConditions, nodeOnConditions, forceCrossJoin);
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
        List<string> nodeOnConditions,
        bool forceCrossJoin = false)
    {
        AddNodeFiltersToConditions(boundNode, boundVar, nodeOnConditions);
        ApplyOptionalWhereExtra(isOptional, optionalWhereExtra, nodeOnConditions);
        EmitVarLenRelAndNodeJoin(cteName, rVar, EscapeVar(boundVar), relOnConditions, nodeOnConditions, joinKeyword, fromAndJoins, forceCrossJoin);

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
        StringBuilder fromAndJoins,
        bool forceCrossJoin = false)
    {
        var cteJoin = forceCrossJoin ? "CROSS JOIN" : joinKeyword;
        var nodeJoin = forceCrossJoin ? "CROSS JOIN" : joinKeyword;
        fromAndJoins.AppendLine();
        fromAndJoins.Append($"{cteJoin} {cteName} {rVar} ON {string.Join(" AND ", relOnConditions)}");
        fromAndJoins.AppendLine();
        fromAndJoins.Append($"{nodeJoin} nodes {nodeVar} ON {string.Join(" AND ", nodeOnConditions)}");
    }

    private (StringBuilder FromJoins, List<string> Conditions) BuildSubqueryPath(PathPattern path, string prefix)
    {
        List<string> conditions = [];
        var fromJoins = new StringBuilder();

        var headVar = path.Head.Variable;
        var headIsOuter = headVar != null && _declaredNodes.Contains(headVar);
        var actualHeadVar = headVar != null && _declaredNodes.Contains(headVar) ? headVar : $"{prefix}_h{_varIndex++}";

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
            var targetVar = targetNode.Variable;
            var targetIsOuter = targetVar != null && _declaredNodes.Contains(targetVar);
            var actualTargetVar = targetVar != null && _declaredNodes.Contains(targetVar) ? targetVar : (targetVar ?? $"{prefix}_t{_varIndex++}");

            if (!targetIsOuter)
            {
                fromJoins.Append(fromJoins.Length == 0 ? $"edges {relVar} JOIN nodes {actualTargetVar}" : $" JOIN edges {relVar} JOIN nodes {actualTargetVar}");
                AddNodeFiltersToConditions(targetNode, actualTargetVar, conditions);
            }
            else
            {
                fromJoins.Append(fromJoins.Length == 0 ? $"edges {relVar}" : $" JOIN edges {relVar}");
            }

            var prevIdSrc = _nodeIdSource.TryGetValue(prevVar, out var pSrc) ? pSrc : $"{prevVar}.id";
            var targetIdSrc = _nodeIdSource.TryGetValue(actualTargetVar, out var tSrc) ? tSrc : $"{actualTargetVar}.id";

            switch (rel.Direction)
            {
                case Direction.Outgoing:
                    conditions.Add($"{relVar}.from_id = {prevIdSrc}");
                    conditions.Add($"{targetIdSrc} = {relVar}.to_id");
                    break;
                case Direction.Incoming:
                    conditions.Add($"{relVar}.to_id = {prevIdSrc}");
                    conditions.Add($"{targetIdSrc} = {relVar}.from_id");
                    break;
                case Direction.Undirected:
                    conditions.Add($"({relVar}.from_id = {prevIdSrc} OR {relVar}.to_id = {prevIdSrc})");
                    conditions.Add($"{targetIdSrc} = CASE WHEN {relVar}.from_id = {prevIdSrc} THEN {relVar}.to_id ELSE {relVar}.from_id END");
                    break;
            }

            AddRelKindConditions(rel, relVar, conditions);
            prevVar = actualTargetVar;
        }

        return (fromJoins, conditions);
    }

    private string VisitPatternExpression(PatternExpression pat)
    {
        var (fromJoins, conditions) = BuildSubqueryPath(pat.Path, "_pe");
        var whereSql = conditions.Count > 0 ? string.Join(" AND ", conditions) : null;
        return SqlTemplates.RenderExistsSubquery(fromJoins.ToString(), whereSql);
    }

    private string VisitPatternComprehension(PatternComprehensionExpression patComp)
    {
        var (fromJoins, conditions) = BuildSubqueryPath(patComp.Path, "_pc");
        if (patComp.Filter != null)
        {
            conditions.Add(VisitExpression(patComp.Filter));
        }

        var projSql = VisitExpression(patComp.Projection);
        var whereSql = conditions.Count > 0 ? string.Join(" AND ", conditions) : null;
        return SqlTemplates.RenderCollectSubquery(false, projSql, fromJoins.ToString(), whereSql);
    }
}
