using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser;

public interface ISyntaxEnricher
{
    Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx);
}
