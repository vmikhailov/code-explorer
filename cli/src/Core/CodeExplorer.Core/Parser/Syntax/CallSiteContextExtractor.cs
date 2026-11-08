using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Extracts structural execution context for AST call-sites in O(depth) ancestor pointer hops.
/// </summary>
public static class CallSiteContextExtractor
{
    private const string RoleFinalizer = "finalizer";
    private const string RoleFinallyClause = "finally_clause";
    private const string OpLogicalAnd = "&&";
    private const string OpLogicalOr = "||";
    private const string OpNullCoalescing = "??";
    private const string OpWordAnd = "and";
    private const string OpWordOr = "or";

    private static readonly HashSet<string> FunctionalIterationMethods = new(StringComparer.Ordinal)
    {
        "forEach", "map", "flatMap", "filter", "reduce", "some", "every",
        "ForEach", "ForEachAsync", "Select", "Where", "All", "Any"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> FunctionalIterationLookup =
        FunctionalIterationMethods.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> ParallelBatchMethods = new(StringComparer.Ordinal)
    {
        "WhenAll", "WaitAll", "all", "allSettled", "allOf", "gather", "join_all", "try_join_all"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> ParallelBatchLookup =
        ParallelBatchMethods.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> ConcurrentSpawnMethods = new(StringComparer.Ordinal)
    {
        "Run", "StartNew", "spawn", "create_task", "Go"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> ConcurrentSpawnLookup =
        ConcurrentSpawnMethods.GetAlternateLookup<ReadOnlySpan<char>>();

    public static CallSiteExecutionContext Extract(SyntaxNode callNode)
    {
        var flags = CallExecutionFlags.None;
        byte loopDepth = 0;
        byte condDepth = 0;
        string? assignedVarName = null;
        bool canCaptureAssignment = true;
        SyntaxNode? assignmentScope = null;

        SyntaxNode? prev = callNode;
        SyntaxNode? curr = callNode.Parent;

        while (curr != null && curr is not FunctionDeclSyntax and not TypeDeclSyntax and not SyntaxDocument)
        {
            switch (curr)
            {
                case UnaryExprSyntax { IsAwait: true }:
                    flags |= CallExecutionFlags.IsAwaited;
                    break;

                case BlockStmtSyntax:
                    canCaptureAssignment = false;
                    break;

                case VariableDeclSyntax varDecl when canCaptureAssignment && !varDecl.NameSpan.IsEmpty:
                    assignedVarName ??= varDecl.Name;
                    canCaptureAssignment = false;
                    break;

                case AssignmentExprSyntax assignExpr when canCaptureAssignment && ReferenceEquals(prev, assignExpr.Right) && assignExpr.Left is IdentifierExprSyntax { NameSpan.IsEmpty: false } leftId:
                    assignedVarName ??= leftId.Name;
                    canCaptureAssignment = false;
                    break;

                case CallExprSyntax outerCallExpr when !ReferenceEquals(prev, outerCallExpr.Receiver):
                    var outerMethodSpan = outerCallExpr.MethodNameSpan;
                    if (ParallelBatchLookup.Contains(outerMethodSpan))
                        flags |= CallExecutionFlags.IsParallelBatch;
                    else if (ConcurrentSpawnLookup.Contains(outerMethodSpan))
                        flags |= CallExecutionFlags.IsConcurrentSpawn;
                    break;

                case ControlFlowStmtSyntax cf:
                    if (cf.Kind != ControlFlowKind.Expression)
                        canCaptureAssignment = false;
                    ApplyControlFlowContext(cf, prev, ref flags, ref loopDepth, ref condDepth);
                    break;

                case ConditionalExprSyntax:
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                    break;

                case BinaryExprSyntax { Operator: OpLogicalAnd or OpLogicalOr or OpNullCoalescing or OpWordAnd or OpWordOr } bin
                    when ReferenceEquals(prev, bin.Right):
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                    break;

                case CatchClauseSyntax:
                    canCaptureAssignment = false;
                    flags |= CallExecutionFlags.InCatchBlock | CallExecutionFlags.IsConditional;
                    break;

                case LambdaExprSyntax:
                    canCaptureAssignment = false;
                    if (assignedVarName != null && assignmentScope == null)
                        assignmentScope = curr;
                    ApplyLambdaContext(curr, ref flags, ref loopDepth);
                    break;
            }

            prev = curr;
            curr = curr.Parent;
        }

        var effectiveScope = assignmentScope ?? curr;
        if (!string.IsNullOrEmpty(assignedVarName) && (flags & CallExecutionFlags.IsParallelBatch) == 0 && effectiveScope != null)
        {
            if (IsVariableAggregatedInParallelBatch(callNode.Document, effectiveScope, assignedVarName))
            {
                flags |= CallExecutionFlags.IsParallelBatch | CallExecutionFlags.IsAwaited;
            }
        }

        return new CallSiteExecutionContext(flags, loopDepth, condDepth);
    }

    public static CallSiteExecutionContext ExtractForLine(SyntaxDocument? doc, int line)
    {
        if (doc == null || line <= 0) return default;
        var targetZeroLine = line - 1;
        for (int i = 0; i < doc.AllCalls.Count; i++)
        {
            var c = doc.AllCalls[i];
            if (c.Span.StartLine == targetZeroLine)
            {
                return Extract(c);
            }
        }
        for (int i = 0; i < doc.AllCalls.Count; i++)
        {
            var c = doc.AllCalls[i];
            if (c.Span.StartLine <= targetZeroLine && c.Span.EndLine >= targetZeroLine)
            {
                return Extract(c);
            }
        }
        return default;
    }

    private static void ApplyControlFlowContext(
        ControlFlowStmtSyntax cf,
        SyntaxNode? prev,
        ref CallExecutionFlags flags,
        ref byte loopDepth,
        ref byte condDepth)
    {
        switch (cf.Kind)
        {
            case ControlFlowKind.Loop:
                if (prev?.FieldRole is not (SyntaxFields.Right or SyntaxFields.Value or SyntaxFields.Initializer))
                {
                    loopDepth++;
                    flags |= CallExecutionFlags.InLoop;
                    if (loopDepth >= 2) flags |= CallExecutionFlags.InNestedLoop;
                }
                break;

            case ControlFlowKind.If:
                if (!ReferenceEquals(prev, cf.Condition) && prev?.FieldRole != SyntaxFields.Condition)
                {
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                }
                break;

            case ControlFlowKind.Switch:
                condDepth++;
                flags |= CallExecutionFlags.IsConditional;
                break;

            case ControlFlowKind.Try:
                if (ReferenceEquals(prev, cf.Alternative) || prev?.FieldRole is RoleFinalizer or RoleFinallyClause)
                    flags |= CallExecutionFlags.InFinallyBlock;
                else if (prev is not CatchClauseSyntax)
                    flags |= CallExecutionFlags.InTryBlock;
                break;

            case ControlFlowKind.Lock:
                flags |= CallExecutionFlags.InLockBlock;
                break;

            case ControlFlowKind.Using:
                flags |= CallExecutionFlags.InUsingResource;
                break;

            case ControlFlowKind.Defer:
                flags |= CallExecutionFlags.InFinallyBlock;
                break;

            case ControlFlowKind.Goroutine:
                flags |= CallExecutionFlags.IsConcurrentSpawn;
                break;
        }
    }

    private static void ApplyLambdaContext(SyntaxNode lambdaNode, ref CallExecutionFlags flags, ref byte loopDepth)
    {
        flags |= CallExecutionFlags.InLambda;
        var outerCall = lambdaNode.Parent switch
        {
            ArgumentSyntax { Parent: CallExprSyntax argParentCall } => argParentCall,
            CallExprSyntax callParent => callParent,
            _ => null
        };

        if (outerCall == null) return;
        var outerMethodSpan = outerCall.MethodNameSpan;
        if (FunctionalIterationLookup.Contains(outerMethodSpan))
        {
            loopDepth++;
            flags |= CallExecutionFlags.InLoop;
            if (loopDepth >= 2) flags |= CallExecutionFlags.InNestedLoop;
        }
        else if (ConcurrentSpawnLookup.Contains(outerMethodSpan))
        {
            flags |= CallExecutionFlags.IsConcurrentSpawn;
        }
    }

    private static bool IsVariableAggregatedInParallelBatch(SyntaxDocument? doc, SyntaxNode enclosingScope, string varName)
    {
        if (doc == null || doc.AllCalls.Count == 0) return false;
        if (doc.HasParallelBatchCallsCache == 0) return false;
        if (doc.HasParallelBatchCallsCache < 0)
        {
            sbyte hasBatch = 0;
            for (int i = 0; i < doc.AllCalls.Count; i++)
            {
                if (ParallelBatchLookup.Contains(doc.AllCalls[i].MethodNameSpan))
                {
                    hasBatch = 1;
                    break;
                }
            }
            doc.HasParallelBatchCallsCache = hasBatch;
            if (hasBatch == 0) return false;
        }

        var startByte = enclosingScope.Span.StartByte;
        var endByte = enclosingScope.Span.EndByte;

        for (int i = 0; i < doc.AllCalls.Count; i++)
        {
            var call = doc.AllCalls[i];
            if (call.Span.StartByte < startByte || call.Span.EndByte > endByte) continue;
            if (!ParallelBatchLookup.Contains(call.MethodNameSpan)) continue;

            for (int a = 0; a < call.Arguments.Count; a++)
            {
                if (call.Arguments[a].SpanContains(varName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
