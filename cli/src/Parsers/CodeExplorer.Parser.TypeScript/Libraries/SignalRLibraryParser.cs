using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SignalRLibraryParser : DeclarativeSemanticExtension
{
    public SignalRLibraryParser()
    {
        Type = SemanticExtensionType.ApiClient;
        Name = "SignalR Client";
        Id = "signalr-client";
        SupportedPatterns =
        [
            "@microsoft/signalr",
            "@aspnet/signalr"
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsSignalRCall(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsSignalRCall(node)) return null;
                    var args = AstHelper.GetCallArguments(node);
                    if (args.Count > 0 && args[0].IsValid())
                    {
                        var resolved = AstHelper.ResolveStringOrTemplate(args[0]);
                        if (!string.IsNullOrWhiteSpace(resolved))
                        {
                            if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                            {
                                var path = uri.AbsolutePath.TrimStart('/');
                                return string.IsNullOrEmpty(path) ? $"ws:{uri.Host}" : $"ws:{uri.Host}/{path}";
                            }
                            var cleanPath = resolved.StartsWith('/') ? resolved : $"/{resolved}";
                            return $"ws:*{cleanPath}";
                        }
                    }
                    return "ws:*";
                }
            }
        ];
    }

    private static bool IsSignalRCall(Node node)
    {
        if (!node.IsValid() || !node.Is(TreeSitterSyntax.TypeScript.CallExpression))
            return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
            return false;

        var prop = func.GetField(TreeSitterSyntax.Fields.Property);
        if (!prop.IsValid()) return false;

        return string.Equals(prop.Text, "withUrl", StringComparison.OrdinalIgnoreCase);
    }
}
