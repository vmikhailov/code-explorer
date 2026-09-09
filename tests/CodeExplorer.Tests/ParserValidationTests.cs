using System.Threading.Channels;
using NUnit.Framework;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;

namespace CodeExplorer.Tests;

[TestFixture]
public class ParserValidationTests
{
    [Test]
    public async Task Test_TypeScriptParser_WithExamples()
    {
        var parser = new TypeScriptParser();
        var filesToTest = new[]
        {
            "SingleFiles/TypeScript/cron.service.ts.test",
            "SingleFiles/TypeScript/calibrate-min-roi.service.ts.test",
            "SingleFiles/TypeScript/config.models.ts.test"
        };

        foreach (var relativeFile in filesToTest)
        {
            using var tempFile = ParserTestData.GetPreparedFile(relativeFile);
            var workspacePath = tempFile.DirectoryPath;
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryMemgraphClient();
            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

            using var syntaxTree =
                await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
            Assert.That(fileNode, Is.Not.Null);
            Assert.That(fileNode.Children, Is.Not.Empty);
        }
    }

    [Test]
    public async Task Test_TypeScriptParser_EmbeddedSql()
    {
        var parser = new TypeScriptParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/TypeScript/embedded_sql.ts.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        Assert.That(fileNode, Is.Not.Null);

        var queryNodes = FindQueryNodes(fileNode.Children);
        Assert.That(queryNodes, Is.Not.Empty, "Should have detected the embedded SQL query");

        var sqlQuery = queryNodes[0];
        Assert.That(sqlQuery.Name, Is.EqualTo("DELETE Query"));

        Assert.That(sqlQuery.QueryText,
            Contains.Substring("DELETE FROM tracking.data_all_leads WHERE bundle_id in"));

        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        Assert.That(dependsOn, Contains.Item("tracking"));
        Assert.That(dependsOn, Contains.Item("data_all_leads"));

        AssertSqlHierarchy(sqlQuery, "default", "tracking", "data_all_leads");
    }

    [Test]
    public async Task Test_CSharpParser_EmbeddedSql()
    {
        var parser = new CSharpParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/CSharp/embedded_sql.cs.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        var queryNodes = FindQueryNodes(fileNode.Children);
        Assert.That(queryNodes, Is.Not.Empty);
        var sqlQuery = queryNodes[0];
        Assert.That(sqlQuery.Name, Is.EqualTo("SELECT Query"));
        Assert.That(sqlQuery.QueryText, Contains.Substring("SELECT id, name FROM users"));
        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        Assert.That(dependsOn, Contains.Item("users"));

        AssertSqlHierarchy(sqlQuery, "default", "dbo", "users");
    }

    [Test]
    public async Task Test_PythonParser_EmbeddedSql()
    {
        var parser = new PythonParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Python/embedded_sql.py.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        var queryNodes = FindQueryNodes(fileNode.Children);
        Assert.That(queryNodes, Is.Not.Empty);
        var sqlQuery = queryNodes[0];
        Assert.That(sqlQuery.Name, Is.EqualTo("INSERT Query"));
        Assert.That(sqlQuery.QueryText, Contains.Substring("INSERT INTO logs"));
        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        Assert.That(dependsOn, Contains.Item("logs"));

        AssertSqlHierarchy(sqlQuery, "default", "dbo", "logs");
    }

    [Test]
    public async Task Test_GoParser_EmbeddedSql()
    {
        var parser = new GoParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Go/embedded_sql.go.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        var queryNodes = FindQueryNodes(fileNode.Children);
        Assert.That(queryNodes, Is.Not.Empty);
        var sqlQuery = queryNodes[0];
        Assert.That(sqlQuery.Name, Is.EqualTo("UPDATE Query"));
        Assert.That(sqlQuery.QueryText, Contains.Substring("UPDATE transactions SET status"));
        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName).ToList();
        Assert.That(dependsOn, Contains.Item("transactions"));

        AssertSqlHierarchy(sqlQuery, "default", "dbo", "transactions");
    }

    private void AssertSqlHierarchy(QueryNode queryNode, string expectedDb, string expectedSchema, string expectedTable)
    {
        var dbNode = queryNode.Children.OfType<DatabaseNode>()
            .FirstOrDefault(d => d.Name.Equals(expectedDb, StringComparison.OrdinalIgnoreCase));
        Assert.That(dbNode, Is.Not.Null, $"Should contain DB node: {expectedDb}");
        Assert.That(dbNode.DbType, Is.EqualTo("relational"));

        var schemaNode = dbNode.Children.OfType<DataSetNode>()
            .FirstOrDefault(s => s.Name.Equals(expectedSchema, StringComparison.OrdinalIgnoreCase));
        Assert.That(schemaNode, Is.Not.Null, $"Should contain Schema/DataSet node: {expectedSchema}");

        var tableNode = schemaNode.Children.OfType<TableNode>()
            .FirstOrDefault(t => t.Name.Equals(expectedTable, StringComparison.OrdinalIgnoreCase));
        Assert.That(tableNode, Is.Not.Null, $"Should contain Table node: {expectedTable}");
    }

    [Test]
    public void Test_NestedSqlParser_CleanQueryText_NestedQuotes()
    {
        var input = "\"'SELECT * FROM my_table'\"";
        var cleaned = NestedSqlParser.CleanQueryText(input);
        Assert.That(cleaned, Is.EqualTo("SELECT * FROM my_table"));
    }

    [Test]
    public async Task Test_TypeScriptParser_EmbeddedSql_ComplexTemplate()
    {
        var parser = new TypeScriptParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/TypeScript/complex_template.ts.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;

        var queryNodes = FindQueryNodes(fileNode.Children);
        Assert.That(queryNodes, Is.Not.Empty);
        var sqlQuery = queryNodes[0];
        Assert.That(sqlQuery.Name, Is.EqualTo("SELECT Query"));

        // Since tableName is a variable, it should be skipped and no database node hierarchy should be created for it.
        var hasDbNode = sqlQuery.Children.OfType<DatabaseNode>().Any();

        Assert.That(hasDbNode, Is.False,
            "Should have skipped tableName because it is a template variable placeholder.");
    }

    private List<QueryNode> FindQueryNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<QueryNode>();

        foreach (var node in nodes)
        {
            if (node is QueryNode q) result.Add(q);
            result.AddRange(FindQueryNodes(node.Children));
        }

        return result;
    }

    private List<EndpointNode> FindEndpointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EndpointNode>();

        foreach (var node in nodes)
        {
            if (node is EndpointNode e) result.Add(e);
            result.AddRange(FindEndpointNodes(node.Children));
        }

        return result;
    }

    private List<EntryPointNode> FindEntryPointNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<EntryPointNode>();

        foreach (var node in nodes)
        {
            if (node is EntryPointNode e) result.Add(e);
            result.AddRange(FindEntryPointNodes(node.Children));
        }

        return result;
    }

    private List<ExternalServiceNode> FindExternalServiceNodes(IEnumerable<IOntologyNode> nodes)
    {
        var result = new List<ExternalServiceNode>();

        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode e) result.Add(e);
            result.AddRange(FindExternalServiceNodes(node.Children));
        }

        return result;
    }

    private List<Reference> FindReferences(IEnumerable<IOntologyNode> nodes)
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
    public async Task Test_CSharpParser_ApiIngressEgress()
    {
        var parser = new CSharpParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/CSharp/api_ingress_egress.cs.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;

        var endpoints = FindEndpointNodes(fileNode.Children);
        Assert.That(endpoints, Is.Not.Empty);
        var ep = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("charge"));
        Assert.That(ep, Is.Not.Null);
        Assert.That(ep.HttpMethod, Is.EqualTo("POST"));

        var externalServices = FindExternalServiceNodes(fileNode.Children);
        Assert.That(externalServices, Is.Not.Empty);
        var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
        Assert.That(es, Is.Not.Null);
        Assert.That(es.Protocol, Is.EqualTo("http"));
    }

    [Test]
    public async Task Test_TypeScriptParser_ApiIngressEgress()
    {
        var parser = new TypeScriptParser();
        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/TypeScript/api_ingress_egress.ts.test");
        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree =
            await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;

        var endpoints = FindEndpointNodes(fileNode.Children);
        Assert.That(endpoints, Is.Not.Empty);
        var ep = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("charge"));
        Assert.That(ep, Is.Not.Null);
        Assert.That(ep.HttpMethod, Is.EqualTo("POST"));

        var entryPoints = FindEntryPointNodes(fileNode.Children);
        var wsEp = entryPoints.FirstOrDefault(e => e.EntryType == "queue-listener");
        Assert.That(wsEp, Is.Not.Null);
        Assert.That(wsEp.Name, Is.EqualTo("ping"));

        var externalServices = FindExternalServiceNodes(fileNode.Children);
        Assert.That(externalServices, Is.Not.Empty);
        var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
        Assert.That(es, Is.Not.Null);
        Assert.That(es.Protocol, Is.EqualTo("http"));
    }



    [Test]
    public async Task Test_SemanticAnalysisAndOntologyEnrichment()
    {
        using var ws = ParserTestData.PrepareTempWorkspace("Workspaces/SemanticEnrichment");
        var tempWorkspace = ws.WorkspacePath;

        // Empty folder in Project A to verify pruning
        var emptySubDir = Path.Combine(tempWorkspace, "ProjectA", "EmptyFolder").Replace('\\', '/');
        Directory.CreateDirectory(emptySubDir);

        // Setup parsing context
        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();

        var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
        ctx.WorkspaceId = "1";
        // Register CSharp parser
        WorkspaceIndexer.Register(new CSharpParser());
        var l1Result = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2Result = await new Layer2ProjectParser().ParseAsync(l1Result, ctx);
        var l3Result = await new Layer3SyntacticParser().ParseAsync(l2Result, ctx);
        var l4Result = await new Layer4SemanticParser().ParseAsync(l3Result, ctx);

        var workspaceNode = l1Result.Workspace;

        var projectsStructure = workspaceNode.Children.OfType<ProjectsStructureNode>().FirstOrDefault();
        Assert.That(projectsStructure, Is.Not.Null);

        var filesStructure = workspaceNode.Children.OfType<FilesStructureNode>().FirstOrDefault();
        Assert.That(filesStructure, Is.Not.Null);

        // Before pruning, ProjectB and EmptyFolder are in the tree
        Assert.That(projectsStructure.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB"), Is.True);

        var projectA = projectsStructure.Children.FirstOrDefault(c => c is ProjectNode pn && pn.Name == "ProjectA") as ProjectNode;
        Assert.That(projectA, Is.Not.Null);

        var projectAFolder = filesStructure.Children.OfType<FolderNode>().FirstOrDefault(f => f.Name == "ProjectA");
        Assert.That(projectAFolder, Is.Not.Null);
        Assert.That(projectAFolder.Children.Any(c => c is FolderNode pfn && pfn.Name == "EmptyFolder"), Is.True);

        // 2. Perform pruning
        OntologyPruner.PruneEmptyFolders(workspaceNode);

        // After pruning:
        // ProjectB is removed because it is an empty project
        Assert.That(projectsStructure.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB"), Is.False);

        // EmptyFolder is removed
        var folderA =
            projectAFolder.Children.FirstOrDefault(c => c is FolderNode pfn && pfn.Name == "EmptyFolder");
        Assert.That(folderA, Is.Null);

        // Verify project framework detection
        Assert.That(projectA.Extensions, Is.Not.Null);
        Assert.That(projectA.Extensions.ContainsKey("framework"), Is.True);
        Assert.That(projectA.Extensions["framework"], Is.EqualTo("ASP.NET Core"));

        // Verify SemanticStructure node grouping
        var semanticNode = workspaceNode.Children.OfType<SemanticStructureNode>().FirstOrDefault();
        Assert.That(semanticNode, Is.Not.Null);

        // Verify external packages are inside ProjectNode directly (Layer 1)
        var extPackages = projectA.Children.OfType<PackageNode>().Where(p => p.Name != "ProjectA").ToList();
        Assert.That(extPackages, Has.Count.EqualTo(3));
        Assert.That(extPackages.Any(p => p.Name == "Dapper"), Is.True);
        Assert.That(extPackages.Any(p => p.Name == "Stripe.net"), Is.True);
        Assert.That(extPackages.Any(p => p.Name == "Microsoft.AspNetCore.App"), Is.True);

        // Verify semanticNode does NOT contain those external packages as children
        var semPackages = semanticNode.Children.OfType<PackageNode>().ToList();
        Assert.That(semPackages.Any(p => p.Name == "Dapper"), Is.False);
        Assert.That(semPackages.Any(p => p.Name == "Stripe.net"), Is.False);
        Assert.That(semPackages.Any(p => p.Name == "Microsoft.AspNetCore.App"), Is.False);

        // Verify projectA contains the produced package directly as child (Layer 1)
        var directPackages = projectA.Children.OfType<PackageNode>().ToList();
        Assert.That(directPackages.Any(p => p.Name == "ProjectA"), Is.True);

        var fileNode = projectAFolder.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "Repository.cs");
        Assert.That(fileNode, Is.Not.Null);

        // Check Repository.cs extensions (file-level extensions are removed)
        if (fileNode.Extensions != null)
        {
            Assert.That(fileNode.Extensions.ContainsKey("db_type"), Is.False);
            Assert.That(fileNode.Extensions.ContainsKey("cloud_service"), Is.False);
        }

        // Check if DatabaseNode child was added at the project level (under SemanticStructureNode)
        var dbNode = semanticNode.Children.OfType<DatabaseNode>().FirstOrDefault();
        Assert.That(dbNode, Is.Not.Null);
        Assert.That(dbNode.Name, Is.EqualTo("Dapper"));
        Assert.That(dbNode.DbType, Is.EqualTo("relational"));

        // Check if CloudServiceNode child was added at the project level (under SemanticStructureNode)
        var cloudNode = semanticNode.Children.OfType<CloudServiceNode>().FirstOrDefault();
        Assert.That(cloudNode, Is.Not.Null);
        Assert.That(cloudNode.Name, Is.EqualTo("Stripe"));

        // Check that file-to-library relationships are created in the context
        var usesDb =
            ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_DB");
        Assert.That(usesDb, Is.Not.Null);
        Assert.That(usesDb.To, Is.EqualTo(dbNode.Id));

        var usesCloud =
            ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_CLOUD");
        Assert.That(usesCloud, Is.Not.Null);
        Assert.That(usesCloud.To, Is.EqualTo(cloudNode.Id));

        // Check member nodes under TypeNode (Repository)
        var classNode = fileNode.Children.OfType<TypeNode>().FirstOrDefault();
        Assert.That(classNode, Is.Not.Null);

        var variables = classNode.Children.OfType<MemberNode>().ToList();
        Assert.That(variables.Any(v => v.Name == "CONNECTION_STRING_URL"), Is.True);
        Assert.That(variables.Any(v => v.Name == "MAX_RETRIES"), Is.True);
        Assert.That(variables.Any(v => v.Name == "timeoutSeconds"), Is.False); // local non-config is ignored

        var connStrVar = variables.First(v => v.Name == "CONNECTION_STRING_URL");
        Assert.That(connStrVar.Extensions, Is.Not.Null);
        Assert.That(connStrVar.Extensions["variable_type"], Contains.Substring("config"));
        Assert.That(connStrVar.Extensions["variable_type"], Contains.Substring("constant"));
    }

    [Test]
    public async Task Test_NewParserFeatures()
    {
        using var ws = ParserTestData.PrepareTempWorkspace("Workspaces/NewParserFeatures");
        var tempWorkspace = ws.WorkspacePath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
        ctx.WorkspaceId = "1";

        // 1. Python Parser - Flask & HTTP calls
        var pythonParser = new PythonParser();
        var pyFile = ws.GetFilePath("app.py");

        using var pySyntax =
            await pythonParser.ParseAsync(pyFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(pySyntax, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var pyNode = pySyntax.FileNode;
        Assert.That(pyNode, Is.Not.Null);

        var pyEndpoints = FindEndpointNodes(pyNode.Children);
        Assert.That(pyEndpoints, Has.Count.EqualTo(1));
        Assert.That(pyEndpoints[0].RouteTemplate, Is.EqualTo("/charge"));
        Assert.That(pyEndpoints[0].HttpMethod, Is.EqualTo("POST"));

        var pyExtServices = FindExternalServiceNodes(pyNode.Children);
        Assert.That(pyExtServices, Has.Count.EqualTo(1));
        Assert.That(pyExtServices[0].Name, Is.EqualTo("api.stripe.com"));

        // Verify reference from process_payment function to Endpoint POST:/charge is collected
        var pyRefs = FindReferences(pyNode.Children);
        Assert.That(pyRefs.Any(r => r.TargetName == "POST /charge" && r.Kind == "IMPLEMENTS"), Is.True);

        // 2. Go Parser - Gin & HTTP Get calls
        var goParser = new GoParser();
        var goFile = ws.GetFilePath("main.go");

        using var goSyntax =
            await goParser.ParseAsync(goFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(goSyntax, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var goNode = goSyntax.FileNode;
        Assert.That(goNode, Is.Not.Null);

        var goEndpoints = FindEndpointNodes(goNode.Children);
        Assert.That(goEndpoints, Has.Count.EqualTo(1));
        Assert.That(goEndpoints[0].RouteTemplate, Is.EqualTo("/api/v1/users"));
        Assert.That(goEndpoints[0].HttpMethod, Is.EqualTo("GET"));

        // Verify Go references
        var goRefs = FindReferences(goNode.Children);

        Assert.That(
            goRefs.Any(r =>
                r.TargetName == "GET /api/v1/users" && r.Kind == "IMPLEMENTS" && r.ScopeSymbolId == "GetUsers"),
            Is.True);

        // 3. SQL Parser - CREATE OR REPLACE PROCEDURE, IF NOT EXISTS, backticks
        var sqlParser = new Parser.SQL.SqlParser();
        var sqlFile = ws.GetFilePath("sp.sql");

        using var sqlSyntax =
            await sqlParser.ParseAsync(sqlFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var sqlNode = sqlSyntax.FileNode;
        Assert.That(sqlNode, Is.Not.Null);

        // Schema and DB hierarchy check
        var dbNode = sqlNode.Children.OfType<DatabaseNode>().FirstOrDefault();
        Assert.That(dbNode, Is.Not.Null);
        Assert.That(dbNode.DbType, Is.EqualTo("relational"));

        var schemaNode = dbNode.Children.OfType<DataSetNode>().FirstOrDefault(s => s.Name == "my_schema");
        Assert.That(schemaNode, Is.Not.Null);

        var procNode = schemaNode.Children.OfType<ProcedureNode>().FirstOrDefault(p => p.Name == "my_proc");
        Assert.That(procNode, Is.Not.Null);

        var tableNode = schemaNode.Children.OfType<TableNode>().FirstOrDefault(t => t.Name == "my_table");
        Assert.That(tableNode, Is.Not.Null);

        var queryNode = procNode.Children.OfType<QueryNode>().FirstOrDefault();
        Assert.That(queryNode, Is.Not.Null);
        Assert.That(queryNode.References.Any(r => r.TargetName == "another_proc" && r.Kind == "CALLS"), Is.True);
    }

    [Test]
    public async Task Test_SemanticModel_DbTypeMapping()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "db_type_mapping_test_workspace_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryMemgraphClient();
            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";
            ctx.SemanticStructure = new SemanticStructureNode("1:semantic_structure", "SemanticStructure", tempWorkspace);

            // Test C# Semantic Analyzer with relational (EntityFramework)
            // Test C# Semantic Model with relational (EntityFramework)
            var csProj = new ProjectNode("cs_project", "cs_project", "cs_project", "csharp");
            var csFile = new FileNode("cs_file", "Repository.cs", "Repository.cs", tempWorkspace + "/Repository.cs");
            csProj.Children.Add(csFile);
            var csParser = new CSharpParser();

            var csSyntaxTree = new SyntaxTree(csFile.FullPath, csFile.Path, null, null, null, csFile, csParser,
                [new RawImport("Microsoft.EntityFrameworkCore", "Repository.cs") { Type = ImportType.External }], [],
                []);
            var csModel = csParser.GetSyntaxEnricher(csSyntaxTree);
            await csModel.EnrichAsync(csProj, ctx);

            var csDbGroup = ctx.SemanticStructure;
            Assert.That(csDbGroup, Is.Not.Null);
            var csDbNode = csDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Microsoft.EntityFrameworkCore");
            Assert.That(csDbNode, Is.Not.Null);
            Assert.That(csDbNode.Name, Is.EqualTo("Microsoft.EntityFrameworkCore"));
            Assert.That(csDbNode!.DbType, Is.EqualTo("relational"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == csFile.Id && r.To == csDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test C# Semantic Model with graph (Neo4j.Driver)
            var csGraphProj = new ProjectNode("cs_graph_project", "cs_graph_project", "cs_graph_project", "csharp");
            var csGraphFile = new FileNode("cs_graph_file", "MemgraphClient.cs", "MemgraphClient.cs", tempWorkspace + "/MemgraphClient.cs");
            csGraphProj.Children.Add(csGraphFile);

            var csGraphSyntaxTree = new SyntaxTree(csGraphFile.FullPath, csGraphFile.Path, null, null, null, csGraphFile, csParser,
                [new RawImport("Neo4j.Driver", "MemgraphClient.cs") { Type = ImportType.External }], [],
                []);
            var csGraphModel = csParser.GetSyntaxEnricher(csGraphSyntaxTree);
            await csGraphModel.EnrichAsync(csGraphProj, ctx);

            var csGraphDbGroup = ctx.SemanticStructure;
            Assert.That(csGraphDbGroup, Is.Not.Null);
            var csGraphDbNode = csGraphDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Neo4j");
            Assert.That(csGraphDbNode, Is.Not.Null);
            Assert.That(csGraphDbNode.Name, Is.EqualTo("Neo4j"));
            Assert.That(csGraphDbNode!.DbType, Is.EqualTo("graph"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == csGraphFile.Id && r.To == csGraphDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test TypeScript Semantic Model with document (mongoose)
            var tsProj = new ProjectNode("ts_project", "ts_project", "ts_project", "typescript");
            var tsFile = new FileNode("ts_file", "index.ts", "index.ts", tempWorkspace + "/index.ts");
            tsProj.Children.Add(tsFile);
            var tsParser = new TypeScriptParser();

            var tsSyntaxTree = new SyntaxTree(tsFile.FullPath, tsFile.Path, null, null, null, tsFile, tsParser,
                [new RawImport("mongoose", "index.ts") { Type = ImportType.External }], [], []);
            var tsModel = tsParser.GetSyntaxEnricher(tsSyntaxTree);
            await tsModel.EnrichAsync(tsProj, ctx);

            var tsDbGroup = ctx.SemanticStructure;
            Assert.That(tsDbGroup, Is.Not.Null);
            var tsDbNode = tsDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "MongoDB");
            Assert.That(tsDbNode, Is.Not.Null);
            Assert.That(tsDbNode.Name, Is.EqualTo("MongoDB"));
            Assert.That(tsDbNode!.DbType, Is.EqualTo("document"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == tsFile.Id && r.To == tsDbNode.Id && r.Kind == "USES_DB"), Is.True);

            // Test Python Semantic Model with keyvalue (redis)
            var pyProj = new ProjectNode("py_project", "py_project", "py_project", "python");
            var pyFile = new FileNode("py_file", "main.py", "main.py", tempWorkspace + "/main.py");
            pyProj.Children.Add(pyFile);
            var pyParser = new PythonParser();

            var pySyntaxTree = new SyntaxTree(pyFile.FullPath, pyFile.Path, null, null, null, pyFile, pyParser,
                [new RawImport("redis", "main.py") { Type = ImportType.External }], [], []);
            var pyModel = pyParser.GetSyntaxEnricher(pySyntaxTree);
            await pyModel.EnrichAsync(pyProj, ctx);

            var pyDbGroup = ctx.SemanticStructure;
            Assert.That(pyDbGroup, Is.Not.Null);
            var pyDbNode = pyDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Redis");
            Assert.That(pyDbNode, Is.Not.Null);
            Assert.That(pyDbNode.Name, Is.EqualTo("Redis"));
            Assert.That(pyDbNode!.DbType, Is.EqualTo("keyvalue"));

            Assert.That(
                ctx.GlobalProjectDependencies.Any(r =>
                    r.From == pyFile.Id && r.To == pyDbNode.Id && r.Kind == "USES_DB"), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, true);
            }
        }
    }

    [Test]
    public async Task Test_LibraryParsers_CSharpAndTS()
    {
        using var ws = ParserTestData.PrepareTempWorkspace("Workspaces/LibraryParsers");
        var tempWorkspace = ws.WorkspacePath;

        var channel = Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
        ctx.WorkspaceId = "1";

        // 1. C# file parsing test (Dapper and Flurl)
        var csFilePath = ws.GetFilePath("Service.cs");
        var csFileParser = new CSharpParser();

        using var csSyntaxTree = await SyntaxTree.ParseAsync(csFilePath, "Service.cs", "1", csFileParser,
            ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(csSyntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var csFileNode = csSyntaxTree.FileNode;

        // Verify Dapper query extraction
        var csQueries = FindQueryNodes([csFileNode]);
        var dapperNode = csQueries.FirstOrDefault(q => q.Name.Contains("SELECT"));
        Assert.That(dapperNode, Is.Not.Null);
        Assert.That(dapperNode.Name, Is.EqualTo("SELECT Query: SELECT name FROM users WHERE id = @id"));

        // Verify Flurl external service extraction
        var csExtServices = FindExternalServiceNodes([csFileNode]);
        var flurlNode = csExtServices.FirstOrDefault();
        Assert.That(flurlNode, Is.Not.Null);
        Assert.That(flurlNode.Name, Is.EqualTo("api.github.com"));

        // 2. TS file parsing test (Mongoose and Redis)
        var tsFilePath = ws.GetFilePath("app.ts");
        var tsFileParser = new TypeScriptParser();

        using var tsSyntaxTree = await SyntaxTree.ParseAsync(tsFilePath, "app.ts", "1", tsFileParser,
            ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(tsSyntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var tsFileNode = tsSyntaxTree.FileNode;

        var tsQueries = FindQueryNodes([tsFileNode]);

        // Verify Mongoose model & query extraction
        var modelNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Mongoose Model"));
        Assert.That(modelNode, Is.Not.Null);
        Assert.That(modelNode.Name, Is.EqualTo("Mongoose Model: Product"));

        var findNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Mongoose: Product.find"));
        Assert.That(findNode, Is.Not.Null);

        // Verify Redis query extraction
        var redisNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Redis: client.set"));
        Assert.That(redisNode, Is.Not.Null);
    }

    [Test]
    public void Test_LibraryTrieRegistry_Matching()
    {
        var parserNest = new GenericLibraryParser("nestjs", "NestJS", "framework", ["@nestjs/*"]);
        var parserFirebaseGeneric = new GenericLibraryParser("firebase", "Firebase", "cloud", ["firebase*"]);

        var parserFirebaseSpecific =
            new GenericLibraryParser("firebaseadmin", "FirebaseAdmin", "cloud", ["firebase-admin"]);
        var parserSql = new GenericLibraryParser("sqlclient", "SqlClient", "db", ["System.Data"]);
        var parserGoogleCloud = new GenericLibraryParser("gcp", "GCP", "cloud", ["Google.Cloud."]);

        var registry = new LibraryTrieRegistry([
            parserNest,
            parserFirebaseGeneric,
            parserFirebaseSpecific,
            parserSql,
            parserGoogleCloud
        ]);

        // Scoped wildcard /*
        Assert.That(registry.Match("@nestjs/common"), Is.SameAs(parserNest));
        Assert.That(registry.Match("@nestjs/core"), Is.SameAs(parserNest));
        Assert.That(registry.Match("@nestjs"), Is.SameAs(parserNest));

        // Prefix priority matching
        Assert.That(registry.Match("firebase-admin"), Is.SameAs(parserFirebaseSpecific));
        Assert.That(registry.Match("firebase"), Is.SameAs(parserFirebaseGeneric));

        // Fallback namespace match
        Assert.That(registry.Match("System.Data.SqlClient"), Is.SameAs(parserSql));
        Assert.That(registry.Match("Google.Cloud.Translation"), Is.SameAs(parserGoogleCloud));
        // Unmatched
        Assert.That(registry.Match("stripe"), Is.Null);
    }

    [Test]
    public async Task Test_ConcurrentIndexingTasks()
    {
        var dbClient = new InMemoryMemgraphClient();
        var indexer = new WorkspaceIndexer(dbClient);
        var taskManager = new IndexingTaskManager(indexer);
        // Register CSharp parser if not already registered
        WorkspaceIndexer.Register(new CSharpParser());

        using var ws = ParserTestData.PrepareTempWorkspace("Workspaces/ConcurrentIndexing");
        var dir1 = Path.Combine(ws.WorkspacePath, "Project1").Replace('\\', '/');
        var dir2 = Path.Combine(ws.WorkspacePath, "Project2").Replace('\\', '/');

        // 1. Start task 1
        var taskId1 = taskManager.StartIndex(dir1, dir1, clear: false, out var msg1);
        Assert.That(taskId1, Is.Not.Null);
        Assert.That(msg1, Contains.Substring("started"));

        // 2. Start task 2 on same directory -> should fail with conflict
        var taskIdConflict = taskManager.StartIndex(dir1, dir1, clear: false, out var msgConflict);
        Assert.That(taskIdConflict, Is.Null);
        Assert.That(msgConflict, Contains.Substring("already running"));

        // 3. Start task 2 on different directory -> should succeed concurrently
        var taskId2 = taskManager.StartIndex(dir2, dir2, clear: false, out var msg2);
        Assert.That(taskId2, Is.Not.Null);
        Assert.That(msg2, Contains.Substring("started"));

        // 4. Check status of both tasks
        var status1 = taskManager.GetStatus(taskId1);
        var status2 = taskManager.GetStatus(taskId2);
        Assert.That(status1, Is.Not.Null);
        Assert.That(status2, Is.Not.Null);
        Assert.That(status1.State, Is.EqualTo("Running").Or.EqualTo("Completed"));
        Assert.That(status2.State, Is.EqualTo("Running").Or.EqualTo("Completed"));

        // 5. Test stopping task 1 specifically
        var stopSuccess = taskManager.StopIndex(taskId1, out var stopMsg);
        Assert.That(stopSuccess, Is.True);
        Assert.That(stopMsg, Contains.Substring("Stop request sent"));

        // Wait for tasks to complete/cancel
        for (int i = 0; i < 50; i++)
        {
            var s1 = taskManager.GetStatus(taskId1);
            var s2 = taskManager.GetStatus(taskId2);
            if (s1?.State != "Running" && s2?.State != "Running")
            {
                break;
            }
            await Task.Delay(100);
        }

        var finalStatus1 = taskManager.GetStatus(taskId1);
        var finalStatus2 = taskManager.GetStatus(taskId2);
        Assert.That(finalStatus1?.State, Is.EqualTo("Cancelled").Or.EqualTo("Completed"));
        Assert.That(finalStatus2?.State, Is.EqualTo("Completed"));
    }

    [Test]
    public async Task Test_CrossServiceInteractionDetection()
    {
        var tsParser = new TypeScriptParser();
        var csParser = new CSharpParser();

        using var ws = ParserTestData.PrepareTempWorkspace("Workspaces/CrossServiceInteraction");
        var workspacePath = ws.DirectoryPath;

        var tsFile = ws.GetFilePath("OrdersController.ts");
        var csFile = ws.GetFilePath("PaymentsController.cs");
        var axiosFile = ws.GetFilePath("AxiosClient.ts");
        var pubsubFile = ws.GetFilePath("PubsubPublisher.ts");
        var rabbitFile = ws.GetFilePath("RabbitConsumer.ts");
        var socketFile = ws.GetFilePath("SocketClient.ts");

        var channel = System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>();
        await using var client = new InMemoryMemgraphClient();
        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTreeTs = await tsParser.ParseAsync(tsFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTreeTs, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var tsEp = FindEndpointNode(syntaxTreeTs.FileNode.Children, "POST:/orders/charge");
        Assert.That(tsEp, Is.Not.Null, "Should aggregate Controller route prefix for NestJS");

        using var syntaxTreeCs = await csParser.ParseAsync(csFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTreeCs, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var csEp = FindEndpointNode(syntaxTreeCs.FileNode.Children, "POST:/api/Payments/charge-card");
        Assert.That(csEp, Is.Not.Null, "Should aggregate Controller route prefix and resolve [controller] for C#");

        using var syntaxTreeAxios = await tsParser.ParseAsync(axiosFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTreeAxios, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var axiosEs = FindExternalServiceNode(syntaxTreeAxios.FileNode.Children, "*");
        Assert.That(axiosEs, Is.Not.Null, "Should resolve variable initializer in Axios call");
        Assert.That(axiosEs.Path, Is.EqualTo("/api/payments/charge-card"), "Should resolve variable initializer path");

        using var syntaxTreePubsub = await tsParser.ParseAsync(pubsubFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTreePubsub, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var pubsubRefs = FindReferences(syntaxTreePubsub.FileNode);
        var pubsubPub = pubsubRefs.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.PublishesTo);
        Assert.That(pubsubPub, Is.Not.Null);
        Assert.That(pubsubPub.TargetName, Is.EqualTo("gcp:negative-profit-topic"));

        using var syntaxTreeRabbit = await tsParser.ParseAsync(rabbitFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTreeRabbit, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var rabbitRefs = FindReferences(syntaxTreeRabbit.FileNode);
        var rabbitPub = rabbitRefs.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.PublishesTo);
        Assert.That(rabbitPub, Is.Not.Null);
        Assert.That(rabbitPub.TargetName, Is.EqualTo("rabbitmq:calc-done-queue"));

        using var syntaxTreeSocket = await tsParser.ParseAsync(socketFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        Layer3SyntacticParser.ProcessVisitor(syntaxTreeSocket, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var socketEs = FindExternalServiceNode(syntaxTreeSocket.FileNode.Children, "ws:ping-event");
        Assert.That(socketEs, Is.Not.Null, "Should map socket.emit to ExternalService");
    }

    private EndpointNode? FindEndpointNode(IEnumerable<IOntologyNode> nodes, string identifier)
    {
        foreach (var node in nodes)
        {
            if (node is EndpointNode ep && ep.Name == identifier) return ep;
            var found = FindEndpointNode(node.Children, identifier);
            if (found != null) return found;
        }
        return null;
    }

    private ExternalServiceNode? FindExternalServiceNode(IEnumerable<IOntologyNode> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (node is ExternalServiceNode es && (es.Name == name || es.Id.Contains(name))) return es;
            var found = FindExternalServiceNode(node.Children, name);
            if (found != null) return found;
        }
        return null;
    }

    private List<Reference> FindReferences(IOntologyNode node)
    {
        var result = new List<Reference>();
        result.AddRange(node.References);
        foreach (var child in node.Children)
        {
            result.AddRange(FindReferences(child));
        }
        return result;
    }
}

