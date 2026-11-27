namespace CodeExplorer.Core.Parser;

public sealed class DatabasePersistenceWriter : IAsyncDisposable
{
    private readonly GraphWriteSink _sink;
    private readonly Task _consumerTask;

    public DatabasePersistenceWriter(GraphWriteSink sink)
    {
        _sink = sink;
        _sink.PipelineLog.PersistenceLoopStarted();
        _consumerTask = Task.Run(ConsumeChannelAsync);
    }

    private async Task ConsumeChannelAsync()
    {
        if (_sink.SharedChannel == null) return;

        await foreach (var writeFunc in _sink.SharedChannel.Reader.ReadAllAsync())
        {
            try
            {
                await writeFunc();
            }
            catch (Exception ex)
            {
                _sink.PipelineLog.DatabaseWriteFailed(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _sink.SharedChannel?.Writer.TryComplete();

        try
        {
            await _consumerTask;
            _sink.FlushPersistenceProgressReport();
        }
        catch (Exception ex)
        {
            _sink.PipelineLog.ConsumerTaskFailed(ex);
        }
    }
}
