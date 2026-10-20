using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class HttpClientLibraryParser : ISemanticExtension
{
    public string Name => "HttpClient";
    public string Id => "httpclient";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["System.Net.Http", "System.Net.Http.*"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> HttpMethods =
    [
        "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "SendAsync",
        "PostAsJsonAsync", "GetFromJsonAsync", "PatchAsync", "PutAsJsonAsync",
        "AddHttpClient", "AddGrpcClient"
    ];

    private static readonly string[] NonHttpReceiverKeywords =
    [
        "dicom", "channel", "queue", "bus", "stream", "pipe", "socket",
        "reader", "writer", "mediat", "broker", "eventhub", "producer", "consumer",
        "repository", "repo", "store", "dao", "context", "manager", "cache",
        "session", "storage", "db", "database", "table", "entity", "factory",
        "builder", "helper", "validator", "mapper", "converter", "hub",
        "clients", "clientproxy", "hubcontext"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node))
        {
            var target = ExtractTarget(node);
            if (!string.IsNullOrEmpty(target) && !target.Equals("http:unknown-service", StringComparison.OrdinalIgnoreCase))
            {
                return OntologyConstants.NodeLabels.ExternalService;
            }
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHttpClientCall(node)) return ExtractTarget(node);
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public static bool IsHttpClientCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
            if (!nameChild.IsValid()) return false;

            var methodName = nameChild.Text;
            var angleIdx = methodName.IndexOf('<');
            if (angleIdx > 0)
            {
                methodName = methodName[..angleIdx].Trim();
            }

            if (!HttpMethods.Contains(methodName))
                return false;

            var exprChild = func.GetField(TreeSitterSyntax.Fields.Expression);

            // Check receiver expression (skip for DI registrations like AddHttpClient / AddGrpcClient)
            if (methodName is not "AddHttpClient" and not "AddGrpcClient")
            {
                if (exprChild.IsValid())
                {
                    var receiverText = exprChild.Text.ToLowerInvariant();
                    foreach (var kw in NonHttpReceiverKeywords)
                    {
                        if (receiverText.Contains(kw))
                            return false;
                    }
                }
            }

            // Check argument list: first argument must not be pure CancellationToken or entity ID / non-URL argument
            var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                if (firstArg.IsValid())
                {
                    var argText = firstArg.Text.Trim();
                    if (IsCancellationToken(argText))
                    {
                        if (!HasFluentHttpRequestInReceiver(exprChild))
                            return false;
                    }

                    if (IsNonUrlArgument(firstArg, argText, methodName))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        return false;
    }

    public static bool IsCancellationToken(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Contains("CancellationToken", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.EndsWith("Token", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Equals("ct", StringComparison.OrdinalIgnoreCase) || text.Equals("token", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsNonUrlArgument(Node argNode, string argText, string methodName)
    {
        if (string.IsNullOrWhiteSpace(argText)) return false;
        if (methodName.Equals("AddHttpClient", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("AddGrpcClient", StringComparison.OrdinalIgnoreCase)) return false;

        if (methodName.Equals("SendAsync", StringComparison.OrdinalIgnoreCase))
        {
            // HttpClient.SendAsync takes an HttpRequestMessage (never a string literal).
            // SignalR IClientProxy.SendAsync("EventName", ...) takes an event name string literal.
            if (argText.StartsWith('"') || argText.StartsWith("@\"") || argText.StartsWith("$\""))
            {
                var unquoted = argText.TrimStart('$', '@').Trim('"');
                if (!unquoted.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !unquoted.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !unquoted.StartsWith('/'))
                {
                    return true;
                }
            }
            return false;
        }

        // 1. Literal numbers / primitives / Guid
        if (int.TryParse(argText, out _) || long.TryParse(argText, out _) || Guid.TryParse(argText, out _))
            return true;

        // 2. Null-forgiveness or property chains like validationResult.CustomerId!.Value
        if (argText.Contains('!'))
            return true;

        var lower = argText.ToLowerInvariant();

        // 3. ID / Key variables or properties (e.g. id, customerId, orderId, item.Id, query.Id)
        if (lower.EndsWith(".id") || lower.EndsWith(".value") || lower.EndsWith(".key") ||
            (lower.EndsWith("id") && (lower.StartsWith("request.") || lower.StartsWith("query.") || lower.StartsWith("validationresult.") || lower.StartsWith("model.") || lower.StartsWith("input."))) ||
            lower is "id" or "key" or "guid" or "customerid" or "orderid" or "userid" or "entityid" or "item")
        {
            return true;
        }

        // 4. Dot navigation without URL characteristics
        if (argText.Contains('.') && !argText.Contains('/') && !argText.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!lower.Contains("url") && !lower.Contains("uri") && !lower.Contains("endpoint") &&
                !lower.Contains("path") && !lower.Contains("route") && !lower.Contains("address"))
            {
                return true;
            }
        }

        return false;
    }


    public static string? ExtractTarget(Node node)
    {
        var func = node.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
            if (nameChild.IsValid())
            {
                var methodName = nameChild.Text;
                var angleIdx = methodName.IndexOf('<');
                if (angleIdx > 0) methodName = methodName[..angleIdx].Trim();

                if (methodName is "AddHttpClient" or "AddGrpcClient")
                {
                    return ExtractAddHttpClientTarget(node);
                }
            }

            // 1. Try fluent request chain first (from receiver expression)
            var expr = func.GetField(TreeSitterSyntax.Fields.Expression);
            var fluentTarget = TryExtractFluentTarget(expr);
            if (!string.IsNullOrEmpty(fluentTarget))
            {
                return NormalizeUrl(fluentTarget);
            }
        }

        // 2. Standard HttpClient arguments
        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!argList.IsValid()) return null;

        var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
        if (args.Count == 0) return null;

        var namedService = TryResolveNamedClientService(node);

        var firstArg = args[0];
        var valNode = firstArg.Children.FirstOrDefault();
        if (valNode.IsValid())
        {
            // If argument is an interpolated string ($"...")
            if (valNode.IsAny(
                    TreeSitterSyntax.CSharp.InterpolatedStringExpression,
                    TreeSitterSyntax.CSharp.InterpolatedVerbatimStringExpression,
                    TreeSitterSyntax.CSharp.InterpolatedRawStringExpression) ||
                valNode.Text.StartsWith('$') || valNode.Text.StartsWith("@$") || valNode.Text.StartsWith("$@"))
            {
                var interpUri = ExtractInterpolatedStringUri(valNode, node, 0);
                if (!string.IsNullOrEmpty(interpUri))
                {
                    var cleanInterp = interpUri.Split('?')[0].Split('&')[0];
                    var norm = NormalizeUrl(cleanInterp);
                    if (!string.IsNullOrEmpty(namedService) && norm.StartsWith('/'))
                    {
                        return $"{namedService}{norm}";
                    }
                    return norm;
                }
            }

            var text = valNode.Text.Trim('"');

            // Check if route registry has this constant directly
            if (RouteDictionaryRegistry.TryResolve(text, out var rp, out var rs))
            {
                var cleanPath = rp.Split('?')[0];
                var s = rs ?? namedService;
                var combined = !string.IsNullOrEmpty(s) ? $"{s}{cleanPath}" : cleanPath;
                return NormalizeUrl(combined);
            }

            if (valNode.Type.Contains("string") || text.Contains("://") || text.StartsWith("/"))
            {
                if (text.StartsWith("$") || text.StartsWith("@$") || text.StartsWith("$@"))
                {
                    text = text.TrimStart('$', '@').Trim('"');
                }
                var normalized = NormalizeUrl(text);
                if (!string.IsNullOrEmpty(namedService) && normalized.StartsWith('/'))
                {
                    return $"{namedService}{normalized}";
                }
                return normalized;
            }

            // If argument is a CancellationToken, try subsequent arguments
            if (IsCancellationToken(text))
            {
                for (int i = 1; i < args.Count; i++)
                {
                    var nextVal = args[i].Children.FirstOrDefault();
                    if (nextVal.IsValid())
                    {
                        var nextText = nextVal.Text.Trim('"');
                        if (!string.IsNullOrEmpty(nextText) && !IsCancellationToken(nextText))
                        {
                            if (RouteDictionaryRegistry.TryResolve(nextText, out var nrp, out var nrs))
                            {
                                var cleanPath = nrp.Split('?')[0];
                                var s = nrs ?? namedService;
                                var combined = !string.IsNullOrEmpty(s) ? $"{s}{cleanPath}" : cleanPath;
                                return NormalizeUrl(combined);
                            }

                            if (nextText.StartsWith("$") || nextText.StartsWith("@$") || nextText.StartsWith("$@"))
                            {
                                nextText = nextText.TrimStart('$', '@').Trim('"');
                            }
                            var norm = NormalizeUrl(nextText);
                            if (!string.IsNullOrEmpty(namedService) && norm.StartsWith('/'))
                            {
                                return $"{namedService}{norm}";
                            }
                            return norm;
                        }
                    }
                }
                return "http:unknown-service";
            }

            // Variable identifier like 'request' or 'uri'
            var resolvedUri = TryResolveVariableUri(node, text);
            if (!string.IsNullOrEmpty(resolvedUri))
            {
                var cleanResolved = resolvedUri.Split('?')[0].Split('&')[0];
                var norm = NormalizeUrl(cleanResolved);
                if (!string.IsNullOrEmpty(namedService) && norm.StartsWith('/'))
                {
                    return $"{namedService}{norm}";
                }
                return norm;
            }

            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeUrl(text);
            }

            if (text.StartsWith('/'))
            {
                return NormalizeUrl(!string.IsNullOrEmpty(namedService) ? $"{namedService}{text}" : text);
            }

            if (ConstantRegistry.TryResolve(null, text, out var resolvedConst))
            {
                var normConst = NormalizeUrl(resolvedConst);
                if (!string.IsNullOrEmpty(namedService) && normConst.StartsWith('/'))
                {
                    return $"{namedService}{normConst}";
                }
                return normConst;
            }

            return null;
        }

        return null;
    }

    private static string? ExtractAddHttpClientTarget(Node node)
    {
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return null;

        string? clientType = null;
        var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
        if (nameChild.IsValid() && nameChild.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var typeArgList = nameChild.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
            if (typeArgList.IsValid())
            {
                var typeArg = typeArgList.Children.FirstOrDefault(c => c.IsAny(
                    TreeSitterSyntax.CSharp.TypeIdentifier,
                    TreeSitterSyntax.Common.Identifier,
                    TreeSitterSyntax.CSharp.QualifiedName));
                if (typeArg.IsValid())
                {
                    clientType = typeArg.Text;
                    if (clientType.Contains('.'))
                    {
                        clientType = clientType[(clientType.LastIndexOf('.') + 1)..];
                    }
                }
            }
        }

        var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!argList.IsValid()) return null;

        var text = argList.Text;
        var match = Regex.Match(text, @"(?:BaseAddress|Address)\s*=\s*(?:new\s*(?:Uri)?\s*\(\s*)?[""']([^""']+)[""']");
        if (match.Success)
        {
            var rawUri = match.Groups[1].Value;
            var host = CleanServiceHost(rawUri);
            if (!string.IsNullOrEmpty(host))
            {
                if (!string.IsNullOrEmpty(clientType))
                {
                    RouteDictionaryRegistry.RegisterServiceDomain(clientType, host);
                }
                return $"http://{host}";
            }
        }
        else
        {
            var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            if (firstArg.IsValid())
            {
                var val = firstArg.Children.FirstOrDefault();
                if (val.IsValid() && val.Type.Contains("string"))
                {
                    var nameStr = val.Text.Trim('"');
                    var host = CleanServiceHost(nameStr);
                    if (!string.IsNullOrEmpty(host))
                    {
                        return $"http://{host}";
                    }
                }
            }
        }

        return null;
    }

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

    private static string ExtractInterpolatedStringUri(Node node, Node scopeNode, int depth)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var child in node.Children)
        {
            if (child.Type == TreeSitterSyntax.CSharp.InterpolatedStringText || child.Type == "string_content")
            {
                sb.Append(child.Text);
            }
            else if (child.Type == TreeSitterSyntax.CSharp.Interpolation)
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
                                    var resolved = TryResolveVariableUri(val, val.Text);
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
                        var resolved = TryResolveVariableUri(expr, expr.Text);
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
                        var createClientMatch = Regex.Match(curr.Text, receiverName + @"\s*=\s*.*?CreateClient\s*\(\s*[""']([^""']+)[""']");
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

    private static string? TryResolveVariableUri(Node node, string varName)
    {
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
                TreeSitterSyntax.CSharp.RecordStructDeclaration,
                TreeSitterSyntax.CSharp.CompilationUnit))
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
        if (depth > 10) return null;

        if (scopeNode.IsAny(
            TreeSitterSyntax.CSharp.ClassDeclaration,
            TreeSitterSyntax.CSharp.StructDeclaration,
            TreeSitterSyntax.CSharp.RecordDeclaration,
            TreeSitterSyntax.CSharp.RecordStructDeclaration))
        {
            var body = scopeNode.FindChildOfType(TreeSitterSyntax.CSharp.DeclarationList);
            if (body.IsValid())
            {
                var uri = FindUriInScope(body, targetVar, depth + 1);
                if (!string.IsNullOrEmpty(uri)) return uri;
            }
        }

        foreach (var child in scopeNode.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.DeclarationList))
            {
                var uri = FindUriInScope(child, targetVar, depth + 1);
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
                            if (valueNode.Is(TreeSitterSyntax.CSharp.EqualsValueClause))
                            {
                                valueNode = valueNode.GetField(TreeSitterSyntax.Fields.Value)
                                            ?? (valueNode.Children.Count > 1 ? valueNode.Children[1] : valueNode);
                            }
                        }
                        else
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
                            var uri = ExtractUriFromExpression(valueNode, scopeNode, depth);
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
                    if (arrow.IsValid() && arrow.Children.Count > 1)
                    {
                        var uri = ExtractUriFromExpression(arrow.Children[1], scopeNode, depth);
                        if (!string.IsNullOrEmpty(uri)) return uri;
                    }
                    var eq = child.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                    if (eq.IsValid() && eq.Children.Count > 1)
                    {
                        var uri = ExtractUriFromExpression(eq.Children[1], scopeNode, depth);
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
                            var uri = ExtractUriFromExpression(right, scopeNode, depth);
                            if (!string.IsNullOrEmpty(uri)) return uri;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractUriFromExpression(Node? expr, Node scopeNode, int depth)
    {
        if (!expr.IsValid() || depth > 10) return null;

        if (expr.IsAny(
            TreeSitterSyntax.CSharp.InterpolatedStringExpression,
            TreeSitterSyntax.CSharp.InterpolatedVerbatimStringExpression,
            TreeSitterSyntax.CSharp.InterpolatedRawStringExpression))
        {
            return ExtractInterpolatedStringUri(expr, scopeNode, depth);
        }

        if (expr.Type.Contains("string"))
        {
            var raw = expr.Text.Trim('"');
            if (raw.StartsWith('$') || raw.StartsWith("@$") || raw.StartsWith("$@"))
            {
                return ExtractInterpolatedStringUri(expr, scopeNode, depth);
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
                        var resolved = TryResolveVariableUri(val, val.Text);
                        if (!string.IsNullOrEmpty(resolved)) return resolved;
                        if (ConstantRegistry.TryResolve(null, val.Text, out var cVal)) return cVal;
                    }
                }
            }
        }

        if (expr.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression) ||
            expr.Type == "implicit_object_creation_expression")
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
                            return ExtractUriFromExpression(val, scopeNode, depth + 1);
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
                                var inner = ExtractUriFromExpression(uriArg, scopeNode, depth + 1);
                                if (!string.IsNullOrEmpty(inner)) return inner;
                            }
                            var resolved = TryResolveVariableUri(uriArg, uriArg.Text);
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
                            var innerUri = ExtractUriFromExpression(val, scopeNode, depth + 1);
                            if (!string.IsNullOrEmpty(innerUri)) return innerUri;
                        }
                        if (val.Is(TreeSitterSyntax.Common.Identifier) && val.Text != "HttpMethod")
                        {
                            var resolved = TryResolveVariableUri(val, val.Text);
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
        CollectNodes(root, nodeType, list);
        return list;
    }

    private static void CollectNodes(Node node, string nodeType, List<Node> result)
    {
        if (node.Is(nodeType))
        {
            result.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectNodes(child, nodeType, result);
        }
    }

    private static bool HasFluentHttpRequestInReceiver(Node? expr)
    {
        var curr = expr;
        while (curr.IsValid())
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
                var initNode = FindVariableInitializer(curr, curr.Text);
                if (initNode.IsValid())
                {
                    return HasFluentHttpRequestInReceiver(initNode);
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
            if (initNode.IsValid())
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
                                if (valueNode.IsValid())
                                {
                                    if (valueNode.Is(TreeSitterSyntax.CSharp.EqualsValueClause))
                                    {
                                        valueNode = valueNode.GetField(TreeSitterSyntax.Fields.Value)
                                                    ?? (valueNode.Children.Count > 1 ? valueNode.Children[1] : valueNode);
                                    }
                                }
                                else
                                {
                                    var eqClause = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                    if (eqClause.IsValid() && eqClause.Children.Count > 1)
                                    {
                                        valueNode = eqClause.Children[1];
                                    }
                                }
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