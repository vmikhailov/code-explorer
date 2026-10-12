using CodeExplorer.Core.Common;
using NUnit.Framework;
using AwesomeAssertions;

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

        ws.Should().NotBeNull();
        ws.RootDirectory.Should().Be(_tempDir);
        Directory.Exists(Path.Combine(_tempDir, ".codeexplorer")).Should().BeTrue();
        Directory.Exists(ws.QueriesDirectory).Should().BeTrue();
        ws.DbPath.Should().Be(Path.Combine(_tempDir, ".codeexplorer", "graph.db"));
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

        found.Should().NotBeNull();
        found!.RootDirectory.Should().Be(_tempDir);
        found.DbPath.Should().Be(Path.Combine(_tempDir, ".codeexplorer", "graph.db"));
    }

    [Test]
    public void Find_WhenNotInWorkspace_ReturnsNull()
    {
        var nonWs = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(nonWs);

        var found = WorkspaceLocator.Find(nonWs);

        found.Should().BeNull();
    }

    [Test]
    public void FindOrThrow_WhenNotInWorkspace_ThrowsDescriptiveException()
    {
        var nonWs = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(nonWs);

        var ex = Assert.Throws<InvalidOperationException>(() => WorkspaceLocator.FindOrThrow(nonWs));

        ex!.Message.Should().Contain("No '.codeexplorer' workspace found");
        ex.Message.Should().Contain("ce init");
    }

    [Test]
    public void FindWithFallbacks_WhenExplicitPathProvided_FindsWorkspace()
    {
        WorkspaceLocator.Initialize(_tempDir, "ExplicitWs");
        var found = WorkspaceLocator.FindWithFallbacks(_tempDir);

        found.Should().NotBeNull();
        found!.RootDirectory.Should().Be(_tempDir);
    }

    [Test]
    public void FindWithFallbacks_WhenExplicitPathInvalid_ReturnsNull()
    {
        var nonWs = Path.Combine(_tempDir, "non_existent_or_empty");
        Directory.CreateDirectory(nonWs);

        var found = WorkspaceLocator.FindWithFallbacks(nonWs);

        found.Should().BeNull("Explicit invalid path should not fall back to CWD");
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

            found.Should().NotBeNull();
            found!.RootDirectory.Should().Be(_tempDir);
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

        found.Should().NotBeNull();
        found!.RootDirectory.Should().Be(_tempDir);
    }
}
