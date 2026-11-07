using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java.Libraries;

public class NettyLibraryParser : ISemanticExtension
{
    private const string NettyEndpointPrefix = "TCP:/netty/";
    private const string SuperclassField = "superclass";
    private const string InboundAdapterName = "ChannelInboundHandlerAdapter";
    private const string SimpleHandlerName = "SimpleChannelInboundHandler";

    public string Name => "Netty";
    public string Id => "netty";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["io.netty.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Java.ClassDeclaration)) return;

        var superclassNode = node.GetField(SuperclassField);
        if (!superclassNode.IsValid()) return;

        var superText = superclassNode.Text;
        if (superText.Contains(InboundAdapterName, StringComparison.Ordinal) ||
            superText.Contains(SimpleHandlerName, StringComparison.Ordinal))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid())
            {
                references.Add(new Reference(scopeSymbolId, NettyEndpointPrefix + nameNode.Text, OntologyConstants.Relationships.Exposes));
            }
        }
    }
}
