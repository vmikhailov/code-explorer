using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GitSettingsTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_git_test_" + Guid.NewGuid().ToString("N"));
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
    public void Test_Subdirectory_Git_Repositories_Are_Discovered_And_Parsed()
    {
        var backendDir = Path.Combine(_tempDir, "Backend");
        var clientDir = Path.Combine(_tempDir, "Client");
        Directory.CreateDirectory(Path.Combine(backendDir, ".git"));
        Directory.CreateDirectory(Path.Combine(clientDir, ".git"));

        File.WriteAllText(Path.Combine(backendDir, ".git", "HEAD"), "ref: refs/heads/feature/super-api\n");
        File.WriteAllText(Path.Combine(backendDir, ".git", "config"), "[remote \"origin\"]\n\turl = https://example.com/backend.git\n");

        File.WriteAllText(Path.Combine(clientDir, ".git", "HEAD"), "ref: refs/heads/release/v2\n");
        File.WriteAllText(Path.Combine(clientDir, ".git", "config"), "[remote \"origin\"]\n\turl = https://example.com/client.git\n");

        var backendGit = GitSettingsParser.Parse("testws", _tempDir, backendDir);
        backendGit.Should().NotBeNull();
        backendGit!.Id.Should().Be("testws:git:Backend");
        backendGit.Branch.Should().Be("feature/super-api");
        backendGit.OriginUrl.Should().Be("https://example.com/backend.git");
        backendGit.Name.Should().Be("Git Settings (Backend)");

        var clientGit = GitSettingsParser.Parse("testws", _tempDir, clientDir);
        clientGit.Should().NotBeNull();
        clientGit!.Id.Should().Be("testws:git:Client");
        clientGit.Branch.Should().Be("release/v2");
        clientGit.OriginUrl.Should().Be("https://example.com/client.git");
        clientGit.Name.Should().Be("Git Settings (Client)");
    }

    [Test]
    public void Test_ParsingContext_FindGitSettingsForPath_Resolves_Closest_Repository()
    {
        var backendDir = Path.Combine(_tempDir, "Backend");
        var clientDir = Path.Combine(_tempDir, "Client");
        Directory.CreateDirectory(Path.Combine(backendDir, ".git"));
        Directory.CreateDirectory(Path.Combine(clientDir, ".git"));

        File.WriteAllText(Path.Combine(backendDir, ".git", "HEAD"), "ref: refs/heads/dev\n");
        File.WriteAllText(Path.Combine(backendDir, ".git", "config"), "[remote \"origin\"]\n\turl = https://gitlab.com/corp/backend.git\n");

        File.WriteAllText(Path.Combine(clientDir, ".git", "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(clientDir, ".git", "config"), "[remote \"origin\"]\n\turl = https://gitlab.com/corp/client.git\n");

        var ctx = new ParsingContext(
            absoluteWorkspacePath: _tempDir,
            hostWorkspacePath: _tempDir,
            dbClient: new InMemoryGraphClient(),
            sharedChannel: System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>(),
            clear: false)
        {
            WorkspaceId = "testws"
        };

        var backendGit = GitSettingsParser.Parse("testws", _tempDir, backendDir)!;
        var clientGit = GitSettingsParser.Parse("testws", _tempDir, clientDir)!;

        ctx.RegisterGitRepository(backendDir, backendGit);
        ctx.RegisterGitRepository(clientDir, clientGit);

        // Nested project inside Backend/Services/OrderService
        var nestedOrderService = Path.Combine(backendDir, "Services", "OrderService");
        Directory.CreateDirectory(nestedOrderService);

        var resolvedBackend = ctx.FindGitSettingsForPath("Backend/Services/OrderService");
        resolvedBackend.Should().NotBeNull();
        resolvedBackend!.Id.Should().Be("testws:git:Backend");
        resolvedBackend.Branch.Should().Be("dev");

        var resolvedClient = ctx.FindGitSettingsForPath("Client/src/components");
        resolvedClient.Should().NotBeNull();
        resolvedClient!.Id.Should().Be("testws:git:Client");
        resolvedClient.Branch.Should().Be("main");
    }

    [Test]
    public async Task Test_Layer1_And_OntologyUploader_Populates_Subrepo_Git_Settings_And_Project_Properties()
    {
        var backendDir = Path.Combine(_tempDir, "Backend");
        var projDir = Path.Combine(backendDir, "Services", "CoreApi");
        Directory.CreateDirectory(Path.Combine(backendDir, ".git"));
        Directory.CreateDirectory(projDir);

        File.WriteAllText(Path.Combine(backendDir, ".git", "HEAD"), "ref: refs/heads/release/1.0\n");
        File.WriteAllText(Path.Combine(backendDir, ".git", "config"), "[remote \"origin\"]\n\turl = https://git.company.com/backend.git\n");

        var memClient = new InMemoryGraphClient();
        var ctx = new ParsingContext(
            absoluteWorkspacePath: _tempDir,
            hostWorkspacePath: _tempDir,
            dbClient: memClient,
            sharedChannel: System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>(),
            clear: false)
        {
            WorkspaceId = "testws"
        };

        // 1. Run Layer 1 Physical scan
        var layer1Parser = new Layer1PhysicalParser();
        var layer1 = await layer1Parser.ParseAsync(ctx);
        layer1.Should().NotBeNull();

        // Verify subrepo GitSettings was discovered and registered in ctx
        var backendSettings = ctx.FindGitSettingsForPath(projDir);
        backendSettings.Should().NotBeNull();
        backendSettings!.Branch.Should().Be("release/1.0");
        backendSettings.OriginUrl.Should().Be("https://git.company.com/backend.git");

        // 2. Create ProjectNode in Layer 2
        var projNode = new ProjectNode(
            $"{ctx.WorkspaceId}:p:Backend/Services/CoreApi:",
            "CoreApi",
            "Backend/Services/CoreApi",
            "csharp",
            extensions: new Dictionary<string, string>()
        );

        // 3. Upload via OntologyUploader
        await OntologyUploader.UploadNodeTreeAsync(projNode, null, ctx);

        // Verify git properties were populated on the project node
        projNode.Extensions.Should().NotBeNull();
        projNode.Extensions!.GetValueOrDefault("git_branch").Should().Be("release/1.0");
        projNode.Extensions!.GetValueOrDefault("git_origin").Should().Be("https://git.company.com/backend.git");
        projNode.Extensions!.GetValueOrDefault("git_repo").Should().Be("Backend");

        // Verify USES_GIT relationship exists in ctx.TreeRelationships
        var usesGitRels = ctx.TreeRelationships.Where(r => r.Kind == "USES_GIT").ToList();
        usesGitRels.Should().HaveCount(1);
        usesGitRels[0].From.Should().Be(projNode.Id);
        usesGitRels[0].To.Should().Be($"{ctx.WorkspaceId}:git:Backend");
    }
}
