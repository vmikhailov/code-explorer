using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using CodeExplorer.Tests.Shared;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
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

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();
            fileNode.Children.Should().NotBeEmpty();
        }
    }

    [Test]
    public async Task Test_TypeScriptParser_EmbeddedSql()
    {
        var parser = new TypeScriptParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/TypeScript/embedded_sql.ts.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        fileNode.Should().NotBeNull();

        var queryNodes = FindQueryNodes(fileNode.Children);
        queryNodes.Should().NotBeEmpty("Should have detected the embedded SQL query");

        var sqlQuery = queryNodes[0];
        sqlQuery.Name.Should().Be("DELETE Query");

        sqlQuery.QueryText.Should().Contain("DELETE FROM tracking.data_all_leads WHERE bundle_id in");

        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName)
            .ToList();
        dependsOn.Should().Contain("tracking");
        dependsOn.Should().Contain("data_all_leads");

        AssertSqlHierarchy(sqlQuery, "default", "tracking", "data_all_leads");
    }

    [Test]
    public async Task Test_CSharpParser_EmbeddedSql()
    {
        var parser = new CSharpParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/CSharp/embedded_sql.cs.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        var queryNodes = FindQueryNodes(fileNode.Children);
        queryNodes.Should().NotBeEmpty();
        var sqlQuery = queryNodes[0];
        sqlQuery.Name.Should().Be("SELECT Query");
        sqlQuery.QueryText.Should().Contain("SELECT id, name FROM users");

        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName)
            .ToList();
        dependsOn.Should().Contain("users");

        AssertSqlHierarchy(sqlQuery, "default", "dbo", "users");
    }

    [Test]
    public async Task Test_PythonParser_EmbeddedSql()
    {
        var parser = new PythonParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Python/embedded_sql.py.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        var queryNodes = FindQueryNodes(fileNode.Children);
        queryNodes.Should().NotBeEmpty();
        var sqlQuery = queryNodes[0];
        sqlQuery.Name.Should().Be("INSERT Query");
        sqlQuery.QueryText.Should().Contain("INSERT INTO logs");

        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName)
            .ToList();
        dependsOn.Should().Contain("logs");

        AssertSqlHierarchy(sqlQuery, "default", "dbo", "logs");
    }

    [Test]
    public async Task Test_GoParser_EmbeddedSql()
    {
        var parser = new GoParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/Go/embedded_sql.go.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;
        var queryNodes = FindQueryNodes(fileNode.Children);
        queryNodes.Should().NotBeEmpty();
        var sqlQuery = queryNodes[0];
        sqlQuery.Name.Should().Be("UPDATE Query");
        sqlQuery.QueryText.Should().Contain("UPDATE transactions SET status");

        var dependsOn = sqlQuery.References.Where(r => r.Kind == "DEPENDS_ON").Select(r => r.TargetName)
            .ToList();
        dependsOn.Should().Contain("transactions");

        AssertSqlHierarchy(sqlQuery, "default", "dbo", "transactions");
    }

    private void AssertSqlHierarchy(QueryNode queryNode, string expectedDb, string expectedSchema, string expectedTable)
    {
        var dbNode = queryNode.Children.OfType<DatabaseNode>()
            .FirstOrDefault(d => d.Name.Equals(expectedDb, StringComparison.OrdinalIgnoreCase) ||
                                (expectedDb.Equals("default", StringComparison.OrdinalIgnoreCase) && d.Name.EndsWith("." + expectedSchema, StringComparison.OrdinalIgnoreCase)));
        dbNode.Should().NotBeNull($"Should contain DB node: {expectedDb}");
        dbNode.DbType.Should().Be("relational");

        var schemaNode = dbNode.Children.OfType<DataSetNode>()
            .FirstOrDefault(s => s.Name.Equals(expectedSchema, StringComparison.OrdinalIgnoreCase));
        schemaNode.Should().NotBeNull($"Should contain Schema/DataSet node: {expectedSchema}");

        var tableNode = schemaNode.Children.OfType<TableNode>()
            .FirstOrDefault(t => t.Name.Equals(expectedTable, StringComparison.OrdinalIgnoreCase));
        tableNode.Should().NotBeNull($"Should contain Table node: {expectedTable}");
    }

    [Test]
    public void Test_NestedSqlParser_CleanQueryText_NestedQuotes()
    {
        var input = "\"'SELECT * FROM my_table'\"";
        var cleaned = NestedSqlParser.CleanQueryText(input);
        cleaned.Should().Be("SELECT * FROM my_table");
    }

    [Test]
    public void Test_NestedSqlParser_CleanSqlIdentifier_UnbalancedBrackets()
    {
        NestedSqlParser.CleanSqlIdentifier("[calculation_history").Should().Be("calculation_history");
        NestedSqlParser.CleanSqlIdentifier("[calculation_history]").Should().Be("calculation_history");
        NestedSqlParser.CleanSqlIdentifier("`calculation_history`").Should().Be("calculation_history");
        NestedSqlParser.CleanSqlIdentifier("\"calculation_history\"").Should().Be("calculation_history");
    }

    [Test]
    public void Test_NestedSqlParser_BigQueryWildcardQuery()
    {
        var rawSql = "SELECT DISTINCT _TABLE_SUFFIX AS suffix FROM `calculation_history.expect_site_cpm_*`";
        var queryNode = NestedSqlParser.ParseNestedSql(rawSql, "ws:query:1", "bq-routes-calculation/src/repository/history.repository.ts");
        
        queryNode.Should().NotBeNull();
        var dbNode = queryNode!.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode.Should().NotBeNull();
        dbNode!.Name.Should().Be("BigQuery.calculation_history");
        dbNode.DbType.Should().Be("analytics");
        dbNode.Extensions!["engine"].Should().Be("BigQuery");
        dbNode.Extensions!["schema"].Should().Be("calculation_history");
        dbNode.Name.Contains('[').Should().BeFalse("DatabaseNode name must not contain bracket '['");

        var datasetNode = dbNode.Children.OfType<DataSetNode>().FirstOrDefault();
        datasetNode.Should().NotBeNull();
        datasetNode!.Name.Should().Be("calculation_history");

        var tableNode = datasetNode.Children.OfType<TableNode>().FirstOrDefault();
        tableNode.Should().NotBeNull();
        tableNode!.Name.Should().Be("expect_site_cpm_*");
    }

    [Test]
    public void Test_NestedSqlParser_DynamicTableTemplateLiterals_NormalizedToWildcardFamily()
    {
        var rawSql = "SELECT * FROM `tracker.data_tracker_${tableSuffix}` JOIN `analytics.impressions_tb_{shard}` ON id = ref_id";
        var queryNode = NestedSqlParser.ParseNestedSql(rawSql, "ws:query:2", "bq-routes-calculation/src/repository/tracker.repository.ts");

        queryNode.Should().NotBeNull();
        var tables = queryNode!.Children
            .OfType<DatabaseNode>()
            .SelectMany(db => db.Children.OfType<DataSetNode>())
            .SelectMany(ds => ds.Children.OfType<TableNode>())
            .Select(t => t.Name)
            .ToList();

        tables.Should().Contain("data_tracker_*");
        tables.Should().Contain("impressions_tb_*");
        (tables.Any(t => t.Contains("tableSuffix") || t.Contains("shard"))).Should().BeFalse("Dynamic template variable names must not become phantom tables");
    }

    [Test]
    public async Task Test_SqlParser_CreateTable_IfNotExists_And_Templated_Identifiers()
    {
        var sqlParser = new Parser.SQL.SqlParser();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_sql_{Guid.NewGuid():N}.sql");
        var sql = """
            BEGIN
                DROP TABLE IF EXISTS ${tracker.main_query_result};

                CREATE TABLE IF NOT EXISTS ${tracker.main_query_result}(
                    id INT
                );

                CREATE TABLE IF NOT EXISTS {DB_NAME}.bundle_placements (
                    timestamp DateTime
                );

                CREATE TABLE IF NOT EXISTS {{BIG_QUERY_DATASET}}.clicktype_rates_history (
                    tb_id INT64
                );

                CREATE TABLE IF NOT EXIST legacy_table (
                    col1 STRING
                );
            END
            """;
        await File.WriteAllTextAsync(tempFile, sql);

        try
        {
            using var syntax = await sqlParser.ParseAsync(tempFile, "parent", "ws", Path.GetTempPath());

            var fileNode = syntax.FileNode;
            fileNode.Should().NotBeNull();

            var allTables = new List<TableNode>();

            void CollectTables(IOntologyNode node)
            {
                if (node is TableNode t) allTables.Add(t);
                foreach (var child in node.Children) CollectTables(child);
            }

            CollectTables(fileNode);

            // Assert "IF", "NOT", "EXISTS", "EXIST" are NEVER created as TableNodes
            allTables.Any(t => t.Name.Equals("IF", StringComparison.OrdinalIgnoreCase)).Should().BeFalse("Table 'IF' must never be created!");

            allTables.Any(t => t.Name.Equals("NOT", StringComparison.OrdinalIgnoreCase)).Should().BeFalse("Table 'NOT' must never be created!");

            allTables.Any(t => t.Name.Equals("EXISTS", StringComparison.OrdinalIgnoreCase)).Should().BeFalse("Table 'EXISTS' must never be created!");

            allTables.Any(t => t.Name.Equals("EXIST", StringComparison.OrdinalIgnoreCase)).Should().BeFalse("Table 'EXIST' must never be created!");

            // Assert templated and legacy tables are correctly recognized
            (allTables.Any(t => t.Name == "main_query_result")).Should().BeTrue("Table 'main_query_result' should be recognized");

            (allTables.Any(t => t.Name == "bundle_placements")).Should().BeTrue("Table 'bundle_placements' should be recognized");

            (allTables.Any(t => t.Name == "clicktype_rates_history")).Should().BeTrue("Table 'clicktype_rates_history' should be recognized");

            (allTables.Any(t => t.Name == "legacy_table")).Should().BeTrue("Table 'legacy_table' should be recognized");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void Test_NestedSqlParser_ExternalQuery_And_TableFunctions()
    {
        var rawSql = """
            SELECT * FROM networks.cost_loss_all_sources
            WHERE source_id IN (
              SELECT * FROM EXTERNAL_QUERY("eu.system-db",
                "SELECT source_id FROM defaults.sources WHERE network_name = 'foo'")
            )
            """;
        var queryNode = NestedSqlParser.ParseNestedSql(rawSql, "ws:query:1", "calculation.controller.ts");
        queryNode.Should().NotBeNull();

        var allTables = new List<TableNode>();
        void CollectTables(IOntologyNode node)
        {
            if (node is TableNode t) allTables.Add(t);
            foreach (var child in node.Children) CollectTables(child);
        }
        CollectTables(queryNode!);

        // "EXTERNAL_QUERY" should NEVER be recognized as a TableNode
        allTables.Any(t => t.Name.Equals("EXTERNAL_QUERY", StringComparison.OrdinalIgnoreCase)).Should().BeFalse("EXTERNAL_QUERY must not be recognized as a table!");
        
        // "sources" and "cost_loss_all_sources" should be recognized
        (allTables.Any(t => t.Name == "cost_loss_all_sources")).Should().BeTrue("Table 'cost_loss_all_sources' should be recognized");
        (allTables.Any(t => t.Name == "sources")).Should().BeTrue("Table 'sources' should be recognized");
    }

    [Test]
    public async Task Test_TypeScriptParser_EmbeddedSql_ComplexTemplate()
    {
        var parser = new TypeScriptParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/TypeScript/complex_template.ts.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;

        var queryNodes = FindQueryNodes(fileNode.Children);
        queryNodes.Should().NotBeEmpty();
        var sqlQuery = queryNodes[0];
        sqlQuery.Name.Should().Be("SELECT Query");

        // Since tableName is a variable, it should be skipped and no database node hierarchy should be created for it.
        var hasDbNode = sqlQuery.Children.OfType<DatabaseNode>().Any();

        hasDbNode.Should().BeFalse("Should have skipped tableName because it is a template variable placeholder.");
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

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;

        var endpoints = FindEndpointNodes(fileNode.Children);
        endpoints.Should().NotBeEmpty();
        var ep = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("charge"));
        ep.Should().NotBeNull();
        ep.HttpMethod.Should().Be("POST");

        var externalServices = FindExternalServiceNodes(fileNode.Children);
        externalServices.Should().NotBeEmpty();
        var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
        es.Should().NotBeNull();
        es.Protocol.Should().Be("http");
    }

    [Test]
    public async Task Test_TypeScriptParser_ApiIngressEgress()
    {
        var parser = new TypeScriptParser();

        using var tempFile = ParserTestData.GetPreparedFile("SingleFiles/TypeScript/api_ingress_egress.ts.test");

        var workspacePath = tempFile.DirectoryPath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTree = await parser.ParseAsync(tempFile.FilePath, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var fileNode = syntaxTree.FileNode;

        var endpoints = FindEndpointNodes(fileNode.Children);
        endpoints.Should().NotBeEmpty();
        var ep = endpoints.FirstOrDefault(e => e.RouteTemplate.Contains("charge"));
        ep.Should().NotBeNull();
        ep.HttpMethod.Should().Be("POST");

        var entryPoints = FindEntryPointNodes(fileNode.Children);
        var wsEp = entryPoints.FirstOrDefault(e => e.EntryType == "queue-listener");
        wsEp.Should().NotBeNull();
        wsEp.Name.Should().Be("ping");

        var externalServices = FindExternalServiceNodes(fileNode.Children);
        externalServices.Should().NotBeEmpty();
        var es = externalServices.FirstOrDefault(e => e.DomainOrService == "api.stripe.com");
        es.Should().NotBeNull();
        es.Protocol.Should().Be("http");
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

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
        ctx.WorkspaceId = "1";

        WorkspaceIndexer.AutoRegisterParsers();
        var l1Result = await new Layer1PhysicalParser().ParseAsync(ctx);
        var l2Result = await new Layer2ProjectParser().ParseAsync(l1Result, ctx);
        var l3Result = await new Layer3SyntacticParser().ParseAsync(l2Result, ctx);
        var l4Result = await new Layer4SemanticParser().ParseAsync(l3Result, ctx);

        var workspaceNode = l1Result.Workspace;

        var projectsStructure = workspaceNode.Children.OfType<ProjectsStructureNode>().FirstOrDefault();
        projectsStructure.Should().NotBeNull();

        var filesStructure = workspaceNode.Children.OfType<FilesStructureNode>().FirstOrDefault();
        filesStructure.Should().NotBeNull();

        // Before pruning, ProjectB and EmptyFolder are in the tree
        (projectsStructure.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB")).Should().BeTrue();

        var projectA =
            projectsStructure.Children.FirstOrDefault(c => c is ProjectNode pn && pn.Name == "ProjectA") as
                ProjectNode;
        projectA.Should().NotBeNull();

        var projectAFolder = filesStructure.Children.OfType<FolderNode>()
            .FirstOrDefault(f => f.Name == "ProjectA");
        projectAFolder.Should().NotBeNull();

        (projectAFolder.Children.Any(c => c is FolderNode pfn && pfn.Name == "EmptyFolder")).Should().BeTrue();

        // 2. Perform pruning
        OntologyPruner.PruneEmptyFolders(workspaceNode);

        // After pruning:
        // ProjectB is removed because it is an empty project
        (projectsStructure.Children.Any(c => c is ProjectNode pn && pn.Name == "ProjectB")).Should().BeFalse();

        // EmptyFolder is removed
        var folderA =
            projectAFolder.Children.FirstOrDefault(c => c is FolderNode pfn && pfn.Name == "EmptyFolder");
        folderA.Should().BeNull();

        // Verify project framework detection
        projectA.Extensions.Should().NotBeNull();
        projectA.Extensions.ContainsKey("framework").Should().BeTrue();
        projectA.Extensions["framework"].Should().Be("ASP.NET Core");

        // Verify SemanticStructure node grouping
        var semanticNode = workspaceNode.Children.OfType<SemanticStructureNode>().FirstOrDefault();
        semanticNode.Should().NotBeNull();

        // Verify external packages are inside ProjectNode directly (Layer 1)
        var extPackages = projectA.Children.OfType<PackageNode>().Where(p => p.Name != "ProjectA").ToList();
        extPackages.Should().HaveCount(3);
        (extPackages.Any(p => p.Name == "Dapper")).Should().BeTrue();
        (extPackages.Any(p => p.Name == "Stripe.net")).Should().BeTrue();
        (extPackages.Any(p => p.Name == "Microsoft.AspNetCore.App")).Should().BeTrue();

        // Verify semanticNode does NOT contain those external packages as children
        var semPackages = semanticNode.Children.OfType<PackageNode>().ToList();
        (semPackages.Any(p => p.Name == "Dapper")).Should().BeFalse();
        (semPackages.Any(p => p.Name == "Stripe.net")).Should().BeFalse();
        (semPackages.Any(p => p.Name == "Microsoft.AspNetCore.App")).Should().BeFalse();

        // Verify projectA contains the produced package directly as child (Layer 1)
        var directPackages = projectA.Children.OfType<PackageNode>().ToList();
        (directPackages.Any(p => p.Name == "ProjectA")).Should().BeTrue();

        var fileNode = projectAFolder.Children.OfType<FileNode>()
            .FirstOrDefault(f => f.Name == "Repository.cs");
        fileNode.Should().NotBeNull();

        // Check Repository.cs extensions (file-level extensions are removed)
        if (fileNode.Extensions != null)
        {
            fileNode.Extensions.ContainsKey("db_type").Should().BeFalse();
            fileNode.Extensions.ContainsKey("cloud_service").Should().BeFalse();
        }

        // Check if DatabaseNode child was added at the project level (under SemanticStructureNode)
        var dbNode = semanticNode.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode.Should().NotBeNull();
        dbNode.Name.Should().StartWith("PostgreSQL");
        dbNode.DbType.Should().Be("relational");

        // Check if CloudServiceNode child was added at the project level (under SemanticStructureNode)
        var cloudNode = semanticNode.Children.OfType<CloudServiceNode>().FirstOrDefault();
        cloudNode.Should().NotBeNull();
        cloudNode.Name.Should().Be("Stripe");

        // Check that file-to-library relationships are created in the context
        var usesDb =
            ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_DB");
        usesDb.Should().NotBeNull();
        usesDb.To.Should().Be(dbNode.Id);

        var usesCloud =
            ctx.GlobalProjectDependencies.FirstOrDefault(r => r.From == fileNode.Id && r.Kind == "USES_CLOUD");
        usesCloud.Should().NotBeNull();
        usesCloud.To.Should().Be(cloudNode.Id);

        // Check member nodes under TypeNode (Repository)
        var classNode = fileNode.Children.OfType<TypeNode>().FirstOrDefault();
        classNode.Should().NotBeNull();

        var variables = classNode.Children.OfType<MemberNode>().ToList();
        (variables.Any(v => v.Name == "CONNECTION_STRING_URL")).Should().BeTrue();
        (variables.Any(v => v.Name == "MAX_RETRIES")).Should().BeTrue();
        (variables.Any(v => v.Name == "timeoutSeconds")).Should().BeFalse(); // local non-config is ignored

        var connStrVar = variables.First(v => v.Name == "CONNECTION_STRING_URL");
        connStrVar.Extensions.Should().NotBeNull();
        connStrVar.Extensions["variable_type"].Should().Contain("config");
        connStrVar.Extensions["variable_type"].Should().Contain("constant");
    }

    [Test]
    public async Task Test_NewParserFeatures()
    {
        using var ws = ParserTestData.PrepareTempWorkspace("Workspaces/NewParserFeatures");

        var tempWorkspace = ws.WorkspacePath;

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
        ctx.WorkspaceId = "1";

        // 1. Python Parser - Flask & HTTP calls
        var pythonParser = new PythonParser();
        var pyFile = ws.GetFilePath("app.py");

        using var pySyntax =
            await pythonParser.ParseAsync(pyFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(pySyntax, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var pyNode = pySyntax.FileNode;
        pyNode.Should().NotBeNull();

        var pyEndpoints = FindEndpointNodes(pyNode.Children);
        pyEndpoints.Should().HaveCount(1);
        pyEndpoints[0].RouteTemplate.Should().Be("/charge");
        pyEndpoints[0].HttpMethod.Should().Be("POST");

        var pyExtServices = FindExternalServiceNodes(pyNode.Children);
        pyExtServices.Should().HaveCount(1);
        pyExtServices[0].Name.Should().Be("api.stripe.com");

        // Verify reference from process_payment function to Endpoint POST:/charge is collected
        var pyRefs = FindReferences(pyNode.Children);
        (pyRefs.Any(r => r.TargetName == "POST /charge" && r.Kind == "IMPLEMENTS")).Should().BeTrue();

        // 2. Go Parser - Gin & HTTP Get calls
        var goParser = new GoParser();
        var goFile = ws.GetFilePath("main.go");

        using var goSyntax =
            await goParser.ParseAsync(goFile, "parent", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(goSyntax, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var goNode = goSyntax.FileNode;
        goNode.Should().NotBeNull();

        var goEndpoints = FindEndpointNodes(goNode.Children);
        goEndpoints.Should().HaveCount(1);
        goEndpoints[0].RouteTemplate.Should().Be("/api/v1/users");
        goEndpoints[0].HttpMethod.Should().Be("GET");

        // Verify Go references
        var goRefs = FindReferences(goNode.Children);

        (goRefs.Any(r =>
                r.TargetName == "GET /api/v1/users" && r.Kind == "IMPLEMENTS" &&
                r.ScopeSymbolId == "GetUsers")).Should().BeTrue();

        // 3. SQL Parser - CREATE OR REPLACE PROCEDURE, IF NOT EXISTS, backticks
        var sqlParser = new Parser.SQL.SqlParser();
        var sqlFile = ws.GetFilePath("sp.sql");

        using var sqlSyntax = await sqlParser.ParseAsync(sqlFile, "parent", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        var sqlNode = sqlSyntax.FileNode;
        sqlNode.Should().NotBeNull();

        // Schema and DB hierarchy check
        var dbNode = sqlNode.Children.OfType<DatabaseNode>().FirstOrDefault();
        dbNode.Should().NotBeNull();
        dbNode.DbType.Should().Be("relational");

        var schemaNode = dbNode.Children.OfType<DataSetNode>()
            .FirstOrDefault(s => s.Name == "my_schema");
        schemaNode.Should().NotBeNull();

        var procNode = schemaNode.Children.OfType<ProcedureNode>()
            .FirstOrDefault(p => p.Name == "my_proc");
        procNode.Should().NotBeNull();

        var tableNode = schemaNode.Children.OfType<TableNode>()
            .FirstOrDefault(t => t.Name == "my_table");
        tableNode.Should().NotBeNull();

        var queryNode = procNode.Children.OfType<QueryNode>().FirstOrDefault();
        queryNode.Should().NotBeNull();

        (queryNode.References.Any(r => r.TargetName == "another_proc" && r.Kind == "CALLS")).Should().BeTrue();
    }

    [Test]
    public async Task Test_SemanticModel_DbTypeMapping()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "db_type_mapping_test_workspace_" + Guid.NewGuid());
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempWorkspace, tempWorkspace, client, channel);
            ctx.WorkspaceId = "1";

            ctx.SemanticStructure =
                new SemanticStructureNode("1:semantic_structure", "SemanticStructure", tempWorkspace);

            // Test C# Semantic Analyzer with relational (EntityFramework)
            // Test C# Semantic Model with relational (EntityFramework)
            var csProjDir = Path.Combine(tempWorkspace, "cs_project");
            Directory.CreateDirectory(csProjDir);
            await File.WriteAllTextAsync(Path.Combine(csProjDir, "cs_project.csproj"), "<Project><ItemGroup><PackageReference Include=\"Npgsql.EntityFrameworkCore.PostgreSQL\" Version=\"8.0\" /></ItemGroup></Project>");

            var csFile = new FileNode("cs_file", "Repository.cs", "Repository.cs", Path.Combine(csProjDir, "Repository.cs"));
            await File.WriteAllTextAsync(csFile.FullPath, "using Microsoft.EntityFrameworkCore; class MyContext : DbContext { protected override void OnModelCreating(ModelBuilder b) => b.HasDefaultSchema(\"catalog\"); }");
            var csProj = new ProjectNode("cs_project", "cs_project", csProjDir, "csharp");
            csProj.Children.Add(csFile);
            var csParser = new CSharpParser();

            var csSyntaxTree = new SyntaxTree(csFile.FullPath, csFile.Path, null, null, null, csFile, csParser,
                [new RawImport("Microsoft.EntityFrameworkCore", "Repository.cs") { Type = ImportType.External }],
                [], []);
            var csModel = csParser.GetSyntaxEnricher(csSyntaxTree);
            await csModel.EnrichAsync(csProj, ctx);

            var csDbGroup = ctx.SemanticStructure;
            csDbGroup.Should().NotBeNull();
            var csDbNode = csDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.DbType == "relational");
            csDbNode.Should().NotBeNull();
            csDbNode.Name.Should().StartWith("PostgreSQL");
            csDbNode.DbType.Should().Be("relational");

            (ctx.GlobalProjectDependencies.Any(r =>
                    r.From == csFile.Id && r.To == csDbNode.Id && r.Kind == "USES_DB")).Should().BeTrue();

            // Test C# Semantic Model with graph (Neo4j.Driver)
            var csGraphProj = new ProjectNode("cs_graph_project", "cs_graph_project", "cs_graph_project", "csharp");

            var csGraphFile = new FileNode("cs_graph_file", "MemgraphClient.cs", "MemgraphClient.cs",
                tempWorkspace + "/MemgraphClient.cs");
            csGraphProj.Children.Add(csGraphFile);

            var csGraphSyntaxTree = new SyntaxTree(csGraphFile.FullPath, csGraphFile.Path, null, null, null,
                csGraphFile, csParser,
                [new RawImport("Neo4j.Driver", "MemgraphClient.cs") { Type = ImportType.External }], [], []);
            var csGraphModel = csParser.GetSyntaxEnricher(csGraphSyntaxTree);
            await csGraphModel.EnrichAsync(csGraphProj, ctx);

            var csGraphDbGroup = ctx.SemanticStructure;
            csGraphDbGroup.Should().NotBeNull();

            var csGraphDbNode = csGraphDbGroup.Children.OfType<DatabaseNode>()
                .FirstOrDefault(d => d.Name == "Neo4j");
            csGraphDbNode.Should().NotBeNull();
            csGraphDbNode.Name.Should().Be("Neo4j");
            csGraphDbNode.DbType.Should().Be("graph");

            (ctx.GlobalProjectDependencies.Any(r =>
                    r.From == csGraphFile.Id && r.To == csGraphDbNode.Id && r.Kind == "USES_DB")).Should().BeTrue();

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
            tsDbGroup.Should().NotBeNull();
            var tsDbNode = tsDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "MongoDB");
            tsDbNode.Should().NotBeNull();
            tsDbNode.Name.Should().Be("MongoDB");
            tsDbNode.DbType.Should().Be("document");

            (ctx.GlobalProjectDependencies.Any(r =>
                    r.From == tsFile.Id && r.To == tsDbNode.Id && r.Kind == "USES_DB")).Should().BeTrue();

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
            pyDbGroup.Should().NotBeNull();
            var pyDbNode = pyDbGroup.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "Redis");
            pyDbNode.Should().NotBeNull();
            pyDbNode.Name.Should().Be("Redis");
            pyDbNode.DbType.Should().Be("keyvalue");

            (ctx.GlobalProjectDependencies.Any(r =>
                    r.From == pyFile.Id && r.To == pyDbNode.Id && r.Kind == "USES_DB")).Should().BeTrue();
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

        await using var client = new InMemoryGraphClient();

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
        dapperNode.Should().NotBeNull();
        dapperNode.Name.Should().Be("SELECT Query: SELECT name FROM users WHERE id = @id");

        // Verify Flurl external service extraction
        var csExtServices = FindExternalServiceNodes([csFileNode]);
        var flurlNode = csExtServices.FirstOrDefault();
        flurlNode.Should().NotBeNull();
        flurlNode.Name.Should().Be("api.github.com");

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
        modelNode.Should().NotBeNull();
        modelNode.Name.Should().Be("Mongoose Model: Product");

        var findNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Mongoose: Product.find"));
        findNode.Should().NotBeNull();

        // Verify Redis query extraction
        var redisNode = tsQueries.FirstOrDefault(q => q.Name.Contains("Redis: client.set"));
        redisNode.Should().NotBeNull();
    }

    private record TestSemanticExtension(string Id, string Name, string Type, IReadOnlyList<string> SupportedPatterns) : ISemanticExtension;

    [Test]
    public void Test_SemanticExtensionRegistry_Matching()
    {
        var parserNest = new TestSemanticExtension("nestjs", "NestJS", "framework", ["@nestjs/*"]);
        var parserFirebaseGeneric = new TestSemanticExtension("firebase", "Firebase", "cloud", ["firebase*"]);

        var parserFirebaseSpecific =
            new TestSemanticExtension("firebaseadmin", "FirebaseAdmin", "cloud", ["firebase-admin"]);
        var parserSql = new TestSemanticExtension("sqlclient", "SqlClient", "db", ["System.Data"]);
        var parserGoogleCloud = new TestSemanticExtension("gcp", "GCP", "cloud", ["Google.Cloud."]);

        var registry = new SemanticExtensionRegistry([
            parserNest,
            parserFirebaseGeneric,
            parserFirebaseSpecific,
            parserSql,
            parserGoogleCloud
        ]);

        // Scoped wildcard /*
        registry.Match("@nestjs/common").Should().BeSameAs(parserNest);
        registry.Match("@nestjs/core").Should().BeSameAs(parserNest);
        registry.Match("@nestjs").Should().BeSameAs(parserNest);

        // Prefix priority matching
        registry.Match("firebase-admin").Should().BeSameAs(parserFirebaseSpecific);
        registry.Match("firebase").Should().BeSameAs(parserFirebaseGeneric);

        // Fallback namespace match
        registry.Match("System.Data.SqlClient").Should().BeSameAs(parserSql);
        registry.Match("Google.Cloud.Translation").Should().BeSameAs(parserGoogleCloud);
        // Unmatched
        registry.Match("stripe").Should().BeNull();
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

        var channel = Channel.CreateUnbounded<Func<Task>>();

        await using var client = new InMemoryGraphClient();

        var ctx = new ParsingContext(workspacePath, workspacePath, client, channel);

        using var syntaxTreeTs =
            await tsParser.ParseAsync(tsFile, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTreeTs, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var tsEp = FindEndpointNode(syntaxTreeTs.FileNode.Children, "POST:/orders/charge");
        tsEp.Should().NotBeNull("Should aggregate Controller route prefix for NestJS");

        using var syntaxTreeCs = await csParser.ParseAsync(csFile, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTreeCs, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
        var csEp = FindEndpointNode(syntaxTreeCs.FileNode.Children, "POST:/api/Payments/charge-card");

        csEp.Should().NotBeNull("Should aggregate Controller route prefix and resolve [controller] for C#");

        using var syntaxTreeAxios = await tsParser.ParseAsync(axiosFile, "parent-id", ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTreeAxios, ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);
        var axiosEs = FindExternalServiceNode(syntaxTreeAxios.FileNode.Children, "payment-service");
        axiosEs.Should().NotBeNull("Should resolve variable initializer in Axios call");

        axiosEs.Path.Should().Be("/api/payments/charge-card", "Should resolve variable initializer path");

        using var syntaxTreePubsub = await tsParser.ParseAsync(pubsubFile, "parent-id",
            ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTreePubsub, ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);
        var pubsubRefs = FindReferences(syntaxTreePubsub.FileNode);

        var pubsubPub = pubsubRefs.FirstOrDefault(r =>
            r.Kind == OntologyConstants.Relationships.PublishesTo);
        pubsubPub.Should().NotBeNull();
        pubsubPub.TargetName.Should().Be("gcp:negative-profit-topic");

        using var syntaxTreeRabbit = await tsParser.ParseAsync(rabbitFile, "parent-id",
            ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTreeRabbit, ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);
        var rabbitRefs = FindReferences(syntaxTreeRabbit.FileNode);

        var rabbitPub = rabbitRefs.FirstOrDefault(r =>
            r.Kind == OntologyConstants.Relationships.PublishesTo);
        rabbitPub.Should().NotBeNull();
        rabbitPub.TargetName.Should().Be("rabbitmq:calc-done-queue");

        using var syntaxTreeSocket = await tsParser.ParseAsync(socketFile, "parent-id",
            ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

        Layer3SyntacticParser.ProcessVisitor(syntaxTreeSocket, ctx.WorkspaceId,
            ctx.AbsoluteWorkspacePath);

        var socketEs = FindExternalServiceNode(syntaxTreeSocket.FileNode.Children,
            "ws:ping-event");
        socketEs.Should().NotBeNull("Should map socket.emit to ExternalService");
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

    [Test]
    public async Task Test_CSharpParser_ClassLevelRouteAndSignalR()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_csharp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "ControllerTest.cs");

        var code = @"
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Test;

[Route(""[controller]"")]
public class HealtCheckController : ControllerBase {
    [HttpGet]
    public IActionResult Index() => Ok();
}

public static class RouteConfig {
    public static void Map(IEndpointRouteBuilder endpoints) {
        endpoints.MapHub<MyHub>(""/hub"");
    }
}

public class MyHub {}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            var entryPoints = FindEntryPointNodes(fileNode.Children);

            // Verify [controller] was resolved to HealtCheck and method is GET
            var getEndpoint = endpoints.FirstOrDefault(e => e.HttpMethod == "GET");
            getEndpoint.Should().NotBeNull();
            getEndpoint!.RouteTemplate.Should().Be("/HealtCheck");

            // Verify SignalR MapHub was captured as EntryPoint with /hub (MyHub)
            var hubEntryPoint = entryPoints.FirstOrDefault(ep => ep.Name.Contains("/hub"));
            hubEntryPoint.Should().NotBeNull();
            hubEntryPoint!.Name.Should().Contain("MyHub");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_CSharpParser_HttpClientCancellationAndUriResolution()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_csharp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "HttpClientTest.cs");

        var code = @"
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class MyService {
    private HttpClient _httpClient = new HttpClient();

    public async Task DoWork(CancellationToken cMoveCancellationToken) {
        var dicomClient = new DicomClient();
        await dicomClient.SendAsync(cMoveCancellationToken);

        var uri = new Uri(""https://rest.spryngsms.com/v1/messages"");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        await _httpClient.SendAsync(request);
    }
}

public class DicomClient {
    public Task SendAsync(CancellationToken token) => Task.CompletedTask;
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var extServices = FindExternalServiceNodes(fileNode.Children);

            // Verify dicomClient and CancellationToken were rejected
            extServices.Any(es => es.Name.Contains("cMoveCancellationToken")).Should().BeFalse("CancellationToken should never be captured as ExternalService");

            (extServices.Any(es => es.Name == "unknown-service")).Should().BeFalse("Unknown service should not be emitted for non-HTTP calls");

            // Verify Spryng URI was successfully extracted from HttpRequestMessage -> Uri declaration
            var spryng = extServices.FirstOrDefault(es => es.Name == "rest.spryngsms.com");
            spryng.Should().NotBeNull("Should resolve variable URI from HttpRequestMessage");
            spryng!.Path.Should().Be("/v1/messages");
            spryng.Protocol.Should().Be("https");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_CSharpParser_HttpClientClassFieldAndHelperMethodResolution()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_csharp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "OrderingAndCatalogServices.cs");

        var code = @"
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Test;

public class OrderingService(HttpClient httpClient)
{
    private readonly string remoteServiceBaseUrl = ""/api/Orders/"";

    public Task<string[]> GetOrders()
    {
        return httpClient.GetFromJsonAsync<string[]>(remoteServiceBaseUrl);
    }

    public Task CreateOrder(object request)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, remoteServiceBaseUrl);
        return httpClient.SendAsync(requestMessage);
    }
}

public class CatalogService(HttpClient httpClient)
{
    private readonly string remoteServiceBaseUrl = ""api/catalog/"";

    public async Task<string> GetCatalogItems(int pageIndex, int pageSize)
    {
        var uri = GetAllCatalogItemsUri(remoteServiceBaseUrl, pageIndex, pageSize);
        var result = await httpClient.GetFromJsonAsync<string>($""{uri}&api-version=2.0"");
        return result;
    }

    private static string GetAllCatalogItemsUri(string baseUri, int pageIndex, int pageSize)
    {
        return baseUri + ""items"";
    }
}

public static class StartupExtensions
{
    public static void Configure(dynamic builder)
    {
        builder.Services.AddHttpClient<CatalogService>(o => o.BaseAddress = new(""https+http://catalog-api""));
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphClient();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var extServices = FindExternalServiceNodes(fileNode.Children);

            // 1. Verify *&api-version=2.0 is NOT generated
            extServices.Any(es => es.Name.Contains("api-version=") || es.Name.StartsWith("*")).Should().BeFalse("Wildcard query string artifacts should never become external services");

            // 2. Verify AddHttpClient resolved catalog-api
            var catalogApi = extServices.FirstOrDefault(es => es.DomainOrService == "catalog-api" || es.Name == "catalog-api");
            catalogApi.Should().NotBeNull("AddHttpClient should resolve target host catalog-api");

            // 3. Verify OrderingService class field was resolved for GetFromJsonAsync and SendAsync
            var orders = extServices.Where(es => es.Path.Contains("Orders")).ToList();
            orders.Should().NotBeEmpty("Class field remoteServiceBaseUrl should be resolved for OrderingService");

            // 4. Verify CatalogService helper method + interpolation resolved to catalog path
            var catalogItems = extServices.Where(es => es.Path.Contains("catalog")).ToList();
            catalogItems.Should().NotBeEmpty("Helper method + interpolation should resolve to catalog path");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_CSharpParser_RestSharp()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_csharp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "RestSharpTest.cs");

        var code = @"
using System;
using RestSharp;

namespace Test;

public class RestService {
    private RestClient _client = new RestClient(""https://api.icometrix.com"");

    public void Call() {
        var request = new RestRequest(""/authentication-service/api/v1/sessions"", Method.Post);
    }
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var extServices = FindExternalServiceNodes(fileNode.Children);

            var clientEs = extServices.FirstOrDefault(es => es.Name == "api.icometrix.com");
            clientEs.Should().NotBeNull("Should capture RestClient base URI");
            clientEs!.Protocol.Should().Be("https");

            var reqEs = extServices.FirstOrDefault(es => es.Name == "authentication-service");
            reqEs.Should().NotBeNull("Should capture RestRequest service name");
            reqEs!.Path.Should().Be("/authentication-service/api/v1/sessions");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_GoParser_ServeMuxMethods_NoDuplicateVerbs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codeexplorer-test-go-mux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "api.go");

        var code = @"package api

import ""net/http""

func handleGetBundles(w http.ResponseWriter, r *http.Request) {}
func handlePostBundle(w http.ResponseWriter, r *http.Request) {}

func registerRoutes() {
    mux := http.NewServeMux()
    mux.HandleFunc(""GET /api/v1/bundles"", handleGetBundles)
    mux.HandleFunc(""POST /api/v1/bundles"", handlePostBundle)
    mux.HandleFunc(""/api/v1/legacy"", handleGetBundles)
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            var parser = new GoParser();

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var endpoints = FindEndpointNodes(fileNode.Children);
            endpoints.Should().HaveCount(3);

            var getBundle = endpoints.FirstOrDefault(e =>
                e.RouteTemplate == "/api/v1/bundles" && e.HttpMethod == "GET");
            getBundle.Should().NotBeNull();
            getBundle!.Name.Should().Be("GET:/api/v1/bundles");
            getBundle.Name.Should().NotContain("GET:GET");

            var postBundle = endpoints.FirstOrDefault(e =>
                e.RouteTemplate == "/api/v1/bundles" && e.HttpMethod == "POST");
            postBundle.Should().NotBeNull();
            postBundle!.Name.Should().Be("POST:/api/v1/bundles");
            postBundle.Name.Should().NotContain("GET:POST");

            var legacy = endpoints.FirstOrDefault(e => e.RouteTemplate == "/api/v1/legacy");
            legacy.Should().NotBeNull();
            legacy!.HttpMethod.Should().Be("GET");
            legacy.Name.Should().Be("GET:/api/v1/legacy");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Test_CSharpParser_Refit()
    {
        var parser = new CSharpParser();
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_test_refit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFilePath = Path.Combine(tempDir, "IUsersApi.cs");

        var code = @"
using System.Threading.Tasks;
using System.Collections.Generic;
using Refit;
using AwesomeAssertions;

namespace Clients;

public interface IUsersApi
{
    [Get(""/api/v1/users"")]
    Task<List<string>> GetUsers();

    [Post(""/api/v1/profiles"")]
    Task CreateProfile([Body] object profile);
}
";
        await File.WriteAllTextAsync(tempFilePath, code);

        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();

            await using var client = new InMemoryGraphClient();

            var ctx = new ParsingContext(tempDir, tempDir, client, channel);

            using var syntaxTree = await parser.ParseAsync(tempFilePath, "parent-id", ctx.WorkspaceId,
                ctx.AbsoluteWorkspacePath);

            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);

            var fileNode = syntaxTree.FileNode;
            fileNode.Should().NotBeNull();

            var extServices = FindExternalServiceNodes(fileNode.Children);
            extServices.Should().HaveCount(2);

            var getUsers = extServices.FirstOrDefault(es => es.Path == "/api/v1/users");
            getUsers.Should().NotBeNull();
            getUsers!.DomainOrService.Should().Be("users");

            var createProfile = extServices.FirstOrDefault(es => es.Path == "/api/v1/profiles");
            createProfile.Should().NotBeNull();
            createProfile!.DomainOrService.Should().Be("users");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task CSharpParser_GetProducedPackageAsync_IgnoresPackageReferenceVersion()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_cs_pkg_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""EntityFramework"">
      <Version>6.5.2</Version>
    </PackageReference>
  </ItemGroup>
</Project>";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "MyLib.csproj"), csproj);

            var parser = new CSharpParser();
            var prod = await parser.GetProducedPackageAsync(tempDir);

            prod.Should().NotBeNull();
            prod!.Name.Should().Be("MyLib");
            // Must default to 1.0.0 because project has no <Version>, NOT 6.5.2 from EntityFramework!
            prod.Version.Should().Be("1.0.0");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Test_ProjectParserRole_Fallback_Behavior()
    {
        IProjectParser scriptParser = new Parser.Script.ScriptProjectParser();
        scriptParser.ParserRole.Should().Be(ProjectParserRole.Fallback);
        scriptParser.IsFallback.Should().BeTrue();

        IProjectParser csParser = new CSharpParser();
        csParser.ParserRole.Should().Be(ProjectParserRole.Standard);
        csParser.IsFallback.Should().BeFalse();

        // Verify WorkspaceIndexer parser ordering puts fallback parser last
        WorkspaceIndexer.AutoRegisterParsers();
        var allParsers = WorkspaceIndexer.GetAllProjectParsers().ToList();
        allParsers.Should().Contain(p => p is Parser.Script.ScriptProjectParser);
        var lastParser = allParsers.Last();
        lastParser.ParserRole.Should().Be(ProjectParserRole.Fallback);
    }
}