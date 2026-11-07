using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class QuartzNetLibraryParser : DeclarativeSemanticExtension
{
    private const string QuartzPrefix = "quartz:";
    private const string JobInterfaceName = "IJob";

    private static readonly HashSet<string> ScheduleMethods = new(StringComparer.Ordinal)
    {
        "ScheduleJob", "AddJob", "TriggerJob", "CheckExists"
    };

    public QuartzNetLibraryParser()
    {
        Id = "quartz-net";
        Name = "Quartz.NET";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns = ["Quartz", "Quartz.*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.CSharp.ClassDeclaration,
                    TreeSitterSyntax.CSharp.InvocationExpression
                ]),
                MapNodeType = (node, ctx) => IsQuartzJobClass(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = (node, _, _, _) => IsQuartzJobClass(node, out var jobName) ? $"QuartzJob:{jobName}" : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsQuartzJobClass(node, out var jobClassName))
        {
            references.Add(new Reference(scopeSymbolId, QuartzPrefix + jobClassName, OntologyConstants.Relationships.SubscribesTo));
            return;
        }

        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        if (TryExtractQuartzScheduledJob(node, out var scheduledTarget))
        {
            references.Add(new Reference(scopeSymbolId, QuartzPrefix + scheduledTarget, OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static bool IsQuartzJobClass(Node node, out string className)
    {
        className = string.Empty;
        if (!node.Is(TreeSitterSyntax.CSharp.ClassDeclaration)) return false;

        var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return false;

        var implementsIJob = baseList.Children.Any(c =>
            c.Text == JobInterfaceName || c.Text.EndsWith("." + JobInterfaceName, StringComparison.Ordinal));
        if (!implementsIJob) return false;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        className = nameNode.IsValid() && !string.IsNullOrEmpty(nameNode.Text) ? nameNode.Text : JobInterfaceName;
        return true;
    }

    private static bool TryExtractQuartzScheduledJob(Node invocation, out string scheduledTarget)
    {
        scheduledTarget = string.Empty;
        var func = invocation.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
        {
            return false;
        }

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name) ?? (func.Children.Count > 0 ? func.Children[^1] : default);
        if (!nameNode.IsValid()) return false;

        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var idNode = nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier);
            if (idNode.IsValid() && (idNode.Text is "AddJob" or "ForJob" or "Create"))
            {
                var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                var firstType = typeArgs.IsValid()
                    ? typeArgs.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
                    : default;
                if (firstType.IsValid() && !string.IsNullOrEmpty(firstType.Text))
                {
                    scheduledTarget = firstType.Text;
                    return true;
                }
            }
        }

        if (ScheduleMethods.Contains(nameNode.Text))
        {
            scheduledTarget = nameNode.Text;
            return true;
        }

        return false;
    }
}
