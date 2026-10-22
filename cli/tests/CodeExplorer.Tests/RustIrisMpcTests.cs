using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Rust;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class RustIrisMpcTests
{
    private static readonly string IrisMpcWorkspacePath = Environment.GetEnvironmentVariable("IRIS_MPC_PATH") ?? @"../iris-mpc";

    [SetUp]
    public void SetUp()
    {
        if (!Directory.Exists(IrisMpcWorkspacePath))
        {
            Assert.Ignore($"iris-mpc repository not found at {IrisMpcWorkspacePath}");
        }
    }

    [Test]
    public void IrisMpc_WorkspaceDetection_DiscoversAllCrates()
    {
        var rootToml = Path.Combine(IrisMpcWorkspacePath, "Cargo.toml");
        File.Exists(rootToml).Should().BeTrue();

        var manifest = CargoManifestParser.ParseManifest(rootToml);
        manifest.Should().NotBeNull();
        manifest.IsVirtualWorkspace.Should().BeTrue();
        manifest.Workspace.Should().NotBeNull();

        var members = CargoManifestParser.GetWorkspaceMembers(rootToml);
        members.Count.Should().BeGreaterThanOrEqualTo(10);

        var memberNames = members.Select(Path.GetFileName).ToList();
        memberNames.Should().Contain("iris-mpc");
        memberNames.Should().Contain("iris-mpc-bins");
        memberNames.Should().Contain("iris-mpc-common");
        memberNames.Should().Contain("iris-mpc-cpu");
        memberNames.Should().Contain("iris-mpc-gpu");
        memberNames.Should().Contain("iris-mpc-store");
        memberNames.Should().Contain("iris-mpc-utils");

        manifest.Workspace!.WorkspaceDependencies.Should().ContainKey("tokio");
        manifest.Workspace.WorkspaceDependencies.Should().ContainKey("axum");
        manifest.Workspace.WorkspaceDependencies.Should().ContainKey("clap");
    }

    [Test]
    public void IrisMpc_ProjectRoleClassification_ClassifiesCratesCorrectly()
    {
        var parser = new RustParser();

        // 1. iris-mpc-bins contains [[bin]] targets and depends on axum, tonic, clap
        var binsDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins");
        var binsFiles = Directory.GetFiles(binsDir);
        parser.IsProjectDirectory(binsDir, binsFiles).Should().BeTrue();

        var binsProps = parser.ExtractManifestProperties(binsDir, binsFiles);
        binsProps.Should().ContainKey("manifest_type");
        binsProps["manifest_type"].Should().BeOneOf("application", "cli", "service");

        // 2. iris-mpc-common is a core domain/utility library ([lib])
        var commonDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-common");
        var commonFiles = Directory.GetFiles(commonDir);
        parser.IsProjectDirectory(commonDir, commonFiles).Should().BeTrue();

        var commonProps = parser.ExtractManifestProperties(commonDir, commonFiles);
        commonProps["manifest_type"].Should().Be("library");
        commonProps["framework_type"].Should().Be("library");

        // 3. iris-mpc-cpu is a computational library
        var cpuDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-cpu");
        var cpuFiles = Directory.GetFiles(cpuDir);
        parser.IsProjectDirectory(cpuDir, cpuFiles).Should().BeTrue();

        var cpuProps = parser.ExtractManifestProperties(cpuDir, cpuFiles);
        cpuProps["manifest_type"].Should().Be("library");
    }

    [Test]
    public async Task IrisMpc_DependencyResolution_ResolvesLocalAndExternalPackages()
    {
        var parser = new RustParser();
        var binsDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins");

        var deps = await parser.ParseDependenciesAsync(binsDir);
        deps.Should().NotBeNull();

        // Local crate path dependencies
        deps.LocalProjectPaths.Count.Should().BeGreaterThanOrEqualTo(5);
        deps.LocalProjectPaths.Any(p => p.EndsWith("iris-mpc-common", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        deps.LocalProjectPaths.Any(p => p.EndsWith("iris-mpc-cpu", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        deps.LocalProjectPaths.Any(p => p.EndsWith("iris-mpc-store", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        // External crates.io packages
        var extNames = deps.ExternalPackages.Select(p => p.Name).ToList();
        extNames.Should().Contain("axum");
        extNames.Should().Contain("tokio");
        extNames.Should().Contain("clap");
        extNames.Should().Contain("tonic");
        extNames.Should().Contain("sqlx");
    }

    [Test]
    public async Task IrisMpc_SyntacticAST_ParsesCommonCrate()
    {
        var libFile = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-common", "src", "lib.rs");
        File.Exists(libFile).Should().BeTrue();

        var parser = new RustParser();
        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryGraphClient();
        var ctx = new ParsingContext(IrisMpcWorkspacePath, IrisMpcWorkspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(libFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();
        fileNode.Path.Should().Contain("iris-mpc-common");
    }

    [Test]
    public async Task IrisMpc_WorkspaceDependencies_ResolvesLocalPathInheritance()
    {
        var parser = new RustParser();
        var mpcDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc");

        var deps = await parser.ParseDependenciesAsync(mpcDir);
        deps.Should().NotBeNull();

        // iris-mpc-cpu is defined as `iris-mpc-cpu.workspace = true` in iris-mpc/Cargo.toml
        // and resolved via [workspace.dependencies] `iris-mpc-cpu = { path = "./iris-mpc-cpu" }`
        deps.LocalProjectPaths.Any(p => p.EndsWith("iris-mpc-cpu", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Test]
    public async Task IrisMpc_DevDependencies_ExcludedFromLocalProjectPathsForNonTestCrates()
    {
        var parser = new RustParser();
        var mpcDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc");

        var deps = await parser.ParseDependenciesAsync(mpcDir);
        deps.Should().NotBeNull();

        // iris-mpc has `iris-mpc-utils` in [dev-dependencies].
        // For non-test crates, dev-dependencies MUST NOT be in LocalProjectPaths to avoid false cycles.
        deps.LocalProjectPaths.Any(p => p.EndsWith("iris-mpc-utils", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();

        // But iris-mpc-utils has `iris-mpc` in [dependencies], which MUST be present.
        var utilsDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-utils");
        var utilsDeps = await parser.ParseDependenciesAsync(utilsDir);
        utilsDeps.LocalProjectPaths.Any(p => p.EndsWith("iris-mpc", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Test]
    public async Task IrisMpc_StandaloneBinarySubfolders_DetectedAndClassified()
    {
        var parser = new RustParser();
        var reaperDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins", "bin", "retention-reaper");
        var reaperFiles = Directory.GetFiles(reaperDir, "*", SearchOption.AllDirectories);

        parser.IsProjectDirectory(reaperDir, reaperFiles).Should().BeTrue();
        parser.GetProjectName(reaperDir, reaperFiles).Should().Be("retention-reaper");

        var produced = await parser.GetProducedPackageAsync(reaperDir);
        produced.Should().BeNull();

        var reaperProps = parser.ExtractManifestProperties(reaperDir, reaperFiles);
        reaperProps["primary_role"].Should().Be("Worker");
        reaperProps["is_library"].Should().Be("false");

        var anonDir = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins", "bin", "iris-mpc-anon-stats-server");
        var anonFiles = Directory.GetFiles(anonDir, "*", SearchOption.AllDirectories);
        parser.IsProjectDirectory(anonDir, anonFiles).Should().BeTrue();

        var anonProps = parser.ExtractManifestProperties(anonDir, anonFiles);
        anonProps["primary_role"].Should().Be("Service");
        anonProps["is_library"].Should().Be("false");
    }

    [Test]
    public async Task IrisMpc_DatabaseSchemaDetection_DetectsConcreteSchemas()
    {
        var parser = new RustParser();
        ConstantRegistry.Clear();

        // 1. Load node config toml and parse common/config/mod.rs to register constants
        var tomlPath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-utils", "assets", "node-config", "docker", "main-0-node-0.toml");
        if (File.Exists(tomlPath))
        {
            ConfigTestHelper.Load(tomlPath, "iris-mpc-utils");
        }
        var configPath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-common", "src", "config", "mod.rs");
        if (File.Exists(configPath))
        {
            using var configTree = await parser.ParseAsync(configPath, "parent", "ws", IrisMpcWorkspacePath);
            AstConstantExtractor.ExtractAndRegister(configTree, "iris-mpc-common");
            var schemas = ResourceSemanticAnalyzer.DetectDeclaredSchemas(null, configTree.FileNode, configTree, "relational");
            TestContext.Out.WriteLine($"iris-mpc-common schemas: {string.Join(", ", schemas)}");
            schemas.Should().Contain(s => s.Equals("SMPC", StringComparison.OrdinalIgnoreCase));
            schemas.Should().Contain(s => s.Equals("SMPC_hnsw", StringComparison.OrdinalIgnoreCase));
            schemas.Should().Contain(s => s.Equals("anon_stats_mpc", StringComparison.OrdinalIgnoreCase));
        }

        // 2. Parse store/src/lib.rs (Library project should NOT invent a project-named schema or leak test schemas)
        var storePath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-store", "src", "lib.rs");
        if (File.Exists(storePath))
        {
            using var storeTree = await parser.ParseAsync(storePath, "parent", "ws", IrisMpcWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(storeTree, "ws", IrisMpcWorkspacePath, null, "iris-mpc-store", false, "Library");
            var schemas = ResourceSemanticAnalyzer.DetectDeclaredSchemas(null, storeTree.FileNode, storeTree, "relational");
            TestContext.Out.WriteLine($"iris-mpc-store schemas: {string.Join(", ", schemas)}");
            schemas.Should().BeEmpty();

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(IrisMpcWorkspacePath, IrisMpcWorkspacePath, client, channel);
            ctx.SemanticStructure = new SemanticStructureNode("ws:sem", "sem", IrisMpcWorkspacePath);
            var projNode = new ProjectNode("proj:iris-mpc-store", "iris-mpc-store", "iris-mpc-store", "rust", "Library", true, new Dictionary<string, string> { ["role"] = "Library", ["is_library"] = "true" });
            await ResourceSemanticAnalyzer.EnrichAsync(storeTree, projNode, parser, ctx);
            ctx.SemanticStructure.Children.Should().NotContain(c => c.Id.Contains("iris-mpc-store", StringComparison.OrdinalIgnoreCase) && c is DatabaseNode);
        }

        // 3. Parse reaper
        var reaperPath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins", "bin", "retention-reaper", "main.rs");
        if (File.Exists(reaperPath))
        {
            using var reaperTree = await parser.ParseAsync(reaperPath, "parent", "ws", IrisMpcWorkspacePath);
            AstConstantExtractor.ExtractAndRegister(reaperTree, "retention-reaper");
            var schemas = ResourceSemanticAnalyzer.DetectDeclaredSchemas(null, reaperTree.FileNode, reaperTree, "relational");
            TestContext.Out.WriteLine($"retention-reaper schemas: {string.Join(", ", schemas)}");
            schemas.Should().BeEquivalentTo(["public"]);
        }

        // 4. Parse anon stats server
        var anonPath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins", "bin", "iris-mpc-anon-stats-server", "main.rs");
        if (File.Exists(anonPath))
        {
            using var anonTree = await parser.ParseAsync(anonPath, "parent", "ws", IrisMpcWorkspacePath);
            AstConstantExtractor.ExtractAndRegister(anonTree, "iris-mpc-anon-stats-server");
            var schemas = ResourceSemanticAnalyzer.DetectDeclaredSchemas(null, anonTree.FileNode, anonTree, "relational");
            TestContext.Out.WriteLine($"iris-mpc-anon-stats-server schemas: {string.Join(", ", schemas)}");
            schemas.Should().BeEquivalentTo(["anon_stats_mpc"]);
        }

        // 5. Parse iris-mpc GPU server (iris-mpc-bins/bin/iris-mpc/server.rs)
        var serverPath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc-bins", "bin", "iris-mpc", "server.rs");
        if (File.Exists(serverPath))
        {
            using var serverTree = await parser.ParseAsync(serverPath, "parent", "ws", IrisMpcWorkspacePath);
            AstConstantExtractor.ExtractAndRegister(serverTree, "iris-mpc");
            var schemas = ResourceSemanticAnalyzer.DetectDeclaredSchemas(null, serverTree.FileNode, serverTree, "relational");
            TestContext.Out.WriteLine($"iris-mpc-bins server.rs schemas: {string.Join(", ", schemas)}");
            schemas.Should().Contain(s => s.Equals("SMPC", StringComparison.OrdinalIgnoreCase));
            schemas.Should().Contain(s => s.Equals("anon_stats_mpc", StringComparison.OrdinalIgnoreCase));
        }

        // 6. Parse iris-mpc HNSW server (iris-mpc/src/server/mod.rs)
        var hnswServerPath = Path.Combine(IrisMpcWorkspacePath, "iris-mpc", "src", "server", "mod.rs");
        if (File.Exists(hnswServerPath))
        {
            using var hnswServerTree = await parser.ParseAsync(hnswServerPath, "parent", "ws", IrisMpcWorkspacePath);
            AstConstantExtractor.ExtractAndRegister(hnswServerTree, "iris-mpc");
            var schemas = ResourceSemanticAnalyzer.DetectDeclaredSchemas(null, hnswServerTree.FileNode, hnswServerTree, "relational");
            TestContext.Out.WriteLine($"iris-mpc HNSW server/mod.rs schemas: {string.Join(", ", schemas)}");
            schemas.Should().Contain(s => s.Equals("SMPC_hnsw", StringComparison.OrdinalIgnoreCase));
            schemas.Should().Contain(s => s.Equals("SMPC", StringComparison.OrdinalIgnoreCase));
            schemas.Should().Contain(s => s.Equals("anon_stats_mpc", StringComparison.OrdinalIgnoreCase));
        }
    }
}
