using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeExplorer.Core.Mcp;

public interface ICodeExplorerRepository
{
    Task<string> GetArchitectureMapAsync(string? projectName, string? workspacePath);
    Task<string> GetProjectDependenciesAsync(string? projectFilter, string? workspacePath);
    Task<string> GetFileOutlineAsync(string filePath, string? workspacePath);
    Task<string> FindSymbolAsync(string name, string? symbolType, string? workspacePath);
    Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth, string? workspacePath);
    Task<string> ResolveCallTargetAsync(string interfaceName, string methodName, string? workspacePath);
    Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath);
    Task<string> InspectDataLineageAsync(string tableName, string? workspacePath);
    Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath);
    Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType, string? workspacePath);
    Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath);
    Task<string> GetWorkspaceContentAsync(string? workspacePath, string? type);
    Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null);
    Task<string> GetTaxonomyAsync(string? workspacePath);
    Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath);
    string GetNodeDefinition(string kind);
}
