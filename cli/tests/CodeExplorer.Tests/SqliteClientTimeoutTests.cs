using CodeExplorer.Core.Database;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SqliteClientTimeoutTests
{
    private string _tempDbPath = null!;
    private SqliteGraphClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_db_timeout_{Guid.NewGuid():N}.db");
        _client = new SqliteGraphClient(_tempDbPath);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Test]
    public void Test_DefaultCommandTimeout_IsConfigured()
    {
        Assert.That(_client.CommandTimeoutSeconds, Is.EqualTo(15));
    }

    [Test]
    public void Test_ExecuteQueryAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            await _client.ExecuteQueryAsync("MATCH (n) RETURN count(n)", null, cts.Token);
        });
    }

    [Test]
    public void Test_ExecuteWriteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
        {
            await _client.ExecuteWriteAsync("CREATE (n:Test)", null, cts.Token);
        });
    }
}
