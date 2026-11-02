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
                case AwaitOrTryExprSyntax { IsTryPropagate: false }:
                    flags |= CallExecutionFlags.IsAwaited;
                    break;

                case LoopStmtSyntax loop:
                    if (prev != loop.Init && prev != loop.Collection)
                    {
                        loopDepth++;
                        flags |= CallExecutionFlags.InLoop;
                        if (loopDepth >= 2) flags |= CallExecutionFlags.InNestedLoop;
                    }
                    break;

                case IfStmtSyntax ifStmt:
                    if (prev != ifStmt.Init && prev != ifStmt.Condition)
                    {
                        condDepth++;
                        flags |= CallExecutionFlags.IsConditional;
                    }
                    break;

                case SwitchOrMatchStmtSyntax or MatchExprSyntax or ConditionalExprSyntax:
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                    break;

                case BinaryExprSyntax { Operator: "&&" or "||" or "??" or "and" or "or" } bin
                    when ReferenceEquals(prev, bin.Right):
                    condDepth++;
                    flags |= CallExecutionFlags.IsConditional;
                    break;

                case TryStmtSyntax tryStmt:
                    if (ReferenceEquals(prev, tryStmt.FinallyBody) || prev?.FieldRole is "finalizer" or "finally_clause")
                        flags |= CallExecutionFlags.InFinallyBlock;
                    else if (prev is not CatchClauseSyntax)
                        flags |= CallExecutionFlags.InTryBlock;
                    break;

                case CatchClauseSyntax:
                    flags |= CallExecutionFlags.InCatchBlock | CallExecutionFlags.IsConditional;
                    break;

                case LockStmtSyntax:
                    flags |= CallExecutionFlags.InLockBlock;
                    break;

                case UsingOrWithStmtSyntax:
                    flags |= CallExecutionFlags.InUsingResource;
                    break;

                case DeferOrGoStmtSyntax deferGo:
                    flags |= deferGo.IsGoroutine
                        ? CallExecutionFlags.IsConcurrentSpawn
                        : CallExecutionFlags.InFinallyBlock;
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
