using System.Text;
using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
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
                BindHeadNode(path.Head, headVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
            }
            return;
        }

        var anyDeclared = _declaredNodes.Contains(headVar) ||
                          path.Chain.Any(c => c.Target.Variable != null && _declaredNodes.Contains(c.Target.Variable));
        if (!anyDeclared)
        {
            BindHeadNode(path.Head, headVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
        }

        ProcessPathChain(path, headVar, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
    }

    private void BindHeadNode(
        NodePattern headNode,
        string headVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
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
        ApplyNodeConditions(headNode, headVar, isOptional, fromAndJoins, mainWhereConditions, optionalWhereExtra);
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
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        List<string> conditions = [];
        AddNodeFiltersToConditions(node, nodeVar, conditions);
        ApplyOptionalWhereExtra(isOptional, optionalWhereExtra, conditions);

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
}
