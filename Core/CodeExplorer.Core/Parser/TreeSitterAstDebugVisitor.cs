using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class TreeSitterAstDebugVisitor : TreeSitterAstVisitor
{
    protected override void VisitNode(Node node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var textSnippet = node.Text.Replace("\r", "").Replace("\n", " ");
        if (textSnippet.Length > 60)
        {
            textSnippet = textSnippet.Substring(0, 57) + "...";
        }
        Console.WriteLine($"{indent}Type: {node.Type}, Text: [{textSnippet}]");
    }
}
