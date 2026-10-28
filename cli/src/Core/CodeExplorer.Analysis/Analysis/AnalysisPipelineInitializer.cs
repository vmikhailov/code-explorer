using System.Runtime.CompilerServices;
using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Analysis;

public static class AnalysisPipelineInitializer
{
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        EnsureRegistered();
    }
#pragma warning restore CA2255

    public static void EnsureRegistered()
    {
        Layer3SyntacticParser.SemanticPostProcessor = (tree, ctx) =>
            EndpointSemanticAnalyzer.Enrich(tree, null, ctx);

        WorkspaceIndexer.PostSyntacticPipelineHook = async (l3, ctx) =>
        {
            var syntaxTrees = l3.SyntaxTrees;
            var indexedFilesCount = l3.Prev.Prev.Files.Count;
            ctx.ReportProgress(52, "Layer 4: Semantic", $"Parsed {syntaxTrees.Count} syntax trees. Analyzing semantics...", syntaxTrees.Count, indexedFilesCount);

            var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);
            ctx.ReportProgress(68, "Layer 5: Global", "Semantic enrichment complete. Resolving global references...", syntaxTrees.Count, indexedFilesCount);

            await new Layer5AnalysisParser().ParseAsync(l4, ctx);
        };

        WorkspaceIndexer.OnIndexCompletedHook = () => ArchitectureViewEngine.InvalidateCache();
    }
}
