using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.Java;
using CodeExplorer.Tests.Shared;

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

        Assert.That(parser.IsProjectDirectory(dir, files), Is.True);

        var producedPkg = await parser.GetProducedPackageAsync(dir);
        Assert.That(producedPkg, Is.Not.Null);
        Assert.That(producedPkg!.Name, Is.EqualTo("com.example:demo-service"));
        Assert.That(producedPkg.Version, Is.EqualTo("1.0.0-SNAPSHOT"));
        Assert.That(producedPkg.Type, Is.EqualTo("maven"));

        var deps = await parser.ParseDependenciesAsync(dir);
        Assert.That(deps, Is.Not.Null);
        var pkgNames = deps.ExternalPackages.Select(p => p.Name).ToList();
        Assert.That(pkgNames, Contains.Item("org.springframework.boot:spring-boot-starter-web"));
        Assert.That(pkgNames, Contains.Item("org.postgresql:postgresql"));
        Assert.That(pkgNames, Contains.Item("org.springframework.kafka:spring-kafka"));
    }

    [Test]
    public async Task Test_JavaParser_ProjectDetection_Gradle()
    {
        var parser = new JavaParser();
        using var workspace = ParserTestData.PrepareTempWorkspace("Workspaces/JavaGradleProject");
        var dir = workspace.WorkspacePath;
        var files = Directory.GetFiles(dir);

        Assert.That(parser.IsProjectDirectory(dir, files), Is.True);

        var producedPkg = await parser.GetProducedPackageAsync(dir);
        Assert.That(producedPkg, Is.Not.Null);
        Assert.That(producedPkg!.Name, Contains.Substring("com.example.gradle"));
        Assert.That(producedPkg.Version, Is.EqualTo("2.1.0"));
        Assert.That(producedPkg.Type, Is.EqualTo("gradle"));

        var deps = await parser.ParseDependenciesAsync(dir);
        Assert.That(deps, Is.Not.Null);
        var pkgNames = deps.ExternalPackages.Select(p => p.Name).ToList();
        Assert.That(pkgNames, Contains.Item("org.springframework.boot:spring-boot-starter-web"));
        Assert.That(pkgNames, Contains.Item("org.postgresql:postgresql"));
        Assert.That(pkgNames, Contains.Item("redis.clients:jedis"));
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

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        Assert.That(fileNode, Is.Not.Null);

        var typeNodes = FindNodes<TypeNode>(fileNode.Children);
        Assert.That(typeNodes, Is.Not.Empty);
        var userController = typeNodes.FirstOrDefault(c => c.Name == "UserController");
        Assert.That(userController, Is.Not.Null);

        var functions = FindNodes<FunctionNode>(fileNode.Children);
        var functionNames = functions.Select(m => m.Name).ToList();
        Assert.That(functionNames, Contains.Item("getAllUsers"));
        Assert.That(functionNames, Contains.Item("getUserById"));
        Assert.That(functionNames, Contains.Item("createUser"));
        Assert.That(functionNames, Contains.Item("deleteUser"));

        var endpoints = FindNodes<EndpointNode>(fileNode.Children);
        var entryPoints = FindNodes<EntryPointNode>(fileNode.Children);
        Assert.That(endpoints.Count + entryPoints.Count, Is.GreaterThan(0));
        var routeNames = endpoints.Select(e => e.Name).Concat(entryPoints.Select(e => e.Name)).ToList();
        Assert.That(routeNames, Contains.Item("GET:/api/v1/users").Or.Contains("GET:/").Or.Contains("GET:/{id}").Or.Contains("/api/v1/users"));
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

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        Assert.That(fileNode, Is.Not.Null);

        var interfaces = FindNodes<TypeNode>(fileNode.Children);
        Assert.That(interfaces, Is.Not.Empty);
        var userRepo = interfaces.FirstOrDefault(i => i.Name == "UserRepository");
        Assert.That(userRepo, Is.Not.Null);

        var queryNodes = FindNodes<QueryNode>(fileNode.Children);
        Assert.That(queryNodes, Is.Not.Empty, "Should detect @Query embedded SQL queries");

        var hasSelectUsers = queryNodes.Any(q => q.QueryText.Contains("users", StringComparison.OrdinalIgnoreCase));
        Assert.That(hasSelectUsers, Is.True);

        var tableDeps = queryNodes.SelectMany(q => q.References)
            .Where(r => r.Kind == "DEPENDS_ON")
            .Select(r => r.TargetName)
            .ToList();
        Assert.That(tableDeps, Contains.Item("users"));
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

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        var fileNode = syntaxTree.FileNode;
        Assert.That(fileNode, Is.Not.Null);

        var classNodes = FindNodes<TypeNode>(fileNode.Children);
        Assert.That(classNodes.Any(c => c.Name == "OrderService"), Is.True);

        var functions = FindNodes<FunctionNode>(fileNode.Children);
        var functionNames = functions.Select(m => m.Name).ToList();
        Assert.That(functionNames, Contains.Item("placeOrder"));
        Assert.That(functionNames, Contains.Item("verifyPayment"));
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
