using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GrpcGoLibraryParser : DeclarativeSemanticExtension
{
    public GrpcGoLibraryParser()
    {
        Type = SemanticExtensionType.GrpcFramework;
        Name = "gRPC Go";
        Id = "grpc-go";
        SupportedPatterns =
        [
            "google.golang.org/grpc*",
            "google.golang.org/grpc"
        ];
        IsBuiltIn = true;
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.MethodDeclaration, TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (IsGrpcMethod(node, out _, out _)) return OntologyConstants.NodeLabels.EntryPoint;
                    if (IsGrpcClientCall(node, out _, out _)) return OntologyConstants.NodeLabels.ExternalService;
                    return null;
                },
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (IsGrpcMethod(node, out var serviceName, out var methodName)) return $"RPC:/{serviceName}/{methodName}";
                    if (IsGrpcClientCall(node, out var clientService, out var clientMethod)) return $"RPC:/{clientService}/{clientMethod}";
                    return null;
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsGrpcClientCall(node, out var serviceName, out var methodName))
                    {
                        var target = $"RPC:/{serviceName}/{methodName}";
                        references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls,
                            (int)node.StartPosition.Row + 1,
                            (int)node.StartPosition.Column + 1));
                    }
                },
                EnrichSymbol = EnrichGrpcGoSymbol
            }
        ];
    }

    private const string ResultFieldName = "result";
    private const string ReceiverFieldName = "receiver";
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> ReceiverToServiceMapCache = new();
    private static readonly Regex StreamServerRegex = new(@"(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+)_[A-Za-z0-9_]+Server\b", RegexOptions.Compiled);
    private static readonly Regex RequestTypeRegex = new(@"([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)(?:Request|Query)\b", RegexOptions.Compiled);
    private static readonly Regex UnimplementedServerRegex = new(@"(?:[A-Za-z0-9_]+\.)?(?:Unimplemented)?([A-Za-z0-9_]+)Server", RegexOptions.Compiled);
    private static readonly Regex RegisterServerRegex = new(@"(?:[A-Za-z0-9_]+\.)?Register([A-Za-z0-9_]+)Server", RegexOptions.Compiled);
    private static readonly Regex ClientTypeRegex = new(@"(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+?)(?:Svc|Service)?Client\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NewClientRegex = new(@"(?:(?:[A-Za-z0-9_]+\.)*New|New)([A-Za-z0-9_]+?)(?:Svc|Service)?Client\b", RegexOptions.Compiled);

    private const string SuffixPkg = "pkg";
    private const string SuffixPb = "pb";
    private const string SuffixV1 = "v1";
    private const string SuffixV2 = "v2";
    private const string SuffixAlpha = "alpha";
    private const string SuffixBeta = "beta";
    private const string ServiceSuffix = "Service";
    private const string ServerKey = "Server";
    private const string ServerKeyLower = "server";
    private const string ServiceKey = "Service";
    private const string ServiceKeyLower = "service";

    private static string CleanServiceName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return ServiceSuffix;
        var s = raw;
        if (s.EndsWith(SuffixPkg, StringComparison.OrdinalIgnoreCase) && s.Length > 3)
        {
            s = s[..^3];
        }
        if (s.EndsWith(SuffixPb, StringComparison.OrdinalIgnoreCase) && s.Length > 2)
        {
            s = s[..^2];
        }
        if (s.EndsWith(SuffixV1, StringComparison.OrdinalIgnoreCase) && s.Length > 2)
        {
            s = s[..^2];
        }
        if (s.EndsWith(SuffixV2, StringComparison.OrdinalIgnoreCase) && s.Length > 2)
        {
            s = s[..^2];
        }
        if (s.EndsWith(SuffixAlpha, StringComparison.OrdinalIgnoreCase) && s.Length > 5)
        {
            s = s[..^5];
        }
        if (s.EndsWith(SuffixBeta, StringComparison.OrdinalIgnoreCase) && s.Length > 4)
        {
            s = s[..^4];
        }
        s = s.TrimEnd('_');
        if (string.IsNullOrEmpty(s)) return ServiceSuffix;
        var clean = char.ToUpperInvariant(s[0]) + s[1..];
        return clean.EndsWith(ServiceSuffix, StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}{ServiceSuffix}";
    }

    private static void EnrichGrpcGoSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsGrpcMethod(node, out _, out _))
        {
            symbol.Protocol = "gRPC";
            symbol.Properties["protocol"] = "gRPC";
            symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.Unary;
        }
        else if (IsGrpcClientCall(node, out var serviceName, out var methodName))
        {
            symbol.Protocol = "gRPC";
            symbol.Properties["protocol"] = "gRPC";
            symbol.Properties["url"] = $"/{serviceName}/{methodName}";
        }
    }

    private static bool IsGrpcMethod(Node node, out string serviceName, out string methodName)
    {
        serviceName = "";
        methodName = "";

        if (!node.Is(TreeSitterSyntax.Go.MethodDeclaration)) return false;

        var nameField = node.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (!nameField.IsValid()) return false;
        var nameText = nameField.Text;
        if (string.IsNullOrEmpty(nameText) || !char.IsUpper(nameText[0])) return false;

        var paramsField = node.GetChildForField(TreeSitterSyntax.Fields.Parameters);
        if (!paramsField.IsValid()) return false;

        var paramTypes = paramsField.Children
            .Where(c => c.Is(TreeSitterSyntax.Go.ParameterDeclaration))
            .Select(p => p.GetChildForField(TreeSitterSyntax.Fields.Type)?.Text ?? p.Text)
            .ToList();

        if (paramTypes.Count == 0) return false;

        var resultField = node.GetChildForField(ResultFieldName);
        if (resultField.IsValid() && !resultField.Text.Contains("error")) return false;

        // Resolve receiver struct type
        var receiver = node.GetChildForField(ReceiverFieldName);
        var receiverTypeName = "";
        if (receiver.IsValid())
        {
            var param = receiver.FindChildOfType(TreeSitterSyntax.Go.ParameterDeclaration);
            if (param.IsValid())
            {
                var typeNode = param.GetChildForField(TreeSitterSyntax.Fields.Type);
                if (typeNode.IsValid())
                {
                    receiverTypeName = typeNode.Text.TrimStart('*', '&');
                }
            }
        }

        if (receiverTypeName.StartsWith("Unimplemented", StringComparison.OrdinalIgnoreCase)) return false;
        if (receiverTypeName.EndsWith("Client", StringComparison.OrdinalIgnoreCase)) return false;

        // Look up receiver struct mapping from file AST cache
        var root = GetFileRoot(node);
        var rootKey = !string.IsNullOrEmpty(node.Document?.FilePath)
            ? node.Document.FilePath
            : ((root as SyntaxDocument)?.FilePath ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root).ToString());
        var map = ReceiverToServiceMapCache.GetOrAdd(rootKey, _ => BuildReceiverMap(root));

        // Check HealthCheck special-case
        if (paramTypes.Any(p => p.Contains("HealthCheckRequest") || p.Contains("Health_WatchServer")))
        {
            serviceName = "Health";
            methodName = nameText;
            return true;
        }

        // Check streaming RPCs (server-streaming or client/bidi streaming)
        foreach (var pt in paramTypes)
        {
            var sm = StreamServerRegex.Match(pt);
            if (sm.Success)
            {
                serviceName = CleanServiceName(sm.Groups[1].Value);
                methodName = nameText;
                return true;
            }
            if (pt.EndsWith("ServerStream", StringComparison.Ordinal) || pt.Contains("grpc.ServerStream"))
            {
                if (!string.IsNullOrEmpty(receiverTypeName) && map.TryGetValue(receiverTypeName, out var mappedSvc))
                {
                    serviceName = mappedSvc;
                    methodName = nameText;
                    return true;
                }
            }
        }

        // Unary RPCs: requires at least 2 parameters, param 0 is context
        if (paramTypes.Count >= 2 &&
            (paramTypes[0].Contains("context", StringComparison.OrdinalIgnoreCase) || paramTypes[0].Contains("Context")))
        {
            var rawReqType = paramTypes[1].TrimStart('*', '&');
            var isProtoReq = rawReqType.EndsWith("Request", StringComparison.OrdinalIgnoreCase) ||
                             rawReqType.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
                             rawReqType.Contains("Empty", StringComparison.OrdinalIgnoreCase);

            if (isProtoReq)
            {
                if (!string.IsNullOrEmpty(receiverTypeName) && map.TryGetValue(receiverTypeName, out var mappedService))
                {
                    serviceName = mappedService;
                    methodName = nameText;
                    return true;
                }

                var isGenericReceiver = string.IsNullOrEmpty(receiverTypeName) ||
                                        receiverTypeName.Equals("GrpcServer", StringComparison.OrdinalIgnoreCase) ||
                                        receiverTypeName.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
                                        receiverTypeName.Equals("server", StringComparison.OrdinalIgnoreCase) ||
                                        receiverTypeName.Equals("Service", StringComparison.OrdinalIgnoreCase) ||
                                        receiverTypeName.Equals("service", StringComparison.OrdinalIgnoreCase) ||
                                        receiverTypeName.Equals("grpcServer", StringComparison.OrdinalIgnoreCase) ||
                                        receiverTypeName.Equals("Handler", StringComparison.OrdinalIgnoreCase);

                var reqPackage = rawReqType.Contains('.') ? rawReqType.Split('.')[0] : "";
                var reqType = rawReqType.Contains('.') ? rawReqType.Split('.')[^1] : rawReqType;

                if (!string.IsNullOrEmpty(reqPackage) && isGenericReceiver)
                {
                    serviceName = CleanServiceName(reqPackage);
                }
                else if (!string.IsNullOrEmpty(receiverTypeName) && !isGenericReceiver)
                {
                    serviceName = CleanServiceName(receiverTypeName);
                }
                else
                {
                    var inferred = reqType;
                    if (inferred.EndsWith("Request", StringComparison.OrdinalIgnoreCase)) inferred = inferred[..^"Request".Length];
                    else if (inferred.EndsWith("Query", StringComparison.OrdinalIgnoreCase)) inferred = inferred[..^"Query".Length];
                    serviceName = CleanServiceName(inferred);
                }
                methodName = nameText;
                return true;
            }
        }

        return false;
    }

    private static bool IsGrpcClientCall(Node node, out string serviceName, out string methodName)
    {
        serviceName = "";
        methodName = "";

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid()) return false;
        var method = field.Text;
        if (string.IsNullOrEmpty(method) || !char.IsUpper(method[0])) return false;

        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (!operand.IsValid()) return false;
        var opText = operand.Text;

        // Skip standard libraries or non-client objects
        if (opText is "slog" or "log" or "fmt" or "strings" or "strconv" or "time" or "os" or "http" or "trace" or "span" or "ctx" or "context" or "json")
        {
            return false;
        }

        var directMatch = NewClientRegex.Match(opText);
        if (directMatch.Success)
        {
            var raw = directMatch.Groups[1].Value;
            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
            serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
            methodName = method;
            return VerifyGrpcCallArguments(node);
        }

        var leafOp = opText.Contains('.') ? opText.Split('.')[^1] : opText;
        var clientVarMatch = ClientTypeRegex.Match(leafOp);
        if (clientVarMatch.Success)
        {
            var raw = clientVarMatch.Groups[1].Value;
            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
            serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
            methodName = method;
            return VerifyGrpcCallArguments(node);
        }

        if (TryResolveClientFromScope(node, opText, out var resolvedService))
        {
            serviceName = resolvedService;
            methodName = method;
            return VerifyGrpcCallArguments(node);
        }

        return false;
    }

    private static bool VerifyGrpcCallArguments(Node node)
    {
        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count == 0) return true;

        var firstArg = args[0].Text;
        return firstArg.Contains("ctx", StringComparison.OrdinalIgnoreCase) ||
               firstArg.Contains("context", StringComparison.OrdinalIgnoreCase) ||
               firstArg.Contains("Context");
    }

    private static bool TryResolveClientFromScope(Node node, string varName, out string serviceName)
    {
        serviceName = "";
        if (string.IsNullOrWhiteSpace(varName)) return false;

        if (varName.Contains('.'))
        {
            var parts = varName.Split('.');
            var fieldName = parts[^1];

            var encMethod = node.Parent;
            while (encMethod.IsValid() && !encMethod.Is(TreeSitterSyntax.Go.MethodDeclaration))
            {
                encMethod = encMethod.Parent;
            }

            if (encMethod.IsValid())
            {
                var receiverNode = encMethod.GetChildForField(ReceiverFieldName);
                if (receiverNode.IsValid())
                {
                    var param = receiverNode.FindChildOfType(TreeSitterSyntax.Go.ParameterDeclaration);
                    var typeNode = param.IsValid() ? param.GetChildForField(TreeSitterSyntax.Fields.Type) : default;
                    var structTypeName = typeNode.IsValid() ? typeNode.Text.TrimStart('*', '&') : "";

                    if (!string.IsNullOrEmpty(structTypeName))
                    {
                        var rootNode = GetFileRoot(node);
                        foreach (var desc in rootNode.Descendants())
                        {
                            if (desc.Is(TreeSitterSyntax.Go.TypeSpec))
                            {
                                var nameNode = desc.GetChildForField(TreeSitterSyntax.Fields.Name);
                                if (nameNode.IsValid() && nameNode.Text == structTypeName)
                                {
                                    var structType = desc.GetChildForField(TreeSitterSyntax.Fields.Type);
                                    if (structType.IsValid() && structType.Is(TreeSitterSyntax.Go.StructType))
                                    {
                                        foreach (var fieldDecl in structType.Descendants().Where(d => d.Is(TreeSitterSyntax.Go.FieldDeclaration)))
                                        {
                                            var fName = fieldDecl.GetChildForField(TreeSitterSyntax.Fields.Name);
                                            var fChild = fieldDecl.Children.FirstOrDefault(c => c.Type is "field_identifier" or "identifier" || c.Is(TreeSitterSyntax.Go.Identifier));
                                            var nameText = fName.IsValid() ? fName.Text : (fChild.IsValid() ? fChild.Text : "");
                                            if (string.IsNullOrEmpty(nameText) && fieldDecl.Text.TrimStart().StartsWith(fieldName))
                                            {
                                                nameText = fieldName;
                                            }

                                            if (nameText == fieldName)
                                            {
                                                var fType = fieldDecl.GetChildForField(TreeSitterSyntax.Fields.Type);
                                                var fTypeText = fType.IsValid() ? fType.Text : fieldDecl.Text;
                                                var cMatch = ClientTypeRegex.Match(fTypeText);
                                                if (cMatch.Success)
                                                {
                                                    var raw = cMatch.Groups[1].Value;
                                                    if (raw.Contains('.')) raw = raw.Split('.')[^1];
                                                    serviceName = CleanServiceName(raw);
                                                    return true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.Go.FunctionDeclaration) ||
                curr.Is(TreeSitterSyntax.Go.MethodDeclaration) ||
                curr.Is(TreeSitterSyntax.Go.FuncLiteral))
            {
                // Check parameters of enclosing function/method
                var paramsNode = curr.GetChildForField(TreeSitterSyntax.Fields.Parameters);
                if (paramsNode.IsValid())
                {
                    foreach (var param in paramsNode.Children.Where(c => c.Is(TreeSitterSyntax.Go.ParameterDeclaration)))
                    {
                        var pName = param.GetChildForField(TreeSitterSyntax.Fields.Name);
                        var pType = param.GetChildForField(TreeSitterSyntax.Fields.Type);
                        var nameStr = pName.IsValid() ? pName.Text : (param.Children.Count >= 2 ? param.Children[0].Text : "");
                        if (nameStr == varName && pType.IsValid())
                        {
                            var match = ClientTypeRegex.Match(pType.Text);
                            if (match.Success)
                            {
                                serviceName = CleanServiceName(match.Groups[1].Value);
                                return true;
                            }
                        }
                    }
                }

                // Check local variable declarations
                foreach (var desc in curr.Descendants())
                {
                    if (desc.Is(TreeSitterSyntax.Go.ShortVarDeclaration) || desc.Is(TreeSitterSyntax.Go.AssignmentStatement))
                    {
                        var left = desc.GetChildForField(TreeSitterSyntax.Fields.Left) ?? (desc.Children.Count >= 2 ? desc.Children[0] : null);
                        var right = desc.GetChildForField(TreeSitterSyntax.Fields.Right) ?? (desc.Children.Count >= 2 ? desc.Children[^1] : null);
                        if (left.IsValid() && right.IsValid())
                        {
                            var varMatches = left.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (varMatches.Contains(varName, StringComparer.Ordinal))
                            {
                                var match = NewClientRegex.Match(right.Text);
                                if (match.Success)
                                {
                                    var raw = match.Groups[1].Value;
                                    serviceName = CleanServiceName(raw);
                                    return true;
                                }
                            }
                        }
                    }
                    else if (desc.Is(TreeSitterSyntax.Go.VarSpec))
                    {
                        var vName = desc.GetChildForField(TreeSitterSyntax.Fields.Name);
                        var vType = desc.GetChildForField(TreeSitterSyntax.Fields.Type);
                        if (vName.IsValid() && vName.Text == varName && vType.IsValid())
                        {
                            var match = ClientTypeRegex.Match(vType.Text);
                            if (match.Success)
                            {
                                serviceName = CleanServiceName(match.Groups[1].Value);
                                return true;
                            }
                        }
                    }
                }
                break;
            }
            curr = curr.Parent;
        }
        return false;
    }

    private static Node GetFileRoot(Node node)
    {
        var curr = node;
        while (curr.Parent.IsValid())
        {
            curr = curr.Parent;
        }
        return curr;
    }

    private static Dictionary<string, string> BuildReceiverMap(Node root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Detect package name
        var pkgNode = root.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Go.PackageClause));
        string? pkgName = null;
        if (pkgNode.IsValid())
        {
            var pkgId = pkgNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Go.PackageIdentifier) || c.Is(TreeSitterSyntax.Go.Identifier));
            if (pkgId.IsValid() && !string.IsNullOrEmpty(pkgId.Text) && pkgId.Text != "main")
            {
                pkgName = pkgId.Text;
            }
        }

        void Walk(Node curr)
        {
            if (curr.Is(TreeSitterSyntax.Go.TypeSpec))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                var typeNode = curr.GetChildForField(TreeSitterSyntax.Fields.Type);
                if (nameNode.IsValid() && typeNode.IsValid() && typeNode.Is(TreeSitterSyntax.Go.StructType))
                {
                    foreach (var field in typeNode.Children.Where(c => c.Is(TreeSitterSyntax.Go.FieldDeclaration)))
                    {
                        var fName = field.GetChildForField(TreeSitterSyntax.Fields.Name);
                        var fType = field.GetChildForField(TreeSitterSyntax.Fields.Type);
                        // An embedded field has no field name identifier
                        var isEmbedded = !fName.IsValid() || string.IsNullOrEmpty(fName.Text);
                        var typeCandidate = fType.IsValid() ? fType.Text : (isEmbedded ? field.Text : "");
                        if (!string.IsNullOrEmpty(typeCandidate))
                        {
                            var match = UnimplementedServerRegex.Match(typeCandidate);
                            if (match.Success)
                            {
                                map[nameNode.Text] = CleanServiceName(match.Groups[1].Value);
                            }
                        }
                    }
                }
            }

            if (curr.Is(TreeSitterSyntax.Go.MethodDeclaration))
            {
                var receiver = curr.GetChildForField(ReceiverFieldName);
                var receiverTypeName = "";
                if (receiver.IsValid())
                {
                    var param = receiver.FindChildOfType(TreeSitterSyntax.Go.ParameterDeclaration);
                    if (param.IsValid())
                    {
                        var typeNode = param.GetChildForField(TreeSitterSyntax.Fields.Type);
                        if (typeNode.IsValid())
                        {
                            receiverTypeName = typeNode.Text.TrimStart('*', '&');
                        }
                    }
                }

                if (!string.IsNullOrEmpty(receiverTypeName) && !map.ContainsKey(receiverTypeName))
                {
                    var paramsField = curr.GetChildForField(TreeSitterSyntax.Fields.Parameters);
                    if (paramsField.IsValid())
                    {
                        var paramTypes = paramsField.Children
                            .Where(c => c.Is(TreeSitterSyntax.Go.ParameterDeclaration))
                            .Select(p => p.GetChildForField(TreeSitterSyntax.Fields.Type)?.Text ?? p.Text)
                            .ToList();

                        foreach (var pt in paramTypes)
                        {
                            var streamMatch = StreamServerRegex.Match(pt);
                            if (streamMatch.Success)
                            {
                                map[receiverTypeName] = CleanServiceName(streamMatch.Groups[1].Value);
                                break;
                            }
                        }

                        if (!map.ContainsKey(receiverTypeName) && paramTypes.Count >= 2)
                        {
                            var rawReq = paramTypes[1].TrimStart('*', '&');
                            var match = RequestTypeRegex.Match(rawReq);
                            if (match.Success)
                            {
                                var pkg = match.Groups[1].Value;
                                if (!string.IsNullOrEmpty(pkg))
                                {
                                    map[receiverTypeName] = CleanServiceName(pkg);
                                }
                            }
                        }
                    }
                }
            }

            if (curr.Is(TreeSitterSyntax.Go.CallExpression))
            {
                var callText = curr.Text;
                var regMatch = RegisterServerRegex.Match(callText);
                if (regMatch.Success)
                {
                    var sName = CleanServiceName(regMatch.Groups[1].Value);
                    var args = GoAstHelper.GetCallArguments(curr);
                    if (args.Count >= 2)
                    {
                        var handlerArg = args[1].Text.TrimStart('&', '*');
                        if (handlerArg.Contains('.')) handlerArg = handlerArg.Split('.')[^1];
                        map[handlerArg] = sName;
                    }
                }
            }

            foreach (var child in curr.Children)
            {
                Walk(child);
            }
        }

        Walk(root);

        if (!string.IsNullOrEmpty(pkgName))
        {
            var cleanPkg = CleanServiceName(pkgName);
            if (!map.ContainsKey(ServerKey)) map[ServerKey] = cleanPkg;
            if (!map.ContainsKey(ServerKeyLower)) map[ServerKeyLower] = cleanPkg;
            if (!map.ContainsKey(ServiceKey)) map[ServiceKey] = cleanPkg;
            if (!map.ContainsKey(ServiceKeyLower)) map[ServiceKeyLower] = cleanPkg;
        }

        return map;
    }
}
