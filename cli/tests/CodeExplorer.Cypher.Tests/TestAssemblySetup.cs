using NUnit.Framework;

[assembly: NonParallelizable]
[assembly: LevelOfParallelism(1)]

namespace CodeExplorer.Cypher.Tests;

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
            _hasHandle = true;
        }
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
                }
            }
            _processMutex.Dispose();
            _processMutex = null;
        }
    }
}
