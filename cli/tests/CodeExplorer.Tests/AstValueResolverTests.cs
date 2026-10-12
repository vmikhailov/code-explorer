using AwesomeAssertions;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class AstValueResolverTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [SetUp]
    public void Setup()
    {
        ConstantRegistry.Clear();
        ConfigStore.Clear();
    }

    [Test]
    public void AstConstantExtractor_ResolvesCSharpConstantsAndInterpolatedStrings()
    {
        var code = @"
namespace TestApp.Constants;

public static class TopicNames
{
    public const string Environment = ""production"";
    public const string OrderBase = $""company.{Environment}.orders"";
    public const string OrderCreated = OrderBase + "".created"";
    public static readonly string OrderCancelled = $""{OrderBase}.cancelled"";
    public static string PaymentQueue => OrderBase + "".payments"";
}
";


        AstConstantExtractor.ExtractAndRegister("src/TopicNames.cs", code, "TestApp");

        ConstantRegistry.TryResolve("TestApp", "Environment", out var env).Should().BeTrue();
        env.Should().Be("production");

        ConstantRegistry.TryResolve("TestApp", "OrderBase", out var orderBase).Should().BeTrue();
        orderBase.Should().Be("company.production.orders");

        ConstantRegistry.TryResolve("TestApp", "OrderCreated", out var orderCreated).Should().BeTrue();
        orderCreated.Should().Be("company.production.orders.created");

        ConstantRegistry.TryResolve("TestApp", "OrderCancelled", out var orderCancelled).Should().BeTrue();
        orderCancelled.Should().Be("company.production.orders.cancelled");

        ConstantRegistry.TryResolve("TestApp", "PaymentQueue", out var paymentQueue).Should().BeTrue();
        paymentQueue.Should().Be("company.production.orders.payments");

        // Qualified lookup
        ConstantRegistry.TryResolve("TestApp", "TopicNames.OrderCreated", out var qualCreated).Should().BeTrue();
        qualCreated.Should().Be("company.production.orders.created");
    }

    [Test]
    public void AstConstantExtractor_ResolvesTypeScriptEnumsAndTemplateStrings()
    {
        var code = @"
export const DOMAIN = 'billing';
export const INVOICE_PREFIX = `${DOMAIN}.invoices`;

export const Topics = {
    InvoiceCreated: `${INVOICE_PREFIX}.created`,
    InvoicePaid: `${INVOICE_PREFIX}.paid`
};

export enum Queues {
    PaymentProcessing = 'payment-processing-queue',
    RefundProcessing = 'refund-processing-queue'
}
";
        AstConstantExtractor.ExtractAndRegister("src/topics.ts", code, "BillingService");

        ConstantRegistry.TryResolve("BillingService", "DOMAIN", out var domain).Should().BeTrue();
        domain.Should().Be("billing");

        ConstantRegistry.TryResolve("BillingService", "INVOICE_PREFIX", out var prefix).Should().BeTrue();
        prefix.Should().Be("billing.invoices");

        ConstantRegistry.TryResolve("BillingService", "Topics.InvoiceCreated", out var created).Should().BeTrue();
        created.Should().Be("billing.invoices.created");

        ConstantRegistry.TryResolve("BillingService", "Topics.InvoicePaid", out var paid).Should().BeTrue();
        paid.Should().Be("billing.invoices.paid");

        ConstantRegistry.TryResolve("BillingService", "Queues.PaymentProcessing", out var queue).Should().BeTrue();
        queue.Should().Be("payment-processing-queue");
    }

    [Test]
    public void AstConstantExtractor_ResolvesGoConstants()
    {
        var code = @"
package messaging

const (
    Prefix = ""store""
    OrdersTopic = Prefix + "".orders""
)
";
        AstConstantExtractor.ExtractAndRegister("messaging/topics.go", code, "StoreService");

        ConstantRegistry.TryResolve("StoreService", "Prefix", out var prefix).Should().BeTrue();
        prefix.Should().Be("store");

        ConstantRegistry.TryResolve("StoreService", "OrdersTopic", out var topic).Should().BeTrue();
        topic.Should().Be("store.orders");
    }

    [Test]
    public void AstConstantExtractor_ResolvesPythonConstants()
    {
        var code = @"
SERVICE_NAME = 'catalog'
BASE_TOPIC = f'{SERVICE_NAME}.items'
ITEM_CREATED_TOPIC = BASE_TOPIC + '.created'
";
        AstConstantExtractor.ExtractAndRegister("catalog/topics.py", code, "CatalogService");

        ConstantRegistry.TryResolve("CatalogService", "SERVICE_NAME", out var svc).Should().BeTrue();
        svc.Should().Be("catalog");

        ConstantRegistry.TryResolve("CatalogService", "ITEM_CREATED_TOPIC", out var topic).Should().BeTrue();
        topic.Should().Be("catalog.items.created");
    }

    [Test]
    public void AstValueResolver_ResolvesReachingDefinitionInLocalScope()
    {
        // Test AST parser with local reaching definition:
        // const queue = 'orders-v2';
        // rabbit.createQueue(queue);
        var tsCode = @"
function setupRabbit(rabbit: any) {
    const queueName = 'orders-v2-queue';
    rabbit.createQueue(queueName);
}
";
        var language = SyntaxTree.GetLanguage("typescript");

        using var parser = new TreeSitter.Parser(language);

        using var tree = parser.Parse(tsCode);

        tree.Should().NotBeNull();

        // Find the call expression rabbit.createQueue(queueName)
        var call = tree!.RootNode.FindDescendantOfType("call_expression");
        call.IsValid().Should().BeTrue();

        var args = call!.FindChildOfType("arguments");
        args.IsValid().Should().BeTrue();
        var firstArg = args!.Children.FirstOrDefault(c => c.Type == "identifier");
        firstArg.IsValid().Should().BeTrue();

        var resolved = AstValueResolver.ResolveString(firstArg!);
        resolved.Should().Be("orders-v2-queue");
    }

    [Test]
    public void AstValueResolver_ResolvesConfigReaderCalls()
    {
        ConstantRegistry.Register(null, "Kafka:Topics:PaymentEvents", "payments-v1");
        ConstantRegistry.Register(null, "ORDER_TOPIC", "orders-stream");

        var csharpCode = @"
class Worker {
    void Run() {
        var t1 = _config[""Kafka:Topics:PaymentEvents""];
        var t2 = process.env.ORDER_TOPIC;
    }
}
";
        var language = SyntaxTree.GetLanguage("c-sharp");

        using var parser = new TreeSitter.Parser(language);

        using var tree = parser.Parse(csharpCode);

        tree.Should().NotBeNull();

        var subscript = tree!.RootNode.FindDescendantOfType("element_access_expression");
        subscript!.IsValid().Should().BeTrue();
        AstValueResolver.ResolveString(subscript!).Should().Be("payments-v1");

        var member = tree!.RootNode.FindDescendantsOfType("member_access_expression")
            .FirstOrDefault(m => m.Text.Contains("ORDER_TOPIC"));
        member!.IsValid().Should().BeTrue();
        AstValueResolver.ResolveString(member!).Should().Be("orders-stream");
    }

    [Test]
    public void AstValueResolver_UnquotesAndUnescapesStringsCorrectly()
    {
        AstValueResolver.Unquote("\"hello\"").Should().Be("hello");
        AstValueResolver.Unquote("'world'").Should().Be("world");
        AstValueResolver.Unquote("`template`").Should().Be("template");
        AstValueResolver.Unquote("@\"escaped\"\"quote\"\"\"").Should().Be("escaped\"quote\"");
        AstValueResolver.Unquote("\"\"\"raw string\"\"\"").Should().Be("raw string");
        AstValueResolver.Unquote("\"line1\\nline2\\tcol\"").Should().Be("line1\nline2\tcol");
    }

    [Test]
    public void AstValueResolver_HandlesRegexPatternsAndRawStringsWithoutThrowing()
    {
        // Go raw string literals in backticks must preserve literal backslashes without Regex.Unescape
        AstValueResolver.Unquote("`[\\s`").Should().Be("[\\s");
        AstValueResolver.Unquote("`<html>\r\n<div>{{name}}</div>`").Should().Be("<html>\n<div>{{name}}</div>");

        // Quoted strings with regex escape sequences like \s, \d must not throw RegexParseException
        AstValueResolver.Unquote("\"[\\s\"").Should().Be("[\\s");
        AstValueResolver.Unquote("\"^(\\+\\d{1,2}\\s)?\\(?\\d{3}\\)?[\\s.-]\\d{3}[\\s.-]\\d{4}$\"").Should().Be("^(\\+\\d{1,2}\\s)?\\(?\\d{3}\\)?[\\s.-]\\d{3}[\\s.-]\\d{4}$");
    }

    [Test]
    public void GoAstHelper_HandlesCyclicSelfReferencingVariablesWithoutStackOverflow()
    {
        var goCode = @"
package main

func Fetch() {
    url := url + ""/api/v1/orders""
    http.Get(url)
}
";
        var language = SyntaxTree.GetLanguage("go");
        using var parser = new TreeSitter.Parser(language);
        using var tree = parser.Parse(goCode);
        tree.Should().NotBeNull();

        var call = tree!.RootNode.FindDescendantOfType("call_expression");
        call.IsValid().Should().BeTrue();

        var args = Parser.Go.Libraries.GoAstHelper.GetCallArguments(call);
        args.Count.Should().BeGreaterThan(0);

        // Must terminate safely without StackOverflowException
        var res = Parser.Go.Libraries.GoAstHelper.ResolveStringOrVariable(args[0]);
        res.Should().Be("/api/v1/orders");
    }

    [Test]
    public void PythonAstHelper_HandlesCyclicSelfReferencingVariablesWithoutStackOverflow()
    {
        var pyCode = @"
def fetch():
    url = url + '/api/v1/items'
    requests.get(url)
";
        var language = SyntaxTree.GetLanguage("python");
        using var parser = new TreeSitter.Parser(language);
        using var tree = parser.Parse(pyCode);
        tree.Should().NotBeNull();

        var call = tree!.RootNode.FindDescendantOfType(TreeSitterSyntax.Python.Call);
        call.IsValid().Should().BeTrue();

        var args = Parser.Python.Libraries.PythonAstHelper.GetCallArguments(call);
        args.Count.Should().BeGreaterThan(0);

        // Must terminate safely without StackOverflowException
        var res = Parser.Python.Libraries.PythonAstHelper.ResolveStringOrVariable(args[0]);
        res.Should().Be("/api/v1/items");
    }

    [Test]
    public void TypeScriptAstHelper_HandlesCyclicSelfReferencingVariablesWithoutStackOverflow()
    {
        var tsCode = @"
function fetch() {
    let url = url + '/api/v1/users';
    get(url);
}
";
        var language = SyntaxTree.GetLanguage("typescript");
        using var parser = new TreeSitter.Parser(language);
        using var tree = parser.Parse(tsCode);
        tree.Should().NotBeNull();

        var call = tree!.RootNode.FindDescendantOfType(TreeSitterSyntax.TypeScript.CallExpression);
        call.IsValid().Should().BeTrue();

        var args = Parser.TypeScript.Libraries.AstHelper.GetCallArguments(call);
        args.Count.Should().BeGreaterThan(0);

        // Must terminate safely without StackOverflowException
        var res = Parser.TypeScript.Libraries.AstHelper.ResolveStringOrTemplate(args[0]);
        res.Should().Be("/api/v1/users");
    }

    [Test]
    public void Test_HttpApiPrefixConfig_AtsExactContent()
    {
        WorkspaceIndexer.AutoRegisterParsers();
        var code = @"
export const HTTP_API_PREFIX_CONFIG = {
  API_GLOBAL_PREFIX: 'api/v1',
  SMART_CPA: {
    GLOBAL_PREFIX: 'smart-cpa',
    START_PREFIX: 'campaigns/start',
    STOP_PREFIX: 'campaigns/stop',
    UPDATE_PREFIX: 'campaigns/update',
  },
};
";
        AstConstantExtractor.ExtractAndRegister("src/config/http-api-prefix.config.ts", code, "integration-service-helper-nest");
        TestContext.WriteLine("Constants registered count: " + ConstantRegistry.ProjectConstants.Count);
        foreach (var kvp in ConstantRegistry.ProjectConstants)
        {
            TestContext.WriteLine($"  {kvp.Key} = {kvp.Value}");
        }
        ConstantRegistry.TryResolve("integration-service-helper-nest", "HTTP_API_PREFIX_CONFIG.SMART_CPA.GLOBAL_PREFIX", out var val).Should().BeTrue();
        val.Should().Be("smart-cpa");
    }
}
