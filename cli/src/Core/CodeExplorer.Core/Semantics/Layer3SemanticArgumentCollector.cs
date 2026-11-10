using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Semantics;

/// <summary>
/// Performs intra-procedural AST parameter &amp; call-argument slicing during Layer 3 while <see cref="SyntaxDocument"/>
/// is resident in memory, storing compact summaries on <see cref="FunctionAnalysisSignals"/> for Layer 5 propagation.
/// </summary>
public static class Layer3SemanticArgumentCollector
{
    private const int MaxRawExpressionChars = 512;
    private const int MaxLocalTraceDepth = 8;
    private const char DotChar = '.';
    private const char ColonChar = ':';
    private const string PlusOperator = "+";
    private const string NullCoalesceOperator = "??";
    private const string LogicalOrOperator = "||";
    private const string BaseSlotSuffix = ":base";
    private const string SuffixSlotSuffix = ":suffix";
    private const string PrimarySlotSuffix = ":primary";
    private const string FallbackSlotSuffix = ":fallback";
    private const string ParamPlaceholderPrefix = "{param:";
    private const string PlaceholderEnd = "}";

    public static void CollectForDocument(
        SyntaxDocument doc,
        FileNode fileNode,
        IReadOnlyDictionary<(int Line, string Name), FunctionNode> functionsByLineAndName,
        string relativePath,
        string? projectName)
    {
        var fieldInits = IndexFieldInitializers(doc, projectName);
        var moduleVars = IndexModuleVariables(doc, projectName);
        var scopeBySyntax = BuildFunctionScopes(doc, functionsByLineAndName);

        FunctionNode? moduleInitFn = null;
        for (var i = 0; i < fileNode.Children.Count; i++)
        {
            if (fileNode.Children[i] is FunctionNode { IsModuleInit: true } modFn)
            {
                moduleInitFn = modFn;
                break;
            }
        }

        foreach (var call in doc.AllCalls)
        {
            var methodName = call.MethodName;
            if (string.IsNullOrWhiteSpace(methodName) || call.Arguments.Count == 0)
            {
                continue;
            }

            var enclosingDecl = Layer3ArgumentExpressionEvaluator.FindEnclosingFunctionDecl(call);
            SemanticFunctionScopeContext? fnScope = null;
            FunctionNode? callerFnNode = null;

            if (enclosingDecl != null && scopeBySyntax.TryGetValue(enclosingDecl, out fnScope))
            {
                callerFnNode = fnScope.Node;
            }
            else if (enclosingDecl == null && moduleInitFn != null)
            {
                callerFnNode = moduleInitFn;
            }

            if (callerFnNode == null)
            {
                continue;
            }

            var callee = !string.IsNullOrEmpty(call.ExplicitReceiverPath)
                ? string.Concat(call.ExplicitReceiverPath, DotChar, methodName)
                : methodName;

            var callLine = call.Span.StartLine + 1;
            var callCol = call.Span.StartColumn + 1;
            var slices = callerFnNode.Signals().OutgoingCallSlices ??= [];

            for (var argIdx = 0; argIdx < call.Arguments.Count; argIdx++)
            {
                var argVal = call.Arguments[argIdx].Value;
                if (argVal == null) continue;

                var directNature = PlatformParameterCatalog.ResolveParameterNature(callee, argIdx);
                if (directNature == ParameterNature.Unspecified)
                {
                    directNature = PlatformParameterCatalog.ResolveParameterNature(methodName, argIdx);
                }

                if (TryCollectObjectArgumentSlices(
                        argVal, callee, methodName, callLine, callCol, argIdx,
                        fnScope, fieldInits, moduleVars, relativePath, projectName, slices))
                {
                    continue;
                }

                if (directNature == ParameterNature.Unspecified &&
                    !Layer3ArgumentExpressionEvaluator.IsPotentialSemanticCandidate(argVal))
                {
                    continue;
                }

                SliceIntoList(
                    argVal, callee, methodName, callLine, callCol, argIdx,
                    propertyKey: null, argIdx.ToString(), directNature, ExpressionRole.Standalone,
                    fnScope, fieldInits, moduleVars, relativePath, projectName, slices,
                    originOverrideSymbol: null, originOverrideLine: null, depth: 0);
            }

            if (fnScope != null)
            {
                for (var s = 0; s < slices.Count; s++)
                {
                    var slice = slices[s];
                    if (slice.Nature != ParameterNature.Unspecified &&
                        slice.ReferencedParamIndex is int pIdx &&
                        pIdx >= 0 && pIdx < fnScope.Params.Count &&
                        fnScope.Params[pIdx].Nature == ParameterNature.Unspecified)
                    {
                        fnScope.Params[pIdx].Nature = slice.Nature;
                        fnScope.Params[pIdx].SinkCallee = slice.Callee;
                    }
                }
            }
        }
    }

    private static Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> IndexFieldInitializers(
        SyntaxDocument doc,
        string? projectName)
    {
        var map = new Dictionary<(string, string), SemanticFieldInitInfo>();
        foreach (var mem in doc.AllMembers)
        {
            var typeDecl = Layer3ArgumentExpressionEvaluator.FindEnclosingTypeDecl(mem);
            var typeName = typeDecl?.Name ?? string.Empty;

            if (!string.IsNullOrEmpty(mem.Name))
            {
                var resolved = mem.ResolvedValue ??
                               (mem.Initializer != null ? AstValueResolver.ResolveString(mem.Initializer, projectName) : null);
                map.TryAdd((typeName, mem.Name), new SemanticFieldInitInfo(resolved, mem.Span.StartLine + 1));
            }

            for (var i = 0; i < mem.Children.Count; i++)
            {
                if (mem.Children[i] is VariableDeclSyntax vd && !string.IsNullOrEmpty(vd.Name))
                {
                    var resolved = vd.ResolvedValue ??
                                   (vd.Initializer != null ? AstValueResolver.ResolveString(vd.Initializer, projectName) : null);
                    map.TryAdd((typeName, vd.Name), new SemanticFieldInitInfo(resolved, vd.Span.StartLine + 1));
                }
            }
        }
        return map;
    }

    private static Dictionary<string, (ExprSyntax? Init, string? Resolved, int Line)> IndexModuleVariables(
        SyntaxDocument doc,
        string? projectName)
    {
        var map = new Dictionary<string, (ExprSyntax?, string?, int)>(StringComparer.Ordinal);
        foreach (var vd in doc.AllVariables)
        {
            if (Layer3ArgumentExpressionEvaluator.FindEnclosingFunctionDecl(vd) != null || string.IsNullOrEmpty(vd.Name))
            {
                continue;
            }

            var resolved = vd.ResolvedValue ??
                           (vd.Initializer != null ? AstValueResolver.ResolveString(vd.Initializer, projectName) : null);
            map.TryAdd(vd.Name, (vd.Initializer, resolved, vd.Span.StartLine + 1));
        }
        return map;
    }

    private static Dictionary<FunctionDeclSyntax, SemanticFunctionScopeContext> BuildFunctionScopes(
        SyntaxDocument doc,
        IReadOnlyDictionary<(int Line, string Name), FunctionNode> functionsByLineAndName)
    {
        var map = new Dictionary<FunctionDeclSyntax, SemanticFunctionScopeContext>(ReferenceEqualityComparer.Instance);
        foreach (var fnDecl in doc.AllFunctions)
        {
            if (fnDecl.Kind == FunctionSyntaxKind.Accessor) continue;
            var startLine = fnDecl.Span.StartLine + 1;
            var name = fnDecl.Name;
            if (string.IsNullOrEmpty(name)) continue;

            var dotIdx = name.LastIndexOf(DotChar);
            var shortName = dotIdx >= 0 && dotIdx < name.Length - 1 ? name[(dotIdx + 1)..] : name;

            if (!functionsByLineAndName.TryGetValue((startLine, name), out var fnNode) &&
                !functionsByLineAndName.TryGetValue((startLine, shortName), out fnNode))
            {
                continue;
            }

            var paramIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            var paramList = new List<SemanticParamSignal>(fnDecl.Parameters.Count);

            for (var p = 0; p < fnDecl.Parameters.Count; p++)
            {
                var pDecl = fnDecl.Parameters[p];
                var pName = pDecl.Name;
                if (string.IsNullOrWhiteSpace(pName) || pDecl.PassingMode == ParameterPassingMode.Receiver)
                {
                    continue;
                }

                var logicalIdx = paramList.Count;
                paramIndexByName.TryAdd(pName, logicalIdx);

                var explicitNature = PlatformParameterCatalog.ResolveAnnotationNature(pDecl.Annotations);
                paramList.Add(new SemanticParamSignal
                {
                    Index = logicalIdx,
                    Name = pName,
                    DataType = pDecl.ParameterType?.Text.Trim(),
                    Line = pDecl.Span.StartLine + 1,
                    Nature = explicitNature
                });
            }

            if (paramList.Count > 0)
            {
                fnNode.Signals().SemanticParams = paramList;
            }

            var enclosingType = Layer3ArgumentExpressionEvaluator.FindEnclosingTypeDecl(fnDecl);
            map[fnDecl] = new SemanticFunctionScopeContext
            {
                Syntax = fnDecl,
                Node = fnNode,
                ParamIndexByName = paramIndexByName,
                Params = paramList,
                EnclosingTypeName = enclosingType?.Name ?? fnNode.TryGetSignals()?.EnclosingTypeName
            };
        }

        return map;
    }

    private static bool TryCollectObjectArgumentSlices(
        ExprSyntax argVal,
        string callee,
        string methodName,
        int callLine,
        int callCol,
        int argIdx,
        SemanticFunctionScopeContext? fnScope,
        Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> fieldInits,
        Dictionary<string, (ExprSyntax? Init, string? Resolved, int Line)> moduleVars,
        string relativePath,
        string? projectName,
        List<SemanticArgumentSlice> slices)
    {
        CollectionExprSyntax? coll = argVal as CollectionExprSyntax;
        string? originSym = null;
        int? originLine = null;

        if (coll == null && argVal is IdentifierExprSyntax idExpr)
        {
            var localDef = Layer3ArgumentExpressionEvaluator.FindLocalVariableInitializer(idExpr, out var defLine);
            if (localDef is CollectionExprSyntax localColl)
            {
                coll = localColl;
                originSym = idExpr.Name;
                originLine = defLine;
            }
            else if (moduleVars.TryGetValue(idExpr.Name, out var modEntry) && modEntry.Init is CollectionExprSyntax modColl)
            {
                coll = modColl;
                originSym = idExpr.Name;
                originLine = modEntry.Line;
            }
        }

        if (coll is not { Kind: CollectionSyntaxKind.Object or CollectionSyntaxKind.Dictionary or CollectionSyntaxKind.Initializer })
        {
            return false;
        }

        var emittedAny = false;
        for (var i = 0; i < coll.Elements.Count; i++)
        {
            if (coll.Elements[i] is not KeyValueExprSyntax kv || kv.Value == null)
            {
                continue;
            }

            var propKey = AstValueResolver.Unquote(kv.KeySpan).Trim();
            if (string.IsNullOrEmpty(propKey)) continue;

            var propNature = PlatformParameterCatalog.ResolveObjectPropertyNature(callee, propKey);
            if (propNature == ParameterNature.Unspecified)
            {
                propNature = PlatformParameterCatalog.ResolveObjectPropertyNature(methodName, propKey);
            }

            if (propNature == ParameterNature.Unspecified)
            {
                continue;
            }

            emittedAny = true;
            var slotKey = string.Concat(argIdx.ToString(), ColonChar, propKey);
            SliceIntoList(
                kv.Value, callee, methodName, callLine, callCol, argIdx, propKey, slotKey,
                propNature, ExpressionRole.Standalone, fnScope, fieldInits, moduleVars,
                relativePath, projectName, slices, originSym, originLine, depth: 0);
        }

        return emittedAny;
    }

    private static void SliceIntoList(
        ExprSyntax expr,
        string callee,
        string methodName,
        int callLine,
        int callCol,
        int argIdx,
        string? propertyKey,
        string slotKey,
        ParameterNature nature,
        ExpressionRole role,
        SemanticFunctionScopeContext? fnScope,
        Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> fieldInits,
        Dictionary<string, (ExprSyntax? Init, string? Resolved, int Line)> moduleVars,
        string relativePath,
        string? projectName,
        List<SemanticArgumentSlice> slices,
        string? originOverrideSymbol,
        int? originOverrideLine,
        int depth)
    {
        if (depth > MaxLocalTraceDepth) return;

        expr = Layer3ArgumentExpressionEvaluator.UnwrapTransparentWrappers(expr);

        if (expr is BinaryExprSyntax bin && bin.Left != null && bin.Right != null)
        {
            var op = bin.Operator.Trim();
            if (op is NullCoalesceOperator or LogicalOrOperator)
            {
                SliceIntoList(bin.Left, callee, methodName, callLine, callCol, argIdx, propertyKey,
                    string.Concat(slotKey, PrimarySlotSuffix), nature, role, fnScope, fieldInits, moduleVars,
                    relativePath, projectName, slices, originOverrideSymbol, originOverrideLine, depth + 1);
                SliceIntoList(bin.Right, callee, methodName, callLine, callCol, argIdx, propertyKey,
                    string.Concat(slotKey, FallbackSlotSuffix), nature, ExpressionRole.FallbackDefault, fnScope, fieldInits, moduleVars,
                    relativePath, projectName, slices, originOverrideSymbol, originOverrideLine, depth + 1);
                return;
            }

            if (op == PlusOperator)
            {
                if (!Layer3ArgumentExpressionEvaluator.ContainsDynamicOrParamReference(bin, fnScope) &&
                    AstValueResolver.TryResolveString(bin, projectName, out var unifiedVal) &&
                    !string.IsNullOrEmpty(unifiedVal))
                {
                    var prov = Layer3ArgumentExpressionEvaluator.ReferencesClassField(bin, fnScope, fieldInits, out var fldSym, out var fldLine)
                        ? ArgumentProvenance.MemberOrField
                        : ArgumentProvenance.InternalConstant;

                    slices.Add(CreateSlice(
                        expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                        unifiedVal, compositeTemplate: null, nature, prov, role, referencedParamIdx: null,
                        relativePath, originOverrideLine ?? fldLine, originOverrideSymbol ?? fldSym));
                    return;
                }

                var compositeTemplate = Layer3ArgumentExpressionEvaluator.TryBuildBinaryCompositeTemplate(bin, fnScope, fieldInits, projectName);
                var countBefore = slices.Count;

                SliceIntoList(bin.Left, callee, methodName, callLine, callCol, argIdx, propertyKey,
                    string.Concat(slotKey, BaseSlotSuffix), nature, ExpressionRole.CompositeBase, fnScope, fieldInits, moduleVars,
                    relativePath, projectName, slices, originOverrideSymbol, originOverrideLine, depth + 1);
                SliceIntoList(bin.Right, callee, methodName, callLine, callCol, argIdx, propertyKey,
                    string.Concat(slotKey, SuffixSlotSuffix), nature, ExpressionRole.CompositeSuffix, fnScope, fieldInits, moduleVars,
                    relativePath, projectName, slices, originOverrideSymbol, originOverrideLine, depth + 1);

                if (!string.IsNullOrEmpty(compositeTemplate))
                {
                    AttachCompositeTemplate(slices, countBefore, compositeTemplate);
                }
                return;
            }
        }

        if (expr is InterpolatedStringExprSyntax || Layer3ArgumentExpressionEvaluator.HasInterpolationHoles(expr))
        {
            Layer3ArgumentExpressionEvaluator.AnalyzeInterpolatedExpression(
                expr, fnScope, fieldInits, projectName,
                out var interpResolved, out var interpTemplate, out var interpProv, out var interpParamIdx, out var interpSym, out var interpLine);

            slices.Add(CreateSlice(
                expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                interpResolved, interpTemplate, nature, interpProv, ExpressionRole.InterpolatedTemplate, interpParamIdx,
                relativePath, originOverrideLine ?? interpLine, originOverrideSymbol ?? interpSym));
            return;
        }

        if (expr is IdentifierExprSyntax ident)
        {
            var idName = ident.Name;
            if (fnScope != null && fnScope.ParamIndexByName.TryGetValue(idName, out var pIdx))
            {
                var pSignal = fnScope.Params[pIdx];
                var effectiveNature = nature != ParameterNature.Unspecified ? nature : pSignal.Nature;
                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    resolvedValue: null,
                    compositeTemplate: string.Concat(ParamPlaceholderPrefix, pIdx.ToString(), PlaceholderEnd),
                    effectiveNature, ArgumentProvenance.CallerParameter, role, pIdx,
                    relativePath, pSignal.Line, idName));
                return;
            }

            var localInit = Layer3ArgumentExpressionEvaluator.FindLocalVariableInitializer(ident, out var localDefLine);
            if (localInit != null)
            {
                SliceIntoList(
                    localInit, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    nature, role, fnScope, fieldInits, moduleVars, relativePath, projectName, slices,
                    originOverrideSymbol ?? idName, originOverrideLine ?? localDefLine, depth + 1);
                return;
            }

            if (Layer3ArgumentExpressionEvaluator.TryLookupFieldInit(idName, fnScope?.EnclosingTypeName, fieldInits, out var fieldInfo))
            {
                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    fieldInfo.ResolvedValue, compositeTemplate: null, nature, ArgumentProvenance.MemberOrField, role,
                    referencedParamIdx: null, relativePath, originOverrideLine ?? fieldInfo.Line, originOverrideSymbol ?? idName));
                return;
            }

            if (moduleVars.TryGetValue(idName, out var modVar))
            {
                if (modVar.Init != null && modVar.Resolved == null)
                {
                    SliceIntoList(
                        modVar.Init, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                        nature, role, fnScope: null, fieldInits, moduleVars, relativePath, projectName, slices,
                        originOverrideSymbol ?? idName, originOverrideLine ?? modVar.Line, depth + 1);
                    return;
                }

                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    modVar.Resolved, compositeTemplate: null, nature, ArgumentProvenance.InternalConstant, role,
                    referencedParamIdx: null, relativePath, originOverrideLine ?? modVar.Line, originOverrideSymbol ?? idName));
                return;
            }

            if (ConstantRegistry.TryResolve(projectName, idName, out var projectConstVal))
            {
                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    projectConstVal, compositeTemplate: null, nature, ArgumentProvenance.InternalConstant, role,
                    referencedParamIdx: null, relativePath, originOverrideLine, originOverrideSymbol ?? idName));
                return;
            }
        }

        if (Layer3ArgumentExpressionEvaluator.TryDetectConfigOrEnvOrigin(expr, projectName, out var configKey))
        {
            slices.Add(CreateSlice(
                expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                resolvedValue: null, compositeTemplate: null, nature, ArgumentProvenance.ConfigOrEnv, role,
                referencedParamIdx: null, relativePath, originOverrideLine ?? (expr.Span.StartLine + 1), configKey ?? originOverrideSymbol));
            return;
        }

        if (expr is MemberAccessExprSyntax memAccess)
        {
            var memName = memAccess.MemberName;
            if (memAccess.StartsWithSelfOrSuper() &&
                Layer3ArgumentExpressionEvaluator.TryLookupFieldInit(memName, fnScope?.EnclosingTypeName, fieldInits, out var selfField))
            {
                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    selfField.ResolvedValue, compositeTemplate: null, nature, ArgumentProvenance.MemberOrField, role,
                    referencedParamIdx: null, relativePath, originOverrideLine ?? selfField.Line, originOverrideSymbol ?? memName));
                return;
            }

            if (AstValueResolver.TryResolveString(memAccess, projectName, out var resolvedMemVal))
            {
                var prov = memAccess.StartsWithSelfOrSuper()
                    ? ArgumentProvenance.MemberOrField
                    : ArgumentProvenance.InternalConstant;
                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    resolvedMemVal, compositeTemplate: null, nature, prov, role,
                    referencedParamIdx: null, relativePath, originOverrideLine, originOverrideSymbol ?? memAccess.GetCanonicalMemberPath()));
                return;
            }

            if (memAccess.StartsWithSelfOrSuper())
            {
                slices.Add(CreateSlice(
                    expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                    resolvedValue: null, compositeTemplate: null, nature, ArgumentProvenance.MemberOrField, role,
                    referencedParamIdx: null, relativePath, originOverrideLine, originOverrideSymbol ?? memName));
                return;
            }
        }

        if (AstValueResolver.TryResolveString(expr, projectName, out var constVal))
        {
            slices.Add(CreateSlice(
                expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
                constVal, compositeTemplate: null, nature, ArgumentProvenance.InternalConstant, role,
                referencedParamIdx: null, relativePath, originOverrideLine, originOverrideSymbol));
            return;
        }

        var fallbackProv = expr is CallExprSyntax
            ? ArgumentProvenance.ExternalReturn
            : ArgumentProvenance.DynamicComposite;
        var fallbackSym = expr is CallExprSyntax innerCall && !string.IsNullOrEmpty(innerCall.MethodName)
            ? innerCall.MethodName
            : originOverrideSymbol;

        slices.Add(CreateSlice(
            expr, callee, methodName, callLine, callCol, argIdx, propertyKey, slotKey,
            resolvedValue: null, compositeTemplate: null, nature, fallbackProv, role,
            referencedParamIdx: null, relativePath, originOverrideLine, fallbackSym));
    }

    private static void AttachCompositeTemplate(List<SemanticArgumentSlice> slices, int startIndex, string template)
    {
        for (var i = startIndex; i < slices.Count; i++)
        {
            if (!slices[i].ReferencedParamIndex.HasValue) continue;
            var s = slices[i];
            slices[i] = new SemanticArgumentSlice
            {
                Callee = s.Callee,
                MethodName = s.MethodName,
                CallLine = s.CallLine,
                CallColumn = s.CallColumn,
                ArgIndex = s.ArgIndex,
                PropertyKey = s.PropertyKey,
                SlotKey = s.SlotKey,
                Line = s.Line,
                Column = s.Column,
                RawExpression = s.RawExpression,
                ResolvedValue = s.ResolvedValue,
                CompositeTemplate = template,
                Nature = s.Nature,
                Provenance = s.Provenance,
                Role = s.Role,
                ReferencedParamIndex = s.ReferencedParamIndex,
                OriginFile = s.OriginFile,
                OriginLine = s.OriginLine,
                OriginSymbol = s.OriginSymbol
            };
        }
    }

    private static SemanticArgumentSlice CreateSlice(
        ExprSyntax expr,
        string callee,
        string methodName,
        int callLine,
        int callCol,
        int argIdx,
        string? propertyKey,
        string slotKey,
        string? resolvedValue,
        string? compositeTemplate,
        ParameterNature nature,
        ArgumentProvenance provenance,
        ExpressionRole role,
        int? referencedParamIdx,
        string relativePath,
        int? originLine,
        string? originSymbol)
    {
        var rawText = expr.Text.Trim();
        if (rawText.Length > MaxRawExpressionChars)
        {
            rawText = rawText[..MaxRawExpressionChars];
        }

        return new SemanticArgumentSlice
        {
            Callee = callee,
            MethodName = methodName,
            CallLine = callLine,
            CallColumn = callCol,
            ArgIndex = argIdx,
            PropertyKey = propertyKey,
            SlotKey = slotKey,
            Line = expr.Span.StartLine + 1,
            Column = expr.Span.StartColumn + 1,
            RawExpression = rawText,
            ResolvedValue = resolvedValue,
            CompositeTemplate = compositeTemplate,
            Nature = nature,
            Provenance = provenance,
            Role = role,
            ReferencedParamIndex = referencedParamIdx,
            OriginFile = originLine.HasValue || !string.IsNullOrEmpty(originSymbol) ? relativePath : null,
            OriginLine = originLine,
            OriginSymbol = originSymbol
        };
    }
}
