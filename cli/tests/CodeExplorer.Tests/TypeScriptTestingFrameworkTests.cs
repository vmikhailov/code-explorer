using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TypeScriptTestingFrameworkTests
{
    private sealed class TestWorkspace : IDisposable
    {
        public string DirectoryPath { get; }
        public string FilePath { get; }
        public SyntaxTree SyntaxTree { get; }
        public FileNode FileNode => SyntaxTree.FileNode;
        public ParsingContext Context { get; }

        private TestWorkspace(string dir, string file, SyntaxTree tree, ParsingContext ctx)
        {
            DirectoryPath = dir;
            FilePath = file;
            SyntaxTree = tree;
            Context = ctx;
        }

        public static async Task<TestWorkspace> CreateAsync(string code, string fileName = "app.test.ts")
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ts_test_fw_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            var parser = new TypeScriptParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var syntaxTree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, ctx, "test-proj", isTest: true);
            return new TestWorkspace(tempDir, filePath, syntaxTree, ctx);
        }

        public void Dispose()
        {
            SyntaxTree.Dispose();
            try { Directory.Delete(DirectoryPath, true); } catch { }
        }
    }

    private static List<T> FindNodes<T>(IEnumerable<IOntologyNode> nodes) where T : IOntologyNode
    {
        var result = new List<T>();
        foreach (var node in nodes)
        {
            if (node is T match) result.Add(match);
            result.AddRange(FindNodes<T>(node.Children));
        }
        return result;
    }

    private static List<Reference> FindAllReferences(FileNode fileNode)
    {
        var result = new List<Reference>(fileNode.References);
        result.AddRange(FindReferences(fileNode.Children));
        return result;
    }

    private static List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<Reference>();
        foreach (var node in nodes)
        {
            result.AddRange(node.References);
            result.AddRange(FindReferences(node.Children));
        }
        return result;
    }

    [Test]
    public async Task Test_Jest_Suites_And_Tests()
    {
        var code = """
jest.mock("./services/auth.service");

describe("Auth Module", () => {
    it("should login successfully", () => {
        expect(true).toBe(true);
    });

    test("should handle invalid credentials", () => {
        expect(false).toBe(false);
    });
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "auth.test.ts");

        var functions = FindNodes<FunctionNode>(ws.FileNode.Children);
        functions.Should().NotBeEmpty();

        var suite = functions.FirstOrDefault(e => e.Name == "Jest:Suite:Auth Module");
        suite.Should().NotBeNull("Expected Jest:Suite:Auth Module to be detected");
        suite!.Extensions?.GetValueOrDefault("is_test").Should().Be("true");
        suite.Extensions?.GetValueOrDefault("test_framework").Should().Be("jest");

        var itTest = functions.FirstOrDefault(e => e.Name == "Jest:Test:should login successfully");
        itTest.Should().NotBeNull("Expected Jest:Test:should login successfully to be detected");
        itTest!.Extensions?.GetValueOrDefault("is_test").Should().Be("true");

        var fnTest = functions.FirstOrDefault(e => e.Name == "Jest:Test:should handle invalid credentials");
        fnTest.Should().NotBeNull("Expected Jest:Test:should handle invalid credentials to be detected");

        var allRefs = FindAllReferences(ws.FileNode);
        allRefs.Any(r => r.TargetName == "./services/auth.service" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Vitest_Suites_And_Tests()
    {
        var code = """
import { describe, test, it, expect, vi } from 'vitest';

vi.mock("./services/user.service");

describe("User Controller", () => {
    test("fetches user by ID", () => {
        expect(1).toBe(1);
    });

    it("updates user email", () => {
        expect(true).toBe(true);
    });
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "user.spec.ts");

        var functions = FindNodes<FunctionNode>(ws.FileNode.Children);
        var suite = functions.FirstOrDefault(e => e.Name == "Vitest:Suite:User Controller");
        suite.Should().NotBeNull("Expected Vitest:Suite:User Controller to be detected");
        suite!.Extensions?.GetValueOrDefault("is_test").Should().Be("true");
        suite.Extensions?.GetValueOrDefault("test_framework").Should().Be("vitest");

        var testCase = functions.FirstOrDefault(e => e.Name == "Vitest:Test:fetches user by ID");
        testCase.Should().NotBeNull();
        testCase!.Extensions?.GetValueOrDefault("is_test").Should().Be("true");

        var allRefs = FindAllReferences(ws.FileNode);
        allRefs.Any(r => r.TargetName == "./services/user.service" && r.Kind == "DEPENDS_ON").Should().BeTrue();
    }

    [Test]
    public async Task Test_Mocha_Suites_And_Tests()
    {
        var code = """
import 'mocha';

describe("Mocha Payment Suite", () => {
    context("when balance is sufficient", () => {
        it("deducts funds", () => {
        });

        specify("sends receipt email", () => {
        });
    });
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "payment.test.ts");

        var functions = FindNodes<FunctionNode>(ws.FileNode.Children);
        var suite = functions.FirstOrDefault(e => e.Name == "Mocha:Suite:Mocha Payment Suite");
        suite.Should().NotBeNull("Expected Mocha:Suite:Mocha Payment Suite");
        suite!.Extensions?.GetValueOrDefault("is_test").Should().Be("true");
        suite.Extensions?.GetValueOrDefault("test_framework").Should().Be("mocha");

        var ctxSuite = functions.FirstOrDefault(e => e.Name == "Mocha:Suite:when balance is sufficient");
        ctxSuite.Should().NotBeNull("Expected Mocha:Suite:when balance is sufficient");

        var specifyTest = functions.FirstOrDefault(e => e.Name == "Mocha:Test:sends receipt email");
        specifyTest.Should().NotBeNull("Expected Mocha:Test:sends receipt email");
        specifyTest!.Extensions?.GetValueOrDefault("is_test").Should().Be("true");
    }

    [Test]
    public async Task Test_Cypress_Navigation_And_Requests()
    {
        var code = """
describe("Checkout Workflow", () => {
    it("completes order", () => {
        cy.visit("https://shop.example.com/checkout");
        cy.request("POST", "https://api.example.com/v1/orders");
    });
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "checkout.cy.ts");

        var functions = FindNodes<FunctionNode>(ws.FileNode.Children);
        var suite = functions.FirstOrDefault(e => e.Name == "Cypress:Suite:Checkout Workflow");
        suite.Should().NotBeNull("Expected Cypress:Suite:Checkout Workflow");
        suite!.Extensions?.GetValueOrDefault("test_framework").Should().Be("cypress");

        var testCase = functions.FirstOrDefault(e => e.Name == "Cypress:Test:completes order");
        testCase.Should().NotBeNull("Expected Cypress:Test:completes order");

        var allRefs = FindAllReferences(ws.FileNode);
        allRefs.Any(r => r.TargetName == "https://shop.example.com/checkout" && r.Kind == "CALLS_ENDPOINT").Should().BeTrue();
        allRefs.Any(r => r.TargetName == "https://api.example.com/v1/orders" && r.Kind == "CALLS_ENDPOINT").Should().BeTrue();

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().NotBeEmpty();
    }

    [Test]
    public async Task Test_Playwright_Suites_Navigation_And_Api()
    {
        var code = """
import { test, expect } from '@playwright/test';

test.describe("Admin Portal", () => {
    test("navigates and verifies telemetry", async ({ page, request }) => {
        await page.goto("https://portal.example.com/dashboard");
        await request.get("https://api.example.com/health");
    });
});
""";
        using var ws = await TestWorkspace.CreateAsync(code, "admin.spec.ts");

        var functions = FindNodes<FunctionNode>(ws.FileNode.Children);
        var suite = functions.FirstOrDefault(e => e.Name == "Playwright:Suite:Admin Portal");
        suite.Should().NotBeNull("Expected Playwright:Suite:Admin Portal");
        suite!.Extensions?.GetValueOrDefault("test_framework").Should().Be("playwright");

        var testCase = functions.FirstOrDefault(e => e.Name == "Playwright:Test:navigates and verifies telemetry");
        testCase.Should().NotBeNull("Expected Playwright:Test:navigates and verifies telemetry");

        var allRefs = FindAllReferences(ws.FileNode);
        allRefs.Any(r => r.TargetName == "https://portal.example.com/dashboard" && r.Kind == "CALLS_ENDPOINT").Should().BeTrue();
        allRefs.Any(r => r.TargetName == "https://api.example.com/health" && r.Kind == "CALLS_ENDPOINT").Should().BeTrue();

        var extServices = FindNodes<ExternalServiceNode>(ws.FileNode.Children);
        extServices.Should().NotBeEmpty();
    }

    [Test]
    public void Test_TypeScriptParser_Packages_Include_TestFrameworks()
    {
        var parser = new TypeScriptParser();
        var packages = parser.Packages;

        var jestPkg = packages.FirstOrDefault(p => p.Id == "jest");
        jestPkg.Should().NotBeNull();
        jestPkg!.Role.Should().Be(LibraryRole.TestFramework);

        var vitestPkg = packages.FirstOrDefault(p => p.Id == "vitest");
        vitestPkg.Should().NotBeNull();
        vitestPkg!.Role.Should().Be(LibraryRole.TestFramework);

        var mochaPkg = packages.FirstOrDefault(p => p.Id == "mocha");
        mochaPkg.Should().NotBeNull();
        mochaPkg!.Role.Should().Be(LibraryRole.TestFramework);

        var cypressPkg = packages.FirstOrDefault(p => p.Id == "cypress");
        cypressPkg.Should().NotBeNull();
        cypressPkg!.Role.Should().Be(LibraryRole.TestFramework);

        var playwrightPkg = packages.FirstOrDefault(p => p.Id == "playwright");
        playwrightPkg.Should().NotBeNull();
        playwrightPkg!.Role.Should().Be(LibraryRole.TestFramework);
    }
}
