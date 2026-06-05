using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptSemanticModel : BaseSemanticModel
{
    public TypeScriptSemanticModel(SyntaxTree syntaxTree)
        : base(new TypeScriptParser().LibraryParsers, syntaxTree)
    {
    }

    public TypeScriptSemanticModel(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree)
        : base(libraryParsers, syntaxTree)
    {
    }

    public override Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        // Debug AST Visitor to print out TS AST for the file being parsed
        var file = SyntaxTree.FileNode;
        if (SyntaxTree.Tree?.RootNode != null && file.FullPath.EndsWith("bq-routes-calc.service.ts"))
        {
            Console.WriteLine($"=== TS AST Visitor Debug: {file.Path} ===");
            var visitor = new TypeScriptAstDebugVisitor();
            visitor.Visit(SyntaxTree.Tree.RootNode);
            Console.WriteLine($"=========================================");
        }

        return base.AnalyzeAndEnrichAsync(projectNode, ctx);
    }
}

public class TypeScriptAstDebugVisitor
{
    public virtual void Visit(Node node, int depth = 0)
    {
        if (node.Id == IntPtr.Zero) return;

        VisitNode(node, depth);

        foreach (var child in node.Children)
        {
            Visit(child, depth + 1);
        }
    }

    protected virtual void VisitNode(Node node, int depth)
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
