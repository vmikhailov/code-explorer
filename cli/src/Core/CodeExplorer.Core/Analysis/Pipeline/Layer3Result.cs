using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Parser.Layers;

public record Layer3Result(
    SyntaxStructureNode SyntaxStructure,
    List<SyntaxTree> SyntaxTrees,
    List<RawImport> RawImports,
    List<RawVariable> RawVariables,
    List<RawTypeBinding> RawTypeBindings,
    List<Reference> GlobalReferences,
    Dictionary<(string Kind, string Name), string> GlobalSymbols,
    ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? DiscoveredContentSkippedFiles = null);
