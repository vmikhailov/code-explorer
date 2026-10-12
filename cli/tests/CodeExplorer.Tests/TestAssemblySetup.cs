using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[SetUpFixture]
public class TestAssemblySetup
{
    [OneTimeSetUp]
    public void GlobalSetup()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }
}
