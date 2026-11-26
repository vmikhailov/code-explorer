using CodeExplorer.Core.Analysis.Subgraph;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CrossRepoDependsOnAndSubgraphTests
{
    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ce-xrepo-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task CrossEcosystemDependsOn_ScopedNpmAndGenericShortNames_ProduceZeroCrossLanguageEdges()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        try
        {
            // 1. Java multi-module repo with ':core' and ':core:common' submodules (exec-ce-commons-data)
            var javaCommons = Path.Combine(tempDir, "exec-ce-commons-data");
            Directory.CreateDirectory(Path.Combine(javaCommons, "core", "src", "main", "java", "com", "by", "core"));
            Directory.CreateDirectory(Path.Combine(javaCommons, "common", "src", "main", "java", "com", "by", "common"));
            await File.WriteAllTextAsync(Path.Combine(javaCommons, "settings.gradle.kts"), """
                rootProject.name = "exec-ce-commons-data"
                include(":core", ":common")
                """);
            await File.WriteAllTextAsync(Path.Combine(javaCommons, "core", "build.gradle.kts"), "plugins { `java-library` }\ngroup = \"com.by.commons\"\nversion = \"1.0.0\"\n");
            await File.WriteAllTextAsync(Path.Combine(javaCommons, "core", "src", "main", "java", "com", "by", "core", "CoreEntity.java"), "package com.by.core; public class CoreEntity {}\n");
            await File.WriteAllTextAsync(Path.Combine(javaCommons, "common", "build.gradle.kts"), "plugins { `java-library` }\ngroup = \"com.by.commons\"\nversion = \"1.0.0\"\n");
            await File.WriteAllTextAsync(Path.Combine(javaCommons, "common", "src", "main", "java", "com", "by", "common", "CommonUtil.java"), "package com.by.common; public class CommonUtil {}\n");

            // 2. C# project nested under .../src
            var csSrcDir = Path.Combine(tempDir, "plat-apim-api-registry", "actions", "arm_creator", "src");
            Directory.CreateDirectory(csSrcDir);
            await File.WriteAllTextAsync(Path.Combine(csSrcDir, "ArmCreator.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(csSrcDir, "Program.cs"), "namespace Arm; public class Builder {}");

            // 3. TypeScript shared library producing @by/plat-lui-portal-utils (also has docs requirements.txt)
            var tsUtils = Path.Combine(tempDir, "plat-lui-portal-utils");
            Directory.CreateDirectory(Path.Combine(tsUtils, "src"));
            await File.WriteAllTextAsync(Path.Combine(tsUtils, "requirements.txt"), "mkdocs==1.6.0\n");
            await File.WriteAllTextAsync(Path.Combine(tsUtils, "package.json"), """
                {
                  "name": "@by/plat-lui-portal-utils",
                  "version": "1.2.0",
                  "main": "dist/index.js",
                  "types": "dist/index.d.ts"
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(tsUtils, "tsconfig.json"), """{"compilerOptions":{"strict":true}}""");
            await File.WriteAllTextAsync(Path.Combine(tsUtils, "src", "index.ts"), "export const formatPortalName = (s: string) => s.trim();\n");

            // 4. TypeScript portal depending on @actions/core (external npm!) and @by/plat-lui-portal-utils (internal npm!)
            var tsPortal = Path.Combine(tempDir, "plat-lui-portal");
            Directory.CreateDirectory(Path.Combine(tsPortal, "src"));
            await File.WriteAllTextAsync(Path.Combine(tsPortal, "package.json"), """
                {
                  "name": "@by/plat-lui-portal",
                  "version": "1.0.0",
                  "dependencies": {
                    "@actions/core": "^1.10.1",
                    "@nestjs/common": "^10.0.0",
                    "@by/plat-lui-portal-utils": "^1.2.0"
                  }
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(tsPortal, "tsconfig.json"), """{"compilerOptions":{"strict":true}}""");
            await File.WriteAllTextAsync(Path.Combine(tsPortal, "src", "main.tsx"), "import { formatPortalName } from '@by/plat-lui-portal-utils';\nexport const title = formatPortalName('Portal');\n");

            // 5. Python repo with generic package names that collide with folder names 'core' / 'src'
            var pyRepo = Path.Combine(tempDir, "retail-lp-gemini");
            Directory.CreateDirectory(Path.Combine(pyRepo, "app"));
            await File.WriteAllTextAsync(Path.Combine(pyRepo, "requirements.txt"), "core==0.1.0\nsrc==0.1.0\ncommon==0.1.0\n");
            await File.WriteAllTextAsync(Path.Combine(pyRepo, "app", "solver.py"), "def solve():\n    return 42\n");

            var dbPath = Path.Combine(tempDir, "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            await new WorkspaceIndexer(client).IndexAsync(tempDir, tempDir, clear: true);

            // Verification Checklist #1: ZERO cross-language Project -> Project DEPENDS_ON edges
            var crossLangCount = await client.QueryScalarAsync<long>(
                "MATCH (a:Project)-[:DEPENDS_ON]->(b:Project) WHERE a.language <> b.language RETURN count(*) AS cnt");
            crossLangCount.Should().Be(0, "@actions/core and @nestjs/common in TypeScript must never bind to Java 'core'/'common' submodules, and Python must never bind to C# 'src'");

            // Verification Checklist #2: exec-ce-commons-data/core has 0 incoming DEPENDS_ON from TypeScript
            var coreInboundFromTs = await client.QueryScalarAsync<long>(
                "MATCH (ts:Project {language: 'typescript'})-[:DEPENDS_ON]->(j:Project {name: 'core'}) RETURN count(*) AS cnt");
            coreInboundFromTs.Should().Be(0);

            // Same-ecosystem @by/plat-lui-portal -> @by/plat-lui-portal-utils MUST resolve with provenance metadata
            var validSameEco = await client.ExecuteCypherAsync(
                "MATCH (a:Project {name: 'plat-lui-portal'})-[r:DEPENDS_ON]->(b:Project {name: 'plat-lui-portal-utils'}) RETURN r.reason AS reason, r.source_dep AS source_dep");
            validSameEco.Rows.Should().ContainSingle();
            validSameEco.Rows[0]["reason"].AsString().Should().Be("package_dep");
            validSameEco.Rows[0]["source_dep"].AsString().Should().Be("@by/plat-lui-portal-utils");
        }
        finally
        {
            PhysicalFileStore.ClearWorkspace(tempDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CrossRepoHttpCalls_SeparateServiceCallFromDependsOn_AndRejectGenericRoutes()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        try
        {
            // 1. Java realm service exposing generic /health and /realms/{id}, plus cucumber-tests submodule
            var realmRepo = Path.Combine(tempDir, "plat-liam-realm-service");
            var webDir = Path.Combine(realmRepo, "realm-web", "src", "main", "java", "com", "by", "realm");
            var cucumberDir = Path.Combine(realmRepo, "cucumber-tests", "src", "test", "java", "com", "by", "bdd");
            Directory.CreateDirectory(webDir);
            Directory.CreateDirectory(cucumberDir);
            await File.WriteAllTextAsync(Path.Combine(realmRepo, "settings.gradle.kts"), """
                rootProject.name = "plat-liam-realm-service"
                include(":realm-web", ":cucumber-tests")
                """);
            await File.WriteAllTextAsync(Path.Combine(realmRepo, "realm-web", "build.gradle.kts"), """
                plugins { id("org.springframework.boot") version "3.3.0" }
                dependencies { implementation("org.springframework.boot:spring-boot-starter-web") }
                """);
            await File.WriteAllTextAsync(Path.Combine(webDir, "RealmController.java"), """
                package com.by.realm;
                import org.springframework.web.bind.annotation.*;
                @RestController
                public class RealmController {
                    @GetMapping("/health")
                    public String health() { return "ok"; }
                    @GetMapping("/realms/{id}")
                    public String getRealm(@PathVariable String id) { return id; }
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(realmRepo, "cucumber-tests", "build.gradle.kts"), "dependencies { testImplementation(\"io.cucumber:cucumber-java:7.0.0\") }\n");
            await File.WriteAllTextAsync(Path.Combine(cucumberDir, "Steps.java"), "package com.by.bdd; public class Steps {}\n");

            // 2. Python agent registry service exposing specific /api/v1/agents/register
            var agentRepo = Path.Combine(tempDir, "genai-agent-registry");
            Directory.CreateDirectory(agentRepo);
            await File.WriteAllTextAsync(Path.Combine(agentRepo, "pyproject.toml"), "[project]\nname = \"genai-agent-registry\"\nversion = \"0.1.0\"\ndependencies = [\"fastapi\"]\n");
            await File.WriteAllTextAsync(Path.Combine(agentRepo, "server.py"), """
                from fastapi import FastAPI
                app = FastAPI()

                @app.post("/api/v1/agents/register")
                def register_agent():
                    return {"status": "registered"}
                """);

            // 3. Python plan-lde-raq calling generic relative /health & /realms/default, plus explicit host genai-agent-registry
            var planRepo = Path.Combine(tempDir, "plan-lde-raq");
            Directory.CreateDirectory(planRepo);
            await File.WriteAllTextAsync(Path.Combine(planRepo, "pyproject.toml"), "[project]\nname = \"plan-lde-raq\"\nversion = \"0.1.0\"\ndependencies = [\"requests\"]\n");
            await File.WriteAllTextAsync(Path.Combine(planRepo, "client.py"), """
                import requests

                def probe_generic(base_url):
                    requests.get("http://localhost:8080/health")
                    requests.get("http://localhost:8080/realms/default")

                def register():
                    return requests.post("http://genai-agent-registry/api/v1/agents/register")
                """);

            var dbPath = Path.Combine(tempDir, "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            await new WorkspaceIndexer(client).IndexAsync(tempDir, tempDir, clear: true);

            // Runtime HTTP calls must NOT emit Project -[:DEPENDS_ON]-> Project
            var allProjDependsOn = await client.QueryScalarAsync<long>(
                "MATCH (a:Project)-[:DEPENDS_ON]->(b:Project) RETURN count(*) AS cnt");
            allProjDependsOn.Should().Be(0);

            // Generic /health and /realms/{id} without host match must NOT link plan-lde-raq -> realm-web
            var falseRealmCall = await client.QueryScalarAsync<long>(
                "MATCH (a:Project {name: 'plan-lde-raq'})-[:SERVICE_CALL]->(b:Project {name: 'realm-web'}) RETURN count(*) AS cnt");
            falseRealmCall.Should().Be(0);

            // Explicit host + specific route DOES emit SERVICE_CALL with reason = runtime_http
            var validAgentCall = await client.ExecuteCypherAsync(
                "MATCH (a:Project {name: 'plan-lde-raq'})-[r:SERVICE_CALL]->(b:Project {name: 'genai-agent-registry'}) RETURN r.reason AS reason, r.matched_endpoint AS ep");
            validAgentCall.Rows.Should().ContainSingle();
            validAgentCall.Rows[0]["reason"].AsString().Should().Be("runtime_http");
        }
        finally
        {
            PhysicalFileStore.ClearWorkspace(tempDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task SubgraphService_CoarsensHierarchy_SeparatesHubsWithLeiden_AndSlicesViaPpr()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = CreateTempDir();
        try
        {
            // Shared TS hub depended on by 3 of 6 repos (50% > 15% hub threshold)
            async Task CreateTsRepoAsync(string folder, string pkgName, string[] deps, bool isLib = false)
            {
                var dir = Path.Combine(tempDir, folder);
                Directory.CreateDirectory(Path.Combine(dir, "src"));
                var depsJson = string.Join(", ", deps.Select(d => $"\"{d}\": \"^1.0.0\""));
                var mainFields = isLib ? "\"main\": \"dist/index.js\", \"types\": \"dist/index.d.ts\"," : "";
                await File.WriteAllTextAsync(Path.Combine(dir, "package.json"), $$"""
                    {
                      "name": "{{pkgName}}",
                      "version": "1.0.0",
                      {{mainFields}}
                      "dependencies": { {{depsJson}} }
                    }
                    """);
                await File.WriteAllTextAsync(Path.Combine(dir, "tsconfig.json"), """{"compilerOptions":{"strict":true}}""");
                await File.WriteAllTextAsync(Path.Combine(dir, "src", "index.ts"), $"export const id = '{folder}';\n");
            }

            await CreateTsRepoAsync("shared-telemetry-sdk", "@by/shared-telemetry-sdk", [], isLib: true);
            await CreateTsRepoAsync("plat-lui-utils", "@by/plat-lui-utils", ["@by/shared-telemetry-sdk"], isLib: true);
            await CreateTsRepoAsync("plat-lui-portal", "@by/plat-lui-portal", ["@by/plat-lui-utils", "@by/shared-telemetry-sdk"]);
            await CreateTsRepoAsync("plat-lui-dashboard", "@by/plat-lui-dashboard", ["@by/plat-lui-utils", "@by/shared-telemetry-sdk"]);

            // Separate Java domain with multi-module Gradle hierarchy (:service + :core:db) + sibling Java repo
            var liamRealm = Path.Combine(tempDir, "plat-liam-realm-service");
            Directory.CreateDirectory(Path.Combine(liamRealm, "service", "src", "main", "java", "com", "by"));
            Directory.CreateDirectory(Path.Combine(liamRealm, "core", "db", "src", "main", "java", "com", "by"));
            await File.WriteAllTextAsync(Path.Combine(liamRealm, "settings.gradle.kts"), "rootProject.name = \"plat-liam-realm-service\"\ninclude(\":service\", \":core:db\")\n");
            await File.WriteAllTextAsync(Path.Combine(liamRealm, "core", "db", "build.gradle.kts"), "plugins { `java-library` }\ngroup = \"com.by.liam\"\nversion = \"1.0.0\"\n");
            await File.WriteAllTextAsync(Path.Combine(liamRealm, "core", "db", "src", "main", "java", "com", "by", "RealmStore.java"), "package com.by; public class RealmStore {}\n");
            await File.WriteAllTextAsync(Path.Combine(liamRealm, "service", "build.gradle.kts"), """
                plugins { id("org.springframework.boot") version "3.3.0" }
                group = "com.by.liam"
                version = "1.0.0"
                dependencies { implementation(project(":core:db")) }
                """);
            await File.WriteAllTextAsync(Path.Combine(liamRealm, "service", "src", "main", "java", "com", "by", "App.java"), "package com.by; public class App { public static void main(String[] a) {} }\n");

            var liamCommon = Path.Combine(tempDir, "plat-liam-common-lib");
            Directory.CreateDirectory(Path.Combine(liamCommon, "src", "main", "java", "com", "by"));
            await File.WriteAllTextAsync(Path.Combine(liamCommon, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                  <groupId>com.by.liam</groupId>
                  <artifactId>liam-common</artifactId>
                  <version>1.0.0</version>
                </project>
                """);
            await File.WriteAllTextAsync(Path.Combine(liamCommon, "src", "main", "java", "com", "by", "Common.java"), "package com.by; public class Common {}\n");

            var liamJob = Path.Combine(tempDir, "plat-liam-job-service");
            Directory.CreateDirectory(Path.Combine(liamJob, "src", "main", "java", "com", "by"));
            await File.WriteAllTextAsync(Path.Combine(liamJob, "pom.xml"), """
                <project xmlns="http://maven.apache.org/POM/4.0.0">
                  <groupId>com.by.liam</groupId>
                  <artifactId>liam-job-service</artifactId>
                  <version>1.0.0</version>
                  <dependencies>
                    <dependency>
                      <groupId>com.by.liam</groupId>
                      <artifactId>liam-common</artifactId>
                      <version>1.0.0</version>
                    </dependency>
                  </dependencies>
                </project>
                """);
            await File.WriteAllTextAsync(Path.Combine(liamJob, "src", "main", "java", "com", "by", "JobApp.java"), "package com.by; public class JobApp { public static void main(String[] a) {} }\n");

            // Standalone Go CLI repo
            var goCli = Path.Combine(tempDir, "ops-standalone-cli");
            Directory.CreateDirectory(goCli);
            await File.WriteAllTextAsync(Path.Combine(goCli, "go.mod"), "module company.internal/ops-standalone-cli\n\ngo 1.22\n");
            await File.WriteAllTextAsync(Path.Combine(goCli, "main.go"), "package main\nfunc main() {}\n");

            var dbPath = Path.Combine(tempDir, "graph.db");
            await using var client = new SqliteGraphClient(dbPath);
            await new WorkspaceIndexer(client).IndexAsync(tempDir, tempDir, clear: true);

            var subgraph = new WorkspaceSubgraphService();

            // Step 0: Coarsening rolls up plat-liam-realm-service's 2 Gradle submodules into 1 repo unit (8 projects -> 7 units)
            var coarsened = await subgraph.BuildCoarsenedGraphAsync(client);
            coarsened.RawProjectCount.Should().Be(9);
            coarsened.Units.Should().HaveCount(8);
            coarsened.Units.Single(u => u.Name == "plat-liam-realm-service").MemberProjectCount.Should().Be(2);

            // Mode A: Leiden domain partitioning separates shared-telemetry-sdk hub, plat-lui domain, plat-liam domain, and standalone units
            var partition = await subgraph.PartitionDomainsAsync(client, resolution: 1.0, hubThreshold: 0.20);
            partition.PlatformHubs.Select(h => h.Unit.Name).Should().Contain("shared-telemetry-sdk");
            partition.StandaloneUnits.Select(u => u.Name).Should().Contain("ops-standalone-cli");

            var luiCluster = partition.Domains.Single(d => d.Members.Any(m => m.Name == "plat-lui-portal"));
            luiCluster.Members.Select(m => m.Name).Should().BeEquivalentTo(["plat-lui-portal", "plat-lui-dashboard", "plat-lui-utils"]);

            var liamCluster = partition.Domains.Single(d => d.Members.Any(m => m.Name == "plat-liam-job-service"));
            liamCluster.Members.Select(m => m.Name).Should().BeEquivalentTo(["plat-liam-job-service", "plat-liam-common-lib"]);

            // Mode B: Personalized PageRank slice around 'plat-lui-portal' ranks its TS domain peers first
            var slice = await subgraph.SliceSubgraphAsync(client, ["plat-lui-portal"], maxRepos: 3);
            slice.ResolvedSeedUnits.Should().ContainSingle().Which.Should().Be("plat-lui-portal");
            slice.SelectedUnits.Should().HaveCount(3);
            slice.SelectedUnits[0].Unit.Name.Should().Be("plat-lui-portal");
            slice.SelectedUnits[0].IsSeed.Should().BeTrue();
            slice.SelectedUnits.Select(s => s.Unit.Name).Should().Contain("plat-lui-utils");
            slice.SelectedUnits.Select(s => s.Unit.Name).Should().NotContain("plat-liam-job-service");
        }
        finally
        {
            PhysicalFileStore.ClearWorkspace(tempDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
