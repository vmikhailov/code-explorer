using CodeExplorer.TreeSitter;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TreeSitterRuntimeTests
{
    private const string LangCSharp = "c-sharp";
    private const string LangTypeScript = "typescript";
    private const string LangTsx = "tsx";
    private const string LangJavaScript = "javascript";
    private const string LangGo = "go";
    private const string LangPython = "python";
    private const string LangJava = "java";
    private const string LangRust = "rust";
    private const string LangR = "r";
    private const string RSourceCode = """
        library(DBI)
        load_orders <- function(con) {
          df <- DBI::dbGetQuery(con, "SELECT * FROM analytics.public.orders")
          return(df)
        }
        """;

    [TestCase(LangCSharp)]
    [TestCase(LangTypeScript)]
    [TestCase(LangTsx)]
    [TestCase(LangJavaScript)]
    [TestCase(LangGo)]
    [TestCase(LangPython)]
    [TestCase(LangJava)]
    [TestCase(LangRust)]
    [TestCase(LangR)]
    public void NativeGrammars_AreAvailableAndLoadable(string languageId)
    {
        var available = Language.IsAvailable(languageId);
        available.Should().BeTrue();

        using var lang = new Language(languageId);
        lang.Name.Should().Be(languageId);
    }

    [Test]
    public void RGrammar_ParsesFunctionsCallsAndStringsIntoZeroAllocationTree()
    {
        using var lang = new Language(LangR);
        using var parser = new Parser(lang);
        using var tree = parser.Parse(RSourceCode);

        tree.Should().NotBeNull();
        var root = tree!.RootNode;
        root.IsNull.Should().BeFalse();
        root.GetNodeType().Should().Be("program");
        root.StartIndex.Should().Be(0);
        root.EndIndex.Should().Be(RSourceCode.Length);

        using var cursor = root.Walk();
        cursor.GotoFirstChild().Should().BeTrue();

        var childKinds = new List<string>();
        do
        {
            var node = cursor.CurrentNode;
            if (node.IsNamed)
            {
                childKinds.Add(node.GetNodeType());
            }
        } while (cursor.GotoNextSibling());

        childKinds.Should().Contain("call");
        childKinds.Should().Contain("binary_operator");
    }

    [Test]
    public void TreeCursorWalk_AllocatesZeroManagedBytesAfterInternWarmup()
    {
        using var lang = new Language(LangCSharp);
        using var parser = new Parser(lang);
        const string csharpSample = """
            namespace Demo.OrderProcessing;
            public sealed class OrderService
            {
                private readonly IOrderRepository _repo;
                public OrderService(IOrderRepository repo) => _repo = repo;
                public async Task<OrderDto> ProcessAsync(CreateOrderCommand cmd, CancellationToken ct)
                {
                    var entity = new Order(cmd.CustomerId, cmd.TotalAmount);
                    await _repo.SaveAsync(entity, ct);
                    return new OrderDto(entity.Id, entity.TotalAmount);
                }
            }
            """;

        using var tree = parser.Parse(csharpSample)!;
        var root = tree.RootNode;

        // Warmup pointer-interning cache
        WalkAllNodes(root);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        var totalNodes = 0;
        const int iterations = 1000;
        for (var i = 0; i < iterations; i++)
        {
            totalNodes += WalkAllNodes(root);
        }
        var afterBytes = GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();

        var allocatedBytes = afterBytes - beforeBytes;
        TestContext.Out.WriteLine($"Walked {totalNodes:N0} AST nodes ({iterations} iterations) in {sw.Elapsed.TotalMilliseconds:F2} ms ({totalNodes / sw.Elapsed.TotalSeconds:N0} nodes/sec), GC allocated: {allocatedBytes} bytes");
        allocatedBytes.Should().Be(0);
    }

    [Test]
    public void TypeScriptGrammar_ImportTypeInArraysUnionsAndGenerics_ParsesWithoutErrors()
    {
        const string tsSample = """
            export interface WorkerConfig {
              clientInfo?: import('../utils/clientInfo').ClientInfo;
              timeouts?: import('../core/fsm/types').TimeoutSpec[];
              result?: import('../core/fsm/types').Result<string> | null;
            }
            """;

        using var lang = new Language(LangTypeScript);
        using var parser = new Parser(lang);
        using var tree = parser.Parse(tsSample)!;

        tree.RootNode.HasError.Should().BeFalse();
    }

    [Test]
    public void CSharpGrammar_InOutTypedLambdaParametersAndPointerCastDereference_ParsesWithoutErrors()
    {
        const string csSample = """
            namespace Mediator.SourceGenerator;
            public sealed class CompilationAnalyzer
            {
                public unsafe void Analyze(SomeStructCommand command, string unitSymbolName)
                {
                    ReportDiagnostic(
                        unitSymbolName,
                        ((in CompilationAnalyzerContext c, string name) => c.ReportRequiredSymbolNotFound(name))
                    );
                    ReportDiagnostic(
                        unitSymbolName,
                        (in CompilationAnalyzerContext c, INamedTypeSymbol s) => c.ReportMessageWithoutHandler(s)
                    );
                    var addr = *(long*)&command;
                    Addresses.Add(addr);
                }
            }
            """;

        using var lang = new Language(LangCSharp);
        using var parser = new Parser(lang);
        using var tree = parser.Parse(csSample)!;

        tree.RootNode.HasError.Should().BeFalse();
    }

    private static int WalkAllNodes(TSNode root)
    {
        var count = 0;
        using var cursor = root.Walk();
        var reachedRoot = false;
        while (!reachedRoot)
        {
            var current = cursor.CurrentNode;
            _ = current.GetNodeType();
            _ = cursor.CurrentFieldName;
            count++;

            if (cursor.GotoFirstChild())
                continue;

            if (cursor.GotoNextSibling())
                continue;

            var retracing = true;
            while (retracing)
            {
                if (!cursor.GotoParent())
                {
                    retracing = false;
                    reachedRoot = true;
                }
                else if (cursor.GotoNextSibling())
                {
                    retracing = false;
                }
            }
        }
        return count;
    }
}
