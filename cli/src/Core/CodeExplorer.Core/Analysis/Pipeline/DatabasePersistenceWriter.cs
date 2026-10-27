namespace CodeExplorer.Core.Parser;

public sealed class DatabasePersistenceWriter : IAsyncDisposable
{
    private readonly ParsingContext _ctx;
    private readonly Task _consumerTask;

    public DatabasePersistenceWriter(ParsingContext ctx)
    {
        _ctx = ctx;
        _ctx.LogDebug("Starting background database persistence loop...");
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
                _ctx.LogError($"[Database   ] Error writing to database: {ex.Message}", ex);
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
            _ctx.LogError($"Consumer task finished with error: {ex.Message}", ex);
        }
    }
}
