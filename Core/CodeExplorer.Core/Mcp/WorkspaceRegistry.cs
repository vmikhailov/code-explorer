using System.Collections.Concurrent;

namespace CodeExplorer.Core.Mcp;

public class WorkspaceRegistry
{
    private readonly ConcurrentDictionary<string, string> _sessionWorkspaces = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterSession(string sessionId, string workspacePath)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(workspacePath)) return;
        _sessionWorkspaces[sessionId] = workspacePath;
    }

    public string? GetWorkspacePath(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return null;
        return _sessionWorkspaces.GetValueOrDefault(sessionId);
    }

    public void RemoveSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        _sessionWorkspaces.TryRemove(sessionId, out _);
    }

    public string? GetSessionIdByPath(string? workspacePath)
    {
        if (_sessionWorkspaces.IsEmpty) return null;

        // Try exact match first
        var normalized = workspacePath?.Replace('\\', '/').TrimEnd('/');
        if (!string.IsNullOrEmpty(normalized))
        {
            foreach (var kvp in _sessionWorkspaces)
            {
                var valNormalized = kvp.Value.Replace('\\', '/').TrimEnd('/');
                if (string.Equals(valNormalized, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }
        }

        // Fallback: if there's only 1 session, use it
        if (_sessionWorkspaces.Count == 1)
        {
            return _sessionWorkspaces.Keys.First();
        }

        return null;
    }
}
