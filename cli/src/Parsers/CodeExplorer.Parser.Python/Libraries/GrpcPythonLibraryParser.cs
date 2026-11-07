using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class GrpcPythonLibraryParser : DeclarativeSemanticExtension
{
    private static readonly ConcurrentDictionary<string, (Dictionary<string, string> ClassToService, Dictionary<string, string> VarToService)> FileMapCache = new();
    private static readonly Regex ServicerInheritanceRegex = new(@"(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+?)Servicer\b", RegexOptions.Compiled);
    private static readonly Regex AddServicerRegex = new(@"(?:[A-Za-z0-9_]+\.)?add_([A-Za-z0-9_]+?)Servicer_to_server\b", RegexOptions.Compiled);
    private static readonly Regex StubConstructorRegex = new(@"(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+?)Stub\b", RegexOptions.Compiled);
    private static readonly Regex StubVarRegex = new(@"([A-Za-z0-9_]+?)(?:_svc|_service)?(?:_stub|_client)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public GrpcPythonLibraryParser()
    {
        Type = SemanticExtensionType.GrpcFramework;
        Name = "gRPC Python";
        Id = "grpc-python";
        SupportedPatterns =
        [
            "grpcio*",
            "grpc*"
        ];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.FunctionDefinition, TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (IsGrpcServerMethod(node, out _, out _)) return OntologyConstants.NodeLabels.EntryPoint;
                    if (IsGrpcClientCall(node, out _, out _)) return OntologyConstants.NodeLabels.ExternalService;
                    return null;
                },
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (IsGrpcServerMethod(node, out var serviceName, out var methodName)) return $"RPC:/{serviceName}/{methodName}";
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
                EnrichSymbol = EnrichGrpcPythonSymbol
            }
        ];
    }

    private static void EnrichGrpcPythonSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsGrpcServerMethod(node, out _, out _))
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

    private static bool IsGrpcServerMethod(Node node, out string serviceName, out string methodName)
    {
        serviceName = "";
        methodName = "";

        if (!node.Is(TreeSitterSyntax.Python.FunctionDefinition)) return false;

        var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;
        var nameText = nameNode.Text;
        if (string.IsNullOrEmpty(nameText) || !char.IsUpper(nameText[0])) return false;

        // Check parameters: gRPC servicer methods typically take (self, request, context)
        var paramsNode = node.GetChildForField(TreeSitterSyntax.Fields.Parameters);
        if (!paramsNode.IsValid()) return false;
        var paramText = paramsNode.Text;
        if (!paramText.Contains("context", StringComparison.OrdinalIgnoreCase) &&
            !paramText.Contains("request", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Find enclosing class
        var classNode = FindEnclosingClass(node);
        if (classNode == null || !classNode.IsValid()) return false;

        var classNameNode = classNode.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (!classNameNode.IsValid()) return false;
        var className = classNameNode.Text;

        // Skip generated stub classes
        if (className.EndsWith("Servicer", StringComparison.OrdinalIgnoreCase) ||
            className.StartsWith("Unimplemented", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var root = GetFileRoot(node);
        var rootKey = !string.IsNullOrEmpty(node.Document?.FilePath)
            ? node.Document.FilePath
            : ((root as SyntaxDocument)?.FilePath ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root).ToString());
        var (classToService, _) = FileMapCache.GetOrAdd(rootKey, _ => BuildFileMaps(root));

        if (classToService.TryGetValue(className, out var mappedService))
        {
            serviceName = mappedService;
            methodName = nameText;
            return true;
        }

        return false;
    }

    private static bool IsGrpcClientCall(Node node, out string serviceName, out string methodName)
    {
        serviceName = "";
        methodName = "";

        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;

        var func = node.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && node.Children.Count > 0) func = node.Children[0];
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Python.Attribute)) return false;

        var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
        if (!attr.IsValid()) return false;
        var method = attr.Text;
        if (string.IsNullOrEmpty(method) || !char.IsUpper(method[0])) return false;

        var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object);
        if (!obj.IsValid()) return false;
        var objText = obj.Text;

        if (objText is "self" or "cls" or "logger" or "logging" or "sys" or "os" or "time")
        {
            return false;
        }

        // 1. Direct constructor call: demo_pb2_grpc.ProductCatalogServiceStub(channel).ListProducts(...)
        var directStubMatch = StubConstructorRegex.Match(objText);
        if (directStubMatch.Success)
        {
            var raw = directStubMatch.Groups[1].Value;
            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
            serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
            methodName = method;
            return true;
        }

        // 2. Look up variable in file map cache
        var root = GetFileRoot(node);
        var rootKey = !string.IsNullOrEmpty(node.Document?.FilePath)
            ? node.Document.FilePath
            : ((root as SyntaxDocument)?.FilePath ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root).ToString());
        var (_, varToService) = FileMapCache.GetOrAdd(rootKey, _ => BuildFileMaps(root));

        if (varToService.TryGetValue(objText, out var mappedService))
        {
            serviceName = mappedService;
            methodName = method;
            return true;
        }

        // 3. Fallback: variable name heuristic (e.g. product_catalog_stub -> ProductCatalogService)
        var leafVar = objText.Contains('.') ? objText.Split('.')[^1] : objText;
        if (leafVar.EndsWith("stub", StringComparison.OrdinalIgnoreCase) || leafVar.EndsWith("client", StringComparison.OrdinalIgnoreCase))
        {
            var stem = leafVar;
            if (stem.EndsWith("_stub", StringComparison.OrdinalIgnoreCase)) stem = stem[..^"_stub".Length];
            else if (stem.EndsWith("stub", StringComparison.OrdinalIgnoreCase)) stem = stem[..^"stub".Length];
            else if (stem.EndsWith("_client", StringComparison.OrdinalIgnoreCase)) stem = stem[..^"_client".Length];
            else if (stem.EndsWith("client", StringComparison.OrdinalIgnoreCase)) stem = stem[..^"client".Length];

            stem = stem.TrimEnd('_');
            if (!string.IsNullOrEmpty(stem))
            {
                var clean = string.Concat(stem.Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
                serviceName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
                methodName = method;
                return true;
            }
        }

        return false;
    }

    private static Node? FindEnclosingClass(Node node)
    {
        var curr = node.Parent;
        while (curr != null && curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.Python.ClassDefinition)) return curr;
            curr = curr.Parent;
        }
        return null;
    }

    private static Node GetFileRoot(Node node)
    {
        var curr = node;
        while (curr.Parent != null && curr.Parent.IsValid())
        {
            curr = curr.Parent;
        }
        return curr;
    }

    private static (Dictionary<string, string> ClassToService, Dictionary<string, string> VarToService) BuildFileMaps(Node root)
    {
        var classToService = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var varToService = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Map class definitions with superclasses
        foreach (var desc in root.Descendants())
        {
            if (desc.Is(TreeSitterSyntax.Python.ClassDefinition))
            {
                var nameNode = desc.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid())
                {
                    var className = nameNode.Text;
                    var superclasses = desc.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                    if (superclasses.IsValid())
                    {
                        var match = ServicerInheritanceRegex.Match(superclasses.Text);
                        if (match.Success)
                        {
                            var raw = match.Groups[1].Value;
                            var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                            var sName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
                            classToService[className] = sName;
                        }
                    }
                }
            }
            else if (desc.Is(TreeSitterSyntax.Python.Assignment))
            {
                var left = desc.GetChildForField(TreeSitterSyntax.Fields.Left) ?? (desc.Children.Count >= 2 ? desc.Children[0] : default);
                var right = desc.GetChildForField(TreeSitterSyntax.Fields.Right) ?? (desc.Children.Count >= 2 ? desc.Children[^1] : default);
                if (left.IsValid() && right.IsValid())
                {
                    var stubMatch = StubConstructorRegex.Match(right.Text);
                    if (stubMatch.Success)
                    {
                        var raw = stubMatch.Groups[1].Value;
                        var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                        var sName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";
                        varToService[left.Text.Trim()] = sName;
                    }

                    // Check if variable is instantiated from a mapped service class: service = DummyEmailService()
                    if (classToService.TryGetValue(right.Text.Trim('(', ')', ' '), out var sClass))
                    {
                        classToService[left.Text.Trim()] = sClass;
                    }
                }
            }
            else if (desc.Is(TreeSitterSyntax.Python.Call))
            {
                // add_EmailServiceServicer_to_server(service, server)
                var callText = desc.Text;
                var addMatch = AddServicerRegex.Match(callText);
                if (addMatch.Success)
                {
                    var raw = addMatch.Groups[1].Value;
                    var clean = char.ToUpperInvariant(raw[0]) + raw[1..];
                    var sName = clean.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ? clean : $"{clean}Service";

                    var args = desc.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                    if (args.IsValid() && args.Children.Count > 0)
                    {
                        var firstArg = args.Children[0].Text.Trim();
                        classToService[firstArg] = sName;
                    }
                }
            }
        }

        // Propagate class inheritance in case of subclass (e.g. DummyEmailService inherits from BaseEmailService)
        foreach (var desc in root.Descendants())
        {
            if (desc.Is(TreeSitterSyntax.Python.ClassDefinition))
            {
                var nameNode = desc.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && !classToService.ContainsKey(nameNode.Text))
                {
                    var superclasses = desc.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                    if (superclasses.IsValid())
                    {
                        foreach (var (baseClass, sName) in classToService.ToList())
                        {
                            if (superclasses.Text.Contains(baseClass, StringComparison.OrdinalIgnoreCase))
                            {
                                classToService[nameNode.Text] = sName;
                                break;
                            }
                        }
                    }
                }
            }
        }

        return (classToService, varToService);
    }
}
