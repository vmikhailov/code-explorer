using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer5AnalysisParser
{
    private static Relationship CreateCallRel(Reference refItem, string targetId) =>
        Relationship.FromRelationship(new CallsRelationship(
            refItem.ScopeSymbolId,
            targetId,
            EvidenceProvenanceHelper.BuildCallSiteExtensions(refItem.Line, refItem.Column)));

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
        var callerFile = ExtractFilePathFromSymbolId(refItem.ScopeSymbolId);
        var normCallerFile = callerFile?.Replace('\\', '/');

        if (targetName.Contains('.'))
        {
            var dotIdx = targetName.LastIndexOf('.');
            var varName = targetName[..dotIdx];
            methodName = targetName[(dotIdx + 1)..];

            // Check if varName is a namespace import in callerFile (e.g. import * as text from './plural')
            if (normCallerFile != null && imports.FileImports.TryGetValue((normCallerFile, varName), out var nsImport) && nsImport.IsNamespace)
            {
                var nsCandidates = ResolveImportCandidateFiles(normCallerFile, nsImport.ImportPath, imports, ctx, symbols);
                foreach (var nsCand in nsCandidates)
                {
                    if (symbols.FunctionSymbolsByFile.TryGetValue((nsCand, methodName), out var nsTargetId))
                    {
                        referenceRelationships.Add(CreateCallRel(refItem, nsTargetId));
                        return;
                    }
                }
            }

            if (callerFile != null)
            {
                if (bindings.BindingsLookup.TryGetValue((callerFile, varName), out var candidates))
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
                else if (bindings.GlobalBindingsLookup.TryGetValue(varName, out var globalCandidates))
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
                else if (!string.IsNullOrEmpty(varName) && char.IsUpper(varName[0]))
                {
                    targetName = $"{varName}.{methodName}";
                    isExternalStaticClass = true;
                }
                else
                {
                    targetName = methodName;
                }
            }
            else
            {
                if (symbols.TypeSymbols.ContainsKey(varName))
                {
                    targetName = $"{varName}.{methodName}";
                }
                else if (!string.IsNullOrEmpty(varName) && char.IsUpper(varName[0]))
                {
                    targetName = $"{varName}.{methodName}";
                    isExternalStaticClass = true;
                }
                else
                {
                    targetName = methodName;
                }
            }
        }

        var callAdded = false;
        string? targetNodeId = null;

        // 1. Same file
        if (normCallerFile != null && ctx.FileScopedSymbols.TryGetValue((normCallerFile, OntologyConstants.NodeLabels.Function, targetName), out var localTargetId))
        {
            referenceRelationships.Add(CreateCallRel(refItem, localTargetId));
            callAdded = true;
            targetNodeId = localTargetId;
        }
        else if (normCallerFile != null && symbols.FunctionSymbolsByFile.TryGetValue((normCallerFile, targetName), out localTargetId))
        {
            referenceRelationships.Add(CreateCallRel(refItem, localTargetId));
            callAdded = true;
            targetNodeId = localTargetId;
        }
        // 2. Through explicit module imports (e.g. import { pluralize } from './plural' or from '@repro/text')
        else if (normCallerFile != null && (targetNodeId = ResolveFunctionCallThroughImports(normCallerFile, targetName, symbols, imports, ctx)) != null)
        {
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 3. Same project fallback (when function is defined uniquely in caller's project)
        else if (normCallerFile != null && (targetNodeId = ResolveSameProjectFunctionFallback(normCallerFile, targetName, symbols)) != null)
        {
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 4. Globally unambiguous function (only 1 function with this name across entire workspace, or 1 non-graphql function when paired with a .graphql operation)
        else if (symbols.FunctionsByName.TryGetValue(targetName, out var uniqueCandidates) &&
                 (uniqueCandidates.Count == 1 || SelectPreferredFunctionCandidate(uniqueCandidates) != null))
        {
            targetNodeId = uniqueCandidates.Count == 1 ? uniqueCandidates[0] : SelectPreferredFunctionCandidate(uniqueCandidates)!;
            referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            callAdded = true;
        }
        // 5. Procedure symbols fallback
        else if (symbols.ProcedureSymbols.TryGetValue(targetName, out var targetProcId))
        {
            referenceRelationships.Add(Relationship.FromRelationship(new CalledByRelationship(
                targetProcId,
                refItem.ScopeSymbolId,
                EvidenceProvenanceHelper.BuildCallSiteExtensions(refItem.Line, refItem.Column))));
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

        // Fallback for methodName
        if (!callAdded && !isExternalStaticClass && methodName != null)
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
            else if (normCallerFile != null && (targetNodeId = ResolveSameProjectFunctionFallback(normCallerFile, methodName, symbols)) != null)
            {
                referenceRelationships.Add(CreateCallRel(refItem, targetNodeId));
            }
            else if (symbols.FunctionsByName.TryGetValue(methodName, out var fallbackCandidates))
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
        if (callerProj != null && symbols.FilesByProject.TryGetValue(callerProj, out var projFiles))
        {
            var matchedIds = new List<string>();
            foreach (var pFile in projFiles)
            {
                if (symbols.FunctionSymbolsByFile.TryGetValue((pFile, targetName), out var id) && !matchedIds.Contains(id))
                {
                    matchedIds.Add(id);
                }
            }
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
                if (symbols.FunctionSymbolsByFile.TryGetValue((cand, importedEntry.ImportedName), out var funcId))
                {
                    return funcId;
                }

                // If default import, try targetName as importedName or file-named function
                if (importedEntry.IsDefault)
                {
                    if (symbols.FunctionSymbolsByFile.TryGetValue((cand, targetName), out var defFuncId))
                    {
                        return defFuncId;
                    }
                    var candFileName = Path.GetFileNameWithoutExtension(cand);
                    if (symbols.FunctionSymbolsByFile.TryGetValue((cand, candFileName), out var namedFuncId))
                    {
                        return namedFuncId;
                    }
                }

                // Follow barrel re-exports: if cand re-exports importedEntry.ImportedName
                if (imports.FileImports.TryGetValue((cand, importedEntry.ImportedName), out var reExportedEntry))
                {
                    var reCandidates = ResolveImportCandidateFiles(cand, reExportedEntry.ImportPath, imports, ctx, symbols);
                    foreach (var reCand in reCandidates)
                    {
                        if (symbols.FunctionSymbolsByFile.TryGetValue((reCand, reExportedEntry.ImportedName), out var reFuncId))
                        {
                            return reFuncId;
                        }
                    }
                }
            }

            // If candidate file was a package/project, search other files in that project for the imported symbol
            foreach (var cand in candidates)
            {
                var candProj = ConstantRegistry.ExtractProjectName(cand);
                if (candProj != null && symbols.FilesByProject.TryGetValue(candProj, out var projFiles))
                {
                    foreach (var pFile in projFiles)
                    {
                        if (symbols.FunctionSymbolsByFile.TryGetValue((pFile, importedEntry.ImportedName), out var projFuncId))
                        {
                            return projFuncId;
                        }
                    }
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
                    if (symbols.FunctionSymbolsByFile.TryGetValue((cand, targetName), out var funcId))
                    {
                        return funcId;
                    }
                }
            }
        }

        return null;
    }

    private static void ResolveTriggersReference(
        Reference refItem,
        SymbolLookups symbols,
        List<Relationship> referenceRelationships)
    {
        var targetId = symbols.FunctionSymbols.GetValueOrDefault(refItem.TargetName);
        if (targetId == null && refItem.TargetName.Contains('.'))
        {
            var shortName = refItem.TargetName[(refItem.TargetName.LastIndexOf('.') + 1)..];
            targetId = symbols.FunctionSymbols.GetValueOrDefault(shortName);
        }

        if (targetId != null)
        {
            if (!referenceRelationships.Any(r => r.From == refItem.ScopeSymbolId && r.To == targetId && r.Kind == OntologyConstants.Relationships.Triggers))
            {
                referenceRelationships.Add(
                    Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetId)));
            }
        }
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
        else if (topicName.StartWithIC("config."))
        {
            var prop = topicName["config.".Length..];
            if (ConstantRegistry.TryResolve(scopeProject, prop, out var resolved) && !string.IsNullOrEmpty(resolved))
            {
                topicName = resolved;
            }
            else
            {
                topicName = prop;
            }
        }
        else if (topicName.StartWithIC("this."))
        {
            var prop = topicName["this.".Length..];
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

        // Only constant-style names (ORDERS_TOPIC) may resolve workspace-wide. A bare parameter or
        // field such as `name` must stay within its project, or it picks up an unrelated constant.
        var isConstantStyle = System.Text.RegularExpressions.Regex.IsMatch(topicName, @"^[A-Z][A-Z0-9_]*$");

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
            var topicExt = new Dictionary<string, string>
            {
                ["is_internal"] = isInternal ? "true" : "false",
                ["scope"] = isInternal ? "internal" : "external",
                ["message_type"] = isInternal ? "internal" : "external",
                ["is_semantic_entity"] = isInternal ? "false" : "true"
            };
            var topicNode = new TopicNode(topicId, topicName, "", brokerType, topicExt);
            newTopicNodes.Add(topicNode);
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Topic, refItem.TargetName, topicId);
        }

        var relKind = refItem.Kind == OntologyConstants.Relationships.PublishesTo
            ? OntologyConstants.Relationships.PublishedBy
            : OntologyConstants.Relationships.SubscribedBy;
        var isPublish = refItem.Kind == OntologyConstants.Relationships.PublishesTo;

        var relProps = new Dictionary<string, object>
        {
            ["is_internal"] = isInternal ? "true" : "false",
            ["scope"] = isInternal ? "internal" : "external",
            ["category"] = "messaging",
            ["dependency_type"] = "messaging",
            ["broker_type"] = brokerType,
            [OntologyConstants.PropertyNames.Operation] = isPublish ? "PUBLISH" : "SUBSCRIBE",
            [OntologyConstants.PropertyNames.EvidenceKind] = isPublish
                ? OntologyConstants.EvidenceKinds.Publish
                : OntologyConstants.EvidenceKinds.Subscribe
        };
        if (!string.IsNullOrEmpty(scopeFilePath))
        {
            relProps[OntologyConstants.PropertyNames.FilePath] = scopeFilePath;
        }
        if (isInternal)
        {
            relProps["is_semantic"] = "false";
        }

        referenceRelationships.Add(new Relationship(topicId, refItem.ScopeSymbolId, relKind, new Dictionary<string, object>(relProps)));
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
