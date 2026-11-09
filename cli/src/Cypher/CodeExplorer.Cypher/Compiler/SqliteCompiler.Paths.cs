using System.Text;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Common;

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

        if (isOptional && match.Where != null)
        {
            List<string> tempNodes = [];
            List<string> tempRels = [];
            foreach (var p in match.Paths)
            {
                if (p.Head.Variable != null && _declaredNodes.Add(p.Head.Variable))
                {
                    tempNodes.Add(p.Head.Variable);
                }

                foreach (var c in p.Chain)
                {
                    if (c.Relationship.Variable != null && _declaredRels.Add(c.Relationship.Variable))
                    {
                        tempRels.Add(c.Relationship.Variable);
                    }

                    if (c.Target.Variable != null && _declaredNodes.Add(c.Target.Variable))
                    {
                        tempNodes.Add(c.Target.Variable);
                    }
                }
            }

            optionalWhereExtra.Add(VisitExpression(match.Where.Predicate));

            foreach (var n in tempNodes)
            {
                _declaredNodes.Remove(n);
            }

            foreach (var r in tempRels)
            {
                _declaredRels.Remove(r);
            }
        }

        foreach (var path in match.Paths)
        {
            ProcessPathPattern(path, isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
        }

        if (!isOptional && match.Where != null)
        {
            mainWhereConditions.Add(VisitExpression(match.Where.Predicate));
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
        var nodes = new NodePattern[path.Chain.Count + 1];
        var vars = new string[path.Chain.Count + 1];
        nodes[0] = path.Head;
        vars[0] = headVar;
        for (var i = 0; i < path.Chain.Count; i++)
        {
            nodes[i + 1] = path.Chain[i].Target;
            vars[i + 1] = path.Chain[i].Target.Variable ?? $"_n{_varIndex++}";
        }

        var anchorIndex = 0;
        for (var i = 0; i < vars.Length; i++)
        {
            if (_declaredNodes.Contains(vars[i]))
            {
                anchorIndex = i;
                break;
            }
        }

        for (var i = anchorIndex - 1; i >= 0; i--)
        {
            ProcessPathElementAt(path, i, nodes[i], vars[i], nodes[i + 1], vars[i + 1], isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
        }

        for (var i = anchorIndex; i < path.Chain.Count; i++)
        {
            ProcessPathElementAt(path, i, nodes[i], vars[i], nodes[i + 1], vars[i + 1], isOptional, joinKeyword, fromAndJoins, mainWhereConditions, optionalWhereExtra);
        }
    }

    private void ProcessPathElementAt(
        PathPattern path,
        int elementIndex,
        NodePattern prevNode,
        string prevVar,
        NodePattern targetNode,
        string targetVar,
        bool isOptional,
        string joinKeyword,
        StringBuilder fromAndJoins,
        List<string> mainWhereConditions,
        List<string> optionalWhereExtra)
    {
        var element = path.Chain[elementIndex];
        var rel = element.Relationship;
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
            fromAndJoins, mainWhereConditions, optionalWhereExtra, path.PathVariable);
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
        List<string> optionalWhereExtra,
        string? pathVariable = null)
    {
        var prevDeclared = _declaredNodes.Contains(prevVar);
        var targetDeclared = _declaredNodes.Contains(targetVar);

        if (isVarLen)
        {
            ProcessVariableLengthRel(
                rel, relVar, prevNode, prevVar, targetNode, targetVar,
                prevDeclared, targetDeclared, isOptional, joinKeyword,
                fromAndJoins, mainWhereConditions, optionalWhereExtra,
                isShortestPath, pathVariable);
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
                relOnConditions.Add(isForward ? $"{rVar}.from_id = {fixedVar}.rowid" : $"{rVar}.to_id = {fixedVar}.rowid");
                nodeOnConditions.Add(isForward ? $"{freeVar}.rowid = {rVar}.to_id" : $"{freeVar}.rowid = {rVar}.from_id");
                break;
            case Direction.Incoming:
                relOnConditions.Add(isForward ? $"{rVar}.to_id = {fixedVar}.rowid" : $"{rVar}.from_id = {fixedVar}.rowid");
                nodeOnConditions.Add(isForward ? $"{freeVar}.rowid = {rVar}.from_id" : $"{freeVar}.rowid = {rVar}.to_id");
                break;
            case Direction.Undirected:
                relOnConditions.Add($"({rVar}.from_id = {fixedVar}.rowid OR {rVar}.to_id = {fixedVar}.rowid)");
                nodeOnConditions.Add($"{freeVar}.rowid = CASE WHEN {rVar}.from_id = {fixedVar}.rowid THEN {rVar}.to_id ELSE {rVar}.from_id END");
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
                relOnConditions.Add($"{rVar}.from_id = {pVar}.rowid AND {rVar}.to_id = {tVar}.rowid");
                break;
            case Direction.Incoming:
                relOnConditions.Add($"{rVar}.to_id = {pVar}.rowid AND {rVar}.from_id = {tVar}.rowid");
                break;
            case Direction.Undirected:
                relOnConditions.Add($"(({rVar}.from_id = {pVar}.rowid AND {rVar}.to_id = {tVar}.rowid) OR ({rVar}.to_id = {pVar}.rowid AND {rVar}.from_id = {tVar}.rowid))");
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

    private void AddRelKindConditions(RelationshipPattern rel, string relVar, List<string> conditions)
    {
        var rVar = EscapeVar(relVar);
        if (rel.RelationshipKinds.Count == 1)
        {
            var kind = rel.RelationshipKinds[0];
            var typeName = kind != RelationshipKind.Unspecified ? kind.ToCypherType() : rel.Types[0];
            conditions.Add($"{rVar}.kind = {GraphKindCodec.EncodeRelKind(typeName)}");
        }
        else if (rel.RelationshipKinds.Count > 1)
        {
            var kinds = string.Join(", ", rel.RelationshipKinds.Select((k, idx) =>
                GraphKindCodec.EncodeRelKind(k != RelationshipKind.Unspecified ? k.ToCypherType() : rel.Types[idx])));
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
        if (node.NodeKinds.Count == 1)
        {
            var kind = node.NodeKinds[0];
            var kindId = kind != NodeKind.Unspecified ? GraphKindCodec.EncodeNodeKind(kind) : GraphKindCodec.EncodeNodeKind(node.Labels[0]);
            conditions.Add($"{nVar}.kind = {kindId}");
        }
        else if (node.NodeKinds.Count > 1)
        {
            var kinds = string.Join(", ", node.NodeKinds.Select((k, idx) =>
                k != NodeKind.Unspecified ? GraphKindCodec.EncodeNodeKind(k) : GraphKindCodec.EncodeNodeKind(node.Labels[idx])));
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
                    $"COALESCE(json_extract({nVar}.properties, '$.kind'), ce_node_kind({nVar}.kind)) = {VisitExpression(v)}");
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
