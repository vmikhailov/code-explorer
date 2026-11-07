using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public partial class HttpClientLibraryParser : DeclarativeSemanticExtension
{
    public HttpClientLibraryParser()
    {
        Name = "HttpClient";
        Id = "httpclient";
        Type = SemanticExtensionType.ApiClient;
        SupportedPatterns = ["System.Net.Http", "System.Net.Http.*"];
        IsBuiltIn = true;
        LibraryRole = LibraryRole.General;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.CSharp.InvocationExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
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
                },
                ExtractIdentifier = static (node, _, _, _) => IsHttpClientCall(node) ? ExtractTarget(node) : null
            }
        ];
    }

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
                if (!unquoted.StartWithIC("http://") &&
                    !unquoted.StartWithIC("https://") &&
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
        if (argText.Contains('.') && !argText.Contains('/') && !argText.StartWithIC("http"))
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
            if (valNode.Is(TreeSitterSyntax.CSharp.InterpolatedStringExpression) ||
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

            if (text.StartWithIC("http://") || text.StartWithIC("https://"))
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

}
