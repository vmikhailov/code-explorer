using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
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
