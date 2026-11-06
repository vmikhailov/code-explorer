using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;

namespace CodeExplorer.Core.Common.Nodes;

public sealed class WorkspaceAnalysisSignals
{
    public bool HasNativeNode { get; set; }
    public bool HasNativeAddon { get; set; }
    public bool HasDotNetDll { get; set; }
    public string? VsCodeFolderOpenTasksJson { get; set; }
    public string? NuspecId { get; set; }
    public string? NuspecVersion { get; set; }
    public string? ComposeParticipants { get; set; }
}

public sealed class FileAnalysisSignals
{
    public bool HasEntityOrTable { get; set; }
    public string? DbContextBaseConn { get; set; }
    public string? DbContextClasses { get; set; }
    public string? DeclaredSchemaRaw { get; set; }
    public string? VerifiedDbSignals { get; set; }
    public string? DbDriverEngine { get; set; }
    public string? DbDriverFamily { get; set; }
    public string? RustTopologySummary { get; set; }
    public string? RustTopologyBodySummary { get; set; }
    public string? ComposeParticipants { get; set; }
    public ConcurrentDictionary<string, int>? SchemaLines { get; set; }

    public void SetSchemaLine(string schema, int line)
    {
        if (string.IsNullOrEmpty(schema) || line <= 0) return;
        SchemaLines ??= new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        SchemaLines[schema] = line;
    }

    public bool TryGetSchemaLine(string schema, out int line)
    {
        if (SchemaLines != null && SchemaLines.TryGetValue(schema, out line))
        {
            return true;
        }
        line = 0;
        return false;
    }
}

public sealed class ProjectAnalysisSignals
{
    public string? DbDriverEngine { get; set; }
    public string? DbDriverFamily { get; set; }
    public bool DbDriverChecked { get; set; }
    public string? GitBranch { get; set; }
    public string? GitOrigin { get; set; }
    public string? GitUser { get; set; }
    public string? GitCommit { get; set; }
    public string? GitRepo { get; set; }
    public string? SourceReachability { get; set; }
    public string? ParentProjectId { get; set; }
    public string? CargoManifestDir { get; set; }
    public Dictionary<string, string>? ManifestHints { get; set; }
}

public sealed class FunctionAnalysisSignals
{
    public string? EnclosingTypeName { get; set; }
    public string? DataflowReturn { get; set; }
    public string? DataflowWrites { get; set; }
    public string? DataflowSummary { get; set; }
    public string? GraphqlField { get; set; }
    public string? GraphqlSchema { get; set; }
    public string? GraphqlAliases { get; set; }
    public string? MessagingDirection { get; set; }
}

/// <summary>
/// Transient in-memory store for cross-layer analysis signals extracted during indexing.
/// Keeps domain ontology nodes (<see cref="FileNode"/>, <see cref="ProjectNode"/>, <see cref="FunctionNode"/>,
/// <see cref="WorkspaceNode"/>) free of pipeline scratchpad fields and cross-layer leaks.
/// </summary>
public static class AnalysisSignalStore
{
    private static ConditionalWeakTable<WorkspaceNode, WorkspaceAnalysisSignals> _workspaceTable = new();
    private static ConditionalWeakTable<FileNode, FileAnalysisSignals> _fileTable = new();
    private static ConditionalWeakTable<ProjectNode, ProjectAnalysisSignals> _projectTable = new();
    private static ConditionalWeakTable<FunctionNode, FunctionAnalysisSignals> _functionTable = new();

    private static readonly ConcurrentDictionary<string, WorkspaceAnalysisSignals> _workspaceById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, FileAnalysisSignals> _fileById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, ProjectAnalysisSignals> _projectById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, FunctionAnalysisSignals> _functionById = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear()
    {
        _workspaceTable = new ConditionalWeakTable<WorkspaceNode, WorkspaceAnalysisSignals>();
        _fileTable = new ConditionalWeakTable<FileNode, FileAnalysisSignals>();
        _projectTable = new ConditionalWeakTable<ProjectNode, ProjectAnalysisSignals>();
        _functionTable = new ConditionalWeakTable<FunctionNode, FunctionAnalysisSignals>();

        _workspaceById.Clear();
        _fileById.Clear();
        _projectById.Clear();
        _functionById.Clear();
    }

    public static WorkspaceAnalysisSignals Signals(this WorkspaceNode node)
    {
        if (_workspaceTable.TryGetValue(node, out var existing)) return existing;
        var created = !string.IsNullOrEmpty(node.Id)
            ? _workspaceById.GetOrAdd(node.Id, static _ => new WorkspaceAnalysisSignals())
            : new WorkspaceAnalysisSignals();
        _workspaceTable.AddOrUpdate(node, created);
        return created;
    }

    public static WorkspaceAnalysisSignals? TryGetSignals(this WorkspaceNode? node)
    {
        if (node == null) return null;
        if (_workspaceTable.TryGetValue(node, out var existing)) return existing;
        if (!string.IsNullOrEmpty(node.Id) && _workspaceById.TryGetValue(node.Id, out var byId)) return byId;
        return null;
    }

    public static FileAnalysisSignals Signals(this FileNode node)
    {
        if (_fileTable.TryGetValue(node, out var existing)) return existing;
        var key = !string.IsNullOrEmpty(node.Id) ? node.Id : node.FullPath;
        var created = !string.IsNullOrEmpty(key)
            ? _fileById.GetOrAdd(key, static _ => new FileAnalysisSignals())
            : new FileAnalysisSignals();
        _fileTable.AddOrUpdate(node, created);
        return created;
    }

    public static FileAnalysisSignals? TryGetSignals(this FileNode? node)
    {
        if (node == null) return null;
        if (_fileTable.TryGetValue(node, out var existing)) return existing;
        var key = !string.IsNullOrEmpty(node.Id) ? node.Id : node.FullPath;
        if (!string.IsNullOrEmpty(key) && _fileById.TryGetValue(key, out var byId)) return byId;
        return null;
    }

    public static ProjectAnalysisSignals Signals(this ProjectNode node)
    {
        if (_projectTable.TryGetValue(node, out var existing)) return existing;
        var created = !string.IsNullOrEmpty(node.Id)
            ? _projectById.GetOrAdd(node.Id, static _ => new ProjectAnalysisSignals())
            : new ProjectAnalysisSignals();
        _projectTable.AddOrUpdate(node, created);
        return created;
    }

    public static ProjectAnalysisSignals? TryGetSignals(this ProjectNode? node)
    {
        if (node == null) return null;
        if (_projectTable.TryGetValue(node, out var existing)) return existing;
        if (!string.IsNullOrEmpty(node.Id) && _projectById.TryGetValue(node.Id, out var byId)) return byId;
        return null;
    }

    public static FunctionAnalysisSignals Signals(this FunctionNode node)
    {
        if (_functionTable.TryGetValue(node, out var existing)) return existing;
        var created = !string.IsNullOrEmpty(node.Id)
            ? _functionById.GetOrAdd(node.Id, static _ => new FunctionAnalysisSignals())
            : new FunctionAnalysisSignals();
        _functionTable.AddOrUpdate(node, created);
        return created;
    }

    public static FunctionAnalysisSignals? TryGetSignals(this FunctionNode? node)
    {
        if (node == null) return null;
        if (_functionTable.TryGetValue(node, out var existing)) return existing;
        if (!string.IsNullOrEmpty(node.Id) && _functionById.TryGetValue(node.Id, out var byId)) return byId;
        return null;
    }
}
