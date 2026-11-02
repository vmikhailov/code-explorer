using CodeExplorer.Options;
using Microsoft.AspNetCore.Builder;

namespace CodeExplorer.Commands;

public static class ServeCommandHandler
{
    public static async Task<int> HandleAsync(ServeOptions opts)
    {
        WorkspaceInfo? ws = null;
        string? dbPath = null;
        if (!string.IsNullOrWhiteSpace(opts.DbPath))
        {
            dbPath = Path.GetFullPath(opts.DbPath);
            ws = WorkspaceLocator.FindFromDbPath(dbPath);
        }
        else
        {
            ws = WorkspaceLocator.FindWithFallbacks(opts.Root);
            if (ws != null)
            {
                dbPath = ws.DbPath;
                WorkspaceLocator.RecordActiveWorkspace(ws.RootDirectory);
            }
        }

        var wsRoot = ws?.RootDirectory ?? (opts.Root != null ? Path.GetFullPath(opts.Root) : Directory.GetCurrentDirectory());
        WorkspaceConventions.LoadFromWorkspace(wsRoot);

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            var targetDir = ws != null
                ? (Path.GetDirectoryName(ws.DbPath) ?? Path.Combine(wsRoot, ".codeexplorer"))
                : Path.Combine(wsRoot, ".codeexplorer");

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            dbPath = ws?.DbPath ?? Path.Combine(targetDir, "graph.db");
            WorkspaceLocator.RecordActiveWorkspace(wsRoot);
        }

        var client = new SqliteGraphClient(dbPath);
        await CodeExplorerServerHost.RunServeServerAsync(
            opts.Host,
            opts.Port,
            opts.IdleTimeoutSeconds,
            opts.Quiet,
            wsRoot,
            client);

        return 0;
    }

    public static WebApplication CreateWebApplication(ServeOptions opts, string wsRoot, SqliteGraphClient client, string[]? args = null)
        => CodeExplorerServerHost.CreateServeApplication(
            opts.Host,
            opts.Port,
            opts.IdleTimeoutSeconds,
            opts.Quiet,
            wsRoot,
            client,
            args);
}
