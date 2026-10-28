using CodeExplorer.Core.Protocol;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class DomainMapServiceIdentityTests
{
    private IDisposable? _lock;

    [SetUp]
    public async Task SetUp()
    {
        _lock = await TestRegistryLock.AcquireAsync();
        WorkspaceConventions.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        WorkspaceConventions.DomainDetectionEnabled = false;
        WorkspaceConventions.Clear();
        _lock?.Dispose();
        _lock = null;
    }

    [Test]
    public async Task DetectionOff_EachServiceIsItsOwnNode_WithoutAnyDomainGrouping()
    {
        var arch = await BuildAsync();

        arch.Nodes.Select(n => n.Name).Should().BeEquivalentTo("domain-settler", "rates", "payouts");
        arch.Nodes.Should().OnlyContain(n => n.Domain == null && n.DomainDisplayName == null);
        arch.Nodes.Should().OnlyContain(n => !n.DisplayName.Contains('&'));
    }

    [Test]
    public async Task DetectionOn_ServicesKeepTheirOwnNames_AndCarryDomainAsAttribute()
    {
        WorkspaceConventions.DomainDetectionEnabled = true;

        var arch = await BuildAsync();

        arch.Nodes.Select(n => n.Name).Should().BeEquivalentTo("domain-settler", "rates", "payouts");
        arch.Nodes.Should().OnlyContain(n => n.DisplayName != "Billing & Payments",
            "a service must never take the display name of its domain");
        arch.Nodes.Should().OnlyContain(n => n.DomainDisplayName == "Billing & Payments");
        arch.Nodes.Select(n => n.Domain).Distinct().Should().ContainSingle();
    }

    [Test]
    public async Task DetectionOff_ExplicitDomainsJson_AssignsListedServicesOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "ce_domain_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".codeexplorer"));
        try
        {
            File.WriteAllText(Path.Combine(root, ".codeexplorer", "domains.json"),
                """{"domains":[{"name":"RatesAndPricing","services":["rates","payouts"]}]}""");
            WorkspaceConventions.LoadFromWorkspace(root);

            var arch = await BuildAsync();

            arch.Nodes.Select(n => n.Name).Should().BeEquivalentTo("domain-settler", "rates", "payouts");
            arch.Nodes.Where(n => n.Domain != null).Select(n => n.Name).Should().BeEquivalentTo("rates", "payouts");
            arch.Nodes.Where(n => n.Domain != null).Should().OnlyContain(n => n.DomainDisplayName == "Rates & Pricing");
            arch.Nodes.Single(n => n.Name == "domain-settler").Domain.Should().BeNull();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { /* best effort */ }
        }
    }

    [Test]
    public void DetectionOff_ProjectInsideListedServiceFolder_InheritsItsDomain()
    {
        var root = Path.Combine(Path.GetTempPath(), "ce_domain_cfg2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".codeexplorer"));
        try
        {
            File.WriteAllText(Path.Combine(root, ".codeexplorer", "domains.json"),
                """{"domains":[{"name":"PartnersAndSources","services":["adhub","fe"]}]}""");
            WorkspaceConventions.LoadFromWorkspace(root);

            WorkspaceConventions.TryGetConfiguredDomain("adhub-cli", "adhub/adhub-cli", out var d1).Should().BeTrue();
            d1.Should().Be("PartnersAndSources");
            WorkspaceConventions.TryGetConfiguredDomain("ui", "fe/projects/ui", out _).Should().BeTrue();
            WorkspaceConventions.TryGetConfiguredDomain("library", "../libs/library", out _).Should().BeFalse();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { /* best effort */ }
        }
    }

    [Test]
    public async Task SharedLibraryInSameDomainAsFrontendApp_NeverRedirectsLibraryDependencyToApp()
    {
        var root = Path.Combine(Path.GetTempPath(), "ce_domain_lib_fold_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".codeexplorer"));
        try
        {
            File.WriteAllText(Path.Combine(root, ".codeexplorer", "domains.json"),
                """{"domains":[{"name":"PlatformAndUI","services":["fe","library"]},{"name":"TrafficAndRouting","services":["bundle-update-adapter"]}]}""");
            WorkspaceConventions.LoadFromWorkspace(root);

            using var db = new SqliteGraphClient(Path.Combine(root, "graph.db").Replace('\\', '/'));
            await db.CreateIndicesAsync();
            var engine = new ArchitectureViewEngine(db);

            await db.UploadNodesAsync(
            [
                new Node("ws:app:fe", "App", new Dictionary<string, object>
                {
                    ["name"] = "fe",
                    ["path"] = "fe",
                    ["role"] = "FrontendApp",
                    ["is_library"] = "false"
                }),
                new Node("ws:s:bundle-update-adapter", "Service", new Dictionary<string, object>
                {
                    ["name"] = "bundle-update-adapter",
                    ["path"] = "bundle-update-adapter",
                    ["role"] = "Service",
                    ["is_library"] = "false"
                }),
                new Node("ws:lib:library", "Library", new Dictionary<string, object>
                {
                    ["name"] = "library",
                    ["path"] = "../libs/library",
                    ["role"] = "SharedLibrary",
                    ["is_library"] = "true"
                })
            ]);

            await db.UploadRelationshipsAsync(
            [
                new Relationship("ws:s:bundle-update-adapter", "ws:lib:library", OntologyConstants.Relationships.DependsOn,
                    new Dictionary<string, object> { ["dependency_type"] = "library", ["category"] = "library" })
            ]);

            // 1. Default (includeLibraries: false): library node is hidden and no bogus library edge points to fe
            var archNoLibs = await engine.GetDomainArchitectureAsync(includeLibraries: false);
            archNoLibs.Nodes.Select(n => n.Name).Should().BeEquivalentTo("fe", "bundle-update-adapter");
            archNoLibs.Edges.Should().NotContain(e =>
                e.Source == "service:bundle-update-adapter" && e.Target == "service:fe",
                "a dependency on a shared library in the same domain as fe must never fold into fe");

            // 2. With includeLibraries: true: library has its own node and the DEPENDS_ON edge targets library:library
            var archWithLibs = await engine.GetDomainArchitectureAsync(includeLibraries: true);
            archWithLibs.Nodes.Select(n => n.Name).Should().BeEquivalentTo("fe", "bundle-update-adapter", "library");
            archWithLibs.Edges.Should().ContainSingle(e =>
                e.Source == "service:bundle-update-adapter" &&
                e.Target == "library:library" &&
                e.Category == "library" &&
                e.Label == "DEPENDS_ON");
            archWithLibs.Edges.Should().NotContain(e =>
                e.Source == "service:bundle-update-adapter" && e.Target == "service:fe");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { /* best effort */ }
        }
    }

    private static async Task<DomainArchitectureDto> BuildAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_domain_identity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            using var db = new SqliteGraphClient(Path.Combine(tempDir, "graph.db").Replace('\\', '/'));
            await db.CreateIndicesAsync();
            var engine = new ArchitectureViewEngine(db);

            await db.UploadNodesAsync(
            [
                Project("domain-settler", "BillingAndPayments"),
                Project("rates", "BillingAndPayments"),
                Project("payouts", "BillingAndPayments"),
                Project("billing-core", "BillingAndPayments", role: "SharedLibrary")
            ]);

            return await engine.GetDomainArchitectureAsync();
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch (IOException) { /* temp cleanup is best effort */ }
        }
    }

    private static Node Project(string name, string domain, string role = "Service") =>
        new($"ws:project:{name}:", "Project", new Dictionary<string, object>
        {
            ["name"] = name,
            ["path"] = $"src/{name}",
            ["role"] = role,
            ["domain"] = domain
        });
}
