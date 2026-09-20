using CodeExplorer.Core.Mcp;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class OntologyRegistryTests
{
    [Test]
    public void KindMapping_ContainsCoreNodeKinds()
    {
        var mapping = OntologyRegistry.KindMapping;

        Assert.That(mapping, Is.Not.Null);
        Assert.That(mapping.ContainsKey("workspace"), Is.True, "Should contain 'workspace'");
        Assert.That(mapping.ContainsKey("project"), Is.True, "Should contain 'project'");
        Assert.That(mapping.ContainsKey("file"), Is.True, "Should contain 'file'");
        Assert.That(mapping.ContainsKey("function"), Is.True, "Should contain 'function'");
    }

    [Test]
    public void ToSnakeCase_ConvertsCorrectly()
    {
        Assert.That(OntologyRegistry.ToSnakeCase("Id"), Is.EqualTo("id"));
        Assert.That(OntologyRegistry.ToSnakeCase("Name"), Is.EqualTo("name"));
        Assert.That(OntologyRegistry.ToSnakeCase("ProjectType"), Is.EqualTo("project_type"));
        Assert.That(OntologyRegistry.ToSnakeCase("StartLine"), Is.EqualTo("start_line"));
        Assert.That(OntologyRegistry.ToSnakeCase(""), Is.EqualTo(""));
        Assert.That(OntologyRegistry.ToSnakeCase(null!), Is.Null);
    }

    [Test]
    public void GetNodeDefinition_ReturnsValidDefinitionForWorkspace()
    {
        var definition = OntologyRegistry.GetNodeDefinition("Workspace");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition, Does.Contain("### Kind: Workspace"));
        Assert.That(definition, Does.Contain("**Purpose**:"));
        Assert.That(definition, Does.Contain("**Key Properties**:"));
        Assert.That(definition, Does.Contain("`name` (string)"));
        Assert.That(definition, Does.Contain("`path` (string)"));
        Assert.That(definition, Does.Contain("**Relationships**:"));
        Assert.That(definition, Does.Contain("Outbound:"));
        Assert.That(definition, Does.Contain("- `-CONTAINS->` FilesStructure"));
    }

    [Test]
    public void GetNodeDefinition_ReturnsValidInboundRelationships()
    {
        // 'File' node has inbound relationships like 'Folder -CONTAINS-> File' or 'FilesStructure -CONTAINS-> File'
        var definition = OntologyRegistry.GetNodeDefinition("File");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition, Does.Contain("### Kind: File"));
        Assert.That(definition, Does.Contain("Inbound:"));
        Assert.That(definition, Does.Contain("Folder `-CONTAINS->` File"));
        Assert.That(definition, Does.Contain("FilesStructure `-CONTAINS->` File"));
    }

    [Test]
    public void GetNodeDefinition_ReturnsErrorForUnknownKind()
    {
        var definition = OntologyRegistry.GetNodeDefinition("SuperUnknownKindXYZ");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition, Does.Contain("Unknown node kind: 'SuperUnknownKindXYZ'"));
        Assert.That(definition, Does.Contain("Active ontological kinds in CodeExplorer are:"));
        Assert.That(definition, Does.Contain("'Workspace'"));
        Assert.That(definition, Does.Contain("'Project'"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetNodeDefinition_ReturnsErrorForInvalidKind(string? kind)
    {
        var definition = OntologyRegistry.GetNodeDefinition(kind!);
        Assert.That(definition, Is.EqualTo("Invalid node kind."));
    }
}
