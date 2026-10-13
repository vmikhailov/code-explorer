namespace CodeExplorer.Tests;

public static class TestRegistryLock
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static async Task<IDisposable> AcquireAsync()
    {
        await _semaphore.WaitAsync();
        return new Releaser(_semaphore);
    }

    public static IDisposable Acquire()
    {
        _semaphore.Wait();
        return new Releaser(_semaphore);
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }
}
