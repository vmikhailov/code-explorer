using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Cypher.Common;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go;

public partial class GoFileVisitor
{
    private readonly Dictionary<string, SyntacticSymbol> _typesByName = new(StringComparer.Ordinal);

    private static string? ExtractReceiverTypeName(Node node)
    {
        if (node is FunctionDeclSyntax fnDecl && fnDecl.Receiver != null)
        {
            var typeStr = fnDecl.Receiver.ParameterType?.Name ?? fnDecl.Receiver.ParameterType?.Text;
            if (string.IsNullOrEmpty(typeStr))
            {
                var raw = fnDecl.Receiver.Text;
                if (!string.IsNullOrEmpty(raw))
                {
                    var parts = raw.Split([' ', '\t', '\r', '\n', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1) typeStr = parts[^1];
                    else typeStr = raw;
                }
            }
            if (!string.IsNullOrEmpty(typeStr))
            {
                return typeStr.Trim().TrimStart('*', '&');
            }
        }
        return null;
    }

    protected override void VisitClassDeclaration(Node node, int depth)
    {
        var name = ExtractIdentifier(node);
        if (!string.IsNullOrEmpty(name) && _typesByName.TryGetValue(name, out var existing))
        {
            SymbolStack.Push(existing);
            PushedNodeIds.Push(node.Id);
            try
            {
                VisitChildren(node, depth);
            }
            finally
            {
                if (PushedNodeIds.Count > 0 && PushedNodeIds.Peek() == node.Id)
                {
                    PushedNodeIds.Pop();
                    SymbolStack.Pop();
                }
            }
            return;
        }

        base.VisitClassDeclaration(node, depth);

        if (!string.IsNullOrEmpty(name))
        {
            var created = RootSymbol.Children.LastOrDefault(c => c.Name == name);
            if (created != null)
            {
                _typesByName[name] = created;
            }
        }
    }

    protected override void VisitMethodDeclaration(Node node, int depth)
    {
        var receiverTypeName = ExtractReceiverTypeName(node);
        if (string.IsNullOrEmpty(receiverTypeName))
        {
            base.VisitMethodDeclaration(node, depth);
            return;
        }

        if (!_typesByName.TryGetValue(receiverTypeName, out var targetSymbol))
        {
            targetSymbol = RootSymbol.Children.FirstOrDefault(c =>
                c.Kind is SyntacticSymbolKind.Class or SyntacticSymbolKind.Struct or SyntacticSymbolKind.Interface
                && c.Name == receiverTypeName);

            if (targetSymbol == null)
            {
                targetSymbol = new SyntacticSymbol(SyntacticSymbolKind.Class, receiverTypeName, node);
                targetSymbol.Properties["type_kind"] = "struct";
                RootSymbol.Children.Add(targetSymbol);
            }
            _typesByName[receiverTypeName] = targetSymbol;
        }

        SymbolStack.Push(targetSymbol);
        PushedNodeIds.Push(node.Id);

        try
        {
            base.VisitMethodDeclaration(node, depth);
        }
        finally
        {
            if (PushedNodeIds.Count > 0 && PushedNodeIds.Peek() == node.Id)
            {
                PushedNodeIds.Pop();
                SymbolStack.Pop();
            }
        }
    }
}
