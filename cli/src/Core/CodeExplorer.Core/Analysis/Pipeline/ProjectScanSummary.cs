using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public sealed class ProjectScanSummary(
    string projectId,
    string projectName,
    IReadOnlySet<string> friendAssemblies,
    IReadOnlyList<AddressableSymbolStub> addressableSymbols,
    IReadOnlyList<FunctionFlowSummary> flowFunctions,
    IReadOnlyList<ModuleInitStub> moduleInits,
    IReadOnlyList<Reference> outboundReferences,
    IReadOnlyList<RawImport> rawImports,
    IReadOnlyList<IOntologyNode> semanticContracts)
{
    public string ProjectId { get; } = projectId;
    public string ProjectName { get; } = projectName;
    public IReadOnlySet<string> FriendAssemblies { get; } = friendAssemblies;
    public IReadOnlyList<AddressableSymbolStub> AddressableSymbols { get; } = addressableSymbols;
    public IReadOnlyList<FunctionFlowSummary> FlowFunctions { get; } = flowFunctions;
    public IReadOnlyList<ModuleInitStub> ModuleInits { get; } = moduleInits;
    public IReadOnlyList<Reference> OutboundReferences { get; } = outboundReferences;
    public IReadOnlyList<RawImport> RawImports { get; } = rawImports;
    public IReadOnlyList<IOntologyNode> SemanticContracts { get; } = semanticContracts;
}
