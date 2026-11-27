using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Storage;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class SemanticLayerOntologyTests
{
    private string _tempWorkspace = null!;

    [SetUp]
    public void SetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), $"ce_l4_ontology_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempWorkspace);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, recursive: true); } catch { /* ignore */ }
        }
    }

    private async Task<(SqliteGraphClient Client, FileGateRepository Repo, GateExecutionEngine Engine)> IndexAndActivateStarterGatesAsync(params string[] gatePrefixes)
    {
        var ws = WorkspaceLocator.Initialize(_tempWorkspace, "SemanticL4App");
        var client = new SqliteGraphClient(ws.DbPath);
        await client.CreateIndicesAsync();
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, clear: true, enableIntentAnalysis: false);

        var repo = new FileGateRepository(_tempWorkspace);
        await repo.InitStarterGatesAsync(overwrite: true);
        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        foreach (var exFile in Directory.GetFiles(gatesDir, "*.yaml.example"))
        {
            var fn = Path.GetFileName(exFile);
            if (gatePrefixes.Length == 0 || gatePrefixes.Any(p => fn.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                File.Move(exFile, exFile[..^".example".Length], overwrite: true);
            }
        }

        return (client, repo, new GateExecutionEngine(client, repo));
    }

    [Test]
    public async Task Phase1_CacheOntology_SeparatesReadThroughCacheFromTableWrites_AndDetectsMissingInvalidation()
    {
        var svcDir = Path.Combine(_tempWorkspace, "wallet-svc");
        Directory.CreateDirectory(svcDir);
        await File.WriteAllTextAsync(Path.Combine(svcDir, "WalletSvc.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MediatR" Version="12.2.0" />
                <PackageReference Include="Dapper" Version="2.1.35" />
                <PackageReference Include="Microsoft.Extensions.Caching.Abstractions" Version="9.0.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(Path.Combine(svcDir, "WalletHandlers.cs"),
            """
            using System.Data;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapper;
            using MediatR;
            using Microsoft.Extensions.Caching.Distributed;

            public record GetWalletQuery(string Id) : IRequest<string>;
            public record CreditWalletCommand(string Id, decimal Amount) : IRequest;
            public record ResetWalletCommand(string Id) : IRequest;

            public class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, string>
            {
                private readonly IDbConnection _db;
                private readonly IDistributedCache _cache;
                public GetWalletQueryHandler(IDbConnection db, IDistributedCache cache) { _db = db; _cache = cache; }

                public async Task<string> Handle(GetWalletQuery query, CancellationToken ct)
                {
                    var cached = await _cache.GetStringAsync("wallet:balance");
                    if (cached != null) return cached;
                    var row = await _db.QuerySingleAsync<string>("SELECT Balance FROM Wallets WHERE Id = @Id", new { query.Id });
                    await _cache.SetStringAsync("wallet:balance", row);
                    return row;
                }
            }

            public class CreditWalletCommandHandler : IRequestHandler<CreditWalletCommand>
            {
                private readonly IDbConnection _db;
                public CreditWalletCommandHandler(IDbConnection db) => _db = db;

                public async Task Handle(CreditWalletCommand cmd, CancellationToken ct)
                {
                    await _db.ExecuteAsync("UPDATE Wallets SET Balance = Balance + @Amount WHERE Id = @Id", cmd);
                }
            }

            public class ResetWalletCommandHandler : IRequestHandler<ResetWalletCommand>
            {
                private readonly IDbConnection _db;
                private readonly IDistributedCache _cache;
                public ResetWalletCommandHandler(IDbConnection db, IDistributedCache cache) { _db = db; _cache = cache; }

                public async Task Handle(ResetWalletCommand cmd, CancellationToken ct)
                {
                    await _db.ExecuteAsync("UPDATE Wallets SET Balance = 0 WHERE Id = @Id", cmd);
                    await _cache.RemoveAsync("wallet:balance");
                }
            }
            """);

        var (client, repo, engine) = await IndexAndActivateStarterGatesAsync("cqrs-", "cache-");
        using (client)
        {
            var cacheRes = await client.ExecuteCypherAsync(
                "MATCH (fn:Function)-[r:READS_CACHE|WRITES_CACHE|INVALIDATES_CACHE]->(c:Cache) RETURN type(r) AS rel, c.name AS region, c.sub_kind AS engine", null);
            cacheRes.Rows.Select(r => r.Values[0].AsString()).Should().Contain(new[] { "READS_CACHE", "WRITES_CACHE", "INVALIDATES_CACHE" });
            cacheRes.Rows.Select(r => r.Values[1].AsString()).Should().Contain("wallet");

            // Read-through cache population inside QueryHandler must NOT trigger SIG-CQRS-001
            var cqrs1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-CQRS-001"))!);
            cqrs1.Violations.Should().BeEmpty("WRITES_CACHE is distinct from WRITES_TO on durable tables");

            // CreditWalletCommandHandler mutates Wallets without invalidating 'wallet' cache, while ResetWalletCommandHandler invalidates it
            var sigCache = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-CACHE-001"))!);
            sigCache.ErrorMessage.Should().BeNull();
            sigCache.Violations.Should().ContainSingle(v => (v.Message ?? "").Contains("CreditWalletCommand"));
        }
    }

    [Test]
    public async Task Phase2And4_TransactionsAndJobs_DetectExternalHttpInTx_MultiWriteWithoutTx_AndCronMutations()
    {
        // Java service with @Transactional calling external HTTP + @Scheduled cron job writing to DB
        var javaDir = Path.Combine(_tempWorkspace, "payment-svc");
        Directory.CreateDirectory(Path.Combine(javaDir, "src", "main", "java", "com", "pay"));
        await File.WriteAllTextAsync(Path.Combine(javaDir, "pom.xml"),
            """
            <project>
              <modelVersion>4.0.0</modelVersion>
              <groupId>com.pay</groupId><artifactId>payment-svc</artifactId><version>1.0.0</version>
              <dependencies>
                <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-web</artifactId><version>3.2.0</version></dependency>
                <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-data-jpa</artifactId><version>3.2.0</version></dependency>
              </dependencies>
            </project>
            """);

        await File.WriteAllTextAsync(Path.Combine(javaDir, "src", "main", "java", "com", "pay", "PaymentService.java"),
            """
            package com.pay;
            import jakarta.persistence.*;
            import org.springframework.transaction.annotation.Transactional;
            import org.springframework.scheduling.annotation.Scheduled;
            import org.springframework.web.client.RestTemplate;
            import org.springframework.jdbc.core.JdbcTemplate;

            @Entity @Table(name = "settlements")
            class SettlementEntity { @Id public String id; }

            public class PaymentService {
                private final RestTemplate restTemplate = new RestTemplate();
                private final JdbcTemplate jdbcTemplate;
                public PaymentService(JdbcTemplate jdbc) { this.jdbcTemplate = jdbc; }

                @Transactional
                public void capturePayment(String orderId) {
                    jdbcTemplate.update("UPDATE payments SET status = 'CAPTURING' WHERE order_id = ?", orderId);
                    callStripeGateway(orderId);
                }

                private void callStripeGateway(String orderId) {
                    restTemplate.postForObject("https://api.stripe.com/v1/charges", orderId, String.class);
                }

                @Scheduled(cron = "0 0 * * * *")
                public void reconcileNightly() {
                    jdbcTemplate.update("UPDATE settlements SET closed = 1 WHERE closed = 0");
                }
            }
            """);

        // C# CommandHandler writing to 2 tables without a transaction
        var csDir = Path.Combine(_tempWorkspace, "order-svc");
        Directory.CreateDirectory(csDir);
        await File.WriteAllTextAsync(Path.Combine(csDir, "OrderSvc.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MediatR" Version="12.2.0" />
                <PackageReference Include="Dapper" Version="2.1.35" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(csDir, "SplitOrderCommandHandler.cs"),
            """
            using System.Data;
            using System.Threading;
            using System.Threading.Tasks;
            using Dapper;
            using MediatR;

            public record SplitOrderCommand(string Id) : IRequest;

            public class SplitOrderCommandHandler : IRequestHandler<SplitOrderCommand>
            {
                private readonly IDbConnection _db;
                public SplitOrderCommandHandler(IDbConnection db) => _db = db;

                public async Task Handle(SplitOrderCommand cmd, CancellationToken ct)
                {
                    await _db.ExecuteAsync("INSERT INTO Orders (Id) VALUES (@Id)", cmd);
                    await _db.ExecuteAsync("INSERT INTO Shipments (OrderId) VALUES (@Id)", cmd);
                }
            }
            """);

        var (client, repo, engine) = await IndexAndActivateStarterGatesAsync("tx-", "job-");
        using (client)
        {
            var tx1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-TX-001"))!);
            tx1.Violations.Should().NotBeEmpty("outbound Stripe HTTP call inside @Transactional must be flagged by SIG-TX-001");

            var tx2 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-TX-002"))!);
            tx2.Violations.Should().ContainSingle(v => (v.Message ?? "").Contains("SplitOrderCommand"));

            var jobNodes = await client.ExecuteCypherAsync(
                "MATCH (j:Job)-[r:TRIGGERS]->(fn:Function) RETURN j.name, j.sub_kind, fn.name", null);
            jobNodes.Rows.Should().ContainSingle(r => r.Values[2].AsString().Contains("reconcileNightly") && r.Values[1].AsString() == "cron");

            var job1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-JOB-001"))!);
            job1.Violations.Should().ContainSingle(v => (v.Target ?? "").Contains("settlements"));
        }
    }

    [Test]
    public async Task Phase3And5_PoliciesAndTableOwnership_DetectUnguardedMutations_AndCrossContextTableViolations()
    {
        // Service 1: BillingService owning 'invoices' table via InvoiceEntity, with guarded, public, and unguarded endpoints
        var billingDir = Path.Combine(_tempWorkspace, "billing-service");
        Directory.CreateDirectory(Path.Combine(billingDir, "src"));
        await File.WriteAllTextAsync(Path.Combine(billingDir, "package.json"),
            """{ "name": "billing-service", "dependencies": { "@nestjs/common": "^10.0.0", "typeorm": "^0.3.20" } }""");

        await File.WriteAllTextAsync(Path.Combine(billingDir, "src", "invoice.entity.ts"),
            """
            import { Entity, PrimaryGeneratedColumn, Column } from 'typeorm';
            @Entity('invoices')
            export class InvoiceEntity {
                @PrimaryGeneratedColumn() id: number;
                @Column() status: string;
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(billingDir, "src", "jwt-auth.guard.ts"),
            """
            import { Injectable, CanActivate, ExecutionContext } from '@nestjs/common';
            @Injectable()
            export class JwtAuthGuard implements CanActivate {
                canActivate(context: ExecutionContext): boolean { return true; }
            }
            """);

        await File.WriteAllTextAsync(Path.Combine(billingDir, "src", "invoice.controller.ts"),
            """
            import { Controller, Post, Delete, UseGuards, SetMetadata } from '@nestjs/common';
            import { Repository } from 'typeorm';
            import { InvoiceEntity } from './invoice.entity';
            import { JwtAuthGuard } from './jwt-auth.guard';

            export const Public = () => SetMetadata('isPublic', true);

            @Controller('invoices')
            export class InvoiceController {
                constructor(private readonly invoiceRepository: Repository<InvoiceEntity>) {}

                @UseGuards(JwtAuthGuard)
                @Post()
                async createInvoice() {
                    return this.invoiceRepository.save({ status: 'OPEN' });
                }

                @Public()
                @Post('webhook')
                async receiveWebhook() {
                    return this.invoiceRepository.update(1, { status: 'PAID' });
                }

                // Unguarded mutating endpoint -> must trigger SIG-SEC-001
                @Delete(':id')
                async deleteInvoice() {
                    return this.invoiceRepository.delete(1);
                }
            }
            """);

        // Service 2: ShippingService (distinct BoundedContext) directly reading & writing Billing's 'invoices' table
        var shippingDir = Path.Combine(_tempWorkspace, "shipping-service");
        Directory.CreateDirectory(shippingDir);
        await File.WriteAllTextAsync(Path.Combine(shippingDir, "ShippingService.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Dapper" Version="2.1.35" /></ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(shippingDir, "ShipmentSync.cs"),
            """
            using System.Data;
            using System.Threading.Tasks;
            using Dapper;
            using Microsoft.AspNetCore.Authorization;
            using Microsoft.AspNetCore.Mvc;

            [Authorize]
            [ApiController]
            [Route("api/shipments")]
            public class ShipmentController : ControllerBase
            {
                private readonly IDbConnection _db;
                public ShipmentController(IDbConnection db) => _db = db;

                [HttpPost]
                public async Task<IActionResult> SyncFromInvoices()
                {
                    await _db.QueryAsync("SELECT id, status FROM invoices");
                    await _db.ExecuteAsync("UPDATE invoices SET status = 'SHIPPED'");
                    return Ok();
                }
            }
            """);

        var (client, repo, engine) = await IndexAndActivateStarterGatesAsync("sec-", "ddd-");
        using (client)
        {
            var polRes = await client.ExecuteCypherAsync(
                "MATCH (ep:Endpoint)-[:GUARDED_BY]->(p:Policy) RETURN p.name AS name, p.sub_kind AS kind", null);
            polRes.Rows.Select(r => r.Values[0].AsString()).Should().Contain(new[] { "JwtAuthGuard", "Anonymous", "Authenticated" });

            var enfRes = await client.ExecuteCypherAsync(
                "MATCH (p:Policy {name: 'JwtAuthGuard'})-[:ENFORCED_BY]->(fn:Function) RETURN fn.name AS fn", null);
            enfRes.Rows.Should().NotBeEmpty("JwtAuthGuard policy must link via ENFORCED_BY to canActivate");

            var ownsRes = await client.ExecuteCypherAsync(
                "MATCH (m:Service)-[:OWNS]->(t:Table {name: 'invoices'})-[:IMPLEMENTED_BY]->(ent:Type) RETURN m.name AS owner, ent.name AS entity", null);
            ownsRes.Rows.Should().ContainSingle();
            ownsRes.Rows[0].Values[1].AsString().Should().Be("InvoiceEntity");

            // Only DELETE /invoices/:id is unguarded; POST /invoices (JwtAuthGuard), POST /invoices/webhook (@Public), and POST /api/shipments ([Authorize]) are guarded
            var sec1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-SEC-001"))!);
            sec1.Violations.Should().ContainSingle(v => (v.Target ?? "").Contains("DELETE"));

            // Both Billing and Shipping Services write to 'invoices' -> SIG-DDD-001
            var ddd1 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-DDD-001"))!);
            ddd1.Violations.Should().Contain(v => (v.Target ?? "").Contains("invoices"));

            // ShippingService directly accesses 'invoices' owned by BillingService -> SIG-DDD-002
            var ddd2 = await engine.CheckGateAsync((await repo.GetGateByIdAsync("SIG-DDD-002"))!);
            ddd2.Violations.Should().ContainSingle(v => (v.Target ?? "").Contains("invoices") && (v.Message ?? "").Contains("shipping-service"));
        }
    }
}
