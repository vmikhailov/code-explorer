using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser;

internal static class ProjectSummaryBuilder
{
    private const string ModuleInitName = "<module_init>";
    private static readonly IReadOnlySet<string> EmptyFriendSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static ProjectScanSummary BuildAndRegister(
        ProjectNode project,
        IReadOnlyList<SyntaxTree> projectTrees,
        IReadOnlyList<IOntologyNode> projectSemanticNodes,
        IReadOnlyList<RawImport> projectRawImports,
        SymbolResolutionBuffer symbols)
    {
        var addressableSymbols = new List<AddressableSymbolStub>();
        var flowFunctions = new List<FunctionFlowSummary>();
        var moduleInits = new List<ModuleInitStub>();
        var outboundRefs = new List<Reference>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var t = 0; t < projectTrees.Count; t++)
        {
            var fileNode = projectTrees[t].FileNode;
            if (fileNode == null) continue;

            for (var c = 0; c < fileNode.Children.Count; c++)
            {
                var child = fileNode.Children[c];
                if (child is FunctionNode modInitFn &&
                    (modInitFn.IsModuleInit == true || string.Equals(modInitFn.Name, ModuleInitName, StringComparison.Ordinal)))
                {
                    moduleInits.Add(new ModuleInitStub(
                        fileNode.Id,
                        fileNode.Path ?? string.Empty,
                        fileNode.FullPath ?? string.Empty,
                        modInitFn.Id));
                }

                CollectProjectSymbolsRecursive(
                    child,
                    enclosingTypeId: null,
                    fileNode.Path ?? string.Empty,
                    addressableSymbols,
                    flowFunctions,
                    outboundRefs,
                    visited);
            }

            if (fileNode.References.Count > 0)
            {
                outboundRefs.AddRange(fileNode.References);
            }
        }

        IReadOnlySet<string> friendAssemblies = project.TryGetSignals()?.FriendAssemblies is { Count: > 0 } friends
            ? friends
            : EmptyFriendSet;

        var summary = new ProjectScanSummary(
            project.Id,
            project.Name,
            friendAssemblies,
            addressableSymbols,
            flowFunctions,
            moduleInits,
            outboundRefs,
            projectRawImports,
            projectSemanticNodes);

        symbols.ProjectSummaries.Add(summary);
        return summary;
    }

    private static void CollectProjectSymbolsRecursive(
        IOntologyNode node,
        string? enclosingTypeId,
        string defaultFilePath,
        List<AddressableSymbolStub> addressableSymbols,
        List<FunctionFlowSummary> flowFunctions,
        List<Reference> outboundRefs,
        HashSet<string> visited)
    {
        if (!visited.Add(node.Id)) return;

        if (node.References.Count > 0)
        {
            outboundRefs.AddRange(node.References);
        }

        var currentTypeId = enclosingTypeId;
        switch (node)
        {
            case TypeNode tn:
            {
                var vis = AddressableSymbolStub.ParseVisibility(null, tn.IsExported);
                var fp = !string.IsNullOrEmpty(tn.FilePath) ? tn.FilePath : defaultFilePath;
                addressableSymbols.Add(new AddressableSymbolStub(
                    tn.Id,
                    tn.Name,
                    fp,
                    enclosingTypeId,
                    NodeKind.Type,
                    vis));
                currentTypeId = tn.Id;
                break;
            }

            case FunctionNode fn:
            {
                var vis = AddressableSymbolStub.ParseVisibility(fn.Visibility, fn.IsExported);
                var fp = !string.IsNullOrEmpty(fn.FilePath) ? fn.FilePath : defaultFilePath;
                addressableSymbols.Add(new AddressableSymbolStub(
                    fn.Id,
                    fn.Name,
                    fp,
                    enclosingTypeId,
                    NodeKind.Function,
                    vis));

                var fnSignals = fn.TryGetSignals();
                var semParams = fnSignals?.SemanticParams;
                var outSlices = fnSignals?.OutgoingCallSlices;
                if ((semParams is { Count: > 0 }) || (outSlices is { Count: > 0 }) || enclosingTypeId != null)
                {
                    flowFunctions.Add(new FunctionFlowSummary(
                        fn.Id,
                        fn.Name,
                        fp,
                        enclosingTypeId,
                        fn.IsTest,
                        fn.ProjectKind,
                        fn.ProjectName,
                        semParams is { Count: > 0 } ? [.. semParams] : null,
                        outSlices is { Count: > 0 } ? [.. outSlices] : null));
                }
                break;
            }

            case MemberNode mn when enclosingTypeId != null && mn.MemberKind is MemberKind.Field or MemberKind.Property:
            {
                var fp = !string.IsNullOrEmpty(mn.FilePath) ? mn.FilePath : defaultFilePath;
                addressableSymbols.Add(new AddressableSymbolStub(
                    mn.Id,
                    mn.Name,
                    fp,
                    enclosingTypeId,
                    NodeKind.Member,
                    SymbolVisibility.Public,
                    mn.DataType));
                break;
            }
        }

        if (node is ScopeNode or CallLiteralNode or MemberNode)
        {
            if (node is ScopeNode)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    CollectScopeReferencesRecursive(node.Children[i], outboundRefs, visited);
                }
            }
            return;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectProjectSymbolsRecursive(
                node.Children[i],
                currentTypeId,
                defaultFilePath,
                addressableSymbols,
                flowFunctions,
                outboundRefs,
                visited);
        }
    }

    private static void CollectScopeReferencesRecursive(
        IOntologyNode node,
        List<Reference> outboundRefs,
        HashSet<string> visited)
    {
        if (!visited.Add(node.Id)) return;
        if (node.References.Count > 0)
        {
            outboundRefs.AddRange(node.References);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectScopeReferencesRecursive(node.Children[i], outboundRefs, visited);
        }
    }
}
