using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private static Relationship CreateCallRel(Reference refItem, string targetId) =>
        Relationship.CreateWithCallSite(
            refItem.ScopeSymbolId,
            targetId,
            CodeExplorer.Cypher.Common.RelationshipKind.Calls,
            refItem.Line,
            refItem.Column,
            refItem.Context,
            refItem.ArgFlow);

    private static void ResolveCallsReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        ImportLookups imports,
        Dictionary<string, List<string>> interfaceToImplementors,
        List<Relationship> referenceRelationships)
    {
        var targetName = refItem.TargetName;
        string? targetTypeName = null;
        string? methodName = null;
        var isExternalStaticClass = false;
        ctx.SymbolScopes.TryGetValue(refItem.ScopeSymbolId, out var scopeForFile);
        var callerFile = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId)
                         ?? (!string.IsNullOrEmpty(scopeForFile.FilePath) ? scopeForFile.FilePath : null)
                         ?? (!string.IsNullOrEmpty(refItem.ArgFlow) && (refItem.ArgFlow.Contains('/') || refItem.ArgFlow.Contains('\\')) ? refItem.ArgFlow.Replace('\\', '/') : null);
        var normCallerFile = Layer5SymbolVisibilityResolver.NormalizeWorkspaceRelativeFilePath(callerFile, ctx);
        var hasExplicitReceiver = refItem.HasExplicitReceiver;

        if (hasExplicitReceiver)
        {
            var varName = refItem.ReceiverName!;
            methodName = refItem.MemberName;

            var encType = scopeForFile.EnclosingTypeName;
            if (!string.IsNullOrEmpty(encType) && !string.IsNullOrEmpty(methodName))
            {
                if ((normCallerFile != null && symbols.MembersByType.TryGetValue((normCallerFile, encType, varName), out var memberId)) ||
                    symbols.MembersByType.TryGetValue(("", encType, varName), out memberId))
                {
                    var accessedRel = Relationship.FromRelationship(new AccessedByRelationship(memberId, refItem.ScopeSymbolId)
                    {
                        Operation = methodName,
                        FilePath = normCallerFile
                    });
                    accessedRel.Line = refItem.Line;
                    accessedRel.Column = refItem.Column;
                    referenceRelationships.Add(accessedRel);
                }
            }

            // Check if varName is a namespace or default/CommonJS module import in callerFile
            if (normCallerFile != null &&
                imports.FileImports.TryGetValue((normCallerFile, varName), out var nsImport) &&
                (nsImport.IsNamespace || nsImport.IsDefault))
            {
                var nsCandidates = ResolveImportCandidateFiles(normCallerFile, nsImport.ImportPath, imports, ctx, symbols);
                foreach (var nsCand in nsCandidates)
                {
                    var nsTargetId = TryResolveFunctionInCandidateFile(nsCand, methodName, symbols, imports, ctx);
                    if (nsTargetId != null)
                    {
                        referenceRelationships.Add(CreateCallRel(refItem, nsTargetId));
                        return;
                    }
                }
                if (nsImport.IsNamespace)
                {
                    return;
                }
            }

            if (normCallerFile != null &&
                bindings.BindingsLookup.TryGetValue((normCallerFile, varName), out var candidates))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (!string.IsNullOrEmpty(candidate.ScopeMarker) && !string.IsNullOrEmpty(candidate.ScopeId) &&
                        (refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal) ||
                         refItem.ScopeSymbolId.Contains($":{candidate.ScopeId}:", StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.EndsWith($":{candidate.ScopeId}", StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                         (normCallerFile != null && (normCallerFile.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) || normCallerFile.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase))) ||
                         (scopeForFile.EnclosingTypeName != null && string.Equals(scopeForFile.EnclosingTypeName, candidate.ScopeId, StringComparison.OrdinalIgnoreCase))))
                    {
                        targetTypeName = candidate.TypeName;
                        break;
                    }
                }
                targetTypeName ??= candidates[0].TypeName;
            }
            else if (bindings.BindingsLookup.TryGetValue(("", varName), out var emptyFileCandidates))
            {
                targetTypeName = emptyFileCandidates[0].TypeName;
            }
            else if (bindings.GlobalBindingsLookup.TryGetValue(varName, out var globalCandidates))
            {
                foreach (var candidate in globalCandidates)
                {
                    if (!string.IsNullOrEmpty(candidate.ScopeMarker) && !string.IsNullOrEmpty(candidate.ScopeId) &&
                        (refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal) ||
                         refItem.ScopeSymbolId.Contains($":{candidate.ScopeId}:", StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.EndsWith($":{candidate.ScopeId}", StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                         refItem.ScopeSymbolId.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                         (normCallerFile != null && (normCallerFile.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) || normCallerFile.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase))) ||
                         (scopeForFile.EnclosingTypeName != null &&
                          (string.Equals(scopeForFile.EnclosingTypeName, candidate.ScopeId, StringComparison.OrdinalIgnoreCase) ||
                           Layer5SymbolVisibilityResolver.DoesTypeInheritFrom(scopeForFile.EnclosingTypeName, candidate.ScopeId, symbols)))))
                    {
                        targetTypeName = candidate.TypeName;
                        if (!string.IsNullOrEmpty(methodName) &&
                            !string.Equals(scopeForFile.EnclosingTypeName, candidate.ScopeId, StringComparison.Ordinal) &&
                            ((!string.IsNullOrEmpty(candidate.FilePath) && symbols.MembersByType.TryGetValue((candidate.FilePath, candidate.ScopeId, varName), out var inhMemberId)) ||
                             symbols.MembersByType.TryGetValue(("", candidate.ScopeId, varName), out inhMemberId)))
                        {
                            var accessedRel = Relationship.FromRelationship(new AccessedByRelationship(inhMemberId, refItem.ScopeSymbolId)
                            {
                                Operation = methodName,
                                FilePath = normCallerFile
                            });
                            accessedRel.Line = refItem.Line;
                            accessedRel.Column = refItem.Column;
                            referenceRelationships.Add(accessedRel);
                        }
                        break;
                    }
                }

                if (targetTypeName == null && (normCallerFile == null || string.IsNullOrEmpty(globalCandidates[0].FilePath)))
                {
                    targetTypeName = globalCandidates[0].TypeName;
                }
            }

            if (targetTypeName != null)
            {
                targetName = $"{targetTypeName}.{methodName}";
            }
            else if (symbols.TypeSymbols.ContainsKey(varName))
            {
                targetTypeName = varName;
                targetName = $"{varName}.{methodName}";
            }
            else if (!string.IsNullOrEmpty(varName) && char.IsUpper(varName[0]))
            {
                targetName = $"{varName}.{methodName}";
                isExternalStaticClass = true;
            }
            else
            {
                // Explicit receiver has no known type binding in workspace; do not strip receiver and match unrelated functions
                return;
            }
        }
        else if (!string.Equals(refItem.MemberName, targetName, StringComparison.Ordinal))
        {
            methodName = refItem.MemberName;
            targetName = methodName;
        }

        var callAdded = false;
        string? targetNodeId = null;
        var isCallbackArg = string.Equals(refItem.ArgFlow, "callback_arg", StringComparison.Ordinal);
        var callerScope = Layer5SymbolVisibilityResolver.GetOrInferSymbolScope(refItem.ScopeSymbolId, normCallerFile, ctx);

        // 0. If unqualified call inside an enclosing type, prefer same-type method first
        if (!hasExplicitReceiver && !targetName.Contains('.') && !string.IsNullOrEmpty(callerScope.EnclosingTypeName))
        {
            var qualifiedInType = $"{callerScope.EnclosingTypeName}.{targetName}";
            if (normCallerFile != null &&
                (ctx.FileScopedSymbols.TryGetValue((normCallerFile, OntologyConstants.NodeLabels.Function, qualifiedInType), out var enclosedLocalId) ||
                 symbols.FunctionSymbolsByFile.TryGetValue((normCallerFile, qualifiedInType), out enclosedLocalId)))
            {
                if (!isCallbackArg || enclosedLocalId != refItem.ScopeSymbolId)
                {
                    referenceRelationships.Add(CreateCallRel(refItem, enclosedLocalId));
                    callAdded = true;
                    targetNodeId = enclosedLocalId;
                }
            }
            else if (!isCallbackArg && normCallerFile != null &&
                     (enclosedLocalId = ResolveSameProjectFunctionFallback(normCallerFile, qualifiedInType, refItem.ScopeSymbolId, hasExplicitReceiver, targetTypeName, symbols, ctx)) != null)
            {
                referenceRelationships.Add(CreateCallRel(refItem, enclosedLocalId));
                callAdded = true;
                targetNodeId = enclosedLocalId;
            }
        }

        // 1. Same file
        if (!callAdded && normCallerFile != null &&
            ctx.FileScopedSymbols.TryGetValue((normCallerFile, OntologyConstants.NodeLabels.Function, targetName), out var localTargetId) &&
            Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(refItem.ScopeSymbolId, normCallerFile, localTargetId, hasExplicitReceiver, targetTypeName, symbols, ctx))
        {
            if (!isCallbackArg || localTargetId != refItem.ScopeSymbolId)
            {
                referenceRelationships.Add(CreateCallRel(refItem, localTargetId));
                callAdded = true;
                targetNodeId = localTargetId;
            }
        }
        else if (!callAdded && normCallerFile != null &&
                 symbols.FunctionSymbolsByFile.TryGetValue((normCallerFile, targetName), out localTargetId) &&
                 Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(refItem.ScopeSymbolId, normCallerFile, localTargetId, hasExplicitReceiver, targetTypeName, symbols, ctx))
        {
            if (!isCallbackArg || localTargetId != refItem.ScopeSymbolId)
            {
                referenceRelationships.Add(CreateCallRel(refItem, localTargetId));
                callAdded = true;
                targetNodeId = localTargetId;
            }
        }
        // 2. Through explicit module imports (e.g. import { pluralize } from './plural' or from '@repro/text')
        else if (!callAdded && normCallerFile != null && (targetNodeId = ResolveFunctionCallThroughImports(normCallerFile, targetName, symbols, imports, ctx)) != null)
        {
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 3. Same project fallback (when function is defined in caller's project and visible; skipped for callback_arg)
        else if (!callAdded && !isCallbackArg && normCallerFile != null &&
                 (targetNodeId = ResolveSameProjectFunctionFallback(normCallerFile, targetName, refItem.ScopeSymbolId, hasExplicitReceiver, targetTypeName, symbols, ctx)) != null)
        {
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 4. Globally unambiguous visible function across reachable workspace projects
        else if (!callAdded && !isCallbackArg &&
                 symbols.FunctionsByName.TryGetValue(targetName, out var uniqueCandidates) &&
                 Layer5SymbolVisibilityResolver.SelectVisibleSymbolCandidate(uniqueCandidates, refItem.ScopeSymbolId, normCallerFile, hasExplicitReceiver, targetTypeName, symbols, ctx) is { } candidateId)
        {
            targetNodeId = candidateId;
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 5. Procedure symbols fallback
        else if (!callAdded && !isCallbackArg &&
                 !IsFileNodeScope(refItem.ScopeSymbolId) &&
                 symbols.ProcedureSymbols.TryGetValue(targetName, out var targetProcId))
        {
            referenceRelationships.Add(Relationship.CreateWithCallSite(
                targetProcId,
                refItem.ScopeSymbolId,
                CodeExplorer.Cypher.Common.RelationshipKind.CalledBy,
                refItem.Line,
                refItem.Column,
                refItem.Context,
                refItem.ArgFlow));
            callAdded = true;
        }

        // Interface implementors
        if (targetTypeName != null && !string.IsNullOrEmpty(methodName) && interfaceToImplementors.TryGetValue(targetTypeName, out var implementors))
        {
            foreach (var implClass in implementors)
            {
                if (symbols.FunctionSymbols.TryGetValue($"{implClass}.{methodName}", out var implNodeId) &&
                    implNodeId != targetNodeId &&
                    Layer5SymbolVisibilityResolver.IsImplementorVisibleToCaller(callerScope, implNodeId, symbols, ctx))
                {
                    referenceRelationships.Add(CreateCallRel(refItem, implNodeId));
                    callAdded = true;
                }
            }
        }

        // If receiver was bound to a known workspace TypeSymbol, check if the method is registered under short name in that type's canonical file
        if (!callAdded && hasExplicitReceiver && targetTypeName != null && methodName != null &&
            Layer5SymbolVisibilityResolver.ResolveVisibleTypeSymbol(targetTypeName, refItem.ScopeSymbolId, ctx, symbols) is { } resolvedTypeId)
        {
            var typeFile = ExtractFilePathFromSymbolId(resolvedTypeId);
            if (typeFile != null &&
                symbols.FunctionSymbolsByFile.TryGetValue((typeFile, methodName), out var typeMethodId) &&
                Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(refItem.ScopeSymbolId, normCallerFile, typeMethodId, hasExplicitReceiver, targetTypeName, symbols, ctx))
            {
                referenceRelationships.Add(CreateCallRel(refItem, typeMethodId));
                callAdded = true;
            }
        }

        // Fallback for methodName (only when there is no explicit receiver, e.g. this.foo() / self.foo() / base.foo())
        if (!callAdded && !isExternalStaticClass && !hasExplicitReceiver && methodName != null)
        {
            if (normCallerFile != null &&
                ctx.FileScopedSymbols.TryGetValue((normCallerFile, OntologyConstants.NodeLabels.Function, methodName), out var localFallbackId) &&
                Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(refItem.ScopeSymbolId, normCallerFile, localFallbackId, hasExplicitReceiver: false, receiverTypeName: null, symbols, ctx))
            {
                referenceRelationships.Add(CreateCallRel(refItem, localFallbackId));
            }
            else if (normCallerFile != null &&
                     symbols.FunctionSymbolsByFile.TryGetValue((normCallerFile, methodName), out localFallbackId) &&
                     Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(refItem.ScopeSymbolId, normCallerFile, localFallbackId, hasExplicitReceiver: false, receiverTypeName: null, symbols, ctx))
            {
                referenceRelationships.Add(CreateCallRel(refItem, localFallbackId));
            }
            else if (normCallerFile != null && (targetNodeId = ResolveFunctionCallThroughImports(normCallerFile, methodName, symbols, imports, ctx)) != null)
            {
                referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            }
            else if (!isCallbackArg && normCallerFile != null &&
                     (targetNodeId = ResolveSameProjectFunctionFallback(normCallerFile, methodName, refItem.ScopeSymbolId, hasExplicitReceiver: false, receiverTypeName: null, symbols, ctx)) != null)
            {
                referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            }
            else if (!isCallbackArg && symbols.FunctionsByName.TryGetValue(methodName, out var fallbackCandidates))
            {
                foreach (var fallbackNodeId in fallbackCandidates)
                {
                    var targetFile = ExtractFilePathFromSymbolId(fallbackNodeId);
                    var isSameProjectOrFile = normCallerFile != null && targetFile != null &&
                        (normCallerFile.Equals(targetFile, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(ConstantRegistry.ExtractProjectName(normCallerFile), ConstantRegistry.ExtractProjectName(targetFile), StringComparison.OrdinalIgnoreCase));

                    if ((isSameProjectOrFile || (normCallerFile == null && targetFile == null)) &&
                        Layer5SymbolVisibilityResolver.IsSymbolVisibleToCaller(refItem.ScopeSymbolId, normCallerFile, fallbackNodeId, hasExplicitReceiver: false, receiverTypeName: null, symbols, ctx))
                    {
                        referenceRelationships.Add(CreateCallRel(refItem, fallbackNodeId));
                        break;
                    }
                }
            }
        }
    }

    internal static bool IsGraphQLDocumentSymbolId(string symbolId)
    {
        var fp = ExtractFilePathFromSymbolId(symbolId);
        return fp != null &&
               (fp.EndsWith(".graphql", StringComparison.OrdinalIgnoreCase) ||
                fp.EndsWith(".gql", StringComparison.OrdinalIgnoreCase));
    }

    private static string? SelectPreferredFunctionCandidate(List<string> candidates)
    {
        if (candidates.Count == 1) return candidates[0];
        var nonGql = candidates.Where(id => !IsGraphQLDocumentSymbolId(id)).Distinct(StringComparer.Ordinal).ToList();
        return nonGql.Count == 1 ? nonGql[0] : null;
    }

    private static string? ResolveSameProjectFunctionFallback(
        string callerFile,
        string targetName,
        string callerSymbolId,
        bool hasExplicitReceiver,
        string? receiverTypeName,
        SymbolLookups symbols,
        ParsingContext ctx)
    {
        var callerScope = Layer5SymbolVisibilityResolver.GetOrInferSymbolScope(callerSymbolId, callerFile, ctx);
        var callerProj = !string.IsNullOrEmpty(callerScope.ProjectName)
            ? callerScope.ProjectName
            : ConstantRegistry.ExtractProjectName(callerFile);

        if (callerProj != null &&
            symbols.FunctionsByProject.TryGetValue(callerProj, out var projFns) &&
            projFns.TryGetValue(targetName, out var matchedIds))
        {
            var resolved = Layer5SymbolVisibilityResolver.SelectVisibleSymbolCandidate(matchedIds, callerSymbolId, callerFile, hasExplicitReceiver, receiverTypeName, symbols, ctx);
            if (resolved != null) return resolved;
        }

        var extProj = ConstantRegistry.ExtractProjectName(callerFile);
        if (extProj != null &&
            !string.Equals(extProj, callerProj, StringComparison.OrdinalIgnoreCase) &&
            symbols.FunctionsByProject.TryGetValue(extProj, out var extProjFns) &&
            extProjFns.TryGetValue(targetName, out var extMatchedIds))
        {
            return Layer5SymbolVisibilityResolver.SelectVisibleSymbolCandidate(extMatchedIds, callerSymbolId, callerFile, hasExplicitReceiver, receiverTypeName, symbols, ctx);
        }

        return null;
    }

    private static string? ResolveFunctionCallThroughImports(
        string callerFile,
        string targetName,
        SymbolLookups symbols,
        ImportLookups imports,
        ParsingContext ctx)
    {
        var normCaller = callerFile.Replace('\\', '/');

        // 1. Direct imported symbol: import { pluralize } from "..."
        if (imports.FileImports.TryGetValue((normCaller, targetName), out var importedEntry))
        {
            var candidates = ResolveImportCandidateFiles(normCaller, importedEntry.ImportPath, imports, ctx, symbols);
            foreach (var cand in candidates)
            {
                var resolvedId = TryResolveFunctionInCandidateFile(cand, importedEntry.ImportedName, symbols, imports, ctx);
                if (resolvedId != null)
                {
                    return resolvedId;
                }

                // If default import, try targetName as importedName or file-named function
                if (importedEntry.IsDefault)
                {
                    var defResolvedId = TryResolveFunctionInCandidateFile(cand, targetName, symbols, imports, ctx);
                    if (defResolvedId != null)
                    {
                        return defResolvedId;
                    }
                    var candFileName = Path.GetFileNameWithoutExtension(cand);
                    var namedResolvedId = TryResolveFunctionInCandidateFile(cand, candFileName, symbols, imports, ctx);
                    if (namedResolvedId != null)
                    {
                        return namedResolvedId;
                    }
                }
            }

            // If candidate file was a package/project, search other files in that project for the imported symbol
            foreach (var cand in candidates)
            {
                var candProj = ConstantRegistry.ExtractProjectName(cand);
                if (candProj != null &&
                    symbols.FunctionsByProject.TryGetValue(candProj, out var projFns) &&
                    projFns.TryGetValue(importedEntry.ImportedName, out var projFuncIds) &&
                    projFuncIds.Count > 0)
                {
                    return projFuncIds[0];
                }
            }
        }

        // 2. Star imports: import * from "..." in caller file
        if (imports.StarImportsByFile.TryGetValue(normCaller, out var starPaths))
        {
            foreach (var starPath in starPaths)
            {
                var candidates = ResolveImportCandidateFiles(normCaller, starPath, imports, ctx, symbols);
                foreach (var cand in candidates)
                {
                    var starFuncId = TryResolveFunctionInCandidateFile(cand, targetName, symbols, imports, ctx);
                    if (starFuncId != null)
                    {
                        return starFuncId;
                    }
                }
            }
        }

        return null;
    }

    private static string? TryResolveFunctionInCandidateFile(
        string cand,
        string exportedName,
        SymbolLookups symbols,
        ImportLookups imports,
        ParsingContext ctx,
        int depth = 0)
    {
        if (depth > 4 || string.IsNullOrEmpty(exportedName))
        {
            return null;
        }

        if (symbols.FunctionSymbolsByFile.TryGetValue((cand, exportedName), out var funcId))
        {
            return funcId;
        }

        if (ctx.FileScopedSymbols.TryGetValue((cand, OntologyConstants.NodeLabels.Type, exportedName), out var typeSymbolId) ||
            ctx.FileScopedSymbols.TryGetValue((cand, "Class", exportedName), out typeSymbolId) ||
            ctx.FileScopedSymbols.TryGetValue((cand, "Interface", exportedName), out typeSymbolId))
        {
            return typeSymbolId;
        }

        if (imports.FileImports.TryGetValue((cand, exportedName), out var reExportedEntry))
        {
            if (string.Equals(reExportedEntry.ImportPath, "__self__", StringComparison.Ordinal))
            {
                if (symbols.FunctionSymbolsByFile.TryGetValue((cand, reExportedEntry.ImportedName), out var aliasFuncId))
                {
                    return aliasFuncId;
                }
            }
            else
            {
                var reCandidates = ResolveImportCandidateFiles(cand, reExportedEntry.ImportPath, imports, ctx, symbols);
                foreach (var reCand in reCandidates)
                {
                    var reResolved = TryResolveFunctionInCandidateFile(reCand, reExportedEntry.ImportedName, symbols, imports, ctx, depth + 1);
                    if (reResolved != null)
                    {
                        return reResolved;
                    }
                }
            }
        }

        if (imports.StarImportsByFile.TryGetValue(cand, out var barrelStarPaths))
        {
            foreach (var starPath in barrelStarPaths)
            {
                var starCandidates = ResolveImportCandidateFiles(cand, starPath, imports, ctx, symbols);
                foreach (var starCand in starCandidates)
                {
                    var starResolved = TryResolveFunctionInCandidateFile(starCand, exportedName, symbols, imports, ctx, depth + 1);
                    if (starResolved != null)
                    {
                        return starResolved;
                    }
                }
            }
        }

        return null;
    }

    private static void ResolveTriggersReference(
        Reference refItem,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var targetName = refItem.TargetName;
        var shortName = refItem.MemberName;
        var callerFile = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId)
                         ?? (!string.IsNullOrEmpty(refItem.ArgFlow) && (refItem.ArgFlow.Contains('/') || refItem.ArgFlow.Contains('\\')) ? refItem.ArgFlow.Replace('\\', '/') : null);
        var callerProj = callerFile != null ? ConstantRegistry.ExtractProjectName(callerFile) : ExtractProjectFromScopeId(refItem.ScopeSymbolId, symbols);

        string? targetId = null;

        // 1. Same file lookup
        if (callerFile != null)
        {
            if (symbols.FunctionSymbolsByFile.TryGetValue((callerFile, targetName), out targetId) ||
                symbols.FunctionSymbolsByFile.TryGetValue((callerFile, shortName), out targetId))
            {
                // Found in same file
            }
        }

        // 2. Same project lookup (only when unambiguous in project or when callerFile is known)
        if (targetId == null && callerProj != null && symbols.FunctionsByProject.TryGetValue(callerProj, out var projFns))
        {
            if (projFns.TryGetValue(targetName, out var ids) && (ids.Count == 1 || callerFile != null))
            {
                targetId = ids[0];
            }
            else if (projFns.TryGetValue(shortName, out ids) && (ids.Count == 1 || callerFile != null))
            {
                targetId = ids[0];
            }
        }

        // 3. Fallback to FunctionsByName filtered to caller project if known, or unique globally
        if (targetId == null)
        {
            if (symbols.FunctionsByName.TryGetValue(targetName, out var candidates) ||
                symbols.FunctionsByName.TryGetValue(shortName, out candidates))
            {
                if (callerProj != null)
                {
                    var projMatches = candidates.Where(c =>
                    {
                        var cFile = ExtractFilePathFromSymbolId(c);
                        return cFile != null && string.Equals(ConstantRegistry.ExtractProjectName(cFile), callerProj, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                    if (projMatches.Count == 1 || (projMatches.Count > 0 && callerFile != null))
                    {
                        targetId = projMatches[0];
                    }
                }
                else if (candidates.Count == 1)
                {
                    targetId = candidates[0];
                }
            }
        }

        // 4. Last resort: only if caller project is unknown, symbols.FunctionSymbols
        if (targetId == null && callerProj == null)
        {
            targetId = symbols.FunctionSymbols.GetValueOrDefault(targetName)
                       ?? symbols.FunctionSymbols.GetValueOrDefault(shortName);
        }

        // 5. Fallback to TypeSymbols if target is a Class/Component (e.g. React root component)
        if (targetId == null)
        {
            if (callerProj != null && symbols.FilesByProject.TryGetValue(callerProj, out var projFiles2))
            {
                if (symbols.TypeSymbols.TryGetValue(targetName, out var typeCand) ||
                    symbols.TypeSymbols.TryGetValue(shortName, out typeCand))
                {
                    var tFile = ExtractFilePathFromSymbolId(typeCand);
                    if (tFile != null && string.Equals(ConstantRegistry.ExtractProjectName(tFile), callerProj, StringComparison.OrdinalIgnoreCase))
                    {
                        targetId = typeCand;
                    }
                }
            }
            if (targetId == null)
            {
                targetId = symbols.TypeSymbols.GetValueOrDefault(targetName)
                           ?? symbols.TypeSymbols.GetValueOrDefault(shortName);
            }
        }

        if (targetId != null)
        {
            if (existingRefRels.Add((refItem.ScopeSymbolId, targetId, OntologyConstants.Relationships.Triggers)))
            {
                referenceRelationships.Add(
                    Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetId)));
            }
            if (refItem.ScopeSymbolId.Contains($":{OntologyConstants.IdPrefixes.Endpoint}:", StringComparison.Ordinal) &&
                existingRefRels.Add((refItem.ScopeSymbolId, targetId, OntologyConstants.Relationships.ExposedBy)))
            {
                referenceRelationships.Add(
                    Relationship.FromRelationship(new ExposedByRelationship(refItem.ScopeSymbolId, targetId)));
            }
        }
    }

    private static string? ExtractProjectFromScopeId(string scopeId, SymbolLookups symbols)
    {
        var parts = scopeId.Split(':');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] is "endpoint" or "ep" or "topic" or "service" or "entry" or "entrypoint")
            {
                var next = parts[i + 1];
                if (symbols.FilesByProject.ContainsKey(next))
                {
                    return next;
                }
            }
        }

        foreach (var proj in symbols.ProjectsSortedByLengthDesc)
        {
            if (scopeId.Contains($":{proj}:", StringComparison.OrdinalIgnoreCase))
            {
                return proj;
            }
        }

        return null;
    }

    internal static List<TopicNode> ResolveMessagingReferenceForTest(Reference refItem, ParsingContext ctx)
    {
        var topics = new List<TopicNode>();
        var symbols = new SymbolLookups([], [], [], [], [], [], [], [], [], []);
        var bindings = new BindingLookups([], [], []);
        ResolveMessagingReference(refItem, ctx, symbols, bindings, topics, new HashSet<string>(), []);
        return topics;
    }

    private static void ResolveMessagingReference(
        Reference refItem,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<TopicNode> newTopicNodes,
        HashSet<string> createdTopicIds,
        List<Relationship> referenceRelationships)
    {
        var topicName = refItem.TargetName;
        var brokerType = "event";
        (brokerType, topicName) = topicName switch
        {
            _ when topicName.StartWithIC("bullmq:") => ("bullmq", topicName["bullmq:".Length..]),
            _ when topicName.StartWithIC("bull:") => ("bullmq", topicName["bull:".Length..]),
            _ when topicName.StartWithIC("rabbitmq:") => ("rabbitmq", topicName["rabbitmq:".Length..]),
            _ when topicName.StartWithIC("kafka:") => ("kafka", topicName["kafka:".Length..]),
            _ when topicName.StartWithIC("sqs:") => ("sqs", topicName["sqs:".Length..]),
            _ when topicName.StartWithIC("sns:") => ("sns", topicName["sns:".Length..]),
            _ when topicName.StartWithIC("gcp:") => ("gcp", topicName["gcp:".Length..]),
            _ when topicName.StartWithIC("pubsub:") => ("gcp", topicName["pubsub:".Length..]),
            _ when topicName.StartWithIC("mediatr:") => ("mediatr", topicName["mediatr:".Length..]),
            _ when topicName.StartWithIC("masstransit:") => ("masstransit", topicName["masstransit:".Length..]),
            _ when topicName.StartWithIC("eventbus:") => ("eventbus", topicName["eventbus:".Length..]),
            _ when topicName.StartWithIC("spring:") => ("spring", topicName["spring:".Length..]),
            _ when topicName.StartWithIC("cqrs:") => ("cqrs", topicName["cqrs:".Length..]),
            _ when topicName.StartWithIC("nats:") => ("nats", topicName["nats:".Length..]),
            _ when topicName.StartWithIC("event:") => ("event", topicName["event:".Length..]),
            _ => (brokerType, topicName)
        };

        if (brokerType == "gcp")
        {
            if (topicName.EndsWith("-SUB", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^4];
            }
            else if (topicName.EndsWith("_SUBSCRIPTION_NAME", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^"_SUBSCRIPTION_NAME".Length] + "_TOPIC_NAME";
            }
            else if (topicName.EndsWith("_SUBSCRIPTION", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^"_SUBSCRIPTION".Length] + "_TOPIC";
            }
            else if (topicName.EndsWith("_SUB", StringComparison.OrdinalIgnoreCase))
            {
                topicName = topicName[..^"_SUB".Length];
            }
        }

        if (topicName.Contains('(') || System.Text.RegularExpressions.Regex.IsMatch(topicName, @"\bawait\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var innerMatch = System.Text.RegularExpressions.Regex.Match(topicName, @"\(\s*([^,\)]+)\s*\)");
            if (innerMatch.Success)
            {
                var inner = innerMatch.Groups[1].Value.Trim().Trim('\'', '"', '`');
                if (!string.IsNullOrEmpty(inner) && !inner.Equals("topic", StringComparison.OrdinalIgnoreCase))
                {
                    topicName = inner;
                }
            }
            else
            {
                return;
            }
        }

        var scopeFilePath = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);
        var scopeProject = ConstantRegistry.ExtractProjectName(scopeFilePath);

        // Mocks and fixtures in tests are not real broker topology.
        if (WorkspaceConventions.IsTestFilePath(scopeFilePath))
        {
            return;
        }

        const string configDotPrefix = "config.";
        const string thisDotPrefix = "this.";
        if (topicName.StartWithIC("this.config."))
        {
            var prop = topicName["this.config.".Length..];
            if (ConstantRegistry.TryResolve(scopeProject, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
            else if (WorkspaceConventions.IsPlaceholderName(prop))
            {
                return;
            }
            else
            {
                topicName = prop;
            }
        }
        else if (topicName.StartWithIC(configDotPrefix))
        {
            var prop = topicName[configDotPrefix.Length..];
            if (ConstantRegistry.TryResolve(scopeProject, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
            else if (WorkspaceConventions.IsPlaceholderName(prop))
            {
                return;
            }
            else
            {
                topicName = prop;
            }
        }
        else if (topicName.StartWithIC(thisDotPrefix))
        {
            var prop = topicName[thisDotPrefix.Length..];
            if (ConstantRegistry.TryResolve(scopeProject, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
            else
            {
                // A field set from constructor parameters is dynamic; a workspace-wide
                // lookup of its bare name would pick an unrelated constant.
                return;
            }
        }

        string? globalConst = null;

        // Only constant-style names (ORDERS_TOPIC) or PascalCase enum members (MessageQueue.emailQueue)
        // may resolve workspace-wide. A bare parameter or field such as `name` must stay within its project,
        // or it picks up an unrelated constant.
        var isConstantStyle = System.Text.RegularExpressions.Regex.IsMatch(topicName, @"^[A-Z][A-Z0-9_]*$|^[A-Z][A-Za-z0-9_]*\.[A-Za-z0-9_]+$");

        if (isConstantStyle &&
            bindings.ConstantLookup.TryGetValue(topicName, out var resolvedConst) &&
            WorkspaceConventions.IsValidTopicOrQueueName(resolvedConst))
        {
            topicName = resolvedConst;
        }
        else if ((isConstantStyle
                     ? ConstantRegistry.TryResolve(scopeProject ?? scopeFilePath, topicName, out var regConst)
                     : ConstantRegistry.TryResolveProjectScoped(scopeProject ?? scopeFilePath, topicName, out regConst)) &&
                 WorkspaceConventions.IsValidTopicOrQueueName(regConst))
        {
            topicName = regConst;
        }
        else if (System.Text.RegularExpressions.Regex.IsMatch(topicName, @"^[A-Z0-9_]{3,}$") &&
                 ConstantRegistry.TryResolve(null, topicName, out globalConst) &&
                 WorkspaceConventions.IsValidTopicOrQueueName(globalConst))
        {
            topicName = globalConst;
        }

        topicName = topicName.Trim('\'', '"', '`');
        if (brokerType is not ("mediatr" or "cqrs" or "spring" or "eventbus"))
        {
            var normalizedTopic = WorkspaceConventions.NormalizeTopicName(topicName);
            if (string.IsNullOrEmpty(normalizedTopic))
            {
                return;
            }
            topicName = normalizedTopic;
        }

        if (IsFileNodeScope(refItem.ScopeSymbolId))
        {
            return;
        }

        var isInternal = brokerType is "mediatr" or "cqrs";
        var topicId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Topic}:{brokerType}:{topicName}";

        if (!createdTopicIds.Contains(topicId))
        {
            createdTopicIds.Add(topicId);
            var topicNode = new TopicNode(
                topicId,
                topicName,
                "",
                brokerType)
            {
                IsInternal = isInternal,
                Scope = isInternal ? "internal" : "external",
                MessageType = isInternal ? "internal" : "external",
                IsSemanticEntity = !isInternal
            };
            newTopicNodes.Add(topicNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, refItem.TargetName, topicId);
        }

        var relKind = refItem.Kind == RelationshipKind.PublishesTo
            ? RelationshipKind.PublishedBy
            : RelationshipKind.SubscribedBy;
        var isPublish = refItem.Kind == RelationshipKind.PublishesTo;

        referenceRelationships.Add(new Relationship(topicId, refItem.ScopeSymbolId, relKind)
        {
            Line = refItem.Line,
            Column = refItem.Column,
            CallContext = refItem.Context,
            ArgFlow = refItem.ArgFlow,
            IsInternal = isInternal,
            Scope = isInternal ? "internal" : "external",
            Category = "messaging",
            DependencyType = "messaging",
            BrokerType = brokerType,
            Operation = isPublish ? "PUBLISH" : "SUBSCRIBE",
            EvidenceKind = isPublish
                ? OntologyConstants.EvidenceKinds.Publish
                : OntologyConstants.EvidenceKinds.Subscribe,
            FilePath = !string.IsNullOrEmpty(scopeFilePath) ? scopeFilePath : null,
            IsSemantic = isInternal ? false : null
        });
    }

    private static void ResolveDeferredParamMessagingReferences(
        List<Reference> deferredRefs,
        ParsingContext ctx,
        SymbolLookups symbols,
        BindingLookups bindings,
        List<TopicNode> newTopicNodes,
        HashSet<string> createdTopicIds,
        List<Relationship> referenceRelationships)
    {
        var callsByTarget = new Dictionary<string, List<Relationship>>(StringComparer.Ordinal);
        foreach (var rel in referenceRelationships)
        {
            if (rel.Kind == RelationshipKind.Calls && !string.IsNullOrEmpty(rel.ArgFlow))
            {
                if (!callsByTarget.TryGetValue(rel.To, out var list))
                {
                    list = [];
                    callsByTarget[rel.To] = list;
                }
                list.Add(rel);
            }
        }

        if (callsByTarget.Count == 0) return;

        foreach (var defRef in deferredRefs)
        {
            var paramIdxPos = defRef.TargetName.IndexOf("$param:", StringComparison.Ordinal);
            if (paramIdxPos < 0) continue;

            var prefix = defRef.TargetName[..paramIdxPos];
            var paramStr = defRef.TargetName[(paramIdxPos + 7)..];
            if (!int.TryParse(paramStr, out var paramIdx)) continue;

            if (!callsByTarget.TryGetValue(defRef.ScopeSymbolId, out var incomingCalls)) continue;

            var matchPrefix = $"{paramIdx}<-";
            foreach (var callRel in incomingCalls)
            {
                var argFlow = callRel.ArgFlow!;
                foreach (var segment in argFlow.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!segment.StartsWith(matchPrefix, StringComparison.Ordinal)) continue;
                    var originsPart = segment[matchPrefix.Length..];
                    foreach (var rawOrigin in originsPart.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var origin = rawOrigin.Trim();
                        if (string.IsNullOrEmpty(origin) ||
                            (origin.Length >= 2 && origin[0] == 'p' && char.IsDigit(origin[1])))
                        {
                            continue;
                        }

                        var concreteRef = defRef with
                        {
                            ScopeSymbolId = callRel.From,
                            TargetName = $"{prefix}{origin}",
                            Line = callRel.Line > 0 ? callRel.Line : defRef.Line,
                            Column = callRel.Column > 0 ? callRel.Column : defRef.Column
                        };
                        ResolveMessagingReference(concreteRef, ctx, symbols, bindings, newTopicNodes, createdTopicIds, referenceRelationships);
                    }
                }
            }
        }
    }

    private static bool IsFileNodeScope(string scopeId) =>
        scopeId.Contains($":{OntologyConstants.IdPrefixes.File}:", StringComparison.Ordinal) ||
        scopeId.Contains(":file:", StringComparison.Ordinal);

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint) => IsMatch(extService, entryPoint, null);

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint, ProjectNode? targetProj)
    {
        var prepExt = PrepareExternalService(extService, null);
        var prepEp = PrepareEntryPoint(entryPoint, targetProj);
        return IsMatch(prepExt, prepEp, (p, d) => DoesProjectMatchServiceDomain(p, d));
    }

    private static string NormalizeGcpTopicName(string raw) => WorkspaceConventions.NormalizeTopicName(raw);
}

internal static partial class Layer5SymbolVisibilityResolver
{
    internal static string? NormalizeWorkspaceRelativeFilePath(string? filePath, ParsingContext ctx)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        var norm = filePath.Replace('\\', '/');
        if (!string.IsNullOrEmpty(ctx.AbsoluteWorkspacePath))
        {
            var wsNorm = ctx.AbsoluteWorkspacePath.Replace('\\', '/').TrimEnd('/');
            if (norm.StartsWith(wsNorm + "/", StringComparison.OrdinalIgnoreCase))
            {
                return norm[(wsNorm.Length + 1)..];
            }
        }
        return norm;
    }

    internal static SymbolScopeInfo GetOrInferSymbolScope(string symbolId, string? fallbackFilePath, ParsingContext ctx)
    {
        if (ctx.SymbolScopes.TryGetValue(symbolId, out var scope))
        {
            return scope;
        }

        var rawFp = Layer5AnalysisParser.ExtractFilePathFromSymbolId(symbolId) ?? fallbackFilePath ?? "";
        var fp = NormalizeWorkspaceRelativeFilePath(rawFp, ctx) ?? "";
        var proj = !string.IsNullOrEmpty(fp) ? ConstantRegistry.ExtractProjectName(fp) : null;
        string? ns = null;
        if (!string.IsNullOrEmpty(fp))
        {
            ctx.FileNamespaces.TryGetValue(fp, out ns);
        }

        string? enclosingType = null;
        var symName = Layer5AnalysisParser.ExtractSymbolNameFromId(symbolId);
        if (!string.IsNullOrEmpty(symName) && !symName.StartsWith('<'))
        {
            var dotIdx = symName.LastIndexOf('.');
            if (dotIdx > 0)
            {
                enclosingType = symName[..dotIdx];
            }
        }

        return new SymbolScopeInfo(
            symbolId,
            fp,
            proj,
            ns,
            enclosingType,
            SymbolVisibility.Public,
            !string.IsNullOrEmpty(fp) && WorkspaceConventions.IsTestFilePath(fp));
    }

    internal static bool IsSameProjectScope(in SymbolScopeInfo callerScope, in SymbolScopeInfo targetScope)
    {
        if (!string.IsNullOrEmpty(callerScope.FilePath) &&
            !string.IsNullOrEmpty(targetScope.FilePath) &&
            string.Equals(callerScope.FilePath, targetScope.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(callerScope.ProjectName) &&
            !string.IsNullOrEmpty(targetScope.ProjectName) &&
            string.Equals(callerScope.ProjectName, targetScope.ProjectName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var callerExtProj = !string.IsNullOrEmpty(callerScope.FilePath) ? ConstantRegistry.ExtractProjectName(callerScope.FilePath) : callerScope.ProjectName;
        var targetExtProj = !string.IsNullOrEmpty(targetScope.FilePath) ? ConstantRegistry.ExtractProjectName(targetScope.FilePath) : targetScope.ProjectName;
        if (!string.IsNullOrEmpty(callerExtProj) &&
            !string.IsNullOrEmpty(targetExtProj) &&
            string.Equals(callerExtProj, targetExtProj, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.IsNullOrEmpty(callerScope.FilePath) &&
               string.IsNullOrEmpty(targetScope.FilePath) &&
               string.IsNullOrEmpty(callerScope.ProjectName) &&
               string.IsNullOrEmpty(targetScope.ProjectName);
    }

    private static string? FindDepGraphProjectKey(in SymbolScopeInfo scope, Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (!string.IsNullOrEmpty(scope.ProjectName) && symbols.ProjectsInDepGraph.Contains(scope.ProjectName))
        {
            return scope.ProjectName;
        }
        if (!string.IsNullOrEmpty(scope.FilePath))
        {
            var ext = ConstantRegistry.ExtractProjectName(scope.FilePath);
            if (!string.IsNullOrEmpty(ext) && symbols.ProjectsInDepGraph.Contains(ext))
            {
                return ext;
            }
        }
        return null;
    }

    internal static bool IsProjectReachable(in SymbolScopeInfo callerScope, in SymbolScopeInfo targetScope, Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (IsSameProjectScope(callerScope, targetScope))
        {
            return true;
        }

        if (symbols.ProjectsInDepGraph.Count == 0)
        {
            return true;
        }

        var callerKey = FindDepGraphProjectKey(callerScope, symbols);
        var targetKey = FindDepGraphProjectKey(targetScope, symbols);
        if (callerKey == null || targetKey == null)
        {
            return true;
        }

        if (string.Equals(callerKey, targetKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (symbols.ReachableProjects.TryGetValue(callerKey, out var reachableSet))
        {
            if (reachableSet.Contains(targetKey))
            {
                return true;
            }
            if (callerScope.IsTest && reachableSet.Count <= 1)
            {
                return true;
            }
            return false;
        }

        return true;
    }

    internal static bool IsImplementorVisibleToCaller(
        in SymbolScopeInfo callerScope,
        string implementorSymbolId,
        Layer5AnalysisParser.SymbolLookups symbols,
        ParsingContext ctx)
    {
        var implScope = GetOrInferSymbolScope(implementorSymbolId, null, ctx);
        if (!callerScope.IsTest && implScope.IsTest)
        {
            return false;
        }
        if (IsProjectReachable(callerScope, implScope, symbols))
        {
            return true;
        }
        return IsProjectReachable(implScope, callerScope, symbols);
    }

    internal static bool DoesTypeInheritFrom(string derivedTypeName, string baseTypeName, Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (string.Equals(derivedTypeName, baseTypeName, StringComparison.Ordinal)) return true;
        if (symbols.InheritedTypeNames.Count > 0)
        {
            return symbols.InheritedTypeNames.Contains((derivedTypeName, baseTypeName));
        }
        if (symbols.InheritanceRels.Count == 0) return false;

        foreach (var (fromId, toId) in symbols.InheritanceRels)
        {
            var fromName = Layer5AnalysisParser.ExtractSymbolNameFromId(fromId);
            var toName = Layer5AnalysisParser.ExtractSymbolNameFromId(toId);
            if (string.Equals(fromName, derivedTypeName, StringComparison.Ordinal) &&
                string.Equals(toName, baseTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool IsSymbolVisibleToCaller(
        string callerSymbolId,
        string? normCallerFile,
        string targetSymbolId,
        bool hasExplicitReceiver,
        string? receiverTypeName,
        Layer5AnalysisParser.SymbolLookups symbols,
        ParsingContext ctx)
    {
        var callerScope = GetOrInferSymbolScope(callerSymbolId, normCallerFile, ctx);
        var targetScope = GetOrInferSymbolScope(targetSymbolId, null, ctx);
        if (!callerScope.IsTest && targetScope.IsTest)
        {
            return false;
        }

        var sameFile = (!string.IsNullOrEmpty(callerScope.FilePath) &&
                        !string.IsNullOrEmpty(targetScope.FilePath) &&
                        string.Equals(callerScope.FilePath, targetScope.FilePath, StringComparison.OrdinalIgnoreCase)) ||
                       (string.IsNullOrEmpty(callerScope.FilePath) && string.IsNullOrEmpty(targetScope.FilePath));

        var sameProject = sameFile || IsSameProjectScope(callerScope, targetScope);

        switch (targetScope.Visibility)
        {
            case SymbolVisibility.FilePrivate:
                return sameFile;

            case SymbolVisibility.Private:
                if (!sameProject) return false;
                if (!string.IsNullOrEmpty(targetScope.EnclosingTypeName))
                {
                    if (hasExplicitReceiver &&
                        !string.IsNullOrEmpty(receiverTypeName) &&
                        !string.Equals(receiverTypeName, targetScope.EnclosingTypeName, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (!string.IsNullOrEmpty(callerScope.EnclosingTypeName))
                    {
                        return string.Equals(callerScope.EnclosingTypeName, targetScope.EnclosingTypeName, StringComparison.Ordinal);
                    }

                    return sameFile && !hasExplicitReceiver;
                }
                return sameFile;

            case SymbolVisibility.Protected:
                if (!string.IsNullOrEmpty(targetScope.EnclosingTypeName) && !string.IsNullOrEmpty(callerScope.EnclosingTypeName))
                {
                    if (string.Equals(callerScope.EnclosingTypeName, targetScope.EnclosingTypeName, StringComparison.Ordinal))
                    {
                        return sameProject;
                    }
                    if (DoesTypeInheritFrom(callerScope.EnclosingTypeName, targetScope.EnclosingTypeName, symbols))
                    {
                        return IsProjectReachable(callerScope, targetScope, symbols);
                    }
                    return false;
                }
                return sameFile;

            case SymbolVisibility.Internal:
                if (!sameProject && !callerScope.IsTest)
                {
                    return false;
                }
                return IsProjectReachable(callerScope, targetScope, symbols);

            default:
                return IsProjectReachable(callerScope, targetScope, symbols);
        }
    }

    internal static string? SelectVisibleSymbolCandidate(
        List<string> candidates,
        string callerSymbolId,
        string? normCallerFile,
        bool hasExplicitReceiver,
        string? receiverTypeName,
        Layer5AnalysisParser.SymbolLookups symbols,
        ParsingContext ctx)
    {
        if (candidates.Count == 0) return null;

        if (candidates.Count == 1)
        {
            var onlyId = candidates[0];
            if (onlyId == callerSymbolId && callerSymbolId.Contains(":Type:", StringComparison.Ordinal))
            {
                return null;
            }
            return IsSymbolVisibleToCaller(callerSymbolId, normCallerFile, onlyId, hasExplicitReceiver, receiverTypeName, symbols, ctx)
                ? onlyId
                : null;
        }

        var visible = new List<string>(candidates.Count);
        for (var i = 0; i < candidates.Count; i++)
        {
            var id = candidates[i];
            if (Layer5AnalysisParser.IsGraphQLDocumentSymbolId(id)) continue;
            if (!visible.Contains(id) &&
                IsSymbolVisibleToCaller(callerSymbolId, normCallerFile, id, hasExplicitReceiver, receiverTypeName, symbols, ctx))
            {
                visible.Add(id);
            }
        }

        if (visible.Count == 0)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var id = candidates[i];
                if (!visible.Contains(id) &&
                    IsSymbolVisibleToCaller(callerSymbolId, normCallerFile, id, hasExplicitReceiver, receiverTypeName, symbols, ctx))
                {
                    visible.Add(id);
                }
            }
        }

        DeduplicateSameFileOverloads(visible, callerSymbolId, ctx);
        if (visible.Count == 0) return null;
        if (visible.Count == 1) return visible[0];

        var callerScope = GetOrInferSymbolScope(callerSymbolId, normCallerFile, ctx);

        if (hasExplicitReceiver && !string.IsNullOrEmpty(receiverTypeName))
        {
            var shortReceiverType = receiverTypeName.Contains('.')
                ? receiverTypeName[(receiverTypeName.LastIndexOf('.') + 1)..]
                : receiverTypeName;
            string? matchedByReceiverType = null;
            var receiverTypeMatchCount = 0;
            for (var i = 0; i < visible.Count; i++)
            {
                var candScope = GetOrInferSymbolScope(visible[i], null, ctx);
                if (string.Equals(candScope.EnclosingTypeName, shortReceiverType, StringComparison.Ordinal))
                {
                    matchedByReceiverType = visible[i];
                    receiverTypeMatchCount++;
                }
            }
            if (receiverTypeMatchCount == 1) return matchedByReceiverType;
        }

        if (!string.IsNullOrEmpty(callerScope.EnclosingTypeName))
        {
            string? matchedByType = null;
            var typeMatchCount = 0;
            for (var i = 0; i < visible.Count; i++)
            {
                var candScope = GetOrInferSymbolScope(visible[i], null, ctx);
                if (string.Equals(candScope.EnclosingTypeName, callerScope.EnclosingTypeName, StringComparison.Ordinal))
                {
                    matchedByType = visible[i];
                    typeMatchCount++;
                }
            }
            if (typeMatchCount == 1) return matchedByType;
        }

        if (!string.IsNullOrEmpty(callerScope.FilePath))
        {
            string? matchedByFile = null;
            var fileMatchCount = 0;
            for (var i = 0; i < visible.Count; i++)
            {
                var candScope = GetOrInferSymbolScope(visible[i], null, ctx);
                if (string.Equals(candScope.FilePath, callerScope.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    matchedByFile = visible[i];
                    fileMatchCount++;
                }
            }
            if (fileMatchCount == 1) return matchedByFile;
        }

        var sameProjList = new List<string>();
        for (var i = 0; i < visible.Count; i++)
        {
            var candScope = GetOrInferSymbolScope(visible[i], null, ctx);
            if (IsSameProjectScope(callerScope, candScope))
            {
                sameProjList.Add(visible[i]);
            }
        }
        if (sameProjList.Count == 1) return sameProjList[0];

        var pool = sameProjList.Count > 1 ? sameProjList : visible;

        if (!string.IsNullOrEmpty(callerScope.Namespace))
        {
            string? matchedByNs = null;
            var nsMatchCount = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                var candScope = GetOrInferSymbolScope(pool[i], null, ctx);
                if (string.Equals(candScope.Namespace, callerScope.Namespace, StringComparison.Ordinal))
                {
                    matchedByNs = pool[i];
                    nsMatchCount++;
                }
            }
            if (nsMatchCount == 1) return matchedByNs;
        }

        if (!string.IsNullOrEmpty(callerScope.FilePath) &&
            ctx.FileImportedNamespaces.TryGetValue(callerScope.FilePath, out var importedNamespaces) &&
            importedNamespaces.Count > 0)
        {
            string? matchedByImportNs = null;
            var importNsCount = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                var candScope = GetOrInferSymbolScope(pool[i], null, ctx);
                if (!string.IsNullOrEmpty(candScope.Namespace) && importedNamespaces.Contains(candScope.Namespace))
                {
                    matchedByImportNs = pool[i];
                    importNsCount++;
                }
            }
            if (importNsCount == 1) return matchedByImportNs;
        }

        return null;
    }

    private static void DeduplicateSameFileOverloads(List<string> visible, string callerSymbolId, ParsingContext ctx)
    {
        if (visible.Count > 1 && callerSymbolId.Contains(":Type:", StringComparison.Ordinal))
        {
            visible.Remove(callerSymbolId);
        }
        if (visible.Count <= 1) return;

        for (var i = visible.Count - 1; i >= 0; i--)
        {
            var idI = visible[i];
            if (!idI.Contains('#', StringComparison.Ordinal)) continue;
            var scopeI = GetOrInferSymbolScope(idI, null, ctx);
            if (string.IsNullOrEmpty(scopeI.FilePath)) continue;

            for (var j = 0; j < visible.Count; j++)
            {
                if (i == j) continue;
                var idJ = visible[j];
                if (idJ.Contains('#', StringComparison.Ordinal) && j > i) continue;
                var scopeJ = GetOrInferSymbolScope(idJ, null, ctx);
                if (string.Equals(scopeI.FilePath, scopeJ.FilePath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(scopeI.EnclosingTypeName ?? "", scopeJ.EnclosingTypeName ?? "", StringComparison.Ordinal))
                {
                    visible.RemoveAt(i);
                    break;
                }
            }
        }
    }

    internal static void UnifyPartialTypeDeclarations(
        Dictionary<string, List<string>> typesByName,
        Dictionary<string, string> typeSymbols,
        Dictionary<(string FilePath, string FuncName), string> functionSymbolsByFile,
        Dictionary<string, List<(string FuncName, string FuncId)>> functionsInFile,
        ParsingContext ctx)
    {
        foreach (var (typeName, tList) in typesByName)
        {
            if (tList.Count <= 1) continue;

            for (var i = 0; i < tList.Count; i++)
            {
                var idA = tList[i];
                var scopeA = GetOrInferSymbolScope(idA, null, ctx);
                if (string.IsNullOrEmpty(scopeA.Namespace)) continue;

                List<string>? siblingFiles = null;
                for (var j = tList.Count - 1; j > i; j--)
                {
                    var idB = tList[j];
                    var scopeB = GetOrInferSymbolScope(idB, null, ctx);
                    if (!string.Equals(scopeA.Namespace, scopeB.Namespace, StringComparison.Ordinal) ||
                        !string.Equals(scopeA.EnclosingTypeName ?? "", scopeB.EnclosingTypeName ?? "", StringComparison.Ordinal) ||
                        !IsSameProjectScope(scopeA, scopeB) ||
                        string.Equals(scopeA.FilePath, scopeB.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    siblingFiles ??= !string.IsNullOrEmpty(scopeA.FilePath) ? [scopeA.FilePath.Replace('\\', '/')] : [];
                    if (!string.IsNullOrEmpty(scopeB.FilePath))
                    {
                        siblingFiles.Add(scopeB.FilePath.Replace('\\', '/'));
                    }

                    if (IsPreferredCanonicalPartialDeclaration(typeName, idB, scopeB, idA, scopeA))
                    {
                        tList[i] = idB;
                        idA = idB;
                        scopeA = scopeB;
                    }
                    tList.RemoveAt(j);
                }

                if (siblingFiles is { Count: > 1 })
                {
                    typeSymbols[typeName] = idA;
                    CrossRegisterPartialTypeFunctions(siblingFiles, functionSymbolsByFile, functionsInFile);
                }
            }
        }
    }

    private static void CrossRegisterPartialTypeFunctions(
        List<string> siblingFiles,
        Dictionary<(string FilePath, string FuncName), string> functionSymbolsByFile,
        Dictionary<string, List<(string FuncName, string FuncId)>> functionsInFile)
    {
        for (var i = 0; i < siblingFiles.Count; i++)
        {
            var srcFile = siblingFiles[i];
            if (!functionsInFile.TryGetValue(srcFile, out var fns)) continue;
            for (var j = 0; j < siblingFiles.Count; j++)
            {
                if (i == j) continue;
                var dstFile = siblingFiles[j];
                for (var k = 0; k < fns.Count; k++)
                {
                    var (fnName, fnId) = fns[k];
                    functionSymbolsByFile.TryAdd((dstFile, fnName), fnId);
                }
            }
        }
    }

    private static bool IsPreferredCanonicalPartialDeclaration(
        string typeName,
        string candId,
        in SymbolScopeInfo candScope,
        string existingId,
        in SymbolScopeInfo existingScope)
    {
        var candFileStem = !string.IsNullOrEmpty(candScope.FilePath) ? Path.GetFileNameWithoutExtension(candScope.FilePath) : "";
        var existFileStem = !string.IsNullOrEmpty(existingScope.FilePath) ? Path.GetFileNameWithoutExtension(existingScope.FilePath) : "";
        var candExact = string.Equals(candFileStem, typeName, StringComparison.OrdinalIgnoreCase);
        var existExact = string.Equals(existFileStem, typeName, StringComparison.OrdinalIgnoreCase);
        if (candExact != existExact) return candExact;

        if (candFileStem.Length != existFileStem.Length && candFileStem.Length > 0 && existFileStem.Length > 0)
        {
            return candFileStem.Length < existFileStem.Length;
        }

        return string.CompareOrdinal(candId, existingId) < 0;
    }
}
