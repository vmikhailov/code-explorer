using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class QuartzNetLibraryParser : ISemanticExtension
{
    private const string QuartzPrefix = "quartz:";
    private const string JobInterfaceName = "IJob";

    public string Name => "Quartz.NET";
    public string Id => "quartz-net";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["Quartz", "Quartz.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    private static readonly HashSet<string> ScheduleMethods = new(StringComparer.Ordinal)
    {
        "ScheduleJob", "AddJob", "TriggerJob", "CheckExists"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsQuartzJobClass(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsQuartzJobClass(node, out var jobName) ? $"QuartzJob:{jobName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
