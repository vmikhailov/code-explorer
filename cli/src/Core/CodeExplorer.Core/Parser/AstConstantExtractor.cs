using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal AST-driven constant, enum, and compile-time expression extractor.
/// Delegates language-specific declaration discovery to registered IFileParser implementations
/// and performs topological multi-pass evaluation for dependent constants (e.g. A + B, template strings).
/// </summary>
public static class AstConstantExtractor
{
    private record PendingConstant(string Key, Node? ValueNode, string? InitialLiteral);

    public static void ExtractAndRegister(SyntaxTree syntaxTree, string? projectName = null)
    {
        if (syntaxTree?.Tree?.RootNode == null || syntaxTree.Tree.RootNode.Children.Count == 0) return;
        ExtractAndRegister(syntaxTree.Tree.RootNode, syntaxTree.FileParser, projectName);
    }

    public static void ExtractAndRegister(Node rootNode, IFileParser? parser, string? projectName = null)
    {
        if (!rootNode.IsValid() || parser == null) return;

        var pending = new List<PendingConstant>();
        parser.ExtractDeclarations(rootNode, (key, valNode, initialLiteral) =>
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                pending.Add(new PendingConstant(key, valNode, initialLiteral));
            }
        });

        if (pending.Count > 0)
        {
            ResolveTopologicalConstants(pending, projectName);
        }
    }

    public static void ExtractAndRegister(string filePath, string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var fileParser = WorkspaceIndexer.GetParserForFile(filePath);
        if (fileParser == null) return;

        try
        {
            var language = SyntaxTree.GetLanguage(fileParser.LanguageName);
            using var parser = new TreeSitter.Parser(language);
            using var tree = parser.Parse(content);

            if (tree?.RootNode != null && tree.RootNode.Children.Count > 0)
            {
                ExtractAndRegister(tree.RootNode, fileParser, projectName);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"ExtractAndRegister failed for {filePath}: {ex}", ex);
        }
    }

    private static void ResolveTopologicalConstants(List<PendingConstant> pending, string? projectName)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Multi-pass topological resolution
        for (int pass = 0; pass < 10; pass++)
        {
            var progress = false;

            foreach (var item in pending)
            {
                if (resolved.Contains(item.Key)) continue;

                if (item.InitialLiteral != null)
                {
                    ConstantRegistry.Register(projectName, item.Key, item.InitialLiteral);
                    resolved.Add(item.Key);
                    progress = true;
                    continue;
                }

                if (item.ValueNode.IsValid())
                {
                    if (AstValueResolver.TryResolveString(item.ValueNode, projectName, out var val) &&
                        !string.IsNullOrWhiteSpace(val))
                    {
                        ConstantRegistry.Register(projectName, item.Key, val);
                        resolved.Add(item.Key);
                        progress = true;
                    }
                }
            }

            if (!progress) break;
        }

        // Fallback pass: any remaining unresolved constants that are literal strings
        foreach (var item in pending)
        {
            if (resolved.Contains(item.Key)) continue;

            if (item.ValueNode.IsValid())
            {
                var text = item.ValueNode.Text.Trim();
                if (AstValueResolver.IsQuoted(text) && !text.Contains('\n') && text.Length is > 0 and < 300)
                {
                    var unquoted = AstValueResolver.Unquote(text);
                    if (!string.IsNullOrWhiteSpace(unquoted))
                    {
                        ConstantRegistry.Register(projectName, item.Key, unquoted);
                        resolved.Add(item.Key);
                    }
                }
            }
        }
    }
}
