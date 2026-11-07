using System.Diagnostics.CodeAnalysis;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser
{
    public static class NodeExtensions
    {
        public static bool IsValid([NotNullWhen(true)] this Node? node)
        {
            return node != null && node.Id != IntPtr.Zero;
        }

        public static string? GetChildFieldText(this Node? node, string fieldName)
        {
            if (node == null || node.Id == IntPtr.Zero) return null;
            var child = node.GetChildForField(fieldName);
            return (child != null && child.Id != IntPtr.Zero) ? child.Text : null;
        }

        public static Node? GetFunctionNode(this Node? node)
        {
            if (node == null || node.Id == IntPtr.Zero) return null;
            var func = node.GetChildForField(SyntaxFields.Function);
            if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0))
            {
                var firstChild = node.Children[0];
                func = firstChild.Id != IntPtr.Zero ? firstChild : null;
            }

            while (func.IsValid() && (func is UnaryExprSyntax { IsAwait: true } or UnaryExprSyntax { IsTryPropagate: true }
                                      or ParenthesizedExprSyntax { IsInterpolation: false }
                                      or TypeCastExprSyntax { IsGenericInstantiation: true }))
            {
                var inner = func.Children.FirstOrDefault(c => c.IsValid() && c is not SyntaxList { Kind: SyntaxListKind.TypeArguments });
                if (inner.IsValid())
                {
                    func = inner;
                }
                else
                {
                    break;
                }
            }

            return func.IsValid() ? func : null;
        }

        public static bool Is([NotNullWhen(true)] this Node? node, string expectedType)
        {
            return SyntaxKindResolver.Matches(node, expectedType);
        }

        public static bool IsAny([NotNullWhen(true)] this Node? node, params string[] expectedTypes)
        {
            if (!node.IsValid()) return false;
            for (int i = 0; i < expectedTypes.Length; i++)
            {
                if (SyntaxKindResolver.Matches(node, expectedTypes[i])) return true;
            }
            return false;
        }

        public static Node? GetField(this Node? node, string fieldName)
        {
            if (!node.IsValid()) return null;
            var child = node.GetChildForField(fieldName);
            return child.IsValid() ? child : null;
        }

        public static Node? FindChildOfType(this Node? node, string type)
        {
            if (!node.IsValid()) return null;
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (SyntaxKindResolver.Matches(child, type)) return child;
            }
            return null;
        }

        public static IEnumerable<Node> FindChildrenOfType(this Node? node, string type)
        {
            if (!node.IsValid()) yield break;
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (SyntaxKindResolver.Matches(child, type)) yield return child;
            }
        }

        public static Node? FindDescendantOfType(this Node? node, string type)
        {
            if (!node.IsValid()) return null;
            var queue = new Queue<Node>();
            queue.Enqueue(node);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                for (int i = 0; i < curr.Children.Count; i++)
                {
                    var child = curr.Children[i];
                    if (child.IsValid())
                    {
                        if (SyntaxKindResolver.Matches(child, type)) return child;
                        queue.Enqueue(child);
                    }
                }
            }

            return null;
        }

        public static IEnumerable<Node> FindDescendantsOfType(this Node? node, string type)
        {
            if (!node.IsValid()) yield break;
            var queue = new Queue<Node>();
            queue.Enqueue(node);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                for (int i = 0; i < curr.Children.Count; i++)
                {
                    var child = curr.Children[i];
                    if (child.IsValid())
                    {
                        if (SyntaxKindResolver.Matches(child, type)) yield return child;
                        queue.Enqueue(child);
                    }
                }
            }
        }

        public static bool IsContainedWithin(this Node? inner, Node? outer)
        {
            if (!inner.IsValid() || !outer.IsValid()) return false;
            if (inner.Tree != outer.Tree) return false;
            if (inner.StartPosition.Row < outer.StartPosition.Row) return false;
            if (inner.StartPosition.Row == outer.StartPosition.Row && inner.StartPosition.Column < outer.StartPosition.Column) return false;
            if (inner.EndPosition.Row > outer.EndPosition.Row) return false;
            if (inner.EndPosition.Row == outer.EndPosition.Row && inner.EndPosition.Column > outer.EndPosition.Column) return false;
            return true;
        }
    }
}