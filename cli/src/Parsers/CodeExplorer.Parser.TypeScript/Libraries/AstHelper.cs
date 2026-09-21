using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public static class AstHelper
{
    public static string? ResolveStringOrTemplate(Node? argNode)
    {
        if (!argNode.IsValid()) return null;

        if (IsStringLiteralNode(argNode))
        {
            var text = argNode.Text.Trim('\'', '"', '`');
            if (text.Contains('\n') || text.Length > 500) return null;

            var routeMatch = Regex.Match(text, @"getServiceDomainByRoute\s*\(\s*['""]([^'""]+)['""]");
            if (routeMatch.Success)
            {
                var routeKey = routeMatch.Groups[1].Value;
                if (RouteDictionaryRegistry.TryResolve(routeKey, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return CombineServiceAndPath(rService, cleanPath);
                }
            }

            return NormalizeResolvedUrl(text);
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var varName = argNode.Text;
            if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
            {
                var cleanPath = rPath.Split('?')[0];
                return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
            }

            var val = FindVariableInitializerInAst(argNode, varName);
            if (val != null)
            {
                return NormalizeResolvedUrl(val);
            }
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            if (RouteDictionaryRegistry.TryResolve(argNode.Text, out var rPath, out var rService))
            {
                var cleanPath = rPath.Split('?')[0];
                return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
            }

            var prop = argNode.GetField(TreeSitterSyntax.Fields.Property);
            if (prop.IsValid() && RouteDictionaryRegistry.TryResolve(prop.Text, out rPath, out rService))
            {
                var cleanPath = rPath.Split('?')[0];
                return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
            }
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var firstArg = ExtractFirstStringArgument(argNode);
            if (!string.IsNullOrEmpty(firstArg))
            {
                return NormalizeResolvedUrl(firstArg);
            }

            var func = argNode.GetFunctionNode();
            if (func.IsValid())
            {
                var funcText = func.Text;
                if (funcText.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
                {
                    funcText = funcText[5..];
                }

                if (RouteDictionaryRegistry.TryResolve(funcText, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
                }

                if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var prop = func.GetField(TreeSitterSyntax.Fields.Property);
                    if (prop.IsValid() && RouteDictionaryRegistry.TryResolve(prop.Text, out rPath, out rService))
                    {
                        var cleanPath = rPath.Split('?')[0];
                        return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
                    }
                }
            }
        }

        return null;
    }

    public static string NormalizeResolvedUrl(string raw)
    {
        return RouteDictionaryRegistry.NormalizeResolvedUrl(raw);
    }

    public static string? ExtractFirstStringArgument(Node? callNode)
    {
        if (!callNode.IsValid()) return null;

        var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments)
            ?? callNode.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);

        if (argList.IsValid())
        {
            var firstArg = argList.Children.FirstOrDefault(c =>
                c.IsValid() && IsStringLiteralNode(c));

            if (firstArg.IsValid())
            {
                return firstArg.Text.Trim('\'', '"', '`');
            }

            var firstIdent = argList.Children.FirstOrDefault(c => c.IsValid() && c.Is(TreeSitterSyntax.TypeScript.Identifier));
            if (firstIdent.IsValid())
            {
                return ResolveStringOrTemplate(firstIdent);
            }
        }

        return null;
    }

    public static List<Node> GetCallArguments(Node? callNode)
    {
        var result = new List<Node>();
        if (!callNode.IsValid()) return result;

        var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments)
            ?? callNode.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);

        if (argList.IsValid())
        {
            foreach (var child in argList.Children)
            {
                if (child.IsValid() && !string.IsNullOrEmpty(child.Type) && (char.IsLetter(child.Type[0]) || child.Type[0] == '_'))
                {
                    result.Add(child);
                }
            }
        }

        return result;
    }

    public static bool TryGetMemberAccess(Node? callNode, out Node? objNode, out string? propName)
    {
        objNode = null;
        propName = null;
        if (!callNode.IsValid()) return false;

        var func = callNode.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            objNode = func.GetField(TreeSitterSyntax.Fields.Object);
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);
            if (propNode.IsValid())
            {
                propName = propNode.Text;
                return true;
            }
        }

        return false;
    }

    public static bool IsStringLiteralNode(Node node)
    {
        return node.IsAny(TreeSitterSyntax.TypeScript.String,
                           TreeSitterSyntax.TypeScript.TemplateString,
                           TreeSitterSyntax.Common.StringLiteral,
                           TreeSitterSyntax.Go.InterpretedStringLiteral);
    }

    public static bool TryGetObjectProperty(Node? objectNode, string propertyName, out Node? valueNode)
    {
        valueNode = null;
        if (!objectNode.IsValid()) return false;

        foreach (var child in objectNode.Children)
        {
            if (child.IsAny(TreeSitterSyntax.TypeScript.Pair, TreeSitterSyntax.TypeScript.Property))
            {
                var keyNode = child.GetField(TreeSitterSyntax.Fields.Key) ??
                              child.GetField(TreeSitterSyntax.Fields.Name) ??
                              child.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.PropertyIdentifier, TreeSitterSyntax.TypeScript.Identifier));

                if (keyNode.IsValid() && string.Equals(keyNode.Text, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    valueNode = child.GetField(TreeSitterSyntax.Fields.Value);
                    if (!valueNode.IsValid() && child.Children.Count >= 3)
                    {
                        valueNode = child.Children[^1];
                    }
                    return valueNode.IsValid();
                }
            }
        }

        return false;
    }

    private static string? FindVariableInitializerInAst(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                    {
                        foreach (var decl in child.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid())
                                {
                                    if (IsStringLiteralNode(valNode))
                                    {
                                        var text = valNode.Text.Trim('\'', '"', '`');
                                        if (!text.Contains('\n') && text.Length <= 500)
                                        {
                                            return text;
                                        }
                                    }
                                    else if (valNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
                                    {
                                        var callRes = ResolveStringOrTemplate(valNode);
                                        if (!string.IsNullOrEmpty(callRes))
                                        {
                                            return callRes;
                                        }
                                    }
                                    else if (valNode.Is(TreeSitterSyntax.Common.BinaryExpression))
                                    {
                                        var right = valNode.GetField(TreeSitterSyntax.Fields.Right);
                                        if (right.IsValid() && IsStringLiteralNode(right))
                                        {
                                            return right.Text.Trim('\'', '"', '`');
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    private static string CombineServiceAndPath(string? service, string cleanPath)
    {
        if (string.IsNullOrEmpty(service) ||
            cleanPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return cleanPath;
        }
        return $"{service}{cleanPath}";
    }
}
