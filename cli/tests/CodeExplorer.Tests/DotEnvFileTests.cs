using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class DotEnvFileTests
{
    [TestCase(".env")]
    [TestCase(".env.local")]
    [TestCase(".env.production")]
    [TestCase("dev.env")]
    [TestCase("Prod.ENV")]
    public void SharedRegistry_RoutesDotEnvVariantsToEnvFileParser(string name)
    {
        SharedFileParserRegistry.GetParsers(name).Select(p => p.Name).Should().Contain("dotenv");
        ConfigurationIndex.IsConfigurationFile(name).Should().BeTrue();
    }

    [TestCase("environment.ts")]
    [TestCase("env.json")]
    [TestCase("README.md")]
    public void SharedRegistry_IgnoresNonEnvFiles(string name)
    {
        SharedFileParserRegistry.GetParsers(name).Select(p => p.Name).Should().NotContain("dotenv");
    }

    [Test]
    public void EnvFileParser_ProducesEnvironmentInformation()
    {
        var content = "# c\nexport A=\"x y\"\nB=plain # tail\nNOEQUALS\n";
        var result = SharedFileParserRegistry.Parse("/w/.env", content);

        var env = result.OfType<EnvironmentInformation>().Single();
        env.Values.Should().BeEquivalentTo([new ConfigValue("A", "x y"), new ConfigValue("B", "plain")]);
    }

    [Test]
    public void SharedRegistry_AllowsMultipleParsersPerExtension()
    {
        SharedFileParserRegistry.Register(new ExtraJsonParser());
        try
        {
            SharedFileParserRegistry.GetParsers("appsettings.json").Select(p => p.Name)
                .Should().Contain(["json-configuration", "extra-json"]);
        }
        finally
        {
            SharedFileParserRegistry.Clear();
            WorkspaceIndexer.AutoRegisterParsers();
        }
    }

    private sealed class ExtraJsonParser : ISharedFileParser
    {
        public string Name => "extra-json";
        public IReadOnlyCollection<string> Extensions { get; } = [".json"];
        public IProjectEnrichment? Parse(SharedFileContext file) => null;
    }
}
