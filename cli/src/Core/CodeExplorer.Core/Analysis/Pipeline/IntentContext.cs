using CodeExplorer.Core.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public sealed class IntentContext
{
    public WorkspaceScope Scope { get; }
    public IGraphClient DbClient { get; }
    public IntentLogger IntentLog { get; }

    public string WorkspaceId
    {
        get => Scope.WorkspaceId;
        set => Scope.WorkspaceId = value;
    }

    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public string HostWorkspacePath => Scope.HostWorkspacePath;

    public bool EnableIntentAnalysis
    {
        get => Scope.EnableIntentAnalysis;
        set => Scope.EnableIntentAnalysis = value;
    }

    public IntentContext(
        WorkspaceScope scope,
        IGraphClient dbClient,
        ILogger? logger = null)
    {
        Scope = scope;
        DbClient = dbClient;
        IntentLog = new IntentLogger(logger ?? NullLogger.Instance);
    }

    public IntentContext(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        IGraphClient dbClient,
        bool enableIntentAnalysis = true,
        string? workspaceId = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
        : this(
            new WorkspaceScope(absoluteWorkspacePath, hostWorkspacePath, cancellationToken: cancellationToken)
            {
                WorkspaceId = workspaceId ?? absoluteWorkspacePath,
                EnableIntentAnalysis = enableIntentAnalysis
            },
            dbClient,
            logger)
    {
    }
}
