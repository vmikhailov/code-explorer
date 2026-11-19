using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class CallersCommandHandler : BaseCommandHandler
{
    private const string MissingTargetError = "Error: Target name is required (e.g. 'ce callers <target>').";

    public static async Task<int> HandleAsync(CallersOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Target))
        {
            PrintError(MissingTargetError);
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = !string.IsNullOrWhiteSpace(opts.MethodName)
            ? await repository.ResolveCallTargetAsync(opts.Target, opts.MethodName, ws.RootDirectory, includeTests: opts.IncludeTests)
            : await repository.GetCallersAsync(target: opts.Target, includeTests: opts.IncludeTests, workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
