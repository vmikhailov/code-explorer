using System.Diagnostics.CodeAnalysis;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public interface IOntologyNode
{
    string Id { get; }
    NodeKind Kind { get; }
    NodeKind NodeKind => Kind;
    string Path { get; }
    SymbolFlags Flags { get; set; }
    Dictionary<string, string>? CustomProperties { get; set; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}

public static class OntologyNodeExtensions
{
    public static void ApplySyntacticProperty(this IOntologyNode node, string key, string value)
    {
        switch (key)
        {
            case "is_exported":
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    node.Flags |= SymbolFlags.IsExported;
                else
                    node.Flags &= ~SymbolFlags.IsExported;
                if (node is Layer3_Syntactic.FunctionNode fnExp) fnExp.IsExported = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                if (node is Layer3_Syntactic.TypeNode tnExp) tnExp.IsExported = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "is_module_init":
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    node.Flags |= SymbolFlags.IsModuleInit;
                if (node is Layer3_Syntactic.FunctionNode fnMod) fnMod.IsModuleInit = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "has_parse_issues":
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    node.Flags |= SymbolFlags.HasParseIssues;
                return;
            case "parse_issue_count":
                if (int.TryParse(value, out var pic) && pic > 0)
                    node.Flags |= SymbolFlags.HasParseIssues;
                return;
            case "has_entity_or_table":
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    node.Flags |= SymbolFlags.HasEntityOrTable;
                if (node is Layer1_Physical.FileNode fileEnt)
                    fileEnt.Signals().HasEntityOrTable = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "is_implicit_schema":
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    node.Flags |= SymbolFlags.IsImplicitSchema;
                else
                    node.Flags &= ~SymbolFlags.IsImplicitSchema;
                if (node is Layer4_Semantic.DatabaseNode dbImp) dbImp.IsImplicitSchema = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                if (node is Layer4_Semantic.TableNode tblImp) tblImp.IsImplicitSchema = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                if (node is Layer4_Semantic.DataSetNode dsImp) dsImp.IsImplicitSchema = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "enclosing_type" when node is Layer3_Syntactic.FunctionNode fnEnc:
                fnEnc.Signals().EnclosingTypeName = value;
                return;
            case "ext_receiver_type" when node is Layer3_Syntactic.FunctionNode fnExtRecv:
                fnExtRecv.Signals().ExtensionReceiverTypeName = value;
                return;
            case "df_return" when node is Layer3_Syntactic.FunctionNode fnDfRet:
                fnDfRet.Signals().DataflowReturn = value;
                return;
            case "df_writes" when node is Layer3_Syntactic.FunctionNode fnDfWr:
                fnDfWr.Signals().DataflowWrites = value;
                return;
            case "df_summary" when node is Layer3_Syntactic.FunctionNode fnDfSum:
                fnDfSum.Signals().DataflowSummary = value;
                return;
            case "graphql_field" when node is Layer3_Syntactic.FunctionNode fnGqlF:
                fnGqlF.Signals().GraphqlField = value;
                return;
            case "graphql_schema" when node is Layer3_Syntactic.FunctionNode fnGqlS:
                fnGqlS.Signals().GraphqlSchema = value;
                return;
            case "graphql_aliases" when node is Layer3_Syntactic.FunctionNode fnGqlA:
                fnGqlA.Signals().GraphqlAliases = value;
                return;
            case "graphql_operation" when node is Layer3_Syntactic.FunctionNode fnGqlOp:
                fnGqlOp.GraphqlOperation = value;
                return;
            case "operation_type" when node is Layer3_Syntactic.FunctionNode fnOpType:
                fnOpType.OperationType = value;
                return;
            case "graphql_fields" when node is Layer3_Syntactic.FunctionNode fnGqlFlds:
                fnGqlFlds.GraphqlFields = value;
                return;
            case "messaging_direction" when node is Layer3_Syntactic.FunctionNode fnMsg:
                fnMsg.Signals().MessagingDirection = value;
                return;
            case "is_async" when node is Layer3_Syntactic.FunctionNode fnAsync:
                fnAsync.IsAsync = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "is_unsafe" when node is Layer3_Syntactic.FunctionNode fnUnsafe:
                fnUnsafe.IsUnsafe = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "visibility" when node is Layer3_Syntactic.FunctionNode fnVis:
                fnVis.Visibility = value;
                return;
            case "dbcontext_base_conn" when node is Layer1_Physical.FileNode fileDbc:
                fileDbc.Signals().DbContextBaseConn = value;
                return;
            case "dbcontext_classes" when node is Layer1_Physical.FileNode fileDbCls:
                fileDbCls.Signals().DbContextClasses = value;
                return;
            case "declared_schema_raw" when node is Layer1_Physical.FileNode fileSch:
                fileSch.Signals().DeclaredSchemaRaw = value;
                return;
            case "verified_db_signals" when node is Layer1_Physical.FileNode fileVdb:
                fileVdb.Signals().VerifiedDbSignals = value;
                return;
            case "rust_topology_summary" when node is Layer1_Physical.FileNode fileRts:
                fileRts.Signals().RustTopologySummary = value;
                return;
            case "rust_topology_body_summary" when node is Layer1_Physical.FileNode fileRtbs:
                fileRtbs.Signals().RustTopologyBodySummary = value;
                return;
            case "compose_participants" when node is Layer1_Physical.FileNode fileCp:
                fileCp.Signals().ComposeParticipants = value;
                return;
            case "db_driver_engine" when node is Layer1_Physical.FileNode fDde:
                fDde.Signals().DbDriverEngine = value;
                return;
            case "db_driver_type" when node is Layer1_Physical.FileNode fDdt:
                fDdt.Signals().DbDriverFamily = value;
                return;
            case "parameter_index" when node is Layer3_Syntactic.MemberNode memPi && int.TryParse(value, out var piVal):
                memPi.ParameterIndex = piVal;
                return;
            case "variable_type" when node is Layer3_Syntactic.MemberNode memVt:
                memVt.VariableClassification = value;
                return;
            case "initializer_expression" when node is Layer3_Syntactic.MemberNode memIe:
                memIe.InitializerExpression = value;
                return;
            case "is_constant" when node is Layer3_Syntactic.MemberNode memIc:
                memIc.IsConstant = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "extends" when node is Layer3_Syntactic.TypeNode tnExt:
                tnExt.Extends = value;
                return;
            case "implements" when node is Layer3_Syntactic.TypeNode tnImp:
                tnImp.Implements = value;
                return;
            case "attributes" when node is Layer3_Syntactic.TypeNode tnAttr:
                tnAttr.Attributes = value;
                return;
            case "attributes" when node is Layer3_Syntactic.FunctionNode fnAttr:
                fnAttr.Attributes = value;
                return;
            case "attributes" when node is Layer3_Syntactic.MemberNode memAttr:
                memAttr.Attributes = value;
                return;
            case "is_test" when node is Layer3_Syntactic.FunctionNode fnTest:
                fnTest.IsTest = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "test_framework" when node is Layer3_Syntactic.FunctionNode fnTf:
                fnTf.TestFramework = value;
                return;
            case "status_code" when node is Layer4_Semantic.EndpointNode epSc:
                epSc.StatusCode = value;
                return;
            case "summary" when node is Layer4_Semantic.EndpointNode epSum:
                epSum.Summary = value;
                return;
            case "graphql_sdl" when node is Layer4_Semantic.EndpointNode epGqlSdl:
                epGqlSdl.IsGraphqlSdl = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "graphql_field" when node is Layer4_Semantic.EndpointNode epGqlFld:
                epGqlFld.GraphqlField = value;
                return;
            case "schema_name" or "schema" when node is Layer4_Semantic.TableNode tblSch:
                tblSch.Schema = value;
                return;
            case "messaging_direction" when node is Layer4_Semantic.TopicNode topMsg:
                topMsg.MessagingDirection = value;
                return;
        }

        if (node is Layer1_Physical.FileNode fileSchemaLine &&
            key.StartsWith("schema_line:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value, out var schemaLineNum))
        {
            fileSchemaLine.Signals().SetSchemaLine(key["schema_line:".Length..], schemaLineNum);
        }
    }

    public static string GetName(this IOntologyNode node) => node switch
    {
        Layer4_Semantic.ServiceNode s => s.Name,
        Layer4_Semantic.AppNode a => a.Name,
        Layer4_Semantic.WorkerNode w => w.Name,
        Layer4_Semantic.CliToolNode c => c.Name,
        Layer4_Semantic.LibraryNode l => l.Name,
        Layer4_Semantic.TestSuiteNode t => t.Name,
        Layer4_Semantic.EndpointNode ep => ep.Name,
        Layer4_Semantic.ExternalServiceNode es => es.Name,
        Layer4_Semantic.DatabaseNode db => db.Name,
        Layer4_Semantic.DataSetNode ds => ds.Name,
        Layer4_Semantic.TableNode tbl => tbl.Name,
        Layer4_Semantic.ProcedureNode proc => proc.Name,
        Layer4_Semantic.QueryNode q => q.Name,
        Layer4_Semantic.TopicNode top => top.Name,
        Layer4_Semantic.CloudServiceNode cs => cs.Name,
        Layer4_Semantic.ApiInUseNode api => api.Name,
        Layer2_Boundaries.ProjectNode p => p.Name,
        Layer2_Boundaries.EntryPointNode ent => ent.Name,
        Layer2_Boundaries.PackageNode pkg => pkg.Name,
        Layer1_Physical.FileNode f => f.Name,
        Layer1_Physical.FolderNode fld => fld.Name,
        Layer1_Physical.GitSettingsNode git => git.Name,
        Layer1_Physical.ScanIssueNode si => si.Name,
        Layer3_Syntactic.TypeNode ty => ty.Name,
        Layer3_Syntactic.FunctionNode fn => fn.Name,
        Layer3_Syntactic.MemberNode m => m.Name,
        Layer3_Syntactic.CallLiteralNode cl => cl.Name,
        Layer3_Syntactic.ParameterNode prm => prm.Name,
        Layer3_Syntactic.CallArgumentNode ca => ca.Name,
        Layer3_Syntactic.CatchClauseNode cc => cc.Name,
        WorkspaceNode ws => ws.Name,
        _ => node.Id.Contains(':') ? node.Id[(node.Id.LastIndexOf(':') + 1)..] : node.Id
    };
}
