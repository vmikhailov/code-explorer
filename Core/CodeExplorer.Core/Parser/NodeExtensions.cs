using System;
using TreeSitter;

namespace CodeExplorer.Core.Parser
{
    public static class NodeExtensions
    {
        public static bool IsValid(this Node? node)
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
            var func = node.GetChildForField("function");
            if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0))
            {
                var firstChild = node.Children[0];
                return firstChild.Id != IntPtr.Zero ? firstChild : null;
            }
            return func.Id != IntPtr.Zero ? func : null;
        }
    }
}