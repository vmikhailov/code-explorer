using System.Threading.Tasks;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.SQL;

public class SqlSemanticAnalyzer : ISemanticAnalyzer
{
    public Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        return Task.CompletedTask;
    }
}
