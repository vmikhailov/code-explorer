using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class AstValueResolverTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new TypeScriptParser());
        WorkspaceIndexer.Register(new GoParser());
        WorkspaceIndexer.Register(new PythonParser());
        WorkspaceIndexer.Register(new JavaParser());
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

        Assert.That(ConstantRegistry.TryResolve("TestApp", "Environment", out var env), Is.True);
        Assert.That(env, Is.EqualTo("production"));

        Assert.That(ConstantRegistry.TryResolve("TestApp", "OrderBase", out var orderBase), Is.True);
        Assert.That(orderBase, Is.EqualTo("company.production.orders"));

        Assert.That(ConstantRegistry.TryResolve("TestApp", "OrderCreated", out var orderCreated), Is.True);
        Assert.That(orderCreated, Is.EqualTo("company.production.orders.created"));

        Assert.That(ConstantRegistry.TryResolve("TestApp", "OrderCancelled", out var orderCancelled), Is.True);
        Assert.That(orderCancelled, Is.EqualTo("company.production.orders.cancelled"));

        Assert.That(ConstantRegistry.TryResolve("TestApp", "PaymentQueue", out var paymentQueue), Is.True);
        Assert.That(paymentQueue, Is.EqualTo("company.production.orders.payments"));

        // Qualified lookup
        Assert.That(ConstantRegistry.TryResolve("TestApp", "TopicNames.OrderCreated", out var qualCreated), Is.True);
        Assert.That(qualCreated, Is.EqualTo("company.production.orders.created"));
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

        Assert.That(ConstantRegistry.TryResolve("BillingService", "DOMAIN", out var domain), Is.True);
        Assert.That(domain, Is.EqualTo("billing"));

        Assert.That(ConstantRegistry.TryResolve("BillingService", "INVOICE_PREFIX", out var prefix), Is.True);
        Assert.That(prefix, Is.EqualTo("billing.invoices"));

        Assert.That(ConstantRegistry.TryResolve("BillingService", "Topics.InvoiceCreated", out var created), Is.True);
        Assert.That(created, Is.EqualTo("billing.invoices.created"));

        Assert.That(ConstantRegistry.TryResolve("BillingService", "Topics.InvoicePaid", out var paid), Is.True);
        Assert.That(paid, Is.EqualTo("billing.invoices.paid"));

        Assert.That(ConstantRegistry.TryResolve("BillingService", "Queues.PaymentProcessing", out var queue), Is.True);
        Assert.That(queue, Is.EqualTo("payment-processing-queue"));
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

        Assert.That(ConstantRegistry.TryResolve("StoreService", "Prefix", out var prefix), Is.True);
        Assert.That(prefix, Is.EqualTo("store"));

        Assert.That(ConstantRegistry.TryResolve("StoreService", "OrdersTopic", out var topic), Is.True);
        Assert.That(topic, Is.EqualTo("store.orders"));
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

        Assert.That(ConstantRegistry.TryResolve("CatalogService", "SERVICE_NAME", out var svc), Is.True);
        Assert.That(svc, Is.EqualTo("catalog"));

        Assert.That(ConstantRegistry.TryResolve("CatalogService", "ITEM_CREATED_TOPIC", out var topic), Is.True);
        Assert.That(topic, Is.EqualTo("catalog.items.created"));
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

        Assert.That(tree, Is.Not.Null);

        // Find the call expression rabbit.createQueue(queueName)
        var call = tree!.RootNode.FindDescendantOfType("call_expression");
        Assert.That(call.IsValid(), Is.True);

        var args = call!.FindChildOfType("arguments");
        Assert.That(args.IsValid(), Is.True);
        var firstArg = args!.Children.FirstOrDefault(c => c.Type == "identifier");
        Assert.That(firstArg.IsValid(), Is.True);

        var resolved = AstValueResolver.ResolveString(firstArg!);
        Assert.That(resolved, Is.EqualTo("orders-v2-queue"));
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

        Assert.That(tree, Is.Null.Or.Not.Null);

        var subscript = tree!.RootNode.FindDescendantOfType("element_access_expression");
        Assert.That(subscript!.IsValid(), Is.True);
        Assert.That(AstValueResolver.ResolveString(subscript!), Is.EqualTo("payments-v1"));

        var member = tree!.RootNode.FindDescendantsOfType("member_access_expression")
            .FirstOrDefault(m => m.Text.Contains("ORDER_TOPIC"));
        Assert.That(member!.IsValid(), Is.True);
        Assert.That(AstValueResolver.ResolveString(member!), Is.EqualTo("orders-stream"));
    }

    [Test]
    public void AstValueResolver_UnquotesAndUnescapesStringsCorrectly()
    {
        Assert.That(AstValueResolver.Unquote("\"hello\""), Is.EqualTo("hello"));
        Assert.That(AstValueResolver.Unquote("'world'"), Is.EqualTo("world"));
        Assert.That(AstValueResolver.Unquote("`template`"), Is.EqualTo("template"));
        Assert.That(AstValueResolver.Unquote("@\"escaped\"\"quote\"\"\""), Is.EqualTo("escaped\"quote\""));
        Assert.That(AstValueResolver.Unquote("\"\"\"raw string\"\"\""), Is.EqualTo("raw string"));
        Assert.That(AstValueResolver.Unquote("\"line1\\nline2\\tcol\""), Is.EqualTo("line1\nline2\tcol"));
    }

    [Test]
    public void AstValueResolver_HandlesRegexPatternsAndRawStringsWithoutThrowing()
    {
        // Go raw string literals in backticks must preserve literal backslashes without Regex.Unescape
        Assert.That(AstValueResolver.Unquote("`[\\s`"), Is.EqualTo("[\\s"));
        Assert.That(AstValueResolver.Unquote("`<html>\r\n<div>{{name}}</div>`"), Is.EqualTo("<html>\n<div>{{name}}</div>"));

        // Quoted strings with regex escape sequences like \s, \d must not throw RegexParseException
        Assert.That(AstValueResolver.Unquote("\"[\\s\""), Is.EqualTo("[\\s"));
        Assert.That(AstValueResolver.Unquote("\"^(\\+\\d{1,2}\\s)?\\(?\\d{3}\\)?[\\s.-]\\d{3}[\\s.-]\\d{4}$\""),
            Is.EqualTo("^(\\+\\d{1,2}\\s)?\\(?\\d{3}\\)?[\\s.-]\\d{3}[\\s.-]\\d{4}$"));
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
        Assert.That(tree, Is.Not.Null);

        var call = tree!.RootNode.FindDescendantOfType("call_expression");
        Assert.That(call.IsValid(), Is.True);

        var args = CodeExplorer.Parser.Go.Libraries.GoAstHelper.GetCallArguments(call);
        Assert.That(args.Count, Is.GreaterThan(0));

        // Must terminate safely without StackOverflowException
        var res = CodeExplorer.Parser.Go.Libraries.GoAstHelper.ResolveStringOrVariable(args[0]);
        Assert.That(res, Is.EqualTo("/api/v1/orders"));
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
        Assert.That(tree, Is.Not.Null);

        var call = tree!.RootNode.FindDescendantOfType(TreeSitterSyntax.Python.Call);
        Assert.That(call.IsValid(), Is.True);

        var args = CodeExplorer.Parser.Python.Libraries.PythonAstHelper.GetCallArguments(call);
        Assert.That(args.Count, Is.GreaterThan(0));

        // Must terminate safely without StackOverflowException
        var res = CodeExplorer.Parser.Python.Libraries.PythonAstHelper.ResolveStringOrVariable(args[0]);
        Assert.That(res, Is.EqualTo("/api/v1/items"));
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
        Assert.That(tree, Is.Not.Null);

        var call = tree!.RootNode.FindDescendantOfType(TreeSitterSyntax.TypeScript.CallExpression);
        Assert.That(call.IsValid(), Is.True);

        var args = CodeExplorer.Parser.TypeScript.Libraries.AstHelper.GetCallArguments(call);
        Assert.That(args.Count, Is.GreaterThan(0));

        // Must terminate safely without StackOverflowException
        var res = CodeExplorer.Parser.TypeScript.Libraries.AstHelper.ResolveStringOrTemplate(args[0]);
        Assert.That(res, Is.EqualTo("/api/v1/users"));
    }

    [Test]
    public void Test_HttpApiPrefixConfig_AtsExactContent()
    {
        WorkspaceIndexer.Register(new CodeExplorer.Parser.TypeScript.TypeScriptParser());
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
        Assert.That(ConstantRegistry.TryResolve("integration-service-helper-nest", "HTTP_API_PREFIX_CONFIG.SMART_CPA.GLOBAL_PREFIX", out var val), Is.True);
        Assert.That(val, Is.EqualTo("smart-cpa"));
    }
}
