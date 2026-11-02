namespace CodeExplorer.Core.Common;

[Flags]
public enum CallExecutionFlags : ushort
{
    None              = 0,
    InLoop            = 1 << 0, // Inside LoopStmtSyntax or functional iteration (.forEach / Parallel.ForEach)
    InNestedLoop      = 1 << 1, // LoopDepth >= 2 (O(N^2) risk)
    IsConditional     = 1 << 2, // Guarded by IfStmt, SwitchOrMatchStmt, ConditionalExpr, or short-circuit && / ??
    InTryBlock        = 1 << 3, // Inside TryStmtSyntax.Body
    InCatchBlock      = 1 << 4, // Inside CatchClauseSyntax (error recovery / retry / logging path)
    InFinallyBlock    = 1 << 5, // Inside TryStmtSyntax.FinallyBody or DeferOrGoStmtSyntax (defer)
    InLockBlock       = 1 << 6, // Inside LockStmtSyntax (C# lock, Java synchronized)
    InUsingResource   = 1 << 7, // Inside UsingOrWithStmtSyntax (scoped disposable resource)
    IsAwaited         = 1 << 8, // Wrapped in AwaitOrTryExprSyntax
    IsConcurrentSpawn = 1 << 9, // Inside Go goroutine (`go fn()`), `Task.Run(...)`, or `tokio::spawn(...)`
    InLambda          = 1 << 10 // Inside LambdaExprSyntax closure
}

public readonly record struct CallSiteExecutionContext(
    CallExecutionFlags Flags = CallExecutionFlags.None,
    byte LoopDepth = 0,
    byte ConditionalDepth = 0
);
