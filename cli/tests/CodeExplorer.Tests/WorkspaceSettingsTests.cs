using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class WorkspaceSettingsTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ce_settings_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Test]
    public void Initialize_CreatesDefaultSettingsJson()
    {
        WorkspaceLocator.Initialize(_tempDir);

        var settingsPath = WorkspaceSettings.GetSettingsPath(_tempDir);
        Assert.That(File.Exists(settingsPath), Is.True);

        var settings = WorkspaceSettings.Load(_tempDir);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.Exclude, Is.Empty);
    }

    [Test]
    public void AddExcludePattern_NormalizesAndDeduplicatesPatterns()
    {
        WorkspaceLocator.Initialize(_tempDir);

        Assert.That(WorkspaceSettings.AddExcludePattern(_tempDir, @"cli\tests\SampleProjects\"), Is.True);
        Assert.That(WorkspaceSettings.AddExcludePattern(_tempDir, "cli/tests/SampleProjects/"), Is.False);
        Assert.That(WorkspaceSettings.AddExcludePattern(_tempDir, "cli/tests/SampleProjects"), Is.False);
        Assert.That(WorkspaceSettings.AddExcludePattern(_tempDir, "cli/tests/CodeExplorer.Tests/TestData/"), Is.True);

        var settings = WorkspaceSettings.Load(_tempDir);
        Assert.That(settings.Exclude, Has.Count.EqualTo(2));
        Assert.That(settings.Exclude, Does.Contain("cli/tests/SampleProjects/"));
        Assert.That(settings.Exclude, Does.Contain("cli/tests/CodeExplorer.Tests/TestData/"));
    }

    [Test]
    public void GitIgnoreMatcher_RespectsWorkspaceSettingsExclusions()
    {
        WorkspaceLocator.Initialize(_tempDir);
        WorkspaceSettings.AddExcludePattern(_tempDir, "tests/SampleProjects/");
        WorkspaceSettings.AddExcludePattern(_tempDir, "**/fixtures/**");

        var matcher = new GitIgnoreMatcher(_tempDir);

        Assert.That(matcher.IsIgnored("tests/SampleProjects", isDirectory: true), Is.True);
        Assert.That(matcher.IsIgnored("tests/SampleProjects/App/Program.cs", isDirectory: false), Is.True);
        Assert.That(matcher.IsIgnored("src/fixtures/mock.ts", isDirectory: false), Is.True);
        Assert.That(matcher.IsIgnored("fixtures/mock.ts", isDirectory: false), Is.True);
        Assert.That(matcher.IsIgnored("tests/UnitTests/ParserTests.cs", isDirectory: false), Is.False);
        Assert.That(matcher.IsIgnored("src/Core/Engine.cs", isDirectory: false), Is.False);
    }
}
