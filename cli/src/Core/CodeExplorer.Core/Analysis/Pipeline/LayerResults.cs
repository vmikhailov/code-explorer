using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public record Layer1Result(
    WorkspaceNode Workspace,
    FilesStructureNode FilesStructure,
    List<FileNode> Files,
    List<FolderNode> Folders
);

public record Layer2Result(
    Layer1Result Prev,
    ProjectsStructureNode ProjectsStructure,
    List<ProjectNode> Projects,
    List<PackageNode> Packages,
    List<Relationship> ProjectDependencies
);

public record Layer3Result(
    Layer2Result Prev,
    SyntaxStructureNode SyntaxStructure,
    List<SyntaxTree> SyntaxTrees,
    List<RawImport> RawImports,
    List<RawVariable> RawVariables,
    List<RawTypeBinding> RawTypeBindings,
    List<Reference> GlobalReferences,
    Dictionary<(string Kind, string Name), string> GlobalSymbols
);

public record Layer4Result(
    Layer3Result Prev,
    SemanticStructureNode SemanticStructure,
    List<IOntologyNode> SemanticNodes,
    List<Relationship> SemanticRelationships
);

public record Layer5Result(
    Layer4Result Prev,
    List<Relationship> LateBoundRelationships
);
