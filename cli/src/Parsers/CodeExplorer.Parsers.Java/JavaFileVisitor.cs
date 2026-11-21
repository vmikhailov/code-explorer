using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java;

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

    protected override void VisitParameter(Node node, int depth)
    {
        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (typeNode.IsValid() && nameNode.IsValid() && SymbolStack.Count > 0)
        {
            var typeName = typeNode.Text.Trim();
            var paramName = nameNode.Text.Trim();
            if (!string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(paramName))
            {
                RawTypeBindings.Add(new RawTypeBinding(paramName, typeName, string.Empty, SymbolStack.Peek().Name));
            }
        }

        base.VisitParameter(node, depth);
    }
}

