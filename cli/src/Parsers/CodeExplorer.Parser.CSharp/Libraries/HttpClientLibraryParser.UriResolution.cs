using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public partial class HttpClientLibraryParser
{
    private static string CleanServiceHost(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var h = raw.Trim();
        h = Regex.Replace(h, @"^[a-zA-Z0-9\+\-]+://", "");
        var slash = h.IndexOf('/');
        if (slash >= 0) h = h[..slash];
        var q = h.IndexOf('?');
        if (q >= 0) h = h[..q];
        return h.Trim();
    }

    private static string ExtractInterpolatedStringUri(Node node, Node scopeNode, int depth, HashSet<string>? visited = null)
    {
        if (depth > 8) return "*";
        var sb = new System.Text.StringBuilder();
        foreach (var child in node.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.StringContent))
            {
                sb.Append(child.Text);
            }
            else if (child.Is(TreeSitterSyntax.CSharp.Interpolation))
            {
                var expr = child.GetField(TreeSitterSyntax.Fields.Expression)
                           ?? child.Children.FirstOrDefault(c => c.Type is not "interpolation_brace" and not "{" and not "}");
                if (expr.IsValid())
                {
                    if (expr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
                    {
                        var argList = expr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                        if (argList.IsValid())
                        {
                            var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                            if (firstArg.IsValid())
                            {
                                var val = firstArg.Children.FirstOrDefault(c => c.Type != ",");
                                if (val.IsValid())
                                {
                                    if (val.Type.Contains("string"))
                                    {
                                        sb.Append(val.Text.Trim('"'));
                                        continue;
                                    }
                                    var resolved = TryResolveVariableUri(val, val.Text, depth + 1, visited);
                                    if (!string.IsNullOrEmpty(resolved))
                                    {
                                        sb.Append(resolved);
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                    else if (expr.Is(TreeSitterSyntax.Common.Identifier))
                    {
                        var resolved = TryResolveVariableUri(expr, expr.Text, depth + 1, visited);
                        if (!string.IsNullOrEmpty(resolved))
                        {
                            sb.Append(resolved);
                            continue;
                        }
                        if (ConstantRegistry.TryResolve(null, expr.Text, out var cVal))
                        {
                            sb.Append(cVal);
                            continue;
                        }
                    }

                    sb.Append('*');
                }
            }
        }

        return sb.ToString();
    }

    private static string? TryResolveNamedClientService(Node node)
    {
        var func = node.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var expr = func.GetField(TreeSitterSyntax.Fields.Expression);
            if (expr.IsValid())
            {
                var receiverName = expr.Text;
                var curr = node.Parent;
                while (curr.IsValid())
                {
                    if (curr.IsAny(TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.ClassDeclaration))
                    {
                        var createClientMatch = Regex.Match(curr.Text, Regex.Escape(receiverName) + @"\s*=\s*.*?CreateClient\s*\(\s*[""']([^""']+)[""']");
                        if (createClientMatch.Success)
                        {
                            return createClientMatch.Groups[1].Value;
                        }

                        if (curr.Is(TreeSitterSyntax.CSharp.ClassDeclaration))
                        {
                            var classNameNode = curr.GetField(TreeSitterSyntax.Fields.Name);
                            if (classNameNode.IsValid())
                            {
                                var clsName = classNameNode.Text;
                                if (RouteDictionaryRegistry.TryResolveServiceDomain(clsName, out var sDomain))
                                {
                                    return sDomain;
                                }
                            }
                        }
                    }
                    curr = curr.Parent;
                }
            }
        }
        return null;
    }

    private static string? TryResolveVariableUri(Node node, string varName, int depth = 0, HashSet<string>? visited = null)
    {
        if (depth > 8 || string.IsNullOrWhiteSpace(varName)) return null;
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visited.Add(varName)) return null;

        var current = node.Parent;
        while (current.IsValid())
        {
            if (current.IsAny(
                TreeSitterSyntax.CSharp.Block,
                TreeSitterSyntax.CSharp.DeclarationList,
                TreeSitterSyntax.CSharp.MethodDeclaration,
                TreeSitterSyntax.CSharp.LocalFunctionStatement,
                TreeSitterSyntax.CSharp.ClassDeclaration,
                TreeSitterSyntax.CSharp.StructDeclaration,
                TreeSitterSyntax.CSharp.RecordDeclaration,
                TreeSitterSyntax.CSharp.CompilationUnit))
            {
                var uri = FindUriInScope(current, varName, depth + 1, visited);
                if (!string.IsNullOrEmpty(uri)) return uri;
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? FindUriInScope(Node scopeNode, string targetVar, int depth, HashSet<string>? visited = null)
    {
        if (depth > 8) return null;

        if (scopeNode.IsAny(
            TreeSitterSyntax.CSharp.ClassDeclaration,
            TreeSitterSyntax.CSharp.StructDeclaration,
            TreeSitterSyntax.CSharp.RecordDeclaration))
        {
            var body = scopeNode.FindChildOfType(TreeSitterSyntax.CSharp.DeclarationList);
            if (body.IsValid())
            {
                var uri = FindUriInScope(body, targetVar, depth + 1, visited);
                if (!string.IsNullOrEmpty(uri)) return uri;
            }
        }

        foreach (var child in scopeNode.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.DeclarationList))
            {
                var uri = FindUriInScope(child, targetVar, depth + 1, visited);
                if (!string.IsNullOrEmpty(uri)) return uri;
            }

            if (child.IsAny(
                TreeSitterSyntax.CSharp.FieldDeclaration,
                TreeSitterSyntax.CSharp.LocalDeclarationStatement,
                TreeSitterSyntax.CSharp.VariableDeclaration,
                TreeSitterSyntax.CSharp.UsingStatement))
            {
                var decls = FindNodesOfType(child, TreeSitterSyntax.CSharp.VariableDeclarator);
                foreach (var decl in decls)
                {
                    var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name)
                                   ?? decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                    if (nameNode.IsValid() && nameNode.Text == targetVar)
                    {
                        var valueNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                        if (valueNode.IsValid())
                        {
                            var uri = ExtractUriFromExpression(valueNode, scopeNode, depth + 1, visited);
                            if (!string.IsNullOrEmpty(uri)) return uri;
                        }
                    }
                }
            }

            if (child.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
            {
                var nameNode = child.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && nameNode.Text == targetVar)
                {
                    var arrow = child.FindChildOfType(TreeSitterSyntax.CSharp.ArrowExpressionClause);
                    if (arrow.IsValid() && arrow.Children.Count > 0)
                    {
                        var uri = ExtractUriFromExpression(arrow.Children[^1], scopeNode, depth + 1, visited);
                        if (!string.IsNullOrEmpty(uri)) return uri;
                    }
                    var valNode = child.GetField(TreeSitterSyntax.Fields.Value);
                    if (valNode.IsValid())
                    {
                        var uri = ExtractUriFromExpression(valNode, scopeNode, depth + 1, visited);
                        if (!string.IsNullOrEmpty(uri)) return uri;
                    }
                }
            }

            if (child.Is(TreeSitterSyntax.CSharp.ExpressionStatement))
            {
                var assign = child.FindChildOfType(TreeSitterSyntax.Common.AssignmentExpression);
                if (assign.IsValid())
                {
                    var left = assign.GetField(TreeSitterSyntax.Fields.Left);
                    if (left.IsValid() && left.Text == targetVar)
                    {
                        var right = assign.GetField(TreeSitterSyntax.Fields.Right)
                                    ?? (assign.Children.Count >= 3 ? assign.Children[2] : null);
                        if (right.IsValid())
                        {
                            var uri = ExtractUriFromExpression(right, scopeNode, depth + 1, visited);
                            if (!string.IsNullOrEmpty(uri)) return uri;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractUriFromExpression(Node? expr, Node scopeNode, int depth, HashSet<string>? visited = null)
    {
        if (!expr.IsValid() || depth > 8) return null;

        if (expr.Is(TreeSitterSyntax.CSharp.InterpolatedStringExpression))
        {
            return ExtractInterpolatedStringUri(expr, scopeNode, depth + 1, visited);
        }

        if (expr.Type.Contains("string"))
        {
            var raw = expr.Text.Trim('"');
            if (raw.StartsWith('$') || raw.StartsWith("@$") || raw.StartsWith("$@"))
            {
                return ExtractInterpolatedStringUri(expr, scopeNode, depth + 1, visited);
            }
            return raw;
        }

        if (expr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var argList = expr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                if (firstArg.IsValid())
                {
                    var val = firstArg.Children.FirstOrDefault(c => c.Type != ",");
                    if (val.IsValid())
                    {
                        if (val.Type.Contains("string")) return val.Text.Trim('"');
                        var resolved = TryResolveVariableUri(val, val.Text, depth + 1, visited);
                        if (!string.IsNullOrEmpty(resolved)) return resolved;
                        if (ConstantRegistry.TryResolve(null, val.Text, out var cVal)) return cVal;
                    }
                }
            }
        }

        if (expr is CodeExplorer.Core.Parser.Syntax.CallExprSyntax { CallKind: CodeExplorer.Core.Parser.Syntax.CallSyntaxKind.Constructor })
        {
            var typeNode = expr.GetField(TreeSitterSyntax.Fields.Type)
                           ?? expr.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
            var typeName = typeNode.IsValid() ? typeNode.Text : "";

            var argList = expr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();

                if (typeName.Contains("Uri") || (string.IsNullOrEmpty(typeName) && args.Count > 0 && args[0].Text.Contains("http")))
                {
                    var firstArg = args.FirstOrDefault();
                    if (firstArg.IsValid())
                    {
                        var val = firstArg.Children.FirstOrDefault();
                        if (val.IsValid())
                        {
                            if (val.Type.Contains("string")) return val.Text.Trim('"');
                            return ExtractUriFromExpression(val, scopeNode, depth + 1, visited);
                        }
                    }
                }
                else if (typeName.Contains("HttpRequestMessage") || string.IsNullOrEmpty(typeName))
                {
                    // Check if second argument is URI: new HttpRequestMessage(HttpMethod.Post, remoteServiceBaseUrl)
                    if (args.Count >= 2)
                    {
                        var uriArg = args[1].Children.FirstOrDefault();
                        if (uriArg.IsValid())
                        {
                            if (uriArg.Type.Contains("string")) return uriArg.Text.Trim('"');
                            if (uriArg.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression) || uriArg.Type == "implicit_object_creation_expression")
                            {
                                var inner = ExtractUriFromExpression(uriArg, scopeNode, depth + 1, visited);
                                if (!string.IsNullOrEmpty(inner)) return inner;
                            }
                            var resolved = TryResolveVariableUri(uriArg, uriArg.Text, depth + 1, visited);
                            if (!string.IsNullOrEmpty(resolved)) return resolved;
                        }
                    }

                    foreach (var arg in args)
                    {
                        var val = arg.Children.FirstOrDefault();
                        if (!val.IsValid()) continue;

                        if (val.Type.Contains("string"))
                        {
                            return val.Text.Trim('"');
                        }
                        if (val.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression) || val.Type == "implicit_object_creation_expression")
                        {
                            var innerUri = ExtractUriFromExpression(val, scopeNode, depth + 1, visited);
                            if (!string.IsNullOrEmpty(innerUri)) return innerUri;
                        }
                        if (val.Is(TreeSitterSyntax.Common.Identifier) && val.Text != "HttpMethod")
                        {
                            var resolved = TryResolveVariableUri(val, val.Text, depth + 1, visited);
                            if (!string.IsNullOrEmpty(resolved)) return resolved;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static List<Node> FindNodesOfType(Node root, string nodeType)
    {
        var list = new List<Node>();
        CollectNodes(root, nodeType, list, 0);
        return list;
    }

    private static void CollectNodes(Node node, string nodeType, List<Node> result, int depth)
    {
        if (depth > 64) return;
        if (node.Is(nodeType))
        {
            result.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectNodes(child, nodeType, result, depth + 1);
        }
    }

    private static bool HasFluentHttpRequestInReceiver(Node? expr, int depth = 0, HashSet<string>? visited = null)
    {
        if (depth > 8) return false;
        var curr = expr;
        var steps = 0;
        while (curr.IsValid() && ++steps <= 32)
        {
            if (curr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var func = curr.GetFunctionNode();
                if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    var name = func.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                    if (name is "GetRequest" or "PostRequest" or "PutRequest" or "DeleteRequest" or "PatchRequest" or "AddUrlSegment" or "AddUriParameter")
                    {
                        return true;
                    }
                }
                curr = func.IsValid() ? func.GetField(TreeSitterSyntax.Fields.Expression) : null;
            }
            else if (curr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var name = curr.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                if (name != null && (name.Contains("HttpClientResource") || curr.Text.Contains("HttpClientResource")))
                {
                    return true;
                }
                curr = curr.GetField(TreeSitterSyntax.Fields.Expression);
            }
            else if (curr.Is(TreeSitterSyntax.Common.Identifier))
            {
                visited ??= new HashSet<string>(StringComparer.Ordinal);
                if (!visited.Add(curr.Text)) break;
                var initNode = FindVariableInitializer(curr, curr.Text);
                if (initNode.IsValid())
                {
                    return HasFluentHttpRequestInReceiver(initNode, depth + 1, visited);
                }
                break;
            }
            else
            {
                break;
            }
        }
        return false;
    }

    private static string? TryExtractFluentTarget(Node? expr)
    {
        if (!expr.IsValid()) return null;

        var curr = expr;
        if (curr.Is(TreeSitterSyntax.Common.Identifier))
        {
            var initNode = FindVariableInitializer(curr, curr.Text);
            if (initNode.IsValid() && initNode.Id != curr.Id)
            {
                curr = initNode;
            }
        }

        string? baseRoute = null;
        var segments = new List<string>();

        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.CSharp.InvocationExpression))
            {
                var func = curr.GetFunctionNode();
                if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
                {
                    var methodName = func.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                    if (methodName is "AddUrlSegment")
                    {
                        var argList = curr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                        var firstArg = argList.IsValid() ? argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument) : null;
                        if (firstArg.IsValid())
                        {
                            var argVal = firstArg.Children.FirstOrDefault();
                            if (argVal.IsValid())
                            {
                                if (argVal.Type.Contains("string"))
                                {
                                    segments.Add(argVal.Text.Trim('"'));
                                }
                                else
                                {
                                    segments.Add("*");
                                }
                            }
                        }
                    }
                    else if (methodName is "GetRequest" or "PostRequest" or "PutRequest" or "DeleteRequest" or "PatchRequest")
                    {
                        var argList = curr.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                        var firstArg = argList.IsValid() ? argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument) : null;
                        if (firstArg.IsValid())
                        {
                            var argVal = firstArg.Children.FirstOrDefault();
                            if (argVal.IsValid())
                            {
                                baseRoute = ResolveResourceArg(argVal);
                            }
                        }
                    }

                    curr = func.GetField(TreeSitterSyntax.Fields.Expression);
                    continue;
                }
            }

            break;
        }

        if (!string.IsNullOrEmpty(baseRoute) || segments.Count > 0)
        {
            segments.Reverse();
            var path = baseRoute ?? "";
            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(path))
                    path = seg;
                else
                    path = path.TrimEnd('/') + "/" + seg.TrimStart('/');
            }
            if (!string.IsNullOrEmpty(path))
            {
                return path.StartsWith('/') ? path : "/" + path;
            }
        }

        return null;
    }

    private static string? ResolveResourceArg(Node argVal)
    {
        if (AstValueResolver.TryResolveString(argVal, null, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        if (argVal.Type.Contains("string"))
        {
            return argVal.Text.Trim('"');
        }

        var text = argVal.Text;
        if (text.Contains('.'))
        {
            var propName = text[(text.LastIndexOf('.') + 1)..];
            if (RouteDictionaryRegistry.TryResolve(propName, out var route, out _))
            {
                return route;
            }
            if (ConstantRegistry.TryResolve(null, propName, out var cVal))
            {
                return cVal;
            }
        }

        if (RouteDictionaryRegistry.TryResolve(text, out var knownRoute, out _))
        {
            return knownRoute;
        }

        if (ConstantRegistry.TryResolve(null, text, out var directConst))
        {
            return directConst;
        }

        return null;
    }

    private static Node? FindVariableInitializer(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(
                TreeSitterSyntax.CSharp.Block,
                TreeSitterSyntax.CSharp.DeclarationList,
                TreeSitterSyntax.CSharp.MethodDeclaration,
                TreeSitterSyntax.CSharp.LocalFunctionStatement,
                TreeSitterSyntax.CSharp.ClassDeclaration,
                TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(
                        TreeSitterSyntax.CSharp.LocalDeclarationStatement,
                        TreeSitterSyntax.CSharp.VariableDeclaration,
                        TreeSitterSyntax.CSharp.FieldDeclaration))
                    {
                        var decls = FindNodesOfType(child, TreeSitterSyntax.CSharp.VariableDeclarator);
                        foreach (var decl in decls)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name)
                                           ?? decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valueNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valueNode.IsValid()) return valueNode;
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    public static string NormalizeUrl(string text)
    {
        return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
    }
}
