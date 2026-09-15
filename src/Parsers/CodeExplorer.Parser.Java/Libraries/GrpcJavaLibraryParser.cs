using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class GrpcJavaLibraryParser : ILibraryParser
{
    public string Type => "framework:grpc";
    public string Name => "gRPC Java";
    public string Id => "grpc-java";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "io.grpc",
        "net.devh.boot.grpc",
        "io.grpc.stub"
    ];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrpcJavaMethod(node)) return OntologyConstants.NodeLabels.EntryPoint;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGrpcJavaMethod(node))
        {
            var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier);
            var methodName = nameNode.IsValid() ? nameNode.Text : "anonymous";
            var serviceName = GetParentClassName(node);
            return string.IsNullOrEmpty(serviceName) ? $"RPC:{methodName}" : $"RPC:/{serviceName}/{methodName}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsGrpcJavaMethod(node)) return;

        symbol.Protocol = "gRPC";
        symbol.OperationType = "Unary";

        var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters);
        if (paramList.IsValid())
        {
            bool hasClientStream = false;
            bool hasServerStream = false;

            foreach (var param in paramList.Children)
            {
                if (!param.Type.Contains("formal_parameter")) continue;
                var pType = param.GetField(TreeSitterSyntax.Fields.Type);
                if (pType.IsValid())
                {
                    var pText = pType.Text.Trim();
                    if (pText.Contains("StreamObserver"))
                    {
                        var inner = ExtractStreamObserverGenericType(pText);
                        // In Java gRPC, StreamObserver parameter can be responseObserver (Unary/ServerStreaming) or return StreamObserver (ClientStreaming)
                        if (symbol.ResponseType == null)
                        {
                            symbol.ResponseType = inner;
                            hasServerStream = true;
                        }
                    }
                    else
                    {
                        var pTypeName = CleanJavaTypeName(pText);
                        if (!IsJavaPrimitiveOrSystemType(pTypeName) && symbol.RequestType == null)
                        {
                            symbol.RequestType = pTypeName;
                        }
                    }
                }
            }

            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid() && typeNode.Text.Contains("StreamObserver"))
            {
                hasClientStream = true;
                symbol.RequestType = ExtractStreamObserverGenericType(typeNode.Text);
            }

            if (hasClientStream && hasServerStream) symbol.OperationType = "BidirectionalStreaming";
            else if (hasClientStream) symbol.OperationType = "ClientStreaming";
            else if (hasServerStream) symbol.OperationType = "ServerStreaming";
            else symbol.OperationType = "Unary";
        }
    }

    private static bool IsGrpcJavaMethod(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Java.MethodDeclaration)) return false;

        var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters);
        if (!paramList.IsValid()) return false;

        return paramList.Text.Contains("StreamObserver");
    }

    private static string? GetParentClassName(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.Is(TreeSitterSyntax.Java.ClassDeclaration))
            {
                var nameNode = parent.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                if (nameNode.IsValid()) return nameNode.Text;
            }
            parent = parent.Parent;
        }
        return null;
    }

    private static string ExtractStreamObserverGenericType(string rawType)
    {
        var idx = rawType.IndexOf('<');
        var end = rawType.LastIndexOf('>');
        if (idx > 0 && end > idx)
        {
            return rawType.Substring(idx + 1, end - idx - 1).Trim();
        }
        return rawType.Trim();
    }

    private static string CleanJavaTypeName(string rawType)
    {
        return rawType.Trim();
    }

    private static bool IsJavaPrimitiveOrSystemType(string type)
    {
        return type is "int" or "long" or "String" or "boolean" or "double" or "float" or "byte" or "short" or "char" or "Integer" or "Long" or "Boolean" or "Double" or "Float" or "UUID" or "Object";
    }
}
