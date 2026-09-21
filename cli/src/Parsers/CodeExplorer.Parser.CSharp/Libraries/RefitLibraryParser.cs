using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class RefitLibraryParser : ILibraryParser
{
    public string Name => "Refit";
    public string Id => "refit";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["Refit", "Refit.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RefitMethodAttributes =
    [
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options",
        "GetAttribute", "PostAttribute", "PutAttribute", "DeleteAttribute",
        "PatchAttribute", "HeadAttribute", "OptionsAttribute"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRefitMethodAttribute(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRefitMethodAttribute(node))
        {
            return ExtractRefitTarget(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsRefitMethodAttribute(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.Attribute)) return false;

        var nameNode = node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
        if (!nameNode.IsValid()) return false;

        var attrName = nameNode.Text;
        if (!RefitMethodAttributes.Contains(attrName)) return false;

        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration)) return true;
            if (current.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration)) return false;
            current = current.Parent;
        }
        return false;
    }

    private static string? ExtractRefitTarget(Node node)
    {
        var argList = node.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
        var routeVal = "/";
        if (argList.IsValid())
        {
            var arg = argList.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgument);
            if (arg.IsValid())
            {
                var strNode = arg.Children.FirstOrDefault(c => c.Type.Contains("string"));
                if (strNode.IsValid()) routeVal = strNode.Text.Trim('"');
            }
        }

        var current = node.Parent;
        string interfaceName = "api-client";
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration))
            {
                var idNode = current.GetField(TreeSitterSyntax.Fields.Name);
                if (idNode.IsValid())
                {
                    interfaceName = idNode.Text;
                    if (interfaceName.StartsWith("I") && interfaceName.Length > 1 && char.IsUpper(interfaceName[1]))
                    {
                        interfaceName = interfaceName[1..];
                    }
                    if (interfaceName.EndsWith("Api", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = interfaceName[..^"Api".Length];
                    }
                    else if (interfaceName.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = interfaceName[..^"Client".Length];
                    }
                    else if (interfaceName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = interfaceName[..^"Service".Length];
                    }
                }
                break;
            }
            current = current.Parent;
        }

        routeVal = "/" + routeVal.Trim('/');
        var cleanService = interfaceName.ToLowerInvariant();
        return $"{cleanService}{routeVal}";
    }
}
