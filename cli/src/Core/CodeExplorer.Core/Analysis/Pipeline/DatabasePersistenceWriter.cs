namespace CodeExplorer.Core.Parser;

public sealed class DatabasePersistenceWriter : IAsyncDisposable
{
    private readonly ParsingContext _ctx;
    private readonly Task _consumerTask;

    public DatabasePersistenceWriter(ParsingContext ctx)
    {
        _ctx = ctx;
        _ctx.PipelineLog.PersistenceLoopStarted();
        _consumerTask = Task.Run(ConsumeChannelAsync);
    }

    private async Task ConsumeChannelAsync()
    {
        await foreach (var writeFunc in _ctx.SharedChannel.Reader.ReadAllAsync())
        {
            try
            {
                await writeFunc();
            }
            catch (Exception ex)
            {
                _ctx.PipelineLog.DatabaseWriteFailed(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _ctx.SharedChannel.Writer.Complete();

        try
        {
            await _consumerTask;
            _ctx.FlushPersistenceProgressReport();
        }
        catch (Exception ex)
        {
            _ctx.PipelineLog.ConsumerTaskFailed(ex);
        }
    }
}
