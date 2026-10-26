using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java;

public class JavaFileVisitor : BaseParserVisitor
{
    public JavaFileVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        JavaParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
        : base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
        // Ensure core route and HTTP client extensions are active even for snippet tests without explicit import statements
        foreach (var ext in parser.SemanticExtensions)
        {
            if (ext.Id is "spring-web" or "java-http" && !SemanticExtensions.Contains(ext))
            {
                SemanticExtensions.Add(ext);
            }
        }
    }
}

