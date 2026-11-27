using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public record Layer4Result(
    SemanticStructureNode SemanticStructure,
    List<IOntologyNode> SemanticNodes,
    List<Relationship> SemanticRelationships);
