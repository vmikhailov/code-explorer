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
