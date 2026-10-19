using AwesomeAssertions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SemanticRuntimeParityGateTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CeParityTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public void CSharpParser_ExtractManifestProperties_ClassifiesClientAppUnitTestsAsTestSuite()
    {
        var parser = new CSharpParser();
        var projDir = Path.Combine(_tempDir, "ClientApp.UnitTests");
        Directory.CreateDirectory(projDir);
        var csproj = Path.Combine(projDir, "ClientApp.UnitTests.csproj");

        File.WriteAllText(csproj, """
            <Project Sdk="MSTest.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <UseMaui>true</UseMaui>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);

        var files = Directory.GetFiles(projDir);
        var props = parser.ExtractManifestProperties(projDir, files);

        props["is_test_project"].Should().Be("true");
        props["manifest_type"].Should().Be("test");
        props["role"].Should().Be("Test");
        props["kind"].Should().Be("TestSuite");

        var context = new ProjectContext(projDir, "tests/ClientApp.UnitTests", "ClientApp.UnitTests", "csharp", files, [], props);
        var classification = parser.ClassifyProject(context);

        classification.Should().Be(ProjectClassification.Test);
        classification.Kind.Should().Be(ProjectEntityKind.Test);
    }

    [Test]
    public void CSharpParser_ExtractManifestProperties_ClassifiesBasketFunctionalTestsAsTestSuite()
    {
        var parser = new CSharpParser();
        var projDir = Path.Combine(_tempDir, "Basket.FunctionalTests");
        Directory.CreateDirectory(projDir);
        var csproj = Path.Combine(projDir, "Basket.FunctionalTests.csproj");

        File.WriteAllText(csproj, """
            <Project Sdk="Aspire.AppHost.Sdk/13.6.0">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit.v3.mtp-v2" />
                <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
              </ItemGroup>
            </Project>
            """);

        var files = Directory.GetFiles(projDir);
        var props = parser.ExtractManifestProperties(projDir, files);

        props["is_test_project"].Should().Be("true");
        props["manifest_type"].Should().Be("test");
        props["role"].Should().Be("Test");
        props["kind"].Should().Be("TestSuite");

        var context = new ProjectContext(projDir, "tests/Basket.FunctionalTests", "Basket.FunctionalTests", "csharp", files, ["Microsoft.AspNetCore.Mvc.Testing"], props);
        var classification = parser.ClassifyProject(context);

        classification.Should().Be(ProjectClassification.Test);
        classification.Kind.Should().Be(ProjectEntityKind.Test);
    }

    [Test]
    public void CSharpParser_ExtractManifestProperties_ClassifiesWebAppComponentsAsLibrary()
    {
        var parser = new CSharpParser();
        var projDir = Path.Combine(_tempDir, "WebAppComponents");
        Directory.CreateDirectory(projDir);
        var csproj = Path.Combine(projDir, "WebAppComponents.csproj");

        File.WriteAllText(csproj, """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>eShop.WebAppComponents</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
              </ItemGroup>
            </Project>
            """);

        var files = Directory.GetFiles(projDir);
        var props = parser.ExtractManifestProperties(projDir, files);

        props["is_library"].Should().Be("true");
        props["manifest_type"].Should().Be("library");
        props["role"].Should().Be("Library");
        props["kind"].Should().Be("Library");

        var context = new ProjectContext(projDir, "src/WebAppComponents", "WebAppComponents", "csharp", files, [], props);
        var classification = parser.ClassifyProject(context);

        classification.Should().Be(ProjectClassification.Library);
        classification.Kind.Should().Be(ProjectEntityKind.Library);
    }

    [Test]
    public void CSharpParser_ExtractManifestProperties_ClassifiesAspireAppHostAsOrchestratorLibrary()
    {
        var parser = new CSharpParser();
        var projDir = Path.Combine(_tempDir, "eShop.AppHost");
        Directory.CreateDirectory(projDir);
        var csproj = Path.Combine(projDir, "eShop.AppHost.csproj");

        File.WriteAllText(csproj, """
            <Project Sdk="Aspire.AppHost.Sdk/13.6.0">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var files = Directory.GetFiles(projDir);
        var props = parser.ExtractManifestProperties(projDir, files);

        props["is_library"].Should().Be("true");
        props["is_orchestrator"].Should().Be("true");
        props["manifest_type"].Should().Be("library");
        props["role"].Should().Be("Library");
        props["kind"].Should().Be("Library");

        var context = new ProjectContext(projDir, "src/eShop.AppHost", "eShop.AppHost", "csharp", files, [], props);
        var classification = parser.ClassifyProject(context);

        classification.Should().Be(ProjectClassification.Library);
        classification.Kind.Should().Be(ProjectEntityKind.Library);
    }

    [Test]
    public async Task BuiltInGateCatalog_ContainsSigOB001_AndCatchesMisclassifications()
    {
        var gate = BuiltInGateCatalog.GetDefaultGates().FirstOrDefault(g => g.Id == "SIG-O-B-001");
        gate.Should().NotBeNull();
        gate!.Name.Should().Be("Semantic Runtime Entity & View Kind Integrity");

        using var db = new SqliteGraphClient(":memory:");

        // 1. Insert a clean graph with valid workloads
        await db.UploadNodesAsync([
            new("ws:app:clientapp", "App", new() { ["name"] = "clientapp", ["kind"] = "App", ["is_test_project"] = "false", ["role"] = "FrontendApp" }),
            new("ws:s:basket.api", "Service", new() { ["name"] = "basket.api", ["kind"] = "Service", ["is_test_project"] = "false", ["role"] = "Service" }),
            new("ws:w:orderprocessor", "Worker", new() { ["name"] = "orderprocessor", ["kind"] = "Worker", ["is_test_project"] = "false", ["role"] = "Worker" }),
            new("ws:suite:basket.unittests", "TestSuite", new() { ["name"] = "basket.unittests", ["kind"] = "TestSuite", ["is_test_project"] = "true", ["role"] = "Test" })
        ]);

        var gateRepo = new FileGateRepository(_tempDir);
        var engine = new GateExecutionEngine(db, gateRepo);
        var resultValid = await engine.CheckGateAsync(gate);
        resultValid.Violations.Should().BeEmpty();

        // 2. Insert a misclassified test project as :Service
        await db.UploadNodesAsync([
            new("ws:s:application.unittests", "Service", new() { ["name"] = "application.unittests", ["kind"] = "Service", ["is_test_project"] = "true", ["role"] = "Test" }),
            new("ws:app:clientapp.unittests", "App", new() { ["name"] = "clientapp.unittests", ["kind"] = "App", ["is_test_project"] = "true", ["role"] = "Test" }),
            new("ws:s:mislabelled", "Service", new() { ["name"] = "mislabelled", ["kind"] = "App", ["is_test_project"] = "false", ["role"] = "Service" })
        ]);

        var resultInvalid = await engine.CheckGateAsync(gate);
        resultInvalid.Violations.Count.Should().Be(3);
        resultInvalid.Violations.Any(v => v.Symbol == "application.unittests").Should().BeTrue();
        resultInvalid.Violations.Any(v => v.Symbol == "clientapp.unittests").Should().BeTrue();
        resultInvalid.Violations.Any(v => v.Symbol == "mislabelled").Should().BeTrue();
    }

    [Test]
    public async Task GetDomainArchitectureAsync_PreservesEntityKindsWithoutCustomRemapping()
    {
        using var db = new SqliteGraphClient(":memory:");

        await db.UploadNodesAsync([
            new("ws:app:clientapp", "App", new() { ["name"] = "clientapp", ["path"] = "src/ClientApp", ["kind"] = "App", ["role"] = "FrontendApp", ["is_library"] = "false" }),
            new("ws:app:hybridapp", "App", new() { ["name"] = "hybridapp", ["path"] = "src/HybridApp", ["kind"] = "App", ["role"] = "FrontendApp", ["is_library"] = "false" }),
            new("ws:s:basket.api", "Service", new() { ["name"] = "basket.api", ["path"] = "src/Basket.API", ["kind"] = "Service", ["role"] = "Service", ["is_library"] = "false" }),
            new("ws:w:orderprocessor", "Worker", new() { ["name"] = "orderprocessor", ["path"] = "src/OrderProcessor", ["kind"] = "Worker", ["role"] = "Worker", ["is_library"] = "false" }),
            new("ws:suite:basket.unittests", "TestSuite", new() { ["name"] = "basket.unittests", ["path"] = "tests/Basket.UnitTests", ["kind"] = "TestSuite", ["role"] = "Test", ["is_test_project"] = "true" })
        ]);

        // Add a call from hybridapp to basket.api
        await db.UploadRelationshipsAsync([
            new("ws:app:hybridapp", "ws:s:basket.api", "SERVICE_CALL", new() { ["kind"] = "SERVICE_CALL", ["dependency_type"] = "service_call" })
        ]);

        var engine = new ArchitectureViewEngine(db);
        var domainArch = await engine.GetDomainArchitectureAsync();

        var apps = domainArch.Nodes.Where(n => n.Kind == NodeKind.App).ToList();
        var services = domainArch.Nodes.Where(n => n.Kind == NodeKind.Service).ToList();
        var workers = domainArch.Nodes.Where(n => n.Kind == NodeKind.Worker).ToList();
        var testSuites = domainArch.Nodes.Where(n => n.Kind == NodeKind.TestSuite).ToList();

        // Both apps must be present as App, never remapped to Service
        apps.Count.Should().Be(2);
        apps.Any(a => a.Name == "clientapp").Should().BeTrue();
        apps.Any(a => a.Name == "hybridapp").Should().BeTrue();

        services.Count.Should().Be(1);
        services[0].Name.Should().Be("basket.api");

        workers.Count.Should().Be(1);
        workers[0].Name.Should().Be("orderprocessor");

        // TestSuites must NOT appear on Domain Map
        testSuites.Should().BeEmpty();

        domainArch.Stats.Ingress.Should().Be(2);
        domainArch.Stats.Services.Should().Be(1);
        domainArch.Stats.Workers.Should().Be(1);
    }
}
