using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Analysis.Dataflow;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class UastCompletionAndFlowAnalysisTests
{
    private readonly TypeScriptSyntaxConverter _tsParser = new();
    private readonly CSharpSyntaxConverter _csParser = new();
    private readonly PythonSyntaxConverter _pyParser = new();
    private readonly GoSyntaxConverter _goParser = new();
    private readonly JavaSyntaxConverter _javaParser = new();

    [Test]
    public void Uast_FunctionDecl_PopulatesParametersAndBody()
    {
        var code = """
                   function calculate(a: number, b: string): void {
                       const x = a + 1;
                       return;
                   }
                   """;

        var doc = _tsParser.Parse(code, "typescript", "calc.ts", "calc.ts");
        var fn = doc.AllFunctions.FirstOrDefault(f => f.Name == "calculate");

        Assert.That(fn, Is.Not.Null);
        Assert.That(fn!.Parameters.Count, Is.EqualTo(2));
        Assert.That(fn.Parameters[0].Name, Is.EqualTo("a"));
        Assert.That(fn.Parameters[1].Name, Is.EqualTo("b"));
        Assert.That(fn.Body, Is.Not.Null);
        Assert.That(fn.Body!.Children.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Uast_CallExpr_PopulatesArguments()
    {
        var code = """
                   function run() {
                       doWork(123, "hello");
                   }
                   """;

        var doc = _tsParser.Parse(code, "typescript", "run.ts", "run.ts");
        var call = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "doWork");

        Assert.That(call, Is.Not.Null);
        Assert.That(call!.Arguments.Count, Is.EqualTo(2));
        Assert.That(call.Arguments[0].Value, Is.Not.Null);
        Assert.That(call.Arguments[1].Value, Is.Not.Null);
    }

    [Test]
    public void Uast_IfAndLoop_PopulatesConditionsAndBodies()
    {
        var code = """
                   function check(arr: number[]) {
                       if (arr.length > 0) {
                           for (let i = 0; i < arr.length; i++) {
                               console.log(arr[i]);
                           }
                       }
                   }
                   """;

        var doc = _tsParser.Parse(code, "typescript", "check.ts", "check.ts");
        var ifStmt = doc.DescendantsAndSelf().OfType<ControlFlowStmtSyntax>().FirstOrDefault(s => s.Kind == ControlFlowKind.If);
        var loop = doc.DescendantsAndSelf().OfType<ControlFlowStmtSyntax>().FirstOrDefault(s => s.Kind == ControlFlowKind.Loop);

        Assert.That(ifStmt, Is.Not.Null);
        Assert.That(ifStmt!.Condition, Is.Not.Null);
        Assert.That(ifStmt.Body, Is.Not.Null);

        Assert.That(loop, Is.Not.Null);
        Assert.That(loop!.Condition, Is.Not.Null);
        Assert.That(loop.Body, Is.Not.Null);
    }

    [Test]
    public void CallSiteContextExtractor_DetectsLoopAndConditional()
    {
        var code = """
                   function processItems(items: string[]) {
                       for (let i = 0; i < items.length; i++) {
                           if (items[i] !== "") {
                               try {
                                   fetch(items[i]);
                               } catch (e) {
                                   log(e);
                               }
                           }
                       }
                   }
                   """;

        var doc = _tsParser.Parse(code, "typescript", "test.ts", "test.ts");
        var fetchCall = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "fetch");
        Assert.That(fetchCall, Is.Not.Null);

        var fetchCtx = CallSiteContextExtractor.Extract(fetchCall!);
        Assert.That(fetchCtx.Flags.HasFlag(CallExecutionFlags.InLoop), Is.True);
        Assert.That(fetchCtx.Flags.HasFlag(CallExecutionFlags.IsConditional), Is.True);
        Assert.That(fetchCtx.Flags.HasFlag(CallExecutionFlags.InTryBlock), Is.True);
        Assert.That(fetchCtx.LoopDepth, Is.EqualTo((byte)1));

        var logCall = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "log");
        Assert.That(logCall, Is.Not.Null);

        var logCtx = CallSiteContextExtractor.Extract(logCall!);
        Assert.That(logCtx.Flags.HasFlag(CallExecutionFlags.InCatchBlock), Is.True);
    }

    [Test]
    public void FunctionDataflowAnalyzer_TracesArgFlowAndReturn()
    {
        var code = """
                   function forward(userId: string, tenantId: string) {
                       const id = userId;
                       doLookup(id, tenantId);
                       return id;
                   }
                   """;

        var doc = _tsParser.Parse(code, "typescript", "forward.ts", "forward.ts");
        var fn = doc.AllFunctions.FirstOrDefault(f => f.Name == "forward");

        Assert.That(fn, Is.Not.Null);
        Assert.That(fn!.DataflowSummary, Is.Not.Null);

        var summary = fn.DataflowSummary!;

        // 1. Return origins
        Assert.That(summary.ReturnOrigins.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(summary.ReturnOrigins.Any(o => o.Kind == FlowSourceKind.Parameter && o.Identifier == "p0"), Is.True);

        // 2. CallArgFlows for doLookup
        var doLookupFlow = summary.CallArgFlows.FirstOrDefault(kv => kv.Key.Callee == "doLookup");
        Assert.That(doLookupFlow.Value, Is.Not.Null);
        Assert.That(doLookupFlow.Value.ContainsKey(0), Is.True);
        Assert.That(doLookupFlow.Value[0].Any(o => o.Kind == FlowSourceKind.Parameter && o.Identifier == "p0"), Is.True);
        Assert.That(doLookupFlow.Value.ContainsKey(1), Is.True);
        Assert.That(doLookupFlow.Value[1].Any(o => o.Kind == FlowSourceKind.Parameter && o.Identifier == "p1"), Is.True);
    }

    [Test]
    public void Uast_CSharp_PopulatesDeclarationsStatementsAndFlow()
    {
        var code = """
                   public class OrderService
                   {
                       private string _repo;

                       public string ProcessOrder(string orderId, string customerId)
                       {
                           this._repo = orderId;
                           lock (this)
                           {
                               while (orderId.Length > 0)
                               {
                                   Dispatch(orderId);
                                   break;
                               }
                           }
                           return orderId;
                       }
                   }
                   """;

        var doc = _csParser.Parse(code, "c-sharp", "OrderService.cs", "OrderService.cs");
        var fn = doc.AllFunctions.FirstOrDefault(f => f.Name == "ProcessOrder");
        Assert.That(fn, Is.Not.Null);
        Assert.That(fn!.Parameters.Count, Is.EqualTo(2));
        Assert.That(fn.Body, Is.Not.Null);

        // Check context on Dispatch call inside lock and while loop
        var dispatchCall = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "Dispatch");
        Assert.That(dispatchCall, Is.Not.Null);
        var ctx = CallSiteContextExtractor.Extract(dispatchCall!);
        Assert.That(ctx.Flags.HasFlag(CallExecutionFlags.InLockBlock), Is.True);
        Assert.That(ctx.Flags.HasFlag(CallExecutionFlags.InLoop), Is.True);

        // Check dataflow summary
        Assert.That(fn.DataflowSummary, Is.Not.Null);
        var summary = fn.DataflowSummary!;
        Assert.That(summary.FieldWrites.ContainsKey("_repo"), Is.True);
        Assert.That(summary.FieldWrites["_repo"].Any(o => o.Identifier == "p0"), Is.True);
        Assert.That(summary.ReturnOrigins.Any(o => o.Identifier == "p0"), Is.True);

        var dispatchFlow = summary.CallArgFlows.FirstOrDefault(kv => kv.Key.Callee == "Dispatch");
        Assert.That(dispatchFlow.Value.ContainsKey(0), Is.True);
        Assert.That(dispatchFlow.Value[0].Any(o => o.Identifier == "p0"), Is.True);
    }

    [Test]
    public void Uast_Python_DataflowSummary_SelfWritesAndReturn()
    {
        var code = """
                   class UserService:
                       def save(self, user_id):
                           self._cached = user_id
                           notify(user_id)
                           return user_id
                   """;

        var doc = _pyParser.Parse(code, "python", "user_service.py", "user_service.py");
        var fn = doc.AllFunctions.FirstOrDefault(f => f.Name == "save");
        Assert.That(fn, Is.Not.Null);
        Assert.That(fn!.DataflowSummary, Is.Not.Null);

        var summary = fn.DataflowSummary!;
        Assert.That(summary.FieldWrites.ContainsKey("_cached"), Is.True);
        Assert.That(summary.ReturnOrigins.Any(o => o.Kind == FlowSourceKind.Parameter), Is.True);
        var notifyFlow = summary.CallArgFlows.FirstOrDefault(kv => kv.Key.Callee == "notify");
        Assert.That(notifyFlow.Value, Is.Not.Null);
    }

    [Test]
    public void Uast_Go_CallExecutionFlags_InLoop()
    {
        var code = """
                   package main
                   func Process(items []string) {
                       for i := 0; i < len(items); i++ {
                           send(items[i])
                       }
                   }
                   """;

        var doc = _goParser.Parse(code, "go", "main.go", "main.go");
        var call = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "send");
        Assert.That(call, Is.Not.Null);

        var ctx = CallSiteContextExtractor.Extract(call!);
        Assert.That(ctx.Flags.HasFlag(CallExecutionFlags.InLoop), Is.True);
    }

    [Test]
    public void Uast_Java_CallExecutionFlags_InTryAndCatch()
    {
        var code = """
                   class Handler {
                       void execute(String msg) {
                           try {
                               handle(msg);
                           } catch (Exception e) {
                               log(e);
                           }
                       }
                   }
                   """;

        var doc = _javaParser.Parse(code, "java", "Handler.java", "Handler.java");
        var handleCall = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "handle");
        var logCall = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "log");

        Assert.That(handleCall, Is.Not.Null);
        Assert.That(logCall, Is.Not.Null);

        var handleCtx = CallSiteContextExtractor.Extract(handleCall!);
        var logCtx = CallSiteContextExtractor.Extract(logCall!);

        Assert.That(handleCtx.Flags.HasFlag(CallExecutionFlags.InTryBlock), Is.True);
        Assert.That(logCtx.Flags.HasFlag(CallExecutionFlags.InCatchBlock), Is.True);
    }

    [Test]
    public void FunctionDataflowSummary_ToCompactString_FormatsExpectedFormat()
    {
        var summary = new FunctionDataflowSummary();
        summary.ReturnOrigins.Add(new DataflowOrigin(FlowSourceKind.Parameter, "p0", ".Id"));
        summary.FieldWrites["_state"] = [new DataflowOrigin(FlowSourceKind.Parameter, "p1")];
        summary.CallArgFlows[("doWork", 10)] = new Dictionary<int, List<DataflowOrigin>>
        {
            [0] = [new DataflowOrigin(FlowSourceKind.Parameter, "p0")]
        };

        var compact = summary.ToCompactString();
        Assert.That(compact, Does.Contain("ret:p0.Id"));
        Assert.That(compact, Does.Contain("writes:_state<-p1"));
        Assert.That(compact, Does.Contain("calls:doWork:10(0<-p0)"));
    }

    [Test]
    public void EvidenceProvenanceHelper_BuildCallSiteExtensions_SerializesContextAndArgFlow()
    {
        var ctx = new CallSiteExecutionContext(
            CallExecutionFlags.InLoop | CallExecutionFlags.IsConditional | CallExecutionFlags.InTryBlock | CallExecutionFlags.InLockBlock | CallExecutionFlags.IsAwaited,
            LoopDepth: 2,
            ConditionalDepth: 1);

        var ext = EvidenceProvenanceHelper.BuildCallSiteExtensions(42, 10, ctx, "0<-p0;1<-const");

        Assert.That(ext, Is.Not.Null);
        Assert.That(ext!["line"], Is.EqualTo("42"));
        Assert.That(ext["column"], Is.EqualTo("10"));
        Assert.That(ext["in_loop"], Is.EqualTo("true"));
        Assert.That(ext["loop_depth"], Is.EqualTo("2"));
        Assert.That(ext["is_conditional"], Is.EqualTo("true"));
        Assert.That(ext["in_try"], Is.EqualTo("true"));
        Assert.That(ext["in_lock"], Is.EqualTo("true"));
        Assert.That(ext["is_awaited"], Is.EqualTo("true"));
        Assert.That(ext["arg_flow"], Is.EqualTo("0<-p0;1<-const"));
    }

    [Test]
    public void Layer3_EnrichesFunctionNodeAndReferencesWithDataflowAndContext()
    {
        var code = """
                   public class PaymentHandler
                   {
                       private string _status;

                       public string Handle(string orderId, string amount)
                       {
                           this._status = orderId;
                           while (amount.Length > 0)
                           {
                               Dispatch(orderId);
                               break;
                           }
                           return orderId;
                       }
                   }
                   """;

        var doc = _csParser.Parse(code, "c-sharp", "PaymentHandler.cs", "PaymentHandler.cs");
        Assert.That(doc, Is.Not.Null);

        var fileNode = new FileNode("ws:file:PaymentHandler.cs", "PaymentHandler.cs", "PaymentHandler.cs", "/workspace/PaymentHandler.cs");
        var syntaxTree = new SyntaxTree("PaymentHandler.cs", "PaymentHandler.cs", doc, fileNode, null!, [], [], []);

        var dispatchCall = doc.DescendantsAndSelf().OfType<CallExprSyntax>().FirstOrDefault(c => c.MethodName == "Dispatch");
        Assert.That(dispatchCall, Is.Not.Null);
        var dispatchLine = dispatchCall!.Span.StartLine + 1;

        var callReference = new Reference(fileNode.Id, "Dispatch", OntologyConstants.Relationships.Calls, dispatchLine, 13);
        fileNode.References.Add(callReference);

        var enrichMethod = typeof(Layer3SyntacticParser).GetMethod(
            "EnrichSyntacticParametersPrototypesAndExports",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        enrichMethod.Invoke(null, [syntaxTree, "ws", "PaymentHandler.cs", false, null, null]);

        // 1. Verify FunctionNode transient signals have df_summary, df_return, and df_writes
        var fnNode = fileNode.Children.OfType<FunctionNode>().FirstOrDefault(f => f.Name == "Handle");
        Assert.That(fnNode, Is.Not.Null);
        var fnSignals = fnNode!.Signals();
        Assert.That(fnSignals.DataflowSummary, Is.Not.Null);
        Assert.That(fnSignals.DataflowSummary, Is.Not.Empty);
        Assert.That(fnSignals.DataflowSummary, Does.Contain("ret:p0"));
        Assert.That(fnSignals.DataflowReturn, Is.Not.Null);
        Assert.That(fnSignals.DataflowReturn, Is.EqualTo("p0"));
        Assert.That(fnSignals.DataflowWrites, Is.Not.Null);
        Assert.That(fnSignals.DataflowWrites, Does.Contain("_status<-p0"));

        // 2. Verify reference was enriched with context and arg_flow
        var enrichedRef = fileNode.References.FirstOrDefault(r => r.TargetName == "Dispatch");
        Assert.That(enrichedRef, Is.Not.Null);
        Assert.That(enrichedRef!.Context.Flags.HasFlag(CallExecutionFlags.InLoop), Is.True);
        Assert.That(enrichedRef.ArgFlow, Is.Not.Null);
        Assert.That(enrichedRef.ArgFlow, Does.Contain("0<-p0"));
    }
}
