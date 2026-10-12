using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Android;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.ColdFusion;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Kotlin;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;

namespace CodeExplorer.Tests;

[TestFixture]
public class Layer1Layer2MatrixBaselineTests
{
    private string _sampleProjectsRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new TypeScriptParser());
        WorkspaceIndexer.Register(new JavaScriptParser());
        WorkspaceIndexer.Register(new PythonParser());
        WorkspaceIndexer.Register(new GoParser());
        WorkspaceIndexer.Register(new JavaParser());
        WorkspaceIndexer.Register(new KotlinProjectParser());
        WorkspaceIndexer.Register(new KotlinFileParser());
        WorkspaceIndexer.Register(new AndroidManifestFileParser());
        WorkspaceIndexer.Register(new SqlParser());
        WorkspaceIndexer.Register(new ColdFusionProjectParser());
        WorkspaceIndexer.Register(new ColdFusionFileParser());

        // Resolve absolute path to cli/tests/SampleProjects
        var currentDir = TestContext.CurrentContext.TestDirectory;
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "cli", "tests", "SampleProjects");
            if (Directory.Exists(candidate))
            {
                _sampleProjectsRoot = candidate;
                break;
            }
            var direct = Path.Combine(dir.FullName, "SampleProjects");
            if (Directory.Exists(direct))
            {
                _sampleProjectsRoot = direct;
                break;
            }
            dir = dir.Parent;
        }

        Assert.That(Directory.Exists(_sampleProjectsRoot), Is.True, $"SampleProjects directory not found. Probed upwards from: {TestContext.CurrentContext.TestDirectory}");
    }

    private async Task<(Layer1Result L1, Layer2Result L2)> ScanL1L2Async(string projectRelativePath)
    {
        var absPath = Path.Combine(_sampleProjectsRoot, projectRelativePath).Replace('\\', '/');
        Assert.That(Directory.Exists(absPath), Is.True, $"Directory does not exist: {absPath}");

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(absPath, absPath, client, channel);

        var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
        return (l1, l2);
    }

    #region C# / .NET Matrix

    [Test]
    public async Task CSharp_WebApi_DetectedAsService()
    {
        var (_, l2) = await ScanL1L2Async("csharp/web-api");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task CSharp_ClassLibrary_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("csharp/class-lib");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    [Test]
    public async Task CSharp_WorkerService_DetectedAsWorker()
    {
        var (_, l2) = await ScanL1L2Async("csharp/worker-service");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Worker));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task CSharp_CliTool_DetectedAsCliTool()
    {
        var (_, l2) = await ScanL1L2Async("csharp/cli-tool");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.CliTool));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task CSharp_TestSuite_DetectedAsTest()
    {
        var (_, l2) = await ScanL1L2Async("csharp/test-suite");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Test));
        Assert.That(proj.IsLibrary, Is.True);
    }

    #endregion

    #region TypeScript / JavaScript Matrix

    [Test]
    public async Task TypeScript_NestService_DetectedAsService()
    {
        var (_, l2) = await ScanL1L2Async("typescript/nest-service");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task TypeScript_ExpressService_DetectedAsService()
    {
        var (_, l2) = await ScanL1L2Async("typescript/express-service");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task TypeScript_ReactApp_DetectedAsApp()
    {
        var (_, l2) = await ScanL1L2Async("typescript/react-app");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task TypeScript_UiLibrary_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("typescript/ui-library");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    [Test]
    public async Task TypeScript_CloudflareWorker_DetectedAsWorker()
    {
        var (_, l2) = await ScanL1L2Async("typescript/cf-worker");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Worker));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task TypeScript_CliApp_DetectedAsCliTool()
    {
        var (_, l2) = await ScanL1L2Async("typescript/cli-app");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.CliTool));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task TypeScript_JestTests_DetectedAsTest()
    {
        var (_, l2) = await ScanL1L2Async("typescript/jest-tests");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Test));
        Assert.That(proj.IsLibrary, Is.True);
    }

    [Test]
    public async Task TypeScript_AngularMonorepo_DiscoversOnlyTopLevelProjects_NoSecondaryEntryPoints()
    {
        var (_, l2) = await ScanL1L2Async("typescript/angular-monorepo");

        var projectNames = l2.Projects.Select(p => p.Name).ToList();

        Assert.That(projectNames.Any(n => n.Equals("button", StringComparison.OrdinalIgnoreCase)), Is.False,
            "Secondary entry point 'button' should not be detected as an independent project");
        Assert.That(projectNames.Any(n => n.Equals("modal", StringComparison.OrdinalIgnoreCase)), Is.False,
            "Secondary entry point 'modal' should not be detected as an independent project");

        var uiLib = l2.Projects.FirstOrDefault(p => p.Name.Contains("ui-components"));
        Assert.That(uiLib, Is.Not.Null, "Expected ui-components project in monorepo");
        Assert.That(uiLib!.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(uiLib.IsLibrary, Is.True);

        var portal = l2.Projects.FirstOrDefault(p => p.Name == "portal");
        Assert.That(portal, Is.Not.Null, "Expected portal project in monorepo");
        Assert.That(portal!.EntityKind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(portal.IsLibrary, Is.False);
    }

    #endregion

    #region Python Matrix

    [Test]
    public async Task Python_FastApiService_DetectedAsService()
    {
        var (_, l2) = await ScanL1L2Async("python/fastapi-service");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Python_StandardLib_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("python/standard-lib");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    [Test]
    public async Task Python_CeleryWorker_DetectedAsWorker()
    {
        var (_, l2) = await ScanL1L2Async("python/celery-worker");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Worker));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Python_ClickCli_DetectedAsCliTool()
    {
        var (_, l2) = await ScanL1L2Async("python/click-cli");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.CliTool));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Python_Alembic_DetectedAsDatabaseMigration()
    {
        var (_, l2) = await ScanL1L2Async("python/alembic-migrations");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.DatabaseMigration));
        Assert.That(proj.IsLibrary, Is.False);
    }

    #endregion

    #region Go Matrix

    [Test]
    public async Task Go_GinService_DetectedAsService()
    {
        var (_, l2) = await ScanL1L2Async("go/gin-service");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Go_PkgLib_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("go/pkg-lib");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    [Test]
    public async Task Go_CobraCli_DetectedAsCliTool()
    {
        var (_, l2) = await ScanL1L2Async("go/cobra-cli");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.CliTool));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Go_KafkaWorker_DetectedAsWorker()
    {
        var (_, l2) = await ScanL1L2Async("go/kafka-worker");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Worker));
        Assert.That(proj.IsLibrary, Is.False);
    }

    #endregion

    #region Java Matrix

    [Test]
    public async Task Java_SpringService_DetectedAsService()
    {
        var (_, l2) = await ScanL1L2Async("java/spring-service");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Service));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Java_JarLibrary_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("java/jar-library");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    #endregion

    #region Kotlin Matrix

    [Test]
    public async Task Kotlin_AndroidApp_DetectedAsApp()
    {
        var (_, l2) = await ScanL1L2Async("kotlin/android-app");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task Kotlin_JvmLib_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("kotlin/jvm-lib");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    #endregion

    #region ColdFusion Matrix

    [Test]
    public async Task ColdFusion_WebApp_DetectedAsApp()
    {
        var (_, l2) = await ScanL1L2Async("coldfusion/cf-web-app");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.App));
        Assert.That(proj.IsLibrary, Is.False);
    }

    [Test]
    public async Task ColdFusion_Components_DetectedAsLibrary()
    {
        var (_, l2) = await ScanL1L2Async("coldfusion/cf-components");
        Assert.That(l2.Projects, Has.Count.EqualTo(1));
        var proj = l2.Projects[0];
        Assert.That(proj.EntityKind, Is.EqualTo(ProjectEntityKind.Library));
        Assert.That(proj.IsLibrary, Is.True);
    }

    #endregion
}
