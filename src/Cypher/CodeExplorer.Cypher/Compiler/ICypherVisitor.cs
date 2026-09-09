using CodeExplorer.Cypher.Ast;

namespace CodeExplorer.Cypher.Compiler;

public interface ICypherVisitor<TResult>
{
    TResult VisitQuery(CypherQuery query);
    TResult VisitMatchClause(MatchClause matchClause);
    TResult VisitWhereClause(WhereClause whereClause);
    TResult VisitReturnClause(ReturnClause returnClause);
    TResult VisitProjectionItem(ProjectionItem projectionItem);
    TResult VisitOrderByClause(OrderByClause orderByClause);
    TResult VisitSkipClause(SkipClause skipClause);
    TResult VisitLimitClause(LimitClause limitClause);

    TResult VisitPathPattern(PathPattern pathPattern);
    TResult VisitNodePattern(NodePattern nodePattern);
    TResult VisitRelationshipPattern(RelationshipPattern relationshipPattern);

    TResult VisitExpression(Expression expression);
}
