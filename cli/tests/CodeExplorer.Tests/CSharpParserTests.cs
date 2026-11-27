using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parsers.CSharp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class CSharpParserTests
{
    private string _sampleProjectsRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var currentDir = TestContext.CurrentContext.TestDirectory;
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "cli", "tests", "SampleProjects");
            if (Directory.Exists(candidate))
            {
                _sampleProjectsRoot = candidate;
                break;
            }
            var direct = Path.Combine(dir.FullName, "SampleProjects");
            if (Directory.Exists(direct))
            {
                _sampleProjectsRoot = direct;
                break;
            }
            dir = dir.Parent;
        }

        Directory.Exists(_sampleProjectsRoot).Should().BeTrue($"SampleProjects directory not found. Probed upwards from: {TestContext.CurrentContext.TestDirectory}");
    }

    [Test]
    public async Task CSharp_LegacyFrameworkLib_ParsesPackagesConfigAndProducesPackage()
    {
        var parser = new CSharpProjectParser();
        var absPath = Path.Combine(_sampleProjectsRoot, "csharp", "legacy-framework-lib");
        var deps = await parser.ParseDependenciesAsync(absPath);
        deps.ExternalPackages.Should().ContainSingle(p => p.Name == "Newtonsoft.Json" && p.Version == "13.0.3");

        var produced = await parser.GetProducedPackageAsync(absPath);
        produced.Should().NotBeNull();
        produced!.Name.Should().Be("LegacyLib");
    }

    [Test]
    public async Task CSharp_LegacyFrameworkWeb_ParsesPackagesConfigDependencies()
    {
        var parser = new CSharpProjectParser();
        var absPath = Path.Combine(_sampleProjectsRoot, "csharp", "legacy-framework-web");
        var deps = await parser.ParseDependenciesAsync(absPath);
        deps.ExternalPackages.Should().Contain(p => p.Name == "Microsoft.AspNet.WebApi.Core" && p.Version == "5.2.9");
        deps.ExternalPackages.Should().Contain(p => p.Name == "Newtonsoft.Json" && p.Version == "13.0.3");
    }

    [Test]
    public async Task CSharp_ExtractsAttributesOnTypesMethodsAndMembers()
    {
        const string code = """
            using Microsoft.AspNetCore.Mvc;
            using System.ComponentModel.DataAnnotations;

            namespace SampleApp.Controllers;

            [Admin, Route("api/users")]
            public class UsersController : Controller
            {
                [Required]
                public string DefaultRole { get; set; } = "User";

                [HttpPost, ValidateAntiForgeryToken]
                public IActionResult Delete(string id) => Ok();
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_attr_cs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "UsersController.cs");
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new SemanticParseContext(tempDir);
            using var syntaxTree = await new CSharpFileParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: false);

            var typeNode = syntaxTree.FileNode!.Children.OfType<TypeNode>().Single(t => t.Name == "UsersController");
            typeNode.Attributes.Should().Be("Admin, Route");

            var memberNode = typeNode.Children.OfType<MemberNode>().Single(m => m.Name == "DefaultRole");
            memberNode.Attributes.Should().Be("Required");

            var methodNode = typeNode.Children.OfType<FunctionNode>().Single(f => f.Name == "Delete");
            methodNode.Attributes.Should().Be("HttpPost, ValidateAntiForgeryToken");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task CSharp_StripsGenericArgumentsFromMemberCallAndDetectsExtensionReceiverType()
    {
        const string code = """
            using System.Threading.Tasks;
            using YesSql;
            using Microsoft.AspNetCore.Authorization;

            namespace SampleApp.Security;

            public static class AuthorizationExtensions
            {
                public static Task<bool> AuthorizeAsync(this IAuthorizationService service, object user)
                    => Task.FromResult(true);
            }

            public class UserService
            {
                private readonly ISession _session;

                public UserService(ISession session)
                {
                    _session = session;
                }

                public void Search(IServiceProvider sp)
                {
                    _session.Query<User, UserIndex>();
                    var cm = sp.GetRequiredService<IContentManager>();
                }
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "ce_ext_cs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "UserService.cs");
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new SemanticParseContext(tempDir);
            using var syntaxTree = await new CSharpFileParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath, isTest: false);

            var extType = syntaxTree.FileNode!.Children.OfType<TypeNode>().Single(t => t.Name == "AuthorizationExtensions");
            var extFn = extType.Children.OfType<FunctionNode>().Single(f => f.Name == "AuthorizeAsync");
            extFn.Signals().ExtensionReceiverTypeName.Should().Be("IAuthorizationService");

            var svcType = syntaxTree.FileNode.Children.OfType<TypeNode>().Single(t => t.Name == "UserService");
            var searchFn = svcType.Children.OfType<FunctionNode>().Single(f => f.Name == "Search");
            searchFn.References.Should().Contain(r => r.ReceiverName == "_session" && r.MemberName == "Query");
            syntaxTree.RawTypeBindings.Should().Contain(b => b.VariableName == "cm" && b.TypeName == "IContentManager");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Test]
    public async Task CSharp_ParameterTypesAndMemberOfType_AppearInAnalyzeCodeImpact()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_impact_cs_" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(tempDir, "graph.db");
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "SharedContext.cs"), """
                namespace SampleApp;

                public class SharedContext
                {
                    public string RootPath { get; set; } = "";
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "Consumers.cs"), """
                using System.Collections.Generic;

                namespace SampleApp;

                public class HolderService
                {
                    private readonly SharedContext _context;

                    public HolderService(SharedContext context)
                    {
                        _context = context;
                    }
                }

                public static class ReaderHelper
                {
                    public static string ReadRoot(SharedContext ctx, int retries, string fallback)
                        => ctx.RootPath ?? fallback;

                    public static int CountAll(IReadOnlyList<SharedContext> items)
                        => items.Count;
                }
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, tempDir, clear: true);

            var repo = new CodeExplorerRepository(client);
            var impactYaml = await repo.AnalyzeCodeImpactAsync("SharedContext");

            impactYaml.Should().Contain("HolderService");
            impactYaml.Should().Contain("INJECTS_TYPE");
            impactYaml.Should().Contain("ReaderHelper.ReadRoot");
            impactYaml.Should().Contain("ReaderHelper.CountAll");
            impactYaml.Should().Contain("USES_TYPE");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
