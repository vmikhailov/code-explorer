using System.Text.Json;
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

        Assert.That(toon, Does.StartWith("[name,type,startLine,endLine]:"));
        Assert.That(toon, Does.Contain("  OrdersController,Class,3,7"));
        Assert.That(toon, Does.Contain("  chargeOrder,Function,5,6"));
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

        Assert.That(toon, Does.Contain("\"hello, world\""));
        Assert.That(toon, Does.Contain("\"says \"\"hi\"\"\""));
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

        Assert.That(toon, Does.Contain("  1,true,-"));
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

        Assert.That(yaml, Does.Contain("workspace: Demo"));
        Assert.That(yaml, Does.Contain("projects:"));
        Assert.That(yaml, Does.Contain("- name: Core"));
        Assert.That(yaml, Does.Contain("  lang: \"C#\""));
    }

    [Test]
    public void StandbyMessage_SupportsYamlAndToon()
    {
        var yamlMsg = CodeExplorerRepository.GetStandbyMessage("yaml");
        var toonMsg = CodeExplorerRepository.GetStandbyMessage("toon");
        var jsonMsg = CodeExplorerRepository.GetStandbyMessage("json");
        var mdMsg = CodeExplorerRepository.GetStandbyMessage("markdown");

        Assert.That(yamlMsg, Does.StartWith("status: standby"));
        Assert.That(toonMsg, Does.StartWith("status: standby"));
        Assert.That(jsonMsg, Does.Contain("\"status\":\"standby\""));
        Assert.That(mdMsg, Does.Contain("⚠️ **CodeExplorer Standby Mode**"));
    }
}
