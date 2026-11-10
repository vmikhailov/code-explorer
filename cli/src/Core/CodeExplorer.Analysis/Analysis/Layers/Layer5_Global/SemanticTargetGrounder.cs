using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Sql;
using CodeExplorer.Core.Parser.Sql.Ast;
using CodeExplorer.Core.Semantics;

namespace CodeExplorer.Core.Parser.Layers;

/// <summary>
/// Indexes Layer 4 semantic targets (<see cref="TableNode"/>, <see cref="ProcedureNode"/>,
/// <see cref="EndpointNode"/>, <see cref="ExternalServiceNode"/>, <see cref="TopicNode"/>,
/// <see cref="DatabaseNode"/>) and grounds downward <c>TARGETED_BY</c> relationships onto
/// colored Layer 3 <see cref="CallArgumentNode"/>s.
/// </summary>
internal sealed class SemanticTargetGrounder
{
    private const char ColonChar = ':';
    private const char SlashChar = '/';
    private const char QuestionChar = '?';
    private const string SqlParamPlaceholder = "__ce_param__";
    private const string UspPrefix = "usp_";
    private const string SpPrefix = "sp_";
    private const string DsPrefixToken = ":ds:";
    private const string DatasetPrefixToken = ":dataset:";

    private readonly Dictionary<string, TableNode> _tablesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TableNode>> _tablesByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProcedureNode> _proceduresById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ProcedureNode>> _proceduresByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<EndpointNode>> _endpointsByNormalizedRoute = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ExternalServiceNode>> _externalServicesByHost = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TopicNode>> _topicsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DatabaseNode> _primaryDbByProjectPath = new(StringComparer.OrdinalIgnoreCase);

    public static SemanticTargetGrounder Build(Layer4Result l4Result)
    {
        var grounder = new SemanticTargetGrounder();
        var visited = new HashSet<IOntologyNode>(ReferenceEqualityComparer.Instance);

        void Walk(IOntologyNode? node)
        {
            if (node == null || !visited.Add(node)) return;

            grounder.IndexNode(node);

            for (var i = 0; i < node.Children.Count; i++)
            {
                Walk(node.Children[i]);
            }
        }

        Walk(l4Result.SemanticStructure);
        Walk(l4Result.Prev.Prev.Prev.Workspace);
        foreach (var p in l4Result.Prev.Prev.Projects) Walk(p);
        foreach (var f in l4Result.Prev.Prev.Prev.Files) Walk(f);

        return grounder;
    }

    public void GroundTargets(
        CallArgumentNode argNode,
        SemanticArgumentSlice slice,
        List<string> effectiveValues,
        FunctionNode callerFn,
        string fileId,
        ProjectNode? ownerProject,
        ParsingContext ctx,
        List<Node> newNodes,
        Action<IOntologyRelationship, bool> addRelationship)
    {
        switch (slice.Nature)
        {
            case ParameterNature.SqlQuery:
                GroundSqlTargets(argNode, effectiveValues, callerFn, fileId, ownerProject, ctx, newNodes, addRelationship);
                break;

            case ParameterNature.UrlOrRoute:
                GroundUrlTargets(argNode, effectiveValues, addRelationship);
                break;

            case ParameterNature.TopicOrQueue:
                for (var i = 0; i < effectiveValues.Count; i++)
                {
                    if (_topicsByName.TryGetValue(effectiveValues[i].Trim(), out var topics))
                    {
                        for (var t = 0; t < topics.Count; t++)
                        {
                            addRelationship(new TargetedByRelationship(topics[t].Id, argNode.Id), false);
                        }
                    }
                }
                break;

            case ParameterNature.ConnectionString:
                if (ownerProject != null &&
                    _primaryDbByProjectPath.TryGetValue(ownerProject.Path, out var projDb))
                {
                    addRelationship(new TargetedByRelationship(projDb.Id, argNode.Id), false);
                }
                break;
        }
    }

    private void IndexNode(IOntologyNode node)
    {
        switch (node)
        {
            case TableNode tbl:
                RegisterTable(tbl);
                break;
            case ProcedureNode proc:
                RegisterProcedure(proc);
                break;
            case EndpointNode ep:
                var normRoute = NormalizeRoutePath(ep.RouteTemplate);
                if (!string.IsNullOrEmpty(normRoute))
                {
                    if (!_endpointsByNormalizedRoute.TryGetValue(normRoute, out var epList))
                    {
                        epList = [];
                        _endpointsByNormalizedRoute[normRoute] = epList;
                    }
                    if (!epList.Contains(ep)) epList.Add(ep);
                }
                break;
            case ExternalServiceNode es:
                var hostKey = !string.IsNullOrEmpty(es.DomainOrService) ? es.DomainOrService : es.Name;
                if (!string.IsNullOrEmpty(hostKey))
                {
                    if (!_externalServicesByHost.TryGetValue(hostKey, out var esList))
                    {
                        esList = [];
                        _externalServicesByHost[hostKey] = esList;
                    }
                    if (!esList.Contains(es)) esList.Add(es);
                }
                break;
            case TopicNode top:
                if (!string.IsNullOrEmpty(top.Name))
                {
                    if (!_topicsByName.TryGetValue(top.Name, out var topList))
                    {
                        topList = [];
                        _topicsByName[top.Name] = topList;
                    }
                    if (!topList.Contains(top)) topList.Add(top);
                }
                break;
            case DatabaseNode db:
                if (!string.IsNullOrEmpty(db.Path))
                {
                    _primaryDbByProjectPath.TryAdd(db.Path, db);
                }
                break;
        }
    }

    private void RegisterTable(TableNode tbl)
    {
        _tablesById.TryAdd(tbl.Id, tbl);
        if (string.IsNullOrEmpty(tbl.Name)) return;

        if (!_tablesByName.TryGetValue(tbl.Name, out var list))
        {
            list = [];
            _tablesByName[tbl.Name] = list;
        }
        if (!list.Contains(tbl))
        {
            list.Add(tbl);
        }
    }

    private void RegisterProcedure(ProcedureNode proc)
    {
        _proceduresById.TryAdd(proc.Id, proc);
        if (string.IsNullOrEmpty(proc.Name)) return;

        if (!_proceduresByName.TryGetValue(proc.Name, out var list))
        {
            list = [];
            _proceduresByName[proc.Name] = list;
        }
        if (!list.Contains(proc))
        {
            list.Add(proc);
        }
    }

    private TableNode? ResolveExistingTable(string tableName, string? schema, string defaultTableId)
    {
        if (_tablesById.TryGetValue(defaultTableId, out var byId))
        {
            return byId;
        }

        if (!_tablesByName.TryGetValue(tableName, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(schema))
        {
            var dsToken = string.Concat(DsPrefixToken, schema, ColonChar);
            var datasetToken = string.Concat(DatasetPrefixToken, schema, ColonChar);
            for (var i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (string.Equals(cand.Schema, schema, StringComparison.OrdinalIgnoreCase) ||
                    cand.Id.Contains(dsToken, StringComparison.OrdinalIgnoreCase) ||
                    cand.Id.Contains(datasetToken, StringComparison.OrdinalIgnoreCase))
                {
                    return cand;
                }
            }
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var cand = candidates[i];
            if (cand.Id.Contains(DsPrefixToken, StringComparison.OrdinalIgnoreCase) ||
                cand.Id.Contains(DatasetPrefixToken, StringComparison.OrdinalIgnoreCase))
            {
                return cand;
            }
        }

        return candidates[0];
    }

    private ProcedureNode? ResolveExistingProcedure(string procName, string? schema, string defaultProcId)
    {
        if (_proceduresById.TryGetValue(defaultProcId, out var byId))
        {
            return byId;
        }

        if (!_proceduresByName.TryGetValue(procName, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(schema))
        {
            var dsToken = string.Concat(DsPrefixToken, schema, ColonChar);
            var datasetToken = string.Concat(DatasetPrefixToken, schema, ColonChar);
            for (var i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (string.Equals(cand.Schema, schema, StringComparison.OrdinalIgnoreCase) ||
                    cand.Id.Contains(dsToken, StringComparison.OrdinalIgnoreCase) ||
                    cand.Id.Contains(datasetToken, StringComparison.OrdinalIgnoreCase))
                {
                    return cand;
                }
            }
        }

        return candidates[0];
    }

    private void GroundSqlTargets(
        CallArgumentNode argNode,
        List<string> effectiveValues,
        FunctionNode callerFn,
        string fileId,
        ProjectNode? ownerProject,
        ParsingContext ctx,
        List<Node> newNodes,
        Action<IOntologyRelationship, bool> addRelationship)
    {
        for (var v = 0; v < effectiveValues.Count; v++)
        {
            var sqlText = effectiveValues[v];
            if (string.IsNullOrWhiteSpace(sqlText)) continue;

            var dialect = SqlDialectDetector.DetectDialect(sqlText, callerFn.FilePath);
            var parser = SqlDialectParserFactory.GetParser(dialect);
            var script = parser.Parse(sqlText);

            var tables = new List<SqlTableRefAst>();
            var procs = new List<SqlProcRefAst>();

            for (var s = 0; s < script.Statements.Count; s++)
            {
                CollectSqlTargetsFromStatement(script.Statements[s], tables, procs);
            }

            var trimmed = sqlText.Trim();
            if (tables.Count == 0 && procs.Count == 0 &&
                !trimmed.Contains(' ') &&
                (trimmed.StartsWith(UspPrefix, StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith(SpPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                procs.Add(new SqlProcRefAst(null, null, trimmed));
            }

            GroundSqlTableRefs(argNode, tables, callerFn, fileId, ownerProject, ctx, newNodes, addRelationship);
            GroundSqlProcedureRefs(argNode, procs, callerFn, fileId, ownerProject, ctx, newNodes, addRelationship);
        }
    }

    private void GroundSqlTableRefs(
        CallArgumentNode argNode,
        List<SqlTableRefAst> tables,
        FunctionNode callerFn,
        string fileId,
        ProjectNode? ownerProject,
        ParsingContext ctx,
        List<Node> newNodes,
        Action<IOntologyRelationship, bool> addRelationship)
    {
        for (var t = 0; t < tables.Count; t++)
        {
            var tblRef = tables[t];
            if (tblRef.IsCte || tblRef.IsSubquery ||
                string.IsNullOrWhiteSpace(tblRef.TableName) ||
                tblRef.TableName.Equals(SqlParamPlaceholder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var defaultTableId = string.Concat(
                ctx.WorkspaceId, ColonChar, OntologyConstants.IdPrefixes.Table, ColonChar,
                tblRef.TableName.ToLowerInvariant());

            var tableNode = ResolveExistingTable(tblRef.TableName, tblRef.Schema, defaultTableId);
            if (tableNode == null)
            {
                if (callerFn.IsTest || !PostIndexAnalyzer.IsValidTableIdentifier(tblRef.TableName))
                {
                    continue;
                }

                tableNode = new TableNode(defaultTableId, tblRef.TableName, callerFn.FilePath)
                {
                    Schema = tblRef.Schema
                };
                RegisterTable(tableNode);
                newNodes.Add(Node.FromNode(tableNode));

                if (!string.IsNullOrEmpty(fileId))
                {
                    addRelationship(new DeclaredInRelationship(tableNode.Id, fileId), false);
                }
                if (ownerProject != null && _primaryDbByProjectPath.TryGetValue(ownerProject.Path, out var dbNode))
                {
                    addRelationship(new ContainsRelationship(dbNode.Id, tableNode.Id), true);
                }
            }

            addRelationship(new TargetedByRelationship(tableNode.Id, argNode.Id), false);
            if (!callerFn.IsTest)
            {
                addRelationship(new AccessedByRelationship(tableNode.Id, callerFn.Id), true);
            }
        }
    }

    private void GroundSqlProcedureRefs(
        CallArgumentNode argNode,
        List<SqlProcRefAst> procs,
        FunctionNode callerFn,
        string fileId,
        ProjectNode? ownerProject,
        ParsingContext ctx,
        List<Node> newNodes,
        Action<IOntologyRelationship, bool> addRelationship)
    {
        for (var p = 0; p < procs.Count; p++)
        {
            var procRef = procs[p];
            if (string.IsNullOrWhiteSpace(procRef.ProcedureName) ||
                procRef.ProcedureName.Equals(SqlParamPlaceholder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var defaultProcId = string.Concat(
                ctx.WorkspaceId, ColonChar, OntologyConstants.IdPrefixes.Procedure, ColonChar,
                procRef.ProcedureName.ToLowerInvariant());

            var procNode = ResolveExistingProcedure(procRef.ProcedureName, procRef.Schema, defaultProcId);
            if (procNode == null)
            {
                if (callerFn.IsTest) continue;

                procNode = new ProcedureNode(defaultProcId, procRef.ProcedureName, callerFn.FilePath)
                {
                    Schema = procRef.Schema
                };
                RegisterProcedure(procNode);
                newNodes.Add(Node.FromNode(procNode));

                if (!string.IsNullOrEmpty(fileId))
                {
                    addRelationship(new DeclaredInRelationship(procNode.Id, fileId), false);
                }
                if (ownerProject != null && _primaryDbByProjectPath.TryGetValue(ownerProject.Path, out var dbNode))
                {
                    addRelationship(new ContainsRelationship(dbNode.Id, procNode.Id), true);
                }
            }

            addRelationship(new TargetedByRelationship(procNode.Id, argNode.Id), false);
            if (!callerFn.IsTest)
            {
                addRelationship(new AccessedByRelationship(procNode.Id, callerFn.Id), true);
            }
        }
    }

    private static void CollectSqlTargetsFromStatement(
        SqlStatementAst? stmt,
        List<SqlTableRefAst> tables,
        List<SqlProcRefAst> procs)
    {
        if (stmt == null) return;

        for (var c = 0; c < stmt.Ctes.Count; c++)
        {
            CollectSqlTargetsFromStatement(stmt.Ctes[c].InnerStatement, tables, procs);
        }

        switch (stmt)
        {
            case SqlSelectStatementAst sel:
                tables.AddRange(sel.Tables);
                tables.AddRange(sel.Joins);
                break;
            case SqlInsertStatementAst ins:
                tables.Add(ins.TargetTable);
                CollectSqlTargetsFromStatement(ins.SourceQuery, tables, procs);
                break;
            case SqlUpdateStatementAst upd:
                tables.Add(upd.TargetTable);
                tables.AddRange(upd.FromTables);
                break;
            case SqlDeleteStatementAst del:
                tables.Add(del.TargetTable);
                tables.AddRange(del.UsingTables);
                break;
            case SqlMergeStatementAst mrg:
                tables.Add(mrg.TargetTable);
                if (mrg.UsingTable != null) tables.Add(mrg.UsingTable);
                break;
            case SqlCreateTableStatementAst ct:
                tables.Add(ct.Table);
                CollectSqlTargetsFromStatement(ct.AsSelectQuery, tables, procs);
                break;
            case SqlCreateViewStatementAst cv:
                tables.Add(cv.View);
                CollectSqlTargetsFromStatement(cv.AsSelectQuery, tables, procs);
                break;
            case SqlCreateProcedureStatementAst cp:
                procs.Add(cp.Procedure);
                for (var b = 0; b < cp.BodyStatements.Count; b++)
                {
                    CollectSqlTargetsFromStatement(cp.BodyStatements[b], tables, procs);
                }
                break;
            case SqlDropStatementAst drp:
                if (drp.DropType is SqlDropObjectType.Table or SqlDropObjectType.View)
                {
                    tables.Add(drp.Target);
                }
                else if (drp.DropType == SqlDropObjectType.Procedure)
                {
                    procs.Add(new SqlProcRefAst(drp.Target.Database, drp.Target.Schema, drp.Target.TableName));
                }
                break;
            case SqlTruncateStatementAst tr:
                tables.Add(tr.TargetTable);
                break;
            case SqlExecStatementAst ex:
                procs.Add(ex.Procedure);
                break;
        }
    }

    private void GroundUrlTargets(
        CallArgumentNode argNode,
        List<string> effectiveValues,
        Action<IOntologyRelationship, bool> addRelationship)
    {
        for (var i = 0; i < effectiveValues.Count; i++)
        {
            var val = effectiveValues[i].Trim();
            if (string.IsNullOrEmpty(val)) continue;

            if (Uri.TryCreate(val, UriKind.Absolute, out var absUri))
            {
                if (!string.IsNullOrEmpty(absUri.Host) &&
                    _externalServicesByHost.TryGetValue(absUri.Host, out var extServices))
                {
                    for (var e = 0; e < extServices.Count; e++)
                    {
                        addRelationship(new TargetedByRelationship(extServices[e].Id, argNode.Id), false);
                    }
                }

                var pathRoute = NormalizeRoutePath(absUri.AbsolutePath);
                if (!string.IsNullOrEmpty(pathRoute) &&
                    _endpointsByNormalizedRoute.TryGetValue(pathRoute, out var absEps))
                {
                    for (var e = 0; e < absEps.Count; e++)
                    {
                        addRelationship(new TargetedByRelationship(absEps[e].Id, argNode.Id), false);
                    }
                }
            }
            else
            {
                var relRoute = NormalizeRoutePath(val);
                if (!string.IsNullOrEmpty(relRoute) &&
                    _endpointsByNormalizedRoute.TryGetValue(relRoute, out var relEps))
                {
                    for (var e = 0; e < relEps.Count; e++)
                    {
                        addRelationship(new TargetedByRelationship(relEps[e].Id, argNode.Id), false);
                    }
                }
            }
        }
    }

    private static string NormalizeRoutePath(string? rawRoute)
    {
        if (string.IsNullOrWhiteSpace(rawRoute)) return string.Empty;

        var route = rawRoute.Trim();
        var qIdx = route.IndexOf(QuestionChar);
        if (qIdx >= 0) route = route[..qIdx];

        if (!route.StartsWith(SlashChar))
        {
            route = string.Concat(SlashChar, route);
        }

        if (route.Length > 1 && route.EndsWith(SlashChar))
        {
            route = route.TrimEnd(SlashChar);
        }

        return route;
    }
}
