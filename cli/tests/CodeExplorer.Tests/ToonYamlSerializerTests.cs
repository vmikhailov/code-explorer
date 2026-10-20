using CodeExplorer.Core.Mcp;
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
    public void StandbyMessage_SupportsYamlAndToon()
    {
        var yamlMsg = CodeExplorerRepository.GetStandbyMessage("yaml");
        var toonMsg = CodeExplorerRepository.GetStandbyMessage("toon");
        var jsonMsg = CodeExplorerRepository.GetStandbyMessage("json");
        var mdMsg = CodeExplorerRepository.GetStandbyMessage("markdown");

        yamlMsg.Should().StartWith("status: standby");
        toonMsg.Should().StartWith("status: standby");
        jsonMsg.Should().Contain("\"status\":\"standby\"");
        mdMsg.Should().Contain("⚠️ **CodeExplorer Standby Mode**");
    }
}
