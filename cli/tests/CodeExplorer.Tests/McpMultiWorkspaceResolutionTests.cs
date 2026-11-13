using CodeExplorer.Configuration;
using CodeExplorer.Configuration.Adapters;
using CodeExplorer.Core.Common;
using CodeExplorer.Cypher.Common;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class McpMultiWorkspaceResolutionTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"ce_multi_ws_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }
    }

    [Test]
    public void WorkspaceLocator_FindExplicitOnly_IgnoresCwdAndUnexpandedPlaceholders()
    {
        var cwdWs = CreateIndexedWorkspaceDirectory("cwd-ws");
        var explicitWs = CreateIndexedWorkspaceDirectory("explicit-ws");

        var prevCwd = Directory.GetCurrentDirectory();
        var prevEnv = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        try
        {
            Directory.SetCurrentDirectory(cwdWs);
            Environment.SetEnvironmentVariable("WORKSPACE_ROOT", null);

            // Without explicit path or env var, FindExplicitOnly must NOT fall back to CWD or last_workspace.txt
            WorkspaceLocator.FindExplicitOnly().Should().BeNull();

            // Unexpanded IDE placeholder '${workspaceFolder}' must also be ignored
            Environment.SetEnvironmentVariable("WORKSPACE_ROOT", "${workspaceFolder}");
            WorkspaceLocator.FindExplicitOnly().Should().BeNull();

            // Real WORKSPACE_ROOT env var resolves cleanly
            Environment.SetEnvironmentVariable("WORKSPACE_ROOT", explicitWs);
            var fromEnv = WorkspaceLocator.FindExplicitOnly();
            fromEnv.Should().NotBeNull();
            Path.GetFullPath(fromEnv!.RootDirectory).Should().Be(Path.GetFullPath(explicitWs));

            // Explicit argument takes precedence over WORKSPACE_ROOT
            var fromArg = WorkspaceLocator.FindExplicitOnly(cwdWs);
            fromArg.Should().NotBeNull();
            Path.GetFullPath(fromArg!.RootDirectory).Should().Be(Path.GetFullPath(cwdWs));
        }
        finally
        {
            Directory.SetCurrentDirectory(prevCwd);
            Environment.SetEnvironmentVariable("WORKSPACE_ROOT", prevEnv);
        }
    }

    [Test]
    public void GlobalMcpAdapters_OmitHardcodedWorkspaceRootAndCwd()
    {
        var sampleWorkspace = Path.Combine(_tempRoot, "sample-project");

        var antigravity = new AntigravityAdapter();
        var agGlobal = antigravity.GenerateMcpConfig(sampleWorkspace, ConfigScope.Global);
        agGlobal.ContainsKey("cwd").Should().BeFalse();
        agGlobal.ContainsKey("env").Should().BeFalse();

        var agWorkspace = antigravity.GenerateMcpConfig(sampleWorkspace, ConfigScope.Workspace);
        agWorkspace.ContainsKey("cwd").Should().BeFalse();
        agWorkspace["env"]!["WORKSPACE_ROOT"]!.GetValue<string>().Should().Be("${workspaceFolder}");

        var cursor = new CursorAdapter();
        var cursorGlobal = cursor.GenerateMcpConfig(sampleWorkspace, ConfigScope.Global);
        cursorGlobal.ContainsKey("cwd").Should().BeFalse();
        cursorGlobal.ContainsKey("env").Should().BeFalse();

        var claudeDesktop = new ClaudeDesktopAdapter();
        var claudeGlobal = claudeDesktop.GenerateMcpConfig(sampleWorkspace, ConfigScope.Global);
        claudeGlobal.ContainsKey("cwd").Should().BeFalse();
        claudeGlobal.ContainsKey("env").Should().BeFalse();
        claudeGlobal["args"]!.ToJsonString().Should().NotContain("--root");
    }

    [Test]
    public async Task McpGraphHandler_StandbyMode_ResolvesClientRootAndSwitchesWorkspaces()
    {
        var wsAlpha = await CreateSeededWorkspaceAsync("ws-alpha", "AlphaController");
        var wsBeta = await CreateSeededWorkspaceAsync("ws-beta", "BetaService");
        var unindexedDir = Path.Combine(_tempRoot, "unindexed-ws");
        Directory.CreateDirectory(unindexedDir);

        var standbyDbPath = Path.Combine(_tempRoot, "standby.db");
        await using var standbyClient = new SqliteGraphClient(standbyDbPath);

        var repo = new CodeExplorerRepository(standbyClient, defaultWorkspacePath: null);
        var handler = new McpGraphHandler(repo, new HttpContextAccessor());

        // 1. Pure Standby Mode (no client roots, no workspacePath) -> returns standby message
        handler.CustomRootsProvider = _ => Task.FromResult<IReadOnlyList<string>>([]);
        var standbyRes = await handler.FindSymbolAsync("AlphaController");
        standbyRes.IsError.Should().NotBeTrue();
        ExtractText(standbyRes).Should().Contain("status: standby");

        // 2. Single client root with URL-encoded Windows drive colon (e.g. file:///c%3A/...) -> auto-resolves Alpha
        var encodedAlphaUri = EncodeColonInFileUri(new Uri(wsAlpha).AbsoluteUri);
        handler.CustomRootsProvider = _ => Task.FromResult<IReadOnlyList<string>>([encodedAlphaUri]);

        var alphaFromRoot = await handler.FindSymbolAsync("AlphaController");
        alphaFromRoot.IsError.Should().NotBeTrue();
        ExtractText(alphaFromRoot).Should().Contain("AlphaController");

        // 3. Explicit workspacePath overrides client root on the same global handler -> switches to Beta
        var betaExplicit = await handler.FindSymbolAsync("BetaService", workspacePath: wsBeta);
        betaExplicit.IsError.Should().NotBeTrue();
        ExtractText(betaExplicit).Should().Contain("BetaService");

        // Ensure Beta symbols do not leak into Alpha when workspacePath is omitted
        var betaOnAlphaRoot = await handler.FindSymbolAsync("BetaService");
        ExtractText(betaOnAlphaRoot).Should().NotContain("BetaService");

        // 4. Multiple indexed client roots without explicit workspacePath -> clear disambiguation error
        handler.CustomRootsProvider = _ => Task.FromResult<IReadOnlyList<string>>(
            [new Uri(wsAlpha).AbsoluteUri, new Uri(wsBeta).AbsoluteUri]);
        var ambiguousRes = await handler.FindSymbolAsync("AlphaController");
        ambiguousRes.IsError.Should().BeTrue();
        ExtractText(ambiguousRes).Should().Contain("Multiple indexed CodeExplorer workspaces");

        // Passing explicit workspacePath disambiguates cleanly even when multiple roots are open
        var disambiguatedRes = await handler.FindSymbolAsync("AlphaController", workspacePath: wsAlpha);
        disambiguatedRes.IsError.Should().NotBeTrue();
        ExtractText(disambiguatedRes).Should().Contain("AlphaController");

        // 5. Explicit unindexed workspacePath -> clear error rather than silently returning another workspace's data
        var unindexedRes = await handler.FindSymbolAsync("AlphaController", workspacePath: unindexedDir);
        unindexedRes.IsError.Should().BeTrue();
        ExtractText(unindexedRes).Should().Contain("No indexed CodeExplorer workspace");
    }

    [Test]
    public async Task McpGraphHandler_StandbyMode_RemembersStickyWorkspaceAcrossCalls()
    {
        var wsAlpha = await CreateSeededWorkspaceAsync("ws-sticky-alpha", "AlphaController");
        var wsBeta = await CreateSeededWorkspaceAsync("ws-sticky-beta", "BetaService");
        var unindexedDir = Path.Combine(_tempRoot, "unindexed-sticky-ws");
        Directory.CreateDirectory(unindexedDir);

        var standbyDbPath = Path.Combine(_tempRoot, "standby-sticky.db");
        await using var standbyClient = new SqliteGraphClient(standbyDbPath);

        var repo = new CodeExplorerRepository(standbyClient, defaultWorkspacePath: null);
        var handler = new McpGraphHandler(repo, new HttpContextAccessor())
        {
            CustomRootsProvider = _ => Task.FromResult<IReadOnlyList<string>>([])
        };

        // 1. Before any workspacePath is provided, empty client roots stay in standby
        var initialStandby = await handler.FindSymbolAsync("AlphaController");
        ExtractText(initialStandby).Should().Contain("status: standby");

        // 2. Passing workspacePath once binds it for subsequent calls in the same session
        var explicitAlpha = await handler.FindSymbolAsync("AlphaController", workspacePath: wsAlpha);
        explicitAlpha.IsError.Should().NotBeTrue();
        ExtractText(explicitAlpha).Should().Contain("AlphaController");

        var stickyAlpha = await handler.FindSymbolAsync("AlphaController");
        stickyAlpha.IsError.Should().NotBeTrue();
        ExtractText(stickyAlpha).Should().Contain("AlphaController");

        // 3. Invalid/unindexed workspacePath fails cleanly without overwriting sticky workspace
        var unindexedAttempt = await handler.FindSymbolAsync("AlphaController", workspacePath: unindexedDir);
        unindexedAttempt.IsError.Should().BeTrue();

        var stillAlpha = await handler.FindSymbolAsync("AlphaController");
        stillAlpha.IsError.Should().NotBeTrue();
        ExtractText(stillAlpha).Should().Contain("AlphaController");

        // 4. Passing another indexed workspacePath switches sticky binding to the new workspace
        var explicitBeta = await handler.FindSymbolAsync("BetaService", workspacePath: wsBeta);
        explicitBeta.IsError.Should().NotBeTrue();
        ExtractText(explicitBeta).Should().Contain("BetaService");

        var stickyBeta = await handler.FindSymbolAsync("BetaService");
        stickyBeta.IsError.Should().NotBeTrue();
        ExtractText(stickyBeta).Should().Contain("BetaService");
    }

    private string CreateIndexedWorkspaceDirectory(string folderName)
    {
        var dir = Path.Combine(_tempRoot, folderName);
        var ceDir = Path.Combine(dir, WorkspaceLocator.FolderName);
        Directory.CreateDirectory(ceDir);
        File.WriteAllBytes(Path.Combine(ceDir, WorkspaceLocator.DbFileName), []);
        return dir;
    }

    private async Task<string> CreateSeededWorkspaceAsync(string folderName, string className)
    {
        var dir = Path.Combine(_tempRoot, folderName);
        var ceDir = Path.Combine(dir, WorkspaceLocator.FolderName);
        Directory.CreateDirectory(ceDir);
        var dbPath = Path.Combine(ceDir, WorkspaceLocator.DbFileName);

        await using var client = new SqliteGraphClient(dbPath);
        await client.ExecuteWriteAsync(
            $"INSERT INTO nodes (id, kind, properties) VALUES (@id, {GraphKindCodec.EncodeNodeKind("Type")}, jsonb(json_object('name', @name, 'kind', 'Class', 'path', @path, 'start_line', 1, 'end_line', 10)))",
            new Dictionary<string, object?>
            {
                ["id"] = $"urn:ce:type:{folderName}:{className}",
                ["name"] = className,
                ["path"] = $"src/{className}.cs"
            });
        return dir;
    }

    private static string EncodeColonInFileUri(string uri)
    {
        var idx = uri.IndexOf(":/", StringComparison.Ordinal);
        if (idx < 0) return uri;
        var afterScheme = idx + 2;
        var driveColon = uri.IndexOf(':', afterScheme);
        return driveColon > 0
            ? string.Concat(uri.AsSpan(0, driveColon), "%3A", uri.AsSpan(driveColon + 1))
            : uri;
    }

    private static string ExtractText(CallToolResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(b => b.Text));
    }
}
