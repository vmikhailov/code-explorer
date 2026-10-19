using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Rust;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class RustParserTests
{
    private static string GetFixturePath(string relativePath)
    {
        var basePath = TestContext.CurrentContext.TestDirectory;
        return Path.GetFullPath(Path.Combine(basePath, "../../../tests/SampleProjects/rust", relativePath));
    }

    [Test]
    public void Test_CargoManifestParser_VirtualWorkspace()
    {
        var rootToml = GetFixturePath("microservices-workspace/Cargo.toml");
        File.Exists(rootToml).Should().BeTrue($"Manifest should exist at {rootToml}");

        var manifest = CargoManifestParser.ParseManifest(rootToml);
        manifest.Should().NotBeNull();
        manifest.IsVirtualWorkspace.Should().BeTrue();
        manifest.Package.Should().BeNull();
        manifest.Workspace.Should().NotBeNull();

        var members = CargoManifestParser.GetWorkspaceMembers(rootToml);
        members.Count.Should().Be(6);
        members.Any(m => m.EndsWith("auth-service", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        members.Any(m => m.EndsWith("domain-models", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        members.Any(m => m.EndsWith("order-processor", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        members.Any(m => m.EndsWith("payment-gateway", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        members.Any(m => m.EndsWith("admin-cli", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        members.Any(m => m.EndsWith("e2e", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        manifest.Workspace!.WorkspaceDependencies.Should().ContainKey("serde");
        manifest.Workspace.WorkspaceDependencies.Should().ContainKey("tokio");
    }

    [Test]
    public void Test_CargoManifestParser_PackageWithInheritance()
    {
        var authToml = GetFixturePath("microservices-workspace/crates/auth-service/Cargo.toml");
        File.Exists(authToml).Should().BeTrue();

        var manifest = CargoManifestParser.ParseManifest(authToml);
        manifest.Should().NotBeNull();
        manifest.IsVirtualWorkspace.Should().BeFalse();
        manifest.Package.Should().NotBeNull();
        manifest.Package!.Name.Should().Be("auth-service");
        manifest.Package.Version.Should().Be("1.0.0"); // Inherited from workspace

        var depNames = manifest.Dependencies.Select(d => d.Name).ToList();
        depNames.Should().Contain("axum");
        depNames.Should().Contain("sqlx");
        depNames.Should().Contain("domain-models");

        var domainDep = manifest.Dependencies.First(d => d.Name == "domain-models");
        domainDep.Path.Should().Be("../domain-models");
    }

    [Test]
    public async Task Test_RustParser_ProjectDetection_And_Dependencies()
    {
        var parser = new RustParser();

        var rootDir = GetFixturePath("microservices-workspace");
        var rootFiles = Directory.GetFiles(rootDir);
        parser.IsProjectDirectory(rootDir, rootFiles).Should().BeFalse("Virtual workspace root should not be detected as a single project");

        var domainDir = GetFixturePath("microservices-workspace/crates/domain-models");
        var domainFiles = Directory.GetFiles(domainDir);
        parser.IsProjectDirectory(domainDir, domainFiles).Should().BeTrue();

        var produced = await parser.GetProducedPackageAsync(domainDir);
        produced.Should().NotBeNull();
        produced!.Name.Should().Be("domain-models");
        produced.Type.Should().Be("rust");

        var authDir = GetFixturePath("microservices-workspace/crates/auth-service");
        var authDeps = await parser.ParseDependenciesAsync(authDir);
        authDeps.Should().NotBeNull();

        authDeps.LocalProjectPaths.Count.Should().BeGreaterThan(0);
        authDeps.LocalProjectPaths.Any(p => p.EndsWith("domain-models", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        var externalDeps = authDeps.ExternalPackages.Select(p => p.Name).ToList();
        externalDeps.Should().Contain("axum");
        externalDeps.Should().Contain("sqlx");
    }

    [Test]
    public async Task Test_RustParser_DomainModels_SyntacticAndSemantic()
    {
        var parser = new RustParser();
        var domainFile = GetFixturePath("microservices-workspace/crates/domain-models/src/lib.rs");
        File.Exists(domainFile).Should().BeTrue();

        var workspacePath = GetFixturePath("microservices-workspace");
        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(domainFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();

        var types = FindNodes<TypeNode>(fileNode.Children);
        var typeNames = types.Select(t => t.Name).ToList();

        typeNames.Should().Contain("User");
        typeNames.Should().Contain("UserRepository");
        typeNames.Should().Contain("InMemoryUserRepo");

        var functions = FindNodes<FunctionNode>(fileNode.Children);
        var functionNames = functions.Select(f => f.Name).ToList();
        functionNames.Should().Contain("get_user");

        // Trait implementation check: InMemoryUserRepo implements UserRepository
        var repoType = types.First(t => t.Name == "InMemoryUserRepo");
        var implRel = repoType.References.FirstOrDefault(r => r.Kind == "IMPLEMENTS");
        implRel.Should().NotBeNull("InMemoryUserRepo should implement UserRepository trait");
        implRel!.TargetName.Should().Be("UserRepository");
    }

    [Test]
    public async Task Test_RustParser_AuthService_EmbeddedSql()
    {
        var parser = new RustParser();
        var authMain = GetFixturePath("microservices-workspace/crates/auth-service/src/main.rs");
        File.Exists(authMain).Should().BeTrue();

        var workspacePath = GetFixturePath("microservices-workspace");
        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(authMain, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();

        var queries = FindNodes<QueryNode>(fileNode.Children);
        queries.Should().NotBeEmpty("Embedded SQL inside list_users should be detected");

        var selectQuery = queries.FirstOrDefault(q => q.QueryText.Contains("SELECT * FROM users", StringComparison.OrdinalIgnoreCase));
        selectQuery.Should().NotBeNull();

        var tableDeps = selectQuery!.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        tableDeps.Should().Contain("users");

        var functions = FindNodes<FunctionNode>(fileNode.Children);
        var listUsersFn = functions.FirstOrDefault(f => f.Name == "list_users");
        listUsersFn.Should().NotBeNull();
        listUsersFn!.Extensions?.GetValueOrDefault("visibility").Should().Be("pub");
    }

    [Test]
    public async Task Test_RustParser_RichSyntaxExtraction()
    {
        const string rustSource = """
            pub enum OrderStatus {
                Pending,
                Shipped(String),
                Delivered { timestamp: u64 },
            }

            pub trait Describable {
                fn describe(&self) -> String;
            }

            pub struct Order {
                pub id: u64,
                pub amount: f64,
                pub status: OrderStatus,
            }

            impl Order {
                pub fn new(id: u64, amount: f64) -> Self {
                    Self { id, amount, status: OrderStatus::Pending }
                }

                pub fn calculate_total(&self) -> f64 {
                    self.amount * 1.1
                }
            }

            impl Describable for Order {
                fn describe(&self) -> String {
                    format!("Order #{}", self.id)
                }
            }

            pub async unsafe fn process_payment(order: &Order) {
                let _ = order.calculate_total();
                println!("Processing payment");
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "rust_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "order.rs");
        await File.WriteAllTextAsync(testFile, rustSource);

        try
        {
            var parser = new RustParser();
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(testFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var types = FindNodes<TypeNode>(fileNode.Children);
            var typeNames = types.Select(t => t.Name).ToList();
            typeNames.Should().Contain("OrderStatus");
            typeNames.Should().Contain("Describable");
            typeNames.Should().Contain("Order");

            var orderType = types.First(t => t.Name == "Order");
            var implRel = orderType.References.FirstOrDefault(r => r.Kind == "IMPLEMENTS");
            implRel.Should().NotBeNull("Order implements Describable");
            implRel!.TargetName.Should().Be("Describable");

            var functions = FindNodes<FunctionNode>(fileNode.Children);
            var fnNames = functions.Select(f => f.Name).ToList();
            fnNames.Should().Contain("new");
            fnNames.Should().Contain("calculate_total");
            fnNames.Should().Contain("describe");
            fnNames.Should().Contain("process_payment");

            var paymentFn = functions.First(f => f.Name == "process_payment");
            paymentFn.Extensions?.GetValueOrDefault("visibility").Should().Be("pub");
            var calls = paymentFn.References.Where(r => r.Kind == "CALLS").Select(r => r.TargetName).ToList();
            calls.Should().Contain("calculate_total");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public void Test_RustModuleResolver_ResolvePaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "rust_mod_test_" + Guid.NewGuid().ToString("N"));
        var srcDir = Path.Combine(tempDir, "src");
        Directory.CreateDirectory(srcDir);

        var libRs = Path.Combine(srcDir, "lib.rs");
        var modelsRs = Path.Combine(srcDir, "models.rs");
        File.WriteAllText(libRs, "mod models;\n");
        File.WriteAllText(modelsRs, "pub struct User;\n");

        try
        {
            var resolvedFile = RustModuleResolver.ResolveModuleFile(libRs, "models");
            resolvedFile.Should().NotBeNull();
            resolvedFile!.EndsWith("models.rs", StringComparison.OrdinalIgnoreCase).Should().BeTrue();

            var modulePath = RustModuleResolver.GetLogicalModulePath(resolvedFile, tempDir);
            modulePath.Should().Be("crate::models");

            var internalImport = RustModuleResolver.ResolveUsePath("crate::models::User", libRs, null);
            internalImport.Should().Be(ImportType.Internal);

            var superImport = RustModuleResolver.ResolveUsePath("super::User", resolvedFile, null);
            superImport.Should().Be(ImportType.Internal);

            var extImport = RustModuleResolver.ResolveUsePath("serde::Serialize", libRs, null);
            extImport.Should().Be(ImportType.External);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static List<T> FindNodes<T>(IEnumerable<IOntologyNode> nodes) where T : IOntologyNode
    {
        var result = new List<T>();
        foreach (var node in nodes)
        {
            if (node is T match)
                result.Add(match);
            if (node.Children.Count > 0)
                result.AddRange(FindNodes<T>(node.Children));
        }
        return result;
    }
}
