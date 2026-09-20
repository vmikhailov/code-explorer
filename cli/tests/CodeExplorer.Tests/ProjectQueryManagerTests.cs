using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using NUnit.Framework;

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

        Assert.That(item, Is.Not.Null);
        Assert.That(item.Name, Is.EqualTo("find_kafka_consumers"));
        Assert.That(File.Exists(item.CypherPath), Is.True);
        Assert.That(File.Exists(item.MetadataPath), Is.True);

        var savedCypher = File.ReadAllText(item.CypherPath);
        Assert.That(savedCypher, Is.EqualTo(cypher));

        var metaJson = File.ReadAllText(item.MetadataPath);
        var readMeta = JsonSerializer.Deserialize<ProjectQueryMetadata>(metaJson);
        Assert.That(readMeta, Is.Not.Null);
        Assert.That(readMeta!.Description, Is.EqualTo(metadata.Description));
        Assert.That(readMeta.Parameters.Count, Is.EqualTo(1));
        Assert.That(readMeta.Parameters[0].Name, Is.EqualTo("topicFilter"));
        Assert.That(readMeta.Tags, Does.Contain("kafka"));
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

        Assert.That(ex!.Message, Does.Contain("syntax validation failed"));

        var queriesDir = _manager.GetQueriesDirectory(_tempDir);
        var cypherFile = Path.Combine(queriesDir, "bad_syntax.cypher");
        Assert.That(File.Exists(cypherFile), Is.False);
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

        Assert.That(ex!.Message, Does.Contain("Security violation"));
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

        Assert.That(list.Count, Is.EqualTo(2));
        var withMeta = list.FirstOrDefault(x => x.Name == "query_with_meta");
        var bare = list.FirstOrDefault(x => x.Name == "bare_query");

        Assert.That(withMeta, Is.Not.Null);
        Assert.That(withMeta!.Description, Is.EqualTo("Has metadata"));
        Assert.That(withMeta.Tags, Does.Contain("custom"));

        Assert.That(bare, Is.Not.Null);
        Assert.That(bare!.Description, Does.Contain("bare_query"));
    }

    [Test]
    public void DeleteQuery_DeletesBothFiles()
    {
        var item = _manager.SaveQuery(_tempDir, "to_delete", "MATCH (w:Workspace) RETURN w.name", new ProjectQueryMetadata
        {
            Description = "Temporary"
        });

        Assert.That(File.Exists(item.CypherPath), Is.True);
        Assert.That(File.Exists(item.MetadataPath), Is.True);

        var deleted = _manager.DeleteQuery(_tempDir, "to_delete");
        Assert.That(deleted, Is.True);
        Assert.That(File.Exists(item.CypherPath), Is.False);
        Assert.That(File.Exists(item.MetadataPath), Is.False);
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

        var saveResultJson = await repo.SaveProjectQueryAsync(
            "find_workspace_by_name",
            "Finds a workspace by its exact name",
            cypher,
            paramSchema,
            "id, name",
            "workspace,lookup",
            _tempDir);

        Assert.That(saveResultJson, Does.Contain("successfully validated and saved"));

        // 2. List queries
        var listJson = await repo.ListProjectQueriesAsync(_tempDir);
        Assert.That(listJson, Does.Contain("find_workspace_by_name"));
        Assert.That(listJson, Does.Contain("wsName"));

        // 3. Execute query with matching parameter
        var execJson = await repo.ExecuteProjectQueryAsync(
            "find_workspace_by_name",
            JsonSerializer.Serialize(new { wsName = "main_ws" }),
            _tempDir);

        Assert.That(execJson, Does.Contain("main_ws"));
        Assert.That(execJson, Does.Contain("ws1"));

        // 4. Missing required parameter throws ArgumentException
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await repo.ExecuteProjectQueryAsync("find_workspace_by_name", "{}", _tempDir));
    }
}
