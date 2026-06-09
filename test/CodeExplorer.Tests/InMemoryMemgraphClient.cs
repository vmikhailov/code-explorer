using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Tests;

public class InMemoryMemgraphClient : IMemgraphClient
{
    public List<Node> UploadedNodes { get; } = [];
    public List<Relationship> UploadedRelationships { get; } = [];
    public int WorkspaceCounter { get; set; } = 1;

    public Task CreateIndicesAsync() => Task.CompletedTask;

    public Task ClearDatabaseAsync()
    {
        lock (UploadedNodes)
        {
            UploadedNodes.Clear();
            UploadedRelationships.Clear();
        }
        return Task.CompletedTask;
    }

    public Task ClearWorkspaceAsync(string workspacePath)
    {
        return Task.CompletedTask;
    }

    public Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath)
    {
        return Task.FromResult(WorkspaceCounter.ToString());
    }

    public Task SaveEmptyWorkspaceNodeAsync(string id, string path)
    {
        return Task.CompletedTask;
    }

    public Task UploadNodesAsync(List<Node> nodes)
    {
        lock (UploadedNodes)
        {
            UploadedNodes.AddRange(nodes);
        }
        return Task.CompletedTask;
    }

    public Task UploadRelationshipsAsync(List<Relationship> rels)
    {
        lock (UploadedRelationships)
        {
            UploadedRelationships.AddRange(rels);
        }
        return Task.CompletedTask;
    }

    public Task<string> ExecuteQueryAsync(string query, object? parameters = null)
    {
        return Task.FromResult("[]");
    }

    public Task ExecuteWriteAsync(string query, object? parameters = null)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
