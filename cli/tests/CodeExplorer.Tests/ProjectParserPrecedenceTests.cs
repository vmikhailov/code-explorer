using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectParserPrecedenceTests
{
    private static readonly string[] ExpectedOrder =
    [
        "coldfusion", "csharp", "go", "java", "kotlin", "python", "rust",
        "typescript", "javascript",
        "sql",
        "script"
    ];

    [Test]
    public void RegisteredProjectParsers_AreOrderedByTierThenSupersedesThenType()
    {
        WorkspaceIndexer.AutoRegisterParsers();

        WorkspaceIndexer.GetAllProjectParsers().Select(p => p.ProjectType)
            .Should().Equal(ExpectedOrder);
    }

    [Test]
    public void Order_IsIndependentOfRegistrationOrder()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var parsers = WorkspaceIndexer.GetAllProjectParsers().ToList();
        var random = new Random(42);

        for (var i = 0; i < 20; i++)
        {
            var shuffled = parsers.OrderBy(_ => random.Next()).ToList();
            ProjectParserPrecedence.Order(shuffled).Select(p => p.ProjectType)
                .Should().Equal(ExpectedOrder);
        }
    }

    [Test]
    public void Order_PutsSupersedingParserFirstEvenWhenRegisteredLater()
    {
        IProjectParser js = new JavaScriptParser();
        IProjectParser ts = new TypeScriptParser();

        ProjectParserPrecedence.Order([js, ts]).Should().Equal(ts, js);
    }

    [Test]
    public void RemoveSuperseded_DropsJavaScriptWhenTypeScriptAlsoMatches()
    {
        IProjectParser cs = new CSharpParser();
        IProjectParser ts = new TypeScriptParser();
        IProjectParser js = new JavaScriptParser();
        var matching = new List<IProjectParser> { cs, ts, js };

        ProjectParserPrecedence.RemoveSuperseded(matching);

        matching.Should().Equal(cs, ts);
    }

    [Test]
    public void RemoveSuperseded_KeepsJavaScriptWhenItMatchesAlone()
    {
        IProjectParser js = new JavaScriptParser();
        var matching = new List<IProjectParser> { js };

        ProjectParserPrecedence.RemoveSuperseded(matching);

        matching.Should().Equal(js);
    }

    [Test]
    public void Order_ThrowsOnSupersedesCycle()
    {
        var act = () => ProjectParserPrecedence.Order([new CycleAParser(), new CycleBParser()]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cyclic*");
    }

    [Test]
    public void ParserWithoutAttribute_DefaultsToDefaultTierAndStandardRole()
    {
        IProjectParser parser = new UnattributedParser();

        ProjectParserPrecedence.GetTier(parser).Should().Be(ParserTier.Default);
        parser.ParserRole.Should().Be(ProjectParserRole.Standard);
    }

    [Test]
    public void TypeScriptAndPackageJsonDirectory_ResolvesToTypeScript()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ce-precedence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "package.json"), """{ "name": "web" }""");
            File.WriteAllText(Path.Combine(dir, "tsconfig.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "index.ts"), "export const x = 1;");

            WorkspaceIndexer.AutoRegisterParsers();
            var files = Directory.GetFiles(dir);
            var matching = WorkspaceIndexer.GetAllProjectParsers()
                .Where(p => !p.IsFallback && p.IsProjectDirectory(dir, files))
                .ToList();
            ProjectParserPrecedence.RemoveSuperseded(matching);

            matching.Select(p => p.ProjectType).Should().Equal("typescript");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public void TypeScriptTryParse_CfWorkerLayout_AbsorbsWorkersSiteAndExcludesPublicSrc()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "ce-cfworker-" + Guid.NewGuid().ToString("N"));
        var trackerDir = Path.Combine(rootDir, "workers", "tracker-worker");
        var workersSiteDir = Path.Combine(trackerDir, "workers-site");
        var publicSrcDir = Path.Combine(trackerDir, "public_src");
        Directory.CreateDirectory(workersSiteDir);
        Directory.CreateDirectory(publicSrcDir);

        try
        {
            File.WriteAllText(Path.Combine(rootDir, "package.json"), """
            {
              "name": "atscfworkers",
              "devDependencies": { "gulp": "^4.0.0", "webpack": "^5.0.0" }
            }
            """);
            File.WriteAllText(Path.Combine(trackerDir, "wrangler.toml"), """
            name = "tracker-worker-dev"
            main = "workers-site/index.js"
            [site]
            bucket = "./public"
            """);
            File.WriteAllText(Path.Combine(workersSiteDir, "package.json"), """{ "name": "workers-site" }""");
            File.WriteAllText(Path.Combine(workersSiteDir, "index.js"), "export default {};");

            var tsParser = new TypeScriptParser();

            // 1. Root cf-worker is detected as a build orchestrator wrapping nested workers
            var rootCtx = new ProjectParseContext(rootDir, Directory.GetFiles(rootDir), rootDir);
            tsParser.TryParse(rootCtx, out var rootRes).Should().BeTrue();
            rootRes!.Project.ManifestProperties.GetValueOrDefault("is_build_orchestrator").Should().Be("true");

            // 2. tracker-worker is detected from wrangler.toml, strips "-dev", absorbs "workers-site", and excludes "public_src" & "public"
            var trackerCtx = new ProjectParseContext(trackerDir, Directory.GetFiles(trackerDir), rootDir);
            tsParser.TryParse(trackerCtx, out var trackerRes).Should().BeTrue();
            trackerRes!.Project.Name.Should().Be("tracker-worker");
            trackerRes.Project.AbsorbedSubpaths.Should().Contain("workers-site");
            trackerRes.Project.ExcludedSubpaths.Should().Contain("public");
            trackerRes.Project.ExcludedSubpaths.Should().Contain("public_src");

            // 3. Nested workers-site/package.json is absorbed and not treated as a standalone project
            var wsCtx = new ProjectParseContext(workersSiteDir, Directory.GetFiles(workersSiteDir), rootDir);
            tsParser.TryParse(wsCtx, out _).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(rootDir, true);
        }
    }

    [Test]
    public void CSharpTryParse_MultipleCsprojInSameFolder_EmitsPrimaryAndAdditionalSiblingProjects()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ce-multicsproj-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Billing.Api.csproj"), """<Project Sdk="Microsoft.NET.Sdk.Web"></Project>""");
            File.WriteAllText(Path.Combine(dir, "Billing.Worker.csproj"), """<Project Sdk="Microsoft.NET.Sdk.Worker"></Project>""");

            var csParser = new CSharpParser();
            var ctx = new ProjectParseContext(dir, Directory.GetFiles(dir), dir);
            csParser.TryParse(ctx, out var res).Should().BeTrue();

            res!.Project.Name.Should().Be("Billing.Api");
            res.ClaimMode.Should().Be(ParserClaimMode.AbsorbSecondaryInDirectory);
            res.AdditionalSiblingProjects.Should().ContainSingle()
                .Which.Name.Should().Be("Billing.Worker");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private abstract class FakeParser : IProjectParser
    {
        public abstract string ProjectType { get; }
        public IReadOnlyCollection<string> ExcludedFolders => [];
        public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory) => false;
        public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory) => Task.FromResult<ProducedPackageInfo?>(null);
        public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory) => Task.FromResult(new ProjectDependencyInfo([], []));
    }

    [ProjectParser(Supersedes = ["cycle-b"])]
    private sealed class CycleAParser : FakeParser
    {
        public override string ProjectType => "cycle-a";
    }

    [ProjectParser(Supersedes = ["cycle-a"])]
    private sealed class CycleBParser : FakeParser
    {
        public override string ProjectType => "cycle-b";
    }

    private sealed class UnattributedParser : FakeParser
    {
        public override string ProjectType => "unattributed";
    }
}
