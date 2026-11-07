using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class OpenTelemetryDotNetLibraryParser : ISemanticExtension
{
    public string Name => "OpenTelemetry .NET";
    public string Id => "opentelemetry-dotnet";
    public SemanticExtensionType Type => SemanticExtensionType.Library;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "OpenTelemetry",
        "OpenTelemetry.*",
        "System.Diagnostics.DiagnosticSource"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name) ?? (func.Children.Count > 0 ? func.Children[^1] : default);
        if (!nameNode.IsValid()) return;

        if (nameNode.Text is "StartActivity" or "AddOpenTelemetry")
        {
            references.Add(new Reference(scopeSymbolId, "otel:" + nameNode.Text, OntologyConstants.Relationships.Calls));
        }
    }
}
