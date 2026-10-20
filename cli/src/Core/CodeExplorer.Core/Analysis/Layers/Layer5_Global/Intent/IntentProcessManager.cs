using System.Diagnostics;
using System.Text.Json;

namespace CodeExplorer.Core.Analysis;

public record IntentProcessLock(
    int ProcessId,
    DateTimeOffset ProcessStartTime,
    DateTimeOffset LockAcquiredTime,
    string WorkspaceRoot,
    string MachineName
);

/// <summary>
/// Manages running process tracking and mutual exclusion for architectural intent distillation.
/// Ensures only one distillation process runs per workspace and provides reliable process termination.
/// </summary>
public static class IntentProcessManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetLockFilePath(string workspaceRoot)
    {
        var ceDir = Path.Combine(workspaceRoot, ".codeexplorer");
        return Path.Combine(ceDir, "intent.lock");
    }

    /// <summary>
    /// Checks if an active intent distillation process is currently running for the specified workspace.
    /// </summary>
    public static bool TryGetRunningProcess(
        string workspaceRoot,
        out IntentProcessLock? lockInfo,
        out Process? process)
    {
        lockInfo = null;
        process = null;

        var lockPath = GetLockFilePath(workspaceRoot);
        if (!File.Exists(lockPath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(lockPath);
            lockInfo = JsonSerializer.Deserialize<IntentProcessLock>(json);
            if (lockInfo == null)
            {
                TryDeleteFile(lockPath);
                return false;
            }

            // If lock was acquired on a different machine name, we cannot inspect local PID
            if (!string.Equals(lockInfo.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Process candidate;
            try
            {
                candidate = Process.GetProcessById(lockInfo.ProcessId);
            }
            catch (ArgumentException)
            {
                // Process no longer exists
                TryDeleteFile(lockPath);
                lockInfo = null;
                return false;
            }

            if (candidate.HasExited)
            {
                TryDeleteFile(lockPath);
                lockInfo = null;
                return false;
            }

            // Verify process name to guard against PID recycling by an unrelated application
            var procName = candidate.ProcessName.ToLowerInvariant();
            if (!procName.Contains("ce") &&
                !procName.Contains("dotnet") &&
                !procName.Contains("codeexplorer") &&
                !procName.Contains("testhost"))
            {
                TryDeleteFile(lockPath);
                lockInfo = null;
                return false;
            }

            // Verify start time if accessible
            try
            {
                var actualStart = candidate.StartTime.ToUniversalTime();
                var recordedStart = lockInfo.ProcessStartTime.UtcDateTime;
                if (Math.Abs((actualStart - recordedStart).TotalSeconds) > 5)
                {
                    // PID was recycled by another process instance
                    TryDeleteFile(lockPath);
                    lockInfo = null;
                    return false;
                }
            }
            catch
            {
                // Access denied or not supported on this platform, accept PID match
            }

            process = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to acquire an exclusive process lock for intent distillation in the workspace.
    /// </summary>
    public static IDisposable? TryAcquireLock(string workspaceRoot, out string? errorMessage)
    {
        errorMessage = null;

        if (TryGetRunningProcess(workspaceRoot, out var existingLock, out _))
        {
            var elapsed = DateTimeOffset.UtcNow - existingLock!.LockAcquiredTime;
            var elapsedStr = elapsed.TotalMinutes >= 1
                ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
                : $"{elapsed.Seconds}s";

            errorMessage = $"Another intent distillation process is already running for this workspace (PID: {existingLock.ProcessId}, running for {elapsedStr}).";
            return null;
        }

        var lockPath = GetLockFilePath(workspaceRoot);
        var dir = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var currentProcess = Process.GetCurrentProcess();
        DateTimeOffset startTime;
        try
        {
            startTime = currentProcess.StartTime.ToUniversalTime();
        }
        catch
        {
            startTime = DateTimeOffset.UtcNow;
        }

        var lockInfo = new IntentProcessLock(
            ProcessId: currentProcess.Id,
            ProcessStartTime: startTime,
            LockAcquiredTime: DateTimeOffset.UtcNow,
            WorkspaceRoot: workspaceRoot,
            MachineName: Environment.MachineName
        );

        var json = JsonSerializer.Serialize(lockInfo, JsonOptions);
        File.WriteAllText(lockPath, json);

        return new LockReleaser(lockPath);
    }

    /// <summary>
    /// Stops any running intent distillation process for the workspace and deletes the lock file.
    /// </summary>
    public static (bool Stopped, int ProcessId, string Message) StopRunningProcess(string workspaceRoot)
    {
        if (TryGetRunningProcess(workspaceRoot, out var lockInfo, out var proc))
        {
            var pid = lockInfo!.ProcessId;
            try
            {
                proc!.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
                TryDeleteFile(GetLockFilePath(workspaceRoot));
                return (true, pid, $"Successfully stopped intent distillation process (PID: {pid}).");
            }
            catch (Exception ex)
            {
                TryDeleteFile(GetLockFilePath(workspaceRoot));
                return (false, pid, $"Failed to terminate process (PID {pid}): {ex.Message}");
            }
        }

        // Clean up stale lock file if it exists
        var lockPath = GetLockFilePath(workspaceRoot);
        if (File.Exists(lockPath))
        {
            TryDeleteFile(lockPath);
            return (true, 0, "Cleaned up stale intent lock file. No active process was running.");
        }

        return (false, 0, $"No active intent distillation process found for workspace: {workspaceRoot}");
    }

    /// <summary>
    /// Gets the current status of the intent distillation process for the workspace.
    /// </summary>
    public static (bool IsRunning, IntentProcessLock? LockInfo, TimeSpan? Elapsed) GetStatus(string workspaceRoot)
    {
        if (TryGetRunningProcess(workspaceRoot, out var lockInfo, out _))
        {
            var elapsed = DateTimeOffset.UtcNow - lockInfo!.LockAcquiredTime;
            return (true, lockInfo, elapsed);
        }

        return (false, null, null);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore file deletion errors during exit/cleanup
        }
    }

    private sealed class LockReleaser : IDisposable
    {
        private readonly string _lockPath;
        private int _disposed;

        public LockReleaser(string lockPath)
        {
            _lockPath = lockPath;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                TryDeleteFile(_lockPath);
            }
        }
    }
}
