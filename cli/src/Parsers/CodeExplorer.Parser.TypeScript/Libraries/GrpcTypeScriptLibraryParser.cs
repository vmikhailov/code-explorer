using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GrpcTypeScriptLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.GrpcFramework;
    public string Name => "gRPC TypeScript";
    public string Id => "grpc-ts";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "@grpc/grpc-js*",
        "@grpc/proto-loader*",
        "grpc-web*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> FileClientMapCache = new();
    private static readonly Regex ClientNameRegex = new(@"([A-Za-z0-9_]+?)(?:Svc|Service)?Client$", RegexOptions.Compiled);
    private static readonly Regex ServiceDefRegex = new(@"([A-Za-z0-9_]+?)(?:Service)?(?:\.service)?$", RegexOptions.Compiled);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrpcClientCall(node, out _, out _)) return OntologyConstants.NodeLabels.ExternalService;
        if (IsGrpcServerAddServiceMethod(node, out _, out _)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsGrpcClientCall(node, out var serviceName, out var methodName))
        {
            return $"RPC:/{serviceName}/{methodName}";
        }
        if (IsGrpcServerAddServiceMethod(node, out var serverService, out var serverMethod))
        {
            return $"RPC:/{serverService}/{serverMethod}";
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
        if (IsGrpcClientCall(node, out var serviceName, out var methodName))
        {
            symbol.Protocol = "gRPC";
            symbol.Properties["protocol"] = "gRPC";
            symbol.Properties["url"] = $"/{serviceName}/{methodName}";
        }
        else if (IsGrpcServerAddServiceMethod(node, out _, out _))
        {
            symbol.Protocol = "gRPC";
            symbol.Properties["protocol"] = "gRPC";
            symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.Unary;
        }
    }

    private static bool IsGrpcClientCall(Node node, out string serviceName, out string methodName)
    {
        serviceName = "";
        methodName = "";

        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.TypeScript.MemberExpression)) return false;

        var prop = func.GetField(TreeSitterSyntax.Fields.Property);
        if (!prop.IsValid()) return false;
        var method = prop.Text;
        if (string.IsNullOrEmpty(method) ||
            method is "then" or "catch" or "finally" or "subscribe" or "pipe" or "map" or "forEach" or "toString")
        {
            return false;
        }

        var obj = func.GetField(TreeSitterSyntax.Fields.Object);
        if (!obj.IsValid()) return false;
        var objText = obj.Text;

        // Skip standard utilities
        if (objText is "console" or "logger" or "Math" or "JSON" or "Promise" or "Object" or "Array" or "fs" or "path")
        {
            return false;
        }

        var root = GetFileRoot(node);
        var rootKey = !string.IsNullOrEmpty(node.Document?.FilePath)
            ? node.Document.FilePath
            : ((root as SyntaxDocument)?.FilePath ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root).ToString());
        var clientMap = FileClientMapCache.GetOrAdd(rootKey, _ => BuildFileClientMap(root));

        if (clientMap.TryGetValue(objText, out var mappedService))
        {
            serviceName = mappedService;
            methodName = method;
            return true;
        }

        // Check if object expression ends with Client, e.g. adClient.getAds
        var leaf = objText.Contains('.') ? objText.Split('.')[^1] : objText;
        if (leaf.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
        {
            var match = ClientNameRegex.Match(leaf);
            if (match.Success)
            {
                var raw = match.Groups[1].Value;
                var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
                methodName = method;
                return true;
            }
        }

        return false;
    }

    private static bool IsGrpcServerAddServiceMethod(Node node, out string serviceName, out string methodName)
    {
        serviceName = "";
        methodName = "";

        // Check if node is a property in an object literal passed to server.addService(serviceDef, { charge: handler })
        if (!node.Is(TreeSitterSyntax.TypeScript.Pair) &&
            !node.Is(TreeSitterSyntax.TypeScript.MethodDefinition) &&
            !node.Is(TreeSitterSyntax.TypeScript.ShorthandPropertyIdentifier) &&
            !node.Is(TreeSitterSyntax.TypeScript.Identifier)) return false;

        var objLit = node.Parent;
        if (!objLit.IsValid() || !objLit.Is(TreeSitterSyntax.TypeScript.Object)) return false;

        var callExpr = objLit.Parent;
        if (callExpr.IsValid() && callExpr.Is(TreeSitterSyntax.TypeScript.Arguments))
        {
            callExpr = callExpr.Parent;
        }

        if (!callExpr.IsValid() || !callExpr.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = callExpr.GetFunctionNode();
        if (!func.IsValid() || !func.Text.EndsWith("addService")) return false;

        var args = AstHelper.GetCallArguments(callExpr);
        if (args.Count < 2) return false;

        var sDefArg = args[0].Text;
        var sName = "Service";
        var parts = sDefArg.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var serviceToken = parts.FirstOrDefault(p => p.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
                           ?? (parts.Length >= 2 && parts[^1].Equals("service", StringComparison.OrdinalIgnoreCase) ? parts[^2] : parts[^1]);
        var sMatch = ServiceDefRegex.Match(serviceToken);
        if (sMatch.Success)
        {
            var raw = sMatch.Groups[1].Value;
            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
            sName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
        }
        else
        {
            var clean = char.ToUpperInvariant(serviceToken[0]) + serviceToken[1..];
            sName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
        }

        var propName = "";
        if (node.Is(TreeSitterSyntax.TypeScript.Pair))
        {
            var keyNode = node.GetField(TreeSitterSyntax.Fields.Key);
            if (keyNode.IsValid()) propName = keyNode.Text;
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.MethodDefinition))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid()) propName = nameNode.Text;
        }
        else if (node.Is(TreeSitterSyntax.TypeScript.ShorthandPropertyIdentifier) || node.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            propName = node.Text;
        }

        if (!string.IsNullOrEmpty(propName))
        {
            serviceName = sName;
            methodName = char.ToUpperInvariant(propName[0]) + propName[1..];
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

    private static Dictionary<string, string> BuildFileClientMap(Node root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Walk(Node curr)
        {
            // Detect: const client = new AdServiceClient(...)
            if (curr.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
            {
                var nameNode = curr.GetField(TreeSitterSyntax.Fields.Name);
                var valNode = curr.GetField(TreeSitterSyntax.Fields.Value);
                if (nameNode.IsValid() && valNode.IsValid() && valNode.Is(TreeSitterSyntax.TypeScript.NewExpression))
                {
                    var ctor = valNode.GetField(TreeSitterSyntax.Fields.Constructor);
                    if (ctor.IsValid())
                    {
                        var ctorText = ctor.Text;
                        var match = ClientNameRegex.Match(ctorText);
                        if (match.Success)
                        {
                            var raw = match.Groups[1].Value;
                            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                            var sName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
                            map[nameNode.Text] = sName;
                        }
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
