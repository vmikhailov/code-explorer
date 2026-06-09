using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Database;

public interface IMemgraphClient : IAsyncDisposable
{
    Task CreateIndicesAsync();
    Task ClearDatabaseAsync();
    Task ClearWorkspaceAsync(string workspacePath);
    Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath);
    Task SaveEmptyWorkspaceNodeAsync(string id, string path);
    Task UploadNodesAsync(List<Node> nodes);
    Task UploadRelationshipsAsync(List<Relationship> rels);
    Task<string> ExecuteQueryAsync(string query, object? parameters = null);
    Task ExecuteWriteAsync(string query, object? parameters = null);
}
