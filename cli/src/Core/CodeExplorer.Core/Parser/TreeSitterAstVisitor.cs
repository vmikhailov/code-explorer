using TreeSitter;

namespace CodeExplorer.Core.Parser;

public abstract class TreeSitterAstVisitor
{
    public virtual void Visit(Node node, int depth = 0)
    {
        if (node.Id == IntPtr.Zero) return;

        VisitNode(node, depth);
    }

    protected abstract void VisitNode(Node node, int depth);

    protected virtual void VisitClassDeclaration(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitInterfaceDeclaration(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitMethodDeclaration(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitFunctionDeclaration(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitVariableDeclaration(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitImportStatement(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitCallExpression(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitAttribute(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitStringLiteral(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitParameter(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitInheritanceClause(Node node, int depth) => VisitChildren(node, depth);
    protected virtual void VisitDefault(Node node, int depth) => VisitChildren(node, depth);

    protected virtual void VisitChildren(Node node, int depth)
    {
        foreach (var child in node.Children)
        {
            Visit(child, depth + 1);
        }
    }
}
