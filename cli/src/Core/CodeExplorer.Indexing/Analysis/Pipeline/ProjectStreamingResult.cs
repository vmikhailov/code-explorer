using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

internal sealed record ProjectStreamingResult(
    SyntaxStructureNode SyntaxStructure,
    Layer3Result Layer3,
    Layer4Result Layer4,
    int ParsedTreeCount,
    long Layer3ElapsedMs,
    long Layer4ElapsedMs,
    ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? SkippedFiles,
    Dictionary<string, string> PrecomputedStructuralHashes);
