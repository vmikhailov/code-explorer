using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptSemanticAnalyzer : BaseSemanticAnalyzer
{
    public TypeScriptSemanticAnalyzer() : base(new TypeScriptParser().LibraryParsers)
    {
    }

    public TypeScriptSemanticAnalyzer(IReadOnlyList<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }

    public override Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        // Debug AST Visitor to print out TS AST for files being parsed
        var files = GetFiles(projectNode);
        foreach (var file in files)
        {
            if (ctx.ParsedAsts.TryGetValue(file.FullPath, out var astInfo))
            {
                if (astInfo.Tree?.RootNode != null)
                {
                    Console.WriteLine($"=== TS AST Visitor Debug: {file.Path} ===");
                    var visitor = new TypeScriptAstDebugVisitor();
                    visitor.Visit(astInfo.Tree.RootNode);
                    Console.WriteLine($"=========================================");
                }
            }
        }

        return base.AnalyzeAndEnrichAsync(projectNode, ctx);
    }

    private static List<FileNode> GetFiles(IOntologyNode node)
    {
        var list = new List<FileNode>();
        if (node is FileNode fn)
        {
            list.Add(fn);
        }
        foreach (var child in node.Children)
        {
            list.AddRange(GetFiles(child));
        }
        return list;
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
