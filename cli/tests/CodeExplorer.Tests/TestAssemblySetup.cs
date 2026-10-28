using NUnit.Framework;

namespace CodeExplorer.Tests;

[SetUpFixture]
public class TestAssemblySetup
{
    private const string MutexName = @"Global\CodeExplorer_TestSuite_Mutex";
    private Mutex? _processMutex;
    private bool _hasHandle;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        _processMutex = new Mutex(false, MutexName);
        try
        {
            _hasHandle = _processMutex.WaitOne(TimeSpan.FromMinutes(10));
        }
        catch (AbandonedMutexException)
        {
            // Previous test runner process terminated abruptly; we now own the mutex.
            _hasHandle = true;
        }

        WorkspaceIndexer.AutoRegisterParsers();
    }

    [OneTimeTearDown]
    public void GlobalTearDown()
    {
        if (_processMutex != null)
        {
            if (_hasHandle)
            {
                try
                {
                    _processMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Ignore if already released
                }
            }
            _processMutex.Dispose();
            _processMutex = null;
        }
    }
}

