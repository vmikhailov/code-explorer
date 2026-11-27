using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

public record Layer1Result(
    WorkspaceNode Workspace,
    FilesStructureNode FilesStructure,
    List<FileNode> Files,
    List<FolderNode> Folders,
    ConcurrentDictionary<string, (string FullPath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes)>? DiscoveredManifestFiles = null,
    ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)>? DiscoveredManifestPaths = null,
    List<(string Label, string Command)>? DiscoveredVsCodeFolderOpenTasks = null);
