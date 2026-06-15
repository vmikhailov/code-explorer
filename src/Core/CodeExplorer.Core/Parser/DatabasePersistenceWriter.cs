using System;
using System.Threading.Tasks;

namespace CodeExplorer.Core.Parser;

public sealed class DatabasePersistenceWriter : IAsyncDisposable
{
    private readonly ParsingContext _ctx;
    private readonly Task _consumerTask;

    public DatabasePersistenceWriter(ParsingContext ctx)
    {
        _ctx = ctx;
        _ctx.Log("[WorkspaceIndexer] Starting background database persistence loop...");
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
                _ctx.Log($"[PersistenceConsumer] Error writing to database: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _ctx.SharedChannel.Writer.Complete();

        try
        {
            await _consumerTask;
        }
        catch (Exception ex)
        {
            _ctx.Log($"[WorkspaceIndexer] Consumer task finished with error: {ex.Message}");
        }
    }
}
