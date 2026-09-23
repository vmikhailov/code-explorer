using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.ColdFusion;

public class ColdFusionSyntaxEnricher : ISyntaxEnricher
{
    private readonly SyntaxTree _syntaxTree;

    public ColdFusionSyntaxEnricher(SyntaxTree syntaxTree)
    {
        _syntaxTree = syntaxTree;
    }

    public Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        if (_syntaxTree.FileNode != null)
        {
            RegisterSymbolsRecursive(_syntaxTree.FileNode, projectNode, ctx);
        }
        return Task.CompletedTask;
    }

    private void RegisterSymbolsRecursive(IOntologyNode node, ProjectNode projectNode, ParsingContext ctx)
    {
        if (node is TypeNode typeNode)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Type, typeNode.Name, typeNode.Id);
        }
        else if (node is FunctionNode funcNode)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, funcNode.Name, funcNode.Id);
        }
        else if (node is TableNode table)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, table.Name, table.Id);
        }
        else if (node is EndpointNode endpoint)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Endpoint, endpoint.Name, endpoint.Id);
        }
        else if (node is ExternalServiceNode extService)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.ExternalService, extService.Name, extService.Id);
        }

        foreach (var child in node.Children)
        {
            RegisterSymbolsRecursive(child, projectNode, ctx);
        }
    }
}
