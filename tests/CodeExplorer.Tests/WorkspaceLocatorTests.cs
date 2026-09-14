using CodeExplorer.Core.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class WorkspaceLocatorTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_locator_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Test]
    public void Initialize_CreatesCodeExplorerFolderAndQueries()
    {
        var ws = WorkspaceLocator.Initialize(_tempDir, "TestWorkspace");

        Assert.That(ws, Is.Not.Null);
        Assert.That(ws.RootDirectory, Is.EqualTo(_tempDir));
        Assert.That(Directory.Exists(Path.Combine(_tempDir, ".codeexplorer")), Is.True);
        Assert.That(Directory.Exists(ws.QueriesDirectory), Is.True);
        Assert.That(ws.DbPath, Is.EqualTo(Path.Combine(_tempDir, ".codeexplorer", "graph.db")));
    }

    [Test]
    public void Find_WhenInNestedDirectory_FindsParentWorkspace()
    {
        // Setup: _tempDir/.codeexplorer
        WorkspaceLocator.Initialize(_tempDir, "ParentWs");

        // Deeply nested subfolder: _tempDir/services/auth/src
        var nested = Path.Combine(_tempDir, "services", "auth", "src");
        Directory.CreateDirectory(nested);

        var found = WorkspaceLocator.Find(nested);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.RootDirectory, Is.EqualTo(_tempDir));
        Assert.That(found.DbPath, Is.EqualTo(Path.Combine(_tempDir, ".codeexplorer", "graph.db")));
    }

    [Test]
    public void Find_WhenNotInWorkspace_ReturnsNull()
    {
        var nonWs = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(nonWs);

        var found = WorkspaceLocator.Find(nonWs);

        Assert.That(found, Is.Null);
    }

    [Test]
    public void FindOrThrow_WhenNotInWorkspace_ThrowsDescriptiveException()
    {
        var nonWs = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(nonWs);

        var ex = Assert.Throws<InvalidOperationException>(() => WorkspaceLocator.FindOrThrow(nonWs));

        Assert.That(ex!.Message, Does.Contain("No '.codeexplorer' workspace found"));
        Assert.That(ex.Message, Does.Contain("ce init"));
    }

    [Test]
    public void FindWithFallbacks_WhenExplicitPathProvided_FindsWorkspace()
    {
        WorkspaceLocator.Initialize(_tempDir, "ExplicitWs");
        var found = WorkspaceLocator.FindWithFallbacks(_tempDir);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.RootDirectory, Is.EqualTo(_tempDir));
    }

    [Test]
    public void FindWithFallbacks_WhenExplicitPathInvalid_ReturnsNull()
    {
        var nonWs = Path.Combine(_tempDir, "non_existent_or_empty");
        Directory.CreateDirectory(nonWs);

        var found = WorkspaceLocator.FindWithFallbacks(nonWs);

        Assert.That(found, Is.Null, "Explicit invalid path should not fall back to CWD");
    }

    [Test]
    public void FindWithFallbacks_WhenEnvVarSet_FindsWorkspace()
    {
        WorkspaceLocator.Initialize(_tempDir, "EnvWs");
        var prevVal = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("WORKSPACE_ROOT", _tempDir);
            var found = WorkspaceLocator.FindWithFallbacks();

            Assert.That(found, Is.Not.Null);
            Assert.That(found!.RootDirectory, Is.EqualTo(_tempDir));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKSPACE_ROOT", prevVal);
        }
    }

    [Test]
    public void FindFromDbPath_ResolvesParentWorkspace()
    {
        var ws = WorkspaceLocator.Initialize(_tempDir, "DbPathWs");
        var found = WorkspaceLocator.FindFromDbPath(ws.DbPath);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.RootDirectory, Is.EqualTo(_tempDir));
    }
}
