using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser;

internal sealed class Layer4StreamingState(
    SemanticStructureNode semanticStructure,
    Dictionary<string, List<FileNode>> filesByProjectId,
    Dictionary<string, List<SyntaxTree>> terraformTreesByProjectId,
    Dictionary<string, string> workloadByProjectId,
    Dictionary<string, IOntologyNode> workloadNodeByProjectId,
    HashSet<string> semanticStructureChildIds,
    Dictionary<string, HashSet<string>> workloadChildIdsByWorkload,
    List<IOntologyNode> semanticNodes,
    List<Relationship> semanticRelationships,
    HashSet<(string From, string To, string Kind)> existingRelKeys)
{
    public SemanticStructureNode SemanticStructure { get; } = semanticStructure;
    public Dictionary<string, List<FileNode>> FilesByProjectId { get; } = filesByProjectId;
    public Dictionary<string, List<SyntaxTree>> TerraformTreesByProjectId { get; } = terraformTreesByProjectId;
    public Dictionary<string, string> WorkloadByProjectId { get; } = workloadByProjectId;
    public Dictionary<string, IOntologyNode> WorkloadNodeByProjectId { get; } = workloadNodeByProjectId;
    public HashSet<string> SemanticStructureChildIds { get; } = semanticStructureChildIds;
    public Dictionary<string, HashSet<string>> WorkloadChildIdsByWorkload { get; } = workloadChildIdsByWorkload;
    public List<IOntologyNode> SemanticNodes { get; } = semanticNodes;
    public List<Relationship> SemanticRelationships { get; } = semanticRelationships;
    public HashSet<(string From, string To, string Kind)> ExistingRelKeys { get; } = existingRelKeys;
    public Dictionary<string, object> SharedEnrichmentCache { get; } = new(StringComparer.Ordinal);
}
