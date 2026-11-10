using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Semantics;

namespace CodeExplorer.Core.Parser.Layers;

/// <summary>
/// Layer 5 interprocedural semantic argument coloring, top-down template instantiation,
/// ontology materialization (<see cref="ParameterNode"/>, <see cref="CallArgumentNode"/>),
/// and downward Layer 4 target grounding (<c>TARGETED_BY</c>).
/// </summary>
internal static class SemanticArgumentPropagator
{
    private const int MaxInterproceduralHops = 6;
    private const char ColonChar = ':';
    private const char DotChar = '.';
    private const char ParenOpen = '(';
    private const char ParenClose = ')';
    private const char BracketOpen = '[';
    private const char BracketClose = ']';
    private const string ParamTokenPrefix = "{param:";
    private const string TokenClose = "}";
    private const string ExprToken = "{expr}";
    private const string SqlParamPlaceholder = "__ce_param__";

    public static async Task PropagateAndGroundAsync(
        Layer4Result l4Result,
        ParsingContext ctx,
        List<Relationship> referenceRelationships,
        List<Relationship> lateBoundRels)
    {
        var functionsById = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        var methodsByTypeId = new Dictionary<string, List<FunctionNode>>(StringComparer.Ordinal);
        var visitedWalk = new HashSet<IOntologyNode>(ReferenceEqualityComparer.Instance);

        CollectFunctionsAndTypeMethods(l4Result.Prev.SyntaxStructure, enclosingType: null, functionsById, methodsByTypeId, visitedWalk);
        foreach (var file in l4Result.Prev.Prev.Prev.Files)
        {
            CollectFunctionsAndTypeMethods(file, enclosingType: null, functionsById, methodsByTypeId, visitedWalk);
        }

        if (functionsById.Count == 0) return;

        var incomingCallersByCalleeId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var outgoingCalleesByCallerId = new Dictionary<string, List<FunctionNode>>(StringComparer.Ordinal);
        var counterpartMethodsByFnId = BuildCounterpartMethodMap(referenceRelationships, methodsByTypeId);

        for (var i = 0; i < referenceRelationships.Count; i++)
        {
            var rel = referenceRelationships[i];
            if (rel.Kind != RelationshipKind.Calls) continue;
            if (!functionsById.TryGetValue(rel.From, out var callerFn) ||
                !functionsById.TryGetValue(rel.To, out var calleeFn))
            {
                continue;
            }

            if (!incomingCallersByCalleeId.TryGetValue(calleeFn.Id, out var callers))
            {
                callers = [];
                incomingCallersByCalleeId[calleeFn.Id] = callers;
            }
            if (!callers.Contains(callerFn.Id, StringComparer.Ordinal))
            {
                callers.Add(callerFn.Id);
            }

            if (!outgoingCalleesByCallerId.TryGetValue(callerFn.Id, out var callees))
            {
                callees = [];
                outgoingCalleesByCallerId[callerFn.Id] = callees;
            }
            if (!callees.Contains(calleeFn))
            {
                callees.Add(calleeFn);
            }
        }

        // Phase A: Bottom-up fixed-point parameter & call-argument coloring
        RunBottomUpColoring(functionsById, incomingCallersByCalleeId, counterpartMethodsByFnId);

        // Phase B: Collect callee parameter composite templates for top-down value assembly
        var paramTemplatesByFnAndIndex = CollectCalleeParamTemplates(functionsById, counterpartMethodsByFnId);

        // Phase C: Materialize :Parameter, :CallArgument, FLOWS_TO, and downward TARGETED_BY edges
        var targetGrounder = SemanticTargetGrounder.Build(l4Result);
        var fileToProject = Layer2ProjectParser.BuildFileToProjectMap(
            l4Result.Prev.Prev.Prev.Files,
            l4Result.Prev.Prev.Projects);

        var newNodes = new List<Node>();
        var newRels = new List<Relationship>();
        var dedupRels = new HashSet<(string From, string To, RelationshipKind Kind)>();

        void AddRelationship(IOntologyRelationship ontRel, bool alsoAddToLateBound = false)
        {
            var dbRel = Relationship.FromRelationship(ontRel);
            if (dedupRels.Add((dbRel.From, dbRel.To, dbRel.Kind)))
            {
                newRels.Add(dbRel);
                if (alsoAddToLateBound)
                {
                    lateBoundRels.Add(dbRel);
                }
            }
        }

        // 1. Materialize colored ParameterNodes
        var paramNodesByFnAndIdx = new Dictionary<(string FnId, int ParamIdx), ParameterNode>();
        foreach (var fn in functionsById.Values)
        {
            var paramSignals = fn.TryGetSignals()?.SemanticParams;
            if (paramSignals == null || paramSignals.Count == 0) continue;

            var fileId = BuildFileId(ctx.WorkspaceId, fn.FilePath);
            for (var p = 0; p < paramSignals.Count; p++)
            {
                var sig = paramSignals[p];
                if (sig.Nature == ParameterNature.Unspecified) continue;

                var paramId = string.Concat(
                    ctx.WorkspaceId, ColonChar, OntologyConstants.IdPrefixes.Parameter, ColonChar,
                    fn.Id, ColonChar, sig.Index.ToString(), ColonChar, sig.Name);

                var paramNode = new ParameterNode(
                    paramId,
                    sig.Name,
                    fn.Id,
                    sig.Index,
                    sig.DataType,
                    fn.FilePath,
                    sig.Line,
                    sig.Nature,
                    sig.SinkCallee,
                    fn.IsTest,
                    fn.ProjectKind,
                    fn.ProjectName);

                paramNodesByFnAndIdx[(fn.Id, sig.Index)] = paramNode;
                newNodes.Add(Node.FromNode(paramNode));

                AddRelationship(new HasParameterRelationship(fn.Id, paramNode.Id));
                if (!string.IsNullOrEmpty(fileId))
                {
                    AddRelationship(new DeclaredInRelationship(paramNode.Id, fileId));
                }
            }
        }

        // 2. Materialize colored CallArgumentNodes, FLOWS_TO, and downward Layer 4 TARGETED_BY edges
        foreach (var fn in functionsById.Values)
        {
            var slices = fn.TryGetSignals()?.OutgoingCallSlices;
            if (slices == null || slices.Count == 0) continue;

            var fileId = BuildFileId(ctx.WorkspaceId, fn.FilePath);
            outgoingCalleesByCallerId.TryGetValue(fn.Id, out var outgoingCallees);
            fileToProject.TryGetValue(fn.FilePath, out var ownerProject);

            for (var s = 0; s < slices.Count; s++)
            {
                var slice = slices[s];
                if (slice.Nature == ParameterNature.Unspecified) continue;

                var argId = string.Concat(
                    ctx.WorkspaceId, ColonChar, OntologyConstants.IdPrefixes.CallArgument, ColonChar,
                    fn.FilePath, ColonChar, slice.Line.ToString(), ColonChar, slice.Column.ToString(), ColonChar, slice.SlotKey);

                var displayName = slice.PropertyKey != null
                    ? string.Concat(slice.Callee, ParenOpen, slice.PropertyKey, ParenClose)
                    : string.Concat(slice.Callee, BracketOpen, slice.ArgIndex.ToString(), BracketClose);

                var assembledValues = AssembleEffectiveTargetValues(slice, outgoingCallees, paramTemplatesByFnAndIndex);
                var valueForClassification = slice.ResolvedValue ??
                                             (assembledValues.Count > 0 ? assembledValues[0] : null);
                var valueKind = ConstantValueClassifier.Classify(slice.Nature, valueForClassification);

                var argNode = new CallArgumentNode(
                    argId,
                    displayName,
                    slice.Callee,
                    slice.ArgIndex,
                    slice.PropertyKey,
                    slice.RawExpression,
                    slice.ResolvedValue,
                    slice.Nature,
                    slice.Provenance,
                    slice.Role,
                    valueKind,
                    fn.FilePath,
                    slice.Line,
                    slice.Column,
                    slice.OriginFile,
                    slice.OriginLine,
                    slice.OriginSymbol,
                    fn.IsTest,
                    fn.ProjectKind,
                    fn.ProjectName);

                newNodes.Add(Node.FromNode(argNode));

                AddRelationship(new PassesArgumentRelationship(fn.Id, argNode.Id));
                if (!string.IsNullOrEmpty(fileId))
                {
                    AddRelationship(new DeclaredInRelationship(argNode.Id, fileId));
                }

                // Intra-procedural Parameter -> CallArgument FLOWS_TO
                if (slice.ReferencedParamIndex is int refParamIdx &&
                    paramNodesByFnAndIdx.TryGetValue((fn.Id, refParamIdx), out var sourceParamNode))
                {
                    AddRelationship(new FlowsToRelationship(sourceParamNode.Id, argNode.Id));
                }

                // Inter-procedural CallArgument -> Callee Parameter FLOWS_TO
                if (slice.PropertyKey == null && outgoingCallees != null)
                {
                    for (var c = 0; c < outgoingCallees.Count; c++)
                    {
                        var calleeFn = outgoingCallees[c];
                        if (!MethodMatchesSlice(calleeFn.Name, slice.MethodName)) continue;

                        if (paramNodesByFnAndIdx.TryGetValue((calleeFn.Id, slice.ArgIndex), out var targetParamNode))
                        {
                            AddRelationship(new FlowsToRelationship(argNode.Id, targetParamNode.Id));
                        }
                    }
                }

                // Downward Layer 4 -> Layer 3 semantic target grounding (TARGETED_BY)
                targetGrounder.GroundTargets(
                    argNode,
                    slice,
                    assembledValues,
                    fn,
                    fileId,
                    ownerProject,
                    ctx,
                    newNodes,
                    AddRelationship);
            }
        }

        if (newNodes.Count > 0)
        {
            for (var n = 0; n < newNodes.Count; n++)
            {
                ctx.IncrementNodeKind(newNodes[n].Kind.ToCypherLabel());
            }
            await ctx.EnqueueUploadNodesAsync(newNodes);
            ctx.AddNodesCount(newNodes.Count);
        }

        if (newRels.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(newRels);
            ctx.TotalRelsCount += newRels.Count;
        }
    }

    private static void CollectFunctionsAndTypeMethods(
        IOntologyNode? root,
        TypeNode? enclosingType,
        Dictionary<string, FunctionNode> functionsById,
        Dictionary<string, List<FunctionNode>> methodsByTypeId,
        HashSet<IOntologyNode> visited)
    {
        if (root == null || !visited.Add(root)) return;

        var nextType = root as TypeNode ?? enclosingType;
        if (root is FunctionNode fn)
        {
            functionsById.TryAdd(fn.Id, fn);
            if (enclosingType != null)
            {
                if (!methodsByTypeId.TryGetValue(enclosingType.Id, out var list))
                {
                    list = [];
                    methodsByTypeId[enclosingType.Id] = list;
                }
                if (!list.Contains(fn))
                {
                    list.Add(fn);
                }
            }
        }

        for (var i = 0; i < root.Children.Count; i++)
        {
            CollectFunctionsAndTypeMethods(root.Children[i], nextType, functionsById, methodsByTypeId, visited);
        }
    }

    private static Dictionary<string, List<FunctionNode>> BuildCounterpartMethodMap(
        List<Relationship> referenceRelationships,
        Dictionary<string, List<FunctionNode>> methodsByTypeId)
    {
        var counterparts = new Dictionary<string, List<FunctionNode>>(StringComparer.Ordinal);
        for (var i = 0; i < referenceRelationships.Count; i++)
        {
            var rel = referenceRelationships[i];
            if (rel.Kind is not (RelationshipKind.Implements or RelationshipKind.InheritsFrom)) continue;
            if (!methodsByTypeId.TryGetValue(rel.From, out var subMethods) ||
                !methodsByTypeId.TryGetValue(rel.To, out var baseMethods))
            {
                continue;
            }

            for (var s = 0; s < subMethods.Count; s++)
            {
                var subFn = subMethods[s];
                for (var b = 0; b < baseMethods.Count; b++)
                {
                    var baseFn = baseMethods[b];
                    if (!subFn.Name.Equals(baseFn.Name, StringComparison.Ordinal)) continue;

                    if (!counterparts.TryGetValue(subFn.Id, out var listA))
                    {
                        listA = [];
                        counterparts[subFn.Id] = listA;
                    }
                    if (!listA.Contains(baseFn)) listA.Add(baseFn);

                    if (!counterparts.TryGetValue(baseFn.Id, out var listB))
                    {
                        listB = [];
                        counterparts[baseFn.Id] = listB;
                    }
                    if (!listB.Contains(subFn)) listB.Add(subFn);
                }
            }
        }

        return counterparts;
    }

    private static void RunBottomUpColoring(
        Dictionary<string, FunctionNode> functionsById,
        Dictionary<string, List<string>> incomingCallersByCalleeId,
        Dictionary<string, List<FunctionNode>> counterpartMethodsByFnId)
    {
        var worklist = new Queue<(string FnId, int ParamIdx, ParameterNature Nature, string SinkCallee, int Hop)>();
        var enqueued = new HashSet<(string FnId, int ParamIdx)>();

        foreach (var fn in functionsById.Values)
        {
            var sigParams = fn.TryGetSignals()?.SemanticParams;
            if (sigParams == null) continue;

            for (var p = 0; p < sigParams.Count; p++)
            {
                var sp = sigParams[p];
                if (sp.Nature != ParameterNature.Unspecified && enqueued.Add((fn.Id, sp.Index)))
                {
                    worklist.Enqueue((fn.Id, sp.Index, sp.Nature, sp.SinkCallee ?? fn.Name, 0));
                }
            }
        }

        while (worklist.Count > 0)
        {
            var (calleeId, paramIdx, nature, sinkCallee, hop) = worklist.Dequeue();
            if (!functionsById.TryGetValue(calleeId, out var calleeFn)) continue;

            // Mirror coloring across interface / base-class method declarations without consuming hop budget
            if (counterpartMethodsByFnId.TryGetValue(calleeId, out var counterparts))
            {
                for (var c = 0; c < counterparts.Count; c++)
                {
                    var cpFn = counterparts[c];
                    var cpParams = cpFn.TryGetSignals()?.SemanticParams;
                    if (cpParams != null && paramIdx >= 0 && paramIdx < cpParams.Count &&
                        cpParams[paramIdx].Nature == ParameterNature.Unspecified)
                    {
                        cpParams[paramIdx].Nature = nature;
                        cpParams[paramIdx].SinkCallee = sinkCallee;
                        if (enqueued.Add((cpFn.Id, paramIdx)))
                        {
                            worklist.Enqueue((cpFn.Id, paramIdx, nature, sinkCallee, hop));
                        }
                    }
                }
            }

            if (!incomingCallersByCalleeId.TryGetValue(calleeId, out var callerIds)) continue;

            for (var i = 0; i < callerIds.Count; i++)
            {
                if (!functionsById.TryGetValue(callerIds[i], out var callerFn)) continue;
                var callerSignals = callerFn.TryGetSignals();
                var slices = callerSignals?.OutgoingCallSlices;
                if (slices == null) continue;

                for (var s = 0; s < slices.Count; s++)
                {
                    var slice = slices[s];
                    if (slice.PropertyKey != null || slice.ArgIndex != paramIdx) continue;
                    if (!MethodMatchesSlice(calleeFn.Name, slice.MethodName)) continue;

                    if (slice.Nature == ParameterNature.Unspecified)
                    {
                        slice.Nature = nature;
                    }

                    if (hop < MaxInterproceduralHops &&
                        slice.ReferencedParamIndex is int callerParamIdx &&
                        callerSignals?.SemanticParams is { } callerParams &&
                        callerParamIdx >= 0 && callerParamIdx < callerParams.Count &&
                        callerParams[callerParamIdx].Nature == ParameterNature.Unspecified)
                    {
                        callerParams[callerParamIdx].Nature = nature;
                        callerParams[callerParamIdx].SinkCallee = sinkCallee;
                        if (enqueued.Add((callerFn.Id, callerParamIdx)))
                        {
                            worklist.Enqueue((callerFn.Id, callerParamIdx, nature, sinkCallee, hop + 1));
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<(string FnId, int ParamIdx), List<string>> CollectCalleeParamTemplates(
        Dictionary<string, FunctionNode> functionsById,
        Dictionary<string, List<FunctionNode>> counterpartMethodsByFnId)
    {
        var templates = new Dictionary<(string FnId, int ParamIdx), List<string>>();

        foreach (var fn in functionsById.Values)
        {
            var slices = fn.TryGetSignals()?.OutgoingCallSlices;
            if (slices == null) continue;

            for (var s = 0; s < slices.Count; s++)
            {
                var slice = slices[s];
                if (slice.Nature == ParameterNature.Unspecified ||
                    string.IsNullOrEmpty(slice.CompositeTemplate) ||
                    slice.ReferencedParamIndex is not int pIdx)
                {
                    continue;
                }

                var purePassThrough = string.Concat(ParamTokenPrefix, pIdx.ToString(), TokenClose);
                if (slice.CompositeTemplate.Equals(purePassThrough, StringComparison.Ordinal))
                {
                    continue;
                }

                AddTemplate(fn.Id, pIdx, slice.CompositeTemplate);
                if (counterpartMethodsByFnId.TryGetValue(fn.Id, out var counterparts))
                {
                    for (var c = 0; c < counterparts.Count; c++)
                    {
                        AddTemplate(counterparts[c].Id, pIdx, slice.CompositeTemplate);
                    }
                }
            }
        }

        void AddTemplate(string fnId, int pIdx, string tpl)
        {
            if (!templates.TryGetValue((fnId, pIdx), out var list))
            {
                list = [];
                templates[(fnId, pIdx)] = list;
            }
            if (!list.Contains(tpl, StringComparer.Ordinal))
            {
                list.Add(tpl);
            }
        }

        return templates;
    }

    private static List<string> AssembleEffectiveTargetValues(
        SemanticArgumentSlice slice,
        List<FunctionNode>? outgoingCallees,
        Dictionary<(string FnId, int ParamIdx), List<string>> paramTemplatesByFnAndIndex)
    {
        var values = new List<string>(2);

        // 1. If caller passes concrete value into a callee wrapper that wraps `{param:idx}` inside a SQL/URL template
        if (!string.IsNullOrWhiteSpace(slice.ResolvedValue) && slice.PropertyKey == null && outgoingCallees != null)
        {
            var placeholder = string.Concat(ParamTokenPrefix, slice.ArgIndex.ToString(), TokenClose);
            for (var c = 0; c < outgoingCallees.Count; c++)
            {
                var calleeFn = outgoingCallees[c];
                if (!MethodMatchesSlice(calleeFn.Name, slice.MethodName)) continue;

                if (paramTemplatesByFnAndIndex.TryGetValue((calleeFn.Id, slice.ArgIndex), out var tpls))
                {
                    for (var t = 0; t < tpls.Count; t++)
                    {
                        var instantiated = tpls[t].Replace(placeholder, slice.ResolvedValue, StringComparison.Ordinal);
                        if (!values.Contains(instantiated))
                        {
                            values.Add(instantiated);
                        }
                    }
                }
            }
        }

        // 2. Direct resolved value on this slice
        if (!string.IsNullOrWhiteSpace(slice.ResolvedValue) && !values.Contains(slice.ResolvedValue))
        {
            values.Add(slice.ResolvedValue);
        }

        // 3. Interpolated SQL template with runtime expression holes (`SELECT * FROM orders WHERE id = {expr}`)
        if (slice.Nature == ParameterNature.SqlQuery &&
            string.IsNullOrWhiteSpace(slice.ResolvedValue) &&
            !string.IsNullOrWhiteSpace(slice.CompositeTemplate) &&
            !slice.CompositeTemplate.StartsWith(ParamTokenPrefix, StringComparison.Ordinal))
        {
            var sqlCandidate = ReplaceTemplateHolesWithSqlPlaceholder(slice.CompositeTemplate);
            if (!string.IsNullOrWhiteSpace(sqlCandidate) && !values.Contains(sqlCandidate))
            {
                values.Add(sqlCandidate);
            }
        }

        return values;
    }

    private static string ReplaceTemplateHolesWithSqlPlaceholder(string template)
    {
        var replaced = template.Replace(ExprToken, SqlParamPlaceholder, StringComparison.Ordinal);
        var idx = replaced.IndexOf(ParamTokenPrefix, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var closeIdx = replaced.IndexOf(TokenClose, idx, StringComparison.Ordinal);
            if (closeIdx < 0) break;
            replaced = string.Concat(replaced.AsSpan(0, idx), SqlParamPlaceholder, replaced.AsSpan(closeIdx + 1));
            idx = replaced.IndexOf(ParamTokenPrefix, StringComparison.Ordinal);
        }
        return replaced;
    }

    private static bool MethodMatchesSlice(string calleeFnName, string sliceMethodName)
    {
        if (calleeFnName.Equals(sliceMethodName, StringComparison.Ordinal))
        {
            return true;
        }

        var dotIdx = calleeFnName.LastIndexOf(DotChar);
        if (dotIdx >= 0 && dotIdx < calleeFnName.Length - 1)
        {
            return calleeFnName.AsSpan(dotIdx + 1).Equals(sliceMethodName.AsSpan(), StringComparison.Ordinal);
        }

        return false;
    }

    private static string BuildFileId(string workspaceId, string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? string.Empty
            : string.Concat(workspaceId, ColonChar, OntologyConstants.IdPrefixes.File, ColonChar, filePath);
    }
}
