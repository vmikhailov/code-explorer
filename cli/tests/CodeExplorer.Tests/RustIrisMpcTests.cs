using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Rust;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class RustIrisMpcTests
{
    private const string IrisMpcWorkspacePath = @"C:\Work\Personal\iris-mpc";

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
}
