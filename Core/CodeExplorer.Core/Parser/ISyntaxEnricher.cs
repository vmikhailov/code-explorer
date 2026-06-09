using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser;

public interface ISyntaxEnricher
{
    Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx);
}
