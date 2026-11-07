using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class AzureFunctionsLibraryParser : ISemanticExtension
{
    private const string FunctionAttr = "Function";
    private const string FunctionNameAttr = "FunctionName";
    private const string ServiceBusTriggerAttr = "ServiceBusTrigger";
    private const string QueueTriggerAttr = "QueueTrigger";
    private const string EventHubTriggerAttr = "EventHubTrigger";
    private const string CosmosDbTriggerAttr = "CosmosDBTrigger";

    public string Name => "Azure Functions";
    public string Id => "azure-functions";
    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "Microsoft.Azure.Functions.Worker*",
        "Microsoft.Azure.WebJobs*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryGetFunctionName(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryGetFunctionName(node, out var fnName) ? $"ALL:/azure-func/{fnName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return;
        if (!HasFunctionAttribute(node)) return;

        var paramList = node.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return;

        foreach (var param in paramList.FindChildrenOfType(TreeSitterSyntax.CSharp.Parameter))
        {
            foreach (var attrList in param.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeList))
            {
                foreach (var attr in attrList.FindChildrenOfType(TreeSitterSyntax.CSharp.Attribute))
                {
                    var nameNode = attr.GetField(TreeSitterSyntax.Fields.Name) ?? attr.Children.FirstOrDefault();
                    if (!nameNode.IsValid()) continue;

                    var attrName = nameNode.Text;
                    var firstArg = ExtractFirstAttributeStringArg(attr);
                    if (attrName.StartsWith(ServiceBusTriggerAttr, StringComparison.Ordinal))
                    {
                        references.Add(new Reference(scopeSymbolId, "servicebus:" + (firstArg ?? "queue"), OntologyConstants.Relationships.SubscribesTo));
                    }
                    else if (attrName.StartsWith(QueueTriggerAttr, StringComparison.Ordinal))
                    {
                        references.Add(new Reference(scopeSymbolId, "azure-queue:" + (firstArg ?? "queue"), OntologyConstants.Relationships.SubscribesTo));
                    }
                    else if (attrName.StartsWith(EventHubTriggerAttr, StringComparison.Ordinal))
                    {
                        references.Add(new Reference(scopeSymbolId, "eventhub:" + (firstArg ?? "hub"), OntologyConstants.Relationships.SubscribesTo));
                    }
                    else if (attrName.StartsWith(CosmosDbTriggerAttr, StringComparison.Ordinal))
                    {
                        references.Add(new Reference(scopeSymbolId, "cosmosdb:" + (firstArg ?? "container"), OntologyConstants.Relationships.UsesDb));
                    }
                }
            }
        }
    }

    private static bool TryGetFunctionName(Node node, out string fnName)
    {
        fnName = string.Empty;
        if (!node.Is(TreeSitterSyntax.CSharp.Attribute)) return false;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name) ?? node.Children.FirstOrDefault();
        if (!nameNode.IsValid()) return false;

        var attrName = nameNode.Text;
        if (attrName is FunctionAttr or FunctionNameAttr or "FunctionAttribute" or "FunctionNameAttribute")
        {
            fnName = ExtractFirstAttributeStringArg(node) ?? FunctionAttr;
            return true;
        }
        return false;
    }

    private static bool HasFunctionAttribute(Node methodNode)
    {
        foreach (var attrList in methodNode.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeList))
        {
            foreach (var attr in attrList.FindChildrenOfType(TreeSitterSyntax.CSharp.Attribute))
            {
                if (TryGetFunctionName(attr, out _)) return true;
            }
        }

        return false;
    }

    private static string? ExtractFirstAttributeStringArg(Node attr)
    {
        var argList = attr.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
        if (!argList.IsValid()) return null;

        var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgument);
        if (!firstArg.IsValid()) return null;

        var raw = firstArg.Text.Trim('"', '\'', ' ');
        return string.IsNullOrEmpty(raw) ? null : raw;
    }
}
