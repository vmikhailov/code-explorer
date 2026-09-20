using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class GrpcCSharpLibraryParser : ILibraryParser
{
    public string Name => "gRPC C#";
    public string Id => "grpc-csharp";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["Grpc.Core", "Grpc.Net.Client", "Grpc.AspNetCore.Server"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrpcMethodDeclaration(node)) return OntologyConstants.NodeLabels.EntryPoint;
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
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) { }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsGrpcMethodDeclaration(node)) return;

        symbol.Protocol = "gRPC";
        symbol.OperationType = "Unary";

        // Response type from method return type
        var typeNode = node.GetField("returns")
                       ?? node.GetField(TreeSitterSyntax.Fields.Type)
                       ?? node.GetField("return_type")
                       ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, "predefined_type", "nullable_type", "generic_name", "type_identifier"));
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
                            ?? param.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, "predefined_type", "nullable_type", "generic_name", "type_identifier"));
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

    private static bool IsGrpcMethodDeclaration(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return false;

        var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters)
                        ?? node.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return false;

        return paramList.Text.Contains("ServerCallContext");
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
