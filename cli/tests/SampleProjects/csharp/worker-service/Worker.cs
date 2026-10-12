namespace WorkerService;
public class QueueWorker : BackgroundService {
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
