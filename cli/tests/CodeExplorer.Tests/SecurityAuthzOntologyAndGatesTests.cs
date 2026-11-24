using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Core.Gates.Storage;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SecurityAuthzOntologyAndGatesTests
{
    [Test]
    public async Task AuthzOntologyAndSecurityGates_ClassifiesSensitivityTenantScopeAndDetectsViolations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ce_sec_authz_{Guid.NewGuid():N}");
        var tempDb = Path.Combine(Path.GetTempPath(), $"ce_sec_authz_{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "SecApi.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
                  </ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "Models.cs"), """
                using System.ComponentModel.DataAnnotations;
                using System.ComponentModel.DataAnnotations.Schema;
                using Microsoft.EntityFrameworkCore;

                namespace SecApi;

                [Table("customers")]
                public class Customer
                {
                    [Key] public int Id { get; set; }
                    public string Email { get; set; } = "";
                }

                [Table("payment_cards")]
                public class PaymentCard
                {
                    [Key] public int Id { get; set; }
                    public string CardNumber { get; set; } = "";
                }

                [Table("api_keys")]
                public class ApiKey
                {
                    [Key] public int Id { get; set; }
                    public string SecretHash { get; set; } = "";
                }

                [Table("audit_events")]
                public class AuditEvent
                {
                    [Key] public int Id { get; set; }
                    public string Message { get; set; } = "";
                }

                [Table("cluster_members")]
                public class ClusterMember
                {
                    [Key] public int Id { get; set; }
                    public int UserId { get; set; }
                    public string NodeAddress { get; set; } = "";
                }

                public class AppDbContext : DbContext
                {
                    public DbSet<Customer> Customers => Set<Customer>();
                    public DbSet<PaymentCard> PaymentCards => Set<PaymentCard>();
                    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
                    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
                    public DbSet<ClusterMember> ClusterMembers => Set<ClusterMember>();
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "Controllers.cs"), """
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                namespace SecApi;

                public class TenantOwnerGuard
                {
                    public bool HandleRequirementAsync()
                    {
                        try
                        {
                            return true;
                        }
                        catch (Exception)
                        {
                        }
                        return false;
                    }
                }

                [ApiController]
                public class BillingController : ControllerBase
                {
                    private readonly AppDbContext _db;
                    public BillingController(AppDbContext db) => _db = db;

                    [HttpGet("/api/public/customers")]
                    [AllowAnonymous]
                    public object GetPublicCustomers() => _db.Customers.ToList();

                    [HttpPost("/api/admin/billing")]
                    [Authorize]
                    public void UpdateBilling()
                    {
                        _db.PaymentCards.Add(new PaymentCard());
                    }

                    [HttpPost("/api/public/audit")]
                    [AllowAnonymous]
                    public void WritePublicAudit()
                    {
                        _db.AuditEvents.Add(new AuditEvent());
                    }

                    [HttpGet("/api/tenant/orders")]
                    [Authorize(Policy = "TenantOwnerGuard")]
                    public object GetTenantOrders() => _db.Customers.ToList();
                }
                """);

            await using var db = new SqliteGraphClient(tempDb);
            var indexer = new WorkspaceIndexer(db);
            await indexer.IndexAsync(tempDir, clear: true);

            // 1. Verify DataSensitivity classification on TableNodes
            var sensitivities = (await db.QueryAsync<(string Name, string? Sensitivity)>(
                "MATCH (t:Table) RETURN t.name AS name, t.sensitivity AS sensitivity"))
                .ToDictionary(r => r.Name, r => r.Sensitivity, StringComparer.OrdinalIgnoreCase);

            sensitivities["customers"].Should().Be("pii");
            sensitivities["payment_cards"].Should().Be("pci");
            sensitivities["api_keys"].Should().Be("secret");
            sensitivities.GetValueOrDefault("audit_events").Should().BeNullOrEmpty();
            sensitivities.GetValueOrDefault("cluster_members").Should().BeNullOrEmpty();

            // 2. Verify SIG-SEC-002 detects anonymous access to PII table (customers)
            var sec002 = await db.QueryAsync<(string Ep, string Tbl)>(
                """
                MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
                      -[:CALLS*0..4]->(acc:Function)-[:READS_FROM|WRITES_TO]->(tbl:Table)
                WHERE tbl.sensitivity IN ['pii', 'pci', 'secret']
                  AND NOT fn.is_test AND NOT acc.is_test
                  AND (NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))
                       OR EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'anonymous'})))
                RETURN DISTINCT ep.route_template AS ep, tbl.name AS tbl
                """);
            sec002.Should().Contain(r => r.Ep.Contains("/api/public/customers") && r.Tbl.Equals("customers", StringComparison.OrdinalIgnoreCase));
            sec002.Should().NotContain(r => r.Ep.Contains("/api/tenant/orders"));

            // 4. Verify SIG-SEC-003 detects privileged /api/admin/billing lacking role/permission/custom_guard
            var sec003 = await db.QueryAsync<string>(
                """
                MATCH (ep:Endpoint)
                WHERE (toLower(ep.route_template) CONTAINS '/admin'
                    OR toLower(ep.route_template) CONTAINS '/billing')
                  AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'role'}))
                  AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'permission'}))
                  AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'custom_guard'}))
                RETURN DISTINCT ep.route_template AS ep
                """);
            sec003.Should().Contain(r => r.Contains("/api/admin/billing"));

            // 5. Verify SIG-SEC-004 detects explicit anonymous DB write (/api/public/audit -> audit_events)
            var sec004 = await db.QueryAsync<(string Ep, string Tbl)>(
                """
                MATCH (ep:Endpoint)-[:GUARDED_BY]->(pol:Policy {sub_kind: 'anonymous'}),
                      (ep)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)-[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
                WHERE NOT fn.is_test AND NOT w.is_test
                RETURN DISTINCT ep.route_template AS ep, tbl.name AS tbl
                """);
            sec004.Should().Contain(r => r.Ep.Contains("/api/public/audit") &&
                (r.Tbl.Equals("audit_events", StringComparison.OrdinalIgnoreCase) || r.Tbl.Equals("AuditEvents", StringComparison.OrdinalIgnoreCase)));

            // 6. Verify SIG-SEC-005 detects empty catch inside auth guard enforcing policy
            var sec005 = await db.QueryAsync<(string Pol, string Fn)>(
                """
                MATCH (pol:Policy)-[:ENFORCED_BY]->(fn:Function)-[:DECLARES|CONTAINS*1..4]->(c:Scope)
                WHERE coalesce(c.kind, 'catch') = 'catch' AND c.is_empty = true AND NOT fn.is_test
                RETURN DISTINCT pol.name AS pol, fn.name AS fn
                """);
            sec005.Should().Contain(r => r.Pol == "TenantOwnerGuard" && r.Fn.Contains("HandleRequirementAsync"));

            // 7. Verify all 5 security starter templates are registered
            var templateNames = StarterGateTemplates.All.Select(t => t.FileName).ToList();
            templateNames.Should().Contain("sec-unguarded-state-mutation.yaml.example");
            templateNames.Should().Contain("sec-unprotected-pii-table-access.yaml.example");
            templateNames.Should().Contain("sec-privileged-route-requires-role.yaml.example");
            templateNames.Should().Contain("sec-anonymous-db-write.yaml.example");
            templateNames.Should().Contain("sec-auth-guard-empty-catch.yaml.example");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
