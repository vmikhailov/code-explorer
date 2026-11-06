using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GrpcGoLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.GrpcFramework;
    public string Name => "gRPC Go";
    public string Id => "grpc-go";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "google.golang.org/grpc*",
        "google.golang.org/grpc"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private const string ResultFieldName = "result";
    private const string ReceiverFieldName = "receiver";
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> ReceiverToServiceMapCache = new();
    private static readonly Regex UnimplementedServerRegex = new(@"(?:[A-Za-z0-9_]+\.)?(?:Unimplemented)?([A-Za-z0-9_]+)Server", RegexOptions.Compiled);
    private static readonly Regex RegisterServerRegex = new(@"(?:[A-Za-z0-9_]+\.)?Register([A-Za-z0-9_]+)Server", RegexOptions.Compiled);
    private static readonly Regex ClientTypeRegex = new(@"([A-Za-z0-9_]+?)(?:Svc|Service)?Client", RegexOptions.Compiled);
    private static readonly Regex NewClientRegex = new(@"(?:[A-Za-z0-9_]+\.)?New([A-Za-z0-9_]+?)(?:Svc|Service)?Client\b", RegexOptions.Compiled);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrpcMethod(node, out _, out _)) return OntologyConstants.NodeLabels.EntryPoint;
        if (IsGrpcClientCall(node, out _, out _)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsGrpcMethod(node, out var serviceName, out var methodName))
        {
            return $"RPC:/{serviceName}/{methodName}";
        }
        if (IsGrpcClientCall(node, out var clientService, out var clientMethod))
        {
            return $"RPC:/{clientService}/{clientMethod}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGrpcClientCall(node, out var serviceName, out var methodName))
        {
            var target = $"RPC:/{serviceName}/{methodName}";
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls,
                (int)node.StartPosition.Row + 1,
                (int)node.StartPosition.Column + 1));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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

        if (paramTypes.Count < 2) return false;
        if (!paramTypes[0].Contains("context", StringComparison.OrdinalIgnoreCase) && !paramTypes[0].Contains("Context")) return false;

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

        // Check HealthCheck special-case
        if (paramTypes.Any(p => p.Contains("HealthCheckRequest") || p.Contains("Health_WatchServer")))
        {
            serviceName = "Health";
            methodName = nameText;
            return true;
        }

        // Look up receiver struct mapping from file AST cache
        var root = GetFileRoot(node);
        var rootKey = !string.IsNullOrEmpty(node.Document?.FilePath)
            ? node.Document.FilePath
            : ((root as SyntaxDocument)?.FilePath ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root).ToString());
        var map = ReceiverToServiceMapCache.GetOrAdd(rootKey, _ => BuildReceiverMap(root));

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
                                receiverTypeName.Equals("grpcServer", StringComparison.OrdinalIgnoreCase) ||
                                receiverTypeName.Equals("Handler", StringComparison.OrdinalIgnoreCase);

        if (paramTypes.Count >= 2 && paramTypes[1].Contains("Request"))
        {
            var rawReqType = paramTypes[1].TrimStart('*', '&');
            var reqPackage = rawReqType.Contains('.') ? rawReqType.Split('.')[0] : "";
            var reqType = rawReqType.Contains('.') ? rawReqType.Split('.')[^1] : rawReqType;

            if (reqType.EndsWith("Request", StringComparison.OrdinalIgnoreCase))
            {
                var inferred = reqType[..^"Request".Length];
                if (!string.IsNullOrEmpty(reqPackage) && isGenericReceiver)
                {
                    var cleanPkg = char.ToUpperInvariant(reqPackage[0]) + reqPackage[1..];
                    serviceName = cleanPkg.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? cleanPkg : $"{cleanPkg}Service";
                }
                else if (!string.IsNullOrEmpty(receiverTypeName) && !isGenericReceiver)
                {
                    serviceName = char.ToUpperInvariant(receiverTypeName[0]) + receiverTypeName[1..];
                    if (!serviceName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
                    {
                        serviceName += "Service";
                    }
                }
                else
                {
                    serviceName = $"{inferred}Service";
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
                                                    var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                                                    serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
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
                                    var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                                    serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
                                    return true;
                                }
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

        void Walk(Node curr)
        {
            if (curr.Is(TreeSitterSyntax.Go.TypeSpec))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                var typeNode = curr.GetChildForField(TreeSitterSyntax.Fields.Type);
                if (nameNode.IsValid() && typeNode.IsValid() && typeNode.Is(TreeSitterSyntax.Go.StructType))
                {
                    var structText = typeNode.Text;
                    var match = UnimplementedServerRegex.Match(structText);
                    if (match.Success)
                    {
                        var sName = match.Groups[1].Value;
                        if (!sName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
                        {
                            sName += "Service";
                        }
                        map[nameNode.Text] = sName;
                    }
                }
            }

            if (curr.Is(TreeSitterSyntax.Go.CallExpression))
            {
                var callText = curr.Text;
                var regMatch = RegisterServerRegex.Match(callText);
                if (regMatch.Success)
                {
                    var sName = regMatch.Groups[1].Value;
                    if (!sName.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
                    {
                        sName += "Service";
                    }
                    var args = GoAstHelper.GetCallArguments(curr);
                    if (args.Count >= 2)
                    {
                        var handlerArg = args[1].Text.TrimStart('&', '*');
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
        return map;
    }
}
