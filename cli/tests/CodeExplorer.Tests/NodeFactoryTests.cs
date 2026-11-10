using System.Buffers;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes.Layer5_Logical;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class NodeFactoryTests
{
    [Test]
    public void NodeFactory_Create_ExtractsExpectedPropertiesAndTypes_AcrossAllNodeLayers()
    {
        var tableNode = new TableNode("tbl:1", "users", "src/models/user.cs", new Dictionary<string, string>
        {
            ["line"] = "42",
            ["size_bytes"] = "8192",
            ["is_test"] = "true",
            ["custom_key"] = "custom_val"
        });
        var createdTable = NodeFactory.Create(tableNode);
        createdTable.Id.Should().Be("tbl:1");
        createdTable.Kind.Should().Be(NodeKind.Table);
        createdTable.Properties["name"].Should().Be("users");
        createdTable.Properties["path"].Should().Be("src/models/user.cs");
        createdTable.Properties["line"].Should().Be(42);
        createdTable.Properties["size_bytes"].Should().Be(8192L);
        createdTable.Properties["is_test"].Should().Be(true);
        createdTable.Properties["custom_key"].Should().Be("custom_val");

        var typeNode = new TypeNode("type:1", "UserService", "App.UserService", "src/user.cs", "user.cs", 10, 50, 1, 1, "class", false, ProjectEntityKind.Service, "App.Core", 1, 40);
        var createdType = NodeFactory.Create(typeNode);
        createdType.Id.Should().Be("type:1");
        createdType.Kind.Should().Be(NodeKind.Type);
        createdType.Properties["name"].Should().Be("UserService");
        createdType.Properties["symbol"].Should().Be("App.UserService");
        createdType.Properties["start_line"].Should().Be(10);
        createdType.Properties["end_line"].Should().Be(50);
        createdType.Properties["project_kind"].Should().Be("Service");
        createdType.Properties["project_name"].Should().Be("App.Core");
        createdType.Properties["loc"].Should().Be(40);

        var fileNode = new FileNode("f:1", "user.cs", "src/user.cs", "/workspace/src/user.cs", false, ProjectEntityKind.Service, "App.Core", 100, 2048L);
        var createdFile = NodeFactory.Create(fileNode);
        createdFile.Kind.Should().Be(NodeKind.File);
        createdFile.Properties["full_path"].Should().Be("/workspace/src/user.cs");
        createdFile.Properties["line_count"].Should().Be(100);
        createdFile.Properties["size_bytes"].Should().Be(2048L);

        var endpointNode = new EndpointNode("ep:1", "GET /api/users", "src/user.cs", "GET", "/api/users");
        var createdEp = NodeFactory.Create(endpointNode);
        createdEp.Kind.Should().Be(NodeKind.Endpoint);
        createdEp.Properties["http_method"].Should().Be("GET");
        createdEp.Properties["route_template"].Should().Be("/api/users");

        var domainNode = new DomainNode("dom:1", "UserManagement", "User Management", "Core", "Manages users and auth", "user", "src/domains/user");
        var createdDomain = NodeFactory.Create(domainNode);
        createdDomain.Kind.Should().Be(NodeKind.Domain);
        createdDomain.Properties["display_name"].Should().Be("User Management");
        createdDomain.Properties["domain_type"].Should().Be("Core");

        var bcNode = new BoundedContextNode("bc:1", "Identity", "Core identity", "dom:1", "Summary", "blue", "gray", 95.5, "src/identity");
        var createdBc = NodeFactory.Create(bcNode);
        createdBc.Kind.Should().Be(NodeKind.BoundedContext);
        createdBc.Properties["purity_percentage"].Should().Be(95.5);

        var catchNode = new CatchClauseNode("catch:1", "catch", "ArgumentNullException", "src/user.cs", 30, 1, false, true, 2);
        var createdCatch = NodeFactory.Create(catchNode);
        createdCatch.Kind.Should().Be(NodeKind.CatchClause);
        createdCatch.Properties["exception_type"].Should().Be("ArgumentNullException");
        createdCatch.Properties["is_empty"].Should().Be(false);
        createdCatch.Properties["has_comment"].Should().Be(true);
        createdCatch.Properties["statement_count"].Should().Be(2);
    }

    [Test]
    public void NodeFactory_WriteJsonProperties_MatchesMaterializedProperties()
    {
        var fnNode = new FunctionNode(
            "fn:1", "GetUser", "App.UserService.GetUser", "src/user.cs", "user.cs",
            15, 25, 5, 1, false, ProjectEntityKind.Service, "App.Core", 1, 10,
            new Dictionary<string, string> { ["is_async"] = "true", ["call_line"] = "18" });

        var dbNode = NodeFactory.Create(fnNode);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            NodeFactory.WriteJsonProperties(dbNode, writer);
        }

        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        var parsed = CypherValue.TryParseJsonText(json);
        parsed.GetStringProp("name").Should().Be("GetUser");
        parsed.GetStringProp("symbol").Should().Be("App.UserService.GetUser");
        parsed.GetIntProp("start_line").Should().Be(15);
        parsed.GetIntProp("end_line").Should().Be(25);
        parsed.GetBoolProp("is_async").Should().BeTrue();
        parsed.GetIntProp("call_line").Should().Be(18);

        var schemaKeys = NodeFactory.GetSchemaPropertyKeys(typeof(FunctionNode));
        schemaKeys.Should().Contain(["id", "name", "symbol", "start_line", "loc"]);
    }
}

