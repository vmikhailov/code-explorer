using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Common.Nodes;

public interface IOntologyNode
{
    string Id { get; }
    NodeKind Kind { get; }
    NodeKind NodeKind => Kind;
    string Path { get; }
    SymbolFlags Flags { get; set; }
    Dictionary<string, string>? Extensions { get; set; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}

public static class OntologyNodeExtensions
{
    public static void SetExtension(this IOntologyNode node, string key, string value)
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
                if (node is Layer1_Physical.FileNode fileParse) fileParse.HasParseIssues = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "parse_issue_count":
                if (node is Layer1_Physical.FileNode fileCount && int.TryParse(value, out var pic))
                {
                    fileCount.ParseIssueCount = pic;
                    if (pic > 0) fileCount.HasParseIssues = true;
                    return;
                }
                break;
            case "has_entity_or_table":
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    node.Flags |= SymbolFlags.HasEntityOrTable;
                if (node is Layer1_Physical.FileNode fileEnt) fileEnt.HasEntityOrTable = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
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
                fnEnc.EnclosingType = value;
                return;
            case "df_return" when node is Layer3_Syntactic.FunctionNode fnDfRet:
                fnDfRet.DataflowReturn = value;
                return;
            case "df_writes" when node is Layer3_Syntactic.FunctionNode fnDfWr:
                fnDfWr.DataflowWrites = value;
                return;
            case "df_summary" when node is Layer3_Syntactic.FunctionNode fnDfSum:
                fnDfSum.DataflowSummary = value;
                return;
            case "graphql_field" when node is Layer3_Syntactic.FunctionNode fnGqlF:
                fnGqlF.GraphqlField = value;
                return;
            case "graphql_schema" when node is Layer3_Syntactic.FunctionNode fnGqlS:
                fnGqlS.GraphqlSchema = value;
                return;
            case "graphql_aliases" when node is Layer3_Syntactic.FunctionNode fnGqlA:
                fnGqlA.GraphqlAliases = value;
                return;
            case "messaging_direction" when node is Layer3_Syntactic.FunctionNode fnMsg:
                fnMsg.MessagingDirection = value;
                return;
            case "git_branch" when node is Layer2_Boundaries.ProjectNode pGb:
                pGb.GitBranch = value;
                return;
            case "git_origin" when node is Layer2_Boundaries.ProjectNode pGo:
                pGo.GitOrigin = value;
                return;
            case "git_user" when node is Layer2_Boundaries.ProjectNode pGu:
                pGu.GitUser = value;
                return;
            case "git_commit" when node is Layer2_Boundaries.ProjectNode pGc:
                pGc.GitCommit = value;
                return;
            case "git_repo" when node is Layer2_Boundaries.ProjectNode pGr:
                pGr.GitRepo = value;
                return;
            case "framework" when node is Layer2_Boundaries.ProjectNode pFw:
                pFw.Framework = value;
                return;
            case "has_native_node" when node is WorkspaceNode wsNn:
                wsNn.HasNativeNode = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "has_native_addon" when node is WorkspaceNode wsNa:
                wsNa.HasNativeAddon = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "has_dotnet_dll" when node is WorkspaceNode wsDn:
                wsDn.HasDotNetDll = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "indexed_at" when node is WorkspaceNode wsIdx:
                wsIdx.IndexedAt = value;
                return;
            case "dbcontext_base_conn" when node is Layer1_Physical.FileNode fileDbc:
                fileDbc.DbContextBaseConn = value;
                return;
            case "dbcontext_classes" when node is Layer1_Physical.FileNode fileDbCls:
                fileDbCls.DbContextClasses = value;
                return;
            case "declared_schema_raw" when node is Layer1_Physical.FileNode fileSch:
                fileSch.DeclaredSchemaRaw = value;
                return;
            case "verified_db_signals" when node is Layer1_Physical.FileNode fileVdb:
                fileVdb.VerifiedDbSignals = value;
                return;
            case "rust_topology_summary" when node is Layer1_Physical.FileNode fileRts:
                fileRts.RustTopologySummary = value;
                return;
            case "rust_topology_body_summary" when node is Layer1_Physical.FileNode fileRtbs:
                fileRtbs.RustTopologyBodySummary = value;
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
            case "archetype" when node is Layer2_Boundaries.ProjectNode pArch:
                pArch.Archetype = TaxonomyEnumExtendedExtensions.ParseProjectArchetype(value);
                return;
            case "primary_role" when node is Layer2_Boundaries.ProjectNode pPr:
                pPr.PrimaryRole = value;
                return;
            case "secondary_roles" when node is Layer2_Boundaries.ProjectNode pSr:
                pSr.SecondaryRoles = value;
                return;
            case "all_roles" when node is Layer2_Boundaries.ProjectNode pAr:
                pAr.AllRoles = value;
                return;
            case "db_driver_engine" when node is Layer2_Boundaries.ProjectNode pDde:
                pDde.DbDriverEngine = value;
                return;
            case "db_driver_type" when node is Layer2_Boundaries.ProjectNode pDdt:
                pDdt.DbDriverFamily = value;
                return;
            case "db_driver_checked" when node is Layer2_Boundaries.ProjectNode pDdc:
                pDdc.DbDriverChecked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                return;
            case "commit_hash" or "head_commit" when node is Layer1_Physical.GitSettingsNode gsCh:
                gsCh.CommitHash = value;
                return;
            case "repo_name" when node is Layer1_Physical.GitSettingsNode gsRn:
                gsRn.RepoName = value;
                return;
        }

        node.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        node.Extensions[key] = value;
    }

    public static bool TryGetExtension(this IOntologyNode node, string key, out string? value)
    {
        switch (key)
        {
            case "is_exported":
                if (node.Flags.HasFlag(SymbolFlags.IsExported) ||
                    node is Layer3_Syntactic.FunctionNode { IsExported: true } ||
                    node is Layer3_Syntactic.TypeNode { IsExported: true })
                {
                    value = "true";
                    return true;
                }
                break;
            case "is_module_init" when node is Layer3_Syntactic.FunctionNode { IsModuleInit: true }:
                value = "true";
                return true;
            case "has_parse_issues" when node is Layer1_Physical.FileNode { HasParseIssues: true }:
                value = "true";
                return true;
            case "parse_issue_count" when node is Layer1_Physical.FileNode { ParseIssueCount: { } pic }:
                value = pic.ToString();
                return true;
            case "has_entity_or_table" when node is Layer1_Physical.FileNode { HasEntityOrTable: true }:
                value = "true";
                return true;
            case "is_implicit_schema":
                if (node.Flags.HasFlag(SymbolFlags.IsImplicitSchema) ||
                    node is Layer4_Semantic.DatabaseNode { IsImplicitSchema: true } ||
                    node is Layer4_Semantic.TableNode { IsImplicitSchema: true } ||
                    node is Layer4_Semantic.DataSetNode { IsImplicitSchema: true })
                {
                    value = "true";
                    return true;
                }
                break;
            case "enclosing_type" when node is Layer3_Syntactic.FunctionNode { EnclosingType: { Length: > 0 } enc }:
                value = enc;
                return true;
            case "df_return" when node is Layer3_Syntactic.FunctionNode { DataflowReturn: { Length: > 0 } dfr }:
                value = dfr;
                return true;
            case "df_writes" when node is Layer3_Syntactic.FunctionNode { DataflowWrites: { Length: > 0 } dfw }:
                value = dfw;
                return true;
            case "df_summary" when node is Layer3_Syntactic.FunctionNode { DataflowSummary: { Length: > 0 } dfs }:
                value = dfs;
                return true;
            case "graphql_field" when node is Layer3_Syntactic.FunctionNode { GraphqlField: { Length: > 0 } gqlf }:
                value = gqlf;
                return true;
            case "graphql_schema" when node is Layer3_Syntactic.FunctionNode { GraphqlSchema: { Length: > 0 } gqls }:
                value = gqls;
                return true;
            case "graphql_aliases" when node is Layer3_Syntactic.FunctionNode { GraphqlAliases: { Length: > 0 } gqla }:
                value = gqla;
                return true;
            case "messaging_direction" when node is Layer3_Syntactic.FunctionNode { MessagingDirection: { Length: > 0 } msgd }:
                value = msgd;
                return true;
            case "dbcontext_base_conn" when node is Layer1_Physical.FileNode { DbContextBaseConn: { Length: > 0 } dbc }:
                value = dbc;
                return true;
            case "dbcontext_classes" when node is Layer1_Physical.FileNode { DbContextClasses: { Length: > 0 } dbcls }:
                value = dbcls;
                return true;
            case "declared_schema_raw" when node is Layer1_Physical.FileNode { DeclaredSchemaRaw: { Length: > 0 } dsr }:
                value = dsr;
                return true;
            case "verified_db_signals" when node is Layer1_Physical.FileNode { VerifiedDbSignals: { Length: > 0 } vdb }:
                value = vdb;
                return true;
            case "rust_topology_summary" when node is Layer1_Physical.FileNode { RustTopologySummary: { Length: > 0 } rts }:
                value = rts;
                return true;
            case "rust_topology_body_summary" when node is Layer1_Physical.FileNode { RustTopologyBodySummary: { Length: > 0 } rtbs }:
                value = rtbs;
                return true;
            case "db_driver_engine" when node is Layer2_Boundaries.ProjectNode { DbDriverEngine: { Length: > 0 } dde }:
                value = dde;
                return true;
            case "db_driver_type" when node is Layer2_Boundaries.ProjectNode { DbDriverFamily: { Length: > 0 } ddt }:
                value = ddt;
                return true;
            case "db_driver_checked" when node is Layer2_Boundaries.ProjectNode { DbDriverChecked: true }:
                value = "true";
                return true;
            case "framework" when node is Layer2_Boundaries.ProjectNode { Framework: { Length: > 0 } fw }:
                value = fw;
                return true;
            case "commit_hash" or "head_commit" when node is Layer1_Physical.GitSettingsNode { CommitHash: { Length: > 0 } ch }:
                value = ch;
                return true;
            case "repo_name" when node is Layer1_Physical.GitSettingsNode { RepoName: { Length: > 0 } rn }:
                value = rn;
                return true;
        }

        if (node.Extensions != null && node.Extensions.TryGetValue(key, out value))
            return true;

        value = null;
        return false;
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
        Layer3_Syntactic.CatchClauseNode cc => cc.Name,
        WorkspaceNode ws => ws.Name,
        _ => node.Id.Contains(':') ? node.Id[(node.Id.LastIndexOf(':') + 1)..] : node.Id
    };
}
