using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Conventions.Services;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Core.Parser.Sql.Dialects;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parsers.Sql;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class BlueYonderGraphNoiseRegressionTests
{
    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ce-by-noise-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task MultiManifestMfeRepo_RequirementsTxtDoesNotOverrideTypeScript()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        try
        {
            // Case A: 0 .py files + requirements.txt + package.json + tsconfig.json (e.g. plat-lui-portal-app-gallery-mfe)
            var mfeZeroPy = Path.Combine(tempDir, "plat-lui-portal-app-gallery-mfe");
            Directory.CreateDirectory(Path.Combine(mfeZeroPy, "src"));
            await File.WriteAllTextAsync(Path.Combine(mfeZeroPy, "requirements.txt"), "mkdocs==1.6.0\npre-commit==3.8.0\n");
            await File.WriteAllTextAsync(Path.Combine(mfeZeroPy, "package.json"), """{"name":"app-gallery-mfe","dependencies":{"react":"^18.3.0"}}""");
            await File.WriteAllTextAsync(Path.Combine(mfeZeroPy, "tsconfig.json"), """{"compilerOptions":{"strict":true}}""");
            await File.WriteAllTextAsync(Path.Combine(mfeZeroPy, "src", "App.tsx"), "export const App = () => <div>Gallery</div>;\n");
            await File.WriteAllTextAsync(Path.Combine(mfeZeroPy, "src", "api.ts"), "export const fetchGallery = () => '/api/gallery';\n");

            // Case B: 1 .py helper script vs multiple .ts/.tsx files (e.g. plat-liam-user-machine-mfe)
            var mfeOnePy = Path.Combine(tempDir, "plat-liam-user-machine-mfe");
            Directory.CreateDirectory(Path.Combine(mfeOnePy, "src"));
            Directory.CreateDirectory(Path.Combine(mfeOnePy, "scripts"));
            await File.WriteAllTextAsync(Path.Combine(mfeOnePy, "requirements.txt"), "pyyaml==6.0.1\n");
            await File.WriteAllTextAsync(Path.Combine(mfeOnePy, "package.json"), """{"name":"user-machine-mfe","dependencies":{"react":"^18.3.0"}}""");
            await File.WriteAllTextAsync(Path.Combine(mfeOnePy, "tsconfig.json"), """{"compilerOptions":{"strict":true}}""");
            await File.WriteAllTextAsync(Path.Combine(mfeOnePy, "scripts", "lint_check.py"), "def main():\n    return 0\n");
            for (var i = 0; i < 6; i++)
            {
                await File.WriteAllTextAsync(Path.Combine(mfeOnePy, "src", $"View{i}.tsx"), $"export const View{i} = () => <span>{i}</span>;\n");
            }

            var dbPath = Path.Combine(tempDir, "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var projRes = await client.ExecuteCypherAsync("MATCH (p:Project) RETURN p.name AS name, p.language AS lang");
            var byName = projRes.Rows.ToDictionary(
                r => r["name"].AsString(),
                r => r["lang"].AsString(),
                StringComparer.OrdinalIgnoreCase);

            byName.Should().ContainKey("plat-lui-portal-app-gallery-mfe");
            byName["plat-lui-portal-app-gallery-mfe"].Should().Be("typescript");

            byName.Should().ContainKey("plat-liam-user-machine-mfe");
            byName["plat-liam-user-machine-mfe"].Should().Be("typescript");
        }
        finally
        {
            PhysicalFileStore.ClearWorkspace(tempDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task DatabaseNoise_RejectsConfigStopwordsOidcUrlsAndClassifiesSnowflake()
    {
        // 1. Generic config-key / variable / test placeholder names rejected
        var invalidDbKeys = new[]
        {
            "self", "url", "uri", "src", "string", "driver-class-name",
            "default", "templateconnectionurl", "template-connection-url",
            "project_0", "project_1", "project_2"
        };
        foreach (var key in invalidDbKeys)
        {
            ResourceNamingHelper.IsGenericConfigKey(key).Should().BeTrue($"'{key}' must be rejected as a generic/placeholder DB key");
            if (key == "default")
            {
                ResourceNamingHelper.IsAbstractEngineOnlyDatabase($"PostgreSQL.{key}", key, "relational", null)
                    .Should().BeTrue("'PostgreSQL.default' is purged as an abstract engine-only database");
            }
            else
            {
                PostIndexAnalyzer.IsLikelyDatabaseName($"PostgreSQL.{key}", "PostgreSQL")
                    .Should().BeFalse($"'PostgreSQL.{key}' must not survive database consolidation");
            }
        }

        // 2. OAuth2 / OIDC token URL under 'oauth2' key must NOT match H2 or create a Database node
        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var memRepo = new InMemoryGraphRepository();
        var ctx = new ParsingContext("ws", "c:/tmp/ws", memRepo, channel);
        var container = new SemanticStructureNode("ws:sem", "Semantic", "");
        ctx.SemanticStructure = container;
        var rels = new List<Relationship>();

        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            "otel:extensions:oauth2client:token_url",
            "https://sso.company.org/realms/Stratosphere/protocol/openid-connect/token",
            "local_otel_collector.yaml",
            "ws:file:otel",
            "ws",
            container,
            rels,
            ctx);

        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            "spring.datasource.driver-class-name",
            "org.h2.Driver",
            "application-configuration-test.yml",
            "ws:file:app",
            "ws",
            container,
            rels,
            ctx);

        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            "postgres.url",
            "{{ postgres_conn_url }}",
            "tasks/main.yml",
            "ws:file:ansible",
            "ws",
            container,
            rels,
            ctx);

        ConfigurationParser.InferAndCreateServiceFromConnectionString(
            "postgres.string",
            "string",
            "sphere-admin.yml",
            "ws:file:schema",
            "ws",
            container,
            rels,
            ctx);

        container.Children.OfType<DatabaseNode>().Should().BeEmpty();

        // 3. Snowflake SQL and JDBC connection strings classify as Snowflake (analytics), never PostgreSQL
        var sfQuery = NestedSqlParser.ParseNestedSql(
            "SELECT query_id, warehouse_name FROM snowflake.account_usage.query_history WHERE total_elapsed_time > 1000",
            "ws:q:sf",
            "plan-lde-raq/queries/usage.sql");
        sfQuery.Should().NotBeNull();
        var sfDbs = sfQuery!.Children.OfType<DatabaseNode>().ToList();
        sfDbs.Should().ContainSingle();
        sfDbs[0].Engine.Should().Be("Snowflake");
        sfDbs[0].DbType.Should().Be(DatabaseType.Analytics);

        var (canonName, canonType, _) = PostIndexAnalyzer.CanonicalizeDatabase("snowflake.account_usage", "analytics", "Snowflake", "account_usage");
        canonName.Should().Be("Snowflake.account_usage");
        canonType.Should().Be("analytics");
    }

    [Test]
    public void ExternalServiceNoise_RejectsConfigKeysMemberExpressionsPlaceholdersAndNormalizesColonKey()
    {
        var validator = new NetworkEntityValidator();

        var noisyCandidates = new[]
        {
            "metadataUrl",
            "tokenIssuerPrefix",
            "token-url",
            "token",
            "scope",
            "gravitee-apim-public-base-url",
            "gravitee-apim-internal-base-url",
            "azure-apim-internal-base-url",
            "graviteePriUrl",
            "self.url",
            "self._mcp_url",
            "self._token_url",
            "args.service_oas_spec_doc_url",
            "More info",
            "home",
            "icon",
            "wrong",
            "0.0.1",
            " conda",
            "test.com",
            "your-org.jfrog.io"
        };

        foreach (var candidate in noisyCandidates)
        {
            validator.IsGarbageExternalService(candidate)
                .Should().BeTrue($"'{candidate}' must be rejected as ExternalService noise");
        }

        // Legitimate external domains and service names must still pass
        validator.IsGarbageExternalService("api.stripe.com").Should().BeFalse();
        validator.IsGarbageExternalService("elasticsearch").Should().BeFalse();
        validator.IsGarbageExternalService("payment-service").Should().BeFalse();

        // Config suffix stripping normalizes *-public-base-url / *PriUrl cleanly
        ConfigurationIndex.StripServiceOptionsSuffixes("gravitee-apim-public-base-url").Should().Be("gravitee-apim");
        ConfigurationIndex.StripServiceOptionsSuffixes("azure-apim-internal-base-url").Should().Be("azure-apim");
        ConfigurationIndex.StripServiceOptionsSuffixes("graviteePriUrl").Should().Be("gravitee");
    }

    [Test]
    public async Task TableNoise_RejectsRichCliTablesSqlAlchemyFallbackInvalidTokensAndSplitsPublicPrefix()
    {
        // 1. Identifier validation rejects observed noise tokens
        var badTableNames = new[]
        {
            "Group Name",
            "Nodepool Name",
            "SQLAlchemy: Table",
            "PYTHONPATH\" =",
            "for",
            "com",
            "Old",
            "*_PLAN_LDE_DB\".\"<uuid>-MODEL\".\"* }++SOURCE_CLP"
        };
        foreach (var bad in badTableNames)
        {
            ResourceNamingHelper.IsValidTableIdentifier(bad).Should().BeFalse($"'{bad}' is not a valid SQL table identifier");
            SqlFileParser.IsDisallowedIdentifier(bad).Should().BeTrue($"'{bad}' must be disallowed by SqlFileParser");
        }

        // 2. Missing dot between PUBLIC/DBO schema and lowercase table name is recovered
        var (_, parsedSchema, parsedTable) = BaseSqlParser.ParseTableIdentifier("PUBLICoutbox_event_audit");
        parsedSchema.Should().Be("PUBLIC");
        parsedTable.Should().Be("outbox_event_audit");

        // 3. Python rich.table.Table in CLI scripts emits 0 Table nodes, while real SQLAlchemy Table(...) still works
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "pyproject.toml"), "[project]\nname = \"cli-tool\"\nversion = \"0.1.0\"\n");
            Directory.CreateDirectory(Path.Combine(tempDir, "cli"));
            await File.WriteAllTextAsync(Path.Combine(tempDir, "cli", "aad_groups.py"), """
                from rich.table import Table

                def render_groups():
                    tbl = Table("Group Name", "Object ID")
                    empty_tbl = Table(show_header=True)
                    return tbl, empty_tbl
                """);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "cli", "db_schema.py"), """
                from sqlalchemy import Table, Column, Integer, MetaData

                metadata = MetaData()
                audit_log = Table("audit_events", metadata, Column("id", Integer, primary_key=True))
                """);

            var dbPath = Path.Combine(tempDir, "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var tblRes = await client.ExecuteCypherAsync("MATCH (t:Table) RETURN t.name AS name");
            var tableNames = tblRes.Rows.Select(r => r["name"].AsString()).ToList();

            tableNames.Should().Contain("audit_events");
            tableNames.Should().NotContain("Group Name");
            tableNames.Should().NotContain("SQLAlchemy: Table");
        }
        finally
        {
            PhysicalFileStore.ClearWorkspace(tempDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task JavaMultiModuleGradle_CoreSubmodulesClassifyAsLibraryAndTestInsteadOfService()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        try
        {
            var repoRoot = Path.Combine(tempDir, "plat-liam-job-service");
            var serviceDir = Path.Combine(repoRoot, "service", "src", "main", "java", "com", "by", "job");
            var coreDbDir = Path.Combine(repoRoot, "core", "database", "src", "main", "java", "com", "by", "db");
            var coreRestDir = Path.Combine(repoRoot, "core", "rest", "rest-service", "src", "main", "java", "com", "by", "rest");
            var coreUnitTestDir = Path.Combine(repoRoot, "core", "unit-test", "src", "main", "java", "com", "by", "test");

            Directory.CreateDirectory(serviceDir);
            Directory.CreateDirectory(coreDbDir);
            Directory.CreateDirectory(coreRestDir);
            Directory.CreateDirectory(coreUnitTestDir);

            await File.WriteAllTextAsync(Path.Combine(repoRoot, "settings.gradle.kts"), """
                rootProject.name = "plat-liam-job-service"
                include(":service", ":core:database", ":core:rest:rest-service", ":core:unit-test")
                """);

            // Deployable microservice module with org.springframework.boot plugin
            await File.WriteAllTextAsync(Path.Combine(repoRoot, "service", "build.gradle.kts"), """
                plugins {
                    java
                    id("org.springframework.boot") version "3.3.2"
                }
                dependencies {
                    implementation(project(":core:database"))
                    implementation("org.springframework.boot:spring-boot-starter-web")
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(serviceDir, "JobServiceApplication.java"), """
                package com.by.job;
                import org.springframework.boot.autoconfigure.SpringBootApplication;
                @SpringBootApplication
                public class JobServiceApplication {}
                """);

            // Internal shared submodules referencing spring-boot starters / grpc without runnable plugin
            await File.WriteAllTextAsync(Path.Combine(repoRoot, "core", "database", "build.gradle.kts"), """
                plugins {
                    `java-library`
                }
                dependencies {
                    api("org.springframework.boot:spring-boot-starter-data-jpa")
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(coreDbDir, "DbConfig.java"), "package com.by.db; public class DbConfig {}\n");

            await File.WriteAllTextAsync(Path.Combine(repoRoot, "core", "rest", "rest-service", "build.gradle.kts"), """
                dependencies {
                    implementation("org.springframework.boot:spring-boot-starter-web")
                    implementation("io.grpc:grpc-protobuf:1.64.0")
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(coreRestDir, "RestHelper.java"), "package com.by.rest; public class RestHelper {}\n");

            await File.WriteAllTextAsync(Path.Combine(repoRoot, "core", "unit-test", "build.gradle.kts"), """
                dependencies {
                    implementation("org.springframework.boot:spring-boot-starter-test")
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(coreUnitTestDir, "TestFixtures.java"), "package com.by.test; public class TestFixtures {}\n");

            var dbPath = Path.Combine(tempDir, "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(repoRoot, repoRoot, clear: true);

            var res = await client.ExecuteCypherAsync("MATCH (p:Project) RETURN p.name AS name, p.kind AS kind, p.role AS role");
            var kinds = res.Rows.ToDictionary(
                r => r["name"].AsString(),
                r => r["kind"].AsString(),
                StringComparer.OrdinalIgnoreCase);

            kinds["service"].Should().Be("Service");
            kinds["database"].Should().Be("Library");
            kinds["rest-service"].Should().Be("Library");
            kinds["unit-test"].Should().Be("Test");
        }
        finally
        {
            PhysicalFileStore.ClearWorkspace(tempDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
