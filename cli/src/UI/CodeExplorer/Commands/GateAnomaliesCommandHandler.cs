using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class GateAnomaliesCommandHandler : BaseCommandHandler
{
    private const string ActionAnomalies = "anomalies";
    private const string DefaultAnomalyType = "all";
    private const string MissingProjectError = "Error: Project name is required for anomaly detection (e.g. 'ce gate anomalies <project>' or 'ce gate check --anomalies all --project <project>').";

    public static async Task<int> HandleOrDelegateAsync(GateOptions opts)
    {
        var isAnomaliesAction = string.Equals(opts.Action, ActionAnomalies, StringComparison.OrdinalIgnoreCase);
        if (!isAnomaliesAction && string.IsNullOrWhiteSpace(opts.Anomalies))
        {
            return await GateCommandHandler.HandleAsync(opts);
        }

        var projectName = opts.ProjectName;
        var workspaceDir = opts.Dir;
        if (string.IsNullOrWhiteSpace(projectName) && isAnomaliesAction && !string.IsNullOrWhiteSpace(opts.PositionalDir))
        {
            projectName = opts.PositionalDir;
        }
        else if (string.IsNullOrWhiteSpace(workspaceDir))
        {
            workspaceDir = opts.PositionalDir;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            PrintError(MissingProjectError);
            return 1;
        }

        var ws = EnsureInitialized(workspaceDir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
        var metricType = string.IsNullOrWhiteSpace(opts.Anomalies) ? DefaultAnomalyType : opts.Anomalies.Trim();
        var output = await repository.FindRefactoringOpportunitiesAsync(projectName, metricType, ws.RootDirectory);
        Console.WriteLine(output);
        return 0;
    }
}
