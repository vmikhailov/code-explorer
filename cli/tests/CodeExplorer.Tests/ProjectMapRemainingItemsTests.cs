using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ProjectMapRemainingItemsTests
{
    [Test]
    public void BuiltInGateCatalog_RegistersModuleCycleProhibitionGate()
    {
        var defaultGates = BuiltInGateCatalog.GetDefaultGates();
        defaultGates.Should().Contain(g => g.Id == "SIG-C-B-004");

        var gate = BuiltInGateCatalog.ModuleCycleProhibitionGate;
        gate.Should().NotBeNull();
        gate.Id.Should().Be("SIG-C-B-004");
        gate.Name.Should().Be("Module Dependency Cycle Prohibition");
        gate.Query.Should().Contain("DEPENDS_ON");
        gate.Ratchet.Enabled.Should().BeTrue();
    }

    [Test]
    public async Task PostIndexAnalyzer_LinksOrphanedDdlTablesToCanonicalDatabase()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"ce_pm_rem_{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new SqliteGraphClient(tempDbPath);

            var nodes = new List<Node>
            {
                new("1:p:api", "Project", new() { ["name"] = "ApiProject", ["path"] = "src/api", ["is_test"] = false }),
                new("1:f:src/db/schema.sql", "File", new() { ["name"] = "schema.sql", ["path"] = "src/db/schema.sql" }),
                new("1:t:customers", "Table", new() { ["name"] = "customers" }),
                new("1:db:relational:app_db", "Database", new() { ["name"] = "app_db", ["path"] = "src/db", ["engine"] = "sqlite", ["db_type"] = "relational" })
            };
            await db.UploadNodesAsync(nodes);

            var edges = new List<Relationship>
            {
                new("1:p:api", "1:f:src/db/schema.sql", "CONTAINS", new()),
                new("1:f:src/db/schema.sql", "1:t:customers", "CONTAINS", new()),
                new("1:p:api", "1:db:relational:app_db", "USES_DB", new())
            };
            await db.UploadRelationshipsAsync(edges);

            // Run database canonicalization
            await PostIndexAnalyzer.CanonicalizeDatabasesAsync(db, "1");

            // Verify:
            // 1. Table is now linked to canonical database via CONTAINS
            var dbTableEdgesJson = (await db.ExecuteCypherAsync(
                $"SELECT fn.id AS from_id, tn.id AS to_id, ce_rel_kind(e.kind) AS kind FROM edges e JOIN nodes fn ON e.from_id = fn.rowid JOIN nodes tn ON e.to_id = tn.rowid WHERE tn.id = '1:t:customers' AND e.kind = {CodeExplorer.Cypher.Common.GraphKindCodec.EncodeRelKind("CONTAINS")} AND fn.id LIKE '%:db:%';",
                null)).ToJson();
            dbTableEdgesJson.Should().Contain("1:t:customers");

            // 2. Project is linked to Table via DEFINES
            var projTableEdgesJson = (await db.ExecuteCypherAsync(
                $"SELECT fn.id AS from_id, tn.id AS to_id, ce_rel_kind(e.kind) AS kind FROM edges e JOIN nodes fn ON e.from_id = fn.rowid JOIN nodes tn ON e.to_id = tn.rowid WHERE fn.id = '1:p:api' AND tn.id = '1:t:customers' AND e.kind = {CodeExplorer.Cypher.Common.GraphKindCodec.EncodeRelKind("DEFINES")};",
                null)).ToJson();
            projTableEdgesJson.Should().Contain("DEFINES");
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Test]
    public async Task ArchitectureViewEngine_ProjectMap_IncludesDdlSchemaTablesAsResources()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"ce_pm_res_{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new SqliteGraphClient(tempDbPath);

            var nodes = new List<Node>
            {
                new("1:p:api", "Project", new() { ["name"] = "ApiProject", ["path"] = "src/api", ["is_test"] = false }),
                new("1:f:src/api/db/schema.sql", "File", new() { ["name"] = "schema.sql", ["path"] = "src/api/db/schema.sql" }),
                new("1:t:orders", "Table", new() { ["name"] = "orders" })
            };
            await db.UploadNodesAsync(nodes);

            var edges = new List<Relationship>
            {
                new("1:p:api", "1:f:src/api/db/schema.sql", "CONTAINS", new()),
                new("1:f:src/api/db/schema.sql", "1:t:orders", "CONTAINS", new())
            };
            await db.UploadRelationshipsAsync(edges);

            var engine = new ArchitectureViewEngine(db);
            var result = await engine.GetProjectMapViewAsync("ApiProject");

            result.Should().NotBeNull();
            result.Nodes.Should().Contain(n => n.Name == "orders" || n.Id == "1:t:orders");
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Test]
    public async Task ArchitectureViewEngine_ProjectMap_FindsProjectsWithoutContainsEdges()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"ce_pm_fallback_{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new SqliteGraphClient(tempDbPath);

            var nodes = new List<Node>
            {
                new("1:p:apps/frontend:", "Project", new() { ["name"] = "frontend", ["path"] = "apps/frontend", ["is_test"] = false }),
                new("1:f:apps/frontend/src/index.ts", "File", new() { ["name"] = "index.ts", ["path"] = "apps/frontend/src/index.ts", ["project_name"] = "frontend", ["line_count"] = 42 })
            };
            await db.UploadNodesAsync(nodes);

            // Notice: ZERO (Project)-[:CONTAINS]->(File) edges are uploaded!

            var engine = new ArchitectureViewEngine(db);
            var result = await engine.GetProjectMapViewAsync("frontend");

            result.Should().NotBeNull();
            result.Metadata.Should().NotContainKey("error");
            result.Metadata.Should().ContainKey("projects");
            result.Metadata["projects"].Should().Contain("frontend");
            result.Nodes.Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }
}
