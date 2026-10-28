using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class McpCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(McpOptions opts)
    {
        if (!string.IsNullOrEmpty(opts.Action))
        {
            if (IsOneOf(opts.Action, "configure", "setup"))
            {
                var cfgOpts = new ConfigureOptions
                {
                    Target = opts.Target,
                    Root = opts.Root
                };
                return await ConfigureCommandHandler.HandleAsync(cfgOpts);
            }

            if (IsOneOf(opts.Action, "doctor"))
            {
                var cfgOpts = new ConfigureOptions
                {
                    Verify = true,
                    Root = opts.Root
                };
                return await ConfigureCommandHandler.HandleAsync(cfgOpts);
            }
        }

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

        var isStandby = string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath);
        if (string.IsNullOrEmpty(dbPath))
        {
            dbPath = "Data Source=:memory:;Mode=Memory;Cache=Shared";
        }

        await using var client = new SqliteGraphClient(dbPath);

        var wsRoot = ws?.RootDirectory;
        if (!string.IsNullOrEmpty(wsRoot))
        {
            WorkspaceConventions.LoadFromWorkspace(wsRoot);
        }

        if (opts.Port > 0)
        {
            await CodeExplorerServerHost.RunMcpWebServerAsync(client, opts.Port, wsRoot, isStandby, opts.Quiet);
        }
        else
        {
            await CodeExplorerServerHost.RunMcpStdioHostAsync(client, wsRoot, isStandby, opts.Quiet);
        }

        return 0;
    }
}
