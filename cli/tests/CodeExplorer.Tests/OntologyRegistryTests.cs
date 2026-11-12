using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class OntologyRegistryTests
{
    [Test]
    public void KindMapping_ContainsCoreNodeKinds()
    {
        var mapping = OntologyRegistry.KindMapping;

        mapping.Should().NotBeNull();
        mapping.ContainsKey("workspace").Should().BeTrue("Should contain 'workspace'");
        mapping.ContainsKey("project").Should().BeTrue("Should contain 'project'");
        mapping.ContainsKey("file").Should().BeTrue("Should contain 'file'");
        mapping.ContainsKey("function").Should().BeTrue("Should contain 'function'");
    }

    [Test]
    public void ToSnakeCase_ConvertsCorrectly()
    {
        OntologyRegistry.ToSnakeCase("Id").Should().Be("id");
        OntologyRegistry.ToSnakeCase("Name").Should().Be("name");
        OntologyRegistry.ToSnakeCase("ProjectType").Should().Be("project_type");
        OntologyRegistry.ToSnakeCase("StartLine").Should().Be("start_line");
        OntologyRegistry.ToSnakeCase("").Should().Be("");
        OntologyRegistry.ToSnakeCase(null!).Should().BeNull();
    }

    [Test]
    public void GetNodeDefinition_ReturnsValidDefinitionForWorkspace()
    {
        var definition = OntologyRegistry.GetNodeDefinition("Workspace");

        definition.Should().NotBeNull();
        definition.Should().Contain("kind: Workspace");
        definition.Should().Contain("purpose:");
        definition.Should().Contain("properties:");
        definition.Should().Contain("name: string");
        definition.Should().Contain("path: string");
        definition.Should().Contain("outbound_relationships:");
        definition.Should().Contain("- CONTAINS -> FilesStructure");
    }

    [Test]
    public void GetNodeDefinition_ReturnsValidInboundRelationships()
    {
        // 'File' node has inbound relationships like 'Folder -CONTAINS-> File' or 'FilesStructure -CONTAINS-> File'
        var definition = OntologyRegistry.GetNodeDefinition("File");

        definition.Should().NotBeNull();
        definition.Should().Contain("kind: File");
        definition.Should().Contain("inbound_relationships:");
        definition.Should().Contain("Folder -CONTAINS-> File");
        definition.Should().Contain("FilesStructure -CONTAINS-> File");

        OntologyRegistry.GetNodeDefinition("CallLiteral").Should().Contain("Function -PASSES_LITERAL-> CallLiteral");
        OntologyRegistry.GetNodeDefinition("CatchClause").Should().Contain("Function -DECLARES-> CatchClause");
        OntologyRegistry.GetNodeDefinition("GitSettings").Should().Contain("Project -USES_GIT-> GitSettings");
    }

    [Test]
    public void GetNodeDefinition_ReturnsErrorForUnknownKind()
    {
        var definition = OntologyRegistry.GetNodeDefinition("SuperUnknownKindXYZ");

        definition.Should().NotBeNull();
        definition.Should().Contain("Unknown node kind: 'SuperUnknownKindXYZ'");
        definition.Should().Contain("Active ontological kinds in CodeExplorer are:");
        definition.Should().Contain("'Workspace'");
        definition.Should().Contain("'Project'");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetNodeDefinition_ReturnsErrorForInvalidKind(string? kind)
    {
        var definition = OntologyRegistry.GetNodeDefinition(kind!);
        definition.Should().Be("Invalid node kind.");
    }

    [Test]
    public void SystemNodes_HaveIsSystemNodeFlag_True()
    {
        var expectedSystemKinds = new[] { "Counter", "FilesStructure", "ProjectsStructure", "SyntaxStructure", "SemanticStructure", "ProjectSyntax" };

        foreach (var kind in expectedSystemKinds)
        {
            var node = OntologyRegistry.AllNodes.FirstOrDefault(n => string.Equals(n.Kind.ToCypherLabel(), kind, StringComparison.OrdinalIgnoreCase));
            node.Should().NotBeNull($"Node '{kind}' should exist in AllNodes");
            node!.Attribute.IsSystemNode.Should().BeTrue($"Node '{kind}' should have IsSystemNode = true");
            OntologyRegistry.IsSystemNode(kind).Should().BeTrue($"IsSystemNode('{kind}') should return true");
        }
    }

    [Test]
    public void DomainNodes_HaveIsSystemNodeFlag_False()
    {
        var domainKinds = new[] { "Project", "File", "Folder", "Service", "App", "Worker", "Library", "Database", "Topic", "Type", "Function" };

        foreach (var kind in domainKinds)
        {
            var node = OntologyRegistry.AllNodes.FirstOrDefault(n => string.Equals(n.Kind.ToCypherLabel(), kind, StringComparison.OrdinalIgnoreCase));
            node.Should().NotBeNull($"Node '{kind}' should exist in AllNodes");
            node!.Attribute.IsSystemNode.Should().BeFalse($"Node '{kind}' should have IsSystemNode = false");
            OntologyRegistry.IsSystemNode(kind).Should().BeFalse($"IsSystemNode('{kind}') should return false");
        }
    }
}
