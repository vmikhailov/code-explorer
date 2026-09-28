using CodeExplorer.Core.Analysis;
using NUnit.Framework;

namespace CodeExplorer.Tests.Analysis;

[TestFixture]
public class IntentProcessManagerTests
{
    private string _tempWorkspace = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_test_ws_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempWorkspace, ".codeexplorer"));
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempWorkspace))
            {
                Directory.Delete(_tempWorkspace, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    public void LockFilePath_ShouldBeInCodeExplorerDirectory()
    {
        var lockPath = IntentProcessManager.GetLockFilePath(_tempWorkspace);
        Assert.That(lockPath, Is.EqualTo(Path.Combine(_tempWorkspace, ".codeexplorer", "intent.lock")));
    }

    [Test]
    public void AcquireLock_ShouldCreateLockFile_AndReleaseOnDispose()
    {
        var lockPath = IntentProcessManager.GetLockFilePath(_tempWorkspace);
        Assert.That(File.Exists(lockPath), Is.False);

        using (var handle = IntentProcessManager.TryAcquireLock(_tempWorkspace, out var error))
        {
            Assert.That(handle, Is.Not.Null);
            Assert.That(error, Is.Null);
            Assert.That(File.Exists(lockPath), Is.True);

            var (isRunning, lockInfo, elapsed) = IntentProcessManager.GetStatus(_tempWorkspace);
            Assert.That(isRunning, Is.True);
            Assert.That(lockInfo, Is.Not.Null);
            Assert.That(lockInfo!.ProcessId, Is.EqualTo(Environment.ProcessId));
            Assert.That(elapsed, Is.Not.Null);
        }

        // After dispose, lock file must be cleaned up
        Assert.That(File.Exists(lockPath), Is.False);
        var (afterRunning, _, _) = IntentProcessManager.GetStatus(_tempWorkspace);
        Assert.That(afterRunning, Is.False);
    }

    [Test]
    public void AcquireLock_WhenAlreadyAcquiredByCurrentProcess_ShouldFailWithErrorMessage()
    {
        using var firstHandle = IntentProcessManager.TryAcquireLock(_tempWorkspace, out var firstError);
        Assert.That(firstHandle, Is.Not.Null);
        Assert.That(firstError, Is.Null);

        using var secondHandle = IntentProcessManager.TryAcquireLock(_tempWorkspace, out var secondError);
        Assert.That(secondHandle, Is.Null);
        Assert.That(secondError, Is.Not.Null);
        Assert.That(secondError, Does.Contain(Environment.ProcessId.ToString()));
    }

    [Test]
    public void StopRunningProcess_WhenNoProcessOrStaleLock_ShouldCleanUpGracefully()
    {
        var lockPath = IntentProcessManager.GetLockFilePath(_tempWorkspace);

        // Case 1: no file exists
        var (stopped1, pid1, _) = IntentProcessManager.StopRunningProcess(_tempWorkspace);
        Assert.That(stopped1, Is.False);
        Assert.That(pid1, Is.EqualTo(0));

        // Case 2: stale lock file with dead PID
        File.WriteAllText(lockPath, """
        {
            "ProcessId": 99999999,
            "ProcessStartTime": "2026-01-01T00:00:00Z",
            "LockAcquiredTime": "2026-01-01T00:00:00Z",
            "WorkspaceRoot": "C:\\test",
            "MachineName": "LOCAL"
        }
        """);

        Assert.That(File.Exists(lockPath), Is.True);
        var (stopped2, _, _) = IntentProcessManager.StopRunningProcess(_tempWorkspace);
        Assert.That(stopped2, Is.True); // Cleans up stale lock
        Assert.That(File.Exists(lockPath), Is.False);
    }
}
