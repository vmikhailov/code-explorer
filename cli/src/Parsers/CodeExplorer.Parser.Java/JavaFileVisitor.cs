using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java;

public class JavaFileVisitor : BaseParserVisitor
{
    public JavaFileVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
        : base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
        // Ensure core route and HTTP client extensions are active even for snippet tests without explicit import statements
        foreach (var ext in fileParser.SemanticExtensions)
        {
            if (ext.Id is "spring-web" or "java-http" or "quarkus-jaxrs" or "quarkus-panache" or "quarkus-messaging" or "micronaut" or "dubbo")
            {
                RegisterActiveExtension(ext);
            }
        }
    }
}

