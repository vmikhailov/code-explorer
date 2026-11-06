using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public partial class SqliteCompiler
{
    #region ICypherVisitor boilerplates

    public string VisitMatchClause(MatchClause matchClause) => "";
    public string VisitWhereClause(WhereClause whereClause) => VisitExpression(whereClause.Predicate);
    public string VisitWithClause(WithClause withClause) => "";
    public string VisitUnwindClause(UnwindClause unwindClause) => "";
    public string VisitReturnClause(ReturnClause returnClause) => "";
    public string VisitProjectionItem(ProjectionItem projectionItem) => "";
    public string VisitOrderByClause(OrderByClause orderByClause) => "";
    public string VisitSkipClause(SkipClause skipClause) => "";
    public string VisitLimitClause(LimitClause limitClause) => "";
    public string VisitCallClause(CallClause callClause) => "";
    public string VisitUnionClause(UnionClause unionClause) => "";
    public string VisitPathPattern(PathPattern pathPattern) => "";
    public string VisitNodePattern(NodePattern nodePattern) => "";
    public string VisitRelationshipPattern(RelationshipPattern relationshipPattern) => "";

    #endregion
}
