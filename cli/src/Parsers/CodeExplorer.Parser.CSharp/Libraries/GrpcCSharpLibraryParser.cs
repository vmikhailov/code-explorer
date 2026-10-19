using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class GrpcCSharpLibraryParser : ISemanticExtension
{
    public string Name => "gRPC";
    public string Id => "grpc";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "Grpc", "Grpc.*", "Grpc.Core", "Grpc.Net.Client", "Grpc.AspNetCore",
        "Grpc.AspNetCore.Server", "Grpc.AspNetCore.*", "Grpc.Net.*",
        "*Grpc*", "*.Grpc", "*.Grpc.*"
    ];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrpcMethodDeclaration(node)) return OntologyConstants.NodeLabels.EntryPoint;
        if (IsGrpcClientCall(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGrpcMethodDeclaration(node))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            var methodName = nameNode.IsValid() ? nameNode.Text : "anonymous";
            var serviceName = GetParentClassName(node);
            return string.IsNullOrEmpty(serviceName) ? $"RPC:{methodName}" : $"RPC:/{serviceName}/{methodName}";
        }
        if (IsGrpcClientCall(node))
        {
            return ExtractClientTarget(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGrpcClientCall(node))
        {
            var target = ExtractClientTarget(node);
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, "CALLS"));
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsGrpcMethodDeclaration(node)) return;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        var methodName = nameNode.IsValid() ? nameNode.Text : "anonymous";
        if (!symbol.Children.Any(c => c.Kind == OntologyConstants.NodeLabels.Function && c.Name == methodName))
        {
            var fnSymbol = new SyntacticSymbol(OntologyConstants.NodeLabels.Function, methodName, node) { Text = node.Text };
            symbol.Children.Add(fnSymbol);
        }

        symbol.Protocol = "gRPC";
        symbol.OperationType = "Unary";

        // Response type from method return type
        var typeNode = node.GetField(TreeSitterSyntax.Fields.Returns)
                       ?? node.GetField(TreeSitterSyntax.Fields.Type)
                       ?? node.GetField(TreeSitterSyntax.Fields.ReturnType)
                       ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.CSharp.PredefinedType, TreeSitterSyntax.CSharp.NullableType));
        if (typeNode.IsValid())
        {
            var ret = CleanTypeName(typeNode.Text);
            if (!string.IsNullOrEmpty(ret) && ret != "void" && ret != "Task" && ret != "ValueTask")
            {
                symbol.ResponseType = ret;
            }
        }

        // Request type and streaming operation type from parameters
        var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters)
                        ?? node.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (paramList.IsValid())
        {
            bool hasClientStream = false;
            bool hasServerStream = false;

            foreach (var param in paramList.Children)
            {
                if (!param.Is(TreeSitterSyntax.CSharp.Parameter)) continue;
                var pType = param.GetField(TreeSitterSyntax.Fields.Type)
                            ?? param.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.CSharp.PredefinedType, TreeSitterSyntax.CSharp.NullableType));
                if (pType.IsValid())
                {
                    var pText = pType.Text.Trim();
                    if (pText.Contains("IAsyncStreamReader"))
                    {
                        hasClientStream = true;
                        symbol.RequestType = CleanTypeName(pText);
                    }
                    else if (pText.Contains("IServerStreamWriter"))
                    {
                        hasServerStream = true;
                        symbol.ResponseType = CleanTypeName(pText);
                    }
                    else if (!pText.Contains("ServerCallContext") && !pText.Contains("CancellationToken"))
                    {
                        var pTypeName = CleanTypeName(pText);
                        if (!IsPrimitiveOrSystemType(pTypeName) && symbol.RequestType == null)
                        {
                            symbol.RequestType = pTypeName;
                        }
                    }
                }
            }

            if (hasClientStream && hasServerStream) symbol.OperationType = "BidirectionalStreaming";
            else if (hasClientStream) symbol.OperationType = "ClientStreaming";
            else if (hasServerStream) symbol.OperationType = "ServerStreaming";
        }
    }

    public static bool IsGrpcMethodDeclaration(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return false;

        // Ignore static or extension methods
        if (node.Children.Any(c => c.Text == "static" || (c.Is(TreeSitterSyntax.CSharp.Modifier) && c.Text == "static")))
        {
            return false;
        }

        var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters)
                        ?? node.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return false;

        var paramText = paramList.Text;
        if (!paramText.Contains("ServerCallContext")) return false;
        if (paramText.Contains("this ServerCallContext") || paramText.Contains("this  ServerCallContext")) return false;

        var isOverride = node.Children.Any(c => c.Text == "override" || (c.Is(TreeSitterSyntax.CSharp.Modifier) && c.Text == "override"));

        var parentClassNode = GetParentClassNode(node);
        var baseList = parentClassNode?.FindChildOfType("base_list");
        var hasBaseClass = baseList.IsValid() && baseList.Text.Contains("Base");

        var parentClassName = GetParentClassName(node);
        var hasGrpcName = !string.IsNullOrEmpty(parentClassName) &&
            (parentClassName.EndsWith("GrpcService", StringComparison.OrdinalIgnoreCase) ||
             parentClassName.EndsWith("Grpc", StringComparison.OrdinalIgnoreCase));

        return isOverride || hasBaseClass || hasGrpcName;
    }

    private static Node? GetParentClassNode(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                return parent;
            }
            parent = parent.Parent;
        }
        return null;
    }

    public static bool IsGrpcClientCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;

        var root = node;
        while (root.Parent.IsValid()) root = root.Parent;
        var rootText = root.Text;
        if (!ContainsGrpcSignals(rootText)) return false;

        return TryExtractGrpcClientCall(node, out _, out _);
    }

    private static bool ContainsGrpcSignals(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains("Grpc", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("GrpcChannel", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("AddGrpcClient", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ServerCallContext", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryExtractGrpcClientCall(
        Node node,
        out string receiverText,
        out string methodName)
    {
        receiverText = "";
        methodName = "";

        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return false;

        var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
        var exprChild = func.GetField(TreeSitterSyntax.Fields.Expression);
        if (!nameChild.IsValid() || !exprChild.IsValid()) return false;

        var mName = nameChild.Text;
        var angleIdx = mName.IndexOf('<');
        if (angleIdx > 0) mName = mName[..angleIdx].Trim();

        if (mName is "Dispose" or "ToString" or "Equals" or "GetHashCode" or "GetType" or "Configure" or "Add" or "Wait" or "AsTask")
            return false;

        var rText = exprChild.Text.Trim();
        if (rText.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
            rText = rText[5..].Trim();
        if (rText.EndsWith("()", StringComparison.Ordinal))
            rText = rText[..^2].Trim();
        if (rText.Contains('.'))
            rText = rText[(rText.LastIndexOf('.') + 1)..].Trim();

        var isClientReceiver = rText.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
                               (rText.StartsWith("Get", StringComparison.OrdinalIgnoreCase) && rText.EndsWith("Client", StringComparison.OrdinalIgnoreCase)) ||
                               rText.Equals("client", StringComparison.OrdinalIgnoreCase) ||
                               rText.Equals("_client", StringComparison.OrdinalIgnoreCase);

        if (!isClientReceiver) return false;

        receiverText = rText;
        methodName = mName;
        return true;
    }

    public static string? ExtractClientTarget(Node node)
    {
        if (!TryExtractGrpcClientCall(node, out var receiverText, out var methodName))
            return null;

        var rpcMethod = methodName;
        if (rpcMethod.EndsWith("Async", StringComparison.Ordinal) && rpcMethod.Length > 5)
        {
            rpcMethod = rpcMethod[..^5];
        }

        var stem = ExtractServiceStem(node, receiverText);
        if (string.IsNullOrEmpty(stem)) stem = "unknown-service";

        return $"grpc://{stem.ToLowerInvariant()}/{rpcMethod}";
    }

    public static string ExtractServiceStem(Node node, string receiverText)
    {
        var clean = receiverText.Trim();
        if (clean.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
            clean = clean[5..].Trim();
        if (clean.EndsWith("()", StringComparison.Ordinal))
            clean = clean[..^2].Trim();
        if (clean.Contains('.'))
            clean = clean[(clean.LastIndexOf('.') + 1)..].Trim();

        clean = clean.TrimStart('_');

        if (clean.StartsWith("Get", StringComparison.OrdinalIgnoreCase) && clean.Length > 3)
            clean = clean[3..];

        if (clean.EndsWith("Client", StringComparison.OrdinalIgnoreCase) && clean.Length > 6)
            clean = clean[..^6];

        if (clean.EndsWith("Grpc", StringComparison.OrdinalIgnoreCase) && clean.Length > 4)
            clean = clean[..^4];

        if (clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) && clean.Length > 7)
            clean = clean[..^7];

        if (!string.IsNullOrWhiteSpace(clean))
            return clean;

        var parentClass = GetParentClassNode(node);
        if (parentClass.IsValid())
        {
            var rName = receiverText.Trim().TrimStart('_');
            foreach (var child in parentClass.FindDescendantsOfType(TreeSitterSyntax.CSharp.VariableDeclarator))
            {
                var vName = child.GetField(TreeSitterSyntax.Fields.Name);
                if (vName.IsValid() && vName.Text.TrimStart('_').Equals(rName, StringComparison.OrdinalIgnoreCase))
                {
                    var parentDecl = child.Parent;
                    var typeNode = parentDecl?.GetField(TreeSitterSyntax.Fields.Type);
                    if (typeNode.IsValid())
                    {
                        var tStem = ExtractStemFromTypeName(typeNode.Text);
                        if (!string.IsNullOrEmpty(tStem)) return tStem;
                    }
                }
            }

            var className = GetParentClassName(node);
            if (!string.IsNullOrEmpty(className))
            {
                var cStem = className;
                if (cStem.EndsWith("Service", StringComparison.OrdinalIgnoreCase) && cStem.Length > 7)
                    cStem = cStem[..^7];
                if (cStem.EndsWith("Client", StringComparison.OrdinalIgnoreCase) && cStem.Length > 6)
                    cStem = cStem[..^6];
                if (!string.IsNullOrEmpty(cStem)) return cStem;
            }
        }

        return "";
    }

    private static string ExtractStemFromTypeName(string typeName)
    {
        var clean = typeName.Trim();
        if (clean.Contains('.')) clean = clean[(clean.LastIndexOf('.') + 1)..];
        if (clean.StartsWith("Grpc", StringComparison.OrdinalIgnoreCase)) clean = clean[4..];
        if (clean.EndsWith("Client", StringComparison.OrdinalIgnoreCase)) clean = clean[..^6];
        if (clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase)) clean = clean[..^7];
        return clean;
    }

    private static string? GetParentClassName(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                var name = parent.GetField(TreeSitterSyntax.Fields.Name);
                if (name.IsValid()) return name.Text;
            }
            parent = parent.Parent;
        }
        return null;
    }

    private static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf('<');
            if (genericIdx > 0 && type.EndsWith('>'))
            {
                var outer = type[..genericIdx].Trim();
                if (outer is "Task" or "ValueTask" or "IAsyncStreamReader" or "IServerStreamWriter")
                {
                    type = type[(genericIdx + 1)..^1].Trim();
                    continue;
                }
            }
            break;
        }
        return type;
    }

    private static bool IsPrimitiveOrSystemType(string type)
    {
        return type is "int" or "long" or "string" or "bool" or "double" or "float" or "decimal" or "Guid" or "DateTime" or "DateTimeOffset" or "CancellationToken" or "ServerCallContext" or "object";
    }
}
