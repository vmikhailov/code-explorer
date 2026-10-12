using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Parser.ColdFusion;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class ColdFusionParserTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cf_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
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
                // Best effort cleanup
            }
        }
    }

    [Test]
    public void Test_ColdFusionProjectParser_ApplicationCfc_Detection()
    {
        var appCfcPath = Path.Combine(_tempDir, "Application.cfc");
        File.WriteAllText(appCfcPath, """
            component {
                this.name = "NSAProd v2.0";
                this.datasource = 'NSA_Integrated';
                this.sessionManagement = true;
            }
            """);

        var parser = new ColdFusionProjectParser();
        var files = Directory.GetFiles(_tempDir);

        parser.IsProjectDirectory(_tempDir, files).Should().BeTrue();
        parser.GetProjectName(_tempDir, files).Should().Be("NSAProd");
        ColdFusionProjectParser.GetDefaultDatasource(_tempDir).Should().Be("NSA_Integrated");
    }

    [Test]
    public void Test_ColdFusionProjectParser_ApplicationCfm_Detection()
    {
        var appCfmPath = Path.Combine(_tempDir, "Application.cfm");
        File.WriteAllText(appCfmPath, """
            <cfapplication name="LegacyColdFusionApp" sessionmanagement="Yes" />
            """);

        var parser = new ColdFusionProjectParser();
        var files = Directory.GetFiles(_tempDir);

        parser.IsProjectDirectory(_tempDir, files).Should().BeTrue();
        parser.GetProjectName(_tempDir, files).Should().Be("LegacyColdFusionApp");
    }

    [Test]
    public void Test_ColdFusionProjectParser_DirectoryFallback()
    {
        var cfmPath = Path.Combine(_tempDir, "index.cfm");
        File.WriteAllText(cfmPath, "<h1>Hello World</h1>");

        var parser = new ColdFusionProjectParser();
        var files = Directory.GetFiles(_tempDir);

        parser.IsProjectDirectory(_tempDir, files).Should().BeTrue();
        var expectedName = Path.GetFileName(_tempDir);
        parser.GetProjectName(_tempDir, files).Should().Be(expectedName);
    }

    [Test]
    public async Task Test_ColdFusionFileParser_TagBased_ComponentsEndpointsAndQueries()
    {
        var cfcPath = Path.Combine(_tempDir, "UserService.cfc");
        File.WriteAllText(cfcPath, """
            <cfcomponent name="UserService" extends="BaseService">
                <cffunction name="getUser" access="remote" returntype="struct">
                    <cfargument name="id" type="numeric" required="true">
                    <cfquery name="qUser" datasource="NSA_Integrated">
                        SELECT id, username, email FROM Users WHERE id = <cfqueryparam value="#arguments.id#" cfsqltype="cf_sql_integer">
                    </cfquery>
                    <cfreturn qUser>
                </cffunction>

                <cffunction name="notifyUser" access="public">
                    <cfhttp url="https://api.notifications.com/v1/send" method="POST">
                        <cfhttpparam type="body" value="{'msg':'hello'}">
                    </cfhttp>
                </cffunction>
            </cfcomponent>
            """);

        var parser = new ColdFusionFileParser();
        var tree = await parser.ParseAsync(cfcPath, "parent", "ws", _tempDir);

        tree.Should().NotBeNull();
        var fileNode = tree.FileNode;
        fileNode.Should().NotBeNull();

        // 1. Verify Component
        var component = fileNode!.Children.OfType<TypeNode>().FirstOrDefault();
        component.Should().NotBeNull();
        component!.Name.Should().Be("UserService");
        component.Extensions.Should().NotBeNull();
        component.Extensions!["extends"].Should().Be("BaseService");

        // 2. Verify Function under Component
        var getUserFunc = component.Children.OfType<FunctionNode>().FirstOrDefault(f => f.Name == "getUser");
        getUserFunc.Should().NotBeNull();

        // 3. Verify Remote Endpoint
        var endpoint = fileNode.Children.OfType<EndpointNode>().FirstOrDefault(e => e.HttpMethod == "POST");
        endpoint.Should().NotBeNull();
        endpoint!.RouteTemplate.Should().Be("/UserService/getUser");

        // 4. Verify Database and Table
        var dbNode = fileNode.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "NSA_Integrated");
        dbNode.Should().NotBeNull();
        var tableNode = dbNode!.Children.OfType<DataSetNode>().SelectMany(s => s.Children.OfType<TableNode>()).FirstOrDefault(t => t.Name.Equals("Users", StringComparison.OrdinalIgnoreCase));
        tableNode.Should().NotBeNull();

        // 5. Verify External Service
        var extService = fileNode.Children.OfType<ExternalServiceNode>().FirstOrDefault();
        extService.Should().NotBeNull();
        extService!.DomainOrService.Should().Be("https://api.notifications.com/v1/send");
        extService.Protocol.Should().Be("http");
    }

    [Test]
    public async Task Test_ColdFusionFileParser_ScriptBased_ComponentsAndSOAP()
    {
        var cfcPath = Path.Combine(_tempDir, "OrderService.cfc");
        File.WriteAllText(cfcPath, """
            component extends="BaseService" {
                remote function getOrder(numeric orderId) {
                    var q = queryExecute("SELECT order_id, total, status FROM orders WHERE order_id = :orderId", { orderId: arguments.orderId }, { datasource: "OrdersDb" });
                    return q;
                }

                public function syncShipping() {
                    var ws = createObject("webservice", "https://soap.shipping.com/v2/service?wsdl");
                    return ws.getStatus();
                }
            }
            """);

        var parser = new ColdFusionFileParser();
        var tree = await parser.ParseAsync(cfcPath, "parent", "ws", _tempDir);

        tree.Should().NotBeNull();
        var fileNode = tree.FileNode;
        fileNode.Should().NotBeNull();

        // 1. Verify Component
        var component = fileNode!.Children.OfType<TypeNode>().FirstOrDefault();
        component.Should().NotBeNull();
        component!.Name.Should().Be("OrderService");

        // 2. Verify Remote Endpoint
        var endpoint = fileNode.Children.OfType<EndpointNode>().FirstOrDefault(e => e.HttpMethod == "POST");
        endpoint.Should().NotBeNull();
        endpoint!.RouteTemplate.Should().Be("/OrderService/getOrder");

        // 3. Verify Database and Table
        var dbNode = fileNode.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "OrdersDb");
        dbNode.Should().NotBeNull();
        var table = dbNode!.Children.OfType<DataSetNode>().SelectMany(s => s.Children.OfType<TableNode>()).FirstOrDefault(t => t.Name.Equals("orders", StringComparison.OrdinalIgnoreCase));
        table.Should().NotBeNull();

        // 4. Verify External SOAP Service
        var extSoap = fileNode.Children.OfType<ExternalServiceNode>().FirstOrDefault(s => s.Protocol == "soap");
        extSoap.Should().NotBeNull();
        extSoap!.DomainOrService.Should().Be("https://soap.shipping.com/v2/service?wsdl");
    }

    [Test]
    public async Task Test_ColdFusionFileParser_StoredProcedures_And_Inclusions()
    {
        var cfmPath = Path.Combine(_tempDir, "process.cfm");
        File.WriteAllText(cfmPath, """
            <cfinclude template="shared/header.cfm">
            <cfstoredproc procedure="usp_CalculateTaxes" datasource="TaxDb">
                <cfprocparam type="in" value="123">
            </cfstoredproc>
            <cfset auditObj = createObject("component", "cfc.AuditLogger")>
            """);

        var parser = new ColdFusionFileParser();
        var tree = await parser.ParseAsync(cfmPath, "parent", "ws", _tempDir);

        tree.Should().NotBeNull();
        var fileNode = tree.FileNode;
        fileNode.Should().NotBeNull();

        // 1. Verify Page Endpoint
        var pageEndpoint = fileNode!.Children.OfType<EndpointNode>().FirstOrDefault(e => e.HttpMethod == "GET");
        pageEndpoint.Should().NotBeNull();

        // 2. Verify Database Stored Procedure
        var dbNode = fileNode.Children.OfType<DatabaseNode>().FirstOrDefault(d => d.Name == "TaxDb");
        dbNode.Should().NotBeNull();
        var proc = dbNode!.Children.OfType<ProcedureNode>().FirstOrDefault(p => p.Name == "usp_CalculateTaxes");
        proc.Should().NotBeNull();

        // 3. Verify Inclusions & Type Bindings
        (tree.RawImports.Any(i => i.Path == "shared/header.cfm")).Should().BeTrue();
        (tree.RawTypeBindings.Any(b => b.TypeName == "cfc.AuditLogger")).Should().BeTrue();
    }
}
