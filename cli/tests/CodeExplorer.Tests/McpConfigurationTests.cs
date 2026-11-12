using System.Text.Json.Nodes;
using CodeExplorer.Commands;
using CodeExplorer.Configuration;
using CodeExplorer.Configuration.Adapters;
using CodeExplorer.Options;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class McpConfigurationTests
{
    [Test]
    public void CursorAdapter_GlobalPath_UsesDotCursorInUserProfileAcrossAllPlatforms()
    {
        var adapter = new CursorAdapter();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, ".cursor", "mcp.json");

        adapter.GetConfigPath(@"C:\workspace", ConfigScope.Global).Should().Be(expected);
    }

    [Test]
    public void VsCodeAdapter_UsesServersSectionKeyAndSupportsGlobalScope()
    {
        var adapter = new VsCodeAdapter();
        adapter.SupportsGlobalScope.Should().BeTrue();
        adapter.GetServersSectionKey().Should().Be("servers");

        var wsConfig = adapter.GenerateMcpConfig(@"C:\workspace", ConfigScope.Workspace);
        wsConfig["type"]!.GetValue<string>().Should().Be("stdio");
        wsConfig["command"]!.GetValue<string>().Should().Be("ce");
        wsConfig["cwd"]!.GetValue<string>().Should().Be("${workspaceFolder}");
    }

    [Test]
    public void McpCommandResolver_RespectsExplicitCommandPathAndWorkspacePortability()
    {
        McpCommandResolver.ResolveCommand(ConfigScope.Global, @"  /opt/ce/ce  ")
            .Should().Be("/opt/ce/ce");

        McpCommandResolver.ResolveCommand(ConfigScope.Workspace)
            .Should().Be("ce");
    }

    [Test]
    public async Task ConfigureCommandHandler_CommandPathOption_WritesCustomExecutablePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-mcp-cfg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var customBinary = Path.Combine(tempDir, "bin", "ce");
            var exitCode = await ConfigureCommandHandler.HandleAsync(new ConfigureOptions
            {
                Category = "mcp",
                Target = "cursor",
                Scope = "workspace",
                NoRules = true,
                Root = tempDir,
                CommandPath = customBinary
            });

            exitCode.Should().Be(0);
            var writtenPath = Path.Combine(tempDir, ".cursor", "mcp.json");
            File.Exists(writtenPath).Should().BeTrue();

            var parsed = JsonNode.Parse(await File.ReadAllTextAsync(writtenPath))!.AsObject();
            var server = parsed["mcpServers"]!["codeexplorer"]!.AsObject();
            server["command"]!.GetValue<string>().Should().Be(customBinary);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
