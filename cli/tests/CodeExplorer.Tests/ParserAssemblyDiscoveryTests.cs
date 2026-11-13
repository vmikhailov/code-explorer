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
        "CodeExplorer.Parser.Terraform",
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

    [Test]
    public void ParserTypes_StrictSeparationOfProjectAndFileParsers()
    {
        var assemblies = ParserAssemblyDiscovery.Discover();
        var allParserTypes = assemblies.SelectMany(ParserAssemblyDiscovery.GetParserTypes).ToList();

        allParserTypes.Should().NotBeEmpty();
        foreach (var parserType in allParserTypes)
        {
            var isProjectParser = typeof(IProjectParser).IsAssignableFrom(parserType);
            var isFileParser = typeof(IFileParser).IsAssignableFrom(parserType);

            (isProjectParser && isFileParser).Should().BeFalse(
                $"{parserType.FullName} must not implement both IProjectParser (Layer 2) and IFileParser (Layer 3)");
        }
    }

    [Test]
    public void AssemblyBoundaries_IndexingPipelineIsInIndexingAssembly_AndAnalysisIsDecoupled()
    {
        var indexingAsm = typeof(WorkspaceIndexer).Assembly;
        indexingAsm.GetName().Name.Should().Be("CodeExplorer.Indexing");

        typeof(CodeExplorer.Core.Parser.Layers.Layer1PhysicalParser).Assembly.Should().BeSameAs(indexingAsm);
        typeof(CodeExplorer.Core.Parser.Layers.Layer2ProjectParser).Assembly.Should().BeSameAs(indexingAsm);
        typeof(CodeExplorer.Core.Parser.Layers.Layer3SyntacticParser).Assembly.Should().BeSameAs(indexingAsm);
        typeof(CodeExplorer.Core.Parser.Layers.Layer4SemanticParser).Assembly.Should().BeSameAs(indexingAsm);
        typeof(CodeExplorer.Core.Parser.Layers.Layer5AnalysisParser).Assembly.Should().BeSameAs(indexingAsm);

        var coreRefs = typeof(ParserRegistry).Assembly.GetReferencedAssemblies().Select(r => r.Name).ToList();
        coreRefs.Should().NotContain("CodeExplorer.Indexing");
        coreRefs.Should().NotContain("CodeExplorer.Analysis");

        var indexingRefs = indexingAsm.GetReferencedAssemblies().Select(r => r.Name).ToList();
        indexingRefs.Should().NotContain("CodeExplorer.Analysis");
    }

    [Test]
    public async Task ScanInMemoryAsync_Layer5Global_BuildsGraphInMemory_AndSavesToSqlite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ce-inmem-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Demo.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Greeter.cs"), "namespace Demo; public class Greeter { public string Greet(string name) => name; }");

            using var scanResult = await WorkspaceIndexer.ScanInMemoryAsync(
                tempDir,
                new WorkspaceScanOptions { MaxLayer = AnalysisLayer.Layer5_Global });
            scanResult.Graph.Should().NotBeNull();
            scanResult.Graph!.UploadedNodes.Should().NotBeEmpty();
            scanResult.Graph.UploadedRelationships.Should().NotBeEmpty();

            var dbPath = Path.Combine(tempDir, "graph.db");
            using var sqlite = new SqliteGraphClient(dbPath);
            await scanResult.SaveToAsync(sqlite);

            var (savedNodes, savedRels) = await sqlite.GetGraphCountsAsync();
            savedNodes.Should().Be(scanResult.Graph.UploadedNodes.Count);
            savedRels.Should().Be(scanResult.Graph.UploadedRelationships.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
