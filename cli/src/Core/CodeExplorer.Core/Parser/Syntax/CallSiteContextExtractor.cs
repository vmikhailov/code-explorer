using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Parser.Syntax;

/// <summary>
/// Extracts structural execution context for AST call-sites in O(depth) ancestor pointer hops.
/// </summary>
public static class CallSiteContextExtractor
{
    private static readonly HashSet<string> FunctionalIterationMethods = new(StringComparer.Ordinal)
    {
        "forEach", "map", "flatMap", "filter", "reduce", "some", "every",
        "ForEach", "ForEachAsync", "Select", "Where", "All", "Any"
    };

    public static CallSiteExecutionContext Extract(SyntaxNode callNode)
    {
        var flags = CallExecutionFlags.None;
        byte loopDepth = 0;
        byte condDepth = 0;

        SyntaxNode? prev = callNode;
        SyntaxNode? curr = callNode.Parent;

        while (curr != null && curr is not FunctionDeclSyntax and not TypeDeclSyntax and not SyntaxDocument)
        {
            switch (curr)
            {
                case UnaryExprSyntax { IsAwait: true }:
                    flags |= CallExecutionFlags.IsAwaited;
                    break;

                case ControlFlowStmtSyntax cf:
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
                            if (ReferenceEquals(prev, cf.Alternative) || prev?.FieldRole is "finalizer" or "finally_clause")
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
                    break;

                case ConditionalExprSyntax:
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                    break;

                case BinaryExprSyntax { Operator: "&&" or "||" or "??" or "and" or "or" } bin
                    when ReferenceEquals(prev, bin.Right):
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                    break;

                case CatchClauseSyntax:
                    flags |= CallExecutionFlags.InCatchBlock | CallExecutionFlags.IsConditional;
                    break;

                case LambdaExprSyntax:
                    flags |= CallExecutionFlags.InLambda;
                    CallExprSyntax? outerCall = null;
                    if (curr.Parent is ArgumentSyntax { Parent: CallExprSyntax argParentCall })
                    {
                        outerCall = argParentCall;
                    }
                    else if (curr.Parent is CallExprSyntax callParent)
                    {
                        outerCall = callParent;
                    }

                    if (outerCall != null)
                    {
                        if (FunctionalIterationMethods.Contains(outerCall.MethodName))
                        {
                            loopDepth++;
                            flags |= CallExecutionFlags.InLoop;
                            if (loopDepth >= 2) flags |= CallExecutionFlags.InNestedLoop;
                        }
                        else if (outerCall.MethodName is "Run" or "StartNew" or "spawn" or "create_task")
                        {
                            flags |= CallExecutionFlags.IsConcurrentSpawn;
                        }
                    }
                    break;
            }

            prev = curr;
            curr = curr.Parent;
        }

        return new CallSiteExecutionContext(flags, loopDepth, condDepth);
    }
}
