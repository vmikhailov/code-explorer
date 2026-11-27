using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
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

        var git = new GitRepositoryRegistry();

        var backendGit = GitSettingsParser.Parse("testws", _tempDir, backendDir)!;
        var clientGit = GitSettingsParser.Parse("testws", _tempDir, clientDir)!;

        git.Register(backendDir, backendGit);
        git.Register(clientDir, clientGit);

        // Nested project inside Backend/Services/OrderService
        var nestedOrderService = Path.Combine(backendDir, "Services", "OrderService");
        Directory.CreateDirectory(nestedOrderService);

        var resolvedBackend = git.FindForPath("testws", _tempDir, "Backend/Services/OrderService");
        resolvedBackend.Should().NotBeNull();
        resolvedBackend!.Id.Should().Be("testws:git:Backend");
        resolvedBackend.Branch.Should().Be("dev");

        var resolvedClient = git.FindForPath("testws", _tempDir, "Client/src/components");
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

        var memClient = new InMemoryGraphRepository();
        var l1Ctx = new Layer1Context(_tempDir, _tempDir, memClient)
        {
            WorkspaceId = "testws"
        };

        // 1. Run Layer 1 Physical scan
        var layer1Parser = new Layer1PhysicalParser();
        var layer1 = await layer1Parser.ParseAsync(l1Ctx);
        layer1.Should().NotBeNull();

        // Verify subrepo GitSettings was discovered and registered in ctx
        var backendSettings = l1Ctx.Git.FindForPath(l1Ctx.WorkspaceId, _tempDir, projDir);
        backendSettings.Should().NotBeNull();
        backendSettings!.Branch.Should().Be("release/1.0");
        backendSettings.OriginUrl.Should().Be("https://git.company.com/backend.git");

        // 2. Create ProjectNode in Layer 2
        var projNode = new ProjectNode(
            $"{l1Ctx.WorkspaceId}:p:Backend/Services/CoreApi:",
            "CoreApi",
            "Backend/Services/CoreApi",
            "csharp"
        );

        // 3. Upload via OntologyUploader
        var l2 = new Layer2Result(new ProjectsStructureNode($"{l1Ctx.WorkspaceId}:projects", "Projects", _tempDir), [projNode], [], []);
        var l3 = new Layer3Result(new SyntaxStructureNode($"{l1Ctx.WorkspaceId}:syntax", "Syntax", _tempDir), [], [], [], [], [], new());
        var l4Ctx = new Layer4Context(l1Ctx.Scope, l1Ctx.Sink, l1Ctx.Git, layer1, l2, l3, new SymbolResolutionBuffer(), new SemanticParseContext(l1Ctx.Scope), [], []);
        await OntologyUploader.UploadNodeTreeAsync(projNode, null, l4Ctx);

        // Verify git signals were recorded in transient ProjectAnalysisSignals
        projNode.Signals().GitBranch.Should().Be("release/1.0");
        projNode.Signals().GitOrigin.Should().Be("https://git.company.com/backend.git");
        projNode.Signals().GitRepo.Should().Be("Backend");

        // Verify USES_GIT relationship exists in ctx.TreeRelationships
        var usesGitRels = l4Ctx.TreeRelationships.Where(r => r.Kind == RelationshipKind.UsesGit).ToList();
        usesGitRels.Should().HaveCount(1);
        usesGitRels[0].From.Should().Be(projNode.Id);
        usesGitRels[0].To.Should().Be($"{l1Ctx.WorkspaceId}:git:Backend");
    }
}
