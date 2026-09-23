using CodeExplorer.Core.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class UrnTests
{
    [Test]
    public void TryParse_SymbolWithWindowsDriveLetter_ParsesCorrectly()
    {
        var raw = "ws:symbol:C:/work/repo/src/Order.cs:Type:Order:42";
        var ok = Urn.TryParse(raw, out var urn);

        Assert.That(ok, Is.True);
        Assert.That(urn.Prefix, Is.EqualTo("ws"));
        Assert.That(urn.Domain, Is.EqualTo("symbol"));
        Assert.That(urn.Path, Is.EqualTo("C:/work/repo/src/Order.cs"));
        Assert.That(urn.Kind, Is.EqualTo("Type"));
        Assert.That(urn.Name, Is.EqualTo("Order"));
        Assert.That(urn.Line, Is.EqualTo(42));
    }

    [Test]
    public void TryParse_SymbolWithGenericType_ParsesCorrectly()
    {
        var raw = "ws:symbol:src/Order.cs:Type:Dictionary<string, int>:15";
        var ok = Urn.TryParse(raw, out var urn);

        Assert.That(ok, Is.True);
        Assert.That(urn.Prefix, Is.EqualTo("ws"));
        Assert.That(urn.Domain, Is.EqualTo("symbol"));
        Assert.That(urn.Path, Is.EqualTo("src/Order.cs"));
        Assert.That(urn.Kind, Is.EqualTo("Type"));
        Assert.That(urn.Name, Is.EqualTo("Dictionary<string, int>"));
        Assert.That(urn.Line, Is.EqualTo(15));
    }

    [Test]
    public void TryParse_SymbolWithoutLineNumber_ParsesCorrectly()
    {
        var raw = "ws:symbol:src/Order.cs:Type:Order";
        var ok = Urn.TryParse(raw, out var urn);

        Assert.That(ok, Is.True);
        Assert.That(urn.Prefix, Is.EqualTo("ws"));
        Assert.That(urn.Domain, Is.EqualTo("symbol"));
        Assert.That(urn.Path, Is.EqualTo("src/Order.cs"));
        Assert.That(urn.Kind, Is.EqualTo("Type"));
        Assert.That(urn.Name, Is.EqualTo("Order"));
        Assert.That(urn.Line, Is.Null);
    }

    [Test]
    public void TryParse_CanonicalUrnCeResource_ParsesCorrectly()
    {
        var raw = "urn:ce:default:res:db:postgres:orders_db";
        var ok = Urn.TryParse(raw, out var urn);

        Assert.That(ok, Is.True);
        Assert.That(urn.Prefix, Is.EqualTo("urn:ce:default"));
        Assert.That(urn.Domain, Is.EqualTo("res"));
        Assert.That(urn.SubDomain, Is.EqualTo("db"));
        Assert.That(urn.Kind, Is.EqualTo("postgres"));
        Assert.That(urn.Name, Is.EqualTo("orders_db"));
    }

    [Test]
    public void TryParse_ProjectUrn_ParsesCorrectly()
    {
        var raw = "ws:project:services/order-service:";
        var ok = Urn.TryParse(raw, out var urn);

        Assert.That(ok, Is.True);
        Assert.That(urn.Prefix, Is.EqualTo("ws"));
        Assert.That(urn.Domain, Is.EqualTo("project"));
        Assert.That(urn.Path, Is.EqualTo("services/order-service"));
        Assert.That(urn.Name, Is.EqualTo("order-service"));
    }

    [Test]
    public void TryParse_EndpointUrn_ParsesCorrectly()
    {
        var raw = "ws:endpoint:api/orders:POST";
        var ok = Urn.TryParse(raw, out var urn);

        Assert.That(ok, Is.True);
        Assert.That(urn.Prefix, Is.EqualTo("ws"));
        Assert.That(urn.Domain, Is.EqualTo("endpoint"));
        Assert.That(urn.Path, Is.EqualTo("api/orders"));
        Assert.That(urn.Kind, Is.EqualTo("POST"));
    }
}
