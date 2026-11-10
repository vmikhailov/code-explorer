using CodeExplorer.Commands;
using CodeExplorer.Options;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class StatusCommandTests
{
    private string _tempWorkspace = null!;
    private TextWriter _originalOut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_status_cmd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
        _originalOut = Console.Out;
    }

    [TearDown]
    public void TearDown()
    {
        Console.SetOut(_originalOut);
        try
        {
            if (Directory.Exists(_tempWorkspace))
            {
                Directory.Delete(_tempWorkspace, true);
            }
        }
        catch { }
    }

    private async Task SetupWorkspaceAsync()
    {
        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "StatusTestApp");
        await using var client = new SqliteGraphClient(ws.DbPath);
    }

    [Test]
    public async Task Status_OutputsSchemaVersion_InYaml()
    {
        await SetupWorkspaceAsync();

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await StatusCommandHandler.HandleAsync(new StatusOptions { Dir = _tempWorkspace });

        exitCode.Should().Be(0);
        var output = sw.ToString();

        var root = ToonYamlSerializer.ParseYamlOrJson(output);

        root.GetIntProp("schema_version").Should().Be(SqliteGraphClient.CurrentSchemaVersion);
        root.GetIntProp("current_schema_version").Should().Be(SqliteGraphClient.CurrentSchemaVersion);
        root.GetBoolProp("is_schema_outdated").Should().BeFalse();
    }

    [Test]
    public async Task Status_OutdatedSchema_ReportsOutdatedFlagInYaml()
    {
        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "StatusTestApp");
        await using (var client = new SqliteGraphClient(ws.DbPath))
        {
        }

        // Drop edges table and set user_version = 2 directly on the SQLite database
        // so that auto-migration from v2 to v4 fails during client initialization.
        using (var conn = new SqliteConnection($"Data Source={ws.DbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TABLE edges; PRAGMA user_version = 2;";
            cmd.ExecuteNonQuery();
        }

        using var sw = new StringWriter();
        Console.SetOut(sw);

        var exitCode = await StatusCommandHandler.HandleAsync(new StatusOptions { Dir = _tempWorkspace });

        exitCode.Should().Be(0);
        var output = sw.ToString();
        var root = ToonYamlSerializer.ParseYamlOrJson(output);

        root.GetIntProp("schema_version").Should().Be(2);
        root.GetBoolProp("is_schema_outdated").Should().BeTrue();
    }
}
