using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.SQL;

public class SqlSyntaxEnricher : ISyntaxEnricher
{
    private readonly SyntaxTree _syntaxTree;

    public SqlSyntaxEnricher(SyntaxTree syntaxTree)
    {
        _syntaxTree = syntaxTree;
    }

    public Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        if (_syntaxTree.FileNode != null)
        {
            RegisterSymbolsRecursive(_syntaxTree.FileNode, ctx);
        }
        return Task.CompletedTask;
    }

    private void RegisterSymbolsRecursive(IOntologyNode node, ParsingContext ctx)
    {
        if (node is TableNode table)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, table.Name, table.Id);
            
            FindParentDataSet(table, out var schemaName);
            if (!string.Equals(schemaName, "dbo", StringComparison.OrdinalIgnoreCase))
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, $"{schemaName}.{table.Name}", table.Id);
            }
        }
        else if (node is ProcedureNode proc)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, proc.Name, proc.Id);
            
            FindParentDataSet(proc, out var schemaName);
            if (!string.Equals(schemaName, "dbo", StringComparison.OrdinalIgnoreCase))
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Procedure, $"{schemaName}.{proc.Name}", proc.Id);
            }
        }

        foreach (var child in node.Children)
        {
            RegisterSymbolsRecursive(child, ctx);
        }
    }

    private void FindParentDataSet(IOntologyNode child, out string schemaName)
    {
        schemaName = "dbo";
        var parts = child.Id.Split(':');
        var datasetIdx = Array.IndexOf(parts, "dataset");
        if (datasetIdx != -1 && datasetIdx + 1 < parts.Length)
        {
            schemaName = parts[datasetIdx + 1];
        }
    }
}
