using System.Reflection;
using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Tests;

[TestFixture]
public class Layer1Layer2MatrixBaselineTests
{
    private string _sampleProjectsRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();

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

        Directory.Exists(_sampleProjectsRoot).Should().BeTrue($"SampleProjects directory not found. Probed upwards from: {TestContext.CurrentContext.TestDirectory}");
    }

    private async Task<(Layer1Result L1, Layer2Result L2)> ScanL1L2Async(string projectRelativePath)
    {
        var absPath = Path.Combine(_sampleProjectsRoot, projectRelativePath).Replace('\\', '/');
        Directory.Exists(absPath).Should().BeTrue($"Directory does not exist: {absPath}");

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(absPath, absPath, client, channel);

        var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
        return (l1, l2);
    }

    private static ProjectNode AssertSingleProject(Layer2Result l2, ProjectEntityKind expectedKind, bool expectedIsLib)
    {
        var proj = l2.Projects.Should().ContainSingle().Subject;
        proj.EntityKind.Should().Be(expectedKind);
        proj.IsLibrary.Should().Be(expectedIsLib);
        return proj;
    }

    #region Parameterized Matrix Tests

    [TestCase("csharp/web-api", "csharp", ProjectEntityKind.Service, false)]
    [TestCase("csharp/class-lib", "csharp", ProjectEntityKind.Library, true)]
    [TestCase("csharp/worker-service", "csharp", ProjectEntityKind.Worker, false)]
    [TestCase("csharp/cli-tool", "csharp", ProjectEntityKind.CliTool, false)]
    [TestCase("csharp/test-suite", "csharp", ProjectEntityKind.Test, true)]
    [TestCase("typescript/nest-service", "typescript", ProjectEntityKind.Service, false)]
    [TestCase("typescript/express-service", "typescript", ProjectEntityKind.Service, false)]
    [TestCase("typescript/react-app", "typescript", ProjectEntityKind.App, false)]
    [TestCase("typescript/ui-library", "typescript", ProjectEntityKind.Library, true)]
    [TestCase("typescript/cf-worker", "typescript", ProjectEntityKind.Worker, false)]
    [TestCase("typescript/cli-app", "typescript", ProjectEntityKind.CliTool, false)]
    [TestCase("typescript/jest-tests", "typescript", ProjectEntityKind.Test, true)]
    [TestCase("python/fastapi-service", "python", ProjectEntityKind.Service, false)]
    [TestCase("python/standard-lib", "python", ProjectEntityKind.Library, true)]
    [TestCase("python/celery-worker", "python", ProjectEntityKind.Worker, false)]
    [TestCase("python/click-cli", "python", ProjectEntityKind.CliTool, false)]
    [TestCase("python/alembic-migrations", "python", ProjectEntityKind.DatabaseMigration, false)]
    [TestCase("go/gin-service", "go", ProjectEntityKind.Service, false)]
    [TestCase("go/pkg-lib", "go", ProjectEntityKind.Library, true)]
    [TestCase("go/cobra-cli", "go", ProjectEntityKind.CliTool, false)]
    [TestCase("go/kafka-worker", "go", ProjectEntityKind.Worker, false)]
    [TestCase("java/spring-service", "java", ProjectEntityKind.Service, false)]
    [TestCase("java/jar-library", "java", ProjectEntityKind.Library, true)]
    [TestCase("kotlin/android-app", "kotlin", ProjectEntityKind.App, false)]
    [TestCase("kotlin/jvm-lib", "kotlin", ProjectEntityKind.Library, true)]
    [TestCase("coldfusion/cf-web-app", "coldfusion", ProjectEntityKind.App, false)]
    [TestCase("coldfusion/cf-components", "coldfusion", ProjectEntityKind.Library, true)]
    public async Task Matrix_SingleProject_DetectedWithExpectedKindAndIsLib(
        string projectPath,
        string expectedProjectType,
        ProjectEntityKind expectedKind,
        bool expectedIsLib)
    {
        var (_, l2) = await ScanL1L2Async(projectPath);
        var proj = AssertSingleProject(l2, expectedKind, expectedIsLib);
        proj.ProjectType.Should().Be(expectedProjectType);
    }

    #endregion

    #region Complex Topology Scenarios

    [Test]
    public async Task TypeScript_AngularMonorepo_DiscoversOnlyTopLevelProjects_NoSecondaryEntryPoints()
    {
        var (_, l2) = await ScanL1L2Async("typescript/angular-monorepo");

        var projectNames = l2.Projects.Select(p => p.Name).ToList();

        // Secondary entry points must be excluded
        projectNames.Should().NotContain("button")
            .And.NotContain("modal");

        var uiLib = l2.Projects.Should().ContainSingle(p => p.Name.Contains("ui-components")).Subject;
        uiLib.EntityKind.Should().Be(ProjectEntityKind.Library);
        uiLib.IsLibrary.Should().BeTrue();

        var portal = l2.Projects.Should().ContainSingle(p => p.Name == "portal").Subject;
        portal.EntityKind.Should().Be(ProjectEntityKind.App);
        portal.IsLibrary.Should().BeFalse();
    }

    #endregion

    #region Invariant Guard: All Parsers Must Be Covered

    [Test]
    public void AllParsers_MustBeCoveredByBaselineMatrixTests()
    {
        // 1. Discover all parsers via reflection
        var registeredParsers = WorkspaceIndexer.AutoRegisterParsers();
        registeredParsers.Should().NotBeEmpty();

        // 2. Collect all project types from registered IProjectParser implementations
        var projectParsers = WorkspaceIndexer.GetAllProjectParsers();
        var discoveredProjectTypes = projectParsers
            .Select(p => p.ProjectType.ToLowerInvariant())
            .Distinct()
            .Where(t => t != "sql" && t != "javascript") // SQL is embedded, JavaScript is handled by TypeScriptParser
            .ToList();

        // 3. Extract all tested project types from Matrix_SingleProject_DetectedWithExpectedKindAndIsLib TestCases
        var method = typeof(Layer1Layer2MatrixBaselineTests)
            .GetMethod(nameof(Matrix_SingleProject_DetectedWithExpectedKindAndIsLib))!;
        var testCases = method.GetCustomAttributes(typeof(TestCaseAttribute), false)
            .Cast<TestCaseAttribute>()
            .ToList();

        var testedProjectTypes = testCases
            .Select(tc => tc.Arguments[1] as string)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        // 4. Assert every discovered project parser is tested in the matrix
        var untested = discoveredProjectTypes.Where(t => !testedProjectTypes.Contains(t)).ToList();
        untested.Should().BeEmpty(
            $"Every project parser must have sample projects and coverage in {nameof(Layer1Layer2MatrixBaselineTests)}! " +
            $"Untested project types: [{string.Join(", ", untested)}]");

        // 5. Assert all IFileParser implementations are registered
        var fileParsers = WorkspaceIndexer.GetAllFileParsers();
        fileParsers.Should().NotBeEmpty();

        // 6. Assert all sample project paths in test cases actually exist on disk
        foreach (var tc in testCases)
        {
            var relPath = (string)tc.Arguments[0]!;
            var absPath = Path.Combine(_sampleProjectsRoot, relPath);
            Directory.Exists(absPath).Should().BeTrue($"Sample project directory must exist on disk: {relPath}");
        }
    }

    #endregion
}
