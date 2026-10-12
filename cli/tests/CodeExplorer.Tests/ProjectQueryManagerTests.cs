using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using NUnit.Framework;
using AwesomeAssertions;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectQueryManagerTests
{
    private string _tempDir = null!;
    private ProjectQueryManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "codeexplorer_query_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _manager = new ProjectQueryManager();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Test]
    public void SaveQuery_ValidQuery_WritesCypherAndJsonSidecar()
    {
        var metadata = new ProjectQueryMetadata
        {
            Name = "find_kafka_consumers",
            Description = "Finds all Kafka message consumers and their topics.",
            Parameters =
            [
                new ProjectQueryParameter("topicFilter", "string", false, "Optional filter for topic name")
            ],
            Returns = "consumerClass, topicName",
            Tags = ["kafka", "messaging"]
        };

        const string cypher = "MATCH (w:Workspace) RETURN w.id AS id, w.name AS name";

        var item = _manager.SaveQuery(_tempDir, "find_kafka_consumers", cypher, metadata);

        item.Should().NotBeNull();
        item.Name.Should().Be("find_kafka_consumers");
        File.Exists(item.CypherPath).Should().BeTrue();
        File.Exists(item.MetadataPath).Should().BeTrue();

        var savedCypher = File.ReadAllText(item.CypherPath);
        savedCypher.Should().Be(cypher);

        var metaJson = File.ReadAllText(item.MetadataPath);
        var readMeta = JsonSerializer.Deserialize<ProjectQueryMetadata>(metaJson);
        readMeta.Should().NotBeNull();
        readMeta!.Description.Should().Be(metadata.Description);
        readMeta.Parameters.Count.Should().Be(1);
        readMeta.Parameters[0].Name.Should().Be("topicFilter");
        readMeta.Tags.Should().Contain("kafka");
    }

    [Test]
    public void SaveQuery_InvalidCypherSyntax_ThrowsException_AndDoesNotWriteFiles()
    {
        var metadata = new ProjectQueryMetadata
        {
            Description = "Invalid query"
        };

        const string invalidCypher = "MATCH (n WHERE n.id = 1";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _manager.SaveQuery(_tempDir, "bad_syntax", invalidCypher, metadata));

        ex!.Message.Should().Contain("syntax validation failed");

        var queriesDir = _manager.GetQueriesDirectory(_tempDir);
        var cypherFile = Path.Combine(queriesDir, "bad_syntax.cypher");
        File.Exists(cypherFile).Should().BeFalse();
    }

    [Test]
    public void SaveQuery_MutatingQuery_ThrowsSecurityException()
    {
        var metadata = new ProjectQueryMetadata
        {
            Description = "Mutating query"
        };

        const string mutatingCypher = "MATCH (n:Workspace) CREATE (m:Workspace {name: 'test'}) RETURN m";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _manager.SaveQuery(_tempDir, "mutating", mutatingCypher, metadata));

        ex!.Message.Should().Contain("Security violation");
    }

    [Test]
    public void ListQueries_ReturnsAllSavedQueries_IncludingBareCypherFiles()
    {
        // 1. Save one query with metadata
        _manager.SaveQuery(_tempDir, "query_with_meta", "MATCH (w:Workspace) RETURN w.name", new ProjectQueryMetadata
        {
            Description = "Has metadata",
            Tags = ["custom"]
        });

        // 2. Manually create a bare .cypher file without .json
        var queriesDir = _manager.GetQueriesDirectory(_tempDir);
        File.WriteAllText(Path.Combine(queriesDir, "bare_query.cypher"), "MATCH (p:Project) RETURN p.name");

        var list = _manager.ListQueries(_tempDir);

        list.Count.Should().Be(2);
        var withMeta = list.FirstOrDefault(x => x.Name == "query_with_meta");
        var bare = list.FirstOrDefault(x => x.Name == "bare_query");

        withMeta.Should().NotBeNull();
        withMeta!.Description.Should().Be("Has metadata");
        withMeta.Tags.Should().Contain("custom");

        bare.Should().NotBeNull();
        bare!.Description.Should().Contain("bare_query");
    }

    [Test]
    public void DeleteQuery_DeletesBothFiles()
    {
        var item = _manager.SaveQuery(_tempDir, "to_delete", "MATCH (w:Workspace) RETURN w.name", new ProjectQueryMetadata
        {
            Description = "Temporary"
        });

        File.Exists(item.CypherPath).Should().BeTrue();
        File.Exists(item.MetadataPath).Should().BeTrue();

        var deleted = _manager.DeleteQuery(_tempDir, "to_delete");
        deleted.Should().BeTrue();
        File.Exists(item.CypherPath).Should().BeFalse();
        File.Exists(item.MetadataPath).Should().BeFalse();
    }

    [Test]
    public async Task Repository_SaveListAndExecuteProjectQuery_WorksEndToEnd()
    {
        var dbPath = Path.Combine(_tempDir, "test.db");

        await using var client = new SqliteGraphClient(dbPath);

        var repo = new CodeExplorerRepository(client, _manager);

        // Seed simple graph data
        await client.ExecuteWriteAsync(
            "INSERT INTO nodes (id, kind, properties) VALUES ('ws1', 'Workspace', json_object('name', 'main_ws', 'path', @path))",
            new Dictionary<string, object?> { ["path"] = _tempDir });

        // 1. Save query via repository
        const string cypher = "MATCH (w:Workspace) WHERE w.name = $wsName RETURN w.id AS id, w.name AS name";

        var paramSchema = JsonSerializer.Serialize(new[]
        {
            new ProjectQueryParameter("wsName", "string", true, "Name of the workspace")
        });

        var saveResultJson = await repo.SaveProjectQueryAsync("find_workspace_by_name",
            "Finds a workspace by its exact name", cypher, paramSchema, "id, name", "workspace,lookup", _tempDir);

        saveResultJson.Should().Contain("successfully validated and saved");

        // 2. List queries
        var listJson = await repo.ListProjectQueriesAsync(_tempDir);
        listJson.Should().Contain("find_workspace_by_name");
        listJson.Should().Contain("wsName");

        // 3. Execute query with matching parameter
        var execJson = await repo.ExecuteProjectQueryAsync("find_workspace_by_name",
            JsonSerializer.Serialize(new { wsName = "main_ws" }), _tempDir);

        execJson.Should().Contain("main_ws");
        execJson.Should().Contain("ws1");

        // 4. Missing required parameter throws ArgumentException
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await repo.ExecuteProjectQueryAsync("find_workspace_by_name", "{}", _tempDir));
    }
}
