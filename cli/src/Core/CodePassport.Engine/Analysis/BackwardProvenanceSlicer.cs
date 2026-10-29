using CodeExplorer.Core.Parser.Syntax;
using CodePassport.Engine.Classification;
using CodePassport.Engine.Models;
using CodePassport.Engine.Semantics;

namespace CodePassport.Engine.Analysis;

/// <summary>
/// Demand-Driven Backward Provenance Slicer ("Bottom-Up Argument Legitimacy Engine").
/// 1. Builds the global Map of Dangerous Calls (Intrinsic Sinks) across the entire package.
/// 2. Traces each sink argument upward along the AST reaching-definitions and interprocedural caller graph
///    until every branch reaches a terminal Root Origin:
///    - <see cref="SinkInputProvenance.CallerParameter"/> (Public Export Parameter -> Legitimate)
///    - <see cref="SinkInputProvenance.SensitiveSourceTaint"/> (ENV/FS/DOM secret -> Illegitimate Exfiltration)
///    - <see cref="SinkInputProvenance.DecodedConstant"/> (Base64/Hex/CharCode -> Illegitimate Obfuscation)
///    - <see cref="SinkInputProvenance.InternalConstant"/> (Hardcoded internal literal -> Autonomous Sink)
/// </summary>
public sealed class BackwardProvenanceSlicer
{
    public sealed class FunctionScopeNode
    {
        public required string Key { get; init; } // "{relPath}:{fnName}"
        public required string RelativePath { get; init; }
        public required string Name { get; init; }
        public required int StartLine { get; init; }
        public required bool IsPublicExport { get; set; }
        public required SyntaxNode Syntax { get; init; }
        public string? EnclosingClassName { get; init; }
        public List<string> ParameterNames { get; init; } = [];
        public Dictionary<string, List<SyntaxNode>> LocalDefinitions { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, SymbolicBinding> LexicalEnv { get; } = new(StringComparer.Ordinal);
        public List<SyntaxNode> ReturnExpressions { get; } = [];
    }

    public sealed record CallEdgeSite(
        string CallerFunctionKey, // "{relPath}:{callerFn}" or "{relPath}:<module_init>"
        string CallerRelativePath,
        string CallerFunctionName,
        string TargetFunctionKey,
        CallExprSyntax CallSyntax,
        IReadOnlyList<SyntaxNode> Arguments,
        IReadOnlyDictionary<string, SymbolicBinding> CallerEnv
    );

    public sealed record DiscoveredSinkCall(
        string RelativePath,
        string EnclosingFunctionKey,
        string EnclosingFunctionName,
        CallExprSyntax CallSyntax,
        SymbolicBinding SinkBinding,
        IReadOnlyList<SyntaxNode> Arguments,
        IReadOnlyDictionary<string, SymbolicBinding> ScopeEnv
    );

    private readonly Dictionary<string, FunctionScopeNode> _functionsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<SyntaxNode, FunctionScopeNode> _functionsBySyntax = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, List<CallEdgeSite>> _incomingCallsByTargetKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (SyntaxNode Rhs, string WriterFnKey)> _classFieldDefs = new(StringComparer.Ordinal);
    private readonly List<DiscoveredSinkCall> _sinkCalls = [];

    public IReadOnlyList<DangerousCallTrace> BuildDangerousCallMapAndVerify(
        IReadOnlyList<SyntaxTree> syntaxTrees,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, SymbolicBinding>> crossModuleExports)
    {
        _functionsByKey.Clear();
        _functionsBySyntax.Clear();
        _incomingCallsByTargetKey.Clear();
        _classFieldDefs.Clear();
        _sinkCalls.Clear();

        // Phase 1: Index all functions, their local variable definitions, return expressions, and module environments
        var moduleEnvs = new Dictionary<string, Dictionary<string, SymbolicBinding>>(StringComparer.OrdinalIgnoreCase);
        var moduleDefs = new Dictionary<string, Dictionary<string, List<SyntaxNode>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tree in syntaxTrees)
        {
            var doc = tree.Document;
            if (doc == null) continue;
            var relPath = tree.RelativePath.Replace('\\', '/');
            if (relPath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase)) continue;

            var modEnv = new Dictionary<string, SymbolicBinding>(StringComparer.Ordinal);
            var modDef = new Dictionary<string, List<SyntaxNode>>(StringComparer.Ordinal);
            moduleEnvs[relPath] = modEnv;
            moduleDefs[relPath] = modDef;

            PopulateModuleImportsAndDefs(tree, doc, relPath, crossModuleExports, modEnv, modDef);

            // Process outer functions before nested functions so nested closures inherit parent locals & parameters
            foreach (var fnDecl in doc.AllFunctions.OrderBy(f => f.Span.StartLine))
            {
                var fnName = ExtractFunctionName(fnDecl);
                var enclosingClass = FindEnclosingTypeDecl(fnDecl);
                var className = ExtractTypeName(enclosingClass);
                var fnKey = !string.IsNullOrEmpty(className)
                    ? $"{relPath}:{className}.{fnName}"
                    : $"{relPath}:{fnName}";
                var isExported = IsFunctionExported(fnDecl, doc);
                var paramNames = ExtractParameterNames(fnDecl);

                var fnNode = new FunctionScopeNode
                {
                    Key = fnKey,
                    RelativePath = relPath,
                    Name = fnName,
                    StartLine = fnDecl.Span.StartLine + 1,
                    IsPublicExport = isExported,
                    Syntax = fnDecl,
                    EnclosingClassName = className,
                    ParameterNames = paramNames
                };

                foreach (var (k, v) in modEnv)
                {
                    fnNode.LexicalEnv[k] = v;
                }
                foreach (var (k, v) in modDef)
                {
                    fnNode.LocalDefinitions[k] = [.. v];
                }

                // Inherit lexical environment and local variable definitions from enclosing parent functions (closures)
                var parentFnDecl = FindEnclosingFunctionDecl(fnDecl);
                FunctionScopeNode? parentFnNode = null;
                if (parentFnDecl != null)
                {
                    var parentName = ExtractFunctionName(parentFnDecl);
                    var parentClass = ExtractTypeName(FindEnclosingTypeDecl(parentFnDecl));
                    var parentKey = !string.IsNullOrEmpty(parentClass)
                        ? $"{relPath}:{parentClass}.{parentName}"
                        : $"{relPath}:{parentName}";
                    if (_functionsByKey.TryGetValue(parentKey, out parentFnNode) ||
                        _functionsByKey.TryGetValue($"{relPath}:{parentName}", out parentFnNode))
                    {
                        foreach (var (k, v) in parentFnNode.LexicalEnv)
                        {
                            fnNode.LexicalEnv[k] = v;
                        }
                        foreach (var (k, v) in parentFnNode.LocalDefinitions)
                        {
                            fnNode.LocalDefinitions[k] = [.. v];
                        }
                        if (parentFnNode.IsPublicExport)
                        {
                            fnNode.IsPublicExport = true;
                        }
                    }
                }

                for (var pIdx = 0; pIdx < paramNames.Count; pIdx++)
                {
                    var pName = paramNames[pIdx];
                    if (!string.IsNullOrEmpty(pName))
                    {
                        if (fnNode.LexicalEnv.TryGetValue(pName, out var existingModBind) &&
                            existingModBind.Role == IntrinsicRole.ModuleNamespace)
                        {
                            fnNode.LexicalEnv[pName] = existingModBind with
                            {
                                SourceParameterIndex = pIdx,
                                SourceParameterName = pName
                            };
                        }
                        else
                        {
                            fnNode.LexicalEnv[pName] = new SymbolicBinding(
                                IntrinsicRole.GlobalObject,
                                CanonicalPath: $"param:{pName}",
                                SourceParameterIndex: pIdx,
                                SourceParameterName: pName);
                        }
                    }
                }

                // Append inherited parent parameters AFTER own parameters so parameter indices never collide
                if (parentFnNode != null)
                {
                    foreach (var parentParam in parentFnNode.ParameterNames)
                    {
                        if (!string.IsNullOrEmpty(parentParam) && !fnNode.ParameterNames.Contains(parentParam, StringComparer.Ordinal))
                        {
                            var inheritedIdx = fnNode.ParameterNames.Count;
                            fnNode.ParameterNames.Add(parentParam);
                            fnNode.LexicalEnv[parentParam] = new SymbolicBinding(
                                IntrinsicRole.GlobalObject,
                                CanonicalPath: $"param:{parentParam}",
                                SourceParameterIndex: inheritedIdx,
                                SourceParameterName: parentParam);
                        }
                    }
                }

                CollectFunctionLocalDefsAndReturns(fnDecl, fnNode, modEnv, _classFieldDefs);
                _functionsByKey[fnKey] = fnNode;
                _functionsByKey.TryAdd($"{relPath}:{fnName}", fnNode);
                _functionsBySyntax[fnDecl] = fnNode;
            }

            // Index ES5 Prototype method assignments: `ClassName.prototype.methodName = function(...) { ... }`
            if (tree.RootNode != null)
            {
                foreach (var assignExpr in EnumerateAssignments(tree.RootNode))
                {
                    if (!TryParsePrototypeMethodAssignment(assignExpr, out var protoClass, out var protoMethod, out var protoLambda))
                    {
                        continue;
                    }

                    var fnKey = $"{relPath}:{protoClass}.{protoMethod}";
                    var paramNames = ExtractParameterNames(protoLambda);
                    var protoFnNode = new FunctionScopeNode
                    {
                        Key = fnKey,
                        RelativePath = relPath,
                        Name = protoMethod,
                        StartLine = protoLambda.Span.StartLine + 1,
                        IsPublicExport = true,
                        Syntax = protoLambda,
                        EnclosingClassName = protoClass,
                        ParameterNames = paramNames
                    };

                    foreach (var (k, v) in modEnv) protoFnNode.LexicalEnv[k] = v;
                    foreach (var (k, v) in modDef) protoFnNode.LocalDefinitions[k] = [.. v];

                    for (var pIdx = 0; pIdx < paramNames.Count; pIdx++)
                    {
                        var pName = paramNames[pIdx];
                        if (!string.IsNullOrEmpty(pName))
                        {
                            protoFnNode.LexicalEnv[pName] = new SymbolicBinding(
                                IntrinsicRole.GlobalObject,
                                CanonicalPath: $"param:{pName}",
                                SourceParameterIndex: pIdx,
                                SourceParameterName: pName);
                        }
                    }

                    CollectFunctionLocalDefsAndReturns(protoLambda, protoFnNode, modEnv, _classFieldDefs);
                    _functionsByKey[fnKey] = protoFnNode;
                    _functionsByKey.TryAdd($"{relPath}:{protoMethod}", protoFnNode);
                    _functionsBySyntax[protoLambda] = protoFnNode;
                }

                // Index named/exported LambdaExprSyntax functions (`const fn = (...) => ...`, `exports.fn = function(...) {}`, `module.exports = function(...) {}`)
                foreach (var (lambdaName, lambdaNode) in EnumerateNamedLambdaFunctions(tree.RootNode))
                {
                    if (_functionsBySyntax.ContainsKey(lambdaNode)) continue;
                    var fnKey = $"{relPath}:{lambdaName}";
                    var paramNames = ExtractParameterNames(lambdaNode);
                    var lambdaFnNode = new FunctionScopeNode
                    {
                        Key = fnKey,
                        RelativePath = relPath,
                        Name = lambdaName,
                        StartLine = lambdaNode.Span.StartLine + 1,
                        IsPublicExport = true,
                        Syntax = lambdaNode,
                        ParameterNames = paramNames
                    };

                    foreach (var (k, v) in modEnv) lambdaFnNode.LexicalEnv[k] = v;
                    foreach (var (k, v) in modDef) lambdaFnNode.LocalDefinitions[k] = [.. v];

                    var parentScope = ResolveEnclosingFunctionScope(lambdaNode, relPath);
                    if (parentScope != null)
                    {
                        foreach (var (k, v) in parentScope.LexicalEnv) lambdaFnNode.LexicalEnv[k] = v;
                        foreach (var (k, v) in parentScope.LocalDefinitions) lambdaFnNode.LocalDefinitions[k] = [.. v];
                    }

                    for (var pIdx = 0; pIdx < paramNames.Count; pIdx++)
                    {
                        var pName = paramNames[pIdx];
                        if (!string.IsNullOrEmpty(pName))
                        {
                            lambdaFnNode.LexicalEnv[pName] = new SymbolicBinding(
                                IntrinsicRole.GlobalObject,
                                CanonicalPath: $"param:{pName}",
                                SourceParameterIndex: pIdx,
                                SourceParameterName: pName);
                        }
                    }

                    if (parentScope != null)
                    {
                        foreach (var parentParam in parentScope.ParameterNames)
                        {
                            if (!string.IsNullOrEmpty(parentParam) && !lambdaFnNode.ParameterNames.Contains(parentParam, StringComparer.Ordinal))
                            {
                                var inheritedIdx = lambdaFnNode.ParameterNames.Count;
                                lambdaFnNode.ParameterNames.Add(parentParam);
                                lambdaFnNode.LexicalEnv[parentParam] = new SymbolicBinding(
                                    IntrinsicRole.GlobalObject,
                                    CanonicalPath: $"param:{parentParam}",
                                    SourceParameterIndex: inheritedIdx,
                                    SourceParameterName: parentParam);
                            }
                        }
                    }

                    CollectFunctionLocalDefsAndReturns(lambdaNode, lambdaFnNode, modEnv, _classFieldDefs);
                    _functionsByKey.TryAdd(fnKey, lambdaFnNode);
                    _functionsBySyntax[lambdaNode] = lambdaFnNode;
                }
            }
        }

        // Phase 2: Discover all Call Sites (both interprocedural Function Calls and Intrinsic Sink Calls)
        foreach (var tree in syntaxTrees)
        {
            var doc = tree.Document;
            if (doc == null) continue;
            var relPath = tree.RelativePath.Replace('\\', '/');
            var modEnv = moduleEnvs[relPath];

            foreach (var callExpr in doc.AllCalls)
            {
                var enclosingFn = ResolveEnclosingFunctionScope(callExpr, relPath);

                var activeEnv = enclosingFn?.LexicalEnv ?? modEnv;
                var callerKey = enclosingFn?.Key ?? $"{relPath}:<module_init>";
                var callerName = enclosingFn?.Name ?? "<module_init>";
                var args = AstSymbolicEvaluator.GetCallArguments(callExpr);

                var targetBinding = ResolveCallTargetInPackage(callExpr, relPath, activeEnv, crossModuleExports);
                if (targetBinding == null) continue;

                // Case A: Call to another function inside the package -> record incoming call edge for upward slicing
                if (targetBinding.TargetLocalFunctionName != null &&
                    _functionsByKey.ContainsKey(targetBinding.TargetLocalFunctionName))
                {
                    var targetKey = targetBinding.TargetLocalFunctionName;
                    if (!_incomingCallsByTargetKey.TryGetValue(targetKey, out var list))
                    {
                        list = [];
                        _incomingCallsByTargetKey[targetKey] = list;
                    }
                    list.Add(new CallEdgeSite(
                        callerKey,
                        relPath,
                        callerName,
                        targetKey,
                        callExpr,
                        args,
                        activeEnv));
                }

                // Case B: Call to an Intrinsic Dangerous Sink (NET, PROC, FS, CODE.EVAL, DOM, CRYPTO)
                // (Skip package-internal function calls so we only treat true leaf OS/Runtime intrinsics as sinks)
                if (targetBinding.TargetLocalFunctionName == null && IsDangerousSinkBinding(targetBinding))
                {
                    // Benign standard global-this resolution idiom: `Function("return this")()`
                    if (targetBinding.CanonicalPath == "Function" &&
                        args.Count == 1 &&
                        !targetBinding.IsDecodedFromObfuscation &&
                        AstSymbolicEvaluator.EvaluateExpression(args[0], activeEnv) is { IsDecodedFromObfuscation: false, ResolvedConstant: { } fnBody } &&
                        fnBody.Trim() is "return this" or "return this;")
                    {
                        continue;
                    }

                    _sinkCalls.Add(new DiscoveredSinkCall(
                        relPath,
                        callerKey,
                        callerName,
                        callExpr,
                        targetBinding,
                        args,
                        activeEnv));
                }
            }
        }

        // Phase 3: For each discovered Dangerous Sink Call, trace every argument upward to its Root Origins
        var traces = new List<DangerousCallTrace>(_sinkCalls.Count);
        foreach (var sinkCall in _sinkCalls)
        {
            var argSlices = new List<SinkArgumentSlice>(sinkCall.Arguments.Count);
            for (var argIdx = 0; argIdx < sinkCall.Arguments.Count; argIdx++)
            {
                var argNode = sinkCall.Arguments[argIdx];
                var origins = new List<ArgumentRootOrigin>();
                var visited = new HashSet<string>(StringComparer.Ordinal);

                TraceExpressionUpward(
                    argNode,
                    sinkCall.RelativePath,
                    sinkCall.EnclosingFunctionKey,
                    sinkCall.ScopeEnv,
                    moduleDefs,
                    [sinkCall.EnclosingFunctionName],
                    origins,
                    visited,
                    depth: 0);

                var distinctOrigins = origins
                    .DistinctBy(o => (o.Provenance, o.Expression, o.OriginFile, o.OriginLine, RootCaller: o.CallChain is { Count: > 0 } ? o.CallChain[^1] : string.Empty))
                    .ToList();

                // An argument is Legitimate Caller-Driven iff:
                // 1) At least one upward path reaches a CallerParameter,
                // 2) Zero upward paths originate from SensitiveSourceTaint or DecodedConstant, AND
                // 3) For primary target arg[0], ZERO upward paths resolve to an InternalConstant (preventing Parameter Laundering
                //    via ternary fallbacks `cond ? param : "https://evil.com"` or hardcoded URL prefixes `"https://evil.com" + param.slice(0,0)`).
                var hasCallerOrigin = distinctOrigins.Any(o => o.Provenance == SinkInputProvenance.CallerParameter);
                var hasTaintOrDecoded = distinctOrigins.Any(o =>
                    o.Provenance is SinkInputProvenance.SensitiveSourceTaint
                        or SinkInputProvenance.DecodedConstant);
                var hasInternalConst = argIdx == 0
                    ? distinctOrigins.Any(o => o.Provenance == SinkInputProvenance.InternalConstant)
                    : distinctOrigins
                        .GroupBy(o => o.CallChain is { Count: > 0 } ? o.CallChain[^1] : string.Empty, StringComparer.Ordinal)
                        .Any(g => g.Any(o => o.Provenance == SinkInputProvenance.InternalConstant) &&
                                  !g.Any(o => o.Provenance == SinkInputProvenance.CallerParameter));

                var isArgLegitimate = hasCallerOrigin && !hasTaintOrDecoded && !hasInternalConst;

                argSlices.Add(new SinkArgumentSlice(
                    ArgumentIndex: argIdx,
                    ArgumentExpression: argNode.Text.Trim(),
                    IsLegitimateCallerDriven: isArgLegitimate,
                    Origins: distinctOrigins));
            }

            var (isCallLegitimate, reason) = ClassifyDangerousCallLegitimacy(sinkCall, argSlices);
            var primaryCap = CapabilityCatalog.GetCanonicalId(PrimaryFlag(sinkCall.SinkBinding.Capability));

            traces.Add(new DangerousCallTrace(
                SinkTarget: sinkCall.SinkBinding.CanonicalPath,
                Capability: primaryCap,
                FilePath: sinkCall.RelativePath,
                Line: sinkCall.CallSyntax.Span.StartLine + 1,
                EnclosingFunction: sinkCall.EnclosingFunctionName,
                IsLegitimate: isCallLegitimate,
                IllegitimacyReason: reason,
                Arguments: argSlices));
        }

        return traces;
    }

    private void TraceExpressionUpward(
        SyntaxNode? exprNode,
        string currentRelPath,
        string currentFunctionKey,
        IReadOnlyDictionary<string, SymbolicBinding> currentEnv,
        IReadOnlyDictionary<string, Dictionary<string, List<SyntaxNode>>> moduleDefs,
        List<string> callChain,
        List<ArgumentRootOrigin> origins,
        HashSet<string> visitedStates,
        int depth)
    {
        if (exprNode == null || depth > 20) return;

        var rootCaller = callChain.Count > 0 ? callChain[^1] : string.Empty;
        var stateKey = $"{currentFunctionKey}:{exprNode.Span.StartByte}:{exprNode.Span.EndByte}:{exprNode.RawGrammarKind}:{rootCaller}";
        if (!visitedStates.Add(stateKey)) return;

        // Unwrap argument / await / parenthesized wrappers
        if (exprNode is ArgumentSyntax arg)
        {
            var inner = arg.Value ?? arg.GetChildForField(SyntaxFields.Value) ?? arg.NamedChildren.FirstOrDefault();
            TraceExpressionUpward(inner, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            return;
        }
        if (exprNode is AwaitOrTryExprSyntax awaitExpr)
        {
            var inner = awaitExpr.Operand ?? awaitExpr.NamedChildren.FirstOrDefault();
            TraceExpressionUpward(inner, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            return;
        }
        if (exprNode is AssignmentExprSyntax assignWrap)
        {
            var rhs = assignWrap.Right ?? assignWrap.GetChildForField(SyntaxFields.Right) ?? (assignWrap.NamedChildren.Count > 1 ? assignWrap.NamedChildren[1] : null);
            TraceExpressionUpward(rhs, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            return;
        }
        if (exprNode.RawGrammarKind is "parenthesized_expression" or "non_null_expression" or "as_expression" or "type_assertion" or "spread_element")
        {
            TraceExpressionUpward(exprNode.NamedChildren.FirstOrDefault(), currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            return;
        }

        // 1. Check if expression is a simple Identifier (variable or parameter)
        if (exprNode is IdentifierExprSyntax idExpr)
        {
            var varName = !string.IsNullOrEmpty(idExpr.Name) ? idExpr.Name : idExpr.Text.Trim();

            // 1a. Is it a formal parameter of the current function?
            if (_functionsByKey.TryGetValue(currentFunctionKey, out var fnNode))
            {
                var paramIdx = fnNode.ParameterNames.IndexOf(varName);
                if (paramIdx >= 0)
                {
                    var hasIncomingCallers = _incomingCallsByTargetKey.TryGetValue(currentFunctionKey, out var callerSites) &&
                                             callerSites.Count > 0;

                    // If this function has incoming callers inside the package, trace upward into each caller's passed argument!
                    if (hasIncomingCallers)
                    {
                        foreach (var callSite in callerSites!)
                        {
                            if (callSite.CallerFunctionKey == currentFunctionKey ||
                                callChain.Contains(callSite.CallerFunctionName, StringComparer.Ordinal))
                            {
                                continue;
                            }

                            if (paramIdx < callSite.Arguments.Count)
                            {
                                var passedArgNode = callSite.Arguments[paramIdx];
                                var nextChain = new List<string>(callChain) { callSite.CallerFunctionName };
                                TraceExpressionUpward(
                                    passedArgNode,
                                    callSite.CallerRelativePath,
                                    callSite.CallerFunctionKey,
                                    callSite.CallerEnv,
                                    moduleDefs,
                                    nextChain,
                                    origins,
                                    visitedStates,
                                    depth + 1);
                            }
                        }
                    }

                    // If the function is a Public Package Export, the caller parameter itself is a legitimate Root Origin
                    if (fnNode.IsPublicExport)
                    {
                        origins.Add(new ArgumentRootOrigin(
                            Provenance: SinkInputProvenance.CallerParameter,
                            Expression: $"param[{paramIdx}]({varName})",
                            OriginFile: fnNode.RelativePath,
                            OriginLine: fnNode.StartLine,
                            ExportFunction: fnNode.Name,
                            ParameterName: varName,
                            ParameterIndex: paramIdx,
                            CallChain: [.. callChain]));
                    }

                    if (hasIncomingCallers)
                    {
                        return;
                    }
                }

                // 1b. Is it a local variable defined in the current function or module? Trace upward to its RHS initializers!
                //     (Do not fall back to module definitions when varName is a formal parameter of fnNode!)
                if (paramIdx < 0 && fnNode.LocalDefinitions.TryGetValue(varName, out var defRhsList) && defRhsList.Count > 0)
                {
                    foreach (var defRhsNode in defRhsList)
                    {
                        if (!ReferenceEquals(defRhsNode, exprNode))
                        {
                            TraceExpressionUpward(defRhsNode, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                        }
                    }
                    return;
                }

                if (paramIdx >= 0)
                {
                    return;
                }
            }
            else if (moduleDefs.TryGetValue(currentRelPath, out var modDef) &&
                     modDef.TryGetValue(varName, out var modRhsList) &&
                     modRhsList.Count > 0)
            {
                foreach (var modRhsNode in modRhsList)
                {
                    if (!ReferenceEquals(modRhsNode, exprNode))
                    {
                        TraceExpressionUpward(modRhsNode, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                    }
                }
                return;
            }
        }

        // 1c. If expression is an inline Lambda (`attempt(() => new Function(...))`), trace its body / return expressions upward
        if (exprNode is LambdaExprSyntax inlineLambda)
        {
            var body = inlineLambda.GetChildForField(SyntaxFields.Body) ??
                       (inlineLambda.NamedChildren.Count > 0 ? inlineLambda.NamedChildren[^1] : null);
            if (body != null)
            {
                TraceExpressionUpward(body, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                return;
            }
        }

        // 2. If expression is a Call to another package function `g(...)`, trace upward into `g()`'s return expressions
        //    AND also trace the arguments passed into `g(...)` (since pure wrapper functions like `setServernameFromNonIpHost(this.connectOpts)`
        //    or `omit(opts, 'headers')` forward their arguments to their return value).
        if (exprNode is CallExprSyntax innerCall)
        {
            var innerArgs = AstSymbolicEvaluator.GetCallArguments(innerCall);
            var calleeBinding = ResolveCallTargetInPackage(innerCall, currentRelPath, currentEnv, null);
            if (calleeBinding?.TargetLocalFunctionName != null &&
                _functionsByKey.TryGetValue(calleeBinding.TargetLocalFunctionName, out var targetFn))
            {
                var nextChain = new List<string>(callChain) { targetFn.Name };
                foreach (var retExpr in targetFn.ReturnExpressions)
                {
                    TraceExpressionUpward(
                        retExpr,
                        targetFn.RelativePath,
                        targetFn.Key,
                        targetFn.LexicalEnv,
                        moduleDefs,
                        nextChain,
                        origins,
                        visitedStates,
                        depth + 1);
                }
                if (innerArgs.Count > 0)
                {
                    TraceExpressionUpward(
                        innerArgs[0],
                        currentRelPath,
                        currentFunctionKey,
                        currentEnv,
                        moduleDefs,
                        nextChain,
                        origins,
                        visitedStates,
                        depth + 1);
                }
                return;
            }

            // If it is a benign utility, method chain (e.g. `b.substring(0, 24).split("").reverse().join("")`), or local lambda call,
            // trace its receiver object and/or arguments upward!
            var evalCall = AstSymbolicEvaluator.EvaluateExpression(innerCall, currentEnv);
            if (evalCall == null ||
                (evalCall.Role != IntrinsicRole.SensitiveSource &&
                 evalCall.TaintMask == CapabilityFlags.None &&
                 !evalCall.IsDecodedFromObfuscation &&
                 evalCall.ResolvedConstant == null))
            {
                var calleeMember = (innerCall.Callee ?? innerCall.GetChildForField(SyntaxFields.Function)) as MemberAccessExprSyntax;
                var recvNode = innerCall.Receiver ?? calleeMember?.Object ?? calleeMember?.GetChildForField(SyntaxFields.Object);
                var methodName = innerCall.MethodName;
                var isStringOrArrayChainMethod = methodName is "replace" or "split" or "join" or "reverse"
                    or "substring" or "slice" or "trim" or "toLowerCase" or "toUpperCase" or "concat";

                var tracedAny = false;
                if (recvNode != null && isStringOrArrayChainMethod)
                {
                    TraceExpressionUpward(
                        recvNode,
                        currentRelPath,
                        currentFunctionKey,
                        currentEnv,
                        moduleDefs,
                        callChain,
                        origins,
                        visitedStates,
                        depth + 1);
                    foreach (var innerArg in innerArgs)
                    {
                        if (innerArg is not LiteralExprSyntax)
                        {
                            TraceExpressionUpward(
                                innerArg,
                                currentRelPath,
                                currentFunctionKey,
                                currentEnv,
                                moduleDefs,
                                callChain,
                                origins,
                                visitedStates,
                                depth + 1);
                        }
                    }
                    return;
                }

                if (innerArgs.Count > 0)
                {
                    var hasNonLiteralArg = innerArgs.Any(a => a is not LiteralExprSyntax);
                    foreach (var innerArg in innerArgs)
                    {
                        if (hasNonLiteralArg && innerArg is LiteralExprSyntax && !IsHardcodedUrlAuthorityFragment(innerArg.Text))
                        {
                            continue;
                        }
                        TraceExpressionUpward(
                            innerArg,
                            currentRelPath,
                            currentFunctionKey,
                            currentEnv,
                            moduleDefs,
                            callChain,
                            origins,
                            visitedStates,
                            depth + 1);
                    }
                    tracedAny = true;
                }
                if (recvNode != null && recvNode.Text.Trim() is not ("Math" or "JSON" or "Object" or "Array" or "String" or "Number" or "Buffer" or "Reflect" or "Promise"))
                {
                    TraceExpressionUpward(
                        recvNode,
                        currentRelPath,
                        currentFunctionKey,
                        currentEnv,
                        moduleDefs,
                        callChain,
                        origins,
                        visitedStates,
                        depth + 1);
                    tracedAny = true;
                }

                if (tracedAny) return;
            }
        }

        // 2b. If expression is a MemberAccess (`this.connectOpts` or `opts.host`), trace upward to `this.field` assignment or receiver object
        if (exprNode is MemberAccessExprSyntax memExpr)
        {
            var objNode = memExpr.Object ??
                          memExpr.GetChildForField(SyntaxFields.Object) ??
                          (memExpr.NamedChildren.Count > 0 ? memExpr.NamedChildren[0] : null);
            var propNode = memExpr.GetChildForField(SyntaxFields.Property) ??
                           (memExpr.NamedChildren.Count > 1 ? memExpr.NamedChildren[^1] : null);
            var propName = !string.IsNullOrEmpty(memExpr.MemberName) ? memExpr.MemberName : propNode?.Text.Trim() ?? string.Empty;

            if (objNode?.Text.Trim() == "this" && !string.IsNullOrEmpty(propName))
            {
                if (_functionsByKey.TryGetValue(currentFunctionKey, out var currFn) &&
                    !string.IsNullOrEmpty(currFn.EnclosingClassName))
                {
                    var fieldKey = $"{currentRelPath}:{currFn.EnclosingClassName}.{propName}";
                    if (_classFieldDefs.TryGetValue(fieldKey, out var fieldDef) &&
                        _functionsByKey.TryGetValue(fieldDef.WriterFnKey, out var writerFn))
                    {
                        var nextChain = new List<string>(callChain) { writerFn.Name };
                        TraceExpressionUpward(
                            fieldDef.Rhs,
                            writerFn.RelativePath,
                            writerFn.Key,
                            writerFn.LexicalEnv,
                            moduleDefs,
                            nextChain,
                            origins,
                            visitedStates,
                            depth + 1);
                        return;
                    }
                    if (_functionsByKey.TryGetValue(fieldKey, out var getterFn) && getterFn.ReturnExpressions.Count > 0)
                    {
                        var nextChain = new List<string>(callChain) { getterFn.Name };
                        foreach (var retExpr in getterFn.ReturnExpressions)
                        {
                            TraceExpressionUpward(
                                retExpr,
                                getterFn.RelativePath,
                                getterFn.Key,
                                getterFn.LexicalEnv,
                                moduleDefs,
                                nextChain,
                                origins,
                                visitedStates,
                                depth + 1);
                        }
                        return;
                    }
                }
            }

            // If it is not a known intrinsic sensitive source (like process.env.SECRET), trace the receiver object upward (e.g., `opts.host` -> `opts`)
            var evalMem = AstSymbolicEvaluator.EvaluateExpression(memExpr, currentEnv);
            if (evalMem == null || (evalMem.Role != IntrinsicRole.SensitiveSource && evalMem.SourceParameterIndex != null))
            {
                if (objNode != null && objNode.Text.Trim() is not ("process" or "window" or "document" or "navigator" or "global" or "globalThis"))
                {
                    TraceExpressionUpward(objNode, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                    return;
                }
            }
        }

        // 2c. Conditional / Ternary expression (`cond ? a : b`)
        if (exprNode is ConditionalExprSyntax condExpr)
        {
            foreach (var branch in condExpr.NamedChildren.Skip(1))
            {
                TraceExpressionUpward(branch, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            }
            return;
        }

        // 2d. Binary expression (`a || b`, `a ?? b`, `a + b`)
        if (exprNode is BinaryExprSyntax binExpr)
        {
            var left = binExpr.Left ?? binExpr.GetChildForField(SyntaxFields.Left) ?? (binExpr.NamedChildren.Count > 0 ? binExpr.NamedChildren[0] : null);
            var right = binExpr.Right ?? binExpr.GetChildForField(SyntaxFields.Right) ?? (binExpr.NamedChildren.Count > 1 ? binExpr.NamedChildren[^1] : null);
            var isFallbackOperator = binExpr.Text.Contains("||", StringComparison.Ordinal) || binExpr.Text.Contains("??", StringComparison.Ordinal);

            // Ignore benign prefix/suffix literals in string concatenations (`"return " + code`, `downloadUrl + ".tmp"`)
            // while always tracing URL authority prefixes (`"https://evil.com" + x`) and logical fallback branches (`x || "https://evil.com"`).
            if (isFallbackOperator || left is not LiteralExprSyntax || right is null or LiteralExprSyntax || IsHardcodedUrlAuthorityFragment(left.Text))
            {
                TraceExpressionUpward(left, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            }
            if (isFallbackOperator || right is not LiteralExprSyntax || left is null or LiteralExprSyntax || IsHardcodedUrlAuthorityFragment(right.Text))
            {
                TraceExpressionUpward(right, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
            }
            return;
        }

        // 2e. Template literal / Interpolated string (`https://evil.com/${param}` or `${protocol}//${host}`)
        if (exprNode is InterpolatedStringExprSyntax interpExpr)
        {
            var rawText = interpExpr.Text;
            if (IsHardcodedUrlAuthorityFragment(rawText))
            {
                origins.Add(new ArgumentRootOrigin(
                    Provenance: SinkInputProvenance.InternalConstant,
                    Expression: rawText.Trim(),
                    OriginFile: currentRelPath,
                    OriginLine: interpExpr.Span.StartLine + 1,
                    CallChain: [.. callChain]));
            }
            foreach (var child in interpExpr.NamedChildren)
            {
                if (child.RawGrammarKind is "template_substitution" or "interpolation")
                {
                    TraceExpressionUpward(child.NamedChildren.FirstOrDefault() ?? child, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                }
                else if (child.RawGrammarKind is not ("string_fragment" or "template_chars"))
                {
                    TraceExpressionUpward(child, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                }
            }
            return;
        }

        // 3. If expression is an Object / Array literal (e.g., `{ method: "POST", body: payload }`), trace its property values upward
        if (exprNode is CollectionExprSyntax collExpr)
        {
            foreach (var child in collExpr.NamedChildren)
            {
                if (child.RawGrammarKind == "pair")
                {
                    var keyNode = child.GetChildForField(SyntaxFields.Key) ??
                                  (child.NamedChildren.Count > 0 ? child.NamedChildren[0] : null);
                    var keyName = AstValueResolver.Unquote(keyNode?.Text.Trim() ?? string.Empty);
                    if (NonEndpointOptionKeys.Contains(keyName))
                    {
                        continue;
                    }

                    var valNode = child.GetChildForField(SyntaxFields.Value) ??
                                  (child.NamedChildren.Count > 1 ? child.NamedChildren[^1] : null);
                    TraceExpressionUpward(valNode, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                }
                else if (child.RawGrammarKind == "spread_element")
                {
                    TraceExpressionUpward(child.NamedChildren.FirstOrDefault(), currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                }
                else
                {
                    TraceExpressionUpward(child, currentRelPath, currentFunctionKey, currentEnv, moduleDefs, callChain, origins, visitedStates, depth + 1);
                }
            }
            return;
        }

        // 4. Evaluate expression symbolically at this leaf to classify its terminal Root Origin
        var evaluated = AstSymbolicEvaluator.EvaluateExpression(exprNode, currentEnv);
        var line = exprNode.Span.StartLine + 1;

        if (evaluated != null)
        {
            if (evaluated.TaintMask != CapabilityFlags.None || evaluated.Role == IntrinsicRole.SensitiveSource)
            {
                var srcCap = CapabilityCatalog.GetCanonicalId(PrimaryFlag(evaluated.TaintMask != CapabilityFlags.None ? evaluated.TaintMask : evaluated.Capability));
                origins.Add(new ArgumentRootOrigin(
                    Provenance: SinkInputProvenance.SensitiveSourceTaint,
                    Expression: evaluated.TaintSourceExpr ?? evaluated.CanonicalPath,
                    OriginFile: currentRelPath,
                    OriginLine: line,
                    SourceCapability: srcCap,
                    CallChain: [.. callChain]));
                return;
            }

            if (evaluated.IsDecodedFromObfuscation)
            {
                origins.Add(new ArgumentRootOrigin(
                    Provenance: SinkInputProvenance.DecodedConstant,
                    Expression: evaluated.ResolvedConstant ?? exprNode.Text.Trim(),
                    OriginFile: currentRelPath,
                    OriginLine: line,
                    SourceCapability: "CRYPTO.OBFUSCATED_PAYLOAD",
                    CallChain: [.. callChain]));
                return;
            }

            if (evaluated.SourceParameterIndex is { } pIdx && evaluated.SourceParameterName is { } pName)
            {
                origins.Add(new ArgumentRootOrigin(
                    Provenance: SinkInputProvenance.CallerParameter,
                    Expression: $"param[{pIdx}]({pName})",
                    OriginFile: currentRelPath,
                    OriginLine: line,
                    ParameterName: pName,
                    ParameterIndex: pIdx,
                    CallChain: [.. callChain]));
                return;
            }

            if (evaluated.ResolvedConstant != null)
            {
                // Ignore empty string defaults (e.g. `dispatchRequest(endpointUrl, "")`) when classifying primary target legitimacy
                if (evaluated.ResolvedConstant.Length > 0)
                {
                    origins.Add(new ArgumentRootOrigin(
                        Provenance: SinkInputProvenance.InternalConstant,
                        Expression: evaluated.ResolvedConstant,
                        OriginFile: currentRelPath,
                        OriginLine: line,
                        CallChain: [.. callChain]));
                }
                return;
            }
        }
    }

    private static (bool IsLegitimate, string? Reason) ClassifyDangerousCallLegitimacy(
        DiscoveredSinkCall sinkCall,
        IReadOnlyList<SinkArgumentSlice> argSlices)
    {
        // Rule 1: Any dangerous sink call executed directly in <module_init> (on import) is never caller-parameterized
        if (sinkCall.EnclosingFunctionName == "<module_init>")
        {
            return (false, "Executed unconditionally at module import time (<module_init>) without caller invocation.");
        }

        // Rule 2: Sink target itself was resolved via obfuscated decoding (e.g. globalThis['Funct'+'ion'] or require(Buffer.from(...)))
        if (sinkCall.SinkBinding.IsDecodedFromObfuscation)
        {
            return (false, $"Sink target '{sinkCall.SinkBinding.CanonicalPath}' was resolved via obfuscated string decoding.");
        }

        // Rule 3: Check all argument origins for SensitiveSourceTaint or DecodedConstant
        foreach (var slice in argSlices)
        {
            foreach (var origin in slice.Origins)
            {
                if (origin.Provenance == SinkInputProvenance.SensitiveSourceTaint)
                {
                    // Passing `env: process.env` (ENV.CONFIG / ENV.DUMP_ALL) into a local child process spawn (PROC.SPAWN)
                    // or resolving a local file path from ENV.CONFIG / ENV.FINGERPRINT (`XDG_CONFIG_HOME`, `os.homedir()`) is standard OS configuration, not exfiltration
                    var isStandardProcessEnvInheritance =
                        (origin.SourceCapability is "ENV.CONFIG" or "ENV.DUMP_ALL" &&
                         (sinkCall.SinkBinding.Capability & CapabilityFlags.ProcSpawn) != 0 &&
                         (sinkCall.SinkBinding.Capability & (CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.DomBeacon)) == 0) ||
                        (origin.SourceCapability is "ENV.CONFIG" or "ENV.FINGERPRINT" &&
                         (sinkCall.SinkBinding.Capability & (CapabilityFlags.FsRead | CapabilityFlags.FsWrite | CapabilityFlags.FsDelete)) != 0 &&
                         (sinkCall.SinkBinding.Capability & (CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.DomBeacon)) == 0);

                    if (isStandardProcessEnvInheritance)
                    {
                        continue;
                    }

                    return (false, $"Argument {slice.ArgumentIndex} ('{slice.ArgumentExpression}') is tainted by sensitive source [{origin.SourceCapability}] '{origin.Expression}'.");
                }
                if (origin.Provenance == SinkInputProvenance.DecodedConstant)
                {
                    return (false, $"Argument {slice.ArgumentIndex} ('{slice.ArgumentExpression}') originates from decoded obfuscated payload '{origin.Expression}'.");
                }
            }
        }

        // Rule 4: Primary target argument (arg[0]: URL, command, code, file path) must trace upward to a PublicExportParameter
        // and must NOT resolve to any hardcoded InternalConstant (whether in a direct call, a secondary caller branch,
        // a ternary fallback branch `cond ? param : "https://evil.com"`, or a hardcoded URL prefix `"https://evil.com" + param`).
        if (argSlices.Count > 0)
        {
            var primaryArg = argSlices[0];
            var hardcodedTargetConst = primaryArg.Origins
                .FirstOrDefault(o => o.Provenance == SinkInputProvenance.InternalConstant);

            if (hardcodedTargetConst != null)
            {
                // Benign read-only Linux libc detection probe used by native addon loaders (`@napi-rs/*`, `@node-rs/*`, `detect-libc`)
                var constCmd = hardcodedTargetConst.Expression.Trim();
                if (sinkCall.SinkBinding.Capability == CapabilityFlags.ProcSpawn &&
                    (constCmd == "ldd --version" || constCmd.StartsWith("getconf GNU_LIBC_VERSION", StringComparison.Ordinal)))
                {
                    return (true, null);
                }

                // Benign ECMAScript intrinsic constructor probe (`Function('"use strict"; return (async function () {}).constructor;')()` in `get-intrinsic`)
                if (sinkCall.SinkBinding.CanonicalPath == "Function" &&
                    argSlices.Count == 1 &&
                    constCmd is "async function () {}" or "function* () {}" or "async function* () {}")
                {
                    return (true, null);
                }

                var isNetworkSink = (sinkCall.SinkBinding.Capability & (CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.DomBeacon)) != 0;
                var hasUnparameterizedCallerBranch = primaryArg.Origins
                    .GroupBy(o => o.CallChain is { Count: > 0 } ? o.CallChain[^1] : string.Empty, StringComparer.Ordinal)
                    .Any(g => g.Any(o => o.Provenance == SinkInputProvenance.InternalConstant) &&
                              !g.Any(o => o.Provenance == SinkInputProvenance.CallerParameter));

                // For non-network local sinks (e.g. `fs.writeFile(path.join(packagePath, "package.json"))` or `execSync("${signTool} ${manifestFile}")`),
                // if every caller branch ALSO contributes a CallerParameter (base directory / binary path) and the constant is not a URL authority, it is caller-parameterized.
                if (!isNetworkSink && !IsHardcodedUrlAuthorityFragment(constCmd) && !hasUnparameterizedCallerBranch)
                {
                    return (true, null);
                }

                var hasSecondaryCallerParam =
                    ((sinkCall.SinkBinding.Capability & (CapabilityFlags.CodeEval | CapabilityFlags.ProcSpawn)) != 0) &&
                    argSlices.Skip(1).Any(s => s.Origins.Any(o => o.Provenance == SinkInputProvenance.CallerParameter));

                if (!hasSecondaryCallerParam)
                {
                    var branchCaller = hardcodedTargetConst.CallChain is { Count: > 0 }
                        ? hardcodedTargetConst.CallChain[^1]
                        : sinkCall.EnclosingFunctionName;
                    return (false, $"Primary target argument ('{primaryArg.ArgumentExpression}') resolves to an internal hardcoded constant '{hardcodedTargetConst.Expression}' (in '{branchCaller}') instead of a public export parameter.");
                }
            }

            if (primaryArg.Origins.Any(o => o.Provenance == SinkInputProvenance.CallerParameter))
            {
                return (true, null);
            }

            // Secondary target arguments (e.g. `execFile('osascript', ['-e', script])`, `fs.createWriteStream(undefined, { fd })`,
            // `tls.connect(0, tlsOptions)`, `new Function(argName, body)`): if arg[0] is not tainted/decoded and a subsequent argument
            // traces to a CallerParameter, the call is caller-driven.
            if (argSlices.Skip(1).Any(s => s.Origins.Any(o => o.Provenance == SinkInputProvenance.CallerParameter)))
            {
                return (true, null);
            }

            // Local config/cache file write whose path is resolved from standard OS config/home directory (`XDG_CONFIG_HOME`, `os.homedir()`)
            // without any sensitive path (`.ssh`, `.npmrc`) or secret taint in the written payload
            if (sinkCall.SinkBinding.Capability == CapabilityFlags.FsWrite &&
                primaryArg.Origins.Any(o => o.Provenance == SinkInputProvenance.SensitiveSourceTaint && o.SourceCapability is "ENV.CONFIG" or "ENV.FINGERPRINT"))
            {
                return (true, null);
            }
        }
        else if (sinkCall.SinkBinding.CanonicalPath == "XMLHttpRequest")
        {
            // Zero-argument `new XMLHttpRequest()` instantiation inside a function (actual URL is passed to `xhr.open(method, url)`)
            return (true, null);
        }

        return (false, "Dangerous sink call does not trace back to any public exported function parameter.");
    }

    private static void PopulateModuleImportsAndDefs(
        SyntaxTree tree,
        SyntaxDocument doc,
        string relPath,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, SymbolicBinding>> crossModuleExports,
        Dictionary<string, SymbolicBinding> modEnv,
        Dictionary<string, List<SyntaxNode>> modDef)
    {
        foreach (var imp in doc.Imports)
        {
            if (imp.IsReExport) continue;
            var modPath = imp.ModulePath;
            if (string.IsNullOrWhiteSpace(modPath))
            {
                var srcNode = imp.GetChildForField(SyntaxFields.Source) ??
                              imp.NamedChildren.FirstOrDefault(c => c is LiteralExprSyntax);
                if (srcNode != null) modPath = AstValueResolver.Unquote(srcNode.Text);
            }
            if (string.IsNullOrWhiteSpace(modPath)) continue;

            if (IntrinsicSymbolOntology.TryResolveModule(modPath, out var modBinding))
            {
                if (!string.IsNullOrEmpty(imp.Alias)) modEnv[imp.Alias] = modBinding;
                if (!string.IsNullOrEmpty(imp.Name)) modEnv[imp.Name] = modBinding;
                foreach (var spec in imp.ImportedSymbols)
                {
                    var localName = !string.IsNullOrEmpty(spec.Alias) ? spec.Alias : spec.Name;
                    modEnv[localName] = AstSymbolicEvaluator.ResolvePropertyProjection(modBinding, modBinding.CanonicalPath, spec.Name) ?? modBinding;
                }
            }
            else if (TryResolveCrossModuleImport(relPath, modPath, crossModuleExports, out var targetExports))
            {
                foreach (var spec in imp.ImportedSymbols)
                {
                    var localName = !string.IsNullOrEmpty(spec.Alias) ? spec.Alias : spec.Name;
                    if (targetExports.TryGetValue(spec.Name, out var exportedSym))
                    {
                        modEnv[localName] = exportedSym;
                    }
                }
            }
        }

        foreach (var rawImp in tree.RawImports)
        {
            if (IntrinsicSymbolOntology.TryResolveModule(rawImp.Path, out var modBinding))
            {
                var shortModName = modBinding.CanonicalPath.Split('/', '.').Last();
                modEnv.TryAdd(shortModName, modBinding);
                if (rawImp.Specifiers != null)
                {
                    foreach (var spec in rawImp.Specifiers)
                    {
                        if (spec.IsDefault || spec.IsNamespace)
                        {
                            modEnv[spec.LocalName] = modBinding;
                        }
                        else
                        {
                            var importedProp = spec.ImportedName ?? spec.LocalName;
                            modEnv[spec.LocalName] = AstSymbolicEvaluator.ResolvePropertyProjection(modBinding, modBinding.CanonicalPath, importedProp) ?? modBinding;
                        }
                    }
                }
            }
            else if (TryResolveCrossModuleImport(relPath, rawImp.Path, crossModuleExports, out var targetExports) &&
                     rawImp.Specifiers != null)
            {
                foreach (var spec in rawImp.Specifiers)
                {
                    var importedName = spec.ImportedName ?? spec.LocalName;
                    if (targetExports.TryGetValue(importedName, out var exportedSym))
                    {
                        modEnv[spec.LocalName] = exportedSym;
                    }
                }
            }
        }

        foreach (var varDecl in doc.AllVariables)
        {
            if (FindEnclosingFunctionDecl(varDecl) != null || FindEnclosingLambda(varDecl) != null) continue;
            BindVariableIntoScope(varDecl, relPath, modEnv, modDef);
        }

        // Top-level factory calls like `module.exports = wrap({ http: http, https: https })`:
        // propagate ModuleNamespace arguments into the target local function's parameter in modEnv.
        foreach (var callExpr in doc.AllCalls)
        {
            if (FindEnclosingFunctionDecl(callExpr) != null) continue;
            var calleeName = callExpr.MethodName;
            if (string.IsNullOrEmpty(calleeName)) continue;

            var targetFn = doc.AllFunctions.FirstOrDefault(f => ExtractFunctionName(f) == calleeName);
            if (targetFn == null) continue;

            var targetParams = ExtractParameterNames(targetFn);
            var callArgs = AstSymbolicEvaluator.GetCallArguments(callExpr);
            for (var i = 0; i < Math.Min(targetParams.Count, callArgs.Count); i++)
            {
                var argEval = AstSymbolicEvaluator.EvaluateExpression(callArgs[i], modEnv);
                if (argEval is { Role: IntrinsicRole.ModuleNamespace })
                {
                    modEnv[targetParams[i]] = argEval;
                }
            }
        }
    }

    private static void CollectFunctionLocalDefsAndReturns(
        SyntaxNode fnDecl,
        FunctionScopeNode fnNode,
        Dictionary<string, SymbolicBinding> modEnv,
        Dictionary<string, (SyntaxNode Rhs, string WriterFnKey)> classFieldDefs)
    {
        var stack = new Stack<SyntaxNode>();
        stack.Push(fnDecl);
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (curr is FunctionDeclSyntax nestedFn &&
                !ReferenceEquals(curr, fnDecl) &&
                !ExtractFunctionName(nestedFn).StartsWith("<anon_", StringComparison.Ordinal))
            {
                continue;
            }

            if (curr is VariableDeclSyntax varDecl)
            {
                BindVariableIntoScope(varDecl, fnNode.RelativePath, fnNode.LexicalEnv, fnNode.LocalDefinitions, modEnv);
            }
            else if (curr is AssignmentExprSyntax assignExpr)
            {
                var leftNode = assignExpr.Left ?? assignExpr.GetChildForField(SyntaxFields.Left) ?? (assignExpr.NamedChildren.Count > 0 ? assignExpr.NamedChildren[0] : null);
                var rightNode = assignExpr.Right ?? assignExpr.GetChildForField(SyntaxFields.Right) ?? (assignExpr.NamedChildren.Count > 1 ? assignExpr.NamedChildren[1] : null);
                var innerRight = rightNode;
                while (innerRight is AssignmentExprSyntax nestedAssign)
                {
                    innerRight = nestedAssign.Right ?? nestedAssign.GetChildForField(SyntaxFields.Right) ?? (nestedAssign.NamedChildren.Count > 1 ? nestedAssign.NamedChildren[1] : innerRight);
                }

                if (leftNode is IdentifierExprSyntax leftId && innerRight != null)
                {
                    var name = !string.IsNullOrEmpty(leftId.Name) ? leftId.Name : leftId.Text.Trim();
                    AddLocalDefinition(fnNode.LocalDefinitions, name, innerRight);
                    if (!fnNode.ParameterNames.Contains(name, StringComparer.Ordinal))
                    {
                        var evaluated = AstSymbolicEvaluator.EvaluateExpression(innerRight, fnNode.LexicalEnv);
                        if (evaluated != null) fnNode.LexicalEnv[name] = evaluated;
                    }
                }
                else if (leftNode is IndexOrSliceExprSyntax leftIdx && innerRight != null)
                {
                    var targetArr = leftIdx.Target ?? leftIdx.GetChildForField(SyntaxFields.Object) ?? (leftIdx.NamedChildren.Count > 0 ? leftIdx.NamedChildren[0] : null);
                    if (targetArr is IdentifierExprSyntax arrId)
                    {
                        var arrName = !string.IsNullOrEmpty(arrId.Name) ? arrId.Name : arrId.Text.Trim();
                        AddLocalDefinition(fnNode.LocalDefinitions, arrName, innerRight);
                        var evaluated = AstSymbolicEvaluator.EvaluateExpression(innerRight, fnNode.LexicalEnv);
                        if (evaluated is { Role: IntrinsicRole.ModuleNamespace })
                        {
                            fnNode.LexicalEnv[arrName] = evaluated;
                            modEnv[arrName] = evaluated;
                        }
                    }
                }
                else if (leftNode is MemberAccessExprSyntax leftMem && innerRight != null)
                {
                    var objNode = leftMem.Object ?? leftMem.GetChildForField(SyntaxFields.Object) ?? (leftMem.NamedChildren.Count > 0 ? leftMem.NamedChildren[0] : null);
                    var propNode = leftMem.GetChildForField(SyntaxFields.Property) ?? (leftMem.NamedChildren.Count > 1 ? leftMem.NamedChildren[^1] : null);
                    var propName = !string.IsNullOrEmpty(leftMem.MemberName) ? leftMem.MemberName : propNode?.Text.Trim() ?? string.Empty;

                    if (objNode?.Text.Trim() == "this" && !string.IsNullOrEmpty(propName))
                    {
                        var ownerClass = !string.IsNullOrEmpty(fnNode.EnclosingClassName)
                            ? fnNode.EnclosingClassName
                            : fnNode.Name;
                        if (!string.IsNullOrEmpty(ownerClass))
                        {
                            var fieldKey = $"{fnNode.RelativePath}:{ownerClass}.{propName}";
                            classFieldDefs[fieldKey] = (innerRight, fnNode.Key);
                        }
                    }
                    else if (!string.IsNullOrEmpty(propName))
                    {
                        var evaluated = AstSymbolicEvaluator.EvaluateExpression(innerRight, fnNode.LexicalEnv);
                        if (evaluated is { Role: IntrinsicRole.ModuleNamespace })
                        {
                            fnNode.LexicalEnv[$"prop:{propName}"] = evaluated;
                            modEnv[$"prop:{propName}"] = evaluated;
                        }
                    }
                }
            }
            else if (curr is CallExprSyntax mutatingCall && mutatingCall.MethodName is "push" or "unshift")
            {
                var calleeMem = (mutatingCall.Callee ?? mutatingCall.GetChildForField(SyntaxFields.Function)) as MemberAccessExprSyntax;
                var recvObj = mutatingCall.Receiver ?? calleeMem?.Object ?? calleeMem?.GetChildForField(SyntaxFields.Object);
                if (recvObj is IdentifierExprSyntax arrId)
                {
                    var arrName = !string.IsNullOrEmpty(arrId.Name) ? arrId.Name : arrId.Text.Trim();
                    foreach (var pushArg in AstSymbolicEvaluator.GetCallArguments(mutatingCall))
                    {
                        AddLocalDefinition(fnNode.LocalDefinitions, arrName, pushArg);
                    }
                }
            }
            else if (curr is ReturnOrYieldStmtSyntax retStmt)
            {
                var retVal = retStmt.Value ?? retStmt.GetChildForField(SyntaxFields.Value) ?? retStmt.NamedChildren.FirstOrDefault();
                if (retVal != null)
                {
                    fnNode.ReturnExpressions.Add(retVal);

                    // Higher-order factory pattern (`function buildConnector(opts) { return function connect({ hostname, port }) { ... } }`):
                    // Merge returned function/lambda parameters into the enclosing function scope so calls inside the returned closure
                    // recognize those parameters as CallerParameters of the exported factory!
                    if (retVal is LambdaExprSyntax retLambda)
                    {
                        var innerParams = ExtractParameterNames(retLambda);
                        foreach (var pName in innerParams)
                        {
                            if (!string.IsNullOrEmpty(pName) && !fnNode.ParameterNames.Contains(pName, StringComparer.Ordinal))
                            {
                                var pIdx = fnNode.ParameterNames.Count;
                                fnNode.ParameterNames.Add(pName);
                                fnNode.LexicalEnv[pName] = new SymbolicBinding(
                                    IntrinsicRole.GlobalObject,
                                    CanonicalPath: $"param:{pName}",
                                    SourceParameterIndex: pIdx,
                                    SourceParameterName: pName);
                            }
                        }
                    }
                }
            }

            for (var i = curr.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(curr.Children[i]);
            }
        }
    }

    private static void AddLocalDefinition(
        Dictionary<string, List<SyntaxNode>> defs,
        string name,
        SyntaxNode rhs)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (!defs.TryGetValue(name, out var list))
        {
            list = [];
            defs[name] = list;
        }

        var isBenignLiteral = (rhs is LiteralExprSyntax && !IsHardcodedUrlAuthorityFragment(rhs.Text)) ||
                              (rhs is CollectionExprSyntax coll && coll.NamedChildren.Count == 0);
        if (isBenignLiteral)
        {
            if (list.Count == 0)
            {
                list.Add(rhs);
            }
        }
        else
        {
            list.RemoveAll(existing =>
                (existing is LiteralExprSyntax && !IsHardcodedUrlAuthorityFragment(existing.Text)) ||
                (existing is CollectionExprSyntax c && c.NamedChildren.Count == 0));
            if (!list.Contains(rhs, System.Collections.Generic.ReferenceEqualityComparer.Instance))
            {
                list.Add(rhs);
            }
        }
    }

    private static void BindVariableIntoScope(
        VariableDeclSyntax varDecl,
        string relPath,
        Dictionary<string, SymbolicBinding> env,
        Dictionary<string, List<SyntaxNode>> defs,
        Dictionary<string, SymbolicBinding>? modEnv = null)
    {
        var initNode = ExtractInitializerNode(varDecl);
        if (initNode == null) return;
        while (initNode is AssignmentExprSyntax nestedInitAssign)
        {
            initNode = nestedInitAssign.Right ?? nestedInitAssign.GetChildForField(SyntaxFields.Right) ?? (nestedInitAssign.NamedChildren.Count > 1 ? nestedInitAssign.NamedChildren[1] : initNode);
        }

        var lhsNode = varDecl.GetChildForField(SyntaxFields.Name) ??
                      varDecl.GetChildForField(SyntaxFields.Pattern) ??
                      varDecl.GetChildForField(SyntaxFields.Left);

        var varName = !string.IsNullOrEmpty(varDecl.Name)
            ? varDecl.Name
            : (lhsNode is IdentifierExprSyntax lhsId ? lhsId.Text.Trim() : null);

        // Support arrow/function expressions assigned to const: `const setServernameFromNonIpHost = (options) => { ... }`
        if (initNode is LambdaExprSyntax lambda && !string.IsNullOrEmpty(varName))
        {
            AddLocalDefinition(defs, varName, lambda);
            env[varName] = new SymbolicBinding(
                IntrinsicRole.LocalFunctionRef,
                CanonicalPath: $"{relPath}:{varName}",
                TargetLocalFunctionName: $"{relPath}:{varName}");
            return;
        }

        var initBinding = AstSymbolicEvaluator.EvaluateExpression(initNode, env);

        if (!string.IsNullOrEmpty(varName))
        {
            if (initBinding?.Role != IntrinsicRole.ModuleNamespace)
            {
                AddLocalDefinition(defs, varName, initNode);
            }
            if (initBinding != null)
            {
                env[varName] = initBinding;
                if (initBinding.Role == IntrinsicRole.ModuleNamespace)
                {
                    modEnv?.TryAdd(varName, initBinding);
                }
            }
        }

        // Context-bag pattern (`const cxt = { self: this, gen, schema: sch.schema }`):
        // Link shorthand builder identifiers (`gen`) that are already in `defs` to the enclosing context object so `gen.toString()` traces to `sch`
        if (initNode is CollectionExprSyntax objInit)
        {
            foreach (var objChild in objInit.NamedChildren)
            {
                if (objChild.RawGrammarKind is "shorthand_property_identifier" or "identifier")
                {
                    var shortName = objChild.Text.Trim();
                    if (!string.IsNullOrEmpty(shortName) && defs.ContainsKey(shortName))
                    {
                        AddLocalDefinition(defs, shortName, objInit);
                    }
                }
            }
        }

        if (lhsNode != null && lhsNode.RawGrammarKind is "object_pattern" or "object_assignment_pattern")
        {
            foreach (var propChild in lhsNode.NamedChildren)
            {
                if (propChild.RawGrammarKind is "pair_pattern" or "property_pattern")
                {
                    var keyNode = propChild.GetChildForField(SyntaxFields.Key) ?? (propChild.NamedChildren.Count > 0 ? propChild.NamedChildren[0] : null);
                    var valNode = propChild.GetChildForField(SyntaxFields.Value) ?? (propChild.NamedChildren.Count > 1 ? propChild.NamedChildren[1] : null);
                    if (keyNode != null && valNode != null)
                    {
                        if (initBinding?.Role != IntrinsicRole.ModuleNamespace)
                        {
                            AddLocalDefinition(defs, valNode.Text.Trim(), initNode);
                        }
                        if (initBinding != null)
                        {
                            var projected = AstSymbolicEvaluator.ResolvePropertyProjection(initBinding, initBinding.CanonicalPath, keyNode.Text.Trim());
                            if (projected != null) env[valNode.Text.Trim()] = projected;
                        }
                    }
                }
                else if (propChild.RawGrammarKind is "shorthand_property_identifier_pattern" or "identifier")
                {
                    var propName = propChild.Text.Trim();
                    if (initBinding?.Role != IntrinsicRole.ModuleNamespace)
                    {
                        AddLocalDefinition(defs, propName, initNode);
                    }
                    if (initBinding != null)
                    {
                        var projected = AstSymbolicEvaluator.ResolvePropertyProjection(initBinding, initBinding.CanonicalPath, propName);
                        if (projected != null) env[propName] = projected;
                    }
                }
            }
        }
    }

    private SymbolicBinding? ResolveCallTargetInPackage(
        CallExprSyntax callExpr,
        string currentRelPath,
        IReadOnlyDictionary<string, SymbolicBinding> activeEnv,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, SymbolicBinding>>? crossModuleExports)
    {
        var methodName = callExpr.MethodName;
        // Prefer a local function declared in the same file over an unqualified global intrinsic of the same name (e.g. `function fetch(tagName)` in domutils)
        if (!string.IsNullOrEmpty(methodName) &&
            callExpr.Receiver == null &&
            !activeEnv.ContainsKey(methodName) &&
            _functionsByKey.ContainsKey($"{currentRelPath}:{methodName}"))
        {
            var localKey = $"{currentRelPath}:{methodName}";
            return new SymbolicBinding(IntrinsicRole.LocalFunctionRef, localKey, TargetLocalFunctionName: localKey);
        }

        var resolved = AstSymbolicEvaluator.ResolveCallTargetBinding(callExpr, activeEnv);
        if (resolved != null)
        {
            // Normalize short local function name to full "{relPath}:{fnName}" key if needed
            if (resolved.TargetLocalFunctionName != null &&
                !resolved.TargetLocalFunctionName.Contains(':') &&
                _functionsByKey.ContainsKey($"{currentRelPath}:{resolved.TargetLocalFunctionName}"))
            {
                return resolved with { TargetLocalFunctionName = $"{currentRelPath}:{resolved.TargetLocalFunctionName}" };
            }
            return resolved;
        }

        if (!string.IsNullOrEmpty(methodName))
        {
            var recvText = callExpr.Receiver?.Text.Trim();
            if (!string.IsNullOrEmpty(recvText) &&
                _functionsByKey.ContainsKey($"{currentRelPath}:{recvText}.{methodName}"))
            {
                var classKey = $"{currentRelPath}:{recvText}.{methodName}";
                return new SymbolicBinding(IntrinsicRole.LocalFunctionRef, classKey, TargetLocalFunctionName: classKey);
            }

            if (_functionsByKey.ContainsKey($"{currentRelPath}:{methodName}"))
            {
                var key = $"{currentRelPath}:{methodName}";
                return new SymbolicBinding(IntrinsicRole.LocalFunctionRef, key, TargetLocalFunctionName: key);
            }
        }

        return null;
    }

    private static bool IsDangerousSinkBinding(SymbolicBinding binding)
    {
        const CapabilityFlags dangerousFlags =
            CapabilityFlags.NetHttpClient |
            CapabilityFlags.NetWebSocket |
            CapabilityFlags.NetRawIp |
            CapabilityFlags.NetExfilHost |
            CapabilityFlags.NetDnsRaw |
            CapabilityFlags.ProcSpawn |
            CapabilityFlags.ProcNative |
            CapabilityFlags.ProcShellDropper |
            CapabilityFlags.FsWrite |
            CapabilityFlags.FsDelete |
            CapabilityFlags.FsSensitivePath |
            CapabilityFlags.CodeEval |
            CapabilityFlags.CodeVm |
            CapabilityFlags.CodeMonkeyPatch |
            CapabilityFlags.DomWeb3 |
            CapabilityFlags.DomBeacon;

        return (binding.Capability & dangerousFlags) != 0 &&
               binding.Role is IntrinsicRole.CapabilitySink;
    }

    private static bool IsFunctionExported(FunctionDeclSyntax fnDecl, SyntaxDocument doc)
    {
        if (fnDecl.Modifiers.HasFlag(SyntaxModifiers.Export) || fnDecl.Modifiers.HasFlag(SyntaxModifiers.Public))
        {
            return true;
        }

        var parent = fnDecl.Parent;
        while (parent != null && !ReferenceEquals(parent, doc))
        {
            if (parent.RawGrammarKind == "export_statement" || parent.Text.TrimStart().StartsWith("export ", StringComparison.Ordinal))
            {
                return true;
            }
            parent = parent.Parent;
        }

        // Top-level functions or methods on top-level classes in a module without explicit private modifier
        var enclosingType = FindEnclosingTypeDecl(fnDecl);
        if (enclosingType != null && ReferenceEquals(enclosingType.Parent, doc))
        {
            return !fnDecl.Modifiers.HasFlag(SyntaxModifiers.Private);
        }

        return ReferenceEquals(fnDecl.Parent, doc) || IsInsideTopLevelFactoryIife(fnDecl);
    }

    private static bool IsInsideTopLevelFactoryIife(SyntaxNode node)
    {
        var curr = node.Parent;
        var foundLambda = false;
        while (curr != null)
        {
            if (curr is FunctionDeclSyntax) return false;
            if (curr is LambdaExprSyntax) foundLambda = true;
            curr = curr.Parent;
        }
        return foundLambda;
    }

    private static LambdaExprSyntax? FindEnclosingLambda(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is LambdaExprSyntax l) return l;
            curr = curr.Parent;
        }
        return null;
    }

    private static string ExtractFunctionName(FunctionDeclSyntax fnDecl)
    {
        if (!string.IsNullOrWhiteSpace(fnDecl.Name)) return fnDecl.Name;
        var nameChild = fnDecl.GetChildForField(SyntaxFields.Name) ??
                        fnDecl.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax);
        return nameChild?.Text.Trim() ?? $"<anon_{fnDecl.Span.StartLine + 1}>";
    }

    private static string? ExtractTypeName(TypeDeclSyntax? typeDecl)
    {
        if (typeDecl == null) return null;
        if (!string.IsNullOrWhiteSpace(typeDecl.Name)) return typeDecl.Name;
        var nameChild = typeDecl.GetChildForField(SyntaxFields.Name) ??
                        typeDecl.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax);
        return nameChild?.Text.Trim();
    }

    private static readonly HashSet<string> NonEndpointOptionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALPNProtocols", "highWaterMark", "method", "family", "order", "keepAlive",
        "keepAliveInitialDelay", "rejectUnauthorized", "flush", "recursive", "mode",
        "encoding", "flag", "flags", "writable", "readable", "all", "maxRedirects",
        "maxBodyLength", "jsonrpc", "timeout", "signal", "agent", "ca",
        "cert", "key", "pfx", "passphrase", "ciphers", "secureProtocol", "minVersion",
        "maxVersion", "sessionTimeout", "honorCipherOrder", "requestCert",
        "content-type", "accept", "user-agent",
        "env", "cwd", "stdio", "shell", "windowsHide", "detached", "uid", "gid",
        "argv0", "serialization", "killSignal", "maxBuffer", "windowsVerbatimArguments"
    };

    private static List<string> ExtractParameterNames(SyntaxNode fnOrLambda)
    {
        var names = new List<string>();
        var paramsContainer = fnOrLambda.GetChildForField(SyntaxFields.Parameters) ??
                              fnOrLambda.Children.FirstOrDefault(c => c.RawGrammarKind is "formal_parameters" or "parameter_list" or "parameters");
        if (paramsContainer != null)
        {
            foreach (var child in paramsContainer.NamedChildren)
            {
                CollectPatternIdentifiers(child, names);
            }
        }
        else
        {
            var singleParam = fnOrLambda.GetChildForField("parameter") ??
                              fnOrLambda.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax);
            if (singleParam != null)
            {
                CollectPatternIdentifiers(singleParam, names);
            }
        }
        return names;
    }

    private static IEnumerable<AssignmentExprSyntax> EnumerateAssignments(SyntaxNode root)
    {
        var stack = new Stack<SyntaxNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (curr is AssignmentExprSyntax assign)
            {
                yield return assign;
            }
            for (var i = curr.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(curr.Children[i]);
            }
        }
    }

    private static void CollectPatternIdentifiers(SyntaxNode patternNode, List<string> names)
    {
        if (patternNode is IdentifierExprSyntax idExpr)
        {
            var name = !string.IsNullOrEmpty(idExpr.Name) ? idExpr.Name : idExpr.Text.Trim();
            if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
            return;
        }

        if (patternNode.RawGrammarKind is "shorthand_property_identifier_pattern" or "identifier")
        {
            var name = patternNode.Text.Trim();
            if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
            return;
        }

        if (patternNode.RawGrammarKind is "pair_pattern" or "property_pattern")
        {
            var valNode = patternNode.GetChildForField(SyntaxFields.Value) ??
                          (patternNode.NamedChildren.Count > 1 ? patternNode.NamedChildren[^1] : null);
            if (valNode != null)
            {
                CollectPatternIdentifiers(valNode, names);
            }
            return;
        }

        if (patternNode.RawGrammarKind is "assignment_pattern" or "default_parameter")
        {
            var leftNode = patternNode.GetChildForField(SyntaxFields.Left) ??
                           patternNode.GetChildForField(SyntaxFields.Pattern) ??
                           patternNode.GetChildForField(SyntaxFields.Name) ??
                           patternNode.NamedChildren.FirstOrDefault();
            if (leftNode != null)
            {
                CollectPatternIdentifiers(leftNode, names);
            }
            return;
        }

        foreach (var child in patternNode.NamedChildren)
        {
            CollectPatternIdentifiers(child, names);
        }
    }

    private static bool TryParsePrototypeMethodAssignment(
        AssignmentExprSyntax assignExpr,
        out string className,
        out string methodName,
        out LambdaExprSyntax lambdaNode)
    {
        className = string.Empty;
        methodName = string.Empty;
        lambdaNode = null!;

        var leftNode = assignExpr.Left ?? assignExpr.GetChildForField(SyntaxFields.Left) ?? (assignExpr.NamedChildren.Count > 0 ? assignExpr.NamedChildren[0] : null);
        var rightNode = assignExpr.Right ?? assignExpr.GetChildForField(SyntaxFields.Right) ?? (assignExpr.NamedChildren.Count > 1 ? assignExpr.NamedChildren[1] : null);
        while (rightNode is AssignmentExprSyntax nestedAssign)
        {
            rightNode = nestedAssign.Right ?? nestedAssign.GetChildForField(SyntaxFields.Right) ?? (nestedAssign.NamedChildren.Count > 1 ? nestedAssign.NamedChildren[1] : null);
        }

        if (leftNode is not MemberAccessExprSyntax outerMem || rightNode is not LambdaExprSyntax rhsLambda)
        {
            return false;
        }

        var propNode = outerMem.GetChildForField(SyntaxFields.Property) ?? (outerMem.NamedChildren.Count > 1 ? outerMem.NamedChildren[^1] : null);
        methodName = !string.IsNullOrEmpty(outerMem.MemberName) ? outerMem.MemberName : propNode?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(methodName)) return false;

        var objNode = outerMem.Object ?? outerMem.GetChildForField(SyntaxFields.Object) ?? (outerMem.NamedChildren.Count > 0 ? outerMem.NamedChildren[0] : null);
        if (objNode is not MemberAccessExprSyntax innerMem) return false;

        var innerPropNode = innerMem.GetChildForField(SyntaxFields.Property) ?? (innerMem.NamedChildren.Count > 1 ? innerMem.NamedChildren[^1] : null);
        var innerPropName = !string.IsNullOrEmpty(innerMem.MemberName) ? innerMem.MemberName : innerPropNode?.Text.Trim() ?? string.Empty;
        if (innerPropName != "prototype") return false;

        var classObj = innerMem.Object ?? innerMem.GetChildForField(SyntaxFields.Object) ?? (innerMem.NamedChildren.Count > 0 ? innerMem.NamedChildren[0] : null);
        className = classObj?.Text.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(className)) return false;

        lambdaNode = rhsLambda;
        return true;
    }

    private FunctionScopeNode? ResolveEnclosingFunctionScope(SyntaxNode node, string relPath)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (_functionsBySyntax.TryGetValue(curr, out var bySyntax))
            {
                return bySyntax;
            }
            if (curr is FunctionDeclSyntax fnDecl)
            {
                var fnName = ExtractFunctionName(fnDecl);
                var enclosingClass = FindEnclosingTypeDecl(fnDecl);
                var className = ExtractTypeName(enclosingClass);
                var fnKey = !string.IsNullOrEmpty(className)
                    ? $"{relPath}:{className}.{fnName}"
                    : $"{relPath}:{fnName}";
                if (_functionsByKey.TryGetValue(fnKey, out var fnNode) ||
                    _functionsByKey.TryGetValue($"{relPath}:{fnName}", out fnNode))
                {
                    return fnNode;
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    private static bool IsHardcodedUrlAuthorityFragment(string rawTemplateText)
    {
        var unquoted = AstValueResolver.Unquote(rawTemplateText.Trim()).TrimStart();
        if (!unquoted.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !unquoted.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !unquoted.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
            !unquoted.StartsWith("wss://", StringComparison.OrdinalIgnoreCase) &&
            !unquoted.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var idx = unquoted.IndexOf("://", StringComparison.Ordinal);
        if (idx < 0) return false;
        var afterScheme = unquoted[(idx + 3)..];
        return afterScheme.Length > 0 && !afterScheme.StartsWith("${", StringComparison.Ordinal);
    }

    private static SyntaxNode? ExtractInitializerNode(VariableDeclSyntax varDecl)
    {
        if (varDecl.Initializer != null) return varDecl.Initializer;
        var valChild = varDecl.GetChildForField(SyntaxFields.Value) ?? varDecl.GetChildForField(SyntaxFields.Right);
        if (valChild != null) return valChild;

        for (var i = 0; i < varDecl.Children.Count - 1; i++)
        {
            if (varDecl.Children[i].RawGrammarKind == "=" || varDecl.Children[i].Text == "=")
            {
                return varDecl.Children[i + 1];
            }
        }
        return null;
    }

    private static FunctionDeclSyntax? FindEnclosingFunctionDecl(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is FunctionDeclSyntax fn) return fn;
            curr = curr.Parent;
        }
        return null;
    }

    private static TypeDeclSyntax? FindEnclosingTypeDecl(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is TypeDeclSyntax td) return td;
            curr = curr.Parent;
        }
        return null;
    }

    private static bool TryResolveCrossModuleImport(
        string importerRelPath,
        string rawImportPath,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, SymbolicBinding>> crossModuleExports,
        out IReadOnlyDictionary<string, SymbolicBinding> targetExports)
    {
        targetExports = default!;
        if (!rawImportPath.StartsWith('.')) return false;

        var lastSlash = importerRelPath.LastIndexOf('/');
        var baseDir = lastSlash >= 0 ? importerRelPath[..lastSlash] : string.Empty;
        var combined = string.IsNullOrEmpty(baseDir)
            ? rawImportPath.Replace('\\', '/')
            : $"{baseDir}/{rawImportPath.Replace('\\', '/')}";

        var parts = new List<string>();
        foreach (var seg in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".") continue;
            if (seg == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(seg);
        }

        var candidate = string.Join('/', parts);
        if (crossModuleExports.TryGetValue(candidate, out targetExports!)) return true;

        foreach (var ext in new[] { ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", "/index.ts", "/index.js", ".py" })
        {
            if (crossModuleExports.TryGetValue(candidate + ext, out targetExports!))
            {
                return true;
            }
        }

        return false;
    }

    private static CapabilityFlags PrimaryFlag(CapabilityFlags mask)
    {
        foreach (var desc in CapabilityCatalog.Descriptors)
        {
            if ((mask & desc.Flag) != 0) return desc.Flag;
        }
        return CapabilityFlags.None;
    }

    private static IEnumerable<(string Name, LambdaExprSyntax Lambda)> EnumerateNamedLambdaFunctions(SyntaxNode? root)
    {
        if (root == null) yield break;
        var stack = new Stack<SyntaxNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (curr is FunctionDeclSyntax) continue;
            if (curr is VariableDeclSyntax varDecl)
            {
                var init = ExtractInitializerNode(varDecl);
                while (init is AssignmentExprSyntax nested)
                {
                    init = nested.Right ?? nested.GetChildForField(SyntaxFields.Right) ?? (nested.NamedChildren.Count > 1 ? nested.NamedChildren[1] : null);
                }
                if (init is LambdaExprSyntax varLambda)
                {
                    var lhs = varDecl.GetChildForField(SyntaxFields.Name) ?? varDecl.GetChildForField(SyntaxFields.Pattern);
                    var name = !string.IsNullOrEmpty(varDecl.Name) ? varDecl.Name : (lhs is IdentifierExprSyntax id ? id.Text.Trim() : null);
                    if (!string.IsNullOrEmpty(name))
                    {
                        yield return (name, varLambda);
                    }
                }
            }
            else if (curr is AssignmentExprSyntax assign)
            {
                var left = assign.Left ?? assign.GetChildForField(SyntaxFields.Left) ?? (assign.NamedChildren.Count > 0 ? assign.NamedChildren[0] : null);
                var right = assign.Right ?? assign.GetChildForField(SyntaxFields.Right) ?? (assign.NamedChildren.Count > 1 ? assign.NamedChildren[1] : null);
                while (right is AssignmentExprSyntax nested)
                {
                    right = nested.Right ?? nested.GetChildForField(SyntaxFields.Right) ?? (nested.NamedChildren.Count > 1 ? nested.NamedChildren[1] : null);
                }
                if (left != null && right is LambdaExprSyntax assignLambda)
                {
                    var leftText = left.Text.Trim();
                    if (leftText == "module.exports" || leftText == "exports")
                    {
                        yield return ("default", assignLambda);
                    }
                    else if (left is MemberAccessExprSyntax mem)
                    {
                        var prop = !string.IsNullOrEmpty(mem.MemberName)
                            ? mem.MemberName
                            : (mem.GetChildForField(SyntaxFields.Property) ?? (mem.NamedChildren.Count > 1 ? mem.NamedChildren[^1] : null))?.Text.Trim();
                        if (!string.IsNullOrEmpty(prop))
                        {
                            yield return (prop, assignLambda);
                        }
                    }
                    else if (left is IdentifierExprSyntax leftId)
                    {
                        var idName = !string.IsNullOrEmpty(leftId.Name) ? leftId.Name : leftId.Text.Trim();
                        if (!string.IsNullOrEmpty(idName))
                        {
                            yield return (idName, assignLambda);
                        }
                    }
                }
            }
            else if (curr.RawGrammarKind == "export_statement")
            {
                var exportedLambda = curr.NamedChildren.OfType<LambdaExprSyntax>().FirstOrDefault();
                if (exportedLambda != null)
                {
                    yield return ("default", exportedLambda);
                }
            }
            else if (curr.RawGrammarKind == "pair")
            {
                var keyNode = curr.GetChildForField(SyntaxFields.Key) ?? (curr.NamedChildren.Count > 0 ? curr.NamedChildren[0] : null);
                var valNode = curr.GetChildForField(SyntaxFields.Value) ?? (curr.NamedChildren.Count > 1 ? curr.NamedChildren[^1] : null);
                if (keyNode != null && valNode is LambdaExprSyntax pairLambda)
                {
                    var keyName = AstValueResolver.Unquote(keyNode.Text.Trim());
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        yield return (keyName, pairLambda);
                    }
                }
            }

            for (var i = curr.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(curr.Children[i]);
            }
        }
    }
}
