using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public interface ISemanticModel
{
    Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx);
}
