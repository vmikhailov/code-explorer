using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ToonYamlSerializerTests
{
    [Test]
    public void SerializeToon_UniformObjectArray_ProducesTabularLayout()
    {
        var json = """
        [
          {"name": "OrdersController", "type": "Class", "startLine": 3, "endLine": 7},
          {"name": "chargeOrder", "type": "Function", "startLine": 5, "endLine": 6}
        ]
        """;

        var toon = ToonYamlSerializer.SerializeToon(json);

        toon.Should().StartWith("[name,type,startLine,endLine]:");
        toon.Should().Contain("  OrdersController,Class,3,7");
        toon.Should().Contain("  chargeOrder,Function,5,6");
    }

    [Test]
    public void SerializeToon_EscapesCommasAndQuotesInValues()
    {
        var json = """
        [
          {"name": "hello, world", "desc": "says \"hi\""}
        ]
        """;

        var toon = ToonYamlSerializer.SerializeToon(json);

        toon.Should().Contain("\"hello, world\"");
        toon.Should().Contain("\"says \"\"hi\"\"\"");
    }

    [Test]
    public void SerializeToon_HandlesNullAndBooleanPrimitives()
    {
        var json = """
        [
          {"id": 1, "isActive": true, "extra": null}
        ]
        """;

        var toon = ToonYamlSerializer.SerializeToon(json);

        toon.Should().Contain("  1,true,-");
    }

    [Test]
    public void SerializeYaml_ObjectWithNestedArray_ProducesValidYaml()
    {
        var json = """
        {
          "workspace": "Demo",
          "projects": [
            {"name": "Core", "lang": "C#"},
            {"name": "Web", "lang": "TypeScript"}
          ]
        }
        """;

        var yaml = ToonYamlSerializer.SerializeYaml(json);

        yaml.Should().Contain("workspace: Demo");
        yaml.Should().Contain("projects:");
        yaml.Should().Contain("- name: Core");
        yaml.Should().Contain("  lang: \"C#\"");
    }

    [Test]
    public void StandbyMessage_UsesYamlFormat()
    {
        var yamlMsg = CodeExplorerRepository.StandbyMessageYaml;
        yamlMsg.Should().StartWith("status: standby");
    }

    [Test]
    public void SerializeYaml_ScalarArray_FormatsInlineAndOmitsEmptyValues()
    {
        var json = """
        {
          "name": "Endpoint",
          "properties": ["id", "name", "route"],
          "emptyArr": [],
          "emptyStr": "",
          "nullVal": null
        }
        """;

        var yaml = ToonYamlSerializer.SerializeYaml(json);

        yaml.Should().Contain("name: Endpoint");
        yaml.Should().Contain("properties: [id, name, route]");
        yaml.Should().NotContain("emptyArr");
        yaml.Should().NotContain("emptyStr");
        yaml.Should().NotContain("nullVal");
    }

    [Test]
    public void ParseYamlOrJson_ParsesBlockYamlAndFlowSyntax()
    {
        var yaml = """
        - name: wsName
          type: string
          required: true
          description: Name of the workspace
        """;

        var parsed = ToonYamlSerializer.ParseYamlOrJson(yaml);
        parsed.Kind.Should().Be(CypherValueKind.Array);
        parsed.AsArray().Count.Should().Be(1);
        parsed.AsArray()[0].GetStringProp("name").Should().Be("wsName");
        parsed.AsArray()[0].TryGetProperty("required", out var req).Should().BeTrue();
        req.AsBool().Should().BeTrue();
    }

    [Test]
    public void FormatTaxonomyYaml_SeparatesCommonPropsNodesAndGroupedRelationships()
    {
        var taxonomyJson = """
        {
          "taxonomy": [
            {
              "label": "Endpoint",
              "properties": ["id", "name", "path", "project_id", "route", "bg_color"],
              "outgoing": [
                {"relationship": "CALLS", "target": "Function"}
              ]
            },
            {
              "label": "File",
              "properties": ["id", "name", "path", "project_id", "language"],
              "outgoing": [
                {"relationship": "CALLS", "target": "Function"}
              ]
            },
            {
              "label": "Function",
              "properties": ["id", "name", "path", "project_id", "is_async"],
              "outgoing": [
                {"relationship": "CALLS", "target": "Function"},
                {"relationship": "CALLS", "target": "Type"}
              ]
            },
            {
              "label": "SyntaxStructure",
              "properties": ["id"],
              "outgoing": []
            }
          ]
        }
        """;

        var yaml = CodeExplorerRepository.FormatTaxonomyYaml(taxonomyJson);

        yaml.Should().Contain("common_properties: [id, name, path, project_id]");
        yaml.Should().Contain("relationship_properties: [call_count, category, dependency_type, file_path, hops, line, operation]");
        yaml.Should().Contain("  L1_physical:");
        yaml.Should().Contain("    File: [language]");
        yaml.Should().Contain("  L3_syntactic:");
        yaml.Should().Contain("    Function: [is_async]");
        yaml.Should().Contain("  L4_semantic:");
        yaml.Should().Contain("    Endpoint: [route]");
        yaml.Should().NotContain("SyntaxStructure");
        yaml.Should().NotContain("bg_color");
        yaml.Should().Contain("  CALLS: [Endpoint|File -> Function, Function -> Function|Type]");
    }

    [Test]
    public void OntologyRegistry_ResolvesSystemNodesPropertiesAndCommonPropertiesFromAttributes()
    {
        OntologyRegistry.CommonPropertyKeys.Should().BeEquivalentTo(["id", "name", "path", "project_id"], o => o.WithStrictOrdering());
        OntologyRegistry.IsSystemNode("SyntaxStructure").Should().BeTrue();
        OntologyRegistry.IsSystemNode("ProjectSyntax").Should().BeTrue();
        OntologyRegistry.IsSystemNode("Counter").Should().BeTrue();
        OntologyRegistry.IsSystemNode("Function").Should().BeFalse();

        OntologyRegistry.IsSystemProperty("bg_color").Should().BeTrue();
        OntologyRegistry.IsSystemProperty("stage_order").Should().BeTrue();
        OntologyRegistry.IsSystemProperty("layerColor").Should().BeTrue();
        OntologyRegistry.IsSystemProperty("start_line").Should().BeFalse();

        var fnDef = OntologyRegistry.GetNodeDefinition("Function");
        fnDef.Should().Contain("start_line:");
        fnDef.Should().NotContain("stage_order:");
        fnDef.Should().NotContain("end_col:");
    }

    [Test]
    public void SerializeYaml_CypherQueryResult_DistinguishesEmptyStringFromNullAndQuotesCommas()
    {
        var cols = new List<string> { "project", "path", "operations" };
        var rows = new List<CypherRow>
        {
            new(cols, [CypherValue.FromString("root"), CypherValue.FromString(""), CypherValue.Null]),
            new(cols, [CypherValue.FromString("ModA"), CypherValue.FromString("src/ModA"), CypherValue.FromString("GetAsync, QueryIndex, SaveAsync")]),
            new(cols, [CypherValue.FromString("ModB"), CypherValue.FromString("src/ModB"), CypherValue.FromString("Query")]),
            new(cols, [CypherValue.FromString("ModC"), CypherValue.FromString("src/ModC"), CypherValue.FromString("Save")]),
            new(cols, [CypherValue.FromString("ModD"), CypherValue.FromString("src/ModD"), CypherValue.FromString("Delete")])
        };
        var result = new CypherQueryResult(cols, rows);

        var yaml = ToonYamlSerializer.SerializeYaml(result);

        yaml.Should().StartWith("columns: [project, path, operations]");
        yaml.Should().Contain("- [root, \"\", null]");
        yaml.Should().Contain("- [ModA, src/ModA, \"GetAsync, QueryIndex, SaveAsync\"]");
    }

    [Test]
    public async Task AccessedByEdgeMerging_PreservesDistinctMemberOperationsAcrossCallsAndBatches()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ce_op_merge_{Guid.NewGuid():N}.db");
        try
        {
            using var client = new SqliteGraphClient(dbPath);
            await client.UploadNodesAsync(
            [
                new Node("ws:m:session", NodeKind.Member, new Dictionary<string, object> { ["name"] = "_session" }),
                new Node("ws:fn:index", NodeKind.Function, new Dictionary<string, object> { ["name"] = "Index" })
            ]);

            await client.BeginBulkLoadAsync();
            await client.UploadRelationshipsAsync(
            [
                Relationship.FromRelationship(new CodeExplorer.Core.Common.Relationships.AccessedByRelationship("ws:m:session", "ws:fn:index") { Operation = "GetAsync", Line = 10, Column = 5 }),
                Relationship.FromRelationship(new CodeExplorer.Core.Common.Relationships.AccessedByRelationship("ws:m:session", "ws:fn:index") { Operation = "QueryIndex", Line = 12, Column = 5 }),
                Relationship.FromRelationship(new CodeExplorer.Core.Common.Relationships.AccessedByRelationship("ws:m:session", "ws:fn:index") { Operation = "GetAsync", Line = 14, Column = 5 })
            ]);
            await client.UploadRelationshipsAsync(
            [
                Relationship.FromRelationship(new CodeExplorer.Core.Common.Relationships.AccessedByRelationship("ws:m:session", "ws:fn:index") { Operation = "SaveAsync", Line = 18, Column = 5 })
            ]);
            await client.CommitBulkLoadAsync();

            var res = await client.ExecuteCypherAsync(
                "MATCH (m:Member)-[r:ACCESSED_BY]->(f:Function) RETURN r.operation AS op, r.call_count AS calls");

            res.Count.Should().Be(1);
            res.Rows[0]["op"].AsString().Should().Be("GetAsync, QueryIndex, SaveAsync");
            res.Rows[0]["calls"].AsInt64().Should().Be(4);
        }
        finally
        {
            try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { /* Best-effort temp cleanup */ }
        }
    }

    [Test]
    public async Task PolyglotWorkspace_RootPackageJsonDoesNotSynthesizeDependsOnToCSharpProjects()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ce_poly_root_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempRoot, ".codeexplorer", "graph.db");
        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(Path.Combine(tempRoot, ".codeexplorer"));
            var csDir = Path.Combine(tempRoot, "src", "OrchardCore.ContentManagement.Abstractions");
            Directory.CreateDirectory(csDir);

            await File.WriteAllTextAsync(Path.Combine(tempRoot, "package.json"), """{"name": "@orchardcore/root", "private": true}""");
            await File.WriteAllTextAsync(Path.Combine(csDir, "OrchardCore.ContentManagement.Abstractions.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>""");
            await File.WriteAllTextAsync(Path.Combine(csDir, "IContentManager.cs"),
                "namespace OrchardCore.ContentManagement; public interface IContentManager { }");

            using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempRoot, tempRoot, clear: true);

            var deps = await client.ExecuteCypherAsync(
                "MATCH (p:Project)-[r:DEPENDS_ON]->(t:Project {name: 'OrchardCore.ContentManagement.Abstractions'}) RETURN p.name AS src, p.project_type AS lang");

            deps.Count.Should().Be(0);
        }
        finally
        {
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { /* Best-effort temp cleanup */ }
        }
    }

    [Test]
    public void SerializeYaml_SingleColumnScalarResult_ProducesFlatYamlArray()
    {
        var cols = new List<string> { "f.path" };
        var rows = new List<CypherRow>
        {
            new(cols, [CypherValue.FromString("apps/help-centre/project.json")]),
            new(cols, [CypherValue.FromString("apps/landing-pages/project.json")]),
            new(cols, [CypherValue.FromString("apps/lounge-key/project.json")])
        };
        var result = new CypherQueryResult(cols, rows);

        var yaml = ToonYamlSerializer.SerializeYaml(result);

        yaml.Replace("\r\n", "\n").Should().Be("""
            - apps/help-centre/project.json
            - apps/landing-pages/project.json
            - apps/lounge-key/project.json
            """.Replace("\r\n", "\n"));
    }

    [Test]
    public void SerializeYaml_SingleColumnObjectResult_ProducesObjectListWithoutColumnWrapper()
    {
        var cols = new List<string> { "f" };
        var node1 = CypherValue.FromObject(
        [
            new KeyValuePair<string, CypherValue>("id", CypherValue.FromString("file:1")),
            new KeyValuePair<string, CypherValue>("name", CypherValue.FromString("project.json"))
        ]);
        var node2 = CypherValue.FromObject(
        [
            new KeyValuePair<string, CypherValue>("id", CypherValue.FromString("file:2")),
            new KeyValuePair<string, CypherValue>("name", CypherValue.FromString("package.json"))
        ]);
        var rows = new List<CypherRow>
        {
            new(cols, [node1]),
            new(cols, [node2])
        };
        var result = new CypherQueryResult(cols, rows);

        var yaml = ToonYamlSerializer.SerializeYaml(result);

        yaml.Should().NotContain("- f:");
        yaml.Should().Contain("- id: \"file:1\"");
        yaml.Should().Contain("  name: project.json");
        yaml.Should().Contain("- id: \"file:2\"");
        yaml.Should().Contain("  name: package.json");
    }

    [Test]
    public void SerializeYaml_MultiRowTableWithScalarArrays_UsesCompactTabularFormat()
    {
        var cols = new List<string> { "module", "class_name", "ops" };
        var rows = Enumerable.Range(1, 5).Select(i => new CypherRow(cols,
        [
            CypherValue.FromString($"Mod{i}"),
            CypherValue.FromString($"Controller{i}"),
            CypherValue.FromArray(i == 5
                ? []
                : [CypherValue.FromString("AuthorizeAsync"), CypherValue.FromString("GetAsync, SaveAsync")])
        ])).ToList();

        var result = new CypherQueryResult(cols, rows);
        var yaml = ToonYamlSerializer.SerializeYaml(result);

        yaml.Should().StartWith("columns: [module, class_name, ops]\nrows:\n");
        yaml.Should().Contain("- [Mod1, Controller1, [AuthorizeAsync, \"GetAsync, SaveAsync\"]]");
        yaml.Should().Contain("- [Mod5, Controller5, []]");

        var parsed = ToonYamlSerializer.ParseYamlOrJson(yaml);
        parsed.TryGetProperty("rows", out var rowsVal).Should().BeTrue();
        var parsedRows = rowsVal.AsArray();
        parsedRows.Count.Should().Be(5);
        parsedRows[0].AsArray()[2].AsArray().Count.Should().Be(2);
        parsedRows[0].AsArray()[2].AsArray()[1].AsString().Should().Be("GetAsync, SaveAsync");
        parsedRows[4].AsArray()[2].AsArray().Count.Should().Be(0);
    }
}
