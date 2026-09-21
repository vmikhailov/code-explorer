using System.Diagnostics.CodeAnalysis;
using TreeSitter;

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
            var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
            if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0))
            {
                var firstChild = node.Children[0];
                func = firstChild.Id != IntPtr.Zero ? firstChild : null;
            }

            while (func.IsValid() && (func.Type is "await_expression" or "parenthesized_expression"))
            {
                var inner = func.Children.FirstOrDefault(c => c.IsValid() && c.Type != "await" && c.Type != "(" && c.Type != ")");
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
            return node.IsValid() && node.Type == expectedType;
        }

        public static bool IsAny([NotNullWhen(true)] this Node? node, params string[] expectedTypes)
        {
            if (!node.IsValid()) return false;
            for (int i = 0; i < expectedTypes.Length; i++)
            {
                if (node.Type == expectedTypes[i]) return true;
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
                if (child.IsValid() && child.Type == type) return child;
            }
            return null;
        }

        public static IEnumerable<Node> FindChildrenOfType(this Node? node, string type)
        {
            if (!node.IsValid()) yield break;
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.IsValid() && child.Type == type) yield return child;
            }
        }
    }
}