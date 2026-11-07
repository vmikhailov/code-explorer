using CodeExplorer.Core.Parser.Components;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Parser.TypeScript.Components;
using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// Regression tests for false projects and false links found while indexing twentyhq/twenty:
/// GraphQL SDL blocks misread as anonymous queries, NestJS parameter decorators misread as endpoints,
/// and NestJS servers with react (server-side rendering) misclassified as frontend apps.
/// </summary>
[TestFixture]
public class FalsePositiveRegressionTests
{
    [Test]
    public void GraphQL_SdlNonRootBlocks_AreNotParsedAsAnonymousQueries()
    {
        const string sdl = """
            scalar UUID
            input CreateCompanyInput { id: UUID! name: String }
            enum Status { ACTIVE INACTIVE }
            interface Node { id: UUID! }
            schema { query: Query mutation: Mutation }
            type Company implements Node { id: UUID! name: String }
            type Query { company(id: UUID!): Company companies: [Company!]! }
            type Mutation { createCompany(input: CreateCompanyInput!): Company }
            """;

        var (operations, sdlFields) = GraphQLDocumentExtractor.ParseDocument(sdl);

        operations.Should().BeEmpty("SDL input/enum/interface/schema blocks are not executable operations");
        sdlFields.Select(f => f.FieldName).Should().BeEquivalentTo(["company", "companies", "createCompany"]);
    }

    [Test]
    public void GraphQL_ShorthandAndNamedOperations_AreStillExtracted()
    {
        const string doc = """
            { me { id } }
            { viewer { name } }
            query GetCompany($id: UUID!) { company(id: $id) { id } }
            mutation { deleteCompany(id: "1") { id } }
            """;

        var (operations, _) = GraphQLDocumentExtractor.ParseDocument(doc);

        operations.SelectMany(o => o.RootFields).Should().BeEquivalentTo(["me", "viewer", "company", "deleteCompany"]);
        operations.Should().Contain(o => o.OperationName == "GetCompany");
    }

    [Test]
    public void ReactNext_NestServerWithReactForServerSideRendering_IsNotAFrontend()
    {
        var context = new ProjectContext(
            "C:/repo/packages/server",
            "packages/server",
            "server",
            "typescript",
            ["C:/repo/packages/server/package.json", "C:/repo/packages/server/nest-cli.json", "C:/repo/packages/server/project.json"],
            ["@nestjs/core", "@nestjs/common", "react", "react-dom", "@react-pdf/renderer"],
            new Dictionary<string, string> { ["manifest_type"] = "application", ["framework_type"] = "web" });

        new ReactNextComponentParser().CanHandle(context).Should().BeFalse();
    }

    [Test]
    public void ReactNext_ViteReactApp_IsStillAFrontend()
    {
        var context = new ProjectContext(
            "C:/repo/packages/front",
            "packages/front",
            "front",
            "typescript",
            ["C:/repo/packages/front/package.json", "C:/repo/packages/front/index.html"],
            ["react", "react-dom", "vite", "@vitejs/plugin-react"],
            new Dictionary<string, string> { ["manifest_type"] = "application" });

        var parser = new ReactNextComponentParser();
        parser.CanHandle(context).Should().BeTrue();
        parser.AnalyzeManifest(context).Capabilities.Should().HaveFlag(ComponentCapabilities.FrontendApp);
    }
}

[TestFixture]
[Category("Integration")]
public class NestJsParameterDecoratorTests
{
    private string _tempWorkspace = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_nest_param_dec_" + Guid.NewGuid()).Replace('\\', '/');
        var tsDir = Path.Combine(_tempWorkspace, "NestApp").Replace('\\', '/');
        Directory.CreateDirectory(tsDir);
        await File.WriteAllTextAsync(Path.Combine(tsDir, "package.json"), """{ "name": "nest-app", "dependencies": { "@nestjs/core": "10.0.0", "@nestjs/common": "10.0.0" } }""");
        await File.WriteAllTextAsync(Path.Combine(tsDir, "ai.controller.ts"), """
            import { Controller, Get, Query } from '@nestjs/common';

            @Controller('ai')
            export class AiController {
                @Get('providers')
                getAiProviders(@Query('mode') mode: string, @Query() filters?: Record<string, string>) {
                    return [];
                }
            }
            """);

        _client = new SqliteGraphClient(Path.Combine(_tempWorkspace, "graph.db"));
        WorkspaceIndexer.AutoRegisterParsers();
        await new WorkspaceIndexer(_client).IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); }
            catch (IOException) { /* best-effort temp cleanup; a locked file must not fail the suite */ }
        }
    }

    [Test]
    public async Task QueryParameterDecorator_DoesNotCreateEndpoint()
    {
        var res = await _client.ExecuteCypherAsync("MATCH (e:Endpoint) RETURN e.name AS name, e.http_method AS method");
        var names = res.Rows.Select(r => r.GetStringProp("name")).ToList();

        names.Should().Contain(n => n.Contains("providers"), "the @Get('providers') handler is a real endpoint");
        names.Should().NotContain(n => n.StartsWith("QUERY:"), "@Query('mode') on a parameter is not an endpoint");
    }
}

/// <summary>
/// Monorepo shaped like twentyhq/twenty: verifies that the workspace root, a source-less nested manifest,
/// an e2e suite, test fixtures and a NestJS server that renders React on the server are classified correctly.
/// </summary>
[TestFixture]
[Category("Integration")]
public class MonorepoProjectClassificationTests
{
    private string _tempWorkspace = null!;
    private SqliteGraphClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_monorepo_roles_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(_tempWorkspace);

        async Task Write(string relative, string content)
        {
            var full = Path.Combine(_tempWorkspace, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, content);
        }

        await Write("package.json", """{ "name": "acme-monorepo", "private": true, "workspaces": ["packages/*"], "devDependencies": { "@nx/react": "20.0.0" } }""");
        await Write("nx.json", "{}");

        await Write("packages/server/package.json", """{ "name": "acme-server", "scripts": { "start:prod": "node dist/main" }, "dependencies": { "@nestjs/core": "10.0.0", "@nestjs/common": "10.0.0", "react": "18.0.0", "react-dom": "18.0.0" } }""");
        await Write("packages/server/project.json", """{ "projectType": "application" }""");
        await Write("packages/server/nest-cli.json", "{}");
        await Write("packages/server/src/main.ts", "import { NestFactory } from '@nestjs/core';\nasync function bootstrap() { const app = await NestFactory.create({}); await app.listen(3000); }\nbootstrap();\n");
        await Write("packages/server/src/templates/app-deps/package.json", """{ "name": "app-deps-template", "dependencies": { "react": "18.0.0" } }""");
        await Write("packages/server/src/templates/app-deps/yarn.lock", "# lockfile\n");

        await Write("packages/smoke/package.json", """{ "name": "acme-e2e", "devDependencies": { "@playwright/test": "1.40.0" } }""");
        await Write("packages/smoke/project.json", """{ "projectType": "application" }""");
        await Write("packages/smoke/tests/login.spec.ts", "import { test } from '@playwright/test';\ntest('login', async () => {});\n");

        await Write("packages/apps/fixtures/minimal-app/package.json", """{ "name": "minimal-app", "dependencies": { "react": "18.0.0" } }""");
        await Write("packages/apps/fixtures/minimal-app/index.ts", "export const app = {};\n");

        _client = new SqliteGraphClient(Path.Combine(_tempWorkspace, "graph.db"));
        WorkspaceIndexer.AutoRegisterParsers();
        await new WorkspaceIndexer(_client).IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); }
            catch (IOException) { /* best-effort temp cleanup; a locked file must not fail the suite */ }
        }
    }

    private async Task<Dictionary<string, string>> ProjectRolesAsync()
    {
        var res = await _client.ExecuteCypherAsync("MATCH (p:Project) RETURN p.name AS name, p.role AS role");
        return res.Rows.ToDictionary(r => r.GetStringProp("name"), r => r.GetStringProp("role"));
    }

    [Test]
    public async Task NestServerWithServerSideReact_IsNotAFrontendApp()
    {
        var roles = await ProjectRolesAsync();
        roles.Should().ContainKey("acme-server");
        roles["acme-server"].Should().NotBe("FrontendApp");
    }

    [Test]
    public async Task SourcelessNestedManifest_IsNotAProject()
    {
        var roles = await ProjectRolesAsync();
        roles.Should().NotContainKey("app-deps-template");
    }

    [Test]
    public async Task WorkspaceRoot_IsNotAService()
    {
        var roles = await ProjectRolesAsync();
        if (roles.TryGetValue("acme-monorepo", out var role))
        {
            role.Should().NotBe("Service");
        }
    }

    [Test]
    public async Task E2eSuiteAndFixtures_AreTestProjects()
    {
        var roles = await ProjectRolesAsync();
        var e2eKey = roles.Keys.FirstOrDefault(k => k is "acme-e2e" or "smoke");
        e2eKey.Should().NotBeNull("the Playwright suite must be detected as a project");
        roles[e2eKey!].Should().Be("Test");
        if (roles.TryGetValue("minimal-app", out var fixtureRole))
        {
            fixtureRole.Should().Be("Test");
        }
    }
}
