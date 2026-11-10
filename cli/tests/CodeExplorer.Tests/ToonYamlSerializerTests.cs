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
}
