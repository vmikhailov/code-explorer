using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public sealed class ScanIssueCollector
{
    private readonly ConcurrentDictionary<string, byte> _flushedIds = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, (ScanIssueNode Issue, string? ParentNodeId, bool IsFileParent)> Issues { get; } =
        new(StringComparer.Ordinal);

    public bool IsEmpty => Issues.IsEmpty;

    public (ScanIssueNode Issue, bool Added) Record(
        WorkspaceScope scope,
        string stage,
        string category,
        string? relativeOrFullPath,
        string message,
        string? name = null,
        string severity = "warning",
        int? startLine = null,
        int? endLine = null,
        int? startCol = null,
        int? endCol = null,
        Exception? exception = null,
        string? language = null,
        string? parentNodeId = null,
        bool isFileParent = true)
    {
        var relPath = scope.NormalizeRelativePath(relativeOrFullPath);
        var wsId = string.IsNullOrEmpty(scope.WorkspaceId) ? "ws" : scope.WorkspaceId;
        var linePart = startLine ?? 0;
        var colPart = startCol ?? 0;
        var issueName = !string.IsNullOrWhiteSpace(name) ? name : category;
        var issueId = $"{wsId}:{OntologyConstants.IdPrefixes.ScanIssue}:{relPath}:{stage}:{category}:{linePart}:{colPart}";

        var issue = new ScanIssueNode(
            Id: issueId,
            Name: issueName,
            Path: relPath,
            Stage: stage,
            Category: category,
            Severity: severity,
            Message: message,
            StartLine: startLine,
            EndLine: endLine,
            StartCol: startCol,
            EndCol: endCol,
            ExceptionType: exception?.GetType().Name,
            Language: language);

        var resolvedParentId = parentNodeId;
        if (string.IsNullOrEmpty(resolvedParentId))
        {
            resolvedParentId = isFileParent && !string.IsNullOrEmpty(relPath)
                ? $"{wsId}:{OntologyConstants.IdPrefixes.File}:{relPath}"
                : $"{wsId}:{OntologyConstants.IdPrefixes.FilesStructure}";
        }

        var added = Issues.TryAdd(issueId, (issue, resolvedParentId, isFileParent));
        return (issue, added);
    }

    public (List<Node> Nodes, List<Relationship> Relationships) DrainUnflushed()
    {
        var nodesToUpload = new List<Node>();
        var relsToUpload = new List<Relationship>();

        foreach (var (id, entry) in Issues)
        {
            if (!_flushedIds.TryAdd(id, 0)) continue;

            nodesToUpload.Add(Node.FromNode(entry.Issue));
            if (!string.IsNullOrEmpty(entry.ParentNodeId))
            {
                relsToUpload.Add(new Relationship(entry.ParentNodeId, entry.Issue.Id, OntologyConstants.Relationships.Contains));
                if (entry.IsFileParent)
                {
                    relsToUpload.Add(new Relationship(entry.Issue.Id, entry.ParentNodeId, OntologyConstants.Relationships.DeclaredIn));
                }
            }
        }

        return (nodesToUpload, relsToUpload);
    }
}
