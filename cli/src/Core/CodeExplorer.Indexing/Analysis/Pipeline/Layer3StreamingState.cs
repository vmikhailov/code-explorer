using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Parser;

internal sealed class Layer3StreamingState(
    SyntaxStructureNode syntaxStructure,
    Dictionary<string, IOntologyNode> parentByChildId,
    Dictionary<string, List<FileNode>> filesByProjectId,
    Dictionary<string, ProjectNode> primaryOwnerByFilePath,
    Dictionary<string, FileNode> fileNodeByPath,
    List<FileNode> workspaceRootFiles,
    List<Relationship> projectRelationships,
    ConcurrentDictionary<string, byte> prunedFileIds)
{
    public SyntaxStructureNode SyntaxStructure { get; } = syntaxStructure;
    public Dictionary<string, IOntologyNode> ParentByChildId { get; } = parentByChildId;
    public Dictionary<string, List<FileNode>> FilesByProjectId { get; } = filesByProjectId;
    public Dictionary<string, ProjectNode> PrimaryOwnerByFilePath { get; } = primaryOwnerByFilePath;
    public Dictionary<string, FileNode> FileNodeByPath { get; } = fileNodeByPath;
    public List<FileNode> WorkspaceRootFiles { get; } = workspaceRootFiles;
    public List<Relationship> ProjectRelationships { get; } = projectRelationships;
    public ConcurrentDictionary<string, byte> PrunedFileIds { get; } = prunedFileIds;
}
