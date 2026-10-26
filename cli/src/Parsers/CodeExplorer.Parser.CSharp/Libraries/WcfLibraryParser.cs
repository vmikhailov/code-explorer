using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class WcfLibraryParser : ISemanticExtension
{
    public string Name => "WCF";
    public string Id => "wcf";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "System.ServiceModel",
        "System.ServiceModel.*",
        "CoreWCF",
        "CoreWCF.*"
    ];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> WcfClientBasePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClientBase",
        "DuplexClientBase",
        "WcfProxyBase",
        "ChannelFactory",
        "DuplexChannelFactory",
        "WebChannelFactory"
    };

    private static readonly HashSet<string> ExcludedInterfaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "IDisposable",
        "IAsyncDisposable",
        "ICommunicationObject",
        "IClientChannel",
        "IServiceBehavior",
        "IEndpointBehavior",
        "IContractBehavior",
        "IOperationBehavior",
        "IErrorHandler"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsWcfServerEndpointNode(node, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        if (IsWcfClientNode(node, out _))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsWcfServerEndpointNode(node, out var serverContract))
        {
            return $"RPC:/{NormalizeContractName(serverContract)}";
        }
        if (IsWcfClientNode(node, out var clientContract))
        {
            return $"/{NormalizeContractName(clientContract)}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsWcfServerEndpointNode(node, out _))
        {
            symbol.Protocol = "gRPC";
            symbol.OperationType = "WCF";
        }
    }

    public static bool IsWcfServerEndpointNode(Node node, out string contractName)
    {
        contractName = string.Empty;

        // 1. [ServiceBehavior] attribute on a service implementation class
        if (node.Is(TreeSitterSyntax.CSharp.Attribute))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name)
                           ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier);
            if (!nameNode.IsValid()) return false;

            var attrName = StripQualifier(nameNode.Text);
            if (attrName is "ServiceBehavior" or "ServiceBehaviorAttribute")
            {
                var classDecl = FindAncestorClass(node);
                if (classDecl != null && classDecl.IsValid())
                {
                    var contract = ExtractContractFromClassDeclaration(classDecl);
                    if (!string.IsNullOrEmpty(contract))
                    {
                        contractName = contract;
                        return true;
                    }
                }
            }
            return false;
        }

        // 2. serviceHost.AddServiceEndpoint(typeof(IMyService), ...) or app.UseServiceModel(builder => builder.AddServiceEndpoint<TService, IMyService>(...))
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (!func.IsValid()) return false;

            if (func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
                if (!nameChild.IsValid()) return false;

                var methodText = nameChild.Text;
                if (methodText.StartsWith("AddServiceEndpoint", StringComparison.Ordinal))
                {
                    // Check generic type arguments first: AddServiceEndpoint<TService, IContract>
                    var typeArgs = nameChild.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                   ?? func.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                   ?? node.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    if (typeArgs.IsValid())
                    {
                        var args = ExtractTypeArguments(typeArgs);
                        var contractArg = args.LastOrDefault(a => !IsGenericTypeParameter(a));
                        if (!string.IsNullOrEmpty(contractArg))
                        {
                            contractName = contractArg;
                            return true;
                        }
                    }

                    // Check first argument: typeof(IContract)
                    var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                    if (argList.IsValid())
                    {
                        var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                        if (firstArg.IsValid())
                        {
                            var typeofContract = ExtractTypeofArgument(firstArg.Text);
                            if (!string.IsNullOrEmpty(typeofContract) && !IsGenericTypeParameter(typeofContract))
                            {
                                contractName = typeofContract;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        // 3. new ServiceHost(typeof(MyService), ...)
        if (node.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type)
                           ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.Common.Identifier));
            if (!typeNode.IsValid()) return false;

            var typeName = StripQualifier(typeNode.Text);
            if (typeName.StartsWith("ServiceHost", StringComparison.Ordinal))
            {
                var argList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
                if (argList.IsValid())
                {
                    var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                    if (firstArg.IsValid())
                    {
                        var typeofTarget = ExtractTypeofArgument(firstArg.Text);
                        if (!string.IsNullOrEmpty(typeofTarget) && !IsGenericTypeParameter(typeofTarget))
                        {
                            contractName = typeofTarget;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static bool IsWcfClientNode(Node node, out string contractName)
    {
        contractName = string.Empty;

        // 1. new ChannelFactory<IContract>(...) or new DuplexChannelFactory<IContract>(...)
        if (node.Is(TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type)
                           ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.Common.Identifier));
            if (!typeNode.IsValid()) return false;

            if (TryExtractWcfGenericType(typeNode, out var genericBase, out var typeArg))
            {
                if (genericBase is "ChannelFactory" or "DuplexChannelFactory" or "WebChannelFactory" &&
                    !IsGenericTypeParameter(typeArg))
                {
                    contractName = typeArg;
                    return true;
                }
            }
            return false;
        }

        // 2. ChannelFactory<IContract>.CreateChannel(...)
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameChild = func.GetField(TreeSitterSyntax.Fields.Name);
                var exprChild = func.GetField(TreeSitterSyntax.Fields.Expression);
                if (nameChild.IsValid() && nameChild.Text == "CreateChannel" && exprChild.IsValid())
                {
                    if (TryExtractWcfGenericType(exprChild, out var genericBase, out var typeArg) &&
                        genericBase is "ChannelFactory" or "DuplexChannelFactory" or "WebChannelFactory" &&
                        !IsGenericTypeParameter(typeArg))
                    {
                        contractName = typeArg;
                        return true;
                    }
                }
            }
            return false;
        }

        // 3. Class inheriting from ClientBase<IContract>, DuplexClientBase<IContract>, or WcfProxyBase<IContract> in base_list
        if (node.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            if (node.Parent.IsValid() && node.Parent.Is(TreeSitterSyntax.CSharp.BaseList))
            {
                if (TryExtractWcfGenericType(node, out var genericBase, out var typeArg) &&
                    WcfClientBasePrefixes.Contains(genericBase) &&
                    !IsGenericTypeParameter(typeArg))
                {
                    contractName = typeArg;
                    return true;
                }
            }
        }

        return false;
    }

    private static Node? FindAncestorClass(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                return curr;
            }
            curr = curr.Parent;
        }
        return null;
    }

    private static string ExtractContractFromClassDeclaration(Node classDecl)
    {
        var className = classDecl.GetField(TreeSitterSyntax.Fields.Name)?.Text
                        ?? classDecl.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text
                        ?? string.Empty;

        var baseList = classDecl.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (baseList.IsValid())
        {
            var candidates = new List<string>();
            foreach (var child in baseList.Children)
            {
                if (child.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.QualifiedName))
                {
                    var name = StripQualifier(child.Text);
                    if (!string.IsNullOrEmpty(name) && !ExcludedInterfaces.Contains(name))
                    {
                        candidates.Add(name);
                    }
                }
            }

            // Prefer interface matching I{ClassName} or first I* interface
            if (!string.IsNullOrEmpty(className))
            {
                var exactInterface = candidates.FirstOrDefault(c => c.Equals("I" + className, StringComparison.OrdinalIgnoreCase));
                if (exactInterface != null) return exactInterface;
            }

            var serviceInterface = candidates.FirstOrDefault(c => c.StartsWith('I') && c.Length > 1 && char.IsUpper(c[1]));
            if (serviceInterface != null) return serviceInterface;
        }

        return className;
    }

    private static bool TryExtractWcfGenericType(Node node, out string genericBaseName, out string firstTypeArg)
    {
        genericBaseName = string.Empty;
        firstTypeArg = string.Empty;

        var text = node.Text.Trim();
        var openIdx = text.IndexOf('<');
        var closeIdx = text.LastIndexOf('>');
        if (openIdx <= 0 || closeIdx <= openIdx + 1)
        {
            return false;
        }

        genericBaseName = StripQualifier(text[..openIdx].Trim());
        var inner = text[(openIdx + 1)..closeIdx].Trim();
        var firstComma = inner.IndexOf(',');
        firstTypeArg = StripQualifier((firstComma >= 0 ? inner[..firstComma] : inner).Trim());
        return !string.IsNullOrEmpty(genericBaseName) && !string.IsNullOrEmpty(firstTypeArg);
    }

    private static List<string> ExtractTypeArguments(Node typeArgList)
    {
        var result = new List<string>();
        var text = typeArgList.Text.Trim().TrimStart('<').TrimEnd('>');
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = StripQualifier(part.Trim());
            if (!string.IsNullOrEmpty(cleaned))
            {
                result.Add(cleaned);
            }
        }
        return result;
    }

    private static string? ExtractTypeofArgument(string argText)
    {
        var trimmed = argText.Trim();
        if (!trimmed.StartsWith("typeof(", StringComparison.Ordinal) || !trimmed.EndsWith(')'))
        {
            return null;
        }
        var inner = trimmed["typeof(".Length..^1].Trim();
        return StripQualifier(inner);
    }

    private static string StripQualifier(string typeName)
    {
        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName[(lastDot + 1)..].Trim() : typeName.Trim();
    }

    private static bool IsGenericTypeParameter(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return true;
        if (typeName.Length == 1 && char.IsUpper(typeName[0])) return true;
        if (typeName.StartsWith('T') && typeName.Length > 1 && char.IsUpper(typeName[1]) &&
            typeName is "TChannel" or "TService" or "TContract" or "TClient" or "TInterface" or "TProxy")
        {
            return true;
        }
        return false;
    }

    public static string NormalizeContractName(string contractOrServiceName)
    {
        var clean = StripQualifier(contractOrServiceName);
        if (clean.Length > 2 && clean.StartsWith('I') && char.IsUpper(clean[1]))
        {
            clean = clean[1..];
        }
        return clean;
    }
}

