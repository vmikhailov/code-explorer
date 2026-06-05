using System.Threading.Tasks;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public interface ISemanticAnalyzer
{
    Task AnalyzeAndEnrichAsync(ProjectNode projectNode, ParsingContext ctx);
}
