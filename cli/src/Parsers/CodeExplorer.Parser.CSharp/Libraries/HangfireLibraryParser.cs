using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class HangfireLibraryParser : DeclarativeSemanticExtension
{
    private const string HangfirePrefix = "hangfire:";
    private const string QueueAttributeName = "Queue";
    private const string QueueAttrSuffix = "QueueAttribute";

    private static readonly HashSet<string> EnqueueMethods = new(StringComparer.Ordinal)
    {
        "Enqueue", "Schedule", "AddOrUpdate", "ContinueJobWith", "TriggerJob"
    };

    private static readonly HashSet<string> ReceiverHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "BackgroundJob", "RecurringJob", "jobClient", "backgroundJob", "recurringJob"
    };

    public HangfireLibraryParser()
    {
        Id = "hangfire";
        Name = "Hangfire";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns = ["Hangfire", "Hangfire.*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.CSharp.MethodDeclaration,
                    TreeSitterSyntax.CSharp.ClassDeclaration,
                    TreeSitterSyntax.CSharp.InvocationExpression
                ]),
                MapNodeType = (node, ctx) => TryGetQueueAttributeValue(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = (node, _, _, _) => TryGetQueueAttributeValue(node, out var q) ? $"HangfireQueue:{q}" : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryGetQueueAttributeValue(node, out var queueName))
        {
            references.Add(new Reference(scopeSymbolId, HangfirePrefix + queueName, OntologyConstants.Relationships.SubscribesTo));
            return;
        }

        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        if (TryExtractHangfireJobTarget(node, out var targetJob))
        {
            references.Add(new Reference(scopeSymbolId, HangfirePrefix + targetJob, OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static bool TryGetQueueAttributeValue(Node node, out string queueName)
    {
        queueName = string.Empty;
        if (!node.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.ClassDeclaration))
        {
            return false;
        }

        foreach (var attrList in node.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeList))
        {
            foreach (var attr in attrList.FindChildrenOfType(TreeSitterSyntax.CSharp.Attribute))
            {
                var nameNode = attr.GetField(TreeSitterSyntax.Fields.Name) ?? attr.Children.FirstOrDefault();
                if (!nameNode.IsValid()) continue;

                var attrName = nameNode.Text;
                if (attrName is QueueAttributeName or QueueAttrSuffix)
                {
                    var argList = attr.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgumentList);
                    var firstArg = argList.IsValid() ? argList.FindChildOfType(TreeSitterSyntax.CSharp.AttributeArgument) : default;
                    var raw = firstArg.IsValid() ? firstArg.Text.Trim('"', '\'', ' ') : "default";
                    queueName = string.IsNullOrEmpty(raw) ? "default" : raw;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryExtractHangfireJobTarget(Node invocation, out string jobTarget)
    {
        jobTarget = string.Empty;
        var func = invocation.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            return false;
        }

        var exprNode = func.GetField(TreeSitterSyntax.Fields.Expression) ?? (func.Children.Count > 0 ? func.Children[0] : default);
        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name) ?? (func.Children.Count > 0 ? func.Children[^1] : default);
        if (!nameNode.IsValid()) return false;

        var idChild = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)
            : default;
        var rawMethod = idChild.IsValid() ? idChild.Text : nameNode.Text;

        if (!EnqueueMethods.Contains(rawMethod)) return false;

        var receiverText = exprNode.IsValid() ? exprNode.Text : string.Empty;
        var matchesReceiver = ReceiverHints.Any(h => receiverText.Contains(h, StringComparison.OrdinalIgnoreCase));
        if (!matchesReceiver && !nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            return false;
        }

        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var typeArgList = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
            var firstType = typeArgList.IsValid()
                ? typeArgList.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
                : default;
            if (firstType.IsValid() && !string.IsNullOrEmpty(firstType.Text))
            {
                jobTarget = firstType.Text;
                return true;
            }
        }

        var argList = invocation.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (argList.IsValid())
        {
            var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
            if (firstArg.IsValid())
            {
                var argText = firstArg.Text.Trim('"', '\'', ' ');
                if (!string.IsNullOrEmpty(argText) && !argText.Contains("=>", StringComparison.Ordinal))
                {
                    jobTarget = argText;
                    return true;
                }
            }
        }

        jobTarget = rawMethod;
        return true;
    }
}
