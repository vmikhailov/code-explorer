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
        var callerFile = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId)
                         ?? (!string.IsNullOrEmpty(refItem.ArgFlow) && (refItem.ArgFlow.Contains('/') || refItem.ArgFlow.Contains('\\')) ? refItem.ArgFlow.Replace('\\', '/') : null);
        var normCallerFile = callerFile?.Replace('\\', '/');
        var hasExplicitReceiver = false;

        if (targetName.Contains('.'))
        {
            var dotIdx = targetName.LastIndexOf('.');
            var varName = targetName[..dotIdx];
            methodName = targetName[(dotIdx + 1)..];

            var cleanVar = varName;
            if (cleanVar.StartsWith("this.", StringComparison.Ordinal) ||
                cleanVar.StartsWith("self.", StringComparison.Ordinal) ||
                cleanVar.StartsWith("base.", StringComparison.Ordinal))
            {
                cleanVar = cleanVar[(cleanVar.IndexOf('.') + 1)..];
            }

            var isThisReceiver = cleanVar is "this" or "self" or "base" || string.IsNullOrEmpty(cleanVar) || varName is "this" or "self";
            hasExplicitReceiver = !isThisReceiver;

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

            if (hasExplicitReceiver)
            {
                if (normCallerFile != null &&
                    (bindings.BindingsLookup.TryGetValue((normCallerFile, varName), out var candidates) ||
                     bindings.BindingsLookup.TryGetValue((normCallerFile, cleanVar), out candidates)))
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var candidate = candidates[i];
                        if (!string.IsNullOrEmpty(candidate.ScopeMarker) && !string.IsNullOrEmpty(candidate.ScopeId) &&
                            (refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal) ||
                             refItem.ScopeSymbolId.Contains($":{candidate.ScopeId}:", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase)))
                        {
                            targetTypeName = candidate.TypeName;
                            break;
                        }
                    }
                    targetTypeName ??= candidates[0].TypeName;
                }
                else if (bindings.BindingsLookup.TryGetValue(("", varName), out var emptyFileCandidates) ||
                         bindings.BindingsLookup.TryGetValue(("", cleanVar), out emptyFileCandidates))
                {
                    targetTypeName = emptyFileCandidates[0].TypeName;
                }
                else if (bindings.GlobalBindingsLookup.TryGetValue(varName, out var globalCandidates) ||
                         bindings.GlobalBindingsLookup.TryGetValue(cleanVar, out globalCandidates))
                {
                    foreach (var candidate in globalCandidates)
                    {
                        if (!string.IsNullOrEmpty(candidate.ScopeMarker) && !string.IsNullOrEmpty(candidate.ScopeId) &&
                            (refItem.ScopeSymbolId.Contains(candidate.ScopeMarker, StringComparison.Ordinal) ||
                             refItem.ScopeSymbolId.Contains($":{candidate.ScopeId}:", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"/{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase) ||
                             refItem.ScopeSymbolId.Contains($"\\{candidate.ScopeId}.", StringComparison.OrdinalIgnoreCase)))
                        {
                            targetTypeName = candidate.TypeName;
                            break;
                        }
                    }

                    targetTypeName ??= globalCandidates[0].TypeName;
                }

                if (targetTypeName != null)
                {
                    targetName = $"{targetTypeName}.{methodName}";
                }
                else if (symbols.TypeSymbols.ContainsKey(varName))
                {
                    targetName = $"{varName}.{methodName}";
                }
                else if (symbols.TypeSymbols.ContainsKey(cleanVar))
                {
                    targetName = $"{cleanVar}.{methodName}";
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
            else
            {
                targetName = methodName;
            }
        }

        var callAdded = false;
        string? targetNodeId = null;
        var isCallbackArg = string.Equals(refItem.ArgFlow, "callback_arg", StringComparison.Ordinal);

        // 1. Same file
        if (normCallerFile != null && ctx.FileScopedSymbols.TryGetValue((normCallerFile, OntologyConstants.NodeLabels.Function, targetName), out var localTargetId))
        {
            if (!isCallbackArg || localTargetId != refItem.ScopeSymbolId)
            {
                referenceRelationships.Add(CreateCallRel(refItem, localTargetId));
                callAdded = true;
                targetNodeId = localTargetId;
            }
        }
        else if (normCallerFile != null && symbols.FunctionSymbolsByFile.TryGetValue((normCallerFile, targetName), out localTargetId))
        {
            if (!isCallbackArg || localTargetId != refItem.ScopeSymbolId)
            {
                referenceRelationships.Add(CreateCallRel(refItem, localTargetId));
                callAdded = true;
                targetNodeId = localTargetId;
            }
        }
        // 2. Through explicit module imports (e.g. import { pluralize } from './plural' or from '@repro/text')
        else if (normCallerFile != null && (targetNodeId = ResolveFunctionCallThroughImports(normCallerFile, targetName, symbols, imports, ctx)) != null)
        {
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 3. Same project fallback (when function is defined uniquely in caller's project; skipped for callback_arg to prevent matching local variables to unrelated project functions)
        else if (!isCallbackArg && normCallerFile != null && (targetNodeId = ResolveSameProjectFunctionFallback(normCallerFile, targetName, symbols)) != null)
        {
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 4. Globally unambiguous function (only 1 function with this name across entire workspace, or 1 non-graphql function when paired with a .graphql operation)
        else if (!isCallbackArg &&
                 symbols.FunctionsByName.TryGetValue(targetName, out var uniqueCandidates) &&
                 (uniqueCandidates.Count == 1 ? uniqueCandidates[0] : SelectPreferredFunctionCandidate(uniqueCandidates)) is { } candidateId)
        {
            targetNodeId = candidateId;
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 5. Procedure symbols fallback
        else if (!isCallbackArg && symbols.ProcedureSymbols.TryGetValue(targetName, out var targetProcId))
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
                if (symbols.FunctionSymbols.TryGetValue($"{implClass}.{methodName}", out var implNodeId) && implNodeId != targetNodeId)
                {
                    referenceRelationships.Add(CreateCallRel(refItem, implNodeId));
                    callAdded = true;
                }
            }
        }

        // If receiver was bound to a known workspace TypeSymbol, check if the method is registered under short name in that type's file
        if (!callAdded && hasExplicitReceiver && targetTypeName != null && methodName != null &&
            symbols.TypeSymbols.TryGetValue(targetTypeName, out var resolvedTypeId))
        {
            var typeFile = ExtractFilePathFromSymbolId(resolvedTypeId);
            if (typeFile != null && symbols.FunctionSymbolsByFile.TryGetValue((typeFile, methodName), out var typeMethodId))
            {
                referenceRelationships.Add(CreateCallRel(refItem, typeMethodId));
                callAdded = true;
            }
        }

        // Fallback for methodName (only when there is no explicit receiver, e.g. this.foo() / self.foo() / base.foo())
        if (!callAdded && !isExternalStaticClass && !hasExplicitReceiver && methodName != null)
        {
            if (normCallerFile != null && ctx.FileScopedSymbols.TryGetValue((normCallerFile, OntologyConstants.NodeLabels.Function, methodName), out var localFallbackId))
            {
                referenceRelationships.Add(CreateCallRel(refItem, localFallbackId));
            }
            else if (normCallerFile != null && symbols.FunctionSymbolsByFile.TryGetValue((normCallerFile, methodName), out localFallbackId))
            {
                referenceRelationships.Add(CreateCallRel(refItem, localFallbackId));
            }
            else if (normCallerFile != null && (targetNodeId = ResolveFunctionCallThroughImports(normCallerFile, methodName, symbols, imports, ctx)) != null)
            {
                referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            }
            else if (!isCallbackArg && normCallerFile != null && (targetNodeId = ResolveSameProjectFunctionFallback(normCallerFile, methodName, symbols)) != null)
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

                    if (isSameProjectOrFile || (normCallerFile == null && targetFile == null))
                    {
                        referenceRelationships.Add(CreateCallRel(refItem, fallbackNodeId));
                        break;
                    }
                }
            }
        }
    }


    private static bool IsGraphQLDocumentSymbolId(string symbolId)
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

    private static string? ResolveSameProjectFunctionFallback(string callerFile, string targetName, SymbolLookups symbols)
    {
        var callerProj = ConstantRegistry.ExtractProjectName(callerFile);
        if (callerProj != null &&
            symbols.FunctionsByProject.TryGetValue(callerProj, out var projFns) &&
            projFns.TryGetValue(targetName, out var matchedIds))
        {
            return SelectPreferredFunctionCandidate(matchedIds);
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
        var shortName = targetName.Contains('.') ? targetName[(targetName.LastIndexOf('.') + 1)..] : targetName;
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

        // 2. Same project lookup
        if (targetId == null && callerProj != null && symbols.FunctionsByProject.TryGetValue(callerProj, out var projFns))
        {
            if (projFns.TryGetValue(targetName, out var ids) && ids.Count > 0)
            {
                targetId = ids[0];
            }
            else if (projFns.TryGetValue(shortName, out ids) && ids.Count > 0)
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
                    targetId = candidates.FirstOrDefault(c =>
                    {
                        var cFile = ExtractFilePathFromSymbolId(c);
                        return cFile != null && string.Equals(ConstantRegistry.ExtractProjectName(cFile), callerProj, StringComparison.OrdinalIgnoreCase);
                    });
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
            if (!string.IsNullOrEmpty(normalizedTopic))
            {
                topicName = normalizedTopic;
            }
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

        var relKind = refItem.Kind == OntologyConstants.Relationships.PublishesTo
            ? OntologyConstants.Relationships.PublishedBy
            : OntologyConstants.Relationships.SubscribedBy;
        var isPublish = refItem.Kind == OntologyConstants.Relationships.PublishesTo;

        referenceRelationships.Add(new Relationship(topicId, refItem.ScopeSymbolId, relKind)
        {
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

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint) => IsMatch(extService, entryPoint, null);

    private bool IsMatch(ExternalServiceNode extService, EntryPointNode entryPoint, ProjectNode? targetProj)
    {
        var prepExt = PrepareExternalService(extService, null);
        var prepEp = PrepareEntryPoint(entryPoint, targetProj);
        return IsMatch(prepExt, prepEp, (p, d) => DoesProjectMatchServiceDomain(p, d));
    }

    private static string NormalizeGcpTopicName(string raw) => WorkspaceConventions.NormalizeTopicName(raw);
}
