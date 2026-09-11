using System.Collections.Concurrent;

namespace CodeExplorer.Core.Parser;

public enum IndexingState
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}

public record IndexingStatus
{
    public string State { get; init; } = nameof(IndexingState.Idle);
    public string? Directory { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public double? DurationSeconds { get; init; }
    public int NodesCount { get; init; }
    public int RelationshipsCount { get; init; }
    public Dictionary<string, int> NodesByKind { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public int NodesPersisted { get; init; }
    public int RelationshipsPersisted { get; init; }
}

public class IndexingTaskContext(CancellationTokenSource cts, IndexingStatus status)
{
    public CancellationTokenSource Cts { get; } = cts;
    public IndexingStatus Status { get; set; } = status;
    public Task? RunningTask { get; set; }
}

public class IndexingTaskManager(WorkspaceIndexer indexer)
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, IndexingTaskContext> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastStartedTaskId;

    public IndexingStatus? GetStatus(string? taskId)
    {
        var id = taskId ?? _lastStartedTaskId;
        if (id == null || !_tasks.TryGetValue(id, out var taskContext))
        {
            return null;
        }

        if (taskContext.Status.State == nameof(IndexingState.Running) &&
            taskContext.Cts.IsCancellationRequested &&
            (taskContext.RunningTask == null || taskContext.RunningTask.IsCompleted))
        {
            taskContext.Status = taskContext.Status with
            {
                State = nameof(IndexingState.Cancelled),
                EndTime = DateTime.UtcNow
            };
        }

        return GetTaskStatusSnapshot(taskContext);
    }

    public List<IndexingStatus> GetAllStatuses()
    {
        return _tasks.Values.OrderByDescending(t => t.Status.StartTime).Select(GetTaskStatusSnapshot).ToList();
    }

    private static IndexingStatus GetTaskStatusSnapshot(IndexingTaskContext taskContext)
    {
        var status = taskContext.Status;
        if (status.StartTime.HasValue)
        {
            var end = status.EndTime ?? DateTime.UtcNow;
            var duration = Math.Round((end - status.StartTime.Value).TotalSeconds, 2);
            return status with { DurationSeconds = duration };
        }
        return status;
    }

    public string? StartIndex(string hostWorkspacePath, string containerWorkspacePath, bool clear, out string message)
    {
        lock (_lock)
        {
            if (IsAlreadyRunning(hostWorkspacePath))
            {
                message = $"Indexing is already running for directory: {hostWorkspacePath}";
                return null;
            }

            var taskId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            var status = CreateInitialStatus(hostWorkspacePath);
            var taskContext = new IndexingTaskContext(cts, status);

            _tasks[taskId] = taskContext;
            _lastStartedTaskId = taskId;

            LaunchIndexingTask(taskContext, hostWorkspacePath, containerWorkspacePath, clear, cts.Token);

            message = "Indexing started in the background.";
            return taskId;
        }
    }

    private bool IsAlreadyRunning(string hostWorkspacePath)
    {
        return _tasks.Values.Any(t =>
            t.Status.State == nameof(IndexingState.Running) &&
            string.Equals(t.Status.Directory, hostWorkspacePath, StringComparison.OrdinalIgnoreCase));
    }

    private static IndexingStatus CreateInitialStatus(string hostWorkspacePath) => new()
    {
        State = nameof(IndexingState.Running),
        Directory = hostWorkspacePath,
        StartTime = DateTime.UtcNow,
        NodesByKind = []
    };

    private void LaunchIndexingTask(
        IndexingTaskContext taskContext,
        string hostPath,
        string containerPath,
        bool clear,
        CancellationToken token)
    {
        var progressReporter = CreateProgressReporter(taskContext);
        taskContext.RunningTask = Task.Run(async () =>
        {
            try
            {
                taskContext.Status = await ExecuteIndexingAsync(taskContext, hostPath, containerPath, clear, token, progressReporter);
            }
            finally
            {
                taskContext.Cts.Dispose();
            }
        });
    }

    private static Progress<IndexingProgress> CreateProgressReporter(IndexingTaskContext taskContext)
    {
        return new Progress<IndexingProgress>(p =>
        {
            taskContext.Status = taskContext.Status with
            {
                NodesPersisted = p.NodesPersisted,
                RelationshipsPersisted = p.RelationshipsPersisted,
                NodesCount = p.NodesCount,
                RelationshipsCount = p.RelationshipsCount,
                NodesByKind = p.NodesByKind as Dictionary<string, int> ?? new Dictionary<string, int>(p.NodesByKind)
            };
        });
    }

    private async Task<IndexingStatus> ExecuteIndexingAsync(
        IndexingTaskContext taskContext,
        string hostPath,
        string containerPath,
        bool clear,
        CancellationToken token,
        IProgress<IndexingProgress> progress)
    {
        try
        {
            var (nodes, rels, kinds) = await indexer.IndexAsync(hostPath, containerPath, clear, token, progress);
            return taskContext.Status with
            {
                State = nameof(IndexingState.Completed),
                EndTime = DateTime.UtcNow,
                NodesCount = nodes,
                RelationshipsCount = rels,
                NodesByKind = kinds
            };
        }
        catch (OperationCanceledException)
        {
            return taskContext.Status with
            {
                State = nameof(IndexingState.Cancelled),
                EndTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return taskContext.Status with
            {
                State = nameof(IndexingState.Failed),
                EndTime = DateTime.UtcNow,
                ErrorMessage = ex.ToString()
            };
        }
    }

    public bool StopIndex(string? taskId, out string message)
    {
        lock (_lock)
        {
            var id = taskId ?? _lastStartedTaskId;
            if (id == null || !_tasks.TryGetValue(id, out var taskContext))
            {
                message = id == null ? "No task has been started yet." : $"Task with ID '{id}' not found.";
                return false;
            }

            if (taskContext.Status.State != nameof(IndexingState.Running))
            {
                message = $"Task with ID '{id}' is not currently running (State: {taskContext.Status.State}).";
                return false;
            }

            taskContext.Cts.Cancel();
            message = "Stop request sent.";
            return true;
        }
    }
}
