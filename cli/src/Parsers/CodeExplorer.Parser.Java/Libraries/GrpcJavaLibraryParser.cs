using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java.Libraries;

public class GrpcJavaLibraryParser : DeclarativeSemanticExtension
{
    public GrpcJavaLibraryParser()
    {
        Id = "grpc-java";
        Name = "gRPC Java";
        Type = SemanticExtensionType.GrpcFramework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "io.grpc",
            "io.grpc.*",
            "net.devh.boot.grpc",
            "net.devh.boot.grpc.*",
            "io.grpc.stub",
            "io.quarkus.grpc*"
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[]
                {
                    TreeSitterSyntax.Java.MethodDeclaration,
                    TreeSitterSyntax.Java.Annotation,
                    TreeSitterSyntax.Java.MarkerAnnotation
                }.ToFrozenSet(),
                MapNodeType = MapNodeTypeCore,
                ExtractIdentifier = (node, ctx, _, _) => ExtractIdentifierCore(node),
                CollectReferences = CollectReferencesCore,
                EnrichSymbol = EnrichSymbolCore
            }
        ];
    }

    private static string? MapNodeTypeCore(Node node, ParsingContext ctx)
    {
        if (IsGrpcJavaMethod(node)) return OntologyConstants.NodeLabels.EntryPoint;
        if (HasGrpcClient(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    private static string? ExtractIdentifierCore(Node node)
    {
        if (IsGrpcJavaMethod(node))
        {
            var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier);
            var methodName = nameNode.IsValid() ? nameNode.Text : "anonymous";
            var serviceName = GetParentClassName(node);
            return string.IsNullOrEmpty(serviceName) ? $"RPC:{methodName}" : $"RPC:/{serviceName}/{methodName}";
        }
        if (HasGrpcClient(node))
        {
            return ExtractGrpcClientService(node);
        }
        return null;
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (HasGrpcClient(node))
        {
            var target = ExtractGrpcClientService(node);
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
            }
        }
    }

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsGrpcJavaMethod(node)) return;

        symbol.Protocol = "gRPC";
        symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.Unary;

        var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters);
        if (paramList.IsValid())
        {
            bool hasClientStream = false;
            bool hasServerStream = false;

            foreach (var param in paramList.Children)
            {
                if (!param.Is(TreeSitterSyntax.Java.FormalParameter)) continue;
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

            if (hasClientStream && hasServerStream) symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.BidirectionalStreaming;
            else if (hasClientStream) symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.ClientStreaming;
            else if (hasServerStream) symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.ServerStreaming;
            else symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.Unary;
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
            return rawType[(idx + 1)..end].Trim();
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

    private static bool HasGrpcClient(Node node)
    {
        return node.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation) &&
               node.Text.Contains("GrpcClient", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractGrpcClientService(Node node)
    {
        var text = node.Text;
        var first = text.IndexOf('"');
        var last = text.LastIndexOf('"');
        if (first >= 0 && last > first)
        {
            return $"grpc:{text[(first + 1)..last].Trim()}";
        }
        return "grpc:unknown-service";
    }
}
