using CodeExplorer.Core.Parser.Syntax;
using CodePassport.Engine.Classification;
using CodePassport.Engine.Models;
using CodePassport.Engine.Semantics;

namespace CodePassport.Engine.Analysis;

/// <summary>
/// True AST &amp; Interprocedural Semantic Capability Analyzer.
/// Zero raw text substring scans: operates strictly on Universal AST declarations,
/// import bindings, lexical scope chains, constant/decode folding, and interprocedural summaries.
/// </summary>
public static class CapabilityScanner
{
    public sealed record FileCapabilityResult(
        string RelativePath,
        bool IsMinifiedOrDist,
        CapabilityFlags TotalMask,
        CapabilityFlags ModuleInitMask,
        IReadOnlyDictionary<string, ExportedSymbolContract> Exports,
        IReadOnlyList<CapabilityEvidence> Evidence,
        IReadOnlyList<SemanticTaintFlow> TaintFlows,
        IReadOnlyList<string> AutonomousTargetLiterals,
        IReadOnlyDictionary<string, SymbolicBinding> ExportedBindings
    );

    private sealed class FunctionSemanticState
    {
        public required string Name { get; init; }
        public required int StartLine { get; init; }
        public required SyntaxNode Syntax { get; init; }
        public Dictionary<string, SymbolicBinding> LocalEnv { get; } = new(StringComparer.Ordinal);
        public CapabilityFlags CapabilityMask { get; set; }
        public CapabilityFlags AutonomousMask { get; set; }
        public CapabilityFlags ReturnTaintMask { get; set; }
        public string? ReturnTaintSourceExpr { get; set; }
        public SymbolicBinding? ReturnedBinding { get; set; }
        public List<ParameterSinkBinding> ParameterFlows { get; } = [];
        public HashSet<string> CalledFunctionNames { get; } = new(StringComparer.Ordinal);
        public List<(string CalleeName, List<SymbolicBinding?> ArgBindings, int Line)> CallSites { get; } = [];
    }

    public static FileCapabilityResult ScanSyntaxTree(
        SyntaxTree syntaxTree,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, SymbolicBinding>>? crossModuleExports = null)
    {
        var relPath = syntaxTree.RelativePath.Replace('\\', '/');
        var artifactRole = syntaxTree.FileNode?.ArtifactRole;
        var isMinified = artifactRole is "dist" or "minified" || IsDistOrMinifiedPath(relPath) || IsLikelyMinifiedFile(syntaxTree);
        var evidence = new List<CapabilityEvidence>();
        var taintFlows = new List<SemanticTaintFlow>();
        var autonomousTargets = new HashSet<string>(StringComparer.Ordinal);
        var moduleEnv = new Dictionary<string, SymbolicBinding>(StringComparer.Ordinal);
        var exportedBindings = new Dictionary<string, SymbolicBinding>(StringComparer.Ordinal);
        var moduleInitMask = CapabilityFlags.None;

        if (isMinified)
        {
            moduleInitMask |= CapabilityFlags.DistBundled;
            AddEvidence(evidence, CapabilityFlags.DistBundled, relPath, 1, "<module_init>", relPath, isMinified, SinkInputProvenance.InternalConstant);
        }

        var doc = syntaxTree.Document;
        if (doc == null)
        {
            return new FileCapabilityResult(
                relPath,
                isMinified,
                moduleInitMask,
                moduleInitMask,
                new Dictionary<string, ExportedSymbolContract>(),
                evidence,
                taintFlows,
                [],
                exportedBindings);
        }

        SyntaxValueEvaluator.EvaluateDocument(doc, syntaxTree.FileNode?.ProjectName);

        // 1. Bind AST ImportDeclSyntax and RawImports into the module lexical environment
        BindModuleImports(syntaxTree, doc, relPath, isMinified, crossModuleExports, moduleEnv, ref moduleInitMask, evidence);

        // 2. Discover all functions declared in the document and index them with formal parameters
        var functionsByName = new Dictionary<string, FunctionSemanticState>(StringComparer.Ordinal);
        foreach (var fnDecl in doc.AllFunctions)
        {
            var fnName = !string.IsNullOrWhiteSpace(fnDecl.Name)
                ? fnDecl.Name
                : (fnDecl.GetChildForField(SyntaxFields.Name) ?? fnDecl.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text
                  ?? $"<anon_{fnDecl.Span.StartLine + 1}>";

            var state = new FunctionSemanticState
            {
                Name = fnName,
                StartLine = fnDecl.Span.StartLine + 1,
                Syntax = fnDecl
            };

            var paramNames = ExtractFunctionParameterNames(fnDecl);
            for (var pIdx = 0; pIdx < paramNames.Count; pIdx++)
            {
                var paramName = paramNames[pIdx];
                if (!string.IsNullOrEmpty(paramName))
                {
                    state.LocalEnv[paramName] = new SymbolicBinding(
                        IntrinsicRole.GlobalObject,
                        CanonicalPath: $"param:{paramName}",
                        SourceParameterIndex: pIdx,
                        SourceParameterName: paramName);
                }
            }

            functionsByName[fnName] = state;
        }

        // 2b. Discover named/exported LambdaExprSyntax functions (`const fn = (...) => ...`, `exports.fn = function(...) {}`, `module.exports = function(...) {}`)
        foreach (var (lambdaName, lambdaNode) in EnumerateNamedLambdaFunctions(doc.RootNode))
        {
            if (functionsByName.ContainsKey(lambdaName)) continue;

            var state = new FunctionSemanticState
            {
                Name = lambdaName,
                StartLine = lambdaNode.Span.StartLine + 1,
                Syntax = lambdaNode
            };

            var paramNames = ExtractLambdaParameterNames(lambdaNode);
            for (var pIdx = 0; pIdx < paramNames.Count; pIdx++)
            {
                var paramName = paramNames[pIdx];
                if (!string.IsNullOrEmpty(paramName))
                {
                    state.LocalEnv[paramName] = new SymbolicBinding(
                        IntrinsicRole.GlobalObject,
                        CanonicalPath: $"param:{paramName}",
                        SourceParameterIndex: pIdx,
                        SourceParameterName: paramName);
                }
            }

            functionsByName[lambdaName] = state;
        }

        // 3. Walk top-level module AST statements (<module_init>)
        WalkModuleRootNode(
            doc.RootNode,
            relPath,
            isMinified,
            moduleEnv,
            functionsByName,
            ref moduleInitMask,
            evidence,
            taintFlows,
            autonomousTargets,
            exportedBindings);

        // 4. Analyze each FunctionDeclSyntax body with full lexical scope + parameter tracking
        foreach (var fnState in functionsByName.Values)
        {
            foreach (var (k, v) in moduleEnv)
            {
                if (v.Role != IntrinsicRole.LocalFunctionRef)
                {
                    fnState.LocalEnv.TryAdd(k, v);
                }
            }

            AnalyzeFunctionBody(
                fnState,
                relPath,
                isMinified,
                functionsByName,
                evidence,
                taintFlows,
                autonomousTargets);

            if (fnState.ReturnedBinding != null)
            {
                moduleEnv[fnState.Name] = fnState.ReturnedBinding with
                {
                    TargetLocalFunctionName = fnState.Name
                };
            }
        }

        // 5. Interprocedural Fixed-Point Propagation across the Call Graph
        PropagateInterproceduralSummaries(
            functionsByName,
            relPath,
            taintFlows,
            autonomousTargets);

        // 6. Propagate capabilities from functions invoked directly during <module_init>
        foreach (var callExpr in doc.AllCalls)
        {
            if (FindEnclosingFunctionDecl(callExpr) == null)
            {
                var target = AstSymbolicEvaluator.ResolveCallTargetBinding(callExpr, moduleEnv);
                var fnName = target?.TargetLocalFunctionName ?? callExpr.MethodName;
                if (!string.IsNullOrEmpty(fnName) && functionsByName.TryGetValue(fnName, out var calledFn))
                {
                    moduleInitMask |= calledFn.CapabilityMask;
                }
            }
        }

        // 7. Build exported symbol contracts and total file mask
        var totalMask = moduleInitMask;
        var exports = new Dictionary<string, ExportedSymbolContract>(StringComparer.Ordinal);
        foreach (var fnState in functionsByName.Values)
        {
            totalMask |= fnState.CapabilityMask;
            exports[fnState.Name] = new ExportedSymbolContract(
                Name: fnState.Name,
                FilePath: relPath,
                Line: fnState.StartLine,
                CapabilityMask: (ulong)fnState.CapabilityMask,
                Capabilities: CapabilityCatalog.FormatCapabilities(fnState.CapabilityMask),
                AutonomousCapabilityMask: (ulong)fnState.AutonomousMask,
                AutonomousCapabilities: CapabilityCatalog.FormatCapabilities(fnState.AutonomousMask),
                ParameterFlows: fnState.ParameterFlows);

            exportedBindings[fnState.Name] = new SymbolicBinding(
                IntrinsicRole.CapabilitySink,
                CanonicalPath: $"{relPath}:{fnState.Name}",
                Capability: fnState.CapabilityMask,
                TaintMask: fnState.ReturnTaintMask,
                TaintSourceExpr: fnState.ReturnTaintSourceExpr,
                TargetLocalFunctionName: $"{relPath}:{fnState.Name}");
        }

        return new FileCapabilityResult(
            relPath,
            isMinified,
            totalMask,
            moduleInitMask,
            exports,
            evidence,
            taintFlows,
            [.. autonomousTargets],
            exportedBindings);
    }

    private static void BindModuleImports(
        SyntaxTree syntaxTree,
        SyntaxDocument doc,
        string relPath,
        bool isMinified,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, SymbolicBinding>>? crossModuleExports,
        Dictionary<string, SymbolicBinding> moduleEnv,
        ref CapabilityFlags moduleInitMask,
        List<CapabilityEvidence> evidence)
    {
        foreach (var imp in doc.Imports)
        {
            if (imp.IsReExport) continue;

            var modPath = imp.ModulePath;
            if (string.IsNullOrWhiteSpace(modPath))
            {
                var srcNode = imp.GetChildForField(SyntaxFields.Source) ??
                              imp.NamedChildren.FirstOrDefault(c => c is LiteralExprSyntax);
                if (srcNode != null)
                {
                    modPath = AstValueResolver.Unquote(srcNode.Text);
                }
            }
            if (string.IsNullOrWhiteSpace(modPath)) continue;

            if (IntrinsicSymbolOntology.TryResolveModule(modPath, out var modBinding))
            {
                moduleInitMask |= modBinding.Capability;
                RecordEvidenceFlags(
                    evidence,
                    modBinding.Capability,
                    relPath,
                    imp.Span.StartLine + 1,
                    "<module_init>",
                    $"import '{modBinding.CanonicalPath}'",
                    isMinified,
                    SinkInputProvenance.InternalConstant);

                if (!string.IsNullOrEmpty(imp.Alias))
                {
                    moduleEnv[imp.Alias] = modBinding;
                }
                if (!string.IsNullOrEmpty(imp.Name))
                {
                    moduleEnv[imp.Name] = modBinding;
                }

                foreach (var spec in imp.ImportedSymbols)
                {
                    var localName = !string.IsNullOrEmpty(spec.Alias) ? spec.Alias : spec.Name;
                    var projected = AstSymbolicEvaluator.ResolvePropertyProjection(modBinding, modBinding.CanonicalPath, spec.Name)
                                    ?? modBinding;
                    moduleEnv[localName] = projected;
                }
            }
            else if (crossModuleExports != null && TryMatchRelativeImport(relPath, modPath, crossModuleExports, out var targetExports))
            {
                foreach (var spec in imp.ImportedSymbols)
                {
                    var localName = !string.IsNullOrEmpty(spec.Alias) ? spec.Alias : spec.Name;
                    if (targetExports.TryGetValue(spec.Name, out var exportedSym))
                    {
                        moduleEnv[localName] = exportedSym;
                    }
                }
            }
        }

        foreach (var rawImp in syntaxTree.RawImports)
        {
            if (IntrinsicSymbolOntology.TryResolveModule(rawImp.Path, out var modBinding))
            {
                moduleInitMask |= modBinding.Capability;
                RecordEvidenceFlags(
                    evidence,
                    modBinding.Capability,
                    relPath,
                    1,
                    "<module_init>",
                    $"import '{modBinding.CanonicalPath}'",
                    isMinified,
                    SinkInputProvenance.InternalConstant);

                var shortModName = modBinding.CanonicalPath.Split('/', '.').Last();
                moduleEnv.TryAdd(shortModName, modBinding);

                if (rawImp.Specifiers != null)
                {
                    foreach (var spec in rawImp.Specifiers)
                    {
                        if (spec.IsDefault || spec.IsNamespace)
                        {
                            moduleEnv[spec.LocalName] = modBinding;
                        }
                        else
                        {
                            var importedProp = spec.ImportedName ?? spec.LocalName;
                            var projected = AstSymbolicEvaluator.ResolvePropertyProjection(modBinding, modBinding.CanonicalPath, importedProp)
                                            ?? modBinding;
                            moduleEnv[spec.LocalName] = projected;
                        }
                    }
                }
            }
            else if (crossModuleExports != null && TryMatchRelativeImport(relPath, rawImp.Path, crossModuleExports, out var targetExports))
            {
                if (rawImp.Specifiers != null)
                {
                    foreach (var spec in rawImp.Specifiers)
                    {
                        var importedName = spec.ImportedName ?? spec.LocalName;
                        if (targetExports.TryGetValue(importedName, out var exportedSym))
                        {
                            moduleEnv[spec.LocalName] = exportedSym;
                        }
                    }
                }
            }
        }
    }

    private static void WalkModuleRootNode(
        SyntaxNode node,
        string relPath,
        bool isMinified,
        Dictionary<string, SymbolicBinding> moduleEnv,
        Dictionary<string, FunctionSemanticState> functionsByName,
        ref CapabilityFlags moduleInitMask,
        List<CapabilityEvidence> evidence,
        List<SemanticTaintFlow> taintFlows,
        HashSet<string> autonomousTargets,
        Dictionary<string, SymbolicBinding> exportedBindings)
    {
        if (node is FunctionDeclSyntax) return;
        if (node is LambdaExprSyntax lambdaNode && !IsImmediatelyInvokedLambda(lambdaNode)) return;
        if (node is AssignmentExprSyntax protoAssign && IsPrototypeFunctionAssignment(protoAssign)) return;

        if (node is VariableDeclSyntax varDecl)
        {
            ProcessVariableDeclaration(
                varDecl,
                "<module_init>",
                relPath,
                isMinified,
                moduleEnv,
                ref moduleInitMask,
                evidence);
        }
        else if (node is AssignmentExprSyntax assignExpr)
        {
            ProcessAssignmentExpression(
                assignExpr,
                "<module_init>",
                relPath,
                isMinified,
                moduleEnv,
                ref moduleInitMask,
                evidence,
                exportedBindings);
        }
        else if (node is CallExprSyntax callExpr)
        {
            var autonomousDummy = CapabilityFlags.None;
            ProcessCallSite(
                callExpr,
                "<module_init>",
                relPath,
                isMinified,
                moduleEnv,
                functionsByName,
                enclosingFunction: null,
                ref moduleInitMask,
                ref autonomousDummy,
                evidence,
                taintFlows,
                autonomousTargets);
        }
        else if (node is MemberAccessExprSyntax or IndexOrSliceExprSyntax)
        {
            if (node.Parent is not (MemberAccessExprSyntax or IndexOrSliceExprSyntax or CallExprSyntax))
            {
                var evaluated = AstSymbolicEvaluator.EvaluateExpression(node, moduleEnv);
                if (evaluated != null && evaluated.Capability != CapabilityFlags.None)
                {
                    moduleInitMask |= evaluated.Capability;
                    RecordEvidenceFlags(
                        evidence,
                        evaluated.Capability,
                        relPath,
                        node.Span.StartLine + 1,
                        "<module_init>",
                        evaluated.CanonicalPath,
                        isMinified,
                        evaluated.IsDecodedFromObfuscation ? SinkInputProvenance.DecodedConstant : SinkInputProvenance.InternalConstant);
                }
            }
        }

        foreach (var child in node.Children)
        {
            WalkModuleRootNode(
                child,
                relPath,
                isMinified,
                moduleEnv,
                functionsByName,
                ref moduleInitMask,
                evidence,
                taintFlows,
                autonomousTargets,
                exportedBindings);
        }
    }

    private static void AnalyzeFunctionBody(
        FunctionSemanticState fnState,
        string relPath,
        bool isMinified,
        Dictionary<string, FunctionSemanticState> functionsByName,
        List<CapabilityEvidence> evidence,
        List<SemanticTaintFlow> taintFlows,
        HashSet<string> autonomousTargets)
    {
        var bodyRoot = fnState.Syntax switch
        {
            FunctionDeclSyntax fnDecl => (SyntaxNode?)fnDecl.Body ?? fnDecl.ExpressionBody ?? fnDecl.GetChildForField(SyntaxFields.Body) ?? fnDecl.Children.FirstOrDefault(c => c is BlockStmtSyntax) ?? fnDecl,
            LambdaExprSyntax lambda => (SyntaxNode?)lambda.Body ?? lambda.GetChildForField(SyntaxFields.Body) ?? lambda.Children.FirstOrDefault(c => c is BlockStmtSyntax) ?? lambda,
            _ => fnState.Syntax
        };

        WalkFunctionSyntax(
            bodyRoot,
            fnState,
            relPath,
            isMinified,
            functionsByName,
            evidence,
            taintFlows,
            autonomousTargets);
    }

    private static void WalkFunctionSyntax(
        SyntaxNode node,
        FunctionSemanticState fnState,
        string relPath,
        bool isMinified,
        Dictionary<string, FunctionSemanticState> functionsByName,
        List<CapabilityEvidence> evidence,
        List<SemanticTaintFlow> taintFlows,
        HashSet<string> autonomousTargets)
    {
        if (node is FunctionDeclSyntax && !ReferenceEquals(node, fnState.Syntax))
        {
            return;
        }

        if (node is VariableDeclSyntax varDecl)
        {
            var mask = fnState.CapabilityMask;
            ProcessVariableDeclaration(
                varDecl,
                fnState.Name,
                relPath,
                isMinified,
                fnState.LocalEnv,
                ref mask,
                evidence);
            fnState.CapabilityMask = mask;
        }
        else if (node is AssignmentExprSyntax assignExpr)
        {
            var mask = fnState.CapabilityMask;
            ProcessAssignmentExpression(
                assignExpr,
                fnState.Name,
                relPath,
                isMinified,
                fnState.LocalEnv,
                ref mask,
                evidence,
                exportedBindings: null);
            fnState.CapabilityMask = mask;
        }
        else if (node is CallExprSyntax callExpr)
        {
            var mask = fnState.CapabilityMask;
            var autoMask = fnState.AutonomousMask;
            ProcessCallSite(
                callExpr,
                fnState.Name,
                relPath,
                isMinified,
                fnState.LocalEnv,
                functionsByName,
                fnState,
                ref mask,
                ref autoMask,
                evidence,
                taintFlows,
                autonomousTargets);
            fnState.CapabilityMask = mask;
            fnState.AutonomousMask = autoMask;
        }
        else if (node is ReturnOrYieldStmtSyntax retStmt)
        {
            var retValNode = retStmt.Value ??
                             retStmt.GetChildForField(SyntaxFields.Value) ??
                             retStmt.NamedChildren.FirstOrDefault();
            if (retValNode != null)
            {
                var retBinding = AstSymbolicEvaluator.EvaluateExpression(retValNode, fnState.LocalEnv);
                if (retBinding != null)
                {
                    fnState.ReturnedBinding ??= retBinding;
                    fnState.CapabilityMask |= retBinding.Capability;
                    if (retBinding.TaintMask != CapabilityFlags.None)
                    {
                        fnState.ReturnTaintMask |= retBinding.TaintMask;
                        fnState.ReturnTaintSourceExpr ??= retBinding.TaintSourceExpr;
                    }
                }
            }
        }
        else if (node is IfStmtSyntax ifStmt)
        {
            var condNode = ifStmt.Condition ??
                           ifStmt.GetChildForField("condition") ??
                           ifStmt.NamedChildren.FirstOrDefault();
            if (condNode != null)
            {
                var condBinding = AstSymbolicEvaluator.EvaluateExpression(condNode, fnState.LocalEnv);
                if (condBinding != null && (condBinding.Capability & CapabilityFlags.EnvCiEvasion) != 0)
                {
                    fnState.CapabilityMask |= CapabilityFlags.EnvCiEvasion;
                    fnState.AutonomousMask |= CapabilityFlags.EnvCiEvasion;
                    RecordEvidenceFlags(
                        evidence,
                        CapabilityFlags.EnvCiEvasion,
                        relPath,
                        ifStmt.Span.StartLine + 1,
                        fnState.Name,
                        $"if ({condBinding.CanonicalPath})",
                        isMinified,
                        SinkInputProvenance.SensitiveSourceTaint);
                }
            }
        }
        else if (node is MemberAccessExprSyntax or IndexOrSliceExprSyntax)
        {
            if (node.Parent is not (MemberAccessExprSyntax or IndexOrSliceExprSyntax or CallExprSyntax or VariableDeclSyntax or AssignmentExprSyntax))
            {
                var evaluated = AstSymbolicEvaluator.EvaluateExpression(node, fnState.LocalEnv);
                if (evaluated != null && evaluated.Capability != CapabilityFlags.None)
                {
                    fnState.CapabilityMask |= evaluated.Capability;
                    RecordEvidenceFlags(
                        evidence,
                        evaluated.Capability,
                        relPath,
                        node.Span.StartLine + 1,
                        fnState.Name,
                        evaluated.CanonicalPath,
                        isMinified,
                        evaluated.IsDecodedFromObfuscation ? SinkInputProvenance.DecodedConstant : SinkInputProvenance.InternalConstant);
                }
            }
        }

        foreach (var child in node.Children)
        {
            WalkFunctionSyntax(
                child,
                fnState,
                relPath,
                isMinified,
                functionsByName,
                evidence,
                taintFlows,
                autonomousTargets);
        }
    }

    private static void ProcessVariableDeclaration(
        VariableDeclSyntax varDecl,
        string enclosingSymbol,
        string relPath,
        bool isMinified,
        Dictionary<string, SymbolicBinding> scopeEnv,
        ref CapabilityFlags currentMask,
        List<CapabilityEvidence> evidence)
    {
        // Only process actual declarators that bind a value (skip outer container `lexical_declaration` if it just wraps `variable_declarator`)
        var initNode = ExtractVariableInitializerNode(varDecl);
        if (initNode == null) return;

        var initBinding = AstSymbolicEvaluator.EvaluateExpression(initNode, scopeEnv);
        if (initBinding == null) return;

        if (initBinding.Capability != CapabilityFlags.None)
        {
            currentMask |= initBinding.Capability;
            var prov = initBinding.IsDecodedFromObfuscation
                ? SinkInputProvenance.DecodedConstant
                : initBinding.TaintMask != CapabilityFlags.None
                    ? SinkInputProvenance.SensitiveSourceTaint
                    : SinkInputProvenance.InternalConstant;

            RecordEvidenceFlags(
                evidence,
                initBinding.Capability,
                relPath,
                varDecl.Span.StartLine + 1,
                enclosingSymbol,
                !string.IsNullOrEmpty(initBinding.CanonicalPath) ? initBinding.CanonicalPath : (initBinding.ResolvedConstant ?? varDecl.Name),
                isMinified,
                prov);
        }

        var lhsNode = varDecl.GetChildForField(SyntaxFields.Name) ??
                      varDecl.GetChildForField(SyntaxFields.Pattern) ??
                      varDecl.GetChildForField(SyntaxFields.Left);

        if (!string.IsNullOrEmpty(varDecl.Name))
        {
            scopeEnv[varDecl.Name] = initBinding;
        }
        else if (lhsNode is IdentifierExprSyntax lhsId && !string.IsNullOrEmpty(lhsId.Text))
        {
            scopeEnv[lhsId.Text.Trim()] = initBinding;
        }

        // Handle object destructuring: const { execSync: runHidden, spawn } = cp
        if (varDecl.Pattern is ObjectDestructuringPatternSyntax objPat)
        {
            foreach (var (key, alias, _) in objPat.Properties)
            {
                var targetVarName = !string.IsNullOrEmpty(alias) ? alias : key;
                BindDestructuredProperty(initBinding, key, targetVarName, varDecl.Span.StartLine + 1, enclosingSymbol, relPath, isMinified, scopeEnv, ref currentMask, evidence);
            }
        }
        else if (lhsNode != null && lhsNode.RawGrammarKind is "object_pattern" or "object_assignment_pattern")
        {
            foreach (var propChild in lhsNode.NamedChildren)
            {
                if (propChild.RawGrammarKind is "pair_pattern" or "property_pattern")
                {
                    var keyNode = propChild.GetChildForField(SyntaxFields.Key) ??
                                  (propChild.NamedChildren.Count > 0 ? propChild.NamedChildren[0] : null);
                    var valNode = propChild.GetChildForField(SyntaxFields.Value) ??
                                  (propChild.NamedChildren.Count > 1 ? propChild.NamedChildren[1] : null);
                    if (keyNode != null && valNode != null)
                    {
                        BindDestructuredProperty(initBinding, keyNode.Text.Trim(), valNode.Text.Trim(), varDecl.Span.StartLine + 1, enclosingSymbol, relPath, isMinified, scopeEnv, ref currentMask, evidence);
                    }
                }
                else if (propChild.RawGrammarKind is "shorthand_property_identifier_pattern" or "identifier")
                {
                    var propName = propChild.Text.Trim();
                    BindDestructuredProperty(initBinding, propName, propName, varDecl.Span.StartLine + 1, enclosingSymbol, relPath, isMinified, scopeEnv, ref currentMask, evidence);
                }
            }
        }
    }

    private static void BindDestructuredProperty(
        SymbolicBinding sourceBinding,
        string propertyKey,
        string targetVarName,
        int line,
        string enclosingSymbol,
        string relPath,
        bool isMinified,
        Dictionary<string, SymbolicBinding> scopeEnv,
        ref CapabilityFlags currentMask,
        List<CapabilityEvidence> evidence)
    {
        var projected = AstSymbolicEvaluator.ResolvePropertyProjection(sourceBinding, sourceBinding.CanonicalPath, propertyKey);
        if (projected != null)
        {
            scopeEnv[targetVarName] = projected;
            if (projected.Capability != CapabilityFlags.None)
            {
                currentMask |= projected.Capability;
                RecordEvidenceFlags(
                    evidence,
                    projected.Capability,
                    relPath,
                    line,
                    enclosingSymbol,
                    $"{targetVarName} <- {projected.CanonicalPath}",
                    isMinified,
                    SinkInputProvenance.InternalConstant);
            }
        }
    }

    private static SyntaxNode? ExtractVariableInitializerNode(VariableDeclSyntax varDecl)
    {
        if (varDecl.Initializer != null) return varDecl.Initializer;

        var valChild = varDecl.GetChildForField(SyntaxFields.Value) ??
                       varDecl.GetChildForField(SyntaxFields.Right);
        if (valChild != null) return valChild;

        // Locate token '=' and return the child immediately following it
        for (var i = 0; i < varDecl.Children.Count - 1; i++)
        {
            if (varDecl.Children[i].RawGrammarKind == "=" || varDecl.Children[i].Text == "=")
            {
                return varDecl.Children[i + 1];
            }
        }

        return null;
    }

    private static void ProcessAssignmentExpression(
        AssignmentExprSyntax assignExpr,
        string enclosingSymbol,
        string relPath,
        bool isMinified,
        Dictionary<string, SymbolicBinding> scopeEnv,
        ref CapabilityFlags currentMask,
        List<CapabilityEvidence> evidence,
        Dictionary<string, SymbolicBinding>? exportedBindings)
    {
        var leftNode = assignExpr.Left ??
                       assignExpr.GetChildForField(SyntaxFields.Left) ??
                       (assignExpr.NamedChildren.Count > 0 ? assignExpr.NamedChildren[0] : null);
        var rightNode = assignExpr.Right ??
                        assignExpr.GetChildForField(SyntaxFields.Right) ??
                        (assignExpr.NamedChildren.Count > 1 ? assignExpr.NamedChildren[1] : null);

        if (leftNode == null || rightNode == null) return;

        var rightBinding = AstSymbolicEvaluator.EvaluateExpression(rightNode, scopeEnv);
        var leftText = leftNode.Text.Trim();

        var isBuiltInPrototypePatch = false;
        var protoIdx = leftText.IndexOf(".prototype.", StringComparison.Ordinal);
        if (protoIdx > 0)
        {
            var targetType = leftText[..protoIdx].Trim();
            isBuiltInPrototypePatch =
                targetType is "Object" or "Array" or "String" or "Number" or "Boolean" or "Function"
                    or "Error" or "Promise" or "RegExp" or "Date" or "Map" or "Set" or "Buffer"
                    or "XMLHttpRequest" or "EventTarget" or "Response" or "Request" or "Headers" ||
                (scopeEnv.TryGetValue(targetType.Split('.')[0], out var protoRecv) &&
                 protoRecv.Role is IntrinsicRole.ModuleNamespace or IntrinsicRole.GlobalObject);
        }

        if (isBuiltInPrototypePatch ||
            leftText.Contains("__proto__", StringComparison.Ordinal) ||
            leftText.StartsWith("require.cache", StringComparison.Ordinal) ||
            leftText.StartsWith("Module._load", StringComparison.Ordinal) ||
            leftText.StartsWith("globalThis.fetch", StringComparison.Ordinal) ||
            leftText.StartsWith("window.fetch", StringComparison.Ordinal) ||
            leftText.StartsWith("XMLHttpRequest.prototype", StringComparison.Ordinal))
        {
            currentMask |= CapabilityFlags.CodeMonkeyPatch;
            RecordEvidenceFlags(
                evidence,
                CapabilityFlags.CodeMonkeyPatch,
                relPath,
                assignExpr.Span.StartLine + 1,
                enclosingSymbol,
                $"{leftText} = ...",
                isMinified,
                SinkInputProvenance.InternalConstant);
        }

        if (rightBinding == null) return;

        if (rightBinding.Capability != CapabilityFlags.None)
        {
            currentMask |= rightBinding.Capability;
        }

        if (leftNode is IdentifierExprSyntax leftId)
        {
            var idName = !string.IsNullOrEmpty(leftId.Name) ? leftId.Name : leftId.Text.Trim();
            if (!string.IsNullOrEmpty(idName))
            {
                scopeEnv[idName] = rightBinding;
            }
        }
        else if (leftNode is IndexOrSliceExprSyntax leftIdx)
        {
            var targetNode = leftIdx.Target ?? leftIdx.GetChildForField(SyntaxFields.Object) ?? leftIdx.NamedChildren.FirstOrDefault();
            if (targetNode is IdentifierExprSyntax targetId)
            {
                var idName = !string.IsNullOrEmpty(targetId.Name) ? targetId.Name : targetId.Text.Trim();
                if (!string.IsNullOrEmpty(idName))
                {
                    scopeEnv[idName] = rightBinding;
                }
            }
        }
        else if (leftNode is MemberAccessExprSyntax leftMem)
        {
            var propNode = leftMem.GetChildForField(SyntaxFields.Property) ?? (leftMem.NamedChildren.Count > 1 ? leftMem.NamedChildren[^1] : null);
            var propName = !string.IsNullOrEmpty(leftMem.MemberName) ? leftMem.MemberName : propNode?.Text.Trim();
            if (!string.IsNullOrEmpty(propName) && rightBinding.Role == IntrinsicRole.ModuleNamespace)
            {
                scopeEnv[$"prop:{propName}"] = rightBinding;
            }
        }

        if (exportedBindings != null &&
            (leftText.StartsWith("exports.", StringComparison.Ordinal) ||
             leftText.StartsWith("module.exports.", StringComparison.Ordinal)))
        {
            var exportedName = leftText.Split('.').Last();
            exportedBindings[exportedName] = rightBinding;
        }
    }

    private static void ProcessCallSite(
        CallExprSyntax callExpr,
        string enclosingSymbol,
        string relPath,
        bool isMinified,
        Dictionary<string, SymbolicBinding> scopeEnv,
        Dictionary<string, FunctionSemanticState> functionsByName,
        FunctionSemanticState? enclosingFunction,
        ref CapabilityFlags currentMask,
        ref CapabilityFlags autonomousMask,
        List<CapabilityEvidence> evidence,
        List<SemanticTaintFlow> taintFlows,
        HashSet<string> autonomousTargets)
    {
        var line = callExpr.Span.StartLine + 1;
        var args = AstSymbolicEvaluator.GetCallArguments(callExpr);

        var argBindings = new List<SymbolicBinding?>(args.Count);
        foreach (var arg in args)
        {
            var argBinding = AstSymbolicEvaluator.EvaluateExpression(arg, scopeEnv);
            argBindings.Add(argBinding);
            if (argBinding != null && argBinding.Capability != CapabilityFlags.None)
            {
                currentMask |= argBinding.Capability;
                var argProv = argBinding.IsDecodedFromObfuscation
                    ? SinkInputProvenance.DecodedConstant
                    : argBinding.SourceParameterIndex != null
                        ? SinkInputProvenance.CallerParameter
                        : argBinding.TaintMask != CapabilityFlags.None
                            ? SinkInputProvenance.SensitiveSourceTaint
                            : SinkInputProvenance.InternalConstant;

                if (argProv != SinkInputProvenance.CallerParameter)
                {
                    autonomousMask |= argBinding.Capability;
                }

                RecordEvidenceFlags(
                    evidence,
                    argBinding.Capability,
                    relPath,
                    line,
                    enclosingSymbol,
                    argBinding.ResolvedConstant ?? argBinding.CanonicalPath,
                    isMinified,
                    argProv);
            }
        }

        var localCalleeName = callExpr.MethodName;
        var targetBinding = (!string.IsNullOrEmpty(localCalleeName) &&
                             callExpr.Receiver == null &&
                             !scopeEnv.ContainsKey(localCalleeName) &&
                             functionsByName.ContainsKey(localCalleeName))
            ? new SymbolicBinding(IntrinsicRole.LocalFunctionRef, localCalleeName, TargetLocalFunctionName: localCalleeName)
            : AstSymbolicEvaluator.ResolveCallTargetBinding(callExpr, scopeEnv);

        // Benign standard global-this resolution idiom: `Function("return this")()`
        if (targetBinding?.CanonicalPath == "Function" &&
            argBindings.Count == 1 &&
            argBindings[0]?.ResolvedConstant != null &&
            argBindings[0]!.ResolvedConstant!.Trim() is "return this" or "return this;" &&
            !targetBinding.IsDecodedFromObfuscation &&
            !argBindings[0]!.IsDecodedFromObfuscation)
        {
            return;
        }

        var wholeCallBinding = targetBinding?.TargetLocalFunctionName != null
            ? null
            : AstSymbolicEvaluator.EvaluateExpression(callExpr, scopeEnv);
        if (wholeCallBinding != null && wholeCallBinding.Capability != CapabilityFlags.None)
        {
            currentMask |= wholeCallBinding.Capability;
            var prov = wholeCallBinding.IsDecodedFromObfuscation
                ? SinkInputProvenance.DecodedConstant
                : wholeCallBinding.TaintMask != CapabilityFlags.None
                    ? SinkInputProvenance.SensitiveSourceTaint
                    : SinkInputProvenance.InternalConstant;

            RecordEvidenceFlags(
                evidence,
                wholeCallBinding.Capability,
                relPath,
                line,
                enclosingSymbol,
                wholeCallBinding.CanonicalPath,
                isMinified,
                prov);
        }

        if (targetBinding == null)
        {
            var localName = callExpr.MethodName;
            if (!string.IsNullOrEmpty(localName) && functionsByName.ContainsKey(localName))
            {
                enclosingFunction?.CalledFunctionNames.Add(localName);
                enclosingFunction?.CallSites.Add((localName, argBindings, line));
            }
            return;
        }

        if (targetBinding.TargetLocalFunctionName != null &&
            functionsByName.ContainsKey(targetBinding.TargetLocalFunctionName))
        {
            enclosingFunction?.CalledFunctionNames.Add(targetBinding.TargetLocalFunctionName);
            enclosingFunction?.CallSites.Add((targetBinding.TargetLocalFunctionName, argBindings, line));
        }

        if (targetBinding.Capability == CapabilityFlags.None) return;

        // Benign standard global-this resolution idiom: `Function("return this")()`
        if (targetBinding.CanonicalPath == "Function" &&
            argBindings.Count == 1 &&
            argBindings[0]?.ResolvedConstant != null &&
            argBindings[0]!.ResolvedConstant!.Trim() is "return this" or "return this;" &&
            !targetBinding.IsDecodedFromObfuscation &&
            !argBindings[0]!.IsDecodedFromObfuscation)
        {
            return;
        }

        currentMask |= targetBinding.Capability;
        if (targetBinding.IsDecodedFromObfuscation)
        {
            currentMask |= CapabilityFlags.CryptoObfuscated;
            autonomousMask |= CapabilityFlags.CryptoObfuscated;
        }

        var sinkProvenance = SinkInputProvenance.Unknown;
        string? sinkTargetDetail = targetBinding.CanonicalPath;

        foreach (var argB in argBindings)
        {
            if (argB == null) continue;

            if (argB.SourceParameterIndex is { } paramIdx && argB.SourceParameterName is { } paramName)
            {
                if (sinkProvenance == SinkInputProvenance.Unknown)
                {
                    sinkProvenance = SinkInputProvenance.CallerParameter;
                }
                enclosingFunction?.ParameterFlows.Add(new ParameterSinkBinding(
                    paramName,
                    paramIdx,
                    CapabilityCatalog.GetCanonicalId(PrimaryFlag(targetBinding.Capability)),
                    targetBinding.CanonicalPath));
            }

            if (argB.TaintMask != CapabilityFlags.None)
            {
                // Passing `env: process.env` (ENV.CONFIG / ENV.DUMP_ALL) into a local child process spawn (PROC.SPAWN)
                // or reading a path from an ENV.CONFIG variable (FS.READ) is standard OS configuration, not exfiltration
                var isStandardProcessEnvInheritance =
                    (argB.TaintMask & ~(CapabilityFlags.EnvConfig | CapabilityFlags.EnvDumpAll)) == CapabilityFlags.None &&
                    ((targetBinding.Capability & CapabilityFlags.ProcSpawn) != 0 ||
                     (argB.TaintMask == CapabilityFlags.EnvConfig && targetBinding.Capability == CapabilityFlags.FsRead)) &&
                    (targetBinding.Capability & (CapabilityFlags.NetHttpClient | CapabilityFlags.NetWebSocket | CapabilityFlags.NetRawIp | CapabilityFlags.NetExfilHost | CapabilityFlags.DomBeacon)) == 0;

                if (!isStandardProcessEnvInheritance && IsActiveExfiltrationSink(targetBinding.Capability))
                {
                    sinkProvenance = SinkInputProvenance.SensitiveSourceTaint;
                    autonomousMask |= targetBinding.Capability | argB.TaintMask;

                    var srcCapId = CapabilityCatalog.GetCanonicalId(PrimaryFlag(argB.TaintMask));
                    var sinkCapId = CapabilityCatalog.GetCanonicalId(PrimaryFlag(targetBinding.Capability));
                    taintFlows.Add(new SemanticTaintFlow(
                        SourceCapability: srcCapId,
                        SourceExpression: argB.TaintSourceExpr ?? "tainted_source",
                        SinkCapability: sinkCapId,
                        SinkExpression: targetBinding.CanonicalPath,
                        FilePath: relPath,
                        Line: line,
                        EnclosingSymbol: enclosingSymbol));
                }
            }

            if (!string.IsNullOrEmpty(argB.ResolvedConstant))
            {
                sinkProvenance = argB.IsDecodedFromObfuscation
                    ? SinkInputProvenance.DecodedConstant
                    : SinkInputProvenance.InternalConstant;
                autonomousMask |= targetBinding.Capability;
                sinkTargetDetail = $"{targetBinding.CanonicalPath}('{Truncate(argB.ResolvedConstant, 64)}')";

                if (IsSignificantAutonomousTarget(targetBinding.Capability, argB.ResolvedConstant))
                {
                    autonomousTargets.Add($"{targetBinding.CanonicalPath} -> {argB.ResolvedConstant}");
                }
            }
        }

        if (sinkProvenance != SinkInputProvenance.CallerParameter)
        {
            autonomousMask |= targetBinding.Capability;
        }

        RecordEvidenceFlags(
            evidence,
            targetBinding.Capability,
            relPath,
            line,
            enclosingSymbol,
            sinkTargetDetail,
            isMinified,
            sinkProvenance);
    }

    private static void PropagateInterproceduralSummaries(
        Dictionary<string, FunctionSemanticState> functionsByName,
        string relPath,
        List<SemanticTaintFlow> taintFlows,
        HashSet<string> autonomousTargets)
    {
        var changed = true;
        var iterations = 0;
        while (changed && iterations++ < 32)
        {
            changed = false;
            foreach (var caller in functionsByName.Values)
            {
                var prevMask = caller.CapabilityMask;
                var prevAuto = caller.AutonomousMask;
                var prevRetTaint = caller.ReturnTaintMask;

                foreach (var (calleeName, argBindings, callLine) in caller.CallSites)
                {
                    if (!functionsByName.TryGetValue(calleeName, out var callee)) continue;

                    caller.CapabilityMask |= callee.CapabilityMask;
                    caller.AutonomousMask |= callee.AutonomousMask;

                    foreach (var paramFlow in callee.ParameterFlows)
                    {
                        if (paramFlow.ParameterIndex < argBindings.Count)
                        {
                            var passedArg = argBindings[paramFlow.ParameterIndex];
                            if (passedArg == null) continue;

                            if (passedArg.TaintMask != CapabilityFlags.None)
                            {
                                // Passing `env: process.env` (ENV.CONFIG / ENV.DUMP_ALL) into a local child process spawn (PROC.SPAWN) is standard OS environment inheritance
                                var isStandardProcEnv =
                                    (passedArg.TaintMask & ~(CapabilityFlags.EnvConfig | CapabilityFlags.EnvDumpAll)) == CapabilityFlags.None &&
                                    (paramFlow.SinkCapability == "PROC.SPAWN" ||
                                     (passedArg.TaintMask == CapabilityFlags.EnvConfig && paramFlow.SinkCapability == "FS.READ"));

                                if (!isStandardProcEnv && IsActiveExfiltrationSinkId(paramFlow.SinkCapability))
                                {
                                    caller.AutonomousMask |= callee.CapabilityMask | passedArg.TaintMask;
                                    var srcCapId = CapabilityCatalog.GetCanonicalId(PrimaryFlag(passedArg.TaintMask));
                                    var flow = new SemanticTaintFlow(
                                        SourceCapability: srcCapId,
                                        SourceExpression: passedArg.TaintSourceExpr ?? "tainted_arg",
                                        SinkCapability: paramFlow.SinkCapability,
                                        SinkExpression: $"{calleeName} -> {paramFlow.SinkTarget}",
                                        FilePath: relPath,
                                        Line: callLine,
                                        EnclosingSymbol: caller.Name);

                                    if (!taintFlows.Any(t =>
                                            t.Line == flow.Line &&
                                            t.SourceCapability == flow.SourceCapability &&
                                            t.SinkCapability == flow.SinkCapability))
                                    {
                                        taintFlows.Add(flow);
                                    }
                                }
                            }
                            else if (passedArg.SourceParameterIndex is { } callerParamIdx &&
                                     passedArg.SourceParameterName is { } callerParamName)
                            {
                                if (!caller.ParameterFlows.Any(pf =>
                                        pf.ParameterIndex == callerParamIdx &&
                                        pf.SinkCapability == paramFlow.SinkCapability))
                                {
                                    caller.ParameterFlows.Add(new ParameterSinkBinding(
                                        callerParamName,
                                        callerParamIdx,
                                        paramFlow.SinkCapability,
                                        $"{calleeName} -> {paramFlow.SinkTarget}"));
                                    changed = true;
                                }
                            }
                            else if (!string.IsNullOrEmpty(passedArg.ResolvedConstant))
                            {
                                caller.AutonomousMask |= callee.CapabilityMask;
                                var autoTarget = $"{calleeName} -> {paramFlow.SinkTarget}('{Truncate(passedArg.ResolvedConstant, 64)}')";
                                autonomousTargets.Add(autoTarget);
                            }
                        }
                    }
                }

                if (caller.CapabilityMask != prevMask ||
                    caller.AutonomousMask != prevAuto ||
                    caller.ReturnTaintMask != prevRetTaint)
                {
                    changed = true;
                }
            }
        }
    }

    private static List<string> ExtractFunctionParameterNames(FunctionDeclSyntax fnDecl)
    {
        var names = new List<string>();
        if (fnDecl.Parameters.Count > 0)
        {
            foreach (var p in fnDecl.Parameters)
            {
                names.Add(ExtractSingleParameterName(p));
            }
            return names;
        }

        var paramsContainer = fnDecl.GetChildForField(SyntaxFields.Parameters) ??
                              fnDecl.Children.FirstOrDefault(c => c.RawGrammarKind is "formal_parameters" or "parameter_list" or "parameters");
        if (paramsContainer != null)
        {
            foreach (var child in paramsContainer.NamedChildren)
            {
                if (child is ParameterDeclSyntax paramDecl)
                {
                    names.Add(ExtractSingleParameterName(paramDecl));
                }
                else if (child is IdentifierExprSyntax idExpr)
                {
                    names.Add(!string.IsNullOrEmpty(idExpr.Name) ? idExpr.Name : idExpr.Text.Trim());
                }
            }
        }

        return names;
    }

    private static string ExtractSingleParameterName(ParameterDeclSyntax param)
    {
        if (!string.IsNullOrEmpty(param.Name)) return param.Name;
        var pat = param.GetChildForField(SyntaxFields.Pattern) ??
                  param.GetChildForField(SyntaxFields.Name) ??
                  param.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax);
        if (pat != null) return pat.Text.Trim();
        return param.Text.Split(':', '=').First().Trim();
    }

    private static bool TryMatchRelativeImport(
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

    private static CapabilityFlags PrimaryFlag(CapabilityFlags mask)
    {
        foreach (var desc in CapabilityCatalog.Descriptors)
        {
            if ((mask & desc.Flag) != 0) return desc.Flag;
        }
        return CapabilityFlags.None;
    }

    private static bool IsSignificantAutonomousTarget(CapabilityFlags sinkMask, string constantVal)
    {
        const CapabilityFlags trackedSinks =
            CapabilityFlags.NetHttpClient |
            CapabilityFlags.NetWebSocket |
            CapabilityFlags.NetRawIp |
            CapabilityFlags.NetExfilHost |
            CapabilityFlags.NetDnsRaw |
            CapabilityFlags.ProcSpawn |
            CapabilityFlags.ProcShellDropper |
            CapabilityFlags.FsSensitivePath |
            CapabilityFlags.CodeEval;

        return (sinkMask & trackedSinks) != 0 && constantVal.Length >= 4;
    }

    public static bool IsDistOrMinifiedPath(string relativePath)
    {
        var norm = relativePath.Replace('\\', '/').ToLowerInvariant();
        if (norm.EndsWith(".min.js", StringComparison.Ordinal) ||
            norm.EndsWith(".bundle.js", StringComparison.Ordinal) ||
            norm.EndsWith(".umd.js", StringComparison.Ordinal) ||
            norm.EndsWith(".cjs.js", StringComparison.Ordinal) ||
            norm.EndsWith(".esm.js", StringComparison.Ordinal))
        {
            return true;
        }

        return norm.StartsWith("dist/", StringComparison.Ordinal) ||
               norm.Contains("/dist/", StringComparison.Ordinal) ||
               norm.StartsWith("build/", StringComparison.Ordinal) ||
               norm.Contains("/build/", StringComparison.Ordinal) ||
               norm.StartsWith("bundle/", StringComparison.Ordinal) ||
               norm.Contains("/bundle/", StringComparison.Ordinal);
    }

    private static bool IsLikelyMinifiedFile(SyntaxTree tree)
    {
        var text = tree.Document?.SourceText;
        if (string.IsNullOrEmpty(text) || text.Length < 500) return false;
        var lineCount = tree.FileNode.LineCount ?? (tree.Document != null ? tree.Document.Span.EndLine + 1 : 1);
        var avgLineLength = text.Length / Math.Max(1, lineCount);
        return avgLineLength > 240;
    }

    private static void RecordEvidenceFlags(
        List<CapabilityEvidence> evidence,
        CapabilityFlags mask,
        string filePath,
        int line,
        string? symbolName,
        string? targetExpression,
        bool isMinified,
        SinkInputProvenance provenance)
    {
        foreach (var desc in CapabilityCatalog.Descriptors)
        {
            if ((mask & desc.Flag) != 0)
            {
                AddEvidence(evidence, desc.Flag, filePath, line, symbolName, targetExpression, isMinified, provenance);
            }
        }
    }

    private static void AddEvidence(
        List<CapabilityEvidence> evidence,
        CapabilityFlags singleFlag,
        string filePath,
        int line,
        string? symbolName,
        string? targetExpression,
        bool isMinified,
        SinkInputProvenance provenance)
    {
        var capId = CapabilityCatalog.GetCanonicalId(singleFlag);
        var dimCode = CapabilityCatalog.GetDimensionCode(singleFlag);

        if (evidence.Any(e => e.Capability == capId && e.FilePath == filePath && e.Line == line))
        {
            return;
        }

        evidence.Add(new CapabilityEvidence(
            capId,
            dimCode,
            filePath,
            line,
            symbolName,
            targetExpression,
            isMinified,
            provenance));
    }

    private static string Truncate(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen] + "...";

    private static bool IsPrototypeFunctionAssignment(AssignmentExprSyntax assignExpr)
    {
        var leftNode = assignExpr.Left ?? assignExpr.GetChildForField(SyntaxFields.Left) ?? (assignExpr.NamedChildren.Count > 0 ? assignExpr.NamedChildren[0] : null);
        var rightNode = assignExpr.Right ?? assignExpr.GetChildForField(SyntaxFields.Right) ?? (assignExpr.NamedChildren.Count > 1 ? assignExpr.NamedChildren[1] : null);
        return leftNode is MemberAccessExprSyntax &&
               leftNode.Text.Contains(".prototype.", StringComparison.Ordinal) &&
               rightNode is LambdaExprSyntax;
    }

    private static bool IsImmediatelyInvokedLambda(LambdaExprSyntax lambda)
    {
        var p = lambda.Parent;
        while (p != null && p.RawGrammarKind is "parenthesized_expression" or "non_null_expression")
        {
            p = p.Parent;
        }
        if (p is CallExprSyntax call)
        {
            var callee = call.Callee ?? call.GetChildForField(SyntaxFields.Function);
            while (callee != null && callee.RawGrammarKind is "parenthesized_expression" or "non_null_expression")
            {
                callee = callee.NamedChildren.FirstOrDefault();
            }
            return ReferenceEquals(callee, lambda);
        }
        return false;
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
                var init = ExtractVariableInitializerNode(varDecl);
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

    private static List<string> ExtractLambdaParameterNames(LambdaExprSyntax lambda)
    {
        var names = new List<string>();
        var paramsContainer = lambda.GetChildForField(SyntaxFields.Parameters) ??
                              lambda.Children.FirstOrDefault(c => c.RawGrammarKind is "formal_parameters" or "parameter_list" or "parameters");
        if (paramsContainer != null)
        {
            foreach (var child in paramsContainer.NamedChildren)
            {
                if (child is ParameterDeclSyntax paramDecl)
                {
                    names.Add(ExtractSingleParameterName(paramDecl));
                }
                else if (child is IdentifierExprSyntax idExpr)
                {
                    names.Add(!string.IsNullOrEmpty(idExpr.Name) ? idExpr.Name : idExpr.Text.Trim());
                }
            }
        }
        else
        {
            var singleParam = lambda.GetChildForField("parameter") ??
                              lambda.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax);
            if (singleParam != null)
            {
                names.Add(singleParam.Text.Trim());
            }
        }
        return names;
    }

    private static bool IsActiveExfiltrationSink(CapabilityFlags cap)
    {
        const CapabilityFlags exfilMask =
            CapabilityFlags.NetHttpClient |
            CapabilityFlags.NetWebSocket |
            CapabilityFlags.NetRawIp |
            CapabilityFlags.NetExfilHost |
            CapabilityFlags.NetDnsRaw |
            CapabilityFlags.ProcSpawn |
            CapabilityFlags.ProcShellDropper |
            CapabilityFlags.CodeEval |
            CapabilityFlags.CodeVm |
            CapabilityFlags.DomBeacon;

        return (PrimaryFlag(cap) & exfilMask) != 0;
    }

    private static bool IsActiveExfiltrationSinkId(string sinkCapId)
    {
        return sinkCapId is "NET.HTTP_CLIENT" or "NET.WEBSOCKET" or "NET.RAW_IP" or "NET.EXFIL_HOST" or "NET.DNS_RAW"
            or "PROC.SPAWN" or "PROC.SHELL_DROPPER" or "CODE.EVAL" or "CODE.VM" or "DOM.BEACON";
    }
}
