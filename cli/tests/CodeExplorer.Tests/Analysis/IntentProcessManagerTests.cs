using CodeExplorer.Core.Analysis;
using NUnit.Framework;
using AwesomeAssertions;

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
        lockPath.Should().Be(Path.Combine(_tempWorkspace, ".codeexplorer", "intent.lock"));
    }

    [Test]
    public void AcquireLock_ShouldCreateLockFile_AndReleaseOnDispose()
    {
        var lockPath = IntentProcessManager.GetLockFilePath(_tempWorkspace);
        File.Exists(lockPath).Should().BeFalse();

        using (var handle = IntentProcessManager.TryAcquireLock(_tempWorkspace, out var error))
        {
            handle.Should().NotBeNull();
            error.Should().BeNull();
            File.Exists(lockPath).Should().BeTrue();

            var (isRunning, lockInfo, elapsed) = IntentProcessManager.GetStatus(_tempWorkspace);
            isRunning.Should().BeTrue();
            lockInfo.Should().NotBeNull();
            lockInfo!.ProcessId.Should().Be(Environment.ProcessId);
            elapsed.Should().NotBeNull();
        }

        // After dispose, lock file must be cleaned up
        File.Exists(lockPath).Should().BeFalse();
        var (afterRunning, _, _) = IntentProcessManager.GetStatus(_tempWorkspace);
        afterRunning.Should().BeFalse();
    }

    [Test]
    public void AcquireLock_WhenAlreadyAcquiredByCurrentProcess_ShouldFailWithErrorMessage()
    {
        using var firstHandle = IntentProcessManager.TryAcquireLock(_tempWorkspace, out var firstError);

        firstHandle.Should().NotBeNull();
        firstError.Should().BeNull();

        using var secondHandle = IntentProcessManager.TryAcquireLock(_tempWorkspace, out var secondError);

        secondHandle.Should().BeNull();
        secondError.Should().NotBeNull();
        secondError.Should().Contain(Environment.ProcessId.ToString());
    }

    [Test]
    public void StopRunningProcess_WhenNoProcessOrStaleLock_ShouldCleanUpGracefully()
    {
        var lockPath = IntentProcessManager.GetLockFilePath(_tempWorkspace);

        // Case 1: no file exists
        var (stopped1, pid1, _) = IntentProcessManager.StopRunningProcess(_tempWorkspace);
        stopped1.Should().BeFalse();
        pid1.Should().Be(0);

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

        File.Exists(lockPath).Should().BeTrue();
        var (stopped2, _, _) = IntentProcessManager.StopRunningProcess(_tempWorkspace);
        stopped2.Should().BeTrue(); // Cleans up stale lock
        File.Exists(lockPath).Should().BeFalse();
    }
}
