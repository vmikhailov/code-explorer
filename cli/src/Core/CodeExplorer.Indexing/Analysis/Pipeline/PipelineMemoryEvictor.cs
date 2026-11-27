using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

internal static class PipelineMemoryEvictor
{
    private const string LangTerraform = "terraform";
    private const string KeywordSchema = "schema";

    public static void ReleaseSyntaxTreeBuffersAfterLayer3(SyntaxTree syntaxTree, bool lightweightAstScanMode)
    {
        if (lightweightAstScanMode) return;

        syntaxTree.RawVariables.Clear();
        syntaxTree.RawVariables.TrimExcess();
        syntaxTree.RawTypeBindings.Clear();
        syntaxTree.RawTypeBindings.TrimExcess();
    }

    public static void ReleaseSyntaxTreesAfterLayer4(Layer3Result layer3, bool lightweightAstScanMode)
    {
        if (lightweightAstScanMode) return;

        for (var i = 0; i < layer3.SyntaxTrees.Count; i++)
        {
            layer3.SyntaxTrees[i].Dispose();
        }
        layer3.SyntaxTrees.Clear();
        layer3.SyntaxTrees.TrimExcess();
    }

    public static void EvictCompletedProjectSyntacticBodies(
        ProjectSyntaxNode projectSyntaxNode,
        List<SyntaxTree> projectTrees,
        bool retainTerraformAst,
        bool lightweightAstScanMode)
    {
        if (lightweightAstScanMode) return;

        for (var i = 0; i < projectTrees.Count; i++)
        {
            var st = projectTrees[i];
            if (st.FileNode != null)
            {
                PruneProjectInternalBodies(st.FileNode);
            }

            var isTerraformTree = string.Equals(st.FileParser.LanguageName, LangTerraform, StringComparison.OrdinalIgnoreCase);
            if (!(retainTerraformAst && isTerraformTree))
            {
                st.Dispose();
                st.RawImports.Clear();
                st.RawImports.TrimExcess();
            }
        }

        PruneProjectInternalBodies(projectSyntaxNode);
        projectTrees.Clear();
        projectTrees.TrimExcess();
    }

    private static void PruneProjectInternalBodies(IOntologyNode parent) =>
        PruneProjectInternalBodiesRecursive(parent, new HashSet<IOntologyNode>(ReferenceEqualityComparer.Instance));

    private static void PruneProjectInternalBodiesRecursive(IOntologyNode parent, HashSet<IOntologyNode> visited)
    {
        if (parent.Children.Count == 0 || !visited.Add(parent)) return;

        if (parent is FunctionNode fn)
        {
            parent.Children.RemoveAll(c =>
                c is ScopeNode or MemberNode ||
                (c is CallLiteralNode lit && lit.ArgIndex != 0 && lit.Line > fn.StartLine + 1));
        }
        else if (parent is TypeNode)
        {
            parent.Children.RemoveAll(static c =>
                c is ScopeNode or CallLiteralNode);
        }
        else
        {
            parent.Children.RemoveAll(static c =>
                c is ScopeNode or CallLiteralNode ||
                (c is MemberNode mn && mn.MemberKind is MemberKind.Variable or MemberKind.Parameter));
        }

        for (var i = 0; i < parent.Children.Count; i++)
        {
            PruneProjectInternalBodiesRecursive(parent.Children[i], visited);
        }
    }

    public static void TryReleaseDocumentEarlyInLayer3(SyntaxTree syntaxTree, bool lightweightAstScanMode)
    {
        if (lightweightAstScanMode) return;

        syntaxTree.RootSymbol = null;
        if (syntaxTree.SemanticSymbols.Count == 0 &&
            !string.Equals(syntaxTree.FileParser.LanguageName, LangTerraform, StringComparison.OrdinalIgnoreCase) &&
            syntaxTree.Document?.SourceText.Contains(KeywordSchema, StringComparison.OrdinalIgnoreCase) != true)
        {
            syntaxTree.ReleaseDocument();
        }
    }

    public static void EvictPostResolutionLeafNodes(Layer5Context ctx)
    {
        if (ctx.LightweightAstScanMode) return;

        var syntaxStructure = ctx.SyntaxStructure;
        if (syntaxStructure != null)
        {
            PruneSyntacticLeafChildrenRecursive(syntaxStructure);
        }

        var files = ctx.Files;
        for (var i = 0; i < files.Count; i++)
        {
            PruneSyntacticLeafChildrenRecursive(files[i]);
        }
    }

    private static void PruneSyntacticLeafChildrenRecursive(IOntologyNode parent)
    {
        if (parent.Children.Count == 0) return;

        parent.Children.RemoveAll(static c =>
            c is MemberNode or CallLiteralNode or ScopeNode);

        for (var i = 0; i < parent.Children.Count; i++)
        {
            PruneSyntacticLeafChildrenRecursive(parent.Children[i]);
        }
    }

    public static void EvictNonPostIndexLayersBeforePostIndex(Layer5Context ctx)
    {
        if (ctx.LightweightAstScanMode) return;

        var syntaxStructure = ctx.SyntaxStructure;
        if (syntaxStructure != null)
        {
            CollectFunctionIdsRecursive(syntaxStructure, ctx.KnownFunctionIds);
            syntaxStructure.Children.Clear();
            syntaxStructure.Children.TrimExcess();
        }

        var files = ctx.Files;
        var hoistedSemanticNodes = new List<IOntologyNode>();
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Children.Count == 0) continue;

            hoistedSemanticNodes.Clear();
            for (var c = 0; c < file.Children.Count; c++)
            {
                CollectFunctionIdsAndHoistSemantics(file.Children[c], ctx.KnownFunctionIds, hoistedSemanticNodes);
            }

            file.Children.Clear();
            for (var h = 0; h < hoistedSemanticNodes.Count; h++)
            {
                file.Children.Add(hoistedSemanticNodes[h]);
            }
            file.Children.TrimExcess();
        }

        var workspace = ctx.Workspace;
        if (workspace != null)
        {
            for (var i = 0; i < workspace.Children.Count; i++)
            {
                if (workspace.Children[i] is FilesStructureNode filesStructure)
                {
                    filesStructure.Children.Clear();
                    filesStructure.Children.TrimExcess();
                }
            }
        }

        AnalysisSignalStore.ClearFunctionSignals();
        ConfigurationIndex.Clear();
        ConstantRegistry.Clear();
        RouteDictionaryRegistry.Clear();
    }

    private static void CollectFunctionIdsRecursive(IOntologyNode node, HashSet<string> functionIds)
    {
        if (node is FunctionNode fn)
        {
            functionIds.Add(fn.Id);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectFunctionIdsRecursive(node.Children[i], functionIds);
        }
    }

    private static void CollectFunctionIdsAndHoistSemantics(
        IOntologyNode node,
        HashSet<string> functionIds,
        List<IOntologyNode> retainedSemantics)
    {
        if (node is FunctionNode fn)
        {
            functionIds.Add(fn.Id);
        }

        if (IsPostIndexSemanticNode(node))
        {
            retainedSemantics.Add(node);
            return;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CollectFunctionIdsAndHoistSemantics(node.Children[i], functionIds, retainedSemantics);
        }
    }

    private static bool IsPostIndexSemanticNode(IOntologyNode node) =>
        node is ModuleNode
            or DatabaseNode
            or DbSchemaNode
            or DataSetNode
            or TableNode
            or QueryNode
            or EndpointNode
            or EntryPointNode
            or ExternalServiceNode
            or TopicNode
            or MessageNode
            or CacheNode
            or TransactionNode
            or PolicyNode
            or JobNode
            or CloudNode
            or CloudResourceNode
            or CloudServiceNode
            or ApiInUseNode;
}
