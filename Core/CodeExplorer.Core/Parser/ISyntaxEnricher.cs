using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public interface ISyntaxEnricher
{
    Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx);
}
