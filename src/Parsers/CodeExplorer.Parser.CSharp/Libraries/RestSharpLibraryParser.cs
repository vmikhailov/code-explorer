using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class RestSharpLibraryParser : ILibraryParser
{
    public string Name => "RestSharp";
    public string Id => "restsharp";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["RestSharp"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> RestClientCallMethods =
    [
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "PatchAsync",
        "GetJsonAsync", "PostJsonAsync", "PutJsonAsync", "DeleteJsonAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsRestSharpTarget(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsRestSharpTarget(node)) return ExtractRestSharpTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    private static bool IsRestSharpTarget(Node node)
    {
        // 1. Check object creation: new RestRequest(...) or new RestClient(...)
        if (node.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type)
                           ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName));
            if (typeNode.IsValid())
            {
                var typeName = typeNode.Text;
                if (typeName is "RestRequest" or "RestClient")
                {
                    return true;
                }
            }
        }

        // 2. Direct HTTP calls on RestClient: client.GetAsync("...") where first arg is a string
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
                if (nameChild.IsValid() && RestClientCallMethods.Contains(nameChild.Text))
                {
                    var expr = func.GetField(TreeSitterSyntax.Fields.Expression);
                    if (expr.IsValid() && expr.Text.ToLowerInvariant().Contains("client"))
                    {
                        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                        var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                        if (firstArg.IsValid())
                        {
                            var valNode = firstArg.Children.FirstOrDefault();
                            if (valNode.IsValid() && (valNode.Type.Contains("string") || valNode.Is(TreeSitterSyntax.Common.BinaryExpression)))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    private static string? ExtractRestSharpTarget(Node node)
    {
        if (node.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type)
                           ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName));
            var typeName = typeNode.IsValid() ? typeNode.Text : null;

            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                if (firstArg.IsValid())
                {
                    var valNode = firstArg.Children.FirstOrDefault();
                    if (valNode.IsValid())
                    {
                        var text = ExtractText(valNode);
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (typeName == "RestClient")
                            {
                                return NormalizeUrl(text);
                            }
                            if (typeName == "RestRequest")
                            {
                                if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
                                {
                                    return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
                                }
                                return text.StartsWith("/") ? text : "/" + text;
                            }
                        }
                    }
                }
            }
        }
        else if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            if (firstArg.IsValid())
            {
                var valNode = firstArg.Children.FirstOrDefault();
                if (valNode.IsValid())
                {
                    var text = ExtractText(valNode);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return NormalizeUrl(text);
                    }
                }
            }
        }

        return "http:restsharp-service";
    }

    private static string? ExtractText(Node? node)
    {
        if (!node.IsValid()) return null;

        if (node.Type.Contains("string"))
        {
            return node.Text.Trim('"');
        }
        if (node.Is(TreeSitterSyntax.Common.BinaryExpression))
        {
            var left = node.GetField(TreeSitterSyntax.Fields.Left) ?? (node.Children.Count > 0 ? node.Children[0] : null);
            if (left.IsValid() && left.Type.Contains("string"))
            {
                return left.Text.Trim('"');
            }
        }
        if (node.Is(TreeSitterSyntax.CSharp.InterpolatedStringExpression))
        {
            return node.Text.Trim('$', '"');
        }
        if (node.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            var val = firstArg?.Children.FirstOrDefault();
            if (val.IsValid()) return ExtractText(val);
        }
        if (node.Is(TreeSitterSyntax.Common.Identifier))
        {
            var resolved = TryResolveVariableUri(node, node.Text);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }
        return null;
    }

    private static string? TryResolveVariableUri(Node node, string varName)
    {
        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement, TreeSitterSyntax.CSharp.CompilationUnit, TreeSitterSyntax.CSharp.ClassDeclaration))
            {
                var uri = FindUriInScope(current, varName, 0);
                if (!string.IsNullOrEmpty(uri)) return uri;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? FindUriInScope(Node scopeNode, string targetVar, int depth)
    {
        if (depth > 5) return null;

        foreach (var child in scopeNode.Children)
        {
            if (child.IsAny(TreeSitterSyntax.CSharp.LocalDeclarationStatement, TreeSitterSyntax.CSharp.VariableDeclaration, TreeSitterSyntax.CSharp.FieldDeclaration, TreeSitterSyntax.CSharp.GlobalStatement))
            {
                var decls = FindNodesOfType(child, TreeSitterSyntax.CSharp.VariableDeclarator);
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ?? decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                    if (nameNode.IsValid() && nameNode.Text == targetVar)
                    {
                        var valueNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                        if (!valueNode.IsValid())
                        {
                            var eqClause = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                            if (eqClause.IsValid() && eqClause.Children.Count > 1)
                            {
                                valueNode = eqClause.Children[1];
                            }
                            else if (decl.Children.Count >= 3 && decl.Children[1].Text == "=")
                            {
                                valueNode = decl.Children[2];
                            }
                        }

                        if (valueNode.IsValid())
                        {
                            var extracted = ExtractText(valueNode);
                            if (!string.IsNullOrEmpty(extracted)) return extracted;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<Node> FindNodesOfType(Node root, string targetType)
    {
        var result = new List<Node>();
        void Recurse(Node current)
        {
            if (current.Type == targetType)
            {
                result.Add(current);
                return;
            }
            foreach (var child in current.Children)
            {
                Recurse(child);
            }
        }
        Recurse(root);
        return result;
    }

    private static string NormalizeUrl(string text)
    {
        text = text.Trim().Trim('"');
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}:{uri.Host}{uri.AbsolutePath}";
        }
        return text.StartsWith("/") ? text : $"http:{text}";
    }
}