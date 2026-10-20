using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CSharpParserTests
{
    private string _sampleProjectsRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
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

    [Test]
    public async Task CSharp_LegacyFrameworkLib_ParsesPackagesConfigAndProducesPackage()
    {
        var parser = new CSharpParser();
        var absPath = Path.Combine(_sampleProjectsRoot, "csharp", "legacy-framework-lib");
        var deps = await parser.ParseDependenciesAsync(absPath);
        deps.ExternalPackages.Should().ContainSingle(p => p.Name == "Newtonsoft.Json" && p.Version == "13.0.3");

        var produced = await parser.GetProducedPackageAsync(absPath);
        produced.Should().NotBeNull();
        produced!.Name.Should().Be("LegacyLib");
    }

    [Test]
    public async Task CSharp_LegacyFrameworkWeb_ParsesPackagesConfigDependencies()
    {
        var parser = new CSharpParser();
        var absPath = Path.Combine(_sampleProjectsRoot, "csharp", "legacy-framework-web");
        var deps = await parser.ParseDependenciesAsync(absPath);
        deps.ExternalPackages.Should().Contain(p => p.Name == "Microsoft.AspNet.WebApi.Core" && p.Version == "5.2.9");
        deps.ExternalPackages.Should().Contain(p => p.Name == "Newtonsoft.Json" && p.Version == "13.0.3");
    }
}
