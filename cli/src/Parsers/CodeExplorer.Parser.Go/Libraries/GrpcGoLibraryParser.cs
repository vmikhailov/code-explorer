using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
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
    private static readonly Regex UnimplementedServerRegex = new(@"(?:pb\.)?(?:Unimplemented)?([A-Za-z0-9_]+)Server", RegexOptions.Compiled);
    private static readonly Regex RegisterServerRegex = new(@"(?:pb\.)?Register([A-Za-z0-9_]+)Server", RegexOptions.Compiled);
    private static readonly Regex ClientTypeRegex = new(@"([A-Za-z0-9_]+?)(?:Svc|Service)?Client", RegexOptions.Compiled);

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
            .Select(p => p.GetChildForField(TreeSitterSyntax.Fields.Type)?.Text ?? "")
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

        // Check HealthCheck special-case
        if (paramTypes.Any(p => p.Contains("HealthCheckRequest") || p.Contains("Health_WatchServer")))
        {
            serviceName = "Health";
            methodName = nameText;
            return true;
        }

        // Look up receiver struct mapping from file AST cache
        var root = GetFileRoot(node);
        var rootKey = root.Id.ToString();
        var map = ReceiverToServiceMapCache.GetOrAdd(rootKey, _ => BuildReceiverMap(root));

        if (!string.IsNullOrEmpty(receiverTypeName) && map.TryGetValue(receiverTypeName, out var mappedService))
        {
            serviceName = mappedService;
            methodName = nameText;
            return true;
        }

        if (paramTypes.Count >= 2 && paramTypes[1].Contains("Request"))
        {
            var reqType = paramTypes[1].TrimStart('*', '&');
            if (reqType.Contains('.')) reqType = reqType.Split('.')[^1];
            if (reqType.EndsWith("Request", StringComparison.OrdinalIgnoreCase))
            {
                var inferred = reqType[..^"Request".Length];
                if (!string.IsNullOrEmpty(receiverTypeName))
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

        var isClient = opText.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
                       opText.Contains("SvcClient", StringComparison.OrdinalIgnoreCase) ||
                       opText.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
                       opText.Contains("ServiceClient", StringComparison.OrdinalIgnoreCase);

        if (!isClient) return false;

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count < 2) return false;

        // First argument is typically ctx (context.Context)
        var firstArg = args[0].Text;
        if (!firstArg.Contains("ctx", StringComparison.OrdinalIgnoreCase) &&
            !firstArg.Contains("context", StringComparison.OrdinalIgnoreCase) &&
            !firstArg.Contains("Context"))
        {
            return false;
        }

        // Derive Service Name from client variable name (e.g. cartSvcClient -> CartService)
        var leafOp = opText.Contains('.') ? opText.Split('.')[^1] : opText;
        var match = ClientTypeRegex.Match(leafOp);
        if (match.Success)
        {
            var raw = match.Groups[1].Value;
            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
            serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
            methodName = method;
            return true;
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
