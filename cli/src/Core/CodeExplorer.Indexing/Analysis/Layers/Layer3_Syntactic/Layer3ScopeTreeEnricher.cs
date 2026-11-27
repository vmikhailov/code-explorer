using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser.Layers;

/// <summary>
/// Builds hierarchical <see cref="ScopeNode"/> trees (<c>try</c>, <c>catch</c>, <c>finally</c>, <c>loop</c>,
/// <c>branch</c>, <c>resource</c>, <c>lock</c>, <c>unsafe</c>, <c>task_spawn</c>) inside <see cref="FunctionNode"/>
/// declarations and computes function-level cyclomatic complexity, cognitive complexity, nesting depth, and async-in-loop cost.
/// </summary>
internal static class Layer3ScopeTreeEnricher
{
    private const string RoleFinalizer = "finalizer";
    private const string RoleFinallyClause = "finally_clause";
    private const string OpLogicalAnd = "&&";
    private const string OpLogicalOr = "||";
    private const string OpNullCoalescing = "??";
    private const string OpWordAnd = "and";
    private const string OpWordOr = "or";
    private const string CommentSingleLine = "//";
    private const string CommentBlockStart = "/*";
    private const string CommentHash = "#";
    private const string KeywordAsync = "async";
    private const string KeywordFinally = "finally";
    private const string KeywordAwait = "await ";
    private const string KeywordForAwait = "for await";
    private const string KeywordAsyncFor = "async for";
    private const string ScopeNameTry = "try";
    private const string ScopeNameCatch = "catch";
    private const string ScopeNameFinally = "finally";
    private const string ScopeNameLoop = "loop";
    private const string ScopeNameIf = "if";
    private const string ScopeNameSwitch = "switch";
    private const string ScopeNameUsing = "using";
    private const string ScopeNameDefer = "defer";
    private const string ScopeNameLock = "lock";
    private const string ScopeNameGoroutine = "goroutine";
    private const string KeywordVar = "var";
    private const string EmptyTargetType = "";

    private static readonly HashSet<string> ConcurrentSpawnMethods = new(StringComparer.Ordinal)
    {
        "Run", "StartNew", "spawn", "create_task", "Go"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> ConcurrentSpawnLookup =
        ConcurrentSpawnMethods.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> FunctionalIterationMethods = new(StringComparer.Ordinal)
    {
        "forEach", "map", "flatMap", "filter", "reduce", "some", "every",
        "ForEach", "ForEachAsync", "Select", "Where", "All", "Any"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> FunctionalIterationLookup =
        FunctionalIterationMethods.GetAlternateLookup<ReadOnlySpan<char>>();

    internal static void EnrichFunctionsWithScopesAndComplexity(
        SyntaxDocument doc,
        FileNode fileNode,
        Dictionary<(int Line, string Name), FunctionNode> functionsByLineAndName,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        if (doc.AllFunctions.Count == 0) return;

        for (var i = 0; i < doc.AllFunctions.Count; i++)
        {
            var fnDecl = doc.AllFunctions[i];
            if (fnDecl.Kind == FunctionSyntaxKind.Accessor) continue;

            var fnName = Layer3SyntacticParser.ExtractSyntaxFunctionName(fnDecl);
            var startLine = fnDecl.Span.StartLine + 1;
            if (!functionsByLineAndName.TryGetValue((startLine, fnName), out var fnNode))
            {
                continue;
            }

            AnalyzeFunctionAstAndPopulateScopes(
                fnDecl,
                fnNode,
                workspaceId,
                relativePath,
                isTest || fnNode.IsTest,
                projectKind ?? fnNode.ProjectKind,
                projectName ?? fnNode.ProjectName);
        }
    }

    private sealed class FunctionScopeAccumulator
    {
        public int TotalBranchPoints;
        public int CognitiveComplexity;
        public int MaxNestingDepth;
        public int MaxLoopDepth;
        public int LoopAwaitCount;
    }

    private static void AnalyzeFunctionAstAndPopulateScopes(
        FunctionDeclSyntax fnDecl,
        FunctionNode fnNode,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        // Map any pre-created Catch ScopeNodes (from BaseParserVisitor.VisitCatchClause) by (Line, Column)
        Dictionary<(int Line, int Col), ScopeNode>? existingCatches = null;
        for (var i = fnNode.Children.Count - 1; i >= 0; i--)
        {
            if (fnNode.Children[i] is ScopeNode { ScopeKind: ScopeKind.Catch } existingCatch)
            {
                existingCatches ??= new Dictionary<(int Line, int Col), ScopeNode>();
                existingCatches[(existingCatch.Line, existingCatch.Column)] = existingCatch;
                if (!isTest)
                {
                    fnNode.Children.RemoveAt(i);
                }
            }
        }

        var acc = new FunctionScopeAccumulator();
        var bodyNode = (SyntaxNode?)fnDecl.Body ?? fnDecl;

        WalkSyntaxSubtree(
            bodyNode,
            parentContainer: fnNode,
            currentScope: null,
            depth: 0,
            loopDepth: 0,
            acc,
            existingCatches,
            workspaceId,
            relativePath,
            isTest,
            projectKind,
            projectName);

        if (!isTest && existingCatches is { Count: > 0 })
        {
            foreach (var unattached in existingCatches.Values)
            {
                if (!ContainsScopeRecursive(fnNode, unattached))
                {
                    fnNode.Children.Add(unattached);
                }
            }
        }

        fnNode.CyclomaticComplexity = 1 + acc.TotalBranchPoints;
        fnNode.CognitiveComplexity = acc.CognitiveComplexity;
        fnNode.MaxNestingDepth = acc.MaxNestingDepth;
        fnNode.MaxLoopDepth = acc.MaxLoopDepth;
        fnNode.LoopAwaitCount = acc.LoopAwaitCount;
    }

    private static void WalkSyntaxSubtree(
        SyntaxNode node,
        IOntologyNode parentContainer,
        ScopeNode? currentScope,
        int depth,
        int loopDepth,
        FunctionScopeAccumulator acc,
        Dictionary<(int Line, int Col), ScopeNode>? existingCatches,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child is FunctionDeclSyntax or TypeDeclSyntax)
            {
                // Nested local function or type has its own FunctionNode / TypeNode
                continue;
            }

            if (child is UnaryExprSyntax { IsAwait: true })
            {
                if (currentScope != null)
                {
                    currentScope.AwaitCount++;
                }
                if (loopDepth > 0)
                {
                    acc.LoopAwaitCount++;
                }
            }
            else if (child is ConditionalExprSyntax)
            {
                acc.TotalBranchPoints++;
                acc.CognitiveComplexity += 1 + depth;
                if (currentScope != null)
                {
                    currentScope.BranchCount++;
                }
            }
            else if (child is BinaryExprSyntax { Operator: OpLogicalAnd or OpLogicalOr or OpNullCoalescing or OpWordAnd or OpWordOr })
            {
                acc.TotalBranchPoints++;
                if (currentScope != null)
                {
                    currentScope.BranchCount++;
                }
            }

            if (TryClassifyScopeNode(
                    child,
                    depth + 1,
                    loopDepth,
                    acc,
                    existingCatches,
                    workspaceId,
                    relativePath,
                    isTest,
                    projectKind,
                    projectName,
                    out var createdScope,
                    out var childLoopDepth,
                    out var isTryWithFinally,
                    out var finallyBlockNode))
            {
                var nextContainer = parentContainer;
                var nextScope = currentScope;

                if (createdScope != null)
                {
                    if (!isTest)
                    {
                        parentContainer.Children.Add(createdScope);
                        nextContainer = createdScope;
                    }
                    nextScope = createdScope;
                }

                if (isTryWithFinally && finallyBlockNode != null)
                {
                    // Walk try children excluding the finally block under the Try scope, then walk finally under a dedicated Finally ScopeNode
                    WalkTryChildrenWithSeparateFinally(
                        child,
                        finallyBlockNode,
                        parentContainer,
                        nextContainer,
                        nextScope,
                        depth + 1,
                        childLoopDepth,
                        acc,
                        existingCatches,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                }
                else
                {
                    WalkSyntaxSubtree(
                        child,
                        nextContainer,
                        nextScope,
                        depth + 1,
                        childLoopDepth,
                        acc,
                        existingCatches,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                }
                continue;
            }

            WalkSyntaxSubtree(
                child,
                parentContainer,
                currentScope,
                depth,
                loopDepth,
                acc,
                existingCatches,
                workspaceId,
                relativePath,
                isTest,
                projectKind,
                projectName);
        }
    }

    private static void WalkTryChildrenWithSeparateFinally(
        SyntaxNode tryNode,
        SyntaxNode finallyBlockNode,
        IOntologyNode tryParentContainer,
        IOntologyNode tryScopeContainer,
        ScopeNode? tryScope,
        int tryDepth,
        int loopDepth,
        FunctionScopeAccumulator acc,
        Dictionary<(int Line, int Col), ScopeNode>? existingCatches,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        for (var i = 0; i < tryNode.Children.Count; i++)
        {
            var c = tryNode.Children[i];
            if (ReferenceEquals(c, finallyBlockNode) ||
                (c is BlockStmtSyntax && c.FirstChildOfType<BlockStmtSyntax>() is { } innerBlock && ReferenceEquals(innerBlock, finallyBlockNode)) ||
                (c is SyntaxList { Kind: SyntaxListKind.Clauses } && (ReferenceEquals(c.FirstChildOfType<BlockStmtSyntax>(), finallyBlockNode) || c.AsSpan().TrimStart().StartsWith(KeywordFinally.AsSpan(), StringComparison.Ordinal))) ||
                c.FieldRole is SyntaxFields.Finalizer or RoleFinalizer or RoleFinallyClause)
            {
                var finNode = CreateScopeNodeForAst(
                    c,
                    ScopeKind.Finally,
                    ScopeNameFinally,
                    targetType: EmptyTargetType,
                    bodyBlock: c as BlockStmtSyntax ?? c.FirstChildOfType<BlockStmtSyntax>(),
                    tryDepth,
                    loopDepth,
                    branchPoints: 0,
                    isAsync: false,
                    workspaceId,
                    relativePath,
                    isTest,
                    projectKind,
                    projectName);

                var finContainer = tryParentContainer;
                if (!isTest)
                {
                    tryParentContainer.Children.Add(finNode);
                    finContainer = finNode;
                }

                WalkSyntaxSubtree(
                    c,
                    finContainer,
                    finNode,
                    tryDepth,
                    loopDepth,
                    acc,
                    existingCatches,
                    workspaceId,
                    relativePath,
                    isTest,
                    projectKind,
                    projectName);
            }
            else if (c is CatchClauseSyntax)
            {
                // Catch is a sibling / child of Try
                if (TryClassifyScopeNode(
                        c,
                        tryDepth,
                        loopDepth,
                        acc,
                        existingCatches,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName,
                        out var catchScope,
                        out var catchLoopDepth,
                        out _,
                        out _))
                {
                    var catchContainer = tryScopeContainer;
                    if (catchScope != null && !isTest)
                    {
                        tryScopeContainer.Children.Add(catchScope);
                        catchContainer = catchScope;
                    }
                    WalkSyntaxSubtree(
                        c,
                        catchContainer,
                        catchScope ?? tryScope,
                        tryDepth,
                        catchLoopDepth,
                        acc,
                        existingCatches,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                }
            }
            else
            {
                WalkSyntaxSubtree(
                    c,
                    tryScopeContainer,
                    tryScope,
                    tryDepth,
                    loopDepth,
                    acc,
                    existingCatches,
                    workspaceId,
                    relativePath,
                    isTest,
                    projectKind,
                    projectName);
            }
        }
    }

    private static bool TryClassifyScopeNode(
        SyntaxNode node,
        int depth,
        int loopDepth,
        FunctionScopeAccumulator acc,
        Dictionary<(int Line, int Col), ScopeNode>? existingCatches,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName,
        out ScopeNode? scopeNode,
        out int nextLoopDepth,
        out bool isTryWithFinally,
        out SyntaxNode? finallyBlockNode)
    {
        scopeNode = null;
        nextLoopDepth = loopDepth;
        isTryWithFinally = false;
        finallyBlockNode = null;

        if (node is CatchClauseSyntax catchSyntax)
        {
            if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
            acc.TotalBranchPoints++;
            acc.CognitiveComplexity += depth;

            var line = catchSyntax.Span.StartLine + 1;
            var col = catchSyntax.Span.StartColumn + 1;
            if (existingCatches != null && existingCatches.Remove((line, col), out var existing))
            {
                existing.Depth = depth;
                existing.LoopDepth = loopDepth;
                existing.BranchCount = 1;
                existing.EndLine = catchSyntax.Span.EndLine + 1;
                scopeNode = existing;
                return true;
            }

            var exType = catchSyntax.ExceptionType?.Text ?? ExtractFallbackCatchType(catchSyntax) ?? EmptyTargetType;
            var body = catchSyntax.Body ?? catchSyntax.FirstChildOfType<BlockStmtSyntax>();
            scopeNode = CreateScopeNodeForAst(
                catchSyntax,
                ScopeKind.Catch,
                !string.IsNullOrEmpty(exType) ? exType : ScopeNameCatch,
                exType,
                body,
                depth,
                loopDepth,
                branchPoints: 1,
                isAsync: false,
                workspaceId,
                relativePath,
                isTest,
                projectKind,
                projectName);
            return true;
        }

        if (node is ControlFlowStmtSyntax cf)
        {
            switch (cf.Kind)
            {
                case ControlFlowKind.Try:
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    finallyBlockNode = cf.Alternative;
                    isTryWithFinally = finallyBlockNode != null;
                    scopeNode = CreateScopeNodeForAst(
                        cf,
                        ScopeKind.Try,
                        ScopeNameTry,
                        targetType: EmptyTargetType,
                        cf.Body,
                        depth,
                        loopDepth,
                        branchPoints: 0,
                        isAsync: false,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    return true;

                case ControlFlowKind.Loop:
                    nextLoopDepth = loopDepth + 1;
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    if (nextLoopDepth > acc.MaxLoopDepth) acc.MaxLoopDepth = nextLoopDepth;
                    acc.TotalBranchPoints++;
                    acc.CognitiveComplexity += depth;

                    var loopHeader = GetBoundedHeaderSpan(cf);
                    var isAsyncLoop = loopHeader.Contains(KeywordForAwait.AsSpan(), StringComparison.Ordinal) ||
                                      loopHeader.Contains(KeywordAsyncFor.AsSpan(), StringComparison.Ordinal) ||
                                      loopHeader.StartsWith(KeywordAwait.AsSpan(), StringComparison.Ordinal);
                    if (isAsyncLoop)
                    {
                        acc.LoopAwaitCount++;
                    }

                    scopeNode = CreateScopeNodeForAst(
                        cf,
                        ScopeKind.Loop,
                        ScopeNameLoop,
                        targetType: EmptyTargetType,
                        cf.Body,
                        depth,
                        nextLoopDepth,
                        branchPoints: 1,
                        isAsync: isAsyncLoop,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    if (isAsyncLoop)
                    {
                        scopeNode.AwaitCount++;
                    }
                    return true;

                case ControlFlowKind.If:
                case ControlFlowKind.Switch:
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    var branchPts = cf.Kind == ControlFlowKind.Switch ? CountSwitchArms(cf) : 1;
                    acc.TotalBranchPoints += branchPts;
                    acc.CognitiveComplexity += depth;
                    scopeNode = CreateScopeNodeForAst(
                        cf,
                        ScopeKind.Branch,
                        cf.Kind == ControlFlowKind.Switch ? ScopeNameSwitch : ScopeNameIf,
                        targetType: EmptyTargetType,
                        cf.Body,
                        depth,
                        loopDepth,
                        branchPoints: branchPts,
                        isAsync: false,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    return true;

                case ControlFlowKind.Using:
                case ControlFlowKind.Defer:
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    var resHeader = GetBoundedHeaderSpan(cf);
                    var isAsyncRes = resHeader.StartsWith(KeywordAwait.AsSpan(), StringComparison.Ordinal);
                    if (isAsyncRes && loopDepth > 0)
                    {
                        acc.LoopAwaitCount++;
                    }
                    var resTarget = ExtractResourceOrLockTarget(cf);
                    scopeNode = CreateScopeNodeForAst(
                        cf,
                        ScopeKind.Resource,
                        cf.Kind == ControlFlowKind.Defer ? ScopeNameDefer : ScopeNameUsing,
                        resTarget,
                        cf.Body,
                        depth,
                        loopDepth,
                        branchPoints: 0,
                        isAsync: isAsyncRes,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    if (isAsyncRes)
                    {
                        scopeNode.AwaitCount++;
                    }
                    return true;

                case ControlFlowKind.Lock:
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    var lockTarget = ExtractResourceOrLockTarget(cf);
                    scopeNode = CreateScopeNodeForAst(
                        cf,
                        ScopeKind.Lock,
                        ScopeNameLock,
                        lockTarget,
                        cf.Body,
                        depth,
                        loopDepth,
                        branchPoints: 0,
                        isAsync: false,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    return true;

                case ControlFlowKind.Goroutine:
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    scopeNode = CreateScopeNodeForAst(
                        cf,
                        ScopeKind.TaskSpawn,
                        ScopeNameGoroutine,
                        targetType: EmptyTargetType,
                        cf.Body ?? cf,
                        depth,
                        loopDepth,
                        branchPoints: 0,
                        isAsync: true,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    return true;
            }
        }

        if (node is LambdaExprSyntax lambda && lambda.Parent is ArgumentSyntax or CallExprSyntax)
        {
            var outerCall = lambda.Parent is ArgumentSyntax { Parent: CallExprSyntax ac } ? ac : lambda.Parent as CallExprSyntax;
            if (outerCall != null && !outerCall.MethodNameSpan.IsEmpty)
            {
                if (FunctionalIterationLookup.Contains(outerCall.MethodNameSpan))
                {
                    nextLoopDepth = loopDepth + 1;
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    if (nextLoopDepth > acc.MaxLoopDepth) acc.MaxLoopDepth = nextLoopDepth;
                    acc.TotalBranchPoints++;
                    acc.CognitiveComplexity += depth;
                    var isAsyncLambda = GetBoundedHeaderSpan(lambda).StartsWith(KeywordAsync.AsSpan(), StringComparison.Ordinal);
                    scopeNode = CreateScopeNodeForAst(
                        lambda,
                        ScopeKind.Loop,
                        outerCall.MethodName,
                        targetType: "",
                        lambda.Body,
                        depth,
                        nextLoopDepth,
                        branchPoints: 1,
                        isAsync: isAsyncLambda,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    return true;
                }

                if (ConcurrentSpawnLookup.Contains(outerCall.MethodNameSpan))
                {
                    if (depth > acc.MaxNestingDepth) acc.MaxNestingDepth = depth;
                    scopeNode = CreateScopeNodeForAst(
                        lambda,
                        ScopeKind.TaskSpawn,
                        outerCall.MethodName,
                        targetType: "",
                        lambda.Body,
                        depth,
                        loopDepth,
                        branchPoints: 0,
                        isAsync: true,
                        workspaceId,
                        relativePath,
                        isTest,
                        projectKind,
                        projectName);
                    return true;
                }
            }
        }

        return false;
    }

    private static ScopeNode CreateScopeNodeForAst(
        SyntaxNode astNode,
        ScopeKind kind,
        string name,
        string targetType,
        SyntaxNode? bodyBlock,
        int depth,
        int loopDepth,
        int branchPoints,
        bool isAsync,
        string workspaceId,
        string relativePath,
        bool isTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        var (stmtCount, hasComment) = InspectScopeBody(bodyBlock ?? astNode);
        var isEmpty = stmtCount == 0;
        var line = astNode.Span.StartLine + 1;
        var endLine = astNode.Span.EndLine + 1;
        var col = astNode.Span.StartColumn + 1;
        var kindSlug = kind.ToCanonicalString();
        var scopeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Scope}:{relativePath}:{kindSlug}:{line}:{col}";

        return new ScopeNode(
            scopeId,
            name,
            kind,
            targetType,
            relativePath,
            line,
            col,
            isEmpty,
            hasComment,
            stmtCount,
            isTest,
            projectKind,
            projectName,
            endLine,
            depth,
            loopDepth,
            AwaitCount: 0,
            BranchCount: branchPoints,
            IsAsync: isAsync);
    }

    private static (int StatementCount, bool HasComment) InspectScopeBody(SyntaxNode? bodyBlock)
    {
        if (bodyBlock == null || !bodyBlock.IsValid())
        {
            return (0, false);
        }

        var span = bodyBlock.AsSpan();
        var hasComment = span.Contains(CommentSingleLine.AsSpan(), StringComparison.Ordinal) ||
                         span.Contains(CommentBlockStart.AsSpan(), StringComparison.Ordinal) ||
                         span.Contains(CommentHash.AsSpan(), StringComparison.Ordinal);

        if (bodyBlock is not BlockStmtSyntax)
        {
            return (1, hasComment);
        }

        var stmtCount = 0;
        for (var i = 0; i < bodyBlock.Children.Count; i++)
        {
            var c = bodyBlock.Children[i];
            if (c is ControlFlowStmtSyntax { Kind: ControlFlowKind.NoOp })
            {
                continue;
            }
            stmtCount++;
        }

        return (stmtCount, hasComment);
    }

    private static ReadOnlySpan<char> GetBoundedHeaderSpan(SyntaxNode node)
    {
        var span = node.AsSpan().TrimStart();
        var braceIdx = span.IndexOf('{');
        if (braceIdx > 0) span = span[..braceIdx];
        if (span.Length > 64) span = span[..64];
        return span;
    }

    private static int CountSwitchArms(ControlFlowStmtSyntax switchStmt)
    {
        var count = 0;
        for (var i = 0; i < switchStmt.Children.Count; i++)
        {
            var c = switchStmt.Children[i];
            if (c is SyntaxList { Kind: SyntaxListKind.SwitchArms } arms)
            {
                count += Math.Max(1, arms.Children.Count);
            }
        }
        return Math.Max(1, count);
    }

    private static string ExtractResourceOrLockTarget(ControlFlowStmtSyntax cf)
    {
        for (var i = 0; i < cf.Children.Count; i++)
        {
            var c = cf.Children[i];
            if (c is BlockStmtSyntax) continue;
            if (c is VariableDeclSyntax vd)
            {
                if (vd.VariableType != null && !vd.VariableType.NameSpan.IsEmpty)
                {
                    var vtText = vd.VariableType.Text.Trim();
                    if (!string.Equals(vtText, KeywordVar, StringComparison.Ordinal))
                    {
                        return vtText;
                    }
                }
                if (vd.Initializer is CallExprSyntax initCall)
                {
                    if (!initCall.MethodNameSpan.IsEmpty) return initCall.MethodName;
                    if (initCall.Callee != null && !initCall.Callee.Span.IsEmpty) return initCall.Callee.Text.Trim();
                }
                for (var j = 0; j < vd.Children.Count; j++)
                {
                    if (vd.Children[j] is VariableDeclSyntax subVd &&
                        subVd.Initializer is CallExprSyntax subCall)
                    {
                        if (!subCall.MethodNameSpan.IsEmpty) return subCall.MethodName;
                        if (subCall.Callee != null && !subCall.Callee.Span.IsEmpty) return subCall.Callee.Text.Trim();
                    }
                }
                if (!vd.NameSpan.IsEmpty && !vd.NameSpan.SequenceEqual(KeywordVar.AsSpan()))
                {
                    return vd.Name;
                }
            }
            if (c is CallExprSyntax call && !call.MethodNameSpan.IsEmpty)
            {
                return call.MethodName;
            }
            if (c is IdentifierExprSyntax id && !id.NameSpan.IsEmpty)
            {
                return id.Name;
            }
        }
        return "";
    }

    private static bool ContainsScopeRecursive(IOntologyNode parent, ScopeNode target)
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var c = parent.Children[i];
            if (ReferenceEquals(c, target)) return true;
            if (c.Children.Count > 0 && ContainsScopeRecursive(c, target)) return true;
        }
        return false;
    }

    private static string? ExtractFallbackCatchType(CatchClauseSyntax node)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child is BlockStmtSyntax) continue;
            if (child is IdentifierExprSyntax or TypeRefSyntax) return child.Text;
            if (child is SyntaxList { Kind: SyntaxListKind.Clauses })
            {
                for (var j = 0; j < child.Children.Count; j++)
                {
                    var gc = child.Children[j];
                    if (gc is IdentifierExprSyntax or TypeRefSyntax or MemberAccessExprSyntax)
                    {
                        return gc.Text;
                    }
                }
            }
        }
        return null;
    }
}
