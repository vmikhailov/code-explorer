using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Java;
using CodeExplorer.Tests.Shared;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class JavaParserTests
{
    [Test]
    public async Task Test_JavaParser_ProjectDetection_Maven()
    {
        var parser = new JavaParser();

        using var workspace = ParserTestData.PrepareTempWorkspace("Workspaces/JavaMavenProject");

        var dir = workspace.WorkspacePath;
        var files = Directory.GetFiles(dir);

        parser.IsProjectDirectory(dir, files).Should().BeTrue();

        var producedPkg = await parser.GetProducedPackageAsync(dir);
        producedPkg.Should().NotBeNull();
        producedPkg!.Name.Should().Be("com.example:demo-service");
        producedPkg.Version.Should().Be("1.0.0-SNAPSHOT");
        producedPkg.Type.Should().Be("maven");

        var deps = await parser.ParseDependenciesAsync(dir);
        deps.Should().NotBeNull();
        var pkgNames = deps.ExternalPackages.Select(p => p.Name).ToList();
        pkgNames.Should().Contain("org.springframework.boot:spring-boot-starter-web");
        pkgNames.Should().Contain("org.postgresql:postgresql");
        pkgNames.Should().Contain("org.springframework.kafka:spring-kafka");
    }

    [Test]
    public async Task Test_JavaParser_ProjectDetection_Gradle()
    {
        var parser = new JavaParser();

        using var workspace = ParserTestData.PrepareTempWorkspace("Workspaces/JavaGradleProject");

        var dir = workspace.WorkspacePath;
        var files = Directory.GetFiles(dir);

        parser.IsProjectDirectory(dir, files).Should().BeTrue();

        var producedPkg = await parser.GetProducedPackageAsync(dir);
        producedPkg.Should().NotBeNull();
        producedPkg!.Name.Should().Contain("com.example.gradle");
        producedPkg.Version.Should().Be("2.1.0");
        producedPkg.Type.Should().Be("gradle");

        var deps = await parser.ParseDependenciesAsync(dir);
        deps.Should().NotBeNull();
        var pkgNames = deps.ExternalPackages.Select(p => p.Name).ToList();
        pkgNames.Should().Contain("org.springframework.boot:spring-boot-starter-web");
        pkgNames.Should().Contain("org.postgresql:postgresql");
        pkgNames.Should().Contain("redis.clients:jedis");
    }

    [Test]
    public async Task Test_JavaParser_UserController_SyntacticAndSemantic()
    {
        var parser = new JavaParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Java/UserController.java.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();

        var typeNodes = FindNodes<TypeNode>(fileNode.Children);
        typeNodes.Should().NotBeEmpty();
        var userController = typeNodes.FirstOrDefault(c => c.Name == "UserController");
        userController.Should().NotBeNull();

        var functions = FindNodes<FunctionNode>(fileNode.Children);
        var functionNames = functions.Select(m => m.Name).ToList();
        functionNames.Should().Contain("getAllUsers");
        functionNames.Should().Contain("getUserById");
        functionNames.Should().Contain("createUser");
        functionNames.Should().Contain("deleteUser");

        var endpoints = FindNodes<EndpointNode>(fileNode.Children);
        var entryPoints = FindNodes<EntryPointNode>(fileNode.Children);
        (endpoints.Count + entryPoints.Count).Should().BeGreaterThan(0);
        var routeNames = endpoints.Select(e => e.Name).Concat(entryPoints.Select(e => e.Name)).ToList();

        routeNames.Any(r => r is "GET:/api/v1/users" or "GET:/" or "GET:/{id}" or "/api/v1/users").Should().BeTrue();
    }

    [Test]
    public async Task Test_JavaParser_UserRepository_EmbeddedSql()
    {
        var parser = new JavaParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Java/UserRepository.java.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();

        var interfaces = FindNodes<TypeNode>(fileNode.Children);
        interfaces.Should().NotBeEmpty();
        var userRepo = interfaces.FirstOrDefault(i => i.Name == "UserRepository");
        userRepo.Should().NotBeNull();

        var queryNodes = FindNodes<QueryNode>(fileNode.Children);
        queryNodes.Should().NotBeEmpty("Should detect @Query embedded SQL queries");

        var hasSelectUsers = queryNodes.Any(q =>
            q.QueryText.Contains("users", StringComparison.OrdinalIgnoreCase));
        hasSelectUsers.Should().BeTrue();

        var tableDeps = queryNodes.SelectMany(q => q.References).Where(r => r.Kind == "DEPENDS_ON")
            .Select(r => r.TargetName).ToList();
        tableDeps.Should().Contain("users");
    }

    [Test]
    public async Task Test_JavaParser_OrderService_KafkaAndHttp()
    {
        var parser = new JavaParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Java/OrderService.java.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();

        var classNodes = FindNodes<TypeNode>(fileNode.Children);
        (classNodes.Any(c => c.Name == "OrderService")).Should().BeTrue();

        var functions = FindNodes<FunctionNode>(fileNode.Children);
        var functionNames = functions.Select(m => m.Name).ToList();
        functionNames.Should().Contain("placeOrder");
        functionNames.Should().Contain("verifyPayment");
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
