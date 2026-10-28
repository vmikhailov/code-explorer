using System.Reflection;
using CodeExplorer.Parser.Script;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ParserAssemblyDiscoveryTests
{
    private static readonly string[] ExpectedParserAssemblies =
    [
        "CodeExplorer.Parser.Android",
        "CodeExplorer.Parser.ColdFusion",
        "CodeExplorer.Parser.CSharp",
        "CodeExplorer.Parser.Go",
        "CodeExplorer.Parser.Java",
        "CodeExplorer.Parser.Kotlin",
        "CodeExplorer.Parser.Python",
        "CodeExplorer.Parser.Rust",
        "CodeExplorer.Parser.Script",
        "CodeExplorer.Parser.Shared",
        "CodeExplorer.Parser.SQL",
        "CodeExplorer.Parser.TypeScript"
    ];

    [Test]
    public void Discover_ReturnsOnlyAttributedParserAssemblies_OrderedByName()
    {
        var assemblies = ParserAssemblyDiscovery.Discover();

        assemblies.Select(a => a.GetName().Name).Should().Equal(ExpectedParserAssemblies);
        assemblies.Should().OnlyContain(a => ParserAssemblyDiscovery.IsParserAssembly(a));
    }

    [Test]
    public void HasParserAssemblyAttribute_ReadsMetadataWithoutLoading()
    {
        var parserDll = typeof(ScriptProjectParser).Assembly.Location;
        var coreDll = typeof(ParserAssemblyAttribute).Assembly.Location;
        var testDll = Assembly.GetExecutingAssembly().Location;

        ParserAssemblyDiscovery.HasParserAssemblyAttribute(parserDll).Should().BeTrue();
        ParserAssemblyDiscovery.HasParserAssemblyAttribute(coreDll).Should().BeFalse();
        ParserAssemblyDiscovery.HasParserAssemblyAttribute(testDll).Should().BeFalse();
    }

    [Test]
    public void HasParserAssemblyAttribute_ReturnsFalseForNonManagedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ce-not-managed-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path, [0x4D, 0x5A, 0x00, 0x01, 0x02]);
        try
        {
            ParserAssemblyDiscovery.HasParserAssemblyAttribute(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void GetParserTypes_ReturnsConcreteParsersOrderedByName()
    {
        var types = ParserAssemblyDiscovery.GetParserTypes(typeof(ScriptProjectParser).Assembly);

        types.Should().Contain(typeof(ScriptProjectParser));
        types.Should().OnlyContain(t => ParserAssemblyDiscovery.IsParserType(t));
        types.Select(t => t.FullName).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Test]
    public void AutoRegisterParsers_RegistersTypesFromEveryParserAssembly()
    {
        var registered = WorkspaceIndexer.AutoRegisterParsers();

        registered.Select(t => t.Assembly.GetName().Name).Distinct()
            .Should().BeEquivalentTo(ExpectedParserAssemblies);
        registered.Should().OnlyHaveUniqueItems();
    }
}
