using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Core.Mcp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ContextPackBuilderTests
{
    private const string SampleSymptomQuery = "when an API key is revoked, clean up usage limits";
    private const string GateManifestYaml = """
        id: SIG-CTX-001
        category: Architectural
        platform: agnostic
        scope: Module
        dimension: Boundaries
        severity: Error
        name: No Direct Controller DB Writes
        description: Controllers must not write tables directly.
        remediation: Delegate table mutations to the owning domain Service.
        query: "MATCH (c:Type)-[:HAS_METHOD]->(fn:Function)-[:WRITES_TO]->(t:Table) WHERE c.name ENDS WITH 'Controller' RETURN coalesce(fn.file_path, c.file_path) AS file, coalesce(fn.start_line, 1) AS line, c.name + '.' + fn.name AS symbol, t.name AS target, 'Direct DB write in controller' AS message"
        """;

    [Test]
    public void ExtractSearchConcepts_SynthesizesCamelCaseCompoundsAndFiltersStopWords()
    {
        var (concepts, tokens) = McpContextPackBuilder.ExtractSearchConcepts(SampleSymptomQuery);

        tokens.Should().Contain("api");
        tokens.Should().Contain("key");
        tokens.Should().Contain("revoked");
        tokens.Should().Contain("usage");
        tokens.Should().Contain("limit");
        tokens.Should().NotContain("when");

        concepts.Should().Contain("apiKey");
        concepts.Should().Contain("usageLimit");
    }

    [Test]
    public async Task GetTaskContextAsync_ReturnsAnchorsCanonicalOwnerSideEffectsAndRatchetedLegacyTrap()
    {
        var tempWs = Path.Combine(Path.GetTempPath(), "ce_ctx_pack_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempWs);
        try
        {
            var srcDir = Path.Combine(tempWs, "src");
            var authDir = Path.Combine(srcDir, "auth");
            var limitsDir = Path.Combine(srcDir, "usage-limit");
            Directory.CreateDirectory(authDir);
            Directory.CreateDirectory(limitsDir);

            await File.WriteAllTextAsync(Path.Combine(tempWs, "package.json"), """{"name":"ctx-demo","dependencies":{"@nestjs/common":"^10.0.0","typeorm":"^0.3.0"}}""");
            await File.WriteAllTextAsync(Path.Combine(limitsDir, "usage-limit.entity.ts"), """
                import { Entity, PrimaryGeneratedColumn, Column } from 'typeorm';
                @Entity('usageLimit')
                export class UsageLimitEntity {
                    @PrimaryGeneratedColumn('uuid') id: string;
                    @Column() spenderId: string;
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(limitsDir, "cache.service.ts"), """
                import { Injectable } from '@nestjs/common';
                @Injectable()
                export class WorkspaceCacheService {
                    async invalidateUsageCache(workspaceId: string): Promise<void> {}
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(limitsDir, "usage-limit.service.ts"), """
                import { Injectable } from '@nestjs/common';
                import { InjectRepository } from '@nestjs/typeorm';
                import { Repository } from 'typeorm';
                import { UsageLimitEntity } from './usage-limit.entity';
                import { WorkspaceCacheService } from './cache.service';

                @Injectable()
                export class UsageLimitService {
                    constructor(
                        @InjectRepository(UsageLimitEntity)
                        private readonly usageLimitRepo: Repository<UsageLimitEntity>,
                        private readonly cacheService: WorkspaceCacheService,
                    ) {}

                    async deleteLimit(id: string, workspaceId: string): Promise<void> {
                        await this.usageLimitRepo.delete({ id });
                        await this.cacheService.invalidateUsageCache(workspaceId);
                    }

                    async findLimits(spenderId: string): Promise<UsageLimitEntity[]> {
                        return this.usageLimitRepo.find({ where: { spenderId } });
                    }
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(authDir, "api-key.controller.ts"), """
                import { Controller, Post, Delete } from '@nestjs/common';
                import { InjectRepository } from '@nestjs/typeorm';
                import { Repository } from 'typeorm';
                import { UsageLimitEntity } from '../usage-limit/usage-limit.entity';

                @Controller('api-keys')
                export class ApiKeyController {
                    constructor(
                        @InjectRepository(UsageLimitEntity)
                        private readonly legacyLimitRepo: Repository<UsageLimitEntity>,
                    ) {}

                    @Post('legacy-seed')
                    async legacySeed(): Promise<void> {
                        await this.legacyLimitRepo.save({ spenderId: 'seed' });
                    }

                    @Delete('revoke')
                    async revokeApiKey(id: string): Promise<void> {}
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(limitsDir, "usage-limit.module.ts"), """
                import { Module } from '@nestjs/common';
                import { UsageLimitService } from './usage-limit.service';
                import { WorkspaceCacheService } from './cache.service';

                @Module({
                    providers: [UsageLimitService, WorkspaceCacheService],
                    exports: [UsageLimitService],
                })
                export class UsageLimitModule {}
                """);
            await File.WriteAllTextAsync(Path.Combine(authDir, "api-key.module.ts"), """
                import { Module } from '@nestjs/common';
                import { ApiKeyController } from './api-key.controller';

                @Module({
                    controllers: [ApiKeyController],
                })
                export class ApiKeyModule {}
                """);

            WorkspaceLocator.Initialize(tempWs, "ctx-demo");
            var gatesDir = Path.Combine(tempWs, ".codeexplorer", "gates");
            Directory.CreateDirectory(gatesDir);
            await File.WriteAllTextAsync(Path.Combine(gatesDir, "sig-ctx-001.yaml"), GateManifestYaml);

            var dbPath = Path.Combine(tempWs, ".codeexplorer", "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            WorkspaceIndexer.AutoRegisterParsers();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempWs, tempWs, clear: true);

            var gateRepo = new FileGateRepository(tempWs);
            var engine = new GateExecutionEngine(client, gateRepo);
            var preSuite = await engine.CheckSuiteAsync();
            foreach (var r in preSuite.Results)
            {
                var baseline = RatchetService.CreateUpdatedBaseline(r.Gate, r.Violations, null);
                await gateRepo.SaveBaselineAsync(baseline, r.Gate);
            }

            var repository = new CodeExplorerRepository(client, defaultWorkspacePath: tempWs);
            var yaml = await repository.GetTaskContextAsync("revoke api key usage limit", maxAnchors: 8, includeGates: true, workspacePath: tempWs);

            TestContext.Out.WriteLine(yaml);
            yaml.Should().Contain("ApiKeyController");
            yaml.Should().Contain("UsageLimitService");
            yaml.Should().Contain("invalidateUsageCache");
            yaml.Should().Contain("canonicalOwner: UsageLimitService");
            yaml.Should().Contain("ApiKeyModule");
            yaml.Should().Contain("UsageLimitModule");
            yaml.Should().Contain("SAFE_DIRECT_IMPORT");
            yaml.Should().Contain("SIG-CTX-001");
            yaml.Should().Contain("TOLERATED LEGACY DEBT");
        }
        finally
        {
            try
            {
                Directory.Delete(tempWs, recursive: true);
            }
            catch (IOException)
            {
                // Ignore transient Windows SQLite lock cleanup race in temp folder
            }
        }
    }
}
