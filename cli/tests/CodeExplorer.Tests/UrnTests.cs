using CodeExplorer.Core.Common;
using NUnit.Framework;
using AwesomeAssertions;

namespace CodeExplorer.Tests;

[TestFixture]
public class UrnTests
{
    [Test]
    public void TryParse_SymbolWithWindowsDriveLetter_ParsesCorrectly()
    {
        var raw = "ws:symbol:C:/work/repo/src/Order.cs:Type:Order:42";
        var ok = Urn.TryParse(raw, out var urn);

        ok.Should().BeTrue();
        urn.Prefix.Should().Be("ws");
        urn.Domain.Should().Be("symbol");
        urn.Path.Should().Be("C:/work/repo/src/Order.cs");
        urn.Kind.Should().Be("Type");
        urn.Name.Should().Be("Order");
        urn.Line.Should().Be(42);
    }

    [Test]
    public void TryParse_SymbolWithGenericType_ParsesCorrectly()
    {
        var raw = "ws:symbol:src/Order.cs:Type:Dictionary<string, int>:15";
        var ok = Urn.TryParse(raw, out var urn);

        ok.Should().BeTrue();
        urn.Prefix.Should().Be("ws");
        urn.Domain.Should().Be("symbol");
        urn.Path.Should().Be("src/Order.cs");
        urn.Kind.Should().Be("Type");
        urn.Name.Should().Be("Dictionary<string, int>");
        urn.Line.Should().Be(15);
    }

    [Test]
    public void TryParse_SymbolWithoutLineNumber_ParsesCorrectly()
    {
        var raw = "ws:symbol:src/Order.cs:Type:Order";
        var ok = Urn.TryParse(raw, out var urn);

        ok.Should().BeTrue();
        urn.Prefix.Should().Be("ws");
        urn.Domain.Should().Be("symbol");
        urn.Path.Should().Be("src/Order.cs");
        urn.Kind.Should().Be("Type");
        urn.Name.Should().Be("Order");
        urn.Line.Should().BeNull();
    }

    [Test]
    public void TryParse_CanonicalUrnCeResource_ParsesCorrectly()
    {
        var raw = "urn:ce:default:res:db:postgres:orders_db";
        var ok = Urn.TryParse(raw, out var urn);

        ok.Should().BeTrue();
        urn.Prefix.Should().Be("urn:ce:default");
        urn.Domain.Should().Be("res");
        urn.SubDomain.Should().Be("db");
        urn.Kind.Should().Be("postgres");
        urn.Name.Should().Be("orders_db");
    }

    [Test]
    public void TryParse_ProjectUrn_ParsesCorrectly()
    {
        var raw = "ws:project:services/order-service:";
        var ok = Urn.TryParse(raw, out var urn);

        ok.Should().BeTrue();
        urn.Prefix.Should().Be("ws");
        urn.Domain.Should().Be("project");
        urn.Path.Should().Be("services/order-service");
        urn.Name.Should().Be("order-service");
    }

    [Test]
    public void TryParse_EndpointUrn_ParsesCorrectly()
    {
        var raw = "ws:endpoint:api/orders:POST";
        var ok = Urn.TryParse(raw, out var urn);

        ok.Should().BeTrue();
        urn.Prefix.Should().Be("ws");
        urn.Domain.Should().Be("endpoint");
        urn.Path.Should().Be("api/orders");
        urn.Kind.Should().Be("POST");
    }
}
